namespace PhiInfo.Core.Models.Information;

/// <summary>
/// Extracted chart level information for a single difficulty.
/// </summary>
/// <param name="Charter">Chart author name. I.e. <c>jouR.ney with hold</c></param>
/// <param name="ChartConstant">Chart constant of the chart. I.e. <c>5</c></param>
/// <param name="HasDifferentMusic">Whether this chart has a different music file than the default one.</param>
/// <param name="HasDifferentCover">Whether this chart has a different cover illustration than the default one.</param>
public record SongLevel(
	string Charter,
	float ChartConstant,
	bool HasDifferentMusic,
	bool HasDifferentCover);