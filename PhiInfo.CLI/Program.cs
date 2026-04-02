using PhiInfo.Core;
using PhiInfo.Core.Models.Information;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using System.CommandLine;
using System.Security.Cryptography;
using System.Text;

namespace PhiInfo.CLI;

public class Program
{
	private static readonly Option<FileInfo> ApkOption = new("--apk")
	{
		Description = "Path to the APK file",
		Required = false
	};
	private static readonly Option<FileInfo> ObbOption = new("--obb") // TODO: support patch obb file
	{
		Description = "Path to the OBB file",
		Required = false
	};
	private static readonly Option<FileInfo> ClassDataOption = new("--classdata")
	{
		Description = "Path to the class data TPK file",
		Required = true
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

	private static readonly List<Option> Options =
	[
		ApkOption,
		ObbOption,
		ClassDataOption,
		ExtractInfoOption,
		ExtractAssetOption,
		NoIllustrationOption,
		NoLowResolutionIllustrationOption,
		NoBlurIllustrationOption,
		NoMusicOption,
		NoChartsOption
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
		FileInfo? classDataFile = parseResult.GetValue(ClassDataOption);

		DirectoryInfo? extractInfoTo = parseResult.GetValue(ExtractInfoOption);
		DirectoryInfo? extractAssetTo = parseResult.GetValue(ExtractAssetOption);

		bool noIllustration = parseResult.GetValue(NoIllustrationOption);
		bool noLowResIllustration = parseResult.GetValue(NoLowResolutionIllustrationOption);
		bool noBlurIllustration = parseResult.GetValue(NoBlurIllustrationOption);
		bool noMusic = parseResult.GetValue(NoMusicOption);
		bool noCharts = parseResult.GetValue(NoChartsOption);

		if (extractInfoTo is not null)
		{
			if (apkFile is null || classDataFile is null || obbFile is null)
			{
				Console.WriteLine("APK, OBB, and class data file are required for extracting information.");
				return;
			}

			using PhigrosRawAssetExtractor rawExtractor = PhigrosRawAssetExtractor.FromApkAndObb(
				apkFile.OpenRead(),
				obbFile.OpenRead(),
				classDataFile.OpenRead());

			PhigrosExtractedDataCollection info = rawExtractor.ExtractAll();
			// TODO: output format
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

			PngEncoder pngEncoder = new();

			// name -> hash, like "-SURREALISM-": "3da229e009e3edc8a4824ee0dc7aa87e796a7b47"
			Dictionary<string, string> avatarMap = [];
			AddressableBundleExtractor assetExtractor = AddressableBundleExtractor.FromObb(obbFile.OpenRead());
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
					// TODO: add avatar name mapping, assetPath contains just id

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
					File.WriteAllBytes(GetAssetOutputPath(assetPath), music);

					continue;
				}
				if (assetPath.EndsWith(".json") && !noCharts)
				{
					string content = assetExtractor.GetTextRaw(assetPath).Content;
					File.WriteAllText(GetAssetOutputPath(assetPath), content, Encoding.UTF8);

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
					image.Save(GetAssetOutputPath($"{assetPath[..^4]}.png"), pngEncoder);

					continue;
				}

			Skip:
				Console.WriteLine($"Skipping {assetPath}.");
			}
		}
	}
}