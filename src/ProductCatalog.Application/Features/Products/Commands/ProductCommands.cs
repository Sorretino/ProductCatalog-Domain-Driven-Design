namespace ProductCatalog.Application.Features.Products.Commands;

public interface IProductInput
{
    string Name { get; }
    string? Description { get; }
    decimal Price { get; }
}

public sealed record CreateProductCommand(string Name, string? Description, decimal Price,
    bool IsActive = true) : IProductInput;

public sealed record UpdateProductCommand(Guid Id, string Name, string? Description,
    decimal Price, bool IsActive, Guid ExpectedVersion) : IProductInput;

public sealed record DeleteProductCommand(Guid Id, Guid ExpectedVersion);
