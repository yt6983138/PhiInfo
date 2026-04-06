namespace PhiInfo.Core.Models.Catalog;

/// <summary>
/// A catalog entry, basically a mutable key-value pair.
/// </summary>
/// <param name="Key">A catalog key.</param>
/// <param name="Value">A catalog value.</param>
public record class CatalogEntry(CatalogKey Key, CatalogValue Value)
{
	/// <summary>
	/// The value of this entry.
	/// </summary>
	public CatalogValue Value { get; set; } = Value;
}
