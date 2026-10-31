using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.Exceptions;
using ProductCatalog.Domain.ValueObjects;

namespace ProductCatalog.Domain.Tests;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_values_and_sets_identity_and_utc_timestamps()
    {
        var product = Product.Create("  Teclado  ", "  USB  ", new ProductPrice(99.90m), true,
            Now.ToOffset(TimeSpan.FromHours(-3)));
        Assert.Equal("Teclado", product.Name);
        Assert.Equal("USB", product.Description);
        Assert.Equal(99.90m, product.Price.Amount);
        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.NotEqual(Guid.Empty, product.Version);
        Assert.Equal(Now, product.CreatedAt);
        Assert.Equal(TimeSpan.Zero, product.CreatedAt.Offset);
        Assert.Null(product.DeletedAt);
        Assert.True(product.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_missing_name(string? name) => Assert.Throws<DomainException>(() =>
        Product.Create(name!, null, new ProductPrice(0), true, Now));

    [Fact]
    public void Create_enforces_text_lengths()
    {
        Assert.Throws<DomainException>(() => Product.Create(new string('a', 201), null, new ProductPrice(1), true, Now));
        Assert.Throws<DomainException>(() => Product.Create("Produto", new string('a', 2001), new ProductPrice(1), true, Now));
        var boundary = Product.Create(new string('a', 200), new string('a', 2000), new ProductPrice(1), true, Now);
        Assert.Equal(200, boundary.Name.Length);
        Assert.Equal(2000, boundary.Description!.Length);
    }

    [Fact]
    public void Invalid_update_preserves_every_existing_value()
    {
        var product = Product.Create("Original", "Descrição", new ProductPrice(12), true, Now);
        var version = product.Version;
        Assert.Throws<DomainException>(() => product.Update("Novo", new string('a', 2001),
            new ProductPrice(20), false, Now.AddHours(1)));
        Assert.Equal("Original", product.Name);
        Assert.Equal("Descrição", product.Description);
        Assert.Equal(12, product.Price.Amount);
        Assert.True(product.IsActive);
        Assert.Equal(Now, product.UpdatedAt);
        Assert.Equal(version, product.Version);
    }

    [Fact]
    public void Update_changes_version_and_keeps_identity_and_creation_time()
    {
        var product = Product.Create("Original", null, new ProductPrice(12), true, Now);
        var id = product.Id;
        var version = product.Version;
        product.Update(" Novo ", "  ", new ProductPrice(0), false, Now.AddHours(1));
        Assert.Equal(id, product.Id);
        Assert.Equal(Now, product.CreatedAt);
        Assert.Equal(Now.AddHours(1), product.UpdatedAt);
        Assert.NotEqual(version, product.Version);
        Assert.Equal("Novo", product.Name);
        Assert.Null(product.Description);
        Assert.False(product.IsActive);
    }

    [Fact]
    public void Delete_is_logical_and_prevents_further_mutations()
    {
        var product = Product.Create("Produto", null, new ProductPrice(1), true, Now);
        product.Delete(Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(1), product.DeletedAt);
        Assert.False(product.IsActive);
        Assert.Throws<DomainException>(() => product.Update("Outro", null, new ProductPrice(2), true, Now));
        Assert.Throws<DomainException>(() => product.Delete(Now));
    }
}
