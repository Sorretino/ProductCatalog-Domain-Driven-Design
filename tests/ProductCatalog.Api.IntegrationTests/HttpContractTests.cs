using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.ValueObjects;

namespace ProductCatalog.Api.IntegrationTests;

public sealed class HttpContractTests
{
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Anonymous_catalog_request_is_unauthorized_with_problem_details()
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/products", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.WwwAuthenticate.Any());
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Invalid_token_is_rejected(bool expired, bool wrongAudience, bool wrongSignature)
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            CatalogApiFactory.Token(expired: expired, audience: wrongAudience ? "other-api" : null,
                wrongSignature: wrongSignature));
        var response = await client.GetAsync("/api/products", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Read_scope_cannot_write()
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = AuthorizedClient(factory, "catalog.read");
        var response = await client.PostAsJsonAsync("/api/products", new { name = "Produto", price = 1 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _repository.DidNotReceive().Add(Arg.Any<Product>());
    }

    [Fact]
    public async Task Valid_create_returns_location_etag_and_persisted_product()
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = AuthorizedClient(factory);
        var response = await client.PostAsJsonAsync("/api/products", new { name = " Teclado ", price = 99.90m },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(product);
        Assert.Equal("Teclado", product.Name);
        Assert.True(product.IsActive);
        Assert.EndsWith($"/api/products/{product.Id}", response.Headers.Location?.ToString());
        Assert.Equal($"\"{product.Version:D}\"", response.Headers.ETag?.Tag);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_input_returns_field_errors_and_trace_id()
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = AuthorizedClient(factory);
        var response = await client.PostAsJsonAsync("/api/products", new { name = "", price = -1 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Name", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("Price", out _));
        Assert.True(problem.TryGetProperty("traceId", out _));
        _repository.DidNotReceive().Add(Arg.Any<Product>());
    }

    [Fact]
    public async Task Malformed_json_returns_problem_details()
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = AuthorizedClient(factory);
        using var content = new StringContent("{invalid", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/products", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Missing_price_is_rejected_instead_of_becoming_zero()
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = AuthorizedClient(factory);
        var response = await client.PostAsJsonAsync("/api/products", new { name = "Produto" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _repository.DidNotReceive().Add(Arg.Any<Product>());
    }

    [Theory]
    [InlineData(null, 428)]
    [InlineData("*", 400)]
    [InlineData("W/\"f5e01351-582b-43a8-9c4a-a4ebbb174c7f\"", 400)]
    [InlineData("f5e01351-582b-43a8-9c4a-a4ebbb174c7f", 400)]
    public async Task Delete_requires_single_strong_etag(string? etag, int expectedStatus)
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = AuthorizedClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/products/{Guid.NewGuid()}");
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stale_etag_returns_precondition_failed()
    {
        var product = Product.Create("Original", null, new ProductPrice(1), true, DateTimeOffset.UtcNow);
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = AuthorizedClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/products/{product.Id}");
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{Guid.NewGuid()}\""));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Null(product.DeletedAt);
    }

    [Fact]
    public async Task Missing_product_returns_not_found()
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = AuthorizedClient(factory);
        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unexpected_error_never_exposes_internal_details()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<Product?>>(_ => throw new InvalidOperationException("secret-password-internal"));
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = AuthorizedClient(factory);
        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("secret-password", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Openapi_includes_routes_authentication_and_precondition_header()
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var schema = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(schema.GetProperty("paths").TryGetProperty("/api/products", out _));
        var update = schema.GetProperty("paths").GetProperty("/api/products/{id}").GetProperty("put");
        Assert.Contains(update.GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "If-Match");
        Assert.True(schema.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task Liveness_is_independent_of_database()
    {
        await using var factory = new CatalogApiFactory(_repository, _unitOfWork);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    internal static HttpClient AuthorizedClient(CatalogApiFactory factory, string scope = "catalog.read catalog.write")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token(scope));
        return client;
    }
}
