using PhiInfo.Core.Models.Information;

namespace PhiInfo.CLI;

/// <summary>
/// Information not related to any specific language, such as songs, avatars, chapters, version information, etc.
/// Intended to be used in downstream applications to ensure consistency.
/// </summary>
/// <param name="Songs">The list of songs.</param>
/// <param name="Avatars">The list of avatars.</param>
/// <param name="Chapters">The list of chapters.</param>
/// <param name="VersionString">The version string of the game, e.g. <c>3.19.2</c></param>
/// <param name="VersionInteger">The version integer of the game, can be used to compare versions.</param>
/// <param name="IsInternational">Indicates whether the game is the international version.</param>
public record class NonMultiLanguageInfos(
	List<SongInfo> Songs,
	List<Avatar> Avatars,
	List<ChapterInfo> Chapters,
	string VersionString,
	int VersionInteger,
	bool IsInternational);
/// <summary>
/// Information related to languages, such as collections and tips.
/// Intended to be used in downstream applications to ensure consistency.
/// </summary>
/// <param name="Collections">The list of collections. It is a tree structure, and collection file is stored in it.</param>
/// <param name="Tips">The tips shown while loading charts.</param>
public record class MultiLanguageInfos(
	List<Folder>? Collections,
	List<string> Tips);
