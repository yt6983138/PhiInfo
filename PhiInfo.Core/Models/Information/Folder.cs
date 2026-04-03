namespace PhiInfo.Core.Models.Information;

/// <summary>
/// Extracted collection folder information (collections)
/// </summary>
/// <param name="Title">Folder title. I.e. <c>冰封世界</c></param>
/// <param name="Subtitle">Folder subtitle. I.e. <c>第一章</c></param>
/// <param name="AddressableCoverPath">Internal addressable path of the folder cover image.</param>
/// <param name="Files">Items contained in the folder.</param>
public record Folder(
	string Title,
	string Subtitle,
	string AddressableCoverPath,
	List<FileItem> Files
);