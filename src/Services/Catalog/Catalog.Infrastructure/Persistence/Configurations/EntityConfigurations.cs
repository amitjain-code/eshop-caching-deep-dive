using Catalog.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Sku).HasMaxLength(64).IsRequired();
        builder.HasIndex(p => p.Sku).IsUnique();
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.PictureUrl).HasMaxLength(500);
        builder.Property(p => p.Version).IsConcurrencyToken();
        builder.HasOne(p => p.Brand).WithMany().HasForeignKey(p => p.BrandId);
        builder.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId);
        builder.HasIndex(p => new { p.CategoryId, p.BrandId });
    }
}

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();
    }
}

internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("stores");
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.City).HasMaxLength(100).IsRequired();
    }
}
