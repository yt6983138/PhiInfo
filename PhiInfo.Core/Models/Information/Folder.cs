namespace PhiInfo.Core.Models.Information;

/// <summary>
/// Excluded file range for collection folder.
/// </summary>
/// <param name="StartIndex">Inclusive start index of this range.</param>
/// <param name="EndIndex">Inclusive end index of this range.</param>
public record struct ExcludedFileRange(
	int StartIndex,
	int EndIndex);
/// <summary>
/// Included individual file for collection folder.
/// </summary>
/// <param name="Key">The key to match with.</param>
/// <param name="SubIndex">The subindex to match with.</param>
public record struct FileReference(
	string Key,
	int SubIndex);

/// <summary>
/// Extracted collection folder information (collections)
/// </summary>
/// <param name="Title">Folder title. I.e. <c>冰封世界</c></param>
/// <param name="Subtitle">Folder subtitle. I.e. <c>第一章</c></param>
/// <param name="StartIndex">Inclusive start index of included files.</param>
/// <param name="EndIndex">Inclusive end index of included files.</param>
/// <param name="ExcludedFileRanges">Excluded file ranges.</param>
/// <param name="IncludedIsolatedFiles">File include override.</param>
/// <param name="AddressableCoverPath">Internal addressable path of the folder cover image.</param>
/// <param name="FakeCoverFlag">Internal unlock flag of this folder. [Unchecked] Eg. <c>unlockFlagOfIgallta</c></param>
/// <param name="FakeCoverAddressablePath">Internal fake cover addressable path. Eg. <c>Assets/Tracks/#ChapterCover/MainStory6Locked.png</c></param>
/// <param name="FakeCoverBlurAddressablePath">Internal fake cover blur addressable path. Eg. <c>Assets/Tracks/#ChapterCover/MainStory6LockedBlur.png</c></param>
/// <param name="AllNum">[Unknown]</param>
/// <param name="Files">Items contained in the folder.</param>
public record Folder(
	string Title,
	string Subtitle,
	int StartIndex,
	int EndIndex,
	ExcludedFileRange[] ExcludedFileRanges,
	FileReference[] IncludedIsolatedFiles,
	string AddressableCoverPath,
	string FakeCoverFlag,
	string FakeCoverAddressablePath,
	string FakeCoverBlurAddressablePath,
	int AllNum,
	List<FileItem> Files)
{
	/// <summary>
	/// Template for unclassified files. Returns a new instance when accessed.
	/// </summary>
	public static Folder UnclassifiedTemplate => new("<Unclassified>", "", 0, 0, [], [], "", "", "", "", 0, []);

	/// <summary>
	/// Check if this instance is <see cref="UnclassifiedTemplate"/>.
	/// </summary>
	public bool IsUnclassified => this.Title == "<Unclassified>";
};