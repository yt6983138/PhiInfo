using System.IO.Compression;

namespace PhiInfo.Core;
public static class PhigrosAssetHelper
{
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

	public static MemoryStream GetLevel22FromObb(Stream obb)
	{
		ZipArchive zip = new(obb, ZipArchiveMode.Read, true);
		return BuildCompleteLevel22FromZip(zip);
	}
	public static void GetRawExtractionRequiredData(Stream apk,
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
			throw new FileNotFoundException($"Required Unity asset missing from APK: {name}");
		return entry;
	}
}
