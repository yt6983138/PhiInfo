namespace PhiInfo.Core.Models.Catalog;

/// <summary>
/// Catalog key type, which can be a UTF-8 string, a Unicode string, or a byte.
/// </summary>
public enum CatalogKeyType : byte
{
	/// <summary>
	/// UTF-8 string.
	/// </summary>
	Utf8String = 0,
	/// <summary>
	/// Unicode (UTF-16) string.
	/// </summary>
	UnicodeString = 1,
	/// <summary>
	/// A single byte.
	/// </summary>
	Byte = 4
}

/// <summary>
/// A catalog key.
/// </summary>
public readonly struct CatalogKey
{
	/// <summary>
	/// Type of this key.
	/// </summary>
	public CatalogKeyType Type { get; }
	/// <summary>
	/// Value of this key if it's a string type. <see langword="null"/> if it's a byte type.
	/// </summary>
	public string? StringValue { get; }
	/// <summary>
	/// Value of this key if it's a byte type. <see langword="null"/> if it's a string type.
	/// </summary>
	public byte? ByteValue { get; }

	private CatalogKey(CatalogKeyType type, string? stringValue, byte? byteValue)
	{
		this.Type = type;
		this.StringValue = stringValue;
		this.ByteValue = byteValue;
	}

	/// <summary>
	/// Constructs a catalog key from a string value.
	/// </summary>
	/// <param name="type">Type of the key, can be <see cref="CatalogKeyType.UnicodeString"/> or 
	/// <see cref="CatalogKeyType.Utf8String"/>.</param>
	/// <param name="value">Value of this key.</param>
	/// <returns>A constructed catalog key from string.</returns>
	public static CatalogKey FromString(CatalogKeyType type, string value)
		=> new(type, value, null);

	/// <summary>
	/// Constructs a catalog key from a byte value.
	/// </summary>
	/// <param name="value">Value of this key.</param>
	/// <returns>A constructed catalog key from string.</returns>
	public static CatalogKey FromByte(byte value)
		=> new(CatalogKeyType.Byte, null, value);

	/// <summary>
	/// Converts this catalog key to a string representation. If this key is a 
	/// byte type, it will be converted to a string using its byte value. If 
	/// this key is a string type, it will return its string value.
	/// </summary>
	/// <returns>A string representation of this key.</returns>
	public override string ToString()
		=> this.StringValue ?? this.ByteValue?.ToString() ?? string.Empty;
}
