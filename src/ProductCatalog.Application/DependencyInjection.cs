using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductCatalog.Application.Features.Products.Commands;
using ProductCatalog.Application.Features.Products.Handlers;
using ProductCatalog.Application.Features.Products.Queries;
using ProductCatalog.Application.Features.Products.Validators;

namespace ProductCatalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IValidator<CreateProductCommand>, CreateProductValidator>();
        services.AddScoped<IValidator<UpdateProductCommand>, UpdateProductValidator>();
        services.AddScoped<IValidator<DeleteProductCommand>, DeleteProductValidator>();
        services.AddScoped<IValidator<GetProductQuery>, GetProductValidator>();
        services.AddScoped<IValidator<ListProductsQuery>, ListProductsValidator>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<DeleteProductHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<ListProductsHandler>();
        return services;
    }
}
