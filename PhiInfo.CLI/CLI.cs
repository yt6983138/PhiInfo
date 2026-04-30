using LibCpp2IL.Logging;
using Microsoft.Extensions.Logging;
using PhiInfo.Core.Cpp2ILLogWriter;
using PhiInfo.Core.Models;
using SixLabors.ImageSharp;
using System.CommandLine;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhiInfo.CLI;

/// <summary>
/// Provides static methods for downloading APK and class data, as well as instance methods for extracting information and assets to specified directories.
/// This is also the entry point of the program.
/// </summary>
public class CLI
{
	/// <summary>
	/// All language identifier for CLI parsing and extraction. 
	/// This is not a real language enum value and should not be used in other contexts.
	/// Passing to <see cref="Core.Extraction.InfoExtractor"/> will cause errors.
	/// </summary>
	public const Language AllLanguage = unchecked((Language)0xFFFFFFFF);

	/// <summary>
	/// For some reason microsoft decided to make <see cref="UTF8Encoding"/> emit BOM by default, 
	/// which is really annoying since some software are failing because of the bom.
	/// </summary>
	public static readonly UTF8Encoding UTF8WithoutBOM = new(false);
	/// <summary>
	/// Default serializer options for writing json related to Phigros information.
	/// Intended to be used in downstream applications to ensure consistency.
	/// </summary>
	public static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = null,
		PropertyNameCaseInsensitive = true,
		NumberHandling = JsonNumberHandling.AllowReadingFromString,
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	#region CLI parsing

	#region Arguments
	private static readonly Option<string> DownloadApkOption = new("--download-apk")
	{
		Description = """
			Download APK from specified URL, or fill "TAPTAP" for to pick the source from TapTap.
			TapTap downloading will not contain OBB since TapTap put everything in APK.
			Specifying this option will use the --apk option as the download destination, 
			if it is not specified, the APK will be downloaded to temporary directory.
			""",
		Required = false
	};
	private static readonly Option<string> DownloadClassDataOption = new("--download-classdata")
	{
		Description = """
			Download classdata.tpk from specified URL, or fill "AUTO" to pick the source automatically.
			Specifying this option will use the --classdata option as the download destination, 
			if it is not specified, the classdata.tpk will be downloaded to temporary directory.
			""",
		Required = false
	};

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

	private static readonly Option<Language> LanguageOption = new("--language")
	{
		Description = """
			Extract collections and tips using language. If All is specified, all language
			will be extracted. However, Phigros_Resource compatible format will always be extracted 
			using EnglishUS since it does not support multiple languages.
			""",
		Required = false,
		CustomParser = result =>
		{
			if (result.Tokens.Count == 0)
				return Language.EnglishUS;
			string token = result.Tokens[0].Value;

			if (token == "All")
				return AllLanguage;

			if (Enum.TryParse(token, out Language language))
			{
				return language;
			}
			result.AddError($"Failed to parse language. Valid values: {string.Join(", ", Enum.GetValues<Language>().Select(x => x.ToString()).Concat(["All"]))}");
			return Language.EnglishUS;
		}
	};

	private static readonly Option<bool> DebugOption = new("--debug")
	{
		Description = "Enable debug logging",
		Required = false
	};

	private static readonly List<Option> Options =
	[
		DownloadApkOption,
		DownloadClassDataOption,
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
		LanguageOption,
		DebugOption
	];
	#endregion

	/// <summary>
	/// The program entry point.
	/// </summary>
	/// <param name="args">CLI arguments.</param>
	/// <returns>The exit code.</returns>
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

		rootCommand.SetAction(AfterArgumentParsedAsync);
		return rootCommand.Parse(args).Invoke();
	}
	private static async Task AfterArgumentParsedAsync(ParseResult parseResult)
	{
		if (parseResult.Tokens.Count == 0)
		{
			Console.WriteLine("Warning: No arguments provided. Use --help to see available options.");
			return;
		}

		string? downloadApk = parseResult.GetValue(DownloadApkOption);
		string? downloadClassData = parseResult.GetValue(DownloadClassDataOption);

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

		bool debug = parseResult.GetValue(DebugOption);

		ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			if (debug)
			{
				builder.SetMinimumLevel(LogLevel.Debug);
			}
		});
		LibLogger.Writer = new QuietLogWriter(); // tells cpp2il to shut up
		ILogger<CLI> logger = loggerFactory.CreateLogger<CLI>();

		if (downloadApk is not null)
		{
			FileInfo downloadedApk = await DownloadApk(downloadApk, apkFile, logger);
			apkFile ??= downloadedApk;

			// replace them with apk file since TapTap puts everything in apk
			obbFile = downloadedApk;
			auxObbFile = null;
		}

		if (downloadClassData is not null)
		{
			classDataFile = await DownloadClassData(downloadClassData, classDataFile, logger);
		}

		CLIExtractor extractor = await CLIExtractor.FromOptionAsync(new()
		{
			ApkFile = apkFile?.OpenRead(),
			ObbFile = obbFile?.OpenRead(),
			AuxObbFile = auxObbFile?.OpenRead(),
			ClassDataFile = classDataFile?.OpenRead(),
			NoIllustration = noIllustration,
			NoLowResIllustration = noLowResIllustration,
			NoBlurIllustration = noBlurIllustration,
			NoMusic = noMusic,
			NoCharts = noCharts
		}, loggerFactory.CreateLogger<CLIExtractor>());
		CLI core = new(extractor, logger);

		if (extractInfoTo is not null)
		{
			await core.ExtractInfoToDirectory(extractInfoTo, language);
		}

		if (extractAssetTo is not null)
		{
			await core.ExtractAssetsToDirectory(extractAssetTo);
		}
	}
	#endregion

	private readonly ILogger<CLI> _logger;

	/// <summary>
	/// Extractor for CLI extraction.
	/// </summary>
	public CLIExtractor Extractor { get; set; }

	/// <summary>
	/// Construct a new <see cref="CLI"/> instance with the specified extractor and logger.
	/// </summary>
	/// <param name="extractor">The extractor to use for CLI extraction.</param>
	/// <param name="logger">The logger to use for logging.</param>
	public CLI(CLIExtractor extractor, ILogger<CLI> logger)
	{
		this.Extractor = extractor;
		this._logger = logger;
	}

	/// <summary>
	/// Download Phigros APK from the specified source. 
	/// If the source is "TAPTAP", the latest APK URL will be fetched from TapTap.
	/// </summary>
	/// <param name="source">The source from which to download the APK.</param>
	/// <param name="destination">The destination file for the downloaded APK.</param>
	/// <param name="logger">The logger to use for logging.</param>
	/// <returns>The downloaded APK file info. This will return <paramref name="destination"/> if specified, otherwise a temporary file.</returns>
	public static async Task<FileInfo> DownloadApk(string source, FileInfo? destination, ILogger<CLI> logger)
	{
		using HttpClient client = new();

		string apkUrl = source == "TAPTAP" ?
			await TapTapDownloader.GetApkLatestUrlAsync(client) : source;
		destination ??= new(Path.Combine(Path.GetTempPath(), "phigros_latest.apk"));

		logger.LogInformation("Downloading APK from {apkUrl} to {apkDestination}...", apkUrl, destination.FullName);

		//apkFile = new(apkDestination);
		//obbFile = new(apkDestination);
		//auxObbFile = null;

		using Stream apkStream = destination.Open(FileMode.Create, FileAccess.Write);
		using Stream downloaded = await client.GetStreamAsync(apkUrl);
		await downloaded.CopyToAsync(apkStream);

		logger.LogInformation("APK downloaded.");

		return destination;
	}
	/// <summary>
	/// Download classdata.tpk from the specified source.
	/// If the source is "AUTO", the latest classdata.tpk will be fetched from the GitHub repository of AssetRipper/Tpk.
	/// </summary>
	/// <param name="source">The source from which to download the classdata.tpk.</param>
	/// <param name="destination">The destination file for the downloaded classdata.tpk.</param>
	/// <param name="logger">The logger to use for logging.</param>
	/// <returns>The downloaded classdata.tpk file info. This will return <paramref name="destination"/> if specified, otherwise a temporary file.</returns>
	public static async Task<FileInfo> DownloadClassData(string source, FileInfo? destination, ILogger<CLI> logger)
	{
		using HttpClient client = new();

		destination ??= new(Path.Combine(Path.GetTempPath(), "classdata.tpk"));
		using FileStream classDataStream = destination.Open(FileMode.Create, FileAccess.Write);

		if (source == "AUTO")
		{
			using ZipArchive archive = new(
				await client.GetStreamAsync("https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/uncompressed_file.zip"));

			using Stream classDataEntryStream = archive.GetEntry("uncompressed.tpk").EnsureNotNull().Open();

			await classDataEntryStream.CopyToAsync(classDataStream);
		}
		else
		{
			logger.LogInformation("Downloading classdata.tpk from {src} to {dst}...", source, destination.FullName);
			using Stream downloaded = await client.GetStreamAsync(source);
			await downloaded.CopyToAsync(classDataStream);
		}
		logger.LogInformation("classdata.tpk downloaded.");

		return destination;
	}

	/// <summary>
	/// Extract Phigros information to the specified directory.
	/// This method accepts <see cref="AllLanguage"/> as a special language parameter to extract all languages.
	/// </summary>
	/// <param name="extractInfoTo">Directory to extract info in.</param>
	/// <param name="language">The language to extract information for. Use <see cref="AllLanguage"/> to extract all languages.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ExtractInfoToDirectory(DirectoryInfo extractInfoTo, Language language)
	{
		extractInfoTo.FullName.EnsureAssetCanCreate(false);

		NonMultiLanguageInfos nonMultiLangInfo = this.Extractor.ExtractNonLanguageSpecificInfo();

		this._logger.LogInformation("Writing non-language specific info...");
		await File.WriteAllTextAsync(
			Path.Combine(extractInfoTo.FullName, "info.json"),
			JsonSerializer.Serialize(nonMultiLangInfo, JsonOptions),
			UTF8WithoutBOM);

		// required for PhigrosLibrary_Resource compatible format, which does not support multiple language
		MultiLanguageInfos multiLangInfo;
		if (language == AllLanguage)
		{
			this._logger.LogInformation("Writing language specific info with all languages...");
			foreach (Language lang in Enum.GetValues<Language>())
			{
				MultiLanguageInfos langInfo = this.Extractor.ExtractLanguageSpecificInfo(lang);
				await File.WriteAllTextAsync(
					Path.Combine(extractInfoTo.FullName, $"tipsAndCollections_{lang}.json"),
					JsonSerializer.Serialize(langInfo, JsonOptions),
					UTF8WithoutBOM);
			}
			multiLangInfo = this.Extractor.ExtractLanguageSpecificInfo(Language.EnglishUS);
		}
		else
		{
			this._logger.LogInformation("Writing language specific info with {lang}...", language);
			multiLangInfo = this.Extractor.ExtractLanguageSpecificInfo(language);
			await File.WriteAllTextAsync(
					Path.Combine(extractInfoTo.FullName, $"tipsAndCollections_{language}.json"),
					JsonSerializer.Serialize(multiLangInfo, JsonOptions),
					UTF8WithoutBOM);
		}

		// PhigrosLibrary_Resource compatible format
		Dictionary<string, string> compatibleOutput = this.Extractor.BuildPhigrosResourceCompatibleOutput(nonMultiLangInfo, multiLangInfo);
		foreach (KeyValuePair<string, string> item in compatibleOutput)
		{
			this._logger.LogInformation("Writing Phigros_Resource compatible output {file}...", item.Key);
			File.WriteAllText(Path.Combine(extractInfoTo.FullName, item.Key), item.Value, UTF8WithoutBOM);
		}
	}
	/// <summary>
	/// Extract Phigros assets to the specified directory. 
	/// The output directory structure will be the same as the addressable path in the game, with "Assets/Avatar/" as the base path for avatar assets.
	/// </summary>
	/// <param name="extractAssetTo">The directory to extract assets to.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ExtractAssetsToDirectory(DirectoryInfo extractAssetTo)
	{
		extractAssetTo.FullName.EnsureAssetCanCreate(false);

		AssetExtractionContext context = this.Extractor.CreateAssetExtractionContext(HandleFile);
		await context.ExtractAll();

		Dictionary<string, string> avatarMap = context.AvatarMap;

		this._logger.LogInformation("Writing AvatarInfo.json...");
		await File.WriteAllTextAsync(
				Path.Combine(extractAssetTo.FullName, AssetExtractionContext.AvatarBasePath, "AvatarInfo.json"),
				JsonSerializer.Serialize(avatarMap, JsonOptions),
				UTF8WithoutBOM);

		async Task HandleFile(string path, Stream stream)
		{
			string outputPath = GetAssetOutputPath(path).EnsureAssetCanCreate();
			using FileStream outputStream = new(outputPath, FileMode.Create, FileAccess.Write);
			await stream.CopyToAsync(outputStream);
		}
		string GetAssetOutputPath(string assetPath)
		{
			string sanitizedAssetPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
			return Path.Combine(extractAssetTo.FullName, sanitizedAssetPath);
		}
	}
}