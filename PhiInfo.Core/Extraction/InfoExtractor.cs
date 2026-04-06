using AssetsTools.NET;
using LibCpp2IL;
using LibCpp2IL.Metadata;
using PhigrosLibraryCSharp.GameRecords;
using PhiInfo.Core.Models;
using PhiInfo.Core.Models.Information;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Extracts information from Phigros assets. Please see warning at 
/// <see cref="InfoExtractor(Stream, Stream, Stream?, byte[], byte[], Stream)"/>.
/// </summary>
public class InfoExtractor : IDisposable
{

	private readonly AssetsFile _level0;
	private readonly AssetsFile? _level22;
	private readonly MonoBehaviourFinder _monoBehaviourFinder;

	private readonly AssetsFileReader _level0Reader;
	private readonly AssetsFileReader? _level22Reader;

	/// <summary>
	/// Checks if this instance is disposed. Accessing any method after this is true 
	/// may cause <see cref="ObjectDisposedException"/> or <see cref="NullReferenceException"/>.
	/// </summary>
	public bool Disposed { get; private set; }

	/// <summary>
	/// Extracts collections and tips in the specified language. Default is Chinese.
	/// </summary>
	public Language ExtractLanguage { get; set; } = Language.Chinese;

	/// <summary>
	/// Warning: Newing multiple instances of this class (concurrently) may cause unexpected behaviour,
	/// because the internal <see cref="MonoBehaviourFinder"/> new some Cpp2Il classes which have static calls to 
	/// <see cref="LibCpp2IlMain"/> class, which may cause some static fields to be overridden. Recommend to new 
	/// only one instance of this class and reuse it to extract all information you need, or new multiple 
	/// instances sequentially.
	/// 
	/// All streams passed to this constructor should be seekable and support reading, and they will be 
	/// disposed when the InfoExtractor is disposed. The <paramref name="level22"/> stream can be null, but if it is null, 
	/// collection data cannot be extracted.
	/// </summary>
	/// <param name="globalGameManagers">The <c>assets/bin/Data/globalgamemanagers.assets</c> file. (In apk)</param>
	/// <param name="level0">The <c>assets/bin/Data/level0</c> file. (In apk)</param>
	/// <param name="level22">The <c>assets/bin/Data/level22.split*</c> files. (In obb) Need to be merged. 
	/// If not supplied collections cannot be extracted.</param>
	/// <param name="il2CppSo">The <c>lib/arm64-v8a/libil2cpp.so</c> file. (In apk)</param>
	/// <param name="globalMetadata">The <c>assets/bin/Data/Managed/Metadata/global-metadata.dat</c> file. (In apk)</param>
	/// <param name="classDataTPK">Class database file. Can be obtained 
	/// <a href="https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/uncompressed_file.zip">here</a>.</param>
	public InfoExtractor(
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
	/// <summary>
	/// Finalizes this instance. Disposes this instance as well.
	/// </summary>
	~InfoExtractor()
	{
		this.Dispose();
	}

	private static Il2CppFieldDefinition GetFieldInConstantsClass(string fieldName)
	{

		Il2CppMetadata meta = LibCpp2IlMain.TheMetadata
					   ?? throw new InvalidOperationException("Cpp2Il is not initialized.");

		Il2CppAssemblyDefinition assembly = meta.AssemblyDefinitions
							   .FirstOrDefault(a => a.AssemblyName.Name == "Assembly-CSharp")
						   ?? throw new InvalidDataException("Cannot find Assembly-CSharp.");

		Il2CppTypeDefinition type = assembly.Image.Types?
						   .FirstOrDefault(t => t.FullName == "Constants")
					   ?? throw new InvalidDataException("Cannot find Constants class.");

		return type.Fields?
			.FirstOrDefault(f => f.Name == fieldName)
			?? throw new ArgumentException($"Cannot find field {fieldName}.", nameof(fieldName));
	}

	/// <summary>
	/// Constructs an <see cref="InfoExtractor"/> from apk and obb streams. Please see warning at 
	/// <see cref="InfoExtractor(Stream, Stream, Stream?, byte[], byte[], Stream)" />.
	/// </summary>
	/// <param name="apk">Apk file stream.</param>
	/// <param name="obb">Obb file stream.</param>
	/// <param name="classDataTPK">Class data database file. See <see cref="InfoExtractor(Stream, Stream, Stream?, byte[], byte[], Stream)"/> 
	/// classDataTpk param.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A constructed <see cref="InfoExtractor"/>.</returns>
	public static async Task<InfoExtractor> FromApkAndObbAsync(Stream apk, Stream? obb, Stream classDataTPK, CancellationToken ct = default)
	{
		(Stream GlobalGameManagers, Stream Level0, byte[] Il2CppSo, byte[] GlobalMetadata) =
			await PhigrosAssetHelper.GetInformationExtractionRequiredDataAsync(apk, ct);

		return new InfoExtractor(
			GlobalGameManagers,
			Level0,
			obb is null ? null : await PhigrosAssetHelper.GetLevel22FromObbAsync(obb, ct),
			Il2CppSo,
			GlobalMetadata,
			classDataTPK
		);
	}

	/// <inheritdoc/>
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

#pragma warning disable CA1822 // Mark members as static
	/// <summary>
	/// Get the Phigros version in integer form. This is intentionally made static as it requires
	/// Cpp2Il to be initialized (which is done when newing a instance of this class).
	/// </summary>
	/// <returns>Phigros version in integer form.</returns>
	/// <exception cref="InvalidOperationException">Thrown if Cpp2Il is not initialized. It is initialized when
	/// anything new a instance of <see cref="MonoBehaviourFinder"/>.</exception>
	/// <exception cref="InvalidDataException">Thrown if failed to find Phigros version data.</exception>
	public int GetVersionInteger()
	{
		Il2CppFieldDefinition field = GetFieldInConstantsClass("IntVersion");

		object? defaultValue = field.DefaultValue?.Value;

		if (field.DefaultValue?.Value is int intValue)
			return intValue;

		throw new InvalidDataException($"Invalid version type: {defaultValue?.GetType()}");
	}
	/// <summary>
	/// Get the Phigros version in string form. This is intentionally made not static as it requires
	/// Cpp2Il to be initialized (which is done when newing a instance of this class).
	/// </summary>
	/// <returns>Phigros version in string form.</returns>
	/// <exception cref="InvalidOperationException">Thrown if Cpp2Il is not initialized. It is initialized when
	/// anything new a instance of <see cref="MonoBehaviourFinder"/>.</exception>
	/// <exception cref="InvalidDataException">Thrown if failed to find Phigros version data.</exception>
	public string GetVersionString()
	{
		Il2CppFieldDefinition field = GetFieldInConstantsClass("Version");

		object? defaultValue = field.DefaultValue?.Value;

		if (field.DefaultValue?.Value is string str)
			return str;

		throw new InvalidDataException($"Invalid version type: {defaultValue?.GetType()}");
	}
	/// <summary>
	/// Get the Phigros <c>RegionType</c>, if it's <c>RegionType.IO</c> (international), this will return true. 
	/// This is intentionally made not static as it requires Cpp2Il to be initialized (which is done when 
	/// newing a instance of this class).
	/// </summary>
	/// <returns>This package of Phigros is international or not.</returns>
	/// <exception cref="InvalidOperationException">Thrown if Cpp2Il is not initialized. It is initialized when
	/// anything new a instance of <see cref="MonoBehaviourFinder"/>.</exception>
	/// <exception cref="InvalidDataException">Thrown if failed to find Phigros version data.</exception>
	public bool GetIsInternational()
	{
		Il2CppFieldDefinition field = GetFieldInConstantsClass("RegionType");

		object? defaultValue = field.DefaultValue?.Value;

		if (field.DefaultValue?.Value is int flag)
			return flag == 1;

		throw new InvalidDataException($"Invalid RegionType type: {defaultValue?.GetType()}");
	}
#pragma warning restore CA1822 // Mark members as static

	/// <summary>
	/// Extract information for each song.
	/// </summary>
	/// <returns>A list of infomation about each song.</returns>
	public List<SongInfo> ExtractSongInfo()
	{
		List<SongInfo> result = [];

		AssetTypeValueField gameInfoField = this._monoBehaviourFinder.FindMonoBehaviour(this._level0, "GameInformation");
		AssetTypeValueField songField = gameInfoField["song"];

		foreach (AssetTypeValueField songArrayField in songField)
		{
			foreach (AssetTypeValueField song in songArrayField["Array"])
			{
				string songId = song["songsId"].AsString;

				AssetTypeValueField levelsArray = song["levels"]["Array"];
				AssetTypeValueField chartersArray = song["charter"]["Array"];
				AssetTypeValueField difficultiesArray = song["difficulty"]["Array"];

				Dictionary<Difficulty, SongLevel> levelsDict = [];
				for (int i = 0; i < difficultiesArray.Children.Count; i++)
				{
					double diff = difficultiesArray[i].AsDouble;
					if (diff == 0) continue;

					if (!Enum.TryParse(levelsArray[i].AsString, out Difficulty difficulty))
						continue;

					string charter = chartersArray[i].AsString;

					levelsDict[difficulty] = new SongLevel(
						charter,
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

	/// <summary>
	/// Extract collections in <see cref="ExtractLanguage"/>.
	/// This require level22, so if it is not supplied in the constructor, 
	/// this method will throw <see cref="InvalidOperationException"/>.
	/// </summary>
	/// <returns>A list of collections. Phigros organizes them in a Folder/File structure.</returns>
	/// <exception cref="InvalidOperationException">Thrown if level22 is not supplied in constructor.</exception>
	public List<Folder> ExtractCollections()
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

	/// <summary>
	/// Extract avatar information.
	/// </summary>
	/// <returns>A list of avatar information.</returns>
	public List<Avatar> ExtractAvatars()
	{
		AssetTypeValueField avatarField = this._monoBehaviourFinder.FindMonoBehaviour(this._level0, "GetCollectionControl");

		return avatarField["avatars"]["Array"]
			.Select(x => new Avatar(x["name"].AsString, x["addressableKey"].AsString))
			.ToList();
	}

	/// <summary>
	/// Extract tips in <see cref="ExtractLanguage"/>.
	/// </summary>
	/// <returns>A list of tips (the ones you see when you load a song)</returns>
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

	/// <summary>
	/// Extract chapter information, such as their name and songs etc.
	/// </summary>
	/// <returns>A list of chapter information.</returns>
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
}