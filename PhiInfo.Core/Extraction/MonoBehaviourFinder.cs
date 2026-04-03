using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using LibCpp2IL;
using LibCpp2IL.Metadata;
using System.Diagnostics.CodeAnalysis;

namespace PhiInfo.Core.Extraction;

public class MonoBehaviourFinder : IDisposable
{
	private bool _disposed;

	private readonly AssetsFile _globalGameManagers = new();
	private readonly ClassDatabaseFile _classDatabase;

	private readonly AssetsFileReader _globalGameManagersReader;

	private readonly Cpp2IlTempGenerator _templateGenerator;

	/// <summary>
	/// Warning: Newing multiple instances of this class (concurrently) may cause unexpected behaviour,
	/// because the internal Cpp2Il classes have static calls to <see cref="LibCpp2IlMain"/> class, 
	/// which may cause some static fields to be overridden. Recommend to new only one instance of this 
	/// class and reuse it to extract all information you need, or new multiple instances sequentially.
	/// 
	/// All streams passed to this constructor should be seekable and support reading, and they will be 
	/// disposed when the <see cref="MonoBehaviourFinder"/> is disposed.
	/// </summary>
	/// <param name="globalGameManagersAsset">The <c>assets/bin/Data/globalgamemanagers.assets</c> file. (In apk)</param>
	/// <param name="il2CppBinary">The <c>lib/arm64-v8a/libil2cpp.so</c> file. (In apk)</param>
	/// <param name="globalMetadataBinary">The <c>assets/bin/Data/Managed/Metadata/global-metadata.dat</c> file. (In apk)</param>
	/// <param name="classDataTPK">Class database file. Can be obtained 
	/// <a href="https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/uncompressed_file.zip">here</a>.</param>
	public MonoBehaviourFinder(
		Stream globalGameManagersAsset,
		byte[] il2CppBinary,
		byte[] globalMetadataBinary,
		Stream classDataTPK)
	{
		AssetsFileReader globalGameManagersReader = new(globalGameManagersAsset);
		this._globalGameManagersReader = globalGameManagersReader;
		this._globalGameManagers.Read(globalGameManagersReader);

		ClassPackageFile classPackage = new();
		using AssetsFileReader classDataTPKReader = new(classDataTPK);
		classPackage.Read(classDataTPKReader);

		this._classDatabase = classPackage.GetClassDatabase(this._globalGameManagers.Metadata.UnityVersion);

		this._templateGenerator = new Cpp2IlTempGenerator(globalMetadataBinary, il2CppBinary);
		this._templateGenerator.SetUnityVersion(new UnityVersion(this._globalGameManagers.Metadata.UnityVersion));
		this._templateGenerator.InitializeCpp2IL();
	}

	~MonoBehaviourFinder()
	{
		this.Dispose();
	}

	public void Dispose()
	{
		if (this._disposed) return;
		this._disposed = true;

		GC.SuppressFinalize(this);

		this._globalGameManagersReader.Dispose();
		this._globalGameManagers.Close();
		this._templateGenerator.Dispose();
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
			ClassDatabaseType cldbType = this._classDatabase.FindAssetClassByID(info.TypeId);
			if (cldbType == null)
				return null;

			baseField = new AssetTypeTemplateField();
			baseField.FromClassDatabase(this._classDatabase, cldbType);
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
				monoScriptFile = this._globalGameManagers;
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

			AssetTypeTemplateField newBase = this._templateGenerator.GetTemplateField(
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

	/// <summary>
	/// Get the Phigros version in integer form.
	/// </summary>
	/// <returns>Phigros version in integer form.</returns>
	/// <exception cref="InvalidOperationException">Thrown if Cpp2Il is not initialized. It is initialized when
	/// anything new a instance of <see cref="MonoBehaviourFinder"/>.</exception>
	/// <exception cref="InvalidDataException">Thrown if failed to find Phigros version data.</exception>
	public static uint GetPhiVersion()
	{
		Il2CppMetadata meta = LibCpp2IlMain.TheMetadata
					   ?? throw new InvalidOperationException("Cpp2Il is not initialized.");

		Il2CppAssemblyDefinition assembly = meta.AssemblyDefinitions
							   .FirstOrDefault(a => a.AssemblyName.Name == "Assembly-CSharp")
						   ?? throw new InvalidDataException("Cannot find Assembly-CSharp.");

		Il2CppTypeDefinition type = assembly.Image.Types?
						   .FirstOrDefault(t => t.FullName == "Constants")
					   ?? throw new InvalidDataException("Cannot find Constants class.");

		Il2CppFieldDefinition field = type.Fields?
							.FirstOrDefault(f => f.Name == "IntVersion")
						?? throw new InvalidDataException("Cannot find IntVersion field.");

		object defaultValue = meta.GetFieldDefaultValue(field)?.Value
							   ?? throw new InvalidDataException("There is no default value for the IntVersion field.");

		if (defaultValue is int intValue)
			return (uint)intValue;

		throw new InvalidDataException($"Invalid version type: {defaultValue.GetType()}");
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

	public AssetTypeValueField? TryFindMonoBehaviour(AssetsFile file, string name)
	{
		foreach (AssetFileInfo? info in file.AssetInfos)
		{
			if (info.TypeId != (int)AssetClassID.MonoBehaviour)
				continue;

			AssetTypeValueField baseField = this.GetBaseField(file, info, false);

			AssetTypeValueField scriptField = baseField["m_Script"];
			if (scriptField == null)
				continue;

			long msId = scriptField["m_PathID"].AsLong;
			if (msId == 0)
				continue;

			AssetFileInfo monoInfo = this._globalGameManagers.GetAssetInfo(msId);
			if (monoInfo == null)
				continue;

			AssetTypeValueField msBase = this.GetBaseField(this._globalGameManagers, monoInfo, false);
			string? msName = msBase["m_Name"]?.AsString;

			if (msName == name)
				return this.GetBaseField(file, info, true);
		}

		return null;
	}

	public AssetTypeValueField FindMonoBehaviour(AssetsFile file, string name)
	{
		return this.TryFindMonoBehaviour(file, name)
			?? throw new ArgumentException($"Requested MonoBehaviour not found in the provided file.", nameof(name));
	}
}
