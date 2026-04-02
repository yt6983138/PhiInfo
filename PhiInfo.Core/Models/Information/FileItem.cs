namespace PhiInfo.Core.Models.Information;

/// <summary>
/// 
/// </summary>
/// <param name="Key">Yshanfeng</param>
/// <param name="SubIndex">1</param>
/// <param name="Name">【录音】山风</param>
/// <param name="Date">770/02/29</param>
/// <param name="Supervisor">鸠</param>
/// <param name="Category">souvenir</param>
/// <param name="Content">记录了异常的山谷风声，如猿啼鹤唳 (truncated)</param>
/// <param name="Properties">长度=10min32s</param>
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