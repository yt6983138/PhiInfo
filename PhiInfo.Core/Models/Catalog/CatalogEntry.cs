namespace PhiInfo.Core.Models.Catalog;
public record class CatalogEntry(CatalogKey Key, CatalogValue Value)
{
	public CatalogValue Value { get; set; } = Value;
}
