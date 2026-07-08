using CLARIHR.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Leave;

internal sealed class IncapacityRiskParameterConfiguration : IEntityTypeConfiguration<IncapacityRiskParameter>
{
    public void Configure(EntityTypeBuilder<IncapacityRiskParameter> builder)
    {
        builder.ToTable("incapacity_risk_parameters", table =>
            table.HasCheckConstraint(
                "ck_incapacity_risk_parameters__bounds",
                "day_to is null or day_to >= day_from"));

        builder.HasKey(parameter => parameter.Id)
            .HasName("pk_incapacity_risk_parameters");

        builder.Property(parameter => parameter.Id)
            .HasColumnName("id");

        builder.Property(parameter => parameter.PublicId)
            .HasColumnName("public_id");

        builder.Property(parameter => parameter.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(parameter => parameter.IncapacityRiskId)
            .HasColumnName("incapacity_risk_id");

        builder.Property(parameter => parameter.DayFrom)
            .HasColumnName("day_from");

        builder.Property(parameter => parameter.DayTo)
            .HasColumnName("day_to");

        builder.Property(parameter => parameter.SubsidyPercent)
            .HasColumnName("subsidy_percent")
            .HasColumnType("numeric(5,2)");

        builder.Property(parameter => parameter.PayerCode)
            .HasColumnName("payer_code")
            .HasMaxLength(IncapacityRiskParameter.MaxPayerCodeLength);

        builder.Property(parameter => parameter.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(parameter => parameter.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(parameter => parameter.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(parameter => parameter.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_incapacity_risk_parameters__public_id");

        builder.HasIndex(parameter => new { parameter.TenantId, parameter.IncapacityRiskId, parameter.SortOrder })
            .HasDatabaseName("ix_incapacity_risk_parameters__risk_sort");
    }
}
