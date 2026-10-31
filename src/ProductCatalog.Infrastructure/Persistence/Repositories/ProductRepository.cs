using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(CatalogDbContext context) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(Product product) => context.Products.Add(product);

    public async Task<PagedResult<Product>> ListAsync(int page, int pageSize, string? search,
        bool? isActive, CancellationToken cancellationToken)
    {
        var query = context.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Escape LIKE metacharacters so search is a literal substring, including % and _.
            var literal = search.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{literal}%", "\\"));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var count = await query.CountAsync(cancellationToken);
        var products = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new PagedResult<Product>(products, count, page, pageSize);
    }
}
