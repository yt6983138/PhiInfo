using PhiInfo.Core.Models.Information;

namespace PhiInfo.CLI;

public record class NonMultiLanguageInfos(List<SongInfo> Songs,
		List<Avatar> Avatars,
		List<ChapterInfo> Chapters);
public record class MultiLanguageInfos(List<Folder>? Collections,
	List<string> Tips);
