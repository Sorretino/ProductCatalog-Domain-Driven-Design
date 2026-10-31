using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.TestSupport;

namespace ProductCatalog.Api.IntegrationTests;

public sealed class ProductLifecycleTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task Full_http_lifecycle_persists_updates_and_soft_delete_in_postgres()
    {
        await using var factory = new CatalogApiFactory(connectionString: postgres.ConnectionString);
        using var client = HttpContractTests.AuthorizedClient(factory);
        var created = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Teclado " + Guid.NewGuid(),
            description = "USB",
            price = 129.90m,
            isActive = true
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = await created.Content.ReadFromJsonAsync<ProductDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(product);

        var get = await client.GetAsync($"/api/products/{product.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(created.Headers.ETag, get.Headers.ETag);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/products/{product.Id}")
        {
            Content = JsonContent.Create(new { name = product.Name, description = "Atualizado", price = 99.90m, isActive = false })
        };
        updateRequest.Headers.IfMatch.Add(get.Headers.ETag!);
        var updatedResponse = await client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<ProductDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(99.90m, updated.Price);
        Assert.False(updated.IsActive);
        Assert.NotEqual(product.Version, updated.Version);

        using var staleDelete = new HttpRequestMessage(HttpMethod.Delete, $"/api/products/{product.Id}");
        staleDelete.Headers.IfMatch.Add(get.Headers.ETag!);
        Assert.Equal(HttpStatusCode.PreconditionFailed,
            (await client.SendAsync(staleDelete, TestContext.Current.CancellationToken)).StatusCode);

        var list = await client.GetFromJsonAsync<PagedResult<ProductDto>>(
            $"/api/products?search={Uri.EscapeDataString(product.Name)}&isActive=false&pageSize=1",
            TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Equal(product.Id, Assert.Single(list.Items).Id);

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/products/{product.Id}");
        delete.Headers.IfMatch.Add(updatedResponse.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.SendAsync(delete, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/products/{product.Id}", TestContext.Current.CancellationToken)).StatusCode);
        var afterDelete = await client.GetFromJsonAsync<PagedResult<ProductDto>>(
            $"/api/products?search={Uri.EscapeDataString(product.Name)}", TestContext.Current.CancellationToken);
        Assert.NotNull(afterDelete);
        Assert.Empty(afterDelete.Items);

        await using var context = postgres.CreateContext();
        var stored = await context.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == product.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(stored.DeletedAt);
        Assert.False(stored.IsActive);
    }
}
