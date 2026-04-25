using CsvHelper;
using Microsoft.Extensions.Logging;
using PhigrosLibraryCSharp.CloudSave;
using PhiInfo.Core.Extraction;
using PhiInfo.Core.Models;
using PhiInfo.Core.Models.Information;
using SixLabors.ImageSharp;
using System.Text;

namespace PhiInfo.CLI;

/// <summary>
/// Convenience wrapper for CLI extraction. This can be used outside CLI scenarios.
/// </summary>
public class CLIExtractor
{
	private readonly ILogger<CLIExtractor> _logger;

	/// <summary>
	/// The info extractor. May be null if the necessary files for info extraction are not provided.
	/// </summary>
	public InfoExtractor? InfoExtractor { get; }
	/// <summary>
	/// The addressable bundle extractor. May be null if the necessary files for extraction are not provided.
	/// </summary>
	public AddressableBundleExtractor? AddressableBundleExtractor { get; }
	/// <summary>
	/// The original extraction options.
	/// </summary>
	public ExtractOptions ExtractOptions { get; }

	#region Constructor and Factory
	/// <summary>
	/// Create a <see cref="CLIExtractor"/> with the provided extractors and options. At least one of the extractors must be non-null.
	/// You can use <see cref="FromOptionAsync"/> to create a <see cref="CLIExtractor"/> from extraction options, which will 
	/// automatically determine the necessary extractors based on the provided files (and is more convenient).
	/// </summary>
	/// <param name="infoExtractor">The info extractor. May be null if the necessary files for info extraction are not provided.</param>
	/// <param name="addressableBundleExtractor">The addressable bundle extractor. May be null if the necessary files for extraction are not provided.</param>
	/// <param name="extractOptions">The extraction options.</param>
	/// <param name="logger">The logger.</param>
	/// <exception cref="ArgumentException">Thrown if both <paramref name="addressableBundleExtractor"/> and <paramref name="infoExtractor"/> are null.</exception>
	public CLIExtractor(
		InfoExtractor? infoExtractor,
		AddressableBundleExtractor? addressableBundleExtractor,
		ExtractOptions extractOptions,
		ILogger<CLIExtractor> logger)
	{
		if (infoExtractor is null && addressableBundleExtractor is null)
		{
			// what do you want this to do then
			throw new ArgumentException("At least one of infoExtractor and addressableBundleExtractor must be provided.");
		}

		this.InfoExtractor = infoExtractor;
		this.AddressableBundleExtractor = addressableBundleExtractor;
		this.ExtractOptions = extractOptions;
		this._logger = logger;
	}

	/// <summary>
	/// Create a <see cref="CLIExtractor"/> from the provided extraction options. 
	/// The necessary extractors will be automatically created based on the provided files in the options.
	/// </summary>
	/// <param name="option">The extraction options.</param>
	/// <param name="logger">The logger.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the created <see cref="CLIExtractor"/>.</returns>
	public static async Task<CLIExtractor> FromOptionAsync(ExtractOptions option, ILogger<CLIExtractor> logger)
	{
		InfoExtractor? infoExtractor = null;
		AddressableBundleExtractor? addressableBundleExtractor = null;

		if (option.ApkFile is not null && option.ClassDataFile is not null)
		{
			infoExtractor = await InfoExtractor.FromApkAndObbAsync(
				option.ApkFile,
				option.ObbFile,
				option.ClassDataFile);
		}
		if (option.ObbFile is not null)
		{
			addressableBundleExtractor = await AddressableBundleExtractor.FromObbAsync(
				option.ObbFile,
				option.AuxObbFile);
		}

		return new(infoExtractor, addressableBundleExtractor, option, logger);
	}
	#endregion

	#region Helper Methods
	private InfoExtractor RequireInfoExtractor()
	{
		if (this.InfoExtractor is null)
			throw new InvalidOperationException("InfoExtractor is not available. Please provide the necessary files to create an InfoExtractor.");

		return this.InfoExtractor;
	}
	private AddressableBundleExtractor RequireAddressableBundleExtractor()
	{
		if (this.AddressableBundleExtractor is null)
			throw new InvalidOperationException("AddressableBundleExtractor is not available. Please provide the necessary files to create an AddressableBundleExtractor.");

		return this.AddressableBundleExtractor;
	}
	#endregion

	#region Information Extraction
	/// <summary>
	/// Extract language specific information.
	/// Note: this does not accept <see cref="CLI.AllLanguage"/>.
	/// </summary>
	/// <param name="lang">The language to extract information for.</param>
	/// <returns>The extracted language specific information.</returns>
	public MultiLanguageInfos ExtractLanguageSpecificInfo(Language lang)
	{
		InfoExtractor infoExtractor = this.RequireInfoExtractor();
		infoExtractor.ExtractLanguage = lang;

		List<Folder>? collections = null;
		if (this.ExtractOptions.ObbFile is null)
		{
			this._logger.LogWarning("Collection cannot be extracted because of missing obb file.");
		}
		else
		{
			this._logger.LogInformation("Extracting Collection in {lang}...", lang);
			collections = infoExtractor.ExtractCollections();
		}

		this._logger.LogInformation("Extracting Tips in {lang}...", lang);
		return new(collections, infoExtractor.ExtractTips());
	}
	/// <summary>
	/// Extract non-language specific information.
	/// </summary>
	/// <returns>The extracted non-language specific information.</returns>
	public NonMultiLanguageInfos ExtractNonLanguageSpecificInfo()
	{
		InfoExtractor infoExtractor = this.RequireInfoExtractor();

		this._logger.LogInformation("Extracting Song Info...");
		List<SongInfo> songInfo = infoExtractor.ExtractSongInfo();
		this._logger.LogInformation("Extracting Avatars...");
		List<Avatar> avatars = infoExtractor.ExtractAvatars();
		this._logger.LogInformation("Extracting Chapters...");
		List<ChapterInfo> chapters = infoExtractor.ExtractChapters();

		return new(
			songInfo,
			avatars,
			chapters,
			infoExtractor.GetVersionString(),
			infoExtractor.GetVersionInteger(),
			infoExtractor.GetIsInternational());
	}

	/// <summary>
	/// Convert the extracted information into a format compatible with applications based on Phigros_Resource. 
	/// The output is a dictionary where the key is the file name and the value is the file content.
	/// </summary>
	/// <param name="nonMultiLangInfo">The non-language specific information.</param>
	/// <param name="multiLangInfo">The language specific information.</param>
	/// <returns>A dictionary where the key is the file name and the value is the file content.</returns>
	public Dictionary<string, string> BuildPhigrosResourceCompatibleOutput(NonMultiLanguageInfos nonMultiLangInfo, MultiLanguageInfos multiLangInfo)
	{
		Dictionary<string, string> output = [];

		if (multiLangInfo.Collections is not null)
		{
			this._logger.LogInformation("Building collection.tsv...");
			StringBuilder collectionTsv = new();
			using (CsvWriter writer = CsvWriter.FromStringBuilder(collectionTsv, "\t"))
			{
				foreach (FileItem? item in multiLangInfo.Collections.SelectMany(x => x.Files))
				{
					writer.WriteFields(item.Key, item.Name, item.SubIndex.ToString());
					writer.NextRecord();
				}
			}
			output["collection.tsv"] = collectionTsv.ToString();
		}
		else
		{
			this._logger.LogWarning("Collection TSV cannot be created because of missing obb file.");
		}

		this._logger.LogInformation("Building difficulty.tsv...");
		StringBuilder difficultyTsv = new();
		using (CsvWriter writer = CsvWriter.FromStringBuilder(difficultyTsv, "\t"))
		{
			foreach (SongInfo song in nonMultiLangInfo.Songs)
			{
				if (song.Id.Contains("Introduction"))
					continue;

				writer.WriteField(song.Id[..^2]);
				writer.WriteField(song.Levels[Difficulty.EZ].ChartConstant);
				writer.WriteField(song.Levels[Difficulty.HD].ChartConstant);
				writer.WriteField(song.Levels[Difficulty.IN].ChartConstant);
				if (song.Levels.TryGetValue(Difficulty.AT, out SongLevel? at))
					writer.WriteField(at.ChartConstant);

				writer.NextRecord();
			}
		}
		output["difficulty.tsv"] = difficultyTsv.ToString();

		this._logger.LogInformation("Building info.tsv...");
		StringBuilder infoTsv = new();
		using (CsvWriter writer = CsvWriter.FromStringBuilder(infoTsv, "\t"))
		{
			foreach (SongInfo song in nonMultiLangInfo.Songs)
			{
				if (song.Id.Contains("Introduction"))
					continue;

				writer.WriteField(song.Id[..^2]);
				writer.WriteField(song.Name);
				writer.WriteField(song.Composer);
				writer.WriteField(song.Illustrator);
				writer.WriteField(song.Levels[Difficulty.EZ].Charter);
				writer.WriteField(song.Levels[Difficulty.HD].Charter);
				writer.WriteField(song.Levels[Difficulty.IN].Charter);
				if (song.Levels.TryGetValue(Difficulty.AT, out SongLevel? at))
					writer.WriteField(at.Charter);

				writer.NextRecord();
			}
		}
		output["info.tsv"] = infoTsv.ToString();

		// seriously why is it named tmp
		this._logger.LogInformation("Building tmp.tsv...");
		StringBuilder tmpTsv = new();
		using (CsvWriter writer = CsvWriter.FromStringBuilder(tmpTsv, "\t"))
		{
			foreach (Avatar avatar in nonMultiLangInfo.Avatars)
			{
				writer.WriteField(avatar.Name);
				writer.WriteField(avatar.AddressablePath[7..]);
				writer.NextRecord();
			}
		}
		output["tmp.tsv"] = tmpTsv.ToString();

		this._logger.LogInformation("Building avatar.txt, tips.txt...");
		string avatarTxt = string.Join('\n', nonMultiLangInfo.Avatars.Select(a => a.Name));
		string tipsTxt = string.Join('\n', multiLangInfo.Tips);
		output["avatar.txt"] = avatarTxt;
		output["tips.txt"] = tipsTxt;

		// TODO: add illustration, single txt

		return output;
	}
	#endregion

	#region Asset Extraction
	/// <summary>
	/// Create an <see cref="AssetExtractionContext"/> for asset extraction. 
	/// This requires an <see cref="AddressableBundleExtractor"/> to be available, and will use <see cref="InfoExtractor"/> if available to create avatar name to hash map.
	/// </summary>
	/// <param name="handler">File handler to decide where is it extracted.</param>
	/// <returns>The created <see cref="AssetExtractionContext"/>.</returns>
	public AssetExtractionContext CreateAssetExtractionContext(ExtractedFileHandler handler)
	{
		AddressableBundleExtractor addressableBundleExtractor = this.RequireAddressableBundleExtractor();

		List<Avatar>? avatars = this.InfoExtractor?.ExtractAvatars();
		if (avatars is null)
		{
			this._logger.LogWarning("Pre-extracted avatar list is not available because of missing apk or class data file.");
		}
		return new AssetExtractionContext(
			addressableBundleExtractor,
			avatars,
			this._logger,
			this.ExtractOptions,
			handler);
	}
	#endregion
}
