namespace PhiInfo.Core.Models.Catalog;
public readonly struct CatalogValue
{
	public bool IsReference { get; }
	public ushort RawValue { get; }
	public CatalogKey? ResolvedKey { get; }

	private CatalogValue(bool isReference, ushort rawValue, CatalogKey? resolvedKey)
	{
		this.IsReference = isReference;
		this.RawValue = rawValue;
		this.ResolvedKey = resolvedKey;
	}

	public static CatalogValue FromRaw(ushort value)
		=> new(true, value, null);

	public static CatalogValue FromResolved(CatalogKey key)
		=> new(false, 0, key);
}