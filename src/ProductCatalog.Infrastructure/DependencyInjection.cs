using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Infrastructure.Persistence;
using ProductCatalog.Infrastructure.Persistence.Repositories;
using ProductCatalog.Infrastructure.Services;

namespace ProductCatalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Catalog");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configure ConnectionStrings:Catalog.");
        }

        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString,
            postgres => postgres.CommandTimeout(15)));
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CatalogDbContext>());
        services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"],
            timeout: TimeSpan.FromSeconds(5));
        return services;
    }
}
