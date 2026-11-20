using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using Microsoft.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Manages multiple package archives (APK, OBB, split APKs) and provides unified file lookup.
/// Handles proper disposal of all underlying ZipArchive instances.
/// Searches through packages in the order provided, returning the first match found.
/// </summary>
public partial class MultiPackageFileLocator : IDisposable
{
	private const string DataDirectory = "assets/bin/Data/";
	private const string DataUnity3dPath = DataDirectory + "data.unity3d";
	private const string GlobalGameManagersName = "globalgamemanagers.assets";

	internal static readonly RecyclableMemoryStreamManager _globalMemoryStreamManager = new();

	private readonly List<ZipArchive> _archives = [];
	private readonly SemaphoreSlim _lock = new(1, 1);
	private bool _disposed;

	/// <summary>
	/// Creates a new instance that manages multiple package streams.
	/// </summary>
	/// <param name="packages">Package streams (APK, OBB, split APKs). Will be opened as ZipArchives. The streams will not be disposed by this class.</param>
	/// <exception cref="ArgumentException">Thrown if no packages are provided.</exception>
	public MultiPackageFileLocator(params Stream[] packages)
	{
		if (packages.Length == 0)
			throw new ArgumentException("At least one package must be provided.", nameof(packages));

		foreach (Stream package in packages)
		{
			this._archives.Add(new(package, ZipArchiveMode.Read, leaveOpen: true));
		}
	}

	/// <summary>
	/// Searches for a file entry across all packages. Returns the first match found.
	/// </summary>
	/// <param name="path">The path to the file within the package (e.g., "assets/aa/catalog.json").</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The ZipArchiveEntry if found, otherwise null.</returns>
	public async Task<ZipArchiveEntry?> TryGetEntryAsync(string path, CancellationToken ct = default)
	{
		await this._lock.WaitAsync(ct);
		try
		{
			foreach (ZipArchive archive in this._archives)
			{
				ZipArchiveEntry? entry = archive.GetEntry(path);
				if (entry is not null)
					return entry;
			}
			return null;
		}
		finally
		{
			this._lock.Release();
		}
	}

	/// <summary>
	/// Searches for a file entry across all packages. Throws if not found.
	/// </summary>
	/// <param name="path">The path to the file within the package (e.g., "assets/aa/catalog.json").</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The ZipArchiveEntry.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the file is not found in any package.</exception>
	public async Task<ZipArchiveEntry> GetEntryOrThrowAsync(string path, CancellationToken ct = default)
	{
		ZipArchiveEntry? entry = await this.TryGetEntryAsync(path, ct);
		if (entry is null)
		{
			throw new FileNotFoundException(
				$"Required Unity asset missing from all {this._archives.Count} package(s): {path}");
		}

		return entry;
	}

	/// <summary>
	/// Gets all entries matching a specific prefix across all packages.
	/// Useful for finding split files like "sharedassets22.assets.split*".
	/// </summary>
	/// <param name="prefix">The prefix to match (e.g., "assets/bin/Data/sharedassets22.assets.split").</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A list of matching entries from all packages.</returns>
	public async Task<List<ZipArchiveEntry>> GetEntriesWithPrefixAsync(string prefix, CancellationToken ct = default)
	{
		await this._lock.WaitAsync(ct);
		try
		{
			List<ZipArchiveEntry> matches = [];
			foreach (ZipArchive archive in this._archives)
			{
				foreach (ZipArchiveEntry entry in archive.Entries)
				{
					if (entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
						matches.Add(entry);
				}
			}
			return matches;
		}
		finally
		{
			this._lock.Release();
		}
	}

	/// <summary>
	/// Extracts and initializes the data.unity3d bundle with AssetsManager and Cpp2IL support.
	/// </summary>
	/// <param name="classDataTPK">Class data database file. Can be obtained 
	/// <a href="https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/uncompressed_file.zip">here</a>.</param>
	/// <returns>A tuple containing the initialized AssetsManager, BundleFileInstance, and a disposer action for cleanup.</returns>
	public async Task<(AssetsManager Manager, BundleFileInstance Bundle, Action Disposer)> ExtractDataUnity3DFile(Stream classDataTPK)
	{
		AssetsManager manager = new();
		manager.LoadClassPackage(classDataTPK);

		// here we load the unity3d file first to get the unity version, also return later
		ZipArchiveEntry entry = await this.GetEntryOrThrowAsync(DataUnity3dPath);
		using Stream entryStream = await entry.OpenAsync();
		RecyclableMemoryStream stream = _globalMemoryStreamManager.GetStream(entry.FullName);
		await entryStream.CopyToAsync(stream);
		stream.Seek(0, SeekOrigin.Begin);

		BundleFileInstance bundle = manager.LoadBundleFile(stream, DataUnity3dPath);

		AssetsFileInstance dummy = manager.LoadAssetsFileFromBundle(bundle, 0);
		string version = dummy.file.Metadata.UnityVersion;
		manager.LoadClassDatabaseFromPackage(version);
		manager.UnloadAssetsFile(dummy);

		await this.InitializeMonoTempGeneratorAsync(manager, version);

		return (manager, bundle, stream.Dispose);
	}

	private async Task InitializeMonoTempGeneratorAsync(AssetsManager manager, string unityVersion)
	{
		(byte[]? il2cpp, byte[]? metadata) = await this.GetAssemblyDataAsync();
		Cpp2IlTempGenerator tempGen = new(metadata, il2cpp);
		tempGen.SetUnityVersion(new UnityVersion(unityVersion));
		tempGen.InitializeCpp2IL();
		manager.MonoTempGenerator = tempGen;
	}

	/// <summary>
	/// Loads the game's Unity serialized files and initializes AssetsManager with Cpp2IL support, 
	/// automatically detecting the layout used by the package:
	/// <list type="bullet">
	/// <item><description>Packed: everything is inside <c>assets/bin/Data/data.unity3d</c>.</description></item>
	/// <item><description>Loose: <c>globalgamemanagers.assets</c>, <c>level0</c>, <c>level1</c>... and 
	/// <c>sharedassetsN.assets</c> (possibly split into <c>.splitN</c> chunks) directly inside 
	/// <c>assets/bin/Data</c>.</description></item>
	/// </list>
	/// </summary>
	/// <param name="classDataTPK">Class data database file. Can be obtained 
	/// <a href="https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/uncompressed_file.zip">here</a>.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The context to be passed to <see cref="InfoExtractor"/>.</returns>
	/// <exception cref="FileNotFoundException">Thrown if neither layout is found in any package.</exception>
	public async Task<UnityAssetContext> LoadUnityAssetsAsync(Stream classDataTPK, CancellationToken ct = default)
	{
		if (await this.TryGetEntryAsync(DataUnity3dPath, ct) is not null)
		{
			(AssetsManager manager, BundleFileInstance bundle, Action disposer) = await this.ExtractDataUnity3DFile(classDataTPK);
			return UnityAssetContext.FromBundle(manager, bundle, disposer);
		}

		return await this.LoadLooseUnityAssetsAsync(classDataTPK, ct);
	}

	// matches level0, sharedassets22.assets, globalgamemanagers.assets, with an optional .splitN suffix
	[GeneratedRegex(@"^(?<name>level\d+|sharedassets\d+\.assets|globalgamemanagers\.assets)(?:\.split(?<index>\d+))?$", RegexOptions.CultureInvariant)]
	private static partial Regex LooseSerializedFileRegex();

	private async Task<UnityAssetContext> LoadLooseUnityAssetsAsync(Stream classDataTPK, CancellationToken ct)
	{
		// logical file name -> (split index, entry); a non-split file uses index -1
		Dictionary<string, List<(int Index, ZipArchiveEntry Entry)>> files = new(StringComparer.Ordinal);
		foreach (ZipArchiveEntry entry in await this.GetEntriesWithPrefixAsync(DataDirectory, ct))
		{
			string relative = entry.FullName[DataDirectory.Length..];
			Match match = LooseSerializedFileRegex().Match(relative);
			if (!match.Success) continue;

			string name = match.Groups["name"].Value;
			int index = match.Groups["index"].Success ? int.Parse(match.Groups["index"].Value) : -1;

			if (!files.TryGetValue(name, out List<(int, ZipArchiveEntry)>? parts))
				files[name] = parts = [];
			parts.Add((index, entry));
		}

		if (!files.ContainsKey(GlobalGameManagersName))
		{
			throw new FileNotFoundException(
				$"Neither {DataUnity3dPath} nor loose Unity files (e.g. {DataDirectory}{GlobalGameManagersName}) " +
				$"were found in any of the {this._archives.Count} package(s).");
		}

		AssetsManager manager = new();
		manager.LoadClassPackage(classDataTPK);

		AssetsFileInstance globalGameManagers = LoadLooseFile(manager, GlobalGameManagersName, files[GlobalGameManagersName]);
		string version = globalGameManagers.file.Metadata.UnityVersion;
		manager.LoadClassDatabaseFromPackage(version);

		await this.InitializeMonoTempGeneratorAsync(manager, version);

		// globalgamemanagers first, then levels and shared assets in natural order, loaded one by one to keep memory low
		IEnumerable<AssetsFileInstance> Enumerate()
		{
			yield return globalGameManagers;

			foreach (string name in files.Keys
				.Where(x => x != GlobalGameManagersName)
				.OrderBy(x => x.StartsWith("level", StringComparison.Ordinal) ? 0 : 1)
				.ThenBy(x => int.Parse(DigitsRegex().Match(x).Value)))
			{
				yield return LoadLooseFile(manager, name, files[name]);
			}
		}

		return new UnityAssetContext(manager, globalGameManagers, Enumerate(), true, () => { });
	}

	[GeneratedRegex(@"\d+")]
	private static partial Regex DigitsRegex();

	private static AssetsFileInstance LoadLooseFile(AssetsManager manager, string name, List<(int Index, ZipArchiveEntry Entry)> parts)
	{
		RecyclableMemoryStream stream = _globalMemoryStreamManager.GetStream(name);
		try
		{
			// split chunks (.split0, .split1...) must be concatenated in index order
			foreach ((int _, ZipArchiveEntry entry) in parts.OrderBy(x => x.Index))
			{
				using Stream entryStream = entry.Open();
				entryStream.CopyTo(stream);
			}
			stream.Seek(0, SeekOrigin.Begin);

			// no loadDeps: dependencies are resolved from disk, and we only need the MonoBehaviours here
			return manager.LoadAssetsFile(stream, DataDirectory + name, false);
		}
		catch
		{
			stream.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Gets a stream to the catalog.json file from the packages.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A stream to the catalog.json file.</returns>
	/// <exception cref="FileNotFoundException">Thrown if catalog.json is not found in any package.</exception>
	public async Task<Stream> GetCatalogStream(CancellationToken ct = default)
	{
		ZipArchiveEntry entry = await this.GetEntryOrThrowAsync("assets/aa/catalog.json", ct);
		return new CustomDisposeProxyStream(entry.Open(), this.Dispose);
	}

	/// <summary>
	/// Creates a bundle stream factory from packages, which maps bundle name to bundle stream. 
	/// The returned factory will search through all packages in order to find the requested bundle.
	/// </summary>
	/// <returns>A bundle stream factory and a disposer action for cleanup.</returns>
	/// <exception cref="FileNotFoundException">If the requested bundle is not found in any package.</exception>
	public (BundleStreamFactory Factory, Action Disposer) CreateBundleFactory()
	{
		SemaphoreSlim @lock = new(1, 1);

		return (Factory, Dispose);

		void Dispose()
		{
			this.Dispose();
			@lock.Dispose();
		}
		async Task<Stream> Factory(string path, CancellationToken ct = default)
		{
			await @lock.WaitAsync(ct);
			try
			{
				ZipArchiveEntry? entry = await this.TryGetEntryAsync($"assets/aa/Android/{path}", ct);
				if (entry is null)
					throw new FileNotFoundException($"Required Unity asset missing from all packages: {path}");

				using Stream zipStream = entry.Open();

				RecyclableMemoryStream stream = _globalMemoryStreamManager.GetStream(entry.FullName);
				await zipStream.CopyToAsync(stream, ct);
				stream.Position = 0;

				return stream;
			}
			finally
			{
				@lock.Release();
			}
		}
	}

	/// <summary>
	/// Extracts required data for information extraction from the provided packages.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A tuple containing streams for <c>globalgamemanagers.assets</c> and <c>level0</c>, 
	/// and byte arrays for <c>libil2cpp.so</c> and <c>global-metadata.dat</c>.</returns>
	/// <exception cref="FileNotFoundException">Thrown if any of the required assets are missing from all packages.</exception>
	public async Task<(byte[] Il2CppSo, byte[] GlobalMetadata)> GetAssemblyDataAsync(
		CancellationToken ct = default)
	{
		byte[] il2CppSo = await (await this.GetEntryOrThrowAsync("lib/arm64-v8a/libil2cpp.so", ct)).OpenAndReadAllBytesAsync(ct);
		byte[] globalMetadata = await (await this.GetEntryOrThrowAsync("assets/bin/Data/Managed/Metadata/global-metadata.dat", ct)).OpenAndReadAllBytesAsync(ct);

		return (il2CppSo, globalMetadata);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref this._disposed, true)) return;

		GC.SuppressFinalize(this);

		foreach (ZipArchive archive in this._archives)
		{
			archive.Dispose();
		}
		this._lock.Dispose();
	}
}

file static class Extension
{
	internal static async Task<byte[]> OpenAndReadAllBytesAsync(this ZipArchiveEntry entry, CancellationToken ct = default)
	{
		Stream stream = entry.Open();
		long length = entry.Length;
		byte[] data = new byte[length];
		await stream.ReadExactlyAsync(data, ct);
		return data;
	}
}
