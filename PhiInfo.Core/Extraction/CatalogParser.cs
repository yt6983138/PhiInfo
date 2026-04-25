using PhigrosLibraryCSharp;
using PhiInfo.Core.Models.Catalog;
using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Parses catalog data from binary representation and provides access to catalog entries.
/// This class handles parsing of key, bucket, and entry data to construct a catalog dictionary.
/// Please use static methods to create an instance of this class since they are more convenient,
/// or use <see cref="PhigrosAssetHelper"/> methods.
/// </summary>
public class CatalogParser
{
	private readonly List<CatalogEntry> _entries;

	/// <summary>
	/// Gets a read-only list of all parsed catalog entries.
	/// </summary>
	public IReadOnlyList<CatalogEntry> Entries => this._entries;
	/// <summary>
	/// Gets a frozen dictionary of cached entries indexed by their string keys.
	/// Only entries with string keys are included in this cache.
	/// </summary>
	public FrozenDictionary<string, CatalogValue> CachedEntries { get; }

	/// <summary>
	/// Parses catalog from parsed catalog.json data. Please use static methods to create
	/// an instance of this class since they are more convenient, or use <see cref="PhigrosAssetHelper"/> methods.
	/// </summary>
	/// <param name="keyData">The binary key data to parse.</param>
	/// <param name="bucketData">The binary bucket data to parse.</param>
	/// <param name="entryData">The binary entry data to parse.</param>
	/// <exception cref="ArgumentException">Thrown when data is invalid or malformed.</exception>
	public CatalogParser(
		byte[] keyData,
		byte[] bucketData,
		byte[] entryData)
	{
		this._entries = ParseEntries(keyData, bucketData, entryData);
		this.CachedEntries = this._entries
			.Where(x => x.Key.StringValue is not null)
			.ToFrozenDictionary(x => x.Key.StringValue!, x => x.Value);
	}

	/// <summary>
	/// Creates a <see cref="CatalogParser"/> from base64-encoded strings.
	/// </summary>
	/// <param name="keyDataString">Base64-encoded key data.</param>
	/// <param name="bucketDataString">Base64-encoded bucket data.</param>
	/// <param name="entryDataString">Base64-encoded entry data.</param>
	/// <returns>A new <see cref="CatalogParser"/> instance.</returns>
	/// <exception cref="FormatException">Thrown when base64 strings are invalid.</exception>
	public static CatalogParser FromBase64Strings(string keyDataString, string bucketDataString, string entryDataString)
	{
		return new(Convert.FromBase64String(keyDataString),
				Convert.FromBase64String(bucketDataString),
				Convert.FromBase64String(entryDataString));
	}

	/// <summary>
	/// Creates a <see cref="CatalogParser"/> from a JSON stream.
	/// </summary>
	/// <param name="json">A stream containing JSON-formatted catalog data.</param>
	/// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A new <see cref="CatalogParser"/> instance.</returns>
	/// <exception cref="ArgumentException">Thrown when the JSON is invalid or missing required fields.</exception>
	public static async Task<CatalogParser> FromJsonAsync(Stream json, CancellationToken ct = default)
	{
		RawCatalogModel? data = await JsonSerializer.DeserializeAsync<RawCatalogModel>(json, cancellationToken: ct)
			?? throw new ArgumentException("Invalid json supplied.", nameof(json));

		return FromBase64Strings(data.KeyDataString, data.BucketDataString, data.EntryDataString);
	}

	/// <summary>
	/// Creates a <see cref="CatalogParser"/> from an OBB (Opaque Binary Blob) stream.
	/// </summary>
	/// <param name="obb">A stream containing OBB data with embedded catalog information.</param>
	/// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A new <see cref="CatalogParser"/> instance.</returns>
	/// <exception cref="ArgumentException">Thrown when the OBB is invalid or catalog cannot be extracted.</exception>
	public static async Task<CatalogParser> FromObbAsync(Stream obb, CancellationToken ct = default)
	{
		using Stream catalogStream = PhigrosAssetHelper.GetCatalogStreamFromObb(obb);
		return await FromJsonAsync(catalogStream, ct);
	}

	/// <summary>
	/// Try to get a catalog value by key. This is an O(n) operation, 
	/// so it's not recommended to call this method frequently. Use 
	/// <see cref="TryGet(string)"/> if you want to get a value by 
	/// string key, which is using a <see cref="FrozenDictionary"/> 
	/// under the hood.
	/// </summary>
	/// <param name="key">A <see cref="CatalogKey"/> representing the key.</param>
	/// <returns>A <see cref="CatalogValue"/> representing the value, or
	/// <see langword="null"/> if key is not found.</returns>
	public CatalogValue? TryGet(CatalogKey key)
	{
		foreach (CatalogEntry entry in this._entries)
		{
			if (KeysEqual(entry.Key, key))
				return entry.Value;
		}

		return null;
	}
	/// <summary>
	/// Try to get a catalog value by string key. If you want to use other
	/// types of keys, use <see cref="TryGet(CatalogKey)"/>. This method is 
	/// using a <see cref="FrozenDictionary"/> under the hood, so it is much faster.
	/// </summary>
	/// <param name="key">A <see cref="string"/> representing the key.</param>
	/// <returns>A <see cref="CatalogValue"/> representing the value, or
	/// <see langword="null"/> if key is not found.</returns>
	public CatalogValue? TryGet(string key)
	{
		if (this.CachedEntries.TryGetValue(key, out CatalogValue value))
			return value;

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

		ByteReader keyReader = new(keyData);
		ByteReader bucketReader = new(bucketData);
		ByteReader entryReader = new(entryData);

		int bucketCount = bucketReader.ReadInt();
		List<CatalogEntry> table = new(bucketCount); // preallocate list, ResolveReferences will add
													 // so we can't just use Memory<T> or something like that
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
			bucketReader.Jump((entryCount - 1) * sizeof(int));

			//ushort raw = (ushort)(entryData[entryStart + 8] ^ (entryData[entryStart + 9] << 8));
			// raw was originally using xor, but it doesn't make sense since they bitshifted the second byte to the left by 8,
			// which is the same as just or. so i changed it to this.
			int entryStart = 4 + (28 * entryPos);
			entryReader.JumpTo(entryStart + 8);
			ushort raw = entryReader.ReadUnsignedShort();
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
			if (index == ushort.MaxValue || index >= table.Count)
				continue;

			table[i].Value = CatalogValue.FromResolved(table[index].Key);
		}
	}
}
