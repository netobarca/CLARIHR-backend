using CLARIHR.Domain.CostCenters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.CostCenters;

internal sealed class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ToTable("cost_centers");

        builder.HasKey(costCenter => costCenter.Id)
            .HasName("pk_cost_centers");

        builder.Property(costCenter => costCenter.Id)
            .HasColumnName("id");

        builder.Property(costCenter => costCenter.PublicId)
            .HasColumnName("public_id");

        builder.Property(costCenter => costCenter.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(costCenter => costCenter.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(costCenter => costCenter.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(costCenter => costCenter.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(costCenter => costCenter.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(costCenter => costCenter.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(costCenter => costCenter.PayrollExpenseAccountCode)
            .HasColumnName("payroll_expense_account_code")
            .HasMaxLength(100);

        builder.Property(costCenter => costCenter.EmployerContributionAccountCode)
            .HasColumnName("employer_contribution_account_code")
            .HasMaxLength(100);

        builder.Property(costCenter => costCenter.ProvisionAccountCode)
            .HasColumnName("provision_account_code")
            .HasMaxLength(100);

        builder.Property(costCenter => costCenter.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(costCenter => costCenter.IsActive)
            .HasColumnName("is_active");

        builder.Property(costCenter => costCenter.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(costCenter => costCenter.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(costCenter => costCenter.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(costCenter => costCenter.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_cost_centers__public_id");

        builder.HasIndex(costCenter => new { costCenter.TenantId, costCenter.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_cost_centers__tenant_code");

        builder.HasIndex(costCenter => new { costCenter.TenantId, costCenter.Type, costCenter.IsActive })
            .HasDatabaseName("ix_cost_centers__tenant_type_active");

        builder.HasIndex(costCenter => new { costCenter.TenantId, costCenter.NormalizedName })
            .HasDatabaseName("ix_cost_centers__tenant_normalized_name");
    }
}
