namespace PhiInfo.Core.Models.Information;

/// <summary>
/// 
/// </summary>
/// <param name="Id">JourneywithYou.Iris.0</param>
/// <param name="Key">Journey with You</param>
/// <param name="Name">Journey with You</param>
/// <param name="Composer">Iris</param>
/// <param name="Illustrator">BTKCyber (青鸟 modified)</param>
/// <param name="PreviewStartTimeSeconds">40.5</param>
/// <param name="PreviewEndTimeSeconds">65.5</param>
/// <param name="Levels"></param>
public record SongInfo(
	string Id,
	string Key,
	string Name,
	string Composer,
	string Illustrator,
	double PreviewStartTimeSeconds,
	double PreviewEndTimeSeconds,
	Dictionary<string, SongLevel> Levels
);