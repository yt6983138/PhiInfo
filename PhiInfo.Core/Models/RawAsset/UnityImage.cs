using AssetRipper.TextureDecoder.Etc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PhiInfo.Core.Models.RawAsset;

/// <summary>
/// Extracted image data from Unity assets.
/// </summary>
/// <param name="Format">Unity internal format flag.</param>
/// <param name="Width">Width of the image.</param>
/// <param name="Height">Height of the image.</param>
/// <param name="Data">Data of the image, just raw pixel data and may need 
/// to be decoded using <see cref="EtcDecoder"/>.</param>
public record UnityImage(uint Format, uint Width, uint Height, byte[] Data)
{
	public byte[] CraftImageBufferWithHeader()
	{
		const int StaticHeaderSize = sizeof(byte) + sizeof(uint) + sizeof(uint) + sizeof(uint);

		byte[] result = new byte[sizeof(byte) + StaticHeaderSize + this.Data.Length];
		result[0] = (byte)'H'; // idk why
		BitConverter.GetBytes(this.Format).CopyTo(result, sizeof(byte));
		BitConverter.GetBytes(this.Height).CopyTo(result, sizeof(byte) + sizeof(uint));
		BitConverter.GetBytes(this.Width).CopyTo(result, sizeof(byte) + sizeof(uint) + sizeof(uint));
		this.Data.CopyTo(result, StaticHeaderSize);
		return result;
	}

	/// <summary>
	/// Decode the image data into an <see cref="Image"/> object.
	/// </summary>
	/// <returns>A decoded <see cref="Image"/>, for convenient processing.</returns>
	/// <exception cref="NotSupportedException">Thrown if the format is not supported.</exception>
	public Image Decode()
	{
		int width = (int)this.Width;
		int height = (int)this.Height;

		switch (this.Format)
		{
			case 3:
				return Image.LoadPixelData<Rgb24>(this.Data, width, height);
			case 4:
				return Image.LoadPixelData<Rgba32>(this.Data, width, height);
			case 34:
				EtcDecoder.DecompressETC<ColorBGRA<byte>, byte>(this.Data, width, height, out byte[] etc);
				return Image.LoadPixelData<Bgra32>(etc, width, height);
			case 47:
				EtcDecoder.DecompressETC2A8<ColorBGRA<byte>, byte>(this.Data, width, height, out byte[] etc2a8);
				return Image.LoadPixelData<Bgra32>(etc2a8, width, height);
			default:
				throw new NotSupportedException($"Unsupported image format: {this.Format}");
		}
	}
}