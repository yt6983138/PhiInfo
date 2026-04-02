using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;

namespace PhiInfo.Core.Models.RawAsset;

public record UnityMusic(float LengthSeconds, byte[] Data)
{
	public FmodSoundBank Decode()
	{
		return FsbLoader.LoadFsbFromByteArray(this.Data);
	}
}