namespace ProductCatalog.Application.Features.Products.Queries;

public sealed record GetProductQuery(Guid Id);

public sealed record ListProductsQuery(int Page = 1, int PageSize = 20,
    string? Search = null, bool? IsActive = null);
