using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace PhiInfo.Core;

public partial class PhiInfo
{
	private AssetTypeValueField? FindMonoBehaviour(
		AssetsFile file,
		string name)
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

			AssetFileInfo monoInfo = this._ggmInst.GetAssetInfo(msId);
			if (monoInfo == null)
				continue;

			AssetTypeValueField msBase = this.GetBaseField(this._ggmInst, monoInfo, false);
			string? msName = msBase["m_Name"]?.AsString;

			if (msName == name)
			{
				return this.GetBaseField(file, info, true);
			}
		}

		return null;
	}
}