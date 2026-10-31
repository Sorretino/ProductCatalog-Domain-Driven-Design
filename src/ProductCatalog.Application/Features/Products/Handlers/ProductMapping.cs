using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Application.Features.Products.Handlers;

internal static class ProductMapping
{
    public static ProductDto ToDto(this Product product) => new(product.Id, product.Name,
        product.Description, product.Price.Amount, product.IsActive, product.CreatedAt,
        product.UpdatedAt, product.Version);
}
