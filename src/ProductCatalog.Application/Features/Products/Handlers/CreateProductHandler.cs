using FluentValidation;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Features.Products.Commands;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.ValueObjects;

namespace ProductCatalog.Application.Features.Products.Handlers;

public sealed class CreateProductHandler(IProductRepository repository, IUnitOfWork unitOfWork,
    IValidator<CreateProductCommand> validator, TimeProvider clock)
{
    public async Task<ProductDto> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var product = Product.Create(command.Name, command.Description, new ProductPrice(command.Price),
            command.IsActive, clock.GetUtcNow());
        repository.Add(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return product.ToDto();
    }
}
