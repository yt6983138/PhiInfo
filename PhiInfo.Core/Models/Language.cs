namespace PhiInfo.Core.Models;

/// <summary>
/// This attribute is used to mark the string id of a language, which is used to retrieve multi-language data from Phigros internal structure.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class LanguageStringIdAttribute : Attribute
{
	/// <summary>
	/// The string id of this language, which is used to retrieve multi-language data from Phigros internal structure.
	/// </summary>
	public string Id { get; }

	internal LanguageStringIdAttribute(string id)
	{
		this.Id = id;
	}
}
/// <summary>
/// Represents a supported language used to retrieve multi-language string data from Phigros internal structures.
/// </summary>
public enum Language
{
	/// <summary>
	/// Simplified Chinese language.
	/// </summary>
	[LanguageStringId("chinese")]
	Chinese = 0x28,

	/// <summary>
	/// Traditional Chinese language.
	/// </summary>
	[LanguageStringId("chineseTraditional")]
	TraditionalChinese = 0x29,

	/// <summary>
	/// English language.
	/// </summary>
	[LanguageStringId("english")]
	English = 0x0A,

	/// <summary>
	/// Japanese language.
	/// </summary>
	[LanguageStringId("japanese")]
	Japanese = 0x16,

	/// <summary>
	/// Korean language.
	/// </summary>
	[LanguageStringId("korean")]
	Korean = 0x17
}
