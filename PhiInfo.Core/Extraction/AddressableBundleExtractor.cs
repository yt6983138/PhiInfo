using AssetsTools.NET;
using AssetsTools.NET.Extra;
using PhiInfo.Core.Models.Catalog;
using PhiInfo.Core.Models.RawAsset;
using System.Globalization;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Maps addressable bundle file name to the actual bundle stream.
/// </summary>
/// <param name="path">The addressable bundle name. I.e. 0a0ec2bd31adfd9c120bf7658ad4fa05.bundle</param>
/// <returns>A stream to the bundle file. Must be seekable and readable. Will be disposed after use.</returns>
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

	/// <summary>
	/// Creates an instance of this class with a <see cref="CatalogParser"/> and a <see cref="BundleStreamFactory"/>. 
	/// Recommend to use the static methods in <see cref="PhigrosAssetHelper"/> to create these parameters, 
	/// or use the static method like <see cref="FromObb(Stream, Stream?)"/> to create an instance of this class directly.
	/// Auxiliary obb is sometimes needed.
	/// </summary>
	/// <param name="catalogParser">A catalog parser.</param>
	/// <param name="bundleStreamFactory">A bundle stream factory.</param>
	public AddressableBundleExtractor(CatalogParser catalogParser, BundleStreamFactory bundleStreamFactory)
	{
		this._catalogParser = catalogParser;
		this._bundleStreamFactory = bundleStreamFactory;
	}

	/// <summary>
	/// Please see <see cref="AddressableBundleExtractor(CatalogParser, BundleStreamFactory)"/> for
	/// more information.
	/// </summary>
	/// <param name="obb">The obb file. May need <paramref name="auxObb"/> sometimes.</param>
	/// <param name="auxObb">The auxiliary (patch) obb file.</param>
	/// <returns>A new instance of <see cref="AddressableBundleExtractor"/>.</returns>
	public static AddressableBundleExtractor FromObb(Stream obb, Stream? auxObb = null)
	{
		CatalogParser catalogParser = CatalogParser.FromObb(obb);
		return new(catalogParser, PhigrosAssetHelper.CreateBundleFactoryFromObb(obb, auxObb));
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
		// this stream will be disposed by the AssetBundleFile dispose method, which is called in MappedAssetBundle dispose method
		Stream file = this.GetBundleStreamByCatalogPath(path);

		AssetsFileReader reader = new(file);
		AssetBundleFile bundleFile = new();
		bundleFile.Read(reader);
		if (bundleFile.DataIsCompressed)
			bundleFile = BundleHelper.UnpackBundle(bundleFile);

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
	/// <summary>
	/// Get all asset paths in the catalog, including the ones with only hexadecimal names.
	/// </summary>
	/// <returns>All asset paths from catalog.</returns>
	public List<string> ListAllAssetPathsInCatalog()
	{
		return this._catalogParser.Entries
			.Where(e => e.Value.ResolvedKey != null && e.Key.StringValue is not null)
			.Select(v => v.Key.StringValue!)
			.ToList();
	}
	/// <summary>
	/// Get asset paths without the ones with only hexadecimal names, 
	/// which seemed to be unused or unimportant for asset extraction purposes.
	/// </summary>
	/// <returns>A list with only meaningful asset paths.</returns>
	public List<string> ListMeaningfulAssetPathsInCatalog()
	{
		return this.ListAllAssetPathsInCatalog()
			.Where(x => !UInt128.TryParse(x, NumberStyles.HexNumber, null, out UInt128 _))
			.ToList();
	}
	/// <summary>
	/// Get raw image data from path.
	/// </summary>
	/// <param name="path">The addressable path.</param>
	/// <returns>Raw image data in unity internal format.</returns>
	/// <exception cref="ArgumentException">Thrown if there is no Texture2D in the bundle.</exception>
	public UnityImage GetImageRaw(string path)
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

				UnityImage image = new(format, width, height, data);
				return image;
			}
		}

		throw new ArgumentException("No Texture2D found in the asset bundle.", nameof(path));
	}
	/// <summary>
	/// Get raw music data from path.
	/// </summary>
	/// <param name="path">The addressable path.</param>
	/// <returns>Raw music data in unity internal format.</returns>
	/// <exception cref="ArgumentException">Thrown if there is no AudioClip in the bundle.</exception>
	public UnityMusic GetMusicRaw(string path)
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

				return new UnityMusic(length, data);
			}
		}

		throw new ArgumentException("No AudioClip found in the asset bundle.", nameof(path));
	}

	/// <summary>
	/// Get raw text data from path.
	/// </summary>
	/// <param name="path">The addressable path.</param>
	/// <returns>Raw text data in unity internal format.</returns>
	/// <exception cref="ArgumentException">Thrown if there is no TextAsset in the bundle.</exception>
	public UnityText GetTextRaw(string path)
	{
		using MappedAssetBundle bundle = this.FindAddressableByCatalogPath(path);

		foreach (AssetFileInfo? info in bundle.InfoAssetFile.AssetInfos)
		{
			if (info.TypeId == (int)AssetClassID.TextAsset)
			{
				AssetTypeValueField baseField = bundle.InfoAssetFile.GetBaseField(info);
				string text = baseField["m_Script"].AsString;
				return new UnityText(text);
			}
		}

		throw new ArgumentException("No TextAsset found in the asset bundle.", nameof(path));
	}
	#endregion
}