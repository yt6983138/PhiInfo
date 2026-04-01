namespace PhiInfo.Core.Models.Raw;

public record Image(uint Format, uint Width, uint Height, byte[] Data)
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
}