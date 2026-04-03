using LibCpp2IL.Logging;
using PhiInfo.Core;
using PhiInfo.Core.Models.Information;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PhiInfo.CLI;

public class Program
{
	private record struct NonMultiLanguageInfos(List<SongInfo> Songs,
		List<Avatar> Avatars,
		List<ChapterInfo> Chapters);
	private record struct MultiLanguageInfos(List<Folder>? Collections,
		List<string> Tips);

	private static readonly Option<FileInfo> ApkOption = new("--apk")
	{
		Description = "Path to the APK file",
		Required = false
	};
	private static readonly Option<FileInfo> ObbOption = new("--obb")
	{
		Description = "Path to the OBB file",
		Required = false
	};
	private static readonly Option<FileInfo> AuxObbOption = new("--aux-obb")
	{
		Description = "Path to the auxiliary OBB file",
		Required = false
	};
	private static readonly Option<FileInfo> ClassDataOption = new("--classdata")
	{
		Description = "Path to the class data TPK file",
		Required = false
	};

	private static readonly Option<DirectoryInfo> ExtractInfoOption = new("--extract-info-to")
	{
		Description = "Extract Phigros information, does not extract if not present",
		Required = false
	};
	private static readonly Option<DirectoryInfo> ExtractAssetOption = new("--extract-asset-to")
	{
		Description = "Extract Phigros assets, does not extract if not present",
		Required = false
	};

	private static readonly Option<bool> NoIllustrationOption = new("--no-illustration")
	{
		Description = "Do not extract illustration (only the highest resolution ones)",
		Required = false
	};
	private static readonly Option<bool> NoLowResolutionIllustrationOption = new("--no-low-res-illustration")
	{
		Description = "Do not extract low resolution illustration",
		Required = false
	};
	private static readonly Option<bool> NoBlurIllustrationOption = new("--no-blur-illustration")
	{
		Description = "Do not extract blurry illustration",
		Required = false
	};
	private static readonly Option<bool> NoMusicOption = new("--no-music")
	{
		Description = "Do not extract music",
		Required = false
	};
	private static readonly Option<bool> NoChartsOption = new("--no-charts")
	{
		Description = "Do not extract charts",
		Required = false
	};

	private static readonly Option<Language> LanguageOption = new("--language") // TODO: accept extract all languages if "All" is specified
	{
		Description = "Extract collections and tips using language",
		Required = false,
		DefaultValueFactory = _ => Language.Chinese
	};

	private static readonly List<Option> Options =
	[
		ApkOption,
		ObbOption,
		AuxObbOption,
		ClassDataOption,
		ExtractInfoOption,
		ExtractAssetOption,
		NoIllustrationOption,
		NoLowResolutionIllustrationOption,
		NoBlurIllustrationOption,
		NoMusicOption,
		NoChartsOption,
		LanguageOption
	];

	public static int Main(string[] args)
	{
#pragma warning disable IDE0028 // Simplify collection initialization
		RootCommand rootCommand = new("PhiInfo CLI");
		// visual studio is having some problem with above line
#pragma warning restore IDE0028 // Simplify collection initialization
		foreach (Option item in Options)
		{
			rootCommand.Options.Add(item);
		}

		rootCommand.SetAction(AfterArgumentParsed);
		return rootCommand.Parse(args).Invoke();
	}

	private static void AfterArgumentParsed(ParseResult parseResult)
	{
		FileInfo? apkFile = parseResult.GetValue(ApkOption);
		FileInfo? obbFile = parseResult.GetValue(ObbOption);
		FileInfo? auxObbFile = parseResult.GetValue(AuxObbOption);
		FileInfo? classDataFile = parseResult.GetValue(ClassDataOption);

		DirectoryInfo? extractInfoTo = parseResult.GetValue(ExtractInfoOption);
		DirectoryInfo? extractAssetTo = parseResult.GetValue(ExtractAssetOption);

		bool noIllustration = parseResult.GetValue(NoIllustrationOption);
		bool noLowResIllustration = parseResult.GetValue(NoLowResolutionIllustrationOption);
		bool noBlurIllustration = parseResult.GetValue(NoBlurIllustrationOption);
		bool noMusic = parseResult.GetValue(NoMusicOption);
		bool noCharts = parseResult.GetValue(NoChartsOption);

		Language language = parseResult.GetValue(LanguageOption);

		LibLogger.Writer = new QuietLogWriter(); // tells cpp2il to shut up

		PhigrosRawAssetExtractor? infoExtractor = null;
		if (apkFile is not null && classDataFile is not null)
		{
			infoExtractor = PhigrosRawAssetExtractor.FromApkAndObb(
				apkFile.OpenRead(),
				obbFile?.OpenRead(),
				classDataFile.OpenRead());

			infoExtractor.ExtractLanguage = language;
		}

		if (extractInfoTo is not null)
		{
			if (apkFile is null || infoExtractor is null)
			{
				Console.WriteLine("APK file is required for extracting information.");
				return;
			}
			if (obbFile is null)
				Console.WriteLine("Warning: Collection cannot be extracted because missing obb file.");

			Console.WriteLine("Extracting information...");
			List<SongInfo> songs = infoExtractor.ExtractSongInfo();
			List<Folder>? collections = obbFile is null ? null : infoExtractor.ExtractCollection();
			List<Avatar> avatars = infoExtractor.ExtractAvatars();
			List<string> tips = infoExtractor.ExtractTips();
			List<ChapterInfo> chapters = infoExtractor.ExtractChapters();


			Console.WriteLine("Writing information...");
			extractInfoTo.Create();
			File.WriteAllText(
				Path.Combine(extractInfoTo.FullName, "info.json"),
				JsonSerializer.Serialize(new NonMultiLanguageInfos(songs, avatars, chapters)),
				Encoding.UTF8);
			File.WriteAllText(
				Path.Combine(extractInfoTo.FullName, $"tipsAndCollections_{language}.json"),
				JsonSerializer.Serialize(new MultiLanguageInfos(collections, tips)),
				Encoding.UTF8);


			// PhigrosLibrary_Resource compatible format
			Console.WriteLine("Writing PhigrosLibrary_Resource compatible information...");

			// i know those string concats are ugly as fuck but lazy to change it rn
			string avatarTxt = string.Join('\n', avatars.Select(a => a.Name));
			string? collectionTsv = collections is null ? null : string.Join('\n', collections.SelectMany(x => x.Files)
				.Select(x => $"{x.Key}\t{x.Name}\t{x.SubIndex}"));
			string difficultyTsv = string.Join('\n', songs
				.Where(x => !x.Id.Contains("Introduction"))
				.Select(x => $"{x.Id[..^2]}\t{x.Levels["EZ"].ChartConstant}\t{x.Levels["HD"].ChartConstant}\t{x.Levels["IN"].ChartConstant}{(x.Levels.TryGetValue("AT", out SongLevel? at) ? $"\t{at.ChartConstant}" : "")}"));
			// TODO: add illustration, single txt
			string infoTsv = string.Join('\n', songs
				.Where(x => !x.Id.Contains("Introduction"))
				.Select(x => $"{x.Id[..^2]}\t{x.Name}\t{x.Illustrator}\t{x.Levels["EZ"].Charter}\t{x.Levels["HD"].Charter}\t{x.Levels["IN"].Charter}{(x.Levels.TryGetValue("AT", out SongLevel? at) ? $"\t{at.Charter}" : "")}"));
			string tipsTxt = string.Join('\n', tips);
			// seriously why is it named tmp
			string tmpTsv = string.Join('\n', avatars.Select(x => $"{x.Name}\t{x.AddressablePath[7..]}"));

			File.WriteAllText(Path.Combine(extractInfoTo.FullName, "avatar.txt"), avatarTxt, Encoding.UTF8);
			File.WriteAllText(Path.Combine(extractInfoTo.FullName, "difficulty.tsv"), difficultyTsv, Encoding.UTF8);
			File.WriteAllText(Path.Combine(extractInfoTo.FullName, "info.tsv"), infoTsv, Encoding.UTF8);
			File.WriteAllText(Path.Combine(extractInfoTo.FullName, "tips.txt"), tipsTxt, Encoding.UTF8);
			File.WriteAllText(Path.Combine(extractInfoTo.FullName, "tmp.tsv"), tmpTsv, Encoding.UTF8);
			if (collectionTsv is not null)
				File.WriteAllText(Path.Combine(extractInfoTo.FullName, "collection.tsv"), collectionTsv, Encoding.UTF8);
		}

		if (extractAssetTo is not null)
		{
			string GetAssetOutputPath(string assetPath)
			{
				string sanitizedAssetPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
				return Path.Combine(extractAssetTo.FullName, sanitizedAssetPath);
			}

			if (obbFile is null)
			{
				Console.WriteLine("OBB file is required for extracting assets.");
				return;
			}

			List<Avatar>? avatars = null;

			if (infoExtractor is not null)
			{
				avatars = infoExtractor.ExtractAvatars();
			}
			else
			{
				Console.WriteLine("Warning: Avatar map will not be generated due to missing apk argument.");
			}

			const string AvatarBasePath = "Assets/Avatar/";

			Directory.CreateDirectory(GetAssetOutputPath(AvatarBasePath));
			PngEncoder pngEncoder = new();

			// name -> hash, like "-SURREALISM-": "3da229e009e3edc8a4824ee0dc7aa87e796a7b47"
			Dictionary<string, string> avatarMap = [];
			AddressableBundleExtractor assetExtractor = AddressableBundleExtractor.FromObb(obbFile.OpenRead(), auxObbFile?.OpenRead());
			foreach (string assetPath in assetExtractor.ListMeaningfulAssetPathsInCatalog())
			{
				Console.WriteLine($"Extracting {assetPath}...");

				if (assetPath.StartsWith("avatar"))
				{
					Image avatar = assetExtractor.GetImageRaw(assetPath).Decode();

					using MemoryStream avatarStream = new();
					avatar.Save(avatarStream, pngEncoder);
					avatarStream.Position = 0;

					string hash = SHA1.HashData(avatarStream).ToHexString();
					File.WriteAllBytes(GetAssetOutputPath($"{AvatarBasePath}{hash}.png"), avatarStream.ToArray());

					if (avatars is null) continue;

					Avatar? avatarInfo = avatars.FirstOrDefault(x => x.AddressablePath == assetPath);
					if (avatarInfo is null)
					{
						Console.WriteLine($"Warning: Cannot find avatar info for {assetPath}, skipping mapping.");
						continue;
					}

					avatarMap[avatarInfo.Name] = hash;

					continue;
				}
				if (!assetPath.StartsWith("Assets/"))
				{
					Console.WriteLine($"Skipping {assetPath} as it is not supported.");
					continue;
				}

				if (assetPath.EndsWith(".wav") && !noMusic)
				{
					byte[] music = assetExtractor.GetMusicRaw(assetPath).Decode().ToOggBytes();
					File.WriteAllBytes(GetAssetOutputPath($"{assetPath[..^4]}.ogg").EnsureAssetCanCreate(), music);

					continue;
				}
				if (assetPath.EndsWith(".json") && !noCharts)
				{
					string content = assetExtractor.GetTextRaw(assetPath).Content;
					File.WriteAllText(GetAssetOutputPath(assetPath).EnsureAssetCanCreate(), content, Encoding.UTF8);

					continue;
				}
				if (assetPath.EndsWith(".jpg") || assetPath.EndsWith(".png"))
				{
					if ((assetPath.Contains("/Illustration.") && noIllustration)
						|| (assetPath.Contains("/IllustrationLowRes.") && noLowResIllustration)
						|| (assetPath.Contains("/IllustrationBlur.") && noBlurIllustration))
					{
						goto Skip;
					}

					Image image = assetExtractor.GetImageRaw(assetPath).Decode();
					image.Save(GetAssetOutputPath($"{assetPath[..^4]}.png").EnsureAssetCanCreate(), pngEncoder);

					continue;
				}

			Skip:
				Console.WriteLine($"Skipping {assetPath}.");
			}

			if (avatars is not null)
			{
				Console.WriteLine("Writing AvatarInfo...");
				File.WriteAllText(
					Path.Combine(extractAssetTo.FullName, AvatarBasePath, "AvatarInfo.json"),
					JsonSerializer.Serialize(avatarMap),
					Encoding.UTF8);
			}
		}
	}
}