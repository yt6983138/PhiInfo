using AssetsTools.NET;
using AssetsTools.NET.Extra;
using PhiInfo.Core.Models.Catalog;
using PhiInfo.Core.Models.Raw;

namespace PhiInfo.Core;

public delegate Stream BundleStreamFactory(string path);
public class AddressableBundleExtractor
{
	private record struct MappedAssetBundle(AssetBundleFile BundleFile, AssetsFile InfoAssetFile) : IDisposable
	{
		public readonly void Dispose()
		{
			this.BundleFile.Close();
			this.InfoAssetFile.Close();
		}
	}

	private readonly CatalogParser _catalogParser;
	private readonly BundleStreamFactory _bundleStreamFactory;

	public AddressableBundleExtractor(CatalogParser catalogParser, BundleStreamFactory bundleStreamFactory)
	{
		this._catalogParser = catalogParser;
		this._bundleStreamFactory = bundleStreamFactory;
	}

	private static byte[] ReadAbsolutePositionRange(Stream baseStream, long offset, int size)
	{
		byte[] buffer = new byte[size];

		long oldPos = baseStream.Position;
		try
		{
			baseStream.Seek(offset, SeekOrigin.Begin);
			baseStream.ReadExactly(buffer, 0, size);
		}
		finally
		{
			baseStream.Position = oldPos;
		}

		return buffer;
	}

	private MappedAssetBundle FindAddressableByCatalogPath(string path)
	{
		Stream file = this.GetBundleStreamByCatalogPath(path);

		AssetsFileReader reader = new(file);
		AssetBundleFile bundleFile = new();
		bundleFile.Read(reader);
		if (bundleFile.DataIsCompressed)
		{
			bundleFile = BundleHelper.UnpackBundle(bundleFile);
		}

		bundleFile.GetFileRange(0, out long offset, out long size);
		SegmentStream stream = new(bundleFile.DataReader.BaseStream, offset, size);
		AssetsFile infoAssetFile = new();
		infoAssetFile.Read(stream);

		return new MappedAssetBundle(bundleFile, infoAssetFile);
	}
	private Stream GetBundleStreamByCatalogPath(string path)
	{
		CatalogValue? bundlePath = this._catalogParser.Get(path);

		if (bundlePath is null)
			throw new ArgumentException($"Asset not found in catalog.", nameof(path));
		if (bundlePath.Value.ResolvedKey is null)
			throw new ArgumentException($"Asset has no resolved bundle path.", nameof(path));
		if (bundlePath.Value.ResolvedKey.Value.StringValue is null)
			throw new ArgumentException($"Asset has invalid resolved bundle path.", nameof(path));

		return this._bundleStreamFactory(bundlePath.Value.ResolvedKey.Value.StringValue);
	}

	#region Public extraction methods
	public List<string> ListAllAssetPathsInCatalog()
	{
		return this._catalogParser.Entries
			.Where(e => e.Value.ResolvedKey != null && e.Key.StringValue is not null)
			.Select(v => v.Key.StringValue!)
			.ToList();
	}
	public Image GetImageRaw(string path)
	{
		using MappedAssetBundle bundle = this.FindAddressableByCatalogPath(path);

		foreach (AssetFileInfo? info in bundle.InfoAssetFile.AssetInfos)
		{
			if (info.TypeId == (int)AssetClassID.Texture2D)
			{
				AssetTypeValueField baseField = bundle.InfoAssetFile.GetBaseField(info);

				uint height = baseField["m_Height"].AsUInt;
				uint width = baseField["m_Width"].AsUInt;
				uint format = baseField["m_TextureFormat"].AsUInt;

				long dataOffset = baseField["m_StreamData"]["offset"].AsLong;
				long dataSize = baseField["m_StreamData"]["size"].AsLong;
				bundle.BundleFile.GetFileRange(1, out long dataFileOffset, out _);

				byte[] data = ReadAbsolutePositionRange(bundle.BundleFile.DataReader.BaseStream, dataFileOffset + dataOffset,
					(int)dataSize);

				Image image = new(format, width, height, data);
				return image;
			}
		}

		throw new ArgumentException("No Texture2D found in the asset bundle.", nameof(path));
	}
	public Music GetMusicRaw(string path)
	{
		using MappedAssetBundle bundle = this.FindAddressableByCatalogPath(path);

		foreach (AssetFileInfo? info in bundle.InfoAssetFile.AssetInfos)
		{
			if (info.TypeId == (int)AssetClassID.AudioClip)
			{
				AssetTypeValueField baseField = bundle.InfoAssetFile.GetBaseField(info);
				long dataOffset = baseField["m_Resource"]["m_Offset"].AsLong;
				long dataSize = baseField["m_Resource"]["m_Size"].AsLong;
				float length = baseField["m_Length"].AsFloat;
				bundle.BundleFile.GetFileRange(1, out long dataFileOffset, out _);

				byte[] data = ReadAbsolutePositionRange(bundle.BundleFile.DataReader.BaseStream, dataFileOffset + dataOffset,
					(int)dataSize);

				return new Music(length, data);
			}
		}

		throw new Exception("No AudioClip found in the asset bundle.");
	}
	public Text GetTextRaw(string path)
	{
		using MappedAssetBundle bundle = this.FindAddressableByCatalogPath(path);

		foreach (AssetFileInfo? info in bundle.InfoAssetFile.AssetInfos)
		{
			if (info.TypeId == (int)AssetClassID.TextAsset)
			{
				AssetTypeValueField baseField = bundle.InfoAssetFile.GetBaseField(info);
				string text = baseField["m_Script"].AsString;
				return new Text(text);
			}
		}

		throw new Exception("No TextAsset found in the asset bundle.");
	}
	#endregion
}