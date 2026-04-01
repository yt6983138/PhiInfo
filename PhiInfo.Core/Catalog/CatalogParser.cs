using PhigrosLibraryCSharp;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace PhiInfo.Core.Catalog;

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

	public CatalogParser(Stream json)
	{
		RawCatalogModel data = JsonSerializer.Deserialize(json, CatalogModelJsonContext.Default.RawCatalogModel) ??
					   throw new ArgumentException("Invalid json supplied.", nameof(json));

		this._entries = ParseEntries(
				Convert.FromBase64String(data.KeyDataString),
				Convert.FromBase64String(data.BucketDataString),
				Convert.FromBase64String(data.EntryDataString))
			.ToImmutableList();
	}


	public CatalogValue? Get(CatalogKey key)
	{
		foreach (CatalogEntry entry in this._entries)
		{
			if (KeysEqual(entry.Key, key))
			{
				return entry.Value;
			}
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

			int entryStart = 4 + (28 * entryPos);
			ushort raw = (ushort)(entryData[entryStart + 8] ^ (entryData[entryStart + 9] << 8));
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
