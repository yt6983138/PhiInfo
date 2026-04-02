namespace PhiInfo.Core.Models.Information;

public record PhigrosExtractedDataCollection(
	List<SongInfo> Songs,
	List<Folder> Collections,
	List<Avatar> Avatars,
	List<string> Tips,
	List<ChapterInfo> Chapters
);