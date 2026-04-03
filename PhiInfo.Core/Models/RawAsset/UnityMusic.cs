using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;

namespace PhiInfo.Core.Models.RawAsset;

/// <summary>
/// Extracted music data from Unity assets. The data is in FSB format, 
/// which can be decoded using <see cref="FsbLoader"/> to get the actual 
/// audio data and metadata.
/// </summary>
/// <param name="LengthSeconds">Length in seconds.</param>
/// <param name="Data">The FSB raw data.</param>
public record UnityMusic(float LengthSeconds, byte[] Data)
{
	/// <summary>
	/// Decode the audio data into an <see cref="FmodSoundBank"/> object.
	/// </summary>
	/// <returns>A decoded <see cref="FmodSoundBank"/>, for convenient processing.</returns>
	public FmodSoundBank Decode()
	{
		return FsbLoader.LoadFsbFromByteArray(this.Data);
	}
}