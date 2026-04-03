namespace PhiInfo.Core.Models.Information;

/// <summary>
/// 
/// </summary>
/// <param name="Code">MainStory8</param>
/// <param name="Banner">Chapter 8</param>
/// <param name="SongIds">Luminescence.米虾Fomiki初云CLoudie.0</param>
public record ChapterInfo(
	string Code,
	string Banner,
	List<string> SongIds
);