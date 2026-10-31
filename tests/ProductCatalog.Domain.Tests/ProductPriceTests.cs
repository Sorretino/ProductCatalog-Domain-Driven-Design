using ProductCatalog.Domain.Exceptions;
using ProductCatalog.Domain.ValueObjects;

namespace ProductCatalog.Domain.Tests;

public sealed class ProductPriceTests
{
    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("1000000000000")]
    public void Rejects_unrepresentable_prices(string value) => Assert.Throws<DomainException>(() =>
        new ProductPrice(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("999999999999.99")]
    public void Accepts_boundaries_and_compares_by_value(string value)
    {
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(new ProductPrice(amount), new ProductPrice(amount));
    }
}
