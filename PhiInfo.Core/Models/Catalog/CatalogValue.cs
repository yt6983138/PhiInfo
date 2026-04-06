namespace PhiInfo.Core.Models.Catalog;

/// <summary>
/// Represents a value within a catalog, which can either be a raw reference or a resolved key.
/// </summary>
public readonly struct CatalogValue
{
	/// <summary>
	/// Gets a value indicating whether this instance represents a raw reference.
	/// </summary>
	public bool IsReference { get; }

	/// <summary>
	/// Gets the raw numeric value associated with the reference.
	/// </summary>
	public ushort RawValue { get; }

	/// <summary>
	/// Gets the resolved <see cref="CatalogKey"/> if available.
	/// </summary>
	public CatalogKey? ResolvedKey { get; }

	private CatalogValue(bool isReference, ushort rawValue, CatalogKey? resolvedKey)
	{
		this.IsReference = isReference;
		this.RawValue = rawValue;
		this.ResolvedKey = resolvedKey;
	}

	/// <summary>
	/// Creates a <see cref="CatalogValue"/> from a raw reference value.
	/// </summary>
	/// <param name="value">The raw numerical reference.</param>
	/// <returns>A new <see cref="CatalogValue"/> instance initialized as a reference.</returns>
	public static CatalogValue FromRaw(ushort value)
		=> new(true, value, null);

	/// <summary>
	/// Creates a <see cref="CatalogValue"/> from a resolved key.
	/// </summary>
	/// <param name="key">The resolved catalog key.</param>
	/// <returns>A new <see cref="CatalogValue"/> instance initialized with a resolved key.</returns>
	public static CatalogValue FromResolved(CatalogKey key)
		=> new(false, 0, key);
}