using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<Product>> ListAsync(int page, int pageSize, string? search,
        bool? isActive, CancellationToken cancellationToken);
    void Add(Product product);
}
