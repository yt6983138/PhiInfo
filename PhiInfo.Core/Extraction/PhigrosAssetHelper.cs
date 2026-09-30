using Fmod5Sharp.CodecRebuilders;
using Fmod5Sharp.FmodTypes;
using Microsoft.IO;
using System.IO.Compression;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Helper class for preparing extraction data from raw apk or obb files, 
/// or convert FMOD sound bank to ogg files.
/// </summary>
public static class PhigrosAssetHelper
{
	internal static readonly RecyclableMemoryStreamManager _globalMemoryStreamManager = new();

	/// <summary>
	/// Merge streams into one stream.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <param name="streams">Streams to be merged. They will not be disposed by this method, but they will be read to the end.</param>
	/// <returns>A new constructed <see cref="MemoryStream"/>, with <paramref name="streams"/> contents copied to it, and position set to 0.</returns>
	public static async Task<RecyclableMemoryStream> MergeStreamsAsync(CancellationToken ct = default, params IEnumerable<Stream> streams)
	{
		RecyclableMemoryStream merged = _globalMemoryStreamManager.GetStream("MergedStream");
		foreach (Stream stream in streams)
		{
			// this cannot be optimized by using concurrent copy since the merged stream needs
			// to be written sequentially, because DeflateStream does not support length property
			// so we cannot know where to start writing
			await stream.CopyToAsync(merged, ct);
		}
		merged.Position = 0;
		return merged;
	}

	/// <summary>
	/// Create a complete sharedassets22 stream by merging all sharedassets22.assets.split* files from the provided packages.
	/// </summary>
	/// <param name="packages">Package streams (APK, OBB, split APKs) to search for sharedassets22 split files.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Merged stream of sharedassets22.</returns>
	/// <exception cref="FileNotFoundException">Thrown if sharedassets22.assets.split files are not found in any package.</exception>
	public static async Task<RecyclableMemoryStream> GetSharedAssets22FromPackagesAsync(CancellationToken ct = default, params Stream[] packages)
	{
		using MultiPackageFileLocator locator = new(packages);

		const string SplitPrefix = "assets/bin/Data/sharedassets22.assets.split";
		List<ZipArchiveEntry> entries = await locator.GetEntriesWithPrefixAsync(SplitPrefix, ct);

		if (entries.Count == 0)
			throw new FileNotFoundException("Required Unity assets missing from all packages: sharedassets22.assets.split*");

		List<(int index, ZipArchiveEntry entry)> sharedassets22Parts = [];
		foreach (ZipArchiveEntry entry in entries)
		{
			string suffix = entry.FullName[SplitPrefix.Length..];
			if (int.TryParse(suffix, out int index))
				sharedassets22Parts.Add((index, entry));
		}

		sharedassets22Parts.Sort((a, b) => a.index.CompareTo(b.index));

		IEnumerable<Stream> streams = sharedassets22Parts.Select(part => part.entry.Open());
		RecyclableMemoryStream sharedassets22 = await MergeStreamsAsync(ct, streams);
		foreach (ValueTask task in streams.Select(x => x.DisposeAsync()))
		{
			await task;
		}

		return sharedassets22;
	}

	/// <summary>
	/// Extract level22 from the provided packages.
	/// </summary>
	/// <param name="packages">Package streams (APK, OBB, split APKs) to search for level22 file.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A level22 stream.</returns>
	/// <exception cref="FileNotFoundException">Thrown if level22 is not found in any package.</exception>
	public static async Task<RecyclableMemoryStream> GetLevel22FromPackagesAsync(CancellationToken ct = default, params Stream[] packages)
	{
		using MultiPackageFileLocator locator = new(packages);
		ZipArchiveEntry entry = await locator.GetEntryOrThrowAsync("assets/bin/Data/level22", ct);
		using Stream level22Stream = entry.Open();
		RecyclableMemoryStream level22 = _globalMemoryStreamManager.GetStream(entry.FullName);
		await level22Stream.CopyToAsync(level22, ct);
		level22.Position = 0;
		return level22;
	}

	/// <summary>
	/// Extracts required data for information extraction from the provided packages.
	/// </summary>
	/// <param name="packages">Package streams (APK, OBB, split APKs) to search for required assets.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A tuple containing streams for <c>globalgamemanagers.assets</c> and <c>level0</c>, 
	/// and byte arrays for <c>libil2cpp.so</c> and <c>global-metadata.dat</c>.</returns>
	/// <exception cref="FileNotFoundException">Thrown if any of the required assets are missing from all packages.</exception>
	public static async Task<(Stream GlobalGameManagers, Stream Level0, byte[] Il2CppSo, byte[] GlobalMetadata)> GetInformationExtractionRequiredDataAsync(
		CancellationToken ct = default, params Stream[] packages)
	{
		using MultiPackageFileLocator locator = new(packages);

		byte[] il2CppSo = await (await locator.GetEntryOrThrowAsync("lib/arm64-v8a/libil2cpp.so", ct)).OpenAndReadAllBytesAsync(ct);
		byte[] globalMetadata = await (await locator.GetEntryOrThrowAsync("assets/bin/Data/Managed/Metadata/global-metadata.dat", ct)).OpenAndReadAllBytesAsync(ct);

		ZipArchiveEntry globalGameManagersEntry = await locator.GetEntryOrThrowAsync("assets/bin/Data/globalgamemanagers.assets", ct);
		ZipArchiveEntry level0Entry = await locator.GetEntryOrThrowAsync("assets/bin/Data/level0", ct);

		using Stream globalGameManagersData = globalGameManagersEntry.Open();
		using Stream level0Data = level0Entry.Open();

		RecyclableMemoryStream globalGameManagers = _globalMemoryStreamManager.GetStream("GlobalGameManagers");
		RecyclableMemoryStream level0 = _globalMemoryStreamManager.GetStream("Level0");
		await globalGameManagersData.CopyToAsync(globalGameManagers, ct);
		await level0Data.CopyToAsync(level0, ct);
		globalGameManagers.Position = 0;
		level0.Position = 0;

		return (globalGameManagers, level0, il2CppSo, globalMetadata);
	}

	/// <summary>
	/// Get catalog.json stream from packages. This file is a map from addressable path to bundle hash.
	/// </summary>
	/// <param name="packages">Package streams (APK, OBB, split APKs) to search for catalog.json.</param>
	/// <returns>A non-seekable catalog.json stream.</returns>
	/// <exception cref="FileNotFoundException">Thrown if catalog.json is not found in any package.</exception>
	public static async Task<Stream> GetCatalogStreamFromPackagesAsync(params Stream[] packages)
	{
		MultiPackageFileLocator locator = new(packages);
		ZipArchiveEntry entry = await locator.GetEntryOrThrowAsync("assets/aa/catalog.json");
		return new CustomDisposeProxyStream(entry.Open(), locator.Dispose);
	}

	/// <summary>
	/// Creates a bundle stream factory from packages, which maps bundle name to bundle stream. 
	/// The returned factory will search through all packages in order to find the requested bundle.
	/// </summary>
	/// <param name="packages">Package streams (APK, OBB, split APKs) to search for bundle files.</param>
	/// <returns>A bundle stream factory and a disposer action.</returns>
	/// <exception cref="FileNotFoundException">If the requested bundle is not found in any package.</exception>
	public static (BundleStreamFactory Factory, Action Disposer) CreateBundleFactoryFromPackages(params Stream[] packages)
	{
		MultiPackageFileLocator locator = new(packages);
		SemaphoreSlim @lock = new(1, 1);

		return (Factory, Dispose);

		void Dispose()
		{
			locator.Dispose();
			@lock.Dispose();
		}
		async Task<Stream> Factory(string path, CancellationToken ct = default)
		{
			await @lock.WaitAsync(ct);
			try
			{
				ZipArchiveEntry? entry = await locator.TryGetEntryAsync($"assets/aa/Android/{path}", ct);
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
	/// Convert a <see cref="FmodSoundBank"/> to ogg file bytes. The returned ogg file will not be 
	/// byte-to-byte same as the original one in the package.
	/// </summary>
	/// <param name="bank">The audio file (.wav extension) extracted from bundle</param>
	/// <returns>Encoded ogg bytes.</returns>
	public static byte[] ToOggBytes(this FmodSoundBank bank)
	{
		return FmodVorbisRebuilder.RebuildOggFile(bank.Samples[0]);
	}

	private static async Task<byte[]> OpenAndReadAllBytesAsync(this ZipArchiveEntry entry, CancellationToken ct = default)
	{
		Stream stream = entry.Open();
		long length = entry.Length;
		byte[] data = new byte[length];
		await stream.ReadExactlyAsync(data, ct);
		return data;
	}
}
