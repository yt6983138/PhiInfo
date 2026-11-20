using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Describes where Unity serialized files (<c>globalgamemanagers.assets</c>, <c>level*</c>, <c>sharedassets*.assets</c>)
/// of the game come from, hiding the difference between the packed layout (<c>data.unity3d</c> bundle) and 
/// the loose layout (<c>level0</c>, <c>level1</c>... directly inside <c>assets/bin/Data</c>).
/// </summary>
/// <param name="Manager">The assets manager that owns the loaded files. It must have its class database and 
/// <c>MonoTempGenerator</c> initialized.</param>
/// <param name="GlobalGameManagers">The loaded <c>globalgamemanagers.assets</c>, which contains the MonoScripts.</param>
/// <param name="AssetFiles">Lazily loads every serialized file that should be searched for MonoBehaviours. 
/// Should be enumerated only once, and only from one thread.</param>
/// <param name="UnloadUnusedFiles">If true, files yielded by <paramref name="AssetFiles"/> that turned out to 
/// be irrelevant will be unloaded immediately to save memory.</param>
/// <param name="Disposer">Cleanup action for underlying resources (streams etc.).</param>
public sealed record UnityAssetContext(
	AssetsManager Manager,
	AssetsFileInstance GlobalGameManagers,
	IEnumerable<AssetsFileInstance> AssetFiles,
	bool UnloadUnusedFiles,
	Action Disposer)
{
	/// <summary>
	/// Creates a context from a <c>data.unity3d</c> bundle.
	/// </summary>
	/// <param name="manager">The assets manager that loaded the bundle.</param>
	/// <param name="dataUnity3d">The data.unity3d bundle file instance.</param>
	/// <param name="disposer">Cleanup action for underlying resources.</param>
	/// <returns>The created context.</returns>
	public static UnityAssetContext FromBundle(AssetsManager manager, BundleFileInstance dataUnity3d, Action disposer)
	{
		AssetsFileInstance globalGameManagers = manager.LoadAssetsFileFromBundle(dataUnity3d, "globalgamemanagers.assets", true);
		return new(manager, globalGameManagers, EnumerateBundle(manager, dataUnity3d), false, disposer);
	}

	private static IEnumerable<AssetsFileInstance> EnumerateBundle(AssetsManager manager, BundleFileInstance dataUnity3d)
	{
		foreach (AssetBundleDirectoryInfo? folder in dataUnity3d.file.BlockAndDirInfo.DirectoryInfos)
		{
			AssetsFileInstance? assetFile = manager.LoadAssetsFileFromBundle(dataUnity3d, folder.Name);
			if (assetFile is null) continue;

			yield return assetFile;
		}
	}
}
