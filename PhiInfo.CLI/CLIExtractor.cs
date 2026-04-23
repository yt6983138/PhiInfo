using CsvHelper;
using Microsoft.Extensions.Logging;
using PhigrosLibraryCSharp.GameRecords;
using PhiInfo.Core.Extraction;
using PhiInfo.Core.Models;
using PhiInfo.Core.Models.Information;
using SixLabors.ImageSharp;
using System.Text;

namespace PhiInfo.CLI;

public class CLIExtractor
{
	public const Language AllLanguage = unchecked((Language)0xFFFFFFFF);

	private readonly ILogger<CLIExtractor> _logger;

	public InfoExtractor? InfoExtractor { get; }
	public AddressableBundleExtractor? AddressableBundleExtractor { get; }
	public ExtractOptions ExtractOptions { get; }

	#region Constructor and Factory
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
	public NonMultiLanguageInfos ExtractNonLanguageSpecificInfo()
	{
		InfoExtractor infoExtractor = this.RequireInfoExtractor();

		this._logger.LogInformation("Extracting Song Info...");
		List<SongInfo> songInfo = infoExtractor.ExtractSongInfo();
		this._logger.LogInformation("Extracting Avatars...");
		List<Avatar> avatars = infoExtractor.ExtractAvatars();
		this._logger.LogInformation("Extracting Chapters...");
		List<ChapterInfo> chapters = infoExtractor.ExtractChapters();

		return new(songInfo, avatars, chapters);
	}
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
