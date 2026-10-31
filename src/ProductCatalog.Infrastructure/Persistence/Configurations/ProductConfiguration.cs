using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.ValueObjects;

namespace ProductCatalog.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", table =>
        {
            table.HasCheckConstraint("ck_products_price", "price >= 0 AND price <= 999999999999.99");
            table.HasCheckConstraint("ck_products_name", "length(btrim(name)) BETWEEN 1 AND 200");
            table.HasCheckConstraint("ck_products_deleted_inactive", "deleted_at IS NULL OR is_active = false");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(Product.MaximumNameLength).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(Product.MaximumDescriptionLength);
        builder.Property(x => x.Price).HasColumnName("price").HasPrecision(14, 2)
            .HasConversion(price => price.Amount, amount => new ProductPrice(amount)).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.CreatedAt, x.Id }).HasFilter("deleted_at IS NULL");
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
