namespace PhiInfo.Core.Models.Information;

/// <summary>
/// Extracted chart level information for a single difficulty.
/// </summary>
/// <param name="Charter">Chart author name. I.e. <c>jouR.ney with hold</c></param>
/// <param name="ChartConstant">Chart constant of the chart. I.e. <c>5</c></param>
public record SongLevel(string Charter, double ChartConstant);