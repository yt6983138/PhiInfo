using PhigrosLibraryCSharp;
using PhiInfo.Core.Catalog;
using PhiInfo.Core.Models.Catalog;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PhiInfo.Core.Extraction;

public class CatalogParser
{
	private readonly ImmutableList<CatalogEntry> _entries;
	public IReadOnlyList<CatalogEntry> Entries => this._entries;

	public CatalogParser(
		byte[] keyData,
		byte[] bucketData,
		byte[] entryData)
	{
		this._entries = ParseEntries(keyData, bucketData, entryData).ToImmutableList();
	}

	private static void EnsureCatalogModelNotNull([NotNull] RawCatalogModel? obj, string paramName)
	{
		if (obj is null)
			throw new ArgumentNullException("Invalid json supplied.", paramName);
	}

	public static CatalogParser FromBase64Strings(string keyDataString, string bucketDataString, string entryDataString)
	{
		return new(Convert.FromBase64String(keyDataString),
				Convert.FromBase64String(bucketDataString),
				Convert.FromBase64String(entryDataString));
	}
	public static CatalogParser FromJson(Stream json)
	{
		RawCatalogModel? data = JsonSerializer.Deserialize(json, CatalogModelJsonContext.Default.RawCatalogModel);
		EnsureCatalogModelNotNull(data, nameof(json));

		return FromBase64Strings(data.KeyDataString, data.BucketDataString, data.EntryDataString);
	}
	public static CatalogParser FromJson(string json)
	{
		RawCatalogModel? data = JsonSerializer.Deserialize(json, CatalogModelJsonContext.Default.RawCatalogModel);
		EnsureCatalogModelNotNull(data, nameof(json));

		return FromBase64Strings(data.KeyDataString, data.BucketDataString, data.EntryDataString);
	}
	public static CatalogParser FromJson(JsonObject obj)
	{
		RawCatalogModel? data = obj.Deserialize(CatalogModelJsonContext.Default.RawCatalogModel);
		EnsureCatalogModelNotNull(data, nameof(obj));

		return FromBase64Strings(data.KeyDataString, data.BucketDataString, data.EntryDataString);
	}
	public static CatalogParser FromObb(Stream obb)
	{
		using Stream catalogStream = PhigrosAssetHelper.GetCatalogStreamFromObb(obb);
		return FromJson(catalogStream);
	}

	public CatalogValue? Get(CatalogKey key)
	{
		foreach (CatalogEntry entry in this._entries)
		{
			if (KeysEqual(entry.Key, key))
				return entry.Value;
		}

		return null;
	}

	public CatalogValue? Get(string key)
	{
		foreach (CatalogEntry entry in this._entries)
		{
			if ((entry.Key.Type == CatalogKeyType.Utf8String ||
				 entry.Key.Type == CatalogKeyType.UnicodeString) &&
				entry.Key.StringValue == key)
			{
				return entry.Value;
			}
		}

		return null;
	}

	private static bool KeysEqual(CatalogKey a, CatalogKey b)
	{
		if (a.Type != b.Type)
			return false;

		return a.Type switch
		{
			CatalogKeyType.Utf8String or CatalogKeyType.UnicodeString =>
				a.StringValue == b.StringValue,
			CatalogKeyType.Byte =>
				a.ByteValue == b.ByteValue,
			_ => false
		};
	}

	private static List<CatalogEntry> ParseEntries(
		byte[] keyData,
		byte[] bucketData,
		byte[] entryData)
	{
		List<CatalogEntry> table = [];

		ByteReader keyReader = new(keyData);
		ByteReader bucketReader = new(bucketData);
		int bucketCount = bucketReader.ReadInt();
		for (int i = 0; i < bucketCount; i++)
		{
			int keyPos = bucketReader.ReadInt();
			keyReader.JumpTo(keyPos);

			CatalogKeyType keyType = keyReader.ReadUnmanaged<CatalogKeyType>();
			CatalogKey key = keyType switch
			{
				CatalogKeyType.Utf8String => CatalogKey.FromString(
										keyType,
										keyReader.ReadStringCustomLength(keyReader.ReadInt(), Encoding.UTF8)),
				CatalogKeyType.UnicodeString => CatalogKey.FromString(
										keyType,
										keyReader.ReadStringCustomLength(keyReader.ReadInt(), Encoding.Unicode)),
				CatalogKeyType.Byte => CatalogKey.FromByte(keyReader.ReadByte()),
				_ => throw new InvalidOperationException($"Unknown key type: {keyType}"),
			};

			int entryCount = bucketReader.ReadInt();
			int entryPos = bucketReader.ReadInt();
			for (int j = 1; j < entryCount; j++)
				bucketReader.ReadInt();

			int entryStart = 4 + 28 * entryPos;
			ushort raw = (ushort)(entryData[entryStart + 8] ^ entryData[entryStart + 9] << 8);
			CatalogValue value = CatalogValue.FromRaw(raw);

			table.Add(new CatalogEntry(key, value));
		}

		ResolveReferences(table);
		return table;
	}

	private static void ResolveReferences(List<CatalogEntry> table)
	{
		for (int i = 0; i < table.Count; i++)
		{
			CatalogValue value = table[i].Value;
			if (!value.IsReference)
				continue;

			ushort index = value.RawValue;
			if (index == 65535 || index >= table.Count)
				continue;

			table[i].Value = CatalogValue.FromResolved(table[index].Key);
		}
	}
}
