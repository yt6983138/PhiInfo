
using System.Text.Json.Serialization;

namespace PhiInfo.Core.Catalog;
public record class RawCatalogModel(
	[property: JsonPropertyName("m_KeyDataString")] string KeyDataString,
	[property: JsonPropertyName("m_BucketDataString")] string BucketDataString,
	[property: JsonPropertyName("m_EntryDataString")] string EntryDataString
);

[JsonSerializable(typeof(RawCatalogModel))]
public partial class CatalogModelJsonContext : JsonSerializerContext
{
}
