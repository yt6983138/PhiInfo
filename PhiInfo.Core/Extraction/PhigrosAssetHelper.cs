using Fmod5Sharp.CodecRebuilders;
using Fmod5Sharp.FmodTypes;
using System.IO.Compression;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Helper class for preparing extraction data from raw apk or obb files, 
/// or convert FMOD sound bank to ogg files.
/// </summary>
public static class PhigrosAssetHelper
{
	/// <summary>
	/// Merge streams into one stream.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <param name="streams">Streams to be merged. They will not be disposed by this method, but they will be read to the end.</param>
	/// <returns>A new constructed <see cref="MemoryStream"/>, with <paramref name="streams"/> contents copied to it, and position set to 0.</returns>
	public static async Task<MemoryStream> MergeStreamsAsync(CancellationToken ct = default, params IEnumerable<Stream> streams)
	{
		MemoryStream merged = new();
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
	/// Create a complete level22 stream by merging all level22.split* files in the given zip.
	/// </summary>
	/// <param name="zip">The zip file. Usually the obb file.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A merged <see cref="MemoryStream"/> with complete level22 content, and position set to 0.</returns>
	/// <exception cref="FileNotFoundException">Thrown if level22.split files does not exist.</exception>
	public static async Task<MemoryStream> BuildCompleteLevel22FromZipAsync(ZipArchive zip, CancellationToken ct = default)
	{
		const string SplitPrefix = "assets/bin/Data/level22.split";

		List<(int index, string name)> level22Parts = [];
		foreach (ZipArchiveEntry entry in zip.Entries)
		{
			if (entry.FullName.StartsWith(SplitPrefix, StringComparison.Ordinal))
			{
				string suffix = entry.FullName[SplitPrefix.Length..];
				if (int.TryParse(suffix, out int index))
					level22Parts.Add((index, entry.FullName));
			}
		}

		if (level22Parts.Count == 0)
			throw new FileNotFoundException("Required Unity assets missing from APK");

		level22Parts.Sort((a, b) => a.index.CompareTo(b.index));

		IEnumerable<Stream> streams = level22Parts.Select(part => zip.GetEntryOrThrow(part.name).Open());
		MemoryStream level22 = await MergeStreamsAsync(ct, streams);
		foreach (ValueTask task in streams.Select(x => x.DisposeAsync()))
		{
			await task;
		}

		return level22;
	}

	/// <summary>
	/// Create a complete level22 stream from obb. <see cref="BuildCompleteLevel22FromZipAsync(ZipArchive, CancellationToken)"/>.
	/// </summary>
	/// <param name="obb">The obb file.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Merged stream of level22.</returns>
	public static async Task<MemoryStream> GetLevel22FromObbAsync(Stream obb, CancellationToken ct = default)
	{
		ZipArchive zip = new(obb, ZipArchiveMode.Read, true);
		return await BuildCompleteLevel22FromZipAsync(zip, ct);
	}
	/// <summary>
	/// Extracts required data for information extraction from the provided APK stream.
	/// </summary>
	/// <param name="apk">The APK file stream.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A tuple containing streams for <c>globalgamemanagers.assets</c> and <c>level0</c>, 
	/// and byte arrays for <c>libil2cpp.so</c> and <c>global-metadata.dat</c>.</returns>
	/// <exception cref="FileNotFoundException">Thrown if any of the required assets are missing from the APK.</exception>
	public static async Task<(Stream GlobalGameManagers, Stream Level0, byte[] Il2CppSo, byte[] GlobalMetadata)> GetInformationExtractionRequiredDataAsync(
		Stream apk, CancellationToken ct = default)
	{
		ZipArchive zip = new(apk, ZipArchiveMode.Read, true);
		byte[] il2CppSo = await zip.GetEntryOrThrow("lib/arm64-v8a/libil2cpp.so").OpenAndReadAllBytesAsync(ct);
		byte[] globalMetadata = await zip.GetEntryOrThrow("assets/bin/Data/Managed/Metadata/global-metadata.dat").OpenAndReadAllBytesAsync(ct);

		byte[] globalGameManagersData = await zip.GetEntryOrThrow("assets/bin/Data/globalgamemanagers.assets").OpenAndReadAllBytesAsync(ct);
		byte[] level0Data = await zip.GetEntryOrThrow("assets/bin/Data/level0").OpenAndReadAllBytesAsync(ct);

		MemoryStream globalGameManagers = new(globalGameManagersData);
		MemoryStream level0 = new(level0Data);
		return (globalGameManagers, level0, il2CppSo, globalMetadata);
	}

	/// <summary>
	/// Get catalog.json stream from obb. This file is map from addressable path to bundle hash.
	/// </summary>
	/// <param name="obb">The obb file.</param>
	/// <returns>A non-seekable catalog.json stream.</returns>
	public static Stream GetCatalogStreamFromObb(Stream obb)
	{
		ZipArchive zip = new(obb, ZipArchiveMode.Read, true);
		return zip.GetEntryOrThrow("assets/aa/catalog.json").Open();
	}

	/// <summary>
	/// Creates a bundle stream factory from obb, which bundle name to obb path. The returned factory 
	/// will first try to get the bundle from the main obb, and if not found, it will try to get it 
	/// from the auxiliary (patch) obb if provided.
	/// </summary>
	/// <param name="obb">The main obb file.</param>
	/// <param name="auxObb">The auxiliary obb file.</param>
	/// <returns>A bundle stream factory.</returns>
	/// <exception cref="FileNotFoundException">If the obb provided does not have requested bundle.</exception>
	public static BundleStreamFactory CreateBundleFactoryFromObb(Stream obb, Stream? auxObb = null)
	{
		ZipArchive zip = new(obb, ZipArchiveMode.Read, true);
		ZipArchive? auxZip = auxObb is null ? null : new(auxObb, ZipArchiveMode.Read, true);
		SemaphoreSlim @lock = new(1, 1);
		return async (path, ct) =>
		{
			await @lock.WaitAsync(ct);
			ZipArchiveEntry entry = zip.GetEntry($"assets/aa/Android/{path}") ??
				auxZip?.GetEntryOrThrow($"assets/aa/Android/{path}") ??
				throw new FileNotFoundException($"Required Unity asset missing from package and no auxiliary obb provided: {path}");

			using Stream zipStream = entry.Open();

			MemoryStream stream = new();
			await zipStream.CopyToAsync(stream, ct);
			stream.Position = 0;
			@lock.Release();

			return stream;
		};
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
	private static ZipArchiveEntry GetEntryOrThrow(this ZipArchive zip, string name)
	{
		ZipArchiveEntry? entry = zip.GetEntry(name);
		if (entry is null)
			throw new FileNotFoundException($"Required Unity asset missing from package: {name}");
		return entry;
	}
}
