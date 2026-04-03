namespace PhiInfo.Core.Models.Information;

/// <summary>
/// Extracted chapter information.
/// </summary>
/// <param name="Code">Internal chapter code. I.e. <c>MainStory8</c></param>
/// <param name="Banner">Display banner text of the chapter. I.e. <c>Chapter 8</c></param>
/// <param name="SongIds">List of song ids (<see cref="SongInfo.Id"/>) included in the chapter.</param>
public record ChapterInfo(
	string Code,
	string Banner,
	List<string> SongIds
);