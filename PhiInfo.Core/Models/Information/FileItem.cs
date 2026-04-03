namespace PhiInfo.Core.Models.Information;

/// <summary>
/// Extracted file/item information inside a <see cref="Folder"/>.
/// </summary>
/// <param name="Key">Internal key of the item. I.e. <c>Yshanfeng</c></param>
/// <param name="SubIndex">[Unknown] I.e. <c>1</c></param>
/// <param name="Name">Display name of the item. I.e. <c>【录音】山风</c></param>
/// <param name="Date">In-game date string. I.e. <c>770/02/29</c></param>
/// <param name="Supervisor">Supervisor/author label shown in-game. I.e. <c>鸠</c></param>
/// <param name="Category">Item category. I.e. <c>souvenir</c></param>
/// <param name="Content">Main text content of the item. I.e. <c>记录了异常的山谷风声，如猿啼鹤唳</c>(leftover truncated)</param>
/// <param name="Properties">Extra properties string. Not sure where it is used. I.e. <c>长度=10min32s</c></param>
public record FileItem(
	string Key,
	int SubIndex,
	string Name,
	string Date,
	string Supervisor,
	string Category,
	string Content,
	string Properties
);