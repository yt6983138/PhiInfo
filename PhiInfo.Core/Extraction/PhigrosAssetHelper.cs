using Fmod5Sharp.CodecRebuilders;
using Fmod5Sharp.FmodTypes;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Helper class for converting FMOD sound bank to ogg files.
/// </summary>
public static class PhigrosAssetHelper
{
	/// <summary>
	/// Convert a <see cref="FmodSoundBank"/> to ogg file bytes. The returned ogg file will not be 
	/// byte-to-byte same as the original one in the package.
	/// </summary>
	/// <param name="bank">The audio file (.wav extension) extracted from bundle</param>
	/// <returns>Encoded ogg bytes.</returns>
	public static byte[] ToOggBytes(this FmodSoundBank bank)
	{
		return FmodVorbisRebuilder.RebuildOggFile(bank.Samples[0]);
	}
}
