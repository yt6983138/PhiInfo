namespace PhiInfo.Core.Models.Raw;

public record Folder(
	string Title,
	string Subtitle,
	string Cover,
	List<FileItem> Files
);