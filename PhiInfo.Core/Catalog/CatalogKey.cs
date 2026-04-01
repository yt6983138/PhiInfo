namespace PhiInfo.Core.Catalog;
public enum CatalogKeyType : byte
{
	Utf8String = 0,
	UnicodeString = 1,
	Byte = 4
}

public readonly struct CatalogKey
{
	public CatalogKeyType Type { get; }
	public string? StringValue { get; }
	public byte? ByteValue { get; }

	private CatalogKey(CatalogKeyType type, string? stringValue, byte? byteValue)
	{
		this.Type = type;
		this.StringValue = stringValue;
		this.ByteValue = byteValue;
	}

	public static CatalogKey FromString(CatalogKeyType type, string value)
		=> new(type, value, null);

	public static CatalogKey FromByte(byte value)
		=> new(CatalogKeyType.Byte, null, value);

	public override string ToString()
		=> this.StringValue ?? this.ByteValue?.ToString() ?? string.Empty;
}
