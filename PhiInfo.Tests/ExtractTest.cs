using Fmod5Sharp.CodecRebuilders;
using PhiInfo.Core;
using PhiInfo.Core.Models.Information;
using SixLabors.ImageSharp.Formats.Png;

namespace PhiInfo.Tests;

[TestClass]
public sealed class ExtractTest
{
	private static volatile bool _hasSetWorkingDirectory = false;

	private static void EnsureWorkingDirectory()
	{
		if (_hasSetWorkingDirectory) return;
		_hasSetWorkingDirectory = true;
		Environment.CurrentDirectory = Path.Combine(Environment.CurrentDirectory, "..", "..", "..");
	}

	[TestMethod]
	public void Information()
	{
		EnsureWorkingDirectory();

		using PhigrosRawAssetExtractor rawExtractor = PhigrosRawAssetExtractor.FromApkAndObb(
			File.OpenRead("./TestData/base.apk"),
			File.OpenRead("./TestData/obb.obb"),
			File.OpenRead("./TestData/classdata.tpk"));

		PhigrosExtractedDataCollection info = rawExtractor.ExtractAll();
		Console.WriteLine(info);
	}
	[TestMethod]
	public void Assets()
	{
		EnsureWorkingDirectory();

		//using PhigrosRawAssetExtractor rawExtractor = PhigrosRawAssetExtractor.FromApkAndObb(
		//	File.OpenRead("./TestData/base.apk"),
		//	File.OpenRead("./TestData/obb.obb"),
		//	File.OpenRead("./TestData/classdata.tpk"));

		AddressableBundleExtractor assetExtractor = AddressableBundleExtractor.FromObb(File.OpenRead("./TestData/obb.obb"));

		Console.WriteLine("Assets in catalog:");
		Console.WriteLine(string.Join('\n', assetExtractor.ListMeaningfulAssetPathsInCatalog()));
		Console.WriteLine("Assets in catalog end");

		SixLabors.ImageSharp.Image image = assetExtractor.GetImageRaw("Assets/Tracks/Glaciaxion.SunsetRay.0/IllustrationLowRes.jpg").Decode();
		Fmod5Sharp.FmodTypes.FmodSoundBank music = assetExtractor.GetMusicRaw("Assets/Tracks/DiamondEyes.SYNTHETIC.0/music.wav").Decode();
		string chart = assetExtractor.GetTextRaw("Assets/Tracks/Elúltimobaile.Θ.0/Chart_EZ.json").Content;

		image.Save(File.Open("./TestData/extracted.png", FileMode.Create), new PngEncoder());
		File.WriteAllBytes("./TestData/extracted.ogg", FmodVorbisRebuilder.RebuildOggFile(music.Samples[0]));
		File.WriteAllText("./TestData/extracted.json", chart);
	}
}
