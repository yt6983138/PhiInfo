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
	/// <param name="streams">Streams to be merged. They will not be disposed by this method, but they will be read to the end.</param>
	/// <returns>A new constructed <see cref="MemoryStream"/>, with <paramref name="streams"/> contents copied to it, and position set to 0.</returns>
	public static MemoryStream MergeStreams(params IEnumerable<Stream> streams)
	{
		MemoryStream merged = new();
		foreach (Stream stream in streams)
		{
			stream.CopyTo(merged);
		}
		merged.Position = 0;
		return merged;
	}
	/// <summary>
	/// Create a complete level22 stream by merging all level22.split* files in the given zip.
	/// </summary>
	/// <param name="zip">The zip file. Usually the obb file.</param>
	/// <returns>A merged <see cref="MemoryStream"/> with complete level22 content, and position set to 0.</returns>
	/// <exception cref="FileNotFoundException">Thrown if level22.split files does not exist.</exception>
	public static MemoryStream BuildCompleteLevel22FromZip(ZipArchive zip)
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
		MemoryStream level22 = MergeStreams(streams);
		foreach (Stream stream in streams)
		{
			stream.Dispose();
		}

		return level22;
	}

	/// <summary>
	/// Create a complete level22 stream from obb. <see cref="BuildCompleteLevel22FromZip(ZipArchive)"/>.
	/// </summary>
	/// <param name="obb">The obb file.</param>
	/// <returns>Merged stream of level22.</returns>
	public static MemoryStream GetLevel22FromObb(Stream obb)
	{
		ZipArchive zip = new(obb, ZipArchiveMode.Read, true);
		return BuildCompleteLevel22FromZip(zip);
	}
	public static void GetInformationExtractionRequiredData(Stream apk,
		out Stream globalGameManagers,
		out Stream level0,
		out byte[] il2CppSo,
		out byte[] globalMetadata)
	{
		ZipArchive zip = new(apk, ZipArchiveMode.Read, true);
		il2CppSo = zip.GetEntryOrThrow("lib/arm64-v8a/libil2cpp.so").OpenAndReadAllBytes();
		globalMetadata = zip.GetEntryOrThrow("assets/bin/Data/Managed/Metadata/global-metadata.dat").OpenAndReadAllBytes();

		byte[] globalGameManagersData = zip.GetEntryOrThrow("assets/bin/Data/globalgamemanagers.assets").OpenAndReadAllBytes();
		byte[] level0Data = zip.GetEntryOrThrow("assets/bin/Data/level0").OpenAndReadAllBytes();

		globalGameManagers = new MemoryStream(globalGameManagersData);
		level0 = new MemoryStream(level0Data);
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
		return path =>
		{
			ZipArchiveEntry entry = zip.GetEntry($"assets/aa/Android/{path}") ??
				auxZip?.GetEntryOrThrow($"assets/aa/Android/{path}") ??
				throw new FileNotFoundException($"Required Unity asset missing from package and no auxiliary obb provided: {path}");

			using Stream zipStream = entry.Open();

			MemoryStream stream = new();
			zipStream.CopyTo(stream);
			stream.Position = 0;

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

	private static byte[] OpenAndReadAllBytes(this ZipArchiveEntry entry)
	{
		Stream stream = entry.Open();
		long length = entry.Length;
		byte[] data = new byte[length];
		stream.ReadExactly(data);
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
