using FluentValidation;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Exceptions;
using ProductCatalog.Application.Features.Products.Commands;
using ProductCatalog.Domain.ValueObjects;

namespace ProductCatalog.Application.Features.Products.Handlers;

public sealed class UpdateProductHandler(IProductRepository repository, IUnitOfWork unitOfWork,
    IValidator<UpdateProductCommand> validator, TimeProvider clock)
{
    public async Task<ProductDto> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var product = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new ProductNotFoundException(command.Id);
        if (product.Version != command.ExpectedVersion)
        {
            throw new ProductConcurrencyException();
        }

        product.Update(command.Name, command.Description, new ProductPrice(command.Price),
            command.IsActive, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return product.ToDto();
    }
}
