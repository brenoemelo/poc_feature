using System.Text.Json.Serialization;

namespace PoC.Shared.Common;

public record ApiResponse<T>(
    [property: JsonPropertyName("data")] T Data,
    [property: JsonPropertyName("links")] IEnumerable<Link> Links
);

public record PagedResponse<T>(
    [property: JsonPropertyName("data")] IEnumerable<T> Data,
    [property: JsonPropertyName("meta")] PaginationMeta Meta,
    [property: JsonPropertyName("links")] IEnumerable<Link> Links
);

public record PaginationMeta(
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("nextCursor")] string? NextCursor = null
);

public record Link(
    [property: JsonPropertyName("rel")] string Rel,
    [property: JsonPropertyName("href")] string Href,
    [property: JsonPropertyName("method")] string Method
);
