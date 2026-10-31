using ProductCatalog.Domain.Exceptions;
using ProductCatalog.Domain.ValueObjects;

namespace ProductCatalog.Domain.Entities;

public sealed class Product
{
    public const int MaximumNameLength = 200;
    public const int MaximumDescriptionLength = 2000;

    private Product() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public ProductPrice Price { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid Version { get; private set; }

    public static Product Create(string name, string? description, ProductPrice price,
        bool isActive, DateTimeOffset now)
    {
        var values = Validate(name, description, price);
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = values.Name,
            Description = values.Description,
            Price = price,
            IsActive = isActive,
            CreatedAt = now.ToUniversalTime(),
            UpdatedAt = now.ToUniversalTime(),
            Version = Guid.NewGuid()
        };
    }

    public void Update(string name, string? description, ProductPrice price,
        bool isActive, DateTimeOffset now)
    {
        EnsureNotDeleted();
        var values = Validate(name, description, price);
        // Validate every value before changing any state.
        Name = values.Name;
        Description = values.Description;
        Price = price;
        IsActive = isActive;
        UpdatedAt = now.ToUniversalTime();
        Version = Guid.NewGuid();
    }

    public void Delete(DateTimeOffset now)
    {
        EnsureNotDeleted();
        DeletedAt = now.ToUniversalTime();
        UpdatedAt = DeletedAt.Value;
        IsActive = false;
        Version = Guid.NewGuid();
    }

    private void EnsureNotDeleted()
    {
        if (DeletedAt.HasValue)
        {
            throw new DomainException("Um produto excluído não pode ser alterado.");
        }
    }

    private static (string Name, string? Description) Validate(string name,
        string? description, ProductPrice price)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaximumNameLength)
        {
            throw new DomainException($"O nome é obrigatório e deve ter até {MaximumNameLength} caracteres.");
        }

        if (description?.Trim().Length > MaximumDescriptionLength)
        {
            throw new DomainException($"A descrição deve ter até {MaximumDescriptionLength} caracteres.");
        }

        ArgumentNullException.ThrowIfNull(price);
        return (name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim());
    }
}
