using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using Microsoft.IO;
using System.IO.Compression;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Manages multiple package archives (APK, OBB, split APKs) and provides unified file lookup.
/// Handles proper disposal of all underlying ZipArchive instances.
/// Searches through packages in the order provided, returning the first match found.
/// </summary>
public class MultiPackageFileLocator : IDisposable
{
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
		const string Path = "assets/bin/Data/data.unity3d";

		AssetsManager manager = new();
		manager.LoadClassPackage(classDataTPK);

		// here we load the unity3d file first to get the unity version, also return later
		ZipArchiveEntry entry = await this.GetEntryOrThrowAsync(Path);
		using Stream entryStream = await entry.OpenAsync();
		RecyclableMemoryStream stream = _globalMemoryStreamManager.GetStream(entry.FullName);
		await entryStream.CopyToAsync(stream);
		stream.Seek(0, SeekOrigin.Begin);

		BundleFileInstance bundle = manager.LoadBundleFile(stream, Path);

		AssetsFileInstance dummy = manager.LoadAssetsFileFromBundle(bundle, 0);
		string version = dummy.file.Metadata.UnityVersion;
		manager.LoadClassDatabaseFromPackage(version);
		manager.UnloadAssetsFile(dummy);

		(byte[]? il2cpp, byte[]? metadata) = await this.GetAssemblyDataAsync();
		Cpp2IlTempGenerator tempGen = new(metadata, il2cpp);
		tempGen.SetUnityVersion(new UnityVersion(version));
		tempGen.InitializeCpp2IL();
		manager.MonoTempGenerator = tempGen;

		return (manager, bundle, stream.Dispose);
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
