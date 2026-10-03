using Fmod5Sharp.CodecRebuilders;
using PhiInfo.Core.Extraction;
using PhiInfo.Core.Models.Information;
using SixLabors.ImageSharp.Formats.Png;

namespace PhiInfo.Tests;

[TestClass]
public sealed class ExtractTest
{
	[TestMethod]
	public async Task Information()
	{
		Helper.EnsureWorkingDirectory();
		MultiPackageFileLocator locator = new(Helper.GetTestPackageStreams());

		using InfoExtractor rawExtractor = await InfoExtractor.FromPackagesAsync(
			Helper.GetClassDataTPKStream(),
			locator);

		List<SongInfo> songs = rawExtractor.ExtractSongInfo();
		List<Folder> collections = rawExtractor.ExtractCollections();
		List<ChapterInfo> chapters = rawExtractor.ExtractChapters();
		List<string> tips = rawExtractor.ExtractTips();
		List<Avatar> avatars = rawExtractor.ExtractAvatars();

		Console.WriteLine($"Phigros version: {rawExtractor.GetVersionInteger()}, {rawExtractor.GetVersionString()}");
		Console.WriteLine($"Is international: {rawExtractor.GetIsInternational()}");
		Console.WriteLine(songs[Random.Shared.Next(songs.Count)]);
		Console.WriteLine(collections[Random.Shared.Next(collections.Count)]);
		Console.WriteLine(chapters[Random.Shared.Next(chapters.Count)]);
		Console.WriteLine(tips[Random.Shared.Next(tips.Count)]);
		Console.WriteLine(avatars[Random.Shared.Next(avatars.Count)]);
	}
	[TestMethod]
	public async Task Assets()
	{
		Helper.EnsureWorkingDirectory();

		//using PhigrosRawAssetExtractor rawExtractor = PhigrosRawAssetExtractor.FromApkAndObb(
		//	File.OpenRead(Helper.TestApkPath),
		//	File.OpenRead(Helper.TestObbPath),
		//	File.OpenRead(Helper.TestClassDataTPKPath));

		MultiPackageFileLocator locator = new(Helper.GetTestPackageStreams());
		AddressableBundleExtractor assetExtractor = await AddressableBundleExtractor.FromPackagesAsync(locator);

		Console.WriteLine("Assets in catalog:");
		Console.WriteLine(string.Join('\n', assetExtractor.ListMeaningfulAssetPathsInCatalog()));
		Console.WriteLine("Assets in catalog end");

		SixLabors.ImageSharp.Image image = (await assetExtractor.GetImageRawAsync("Assets/Tracks/Glaciaxion.SunsetRay.0/IllustrationLowRes.jpg")).Decode();
		SixLabors.ImageSharp.Image image2 = (await assetExtractor.GetImageRawAsync("avatar.praw")).Decode();
		SixLabors.ImageSharp.Image image3 = (await assetExtractor.GetImageRawAsync("Assets/Tracks/WhatdoyouwantmorethanaHappyending.Apo11oHALOprogramft安月名莉子大瀬良あい.0/Illustration.jpg.c9Locked")).Decode();
		Fmod5Sharp.FmodTypes.FmodSoundBank music = (await assetExtractor.GetMusicRawAsync("Assets/Tracks/DiamondEyes.SYNTHETIC.0/music.wav")).Decode();
		string chart = (await assetExtractor.GetTextRawAsync("Assets/Tracks/Elúltimobaile.Θ.0/Chart_EZ.json")).Content;

		await image.SaveAsync(File.Open("./TestData/extracted.png", FileMode.Create), new PngEncoder());
		await image2.SaveAsync(File.Open("./TestData/extracted2.png", FileMode.Create), new PngEncoder());
		await image3.SaveAsync(File.Open("./TestData/extracted3.png", FileMode.Create), new PngEncoder());
		File.WriteAllBytes("./TestData/extracted.ogg", FmodVorbisRebuilder.RebuildOggFile(music.Samples[0]));
		File.WriteAllText("./TestData/extracted.json", chart);
	}
}
