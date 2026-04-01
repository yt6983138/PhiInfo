using AssetsTools.NET;
using PhiInfo.Core.Type;

namespace PhiInfo.Core;

public partial class PhiInfo
{
	public List<SongInfo> ExtractSongInfo()
	{
		List<SongInfo> result = [];

		AssetTypeValueField gameInfoField = this.FindMonoBehaviour(
			this._level0Inst,
				"GameInformation"
			) ?? throw new Exception("GameInformation MonoBehaviour not found");

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
		List<Folder> result = [];

		AssetTypeValueField collectionField = this.FindMonoBehaviour(
			this._level22Inst,
				"SaturnOSControl"
			) ?? throw new Exception("SaturnOSControl MonoBehaviour not found");

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
					file["name"][Lang].AsString,
					file["date"].AsString,
					file["supervisor"][Lang].AsString,
					file["category"].AsString,
					file["content"][Lang].AsString,
					file["properties"][Lang].AsString
				));
			}

			result.Add(new Folder(
				folder["title"][Lang].AsString,
				folder["subTitle"][Lang].AsString,
				folder["cover"].AsString,
				files
			));
		}

		return result;
	}

	public List<Avatar> ExtractAvatars()
	{
		List<Avatar> result = [];

		AssetTypeValueField avatarField = this.FindMonoBehaviour(
			this._level0Inst,
				"GetCollectionControl"
			) ?? throw new Exception("GetCollectionControl MonoBehaviour not found");

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

		AssetTypeValueField tipsField = this.FindMonoBehaviour(
			this._level0Inst,
				"TipsProvider"
			) ?? throw new Exception("TipsProvider MonoBehaviour not found");

		AssetTypeValueField tipsArray = tipsField["tips"]["Array"];

		for (int i = 0; i < tipsArray.Children.Count; i++)
		{
			AssetTypeValueField tipsLang = tipsArray[i];
			if (tipsLang["language"].AsInt == LangId)
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

		AssetTypeValueField chapterField = this.FindMonoBehaviour(
			this._level0Inst,
				"GameInformation"
			) ?? throw new Exception("GameInformation MonoBehaviour not found");

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

	public AllInfo ExtractAll()
	{
		return new AllInfo(
			this.ExtractSongInfo(),
			this.ExtractCollection(),
			this.ExtractAvatars(),
			this.ExtractTips(),
			this.ExtractChapters());
	}
}