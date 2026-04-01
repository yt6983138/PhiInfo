namespace PhiInfo.Core;

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
	Chinese = 40 // TODO: add more languages when needed
}
