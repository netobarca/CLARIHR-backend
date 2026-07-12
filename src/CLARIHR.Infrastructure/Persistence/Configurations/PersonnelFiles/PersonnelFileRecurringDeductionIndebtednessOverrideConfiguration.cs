using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileRecurringDeductionIndebtednessOverrideConfiguration
    : IEntityTypeConfiguration<PersonnelFileRecurringDeductionIndebtednessOverride>
{
    public void Configure(EntityTypeBuilder<PersonnelFileRecurringDeductionIndebtednessOverride> builder)
    {
        // Abbreviated on purpose: PostgreSQL truncates identifiers at 63 chars.
        builder.ToTable("pf_rd_indebtedness_overrides");

        builder.HasKey(item => item.Id).HasName("pk_pf_rd_indebtedness_overrides");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.RecurringDeductionId).HasColumnName("recurring_deduction_id");
        builder.Property(item => item.Stage)
            .HasColumnName("stage")
            .HasMaxLength(PersonnelFileRecurringDeductionIndebtednessOverride.MaxStageLength);
        builder.Property(item => item.AcknowledgedByUserId).HasColumnName("acknowledged_by_user_id");
        builder.Property(item => item.AcknowledgedUtc).HasColumnName("acknowledged_utc");
        builder.Property(item => item.BaseIncome).HasColumnName("base_income").HasColumnType("numeric(18,2)");
        builder.Property(item => item.MonthlyLoad).HasColumnName("monthly_load").HasColumnType("numeric(18,2)");
        builder.Property(item => item.NewInstallment).HasColumnName("new_installment").HasColumnType("numeric(18,2)");
        builder.Property(item => item.ProjectedPercent).HasColumnName("projected_percent").HasColumnType("numeric(11,4)");
        builder.Property(item => item.LimitPercent).HasColumnName("limit_percent").HasColumnType("numeric(11,4)");
        builder.Property(item => item.LimitSource)
            .HasColumnName("limit_source")
            .HasMaxLength(PersonnelFileRecurringDeductionIndebtednessOverride.MaxLimitSourceLength);
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_rd_indebtedness_overrides__public_id");

        builder.HasIndex(item => new { item.RecurringDeductionId, item.AcknowledgedUtc })
            .HasDatabaseName("ix_pf_rd_indebt_overrides__deduction_utc");
    }
}
