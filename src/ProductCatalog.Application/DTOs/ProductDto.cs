namespace ProductCatalog.Application.DTOs;

public sealed record ProductDto(Guid Id, string Name, string? Description, decimal Price,
    bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid Version);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
