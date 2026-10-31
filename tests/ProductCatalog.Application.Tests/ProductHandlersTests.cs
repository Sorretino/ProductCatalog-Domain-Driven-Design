using FluentValidation;
using NSubstitute;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Exceptions;
using ProductCatalog.Application.Features.Products.Commands;
using ProductCatalog.Application.Features.Products.Handlers;
using ProductCatalog.Application.Features.Products.Queries;
using ProductCatalog.Application.Features.Products.Validators;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.ValueObjects;

namespace ProductCatalog.Application.Tests;

public sealed class ProductHandlersTests
{
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider _clock = Substitute.For<TimeProvider>();
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    public ProductHandlersTests() => _clock.GetUtcNow().Returns(Now);

    [Fact]
    public async Task Create_persists_once_and_propagates_cancellation()
    {
        using var source = new CancellationTokenSource();
        var handler = new CreateProductHandler(_repository, _unitOfWork, new CreateProductValidator(), _clock);
        var result = await handler.HandleAsync(new CreateProductCommand(" Produto ", null, 12), source.Token);
        Assert.Equal("Produto", result.Name);
        Assert.Equal(Now, result.CreatedAt);
        _repository.Received(1).Add(Arg.Is<Product>(x => x.Id == result.Id && x.Price.Amount == 12));
        await _unitOfWork.Received(1).SaveChangesAsync(source.Token);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Produto", -1)]
    [InlineData("Produto", 1.001)]
    public async Task Invalid_create_does_not_touch_persistence(string name, double price)
    {
        var handler = new CreateProductHandler(_repository, _unitOfWork, new CreateProductValidator(), _clock);
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(
            new CreateProductCommand(name, null, (decimal)price), CancellationToken.None));
        _repository.DidNotReceive().Add(Arg.Any<Product>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_changes_the_aggregate_and_saves()
    {
        var product = Existing();
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new UpdateProductHandler(_repository, _unitOfWork, new UpdateProductValidator(), _clock);
        var result = await handler.HandleAsync(new UpdateProductCommand(product.Id, "Novo", "Texto", 25,
            false, product.Version), CancellationToken.None);
        Assert.Equal("Novo", result.Name);
        Assert.False(result.IsActive);
        Assert.Equal(25, result.Price);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Missing_update_throws_not_found_without_saving()
    {
        var handler = new UpdateProductHandler(_repository, _unitOfWork, new UpdateProductValidator(), _clock);
        await Assert.ThrowsAsync<ProductNotFoundException>(() => handler.HandleAsync(
            new UpdateProductCommand(Guid.NewGuid(), "Novo", null, 1, true, Guid.NewGuid()), CancellationToken.None));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stale_update_preserves_the_aggregate_and_does_not_save()
    {
        var product = Existing();
        var originalVersion = product.Version;
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new UpdateProductHandler(_repository, _unitOfWork, new UpdateProductValidator(), _clock);
        await Assert.ThrowsAsync<ProductConcurrencyException>(() => handler.HandleAsync(
            new UpdateProductCommand(product.Id, "Novo", null, 1, true, Guid.NewGuid()), CancellationToken.None));
        Assert.Equal("Existente", product.Name);
        Assert.Equal(originalVersion, product.Version);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_marks_the_product_without_removing_it()
    {
        var product = Existing();
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new DeleteProductHandler(_repository, _unitOfWork, new DeleteProductValidator(), _clock);
        await handler.HandleAsync(new DeleteProductCommand(product.Id, product.Version), CancellationToken.None);
        Assert.Equal(Now, product.DeletedAt);
        Assert.False(product.IsActive);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stale_delete_does_not_change_product()
    {
        var product = Existing();
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new DeleteProductHandler(_repository, _unitOfWork, new DeleteProductValidator(), _clock);
        await Assert.ThrowsAsync<ProductConcurrencyException>(() => handler.HandleAsync(
            new DeleteProductCommand(product.Id, Guid.NewGuid()), CancellationToken.None));
        Assert.Null(product.DeletedAt);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_missing_product_throws_not_found()
    {
        var handler = new GetProductHandler(_repository, new GetProductValidator());
        await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            handler.HandleAsync(new GetProductQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task List_maps_results_and_trims_search()
    {
        var product = Existing();
        _repository.ListAsync(2, 5, "USB", false, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([product], 11, 2, 5));
        var handler = new ListProductsHandler(_repository, new ListProductsValidator());
        var result = await handler.HandleAsync(new ListProductsQuery(2, 5, " USB ", false), CancellationToken.None);
        Assert.Equal(11, result.TotalCount);
        Assert.Equal(product.Id, Assert.Single(result.Items).Id);
        Assert.Equal(2, result.Page);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task Invalid_pagination_is_rejected_before_querying(int page, int pageSize)
    {
        var handler = new ListProductsHandler(_repository, new ListProductsValidator());
        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new ListProductsQuery(page, pageSize), CancellationToken.None));
        await _repository.DidNotReceive().ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(),
            Arg.Any<bool?>(), Arg.Any<CancellationToken>());
    }

    private static Product Existing() => Product.Create("Existente", null, new ProductPrice(10), true, Now.AddDays(-1));
}
