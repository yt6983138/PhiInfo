namespace PhiInfo.Core.Models.Raw;

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