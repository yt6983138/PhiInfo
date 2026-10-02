using AssetsTools.NET;
using AssetsTools.NET.Extra;
using LibCpp2IL;
using System.Diagnostics.CodeAnalysis;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Represents a MonoBehaviour asset with its associated script and game manager information.
/// </summary>
/// <param name="MonoScriptInfo">The asset file info for the MonoScript.</param>
/// <param name="GameManagerInfo">The type value field containing game manager data.</param>
/// <param name="Behaviour">The type value field containing the MonoBehaviour data.</param>
public record class MonoBehaviourInfo(AssetFileInfo MonoScriptInfo, AssetTypeValueField GameManagerInfo, AssetTypeValueField Behaviour);
/// <summary>
/// Finds and reads <c>MonoBehaviour</c> instances from Unity asset files.
/// </summary>
public class MonoBehaviourFinder : IDisposable
{
	private bool _disposed;

	private readonly AssetsFileInstance _globalGameManagers;
	private readonly AssetsManager _assetsManager;

	/// <summary>
	/// Warning: Newing multiple instances of this class (concurrently) may cause unexpected behaviour,
	/// because the internal Cpp2Il classes have static calls to <see cref="LibCpp2IlMain"/> class, 
	/// which may cause some static fields to be overridden. Recommend to new only one instance of this 
	/// class and reuse it to extract all information you need, or new multiple instances sequentially.
	/// 
	/// Resources will be managed by the provided <see cref="AssetsManager"/>.
	/// </summary>
	/// <param name="dataUnity3d">The data.unity3d bundle file instance.</param>
	/// <param name="manager">The assets manager containing loaded assets and class database.</param>
	public MonoBehaviourFinder(
		BundleFileInstance dataUnity3d,
		AssetsManager manager)
	{
		this._globalGameManagers = manager.LoadAssetsFileFromBundle(dataUnity3d, "globalgamemanagers.assets", true);
		this._assetsManager = manager;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref this._disposed, true)) return;

		GC.SuppressFinalize(this);

		this._assetsManager.UnloadAssetsFile(this._globalGameManagers);
	}

	private AssetTypeValueField GetBaseField(
		AssetsFile file,
		AssetFileInfo info,
		bool monoFields)
	{
		lock (file.Reader)
		{
			long offset = info.GetAbsoluteByteOffset(file);

			AssetTypeTemplateField? template = this.GetTemplateBaseField(file, info, file.Reader, offset, monoFields);

			if (template == null)
				throw new InvalidDataException($"Failed to build template for type {info.TypeId}");

			RefTypeManager refMan = new();
			refMan.FromTypeTree(file.Metadata);

			return template.MakeValue(file.Reader, offset, refMan);
		}
	}

	private AssetTypeTemplateField? GetTemplateBaseField(
		AssetsFile file,
		AssetFileInfo info,
		AssetsFileReader? reader,
		long absByteStart,
		bool monoFields = false)
	{
		ushort scriptIndex = info.GetScriptIndex(file);

		AssetTypeTemplateField? baseField = null;

		// 1. 优先 TypeTree
		if (file.Metadata.TypeTreeEnabled)
		{
			TypeTreeType tt = file.Metadata.FindTypeTreeTypeByID(info.TypeId, scriptIndex);
			if (tt != null && tt.Nodes.Count > 0)
			{
				baseField = new AssetTypeTemplateField();
				baseField.FromTypeTree(tt);
			}
		}

		// 2. 回退到 ClassDatabase
		if (baseField == null)
		{
			ClassDatabaseType cldbType = this._assetsManager.ClassDatabase.FindAssetClassByID(info.TypeId);
			if (cldbType == null)
				return null;

			baseField = new AssetTypeTemplateField();
			baseField.FromClassDatabase(this._assetsManager.ClassDatabase, cldbType);
		}

		// 3. MonoBehaviour: 使用 MonoTempGenerator 补充字段
		if (info.TypeId == (int)AssetClassID.MonoBehaviour && monoFields && reader != null)
		{
			// 保存原始位置
			long originalPosition = reader.Position;
			reader.Position = absByteStart;

			// 创建临时的 RefTypeManager 用于读取值
			RefTypeManager tempRefMan = new();
			tempRefMan.FromTypeTree(file.Metadata);

			AssetTypeValueField mbBase = baseField.MakeValue(reader, absByteStart, tempRefMan);
			AssetPPtr scriptPtr = AssetPPtr.FromField(mbBase["m_Script"]);

			if (scriptPtr.IsNull())
				goto OutAndReset;

			// 确定 MonoScript 所在的文件
			AssetsFile monoScriptFile;
			if (scriptPtr.FileId == 0)
			{
				monoScriptFile = file;
			}
			else if (scriptPtr.FileId == 1)
			{
				monoScriptFile = this._globalGameManagers.file;
			}
			else
			{
				throw new InvalidDataException("Unsupported MonoScript FileID");
			}

			AssetFileInfo monoInfo = monoScriptFile.GetAssetInfo(scriptPtr.PathId);

			if (monoInfo is null)
				goto OutAndReset;

			if (!this.GetMonoScriptInfo(monoScriptFile, monoInfo, out string? assemblyName, out string? nameSpace, out string? className))
				goto OutAndReset;

			// 移除 .dll 扩展名
			if (assemblyName.EndsWith(".dll"))
				assemblyName = assemblyName[..^4];

			AssetTypeTemplateField newBase = this._assetsManager.MonoTempGenerator.GetTemplateField(
					baseField,
					assemblyName,
					nameSpace,
					className,
					new(file.Metadata.UnityVersion));

			if (newBase != null)
				baseField = newBase;

		OutAndReset:
			// 恢复原始位置
			reader.Position = originalPosition;
		}

		return baseField;
	}

	private bool GetMonoScriptInfo(
		AssetsFile file,
		AssetFileInfo info,
		[NotNullWhen(true)] out string? assemblyName,
		out string? nameSpace,
		[NotNullWhen(true)] out string? className)
	{
		assemblyName = null;
		nameSpace = null;
		className = null;

		AssetTypeTemplateField? template = this.GetTemplateBaseField(
				file,
				info,
				file.Reader,
				info.GetAbsoluteByteOffset(file),
				monoFields: false);

		if (template == null)
			return false;

		long offset = info.GetAbsoluteByteOffset(file);
		file.Reader.Position = offset;

		RefTypeManager refMan = new();
		refMan.FromTypeTree(file.Metadata);

		AssetTypeValueField valueField = template.MakeValue(file.Reader, offset, refMan);

		assemblyName = valueField["m_AssemblyName"]?.AsString;
		nameSpace = valueField["m_Namespace"]?.AsString;
		className = valueField["m_ClassName"]?.AsString;

		return !string.IsNullOrEmpty(assemblyName) && !string.IsNullOrEmpty(className) && nameSpace is not null;
	}

	/// <summary>
	/// Attempts to get MonoBehaviour information from an asset file.
	/// </summary>
	/// <param name="file">The asset file to search.</param>
	/// <param name="info">The asset file info for the MonoBehaviour.</param>
	/// <param name="filter">Optional filter function to match specific MonoBehaviours.</param>
	/// <returns>The MonoBehaviour information if found and passes the filter, otherwise <see langword="null"/>.</returns>
	public MonoBehaviourInfo? TryGetMonoBehaviourInfo(AssetsFile file, AssetFileInfo info, Func<AssetTypeValueField, bool>? filter = null)
	{
		if (info.TypeId != (int)AssetClassID.MonoBehaviour)
			return null;

		AssetTypeValueField baseField = this.GetBaseField(file, info, false);

		AssetTypeValueField scriptField = baseField["m_Script"];
		if (scriptField == null) return null;

		long msId = scriptField["m_PathID"].AsLong;
		if (msId == 0) return null;

		AssetFileInfo monoInfo = this._globalGameManagers.file.GetAssetInfo(msId);
		if (monoInfo == null) return null;

		AssetTypeValueField msBase = this.GetBaseField(this._globalGameManagers.file, monoInfo, false);

		if (filter?.Invoke(msBase) != true)
			return null;

		AssetTypeValueField behaviour = this.GetBaseField(file, info, true);
		return new(monoInfo, msBase, behaviour);
	}

	/// <summary>
	/// Attempts to find a <c>MonoBehaviour</c> by its associated script name.
	/// </summary>
	/// <param name="file">The asset file to search.</param>
	/// <param name="name">The script name to match.</param>
	/// <returns>The matching <see cref="AssetTypeValueField"/>, or <see langword="null"/> if not found.</returns>
	/// <exception cref="ObjectDisposedException">Thrown when this instance has already been disposed.</exception>
	public AssetTypeValueField? TryFindMonoBehaviour(AssetsFile file, string name)
	{
		if (this._disposed)
			throw new ObjectDisposedException(nameof(MonoBehaviourFinder));

		foreach (AssetFileInfo? info in file.AssetInfos)
		{
			MonoBehaviourInfo? behaviourInfo = this.TryGetMonoBehaviourInfo(file, info);
			if (behaviourInfo is null || behaviourInfo.GameManagerInfo["m_Name"]?.AsString != name) continue;

			return behaviourInfo.Behaviour;
		}

		return null;
	}
	/// <summary>
	/// Finds a <c>MonoBehaviour</c> by its associated script name.
	/// </summary>
	/// <param name="file">The asset file to search.</param>
	/// <param name="name">The script name to match.</param>
	/// <returns>The matching <see cref="AssetTypeValueField"/>.</returns>
	/// <exception cref="ArgumentException">Thrown when the requested <c>MonoBehaviour</c> is not found.</exception>
	public AssetTypeValueField FindMonoBehaviour(AssetsFile file, string name)
	{
		return this.TryFindMonoBehaviour(file, name)
			?? throw new ArgumentException($"Requested MonoBehaviour not found in the provided file.", nameof(name));
	}
}
