using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Exceptions;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.ValueObjects;
using ProductCatalog.Infrastructure.Persistence.Repositories;
using ProductCatalog.TestSupport;

namespace ProductCatalog.Infrastructure.IntegrationTests;

public sealed class PersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task Migration_roundtrip_and_soft_delete_preserve_row_and_hide_it_from_queries()
    {
        await using var context = postgres.CreateContext();
        var product = Product.Create("USB " + Guid.NewGuid(), "Descrição", new ProductPrice(12.34m), true, DateTimeOffset.UtcNow);
        var repository = new ProductRepository(context);
        repository.Add(product);
        await ((IUnitOfWork)context).SaveChangesAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(product.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(product.Price, loaded.Price);
        Assert.Equal(product.Version, loaded.Version);
        loaded.Delete(DateTimeOffset.UtcNow);
        await ((IUnitOfWork)context).SaveChangesAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        Assert.Null(await repository.GetByIdAsync(product.Id, CancellationToken.None));
        var persisted = await context.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == product.Id);
        Assert.NotNull(persisted.DeletedAt);
        Assert.False(persisted.IsActive);
        var page = await repository.ListAsync(1, 20, product.Name, null, CancellationToken.None);
        Assert.Empty(page.Items);
    }

    [PostgresFact]
    public async Task Concurrent_writes_are_rejected_by_database_token()
    {
        await using var first = postgres.CreateContext();
        var product = Product.Create("Concorrência", null, new ProductPrice(1), true, DateTimeOffset.UtcNow);
        first.Products.Add(product);
        await first.SaveChangesAsync();
        await using var second = postgres.CreateContext();
        var stale = await second.Products.SingleAsync(x => x.Id == product.Id);
        product.Update("Primeiro", null, new ProductPrice(2), true, DateTimeOffset.UtcNow);
        await ((IUnitOfWork)first).SaveChangesAsync(CancellationToken.None);
        stale.Update("Segundo", null, new ProductPrice(3), true, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ProductConcurrencyException>(() =>
            ((IUnitOfWork)second).SaveChangesAsync(CancellationToken.None));
    }

    [PostgresFact]
    public async Task Search_is_case_insensitive_literal_and_paginated_with_active_filter()
    {
        await using var context = postgres.CreateContext();
        var prefix = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        context.Products.AddRange(
            Product.Create(prefix + " USB_100%", null, new ProductPrice(1), true, now),
            Product.Create(prefix + " USBx100x", null, new ProductPrice(1), true, now.AddSeconds(1)),
            Product.Create(prefix + " USB_100%", null, new ProductPrice(1), false, now.AddSeconds(2)));
        await context.SaveChangesAsync();
        var repository = new ProductRepository(context);
        var literal = await repository.ListAsync(1, 1, prefix + " usb_100%", true, CancellationToken.None);
        Assert.Equal(1, literal.TotalCount);
        Assert.True(Assert.Single(literal.Items).IsActive);
        var secondPage = await repository.ListAsync(2, 1, prefix, null, CancellationToken.None);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.EndsWith("USBx100x", Assert.Single(secondPage.Items).Name);
    }

    [PostgresFact]
    public async Task Database_constraints_reject_invalid_price_outside_domain()
    {
        await using var context = postgres.CreateContext();
        var product = Product.Create("Constraint", null, new ProductPrice(1), true, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE products SET price = -1 WHERE id = {product.Id}"));
    }

    [Fact]
    public void Model_and_migration_snapshot_are_in_sync_without_database()
    {
        using var context = new ProductCatalog.Infrastructure.Persistence.CatalogDbContext(
            new DbContextOptionsBuilder<ProductCatalog.Infrastructure.Persistence.CatalogDbContext>()
                .UseNpgsql("Host=localhost;Database=model_only;Username=model_only").Options);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.NotEmpty(context.Database.GetMigrations());
    }
}
