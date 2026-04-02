using AssetsTools.NET;
using PhiInfo.Core.Models.Information;

namespace PhiInfo.Core;

/// <summary>
/// this class is NOT thread and async safe
/// </summary>
public class PhigrosRawAssetExtractor : IDisposable // TODO: check if this class can be made thread and async safe, this class calls some static cpp2il methods so it blow up
{

	private readonly AssetsFile _level0;
	private readonly AssetsFile? _level22;
	private readonly MonoBehaviourFinder _monoBehaviourFinder;

	private readonly AssetsFileReader _level0Reader;
	private readonly AssetsFileReader? _level22Reader;

	public bool Disposed { get; private set; }

	public Language ExtractLanguage { get; set; } = Language.Chinese;

	/// <summary>
	/// all stream must be readable and seekable
	/// </summary>
	/// <param name="globalGameManagers"></param>
	/// <param name="level0"></param>
	/// <param name="level22">need to be merged from split files, if not supplied collection cannot be extracted</param>
	/// <param name="il2CppSo"></param>
	/// <param name="globalMetadata"></param>
	/// <param name="classDataTPK"></param>
	public PhigrosRawAssetExtractor(
		Stream globalGameManagers,
		Stream level0,
		Stream? level22,
		byte[] il2CppSo,
		byte[] globalMetadata,
		Stream classDataTPK)
	{
		AssetsFileReader level0Reader = new(level0);
		this._level0Reader = level0Reader;
		this._level0 = new();
		this._level0.Read(level0Reader);

		if (level22 is not null)
		{
			AssetsFileReader level22Reader = new(level22);
			this._level22Reader = level22Reader;
			this._level22 = new();
			this._level22.Read(level22Reader);
		}

		this._monoBehaviourFinder = new MonoBehaviourFinder(
			globalGameManagers,
			il2CppSo,
			globalMetadata,
			classDataTPK
		);
	}
	~PhigrosRawAssetExtractor()
	{
		this.Dispose();
	}

	public static PhigrosRawAssetExtractor FromApkAndObb(Stream apk, Stream? obb, Stream classDataTPK)
	{
		PhigrosAssetHelper.GetInformationExtractionRequiredData(apk,
			out Stream globalGameManagers,
			out Stream level0,
			out byte[] il2CppSo,
			out byte[] globalMetadata);
		return new PhigrosRawAssetExtractor(
			globalGameManagers,
			level0,
			obb is null ? null : PhigrosAssetHelper.GetLevel22FromObb(obb),
			il2CppSo,
			globalMetadata,
			classDataTPK
		);
	}

	public void Dispose()
	{
		if (this.Disposed) return;
		this.Disposed = true;

		GC.SuppressFinalize(this);

		this._level0Reader.Dispose();
		this._level22Reader?.Dispose();
		this._level0.Close();
		this._level22?.Close();
		this._monoBehaviourFinder.Dispose();
	}

	public List<SongInfo> ExtractSongInfo()
	{
		List<SongInfo> result = [];

		AssetTypeValueField gameInfoField = this._monoBehaviourFinder.FindMonoBehaviour(this._level0, "GameInformation");

		AssetTypeValueField songField = gameInfoField["song"];
		AssetTypeValueField comboArray = gameInfoField["songAllCombos"]["Array"];

		Dictionary<string, List<int>> comboDict = [];
		foreach (AssetTypeValueField combo in comboArray)
		{
			AssetTypeValueField allComboField = combo["allComboNum"]["Array"];

			List<int> allComboList = allComboField
				.Select(x => x.AsInt)
				.ToList();

			string songId = combo["songsId"].AsString;
			comboDict[songId] = allComboList;
		}

		foreach (AssetTypeValueField songArrayField in songField)
		{
			foreach (AssetTypeValueField song in songArrayField["Array"])
			{
				string songId = song["songsId"].AsString;

				AssetTypeValueField levelsArray = song["levels"]["Array"];
				AssetTypeValueField chartersArray = song["charter"]["Array"];
				AssetTypeValueField difficultiesArray = song["difficulty"]["Array"];

				Dictionary<string, SongLevel> levelsDict = [];
				List<int> allComboNum = comboDict.TryGetValue(songId, out List<int>? value) ? value : [];
				for (int i = 0; i < difficultiesArray.Children.Count; i++)
				{
					double diff = difficultiesArray[i].AsDouble;
					if (diff == 0) continue;

					string levelName = levelsArray[i].AsString;
					string charter = chartersArray[i].AsString;
					int allCombo = i < allComboNum.Count ? allComboNum[i] : 0; // TODO: fix some songs have combo count of 0

					levelsDict[levelName] = new SongLevel(
						charter,
						allCombo,
						Math.Round(diff, 1)
					);
				}

				if (levelsDict.Count == 0) continue;

				result.Add(new SongInfo(
					songId,
					song["songsKey"].AsString,
					song["songsName"].AsString,
					song["composer"].AsString,
					song["illustrator"].AsString,
					Math.Round(song["previewTime"].AsDouble, 2),
					Math.Round(song["previewEndTime"].AsDouble, 2),
					levelsDict
				));
			}
		}

		return result;
	}

	public List<Folder> ExtractCollection()
	{
		if (this._level22 is null)
			throw new InvalidOperationException("Level22 asset is required to extract collection data");

		AssetTypeValueField collectionField = this._monoBehaviourFinder.FindMonoBehaviour(this._level22, "SaturnOSControl");

		List<Folder> result = [];
		foreach (AssetTypeValueField folder in collectionField["folders"]["Array"])
		{
			List<FileItem> files = folder["files"]["Array"]
				.Select(file => new FileItem(
					file["key"].AsString,
					file["subIndex"].AsInt,
					file["name"][this.ExtractLanguage.GetStringId()].AsString,
					file["date"].AsString,
					file["supervisor"][this.ExtractLanguage.GetStringId()].AsString,
					file["category"].AsString,
					file["content"][this.ExtractLanguage.GetStringId()].AsString,
					file["properties"][this.ExtractLanguage.GetStringId()].AsString
				)).ToList();

			result.Add(new Folder(
				folder["title"][this.ExtractLanguage.GetStringId()].AsString,
				folder["subTitle"][this.ExtractLanguage.GetStringId()].AsString,
				folder["cover"].AsString,
				files
			));
		}

		return result;
	}

	public List<Avatar> ExtractAvatars()
	{
		AssetTypeValueField avatarField = this._monoBehaviourFinder.FindMonoBehaviour(this._level0, "GetCollectionControl");

		return avatarField["avatars"]["Array"]
			.Select(x => new Avatar(x["name"].AsString, x["addressableKey"].AsString))
			.ToList();
	}

	public List<string> ExtractTips()
	{

		AssetTypeValueField tipsField = this._monoBehaviourFinder.FindMonoBehaviour(this._level0, "TipsProvider");

		AssetTypeValueField tipsArray = tipsField["tips"]["Array"];
		AssetTypeValueField? tipsTargetLang = tipsArray
			.FirstOrDefault(x => x["language"].AsInt == (int)this.ExtractLanguage);

		if (tipsTargetLang is null)
			return [];

		return tipsTargetLang["tips"]["Array"]
			.Select(x => x.AsString)
			.ToList();

	}

	public List<ChapterInfo> ExtractChapters()
	{
		List<ChapterInfo> result = [];

		AssetTypeValueField chapterField = this._monoBehaviourFinder.FindMonoBehaviour(this._level0, "GameInformation");

		AssetTypeValueField chaptersArray = chapterField["chapters"]["Array"];

		foreach (AssetTypeValueField chapter in chaptersArray)
		{
			string code = chapter["chapterCode"].AsString;
			AssetTypeValueField songInfo = chapter["songInfo"];
			string banner = songInfo["banner"].AsString;
			AssetTypeValueField songsArray = songInfo["songs"]["Array"];
			List<string> songs = songsArray.Select(x => x["songsId"].AsString).ToList();

			result.Add(new ChapterInfo(code, banner, songs));
		}

		return result;
	}

	public PhigrosExtractedDataCollection ExtractAll()
	{
		return new PhigrosExtractedDataCollection(
			this.ExtractSongInfo(),
			this.ExtractCollection(),
			this.ExtractAvatars(),
			this.ExtractTips(),
			this.ExtractChapters());
	}
}