using System.Text.Json.Serialization;

namespace ProductCatalog.Api.Contracts;

public sealed record CreateProductRequest([property: JsonRequired] string Name, string? Description,
    [property: JsonRequired] decimal Price, bool IsActive = true);

public sealed record UpdateProductRequest([property: JsonRequired] string Name, string? Description,
    [property: JsonRequired] decimal Price, [property: JsonRequired] bool IsActive);
