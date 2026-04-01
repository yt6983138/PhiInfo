using AssetsTools.NET;
using System.Reflection;

namespace PhiInfo.Core;
public static class Extensions
{
	private static Dictionary<Language, LanguageStringIdAttribute> _langAttributeMap = typeof(Language)
		.GetFields(BindingFlags.Static | BindingFlags.Public)
		.ToDictionary(x => (Language)x.GetValue(null)!,
			x => x.GetCustomAttribute<LanguageStringIdAttribute>() ?? throw new ArgumentNullException());

	internal static AssetTypeValueField GetBaseField(this AssetsFile file, AssetFileInfo info)
	{
		lock (file.Reader)
		{
			long offset = info.GetAbsoluteByteOffset(file);

			if (!file.Metadata.TypeTreeEnabled)
				throw new Exception($"Failed to build template for type {info.TypeId}");
			TypeTreeType tt = file.Metadata.FindTypeTreeTypeByID(info.TypeId, info.GetScriptIndex(file));
			if (tt == null || tt.Nodes.Count <= 0)
				throw new Exception($"Failed to build template for type {info.TypeId}");
			AssetTypeTemplateField template = new();
			template.FromTypeTree(tt);

			RefTypeManager refMan = new();
			refMan.FromTypeTree(file.Metadata);

			return template.MakeValue(file.Reader, offset, refMan);
		}
	}

	public static string GetStringId(this Language lang)
	{
		return _langAttributeMap[lang].Id;
	}
}
