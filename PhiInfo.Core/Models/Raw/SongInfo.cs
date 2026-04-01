namespace PhiInfo.Core.Models.Raw;

public record SongInfo(
	string Id,
	string Key,
	string Name,
	string Composer,
	string Illustrator,
	double PreviewStartTimeSeconds,
	double PreviewEndTimeSeconds,
	Dictionary<string, SongLevel> Levels
);