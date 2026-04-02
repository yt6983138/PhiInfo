namespace PhiInfo.Core.Models.Information;

/// <summary>
/// 
/// </summary>
/// <param name="Title">冰封世界</param>
/// <param name="Subtitle">第一章</param>
/// <param name="Cover">Assets/Tracks/Dlyrotz.Likey.0/Illustration.jpg</param>
/// <param name="Files"></param>
public record Folder(
	string Title,
	string Subtitle,
	string Cover,
	List<FileItem> Files
);