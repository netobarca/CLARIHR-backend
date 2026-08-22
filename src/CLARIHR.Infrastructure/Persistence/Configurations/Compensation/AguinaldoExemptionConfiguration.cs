using CLARIHR.Domain.Compensation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Compensation;

internal sealed class AguinaldoExemptionConfiguration : IEntityTypeConfiguration<AguinaldoExemption>
{
    public void Configure(EntityTypeBuilder<AguinaldoExemption> builder)
    {
        builder.ToTable("aguinaldo_exemptions", table =>
            table.HasCheckConstraint(
                "ck_aguinaldo_exemptions__amount",
                "exempt_amount >= 0"));

        builder.HasKey(item => item.Id).HasName("pk_aguinaldo_exemptions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.Year).HasColumnName("year");
        builder.Property(item => item.ExemptAmount).HasColumnName("exempt_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_aguinaldo_exemptions__public_id");

        // Un año, un monto: la ley publica UN valor. Dos filas para 2026 serían dos verdades y la corrida
        // tendría que elegir una en silencio.
        builder.HasIndex(item => new { item.TenantId, item.Year })
            .IsUnique()
            .HasDatabaseName("uq_aguinaldo_exemptions__tenant_year");
    }
}
