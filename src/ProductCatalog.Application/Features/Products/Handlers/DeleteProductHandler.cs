using FluentValidation;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Exceptions;
using ProductCatalog.Application.Features.Products.Commands;

namespace ProductCatalog.Application.Features.Products.Handlers;

public sealed class DeleteProductHandler(IProductRepository repository, IUnitOfWork unitOfWork,
    IValidator<DeleteProductCommand> validator, TimeProvider clock)
{
    public async Task HandleAsync(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var product = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new ProductNotFoundException(command.Id);
        if (product.Version != command.ExpectedVersion)
        {
            throw new ProductConcurrencyException();
        }

        product.Delete(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
