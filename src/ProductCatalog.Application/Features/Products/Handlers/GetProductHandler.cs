using FluentValidation;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Exceptions;
using ProductCatalog.Application.Features.Products.Queries;

namespace ProductCatalog.Application.Features.Products.Handlers;

public sealed class GetProductHandler(IProductRepository repository, IValidator<GetProductQuery> validator)
{
    public async Task<ProductDto> HandleAsync(GetProductQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        var product = await repository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new ProductNotFoundException(query.Id);
        return product.ToDto();
    }
}
