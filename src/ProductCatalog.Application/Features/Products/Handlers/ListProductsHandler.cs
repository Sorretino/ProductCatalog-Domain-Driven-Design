using FluentValidation;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Features.Products.Queries;

namespace ProductCatalog.Application.Features.Products.Handlers;

public sealed class ListProductsHandler(IProductRepository repository, IValidator<ListProductsQuery> validator)
{
    public async Task<PagedResult<ProductDto>> HandleAsync(ListProductsQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        var page = await repository.ListAsync(query.Page, query.PageSize, query.Search?.Trim(),
            query.IsActive, cancellationToken);
        return new PagedResult<ProductDto>(page.Items.Select(x => x.ToDto()).ToArray(),
            page.TotalCount, page.Page, page.PageSize);
    }
}
