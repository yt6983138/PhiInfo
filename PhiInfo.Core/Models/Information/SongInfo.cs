using PhigrosLibraryCSharp.CloudSave;

namespace PhiInfo.Core.Models.Information;

/// <summary>
/// Extracted song information.
/// </summary>
/// <param name="Id">Internal song id. I.e. <c>JourneywithYou.Iris.0</c></param>
/// <param name="Key">[Unknown]. I.e. <c>Journey with You</c></param>
/// <param name="Name">Display name of the song. I.e. <c>Journey with You</c></param>
/// <param name="Composer">Song composer. I.e. <c>Iris</c></param>
/// <param name="Illustrator">Song illustration artist. I.e. <c>BTKCyber (青鸟 modified)</c></param>
/// <param name="PreviewStartTimeSeconds">Preview start timestamp (seconds). I.e. <c>40.5</c></param>
/// <param name="PreviewEndTimeSeconds">Preview end timestamp (seconds). I.e. <c>65.5</c></param>
/// <param name="Levels">Per-difficulty chart metadata.</param>
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

	/// <summary>
	/// Gets the addressable path to the low-resolution illustration.
	/// </summary>
	public string LowResolutionIllustrationAddressablePath => $"Assets/Tracks/{this.Id}/IllustrationLowRes.jpg";

	/// <summary>
	/// Gets the addressable path to the full-resolution illustration.
	/// </summary>
	public string IllustrationAddressablePath => $"Assets/Tracks/{this.Id}/Illustration.jpg";

	/// <summary>
	/// Gets the addressable path to the blurred illustration.
	/// </summary>
	public string BlurIllustrationAddressablePath => $"Assets/Tracks/{this.Id}/IllustrationBlur.jpg";

	/// <summary>
	/// Gets the addressable path to the music file.
	/// </summary>
	public string MusicAddressablePath => $"Assets/Tracks/{this.Id}/music.wav";

	/// <summary>
	/// Gets the addressable path for a specific chart difficulty.
	/// </summary>
	/// <param name="difficulty">The difficulty level of the chart.</param>
	/// <returns>The addressable asset path string.</returns>
	/// <exception cref="ArgumentException">Thrown if the song does not contain the specified difficulty.</exception>
	public string GetChartAddressablePath(Difficulty difficulty)
	{
		if (!this.Levels.ContainsKey(difficulty))
		{
			throw new ArgumentException($"This song does not have requested difficulty.", nameof(difficulty));
		}
		return $"Assets/Tracks/{this.Id}/Chart_{difficulty}.json";
	}
}