using PhigrosLibraryCSharp.GameRecords;

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
	Dictionary<Difficulty, SongLevel> Levels)
{

	public string LowResolutionIllustrationAddressablePath => $"Assets/Tracks/{this.Id}/IllustrationLowRes.jpg";
	public string IllustrationAddressablePath => $"Assets/Tracks/{this.Id}/Illustration.jpg";
	public string BlurIllustrationAddressablePath => $"Assets/Tracks/{this.Id}/IllustrationBlur.jpg";

	public string MusicAddressablePath => $"Assets/Tracks/{this.Id}/music.wav";

	public string GetChartAddressablePath(Difficulty difficulty)
	{
		if (!this.Levels.ContainsKey(difficulty))
		{
			throw new ArgumentException($"This song does not have requested difficulty.", nameof(difficulty));
		}
		return $"Assets/Tracks/{this.Id}/Chart_{difficulty}.json";
	}
}