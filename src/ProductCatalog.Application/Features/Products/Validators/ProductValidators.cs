using FluentValidation;
using ProductCatalog.Application.Features.Products.Commands;
using ProductCatalog.Application.Features.Products.Queries;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.ValueObjects;

namespace ProductCatalog.Application.Features.Products.Validators;

public abstract class ProductInputValidator<T> : AbstractValidator<T> where T : IProductInput
{
    protected ProductInputValidator()
    {
        RuleFor(x => x.Name).Must(name => !string.IsNullOrWhiteSpace(name)
            && name.Trim().Length <= Product.MaximumNameLength)
            .WithMessage("Informe um nome com até 200 caracteres.");
        RuleFor(x => x.Description).Must(value => value?.Trim().Length is null or <= Product.MaximumDescriptionLength)
            .WithMessage("A descrição deve ter até 2000 caracteres.");
        RuleFor(x => x.Price).InclusiveBetween(0, ProductPrice.Maximum)
            .Must(value => decimal.Round(value, 2) == value)
            .WithMessage("Informe um preço válido com até duas casas decimais.");
    }
}

public sealed class CreateProductValidator : ProductInputValidator<CreateProductCommand>;

public sealed class UpdateProductValidator : ProductInputValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ExpectedVersion).NotEmpty();
    }
}

public sealed class DeleteProductValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ExpectedVersion).NotEmpty();
    }
}

public sealed class GetProductValidator : AbstractValidator<GetProductQuery>
{
    public GetProductValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ListProductsValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(Product.MaximumNameLength);
    }
}
