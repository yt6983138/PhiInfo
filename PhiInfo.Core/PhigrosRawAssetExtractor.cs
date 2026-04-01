using AssetsTools.NET;
using PhiInfo.Core.Models.Raw;

namespace PhiInfo.Core;

public partial class PhigrosRawAssetExtractor : IDisposable
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

	public static PhigrosRawAssetExtractor FromApk(Stream apk, Stream? obb, Stream classDataTPK)
	{
		PhigrosAssetHelper.GetRawExtractionRequiredData(apk,
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
		for (int i = 0; i < comboArray.Children.Count; i++)
		{
			AssetTypeValueField combo = comboArray[i];
			string songId = combo["songsId"].AsString;
			List<int> allComboList = [];
			AssetTypeValueField allComboField = combo["allComboNum"]["Array"];
			for (int j = 0; j < allComboField.Children.Count; j++)
				allComboList.Add(allComboField[j].AsInt);
			comboDict[songId] = allComboList;
		}

		for (int i = 0; i < songField.Children.Count; i++)
		{
			AssetTypeValueField songArray = songField[i]["Array"];
			for (int j = 0; j < songArray.Children.Count; j++)
			{
				AssetTypeValueField song = songArray[j];
				string songId = song["songsId"].AsString;

				List<int> allComboNum = comboDict.TryGetValue(songId, out List<int>? value) ? value : [];
				AssetTypeValueField levelsArray = song["levels"]["Array"];
				AssetTypeValueField chartersArray = song["charter"]["Array"];
				AssetTypeValueField difficultiesArray = song["difficulty"]["Array"];

				Dictionary<string, SongLevel> levelsDict = [];

				for (int k = 0; k < difficultiesArray.Children.Count; k++)
				{
					double diff = difficultiesArray[k].AsDouble;
					if (diff == 0) continue;

					string levelName = levelsArray[k].AsString;
					string charter = chartersArray[k].AsString;
					int allCombo = k < allComboNum.Count ? allComboNum[k] : 0;

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

		List<Folder> result = [];

		AssetTypeValueField collectionField = this._monoBehaviourFinder.FindMonoBehaviour(this._level22, "SaturnOSControl");

		AssetTypeValueField folders = collectionField["folders"]["Array"];

		for (int i = 0; i < folders.Children.Count; i++)
		{
			AssetTypeValueField folder = folders[i];
			AssetTypeValueField filesArray = folder["files"]["Array"];
			List<FileItem> files = [];

			for (int j = 0; j < filesArray.Children.Count; j++)
			{
				AssetTypeValueField file = filesArray[j];

				files.Add(new FileItem(
					file["key"].AsString,
					file["subIndex"].AsInt,
					file["name"][this.ExtractLanguage.GetStringId()].AsString,
					file["date"].AsString,
					file["supervisor"][this.ExtractLanguage.GetStringId()].AsString,
					file["category"].AsString,
					file["content"][this.ExtractLanguage.GetStringId()].AsString,
					file["properties"][this.ExtractLanguage.GetStringId()].AsString
				));
			}

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
		List<Avatar> result = [];

		AssetTypeValueField avatarField = this._monoBehaviourFinder.FindMonoBehaviour(this._level0, "GetCollectionControl");

		AssetTypeValueField avatarsArray = avatarField["avatars"]["Array"];

		for (int i = 0; i < avatarsArray.Children.Count; i++)
		{
			AssetTypeValueField avatar = avatarsArray[i];
			result.Add(new Avatar(
				avatar["name"].AsString,
				avatar["addressableKey"].AsString
			));
		}

		return result;
	}

	public List<string> ExtractTips()
	{
		List<string> result = [];

		AssetTypeValueField tipsField = this._monoBehaviourFinder.FindMonoBehaviour(this._level0, "TipsProvider");

		AssetTypeValueField tipsArray = tipsField["tips"]["Array"];

		for (int i = 0; i < tipsArray.Children.Count; i++)
		{
			AssetTypeValueField tipsLang = tipsArray[i];
			if (tipsLang["language"].AsInt == (int)this.ExtractLanguage)
			{
				for (int j = 0; j < tipsLang["tips"]["Array"].Children.Count; j++)
				{
					result.Add(tipsLang["tips"]["Array"][j].AsString);
				}

				break;
			}
		}

		return result;
	}

	public List<ChapterInfo> ExtractChapters()
	{
		List<ChapterInfo> result = [];

		AssetTypeValueField chapterField = this._monoBehaviourFinder.FindMonoBehaviour(this._level0, "GameInformation");

		AssetTypeValueField chaptersArray = chapterField["chapters"]["Array"];

		for (int i = 0; i < chaptersArray.Children.Count; i++)
		{
			AssetTypeValueField chapter = chaptersArray[i];
			string code = chapter["chapterCode"].AsString;
			AssetTypeValueField songInfo = chapter["songInfo"];
			string banner = songInfo["banner"].AsString;
			AssetTypeValueField songsArray = songInfo["songs"]["Array"];
			List<string> songs = [];
			for (int j = 0; j < songsArray.Children.Count; j++)
			{
				songs.Add(songsArray[j]["songsId"].AsString);
			}

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