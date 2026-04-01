using AssetsTools.NET;
using AssetsTools.NET.Extra;
using PhiInfo.Core.Type;

namespace PhiInfo.Core;

public class PhiInfoAsset(CatalogParser catalogParser, Func<string, Stream> getBundleStreamFunc)
{
	private static byte[] ReadRangeAsBytes(Stream baseStream, long offset, int size)
	{
		byte[] buffer = new byte[size];

		long oldPos = baseStream.Position;
		try
		{
			baseStream.Seek(offset, SeekOrigin.Begin);

			int readTotal = 0;
			while (readTotal < size)
			{
				int read = baseStream.Read(
					buffer,
					readTotal,
					size - readTotal
				);

				if (read == 0)
					throw new EndOfStreamException();

				readTotal += read;
			}
		}
		finally
		{
			baseStream.Position = oldPos;
		}

		return buffer;
	}

	private T ProcessAssetBundle<T>(string path, Func<AssetBundleFile, AssetsFile, T> processor)
	{
		Stream file = this.GetBundle(path);
		AssetsFileReader reader = new(file);
		AssetBundleFile bun = new();
		bun.Read(reader);
		if (bun.DataIsCompressed)
		{
			bun = BundleHelper.UnpackBundle(bun);
		}

		bun.GetFileRange(0, out long offset, out long size);
		SegmentStream stream = new(bun.DataReader.BaseStream, offset, size);
		AssetsFile infoFile = new();
		infoFile.Read(new AssetsFileReader(stream));

		try
		{
			return processor(bun, infoFile);
		}
		finally
		{
			bun.Close();
			infoFile.Close();
		}
	}

	public Image GetImageRaw(string path)
	{
		return this.ProcessAssetBundle(path, (bun, infoFile) =>
		{
			foreach (AssetFileInfo? info in infoFile.AssetInfos)
			{
				if (info.TypeId == (int)AssetClassID.Texture2D)
				{
					AssetTypeValueField baseField = PhiInfo.GetBaseField(infoFile, info);
					uint height = baseField["m_Height"].AsUInt;
					uint width = baseField["m_Width"].AsUInt;
					uint format = baseField["m_TextureFormat"].AsUInt;
					long dataOffset = baseField["m_StreamData"]["offset"].AsLong;
					long dataSize = baseField["m_StreamData"]["size"].AsLong;
					bun.GetFileRange(1, out long dataFileOffset, out _);
					byte[] data = ReadRangeAsBytes(bun.DataReader.BaseStream, dataFileOffset + dataOffset,
						(int)dataSize);
					Image image = new(format, width, height, data);
					return image;
				}
			}

			throw new Exception("No Texture2D found in the asset bundle.");
		});
	}

	public Music GetMusicRaw(string path)
	{
		return this.ProcessAssetBundle(path, (bun, infoFile) =>
		{
			foreach (AssetFileInfo? info in infoFile.AssetInfos)
			{
				if (info.TypeId == (int)AssetClassID.AudioClip)
				{
					AssetTypeValueField baseField = PhiInfo.GetBaseField(infoFile, info);
					long dataOffset = baseField["m_Resource"]["m_Offset"].AsLong;
					long dataSize = baseField["m_Resource"]["m_Size"].AsLong;
					float length = baseField["m_Length"].AsFloat;
					bun.GetFileRange(1, out long dataFileOffset, out _);
					byte[] data = ReadRangeAsBytes(bun.DataReader.BaseStream, dataFileOffset + dataOffset,
						(int)dataSize);
					return new Music(length, data);
				}
			}

			throw new Exception("No AudioClip found in the asset bundle.");
		});
	}

	public Text GetText(string path)
	{
		return this.ProcessAssetBundle(path, (_, infoFile) =>
		{
			foreach (AssetFileInfo? info in infoFile.AssetInfos)
			{
				if (info.TypeId == (int)AssetClassID.TextAsset)
				{
					AssetTypeValueField baseField = PhiInfo.GetBaseField(infoFile, info);
					string text = baseField["m_Script"].AsString;
					return new Text(text);
				}
			}

			throw new Exception("No TextAsset found in the asset bundle.");
		});
	}

	private Stream GetBundle(string path)
	{
		CatalogValue? bundlePath = catalogParser.Get(path);
		if (bundlePath == null)
			throw new Exception($"Asset {path} not found in catalog.");
		if (bundlePath.Value.ResolvedKey == null)
			throw new Exception($"Asset {path} has no resolved bundle path.");
		if (bundlePath.Value.ResolvedKey.Value.StringValue == null)
			throw new Exception($"Asset {path} has invalid resolved bundle path.");

		return getBundleStreamFunc(bundlePath.Value.ResolvedKey.Value.StringValue);
	}

	public List<string> List()
	{
		return catalogParser.GetAll()
			.Where(e => e.Value.ResolvedKey != null && e.Key.StringValue is not null)
			.Select(v => v.Key.StringValue!)
			.ToList();
	}
}