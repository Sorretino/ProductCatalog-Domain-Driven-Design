using ProductCatalog.Domain.Exceptions;

namespace ProductCatalog.Domain.ValueObjects;

/// <summary>A nonnegative price in BRL, representable without database rounding.</summary>
public sealed record ProductPrice
{
    public const decimal Maximum = 999_999_999_999.99m;
    public decimal Amount { get; }

    public ProductPrice(decimal amount)
    {
        if (amount < 0 || amount > Maximum || decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("O preço deve estar entre 0 e 999999999999,99 e ter até duas casas decimais.");
        }

        Amount = amount;
    }
}
