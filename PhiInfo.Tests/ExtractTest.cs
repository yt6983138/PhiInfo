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

		using InfoExtractor rawExtractor = await InfoExtractor.FromApkAndObbAsync(
			File.OpenRead(Helper.TestApkPath),
			File.OpenRead(Helper.TestObbPath),
			File.OpenRead(Helper.TestClassDataTPKPath));

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

		AddressableBundleExtractor assetExtractor = await AddressableBundleExtractor.FromObbAsync(
			File.OpenRead(Helper.TestObbPath),
			File.Exists(Helper.TestAuxObbPath) ? File.OpenRead(Helper.TestAuxObbPath) : null);

		Console.WriteLine("Assets in catalog:");
		Console.WriteLine(string.Join('\n', assetExtractor.ListMeaningfulAssetPathsInCatalog()));
		Console.WriteLine("Assets in catalog end");

		SixLabors.ImageSharp.Image image = (await assetExtractor.GetImageRawAsync("Assets/Tracks/Glaciaxion.SunsetRay.0/IllustrationLowRes.jpg")).Decode();
		SixLabors.ImageSharp.Image image2 = (await assetExtractor.GetImageRawAsync("avatar.praw")).Decode();
		Fmod5Sharp.FmodTypes.FmodSoundBank music = (await assetExtractor.GetMusicRawAsync("Assets/Tracks/DiamondEyes.SYNTHETIC.0/music.wav")).Decode();
		string chart = (await assetExtractor.GetTextRawAsync("Assets/Tracks/Elúltimobaile.Θ.0/Chart_EZ.json")).Content;

		await image.SaveAsync(File.Open("./TestData/extracted.png", FileMode.Create), new PngEncoder());
		File.WriteAllBytes("./TestData/extracted.ogg", FmodVorbisRebuilder.RebuildOggFile(music.Samples[0]));
		File.WriteAllText("./TestData/extracted.json", chart);
	}
}
