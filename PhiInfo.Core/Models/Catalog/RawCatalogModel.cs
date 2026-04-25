using System.Text.Json.Serialization;

namespace PhiInfo.Core.Models.Catalog;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public record class RawCatalogModel(
	[property: JsonPropertyName("m_KeyDataString")] string KeyDataString,
	[property: JsonPropertyName("m_BucketDataString")] string BucketDataString,
	[property: JsonPropertyName("m_EntryDataString")] string EntryDataString
);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
