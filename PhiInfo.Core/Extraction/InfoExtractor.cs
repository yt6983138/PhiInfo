using AssetsTools.NET;
using AssetsTools.NET.Extra;
using LibCpp2IL;
using LibCpp2IL.Metadata;
using PhigrosLibraryCSharp.CloudSave;
using PhiInfo.Core.Models;
using PhiInfo.Core.Models.Information;
using System.Collections.Frozen;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Extracts information from Phigros assets. Please see warning at 
/// <see cref="InfoExtractor(AssetsManager, BundleFileInstance, Action)"/>.
/// </summary>
public class InfoExtractor : IDisposable
{
	private record struct CollectedMonoBehaviour(AssetsFileInstance Instance, AssetTypeValueField Behaviour);

	private const string GameInformationBehaviourName = "GameInformation";
	private const string CollectionDatabaseBehaviourName = "CollectionDatabase";
	private const string SaturnOSControlBehaviourName = "SaturnOSControl";
	private const string GetCollectionControlBehaviourName = "GetCollectionControl";
	private const string TipsProviderBehaviourName = "TipsProvider";

	private static readonly FrozenSet<string> _monoBehaviourNames = [
		GameInformationBehaviourName,
		CollectionDatabaseBehaviourName,
		SaturnOSControlBehaviourName,
		GetCollectionControlBehaviourName,
		TipsProviderBehaviourName
	];

	private readonly Dictionary<string, CollectedMonoBehaviour> _collectedMonoBehaviours;
	private readonly AssetsManager _assetsManager;
	private readonly MonoBehaviourFinder _behaviourFinder;
	private readonly BundleFileInstance _dataUnity3d;
	private readonly Action _extraDisposer;

	/// <summary>
	/// Checks if this instance is disposed. Accessing any method after this is true 
	/// may cause <see cref="ObjectDisposedException"/> or <see cref="NullReferenceException"/>.
	/// </summary>
	public bool Disposed { get; private set; }

	/// <summary>
	/// Extracts collections and tips in the specified language. Default is Chinese.
	/// </summary>
	public Language ExtractLanguage { get; set; } = Language.SimplifiedChinese;

	/// <summary>
	/// Warning: Newing multiple instances of this class (concurrently) may cause unexpected behaviour,
	/// because the internal <see cref="MonoBehaviourFinder"/> new some Cpp2Il classes which have static calls to 
	/// <see cref="LibCpp2IlMain"/> class, which may cause some static fields to be overridden. Recommend to new 
	/// only one instance of this class and reuse it to extract all information you need, or new multiple 
	/// instances sequentially.
	/// 
	/// Resources will be disposed when the InfoExtractor is disposed.
	/// </summary>
	/// <param name="manager">The assets manager containing loaded assets.</param>
	/// <param name="dataUnity3d">The data.unity3d bundle file instance.</param>
	/// <param name="disposer">A custom dispose action for cleanup of resources like streams.</param>
	public InfoExtractor(
		AssetsManager manager,
		BundleFileInstance dataUnity3d,
		Action disposer)
	{
		this._assetsManager = manager;
		this._behaviourFinder = new(dataUnity3d, manager);
		this._dataUnity3d = dataUnity3d;
		this._collectedMonoBehaviours = CollectMonoBehaviours(manager, dataUnity3d, this._behaviourFinder);
		this._extraDisposer = disposer;
	}

	private static Dictionary<string, CollectedMonoBehaviour> CollectMonoBehaviours(AssetsManager manager, BundleFileInstance dataUnity3d, MonoBehaviourFinder finder)
	{
		Dictionary<string, CollectedMonoBehaviour> result = [];
		foreach (AssetBundleDirectoryInfo? folder in dataUnity3d.file.BlockAndDirInfo.DirectoryInfos)
		{
			AssetsFileInstance assetFile = manager.LoadAssetsFileFromBundle(dataUnity3d, folder.Name);
			if (assetFile is null) continue;

			foreach (AssetFileInfo? behaviour in assetFile.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
			{
				MonoBehaviourInfo? realBehaviour = finder.TryGetMonoBehaviourInfo(assetFile.file, behaviour, x => _monoBehaviourNames.Contains(x["m_Name"]?.AsString ?? ""));
				if (realBehaviour is null) continue;

				// here we just override the previous one if there are multiple with the same name, because we don't expect that to happen
				// even it happens, it will be likely that the behaviour is not the one we want, critical behaviours should be unique
				result[realBehaviour.GameManagerInfo["m_Name"]?.AsString ?? ""] = new(assetFile, realBehaviour.Behaviour);
			}
		}

		return result;
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
	/// Constructs an <see cref="InfoExtractor"/> from package streams. Please see warning at 
	/// <see cref="InfoExtractor(AssetsManager, BundleFileInstance, Action)" />.
	/// 
	/// Searches through all provided packages to find the required assets.
	/// </summary>
	/// <param name="classDataTPK">Class data database file. Can be obtained 
	/// <a href="https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/uncompressed_file.zip">here</a>.</param>
	/// <param name="locator">A multi-package file locator to search for required assets.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A constructed <see cref="InfoExtractor"/>.</returns>
	public static async Task<InfoExtractor> FromPackagesAsync(Stream classDataTPK, MultiPackageFileLocator locator, CancellationToken ct = default)
	{
		(AssetsManager? manager, BundleFileInstance? bundle, Action? disposer) = await locator.ExtractDataUnity3DFile(classDataTPK);
		return new(manager, bundle, disposer);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (this.Disposed) return;
		this.Disposed = true;

		GC.SuppressFinalize(this);

		// here we don't unload dataUnity3d because it may not be managed by this
		this._behaviourFinder.Dispose();
		this._extraDisposer.Invoke();
	}

#pragma warning disable CA1822 // Mark members as static
	/// <summary>
	/// Get the Phigros version in integer form. This is intentionally made not static as it requires
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

		AssetTypeValueField gameInfoField = this._collectedMonoBehaviours["GameInformation"].Behaviour;
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
						(float)Math.Round(diff, 1)
					);
				}

				if (levelsDict.Count == 0) continue;

				AssetTypeValueField cnLimitedField = song["isCnLimited"];
				result.Add(new SongInfo(
					songId,
					song["songsKey"].AsString,
					song["songsName"].AsString,
					song["composer"].AsString,
					song["illustrator"].AsString,
					Math.Round(song["previewTime"].AsDouble, 2),
					Math.Round(song["previewEndTime"].AsDouble, 2),
					!cnLimitedField.IsDummy && cnLimitedField.AsBool,
					levelsDict
				));
			}
		}

		return result;
	}

	/// <summary>
	/// Extract collections in <see cref="ExtractLanguage"/>.
	/// This require sharedassets22, so if it is not supplied in the constructor, 
	/// this method will throw <see cref="InvalidOperationException"/>.
	/// </summary>
	/// <returns>A list of collections. Phigros organizes them in a Folder/File structure.</returns>
	/// <exception cref="InvalidOperationException">Thrown if sharedassets22 is not supplied in constructor.</exception>
	public List<Folder> ExtractCollections()
	{
		AssetTypeValueField collectionDatabase = this._collectedMonoBehaviours["CollectionDatabase"].Behaviour;
		AssetTypeValueField saturnOSControl = this._collectedMonoBehaviours["SaturnOSControl"].Behaviour;

		FileItem[] allFiles = collectionDatabase["items"]["Array"]
			.Select(x => new FileItem(
				x["key"].AsString,
				x["subIndex"].AsInt,
				x["getSong"].AsInt,
				x["name"][this.ExtractLanguage.GetStringId()].AsString,
				x["date"].AsString,
				x["supervisor"][this.ExtractLanguage.GetStringId()].AsString,
				x["category"].AsString,
				x["content"][this.ExtractLanguage.GetStringId()].AsString,
				x["properties"][this.ExtractLanguage.GetStringId()].AsString
			))
			.ToArray();

		List<Folder> folders = saturnOSControl["folders"]["Array"]
			.Select(x => new Folder(
				x["title"][this.ExtractLanguage.GetStringId()].AsString,
				x["subTitle"][this.ExtractLanguage.GetStringId()].AsString,
				x["startIndex"].AsInt,
				x["endIndex"].AsInt,
				x["excludedFiles"]["Array"].Select(f => new ExcludedFileRange(f["start"].AsInt, f["end"].AsInt)).ToArray(),
				x["includedIsolatedFiles"]["Array"].Select(f => new FileReference(f["key"].AsString, f["subIndex"].AsInt)).ToArray(),
				x["cover"].AsString,
				x["fakeCoverFlag"].AsString,
				x["fakeCoverCode"].AsString,
				x["fakeCoverBlurCode"].AsString,
				x["allNum"].AsInt,
				[]
			)).ToList();

		foreach (Folder folder in folders)
		{
			foreach (FileItem file in allFiles)
			{
				int getSong = Math.Abs(file.GetSong);

				if (folder.IncludedIsolatedFiles.Any(x => x.Key == file.Key && x.SubIndex == file.SubIndex))
				{
					file.Classified = true;
					folder.Files.Add(file);
					continue;
				}

				if (folder.ExcludedFileRanges.Any(x => getSong >= x.StartIndex && getSong <= x.EndIndex))
					continue;

				if (getSong < folder.StartIndex || getSong > folder.EndIndex)
					continue;

				file.Classified = true;
				folder.Files.Add(file);
			}
		}

		Folder unclassifiedTemplate = Folder.UnclassifiedTemplate with
		{
			Files = allFiles.Where(x => !x.Classified).ToList()
		};
		folders.Add(unclassifiedTemplate);

		return folders;
	}

	/// <summary>
	/// Extract avatar information.
	/// </summary>
	/// <returns>A list of avatar information.</returns>
	public List<Avatar> ExtractAvatars()
	{
		AssetTypeValueField avatarField = this._collectedMonoBehaviours["GetCollectionControl"].Behaviour;

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

		AssetTypeValueField tipsField = this._collectedMonoBehaviours["TipsProvider"].Behaviour;

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

		AssetTypeValueField chapterField = this._collectedMonoBehaviours["GameInformation"].Behaviour;

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