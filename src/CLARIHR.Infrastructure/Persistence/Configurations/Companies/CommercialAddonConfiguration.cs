using CLARIHR.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Companies;

internal sealed class CommercialAddonConfiguration : IEntityTypeConfiguration<CommercialAddon>
{
    public void Configure(EntityTypeBuilder<CommercialAddon> builder)
    {
        builder.ToTable("commercial_addons");

        builder.HasKey(addon => addon.Id)
            .HasName("pk_commercial_addons");

        builder.Property(addon => addon.Id)
            .HasColumnName("id");

        builder.Property(addon => addon.PublicId)
            .HasColumnName("public_id");

        builder.Property(addon => addon.Code)
            .HasColumnName("code")
            .HasMaxLength(40);

        builder.Property(addon => addon.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(40);

        builder.Property(addon => addon.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(addon => addon.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(addon => addon.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(addon => addon.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(addon => addon.PricePerActiveEmployee)
            .HasColumnName("price_per_active_employee")
            .HasPrecision(18, 2);

        builder.Property(addon => addon.MinimumMonthlyFee)
            .HasColumnName("minimum_monthly_fee")
            .HasPrecision(18, 2);

        builder.Property(addon => addon.Periodicity)
            .HasColumnName("periodicity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(addon => addon.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(addon => addon.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(addon => addon.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(addon => addon.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(addon => addon.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_commercial_addons__public_id");

        builder.HasIndex(addon => addon.NormalizedCode)
            .IsUnique()
            .HasDatabaseName("uq_commercial_addons__normalized_code");

        builder.HasIndex(addon => addon.NormalizedName)
            .HasDatabaseName("ix_commercial_addons__normalized_name");

        builder.HasIndex(addon => addon.Status)
            .HasDatabaseName("ix_commercial_addons__status");

        builder.HasIndex(addon => addon.Type)
            .HasDatabaseName("ix_commercial_addons__type");
    }
}
