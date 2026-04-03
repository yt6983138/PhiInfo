namespace PhiInfo.Core.Models;

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class LanguageStringIdAttribute : Attribute
{
	public string Id { get; }
	public LanguageStringIdAttribute(string id)
	{
		this.Id = id;
	}
}
public enum Language
{
	[LanguageStringId("chinese")]
	Chinese = 0x28,

	[LanguageStringId("chineseTraditional")]
	TraditionalChinese = 0x29,

	[LanguageStringId("english")]
	English = 0x0A,

	[LanguageStringId("japanese")]
	Japanese = 0x16,

	[LanguageStringId("korean")]
	Korean = 0x17
}
