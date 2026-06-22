using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileCompensationConceptConfiguration : IEntityTypeConfiguration<PersonnelFileCompensationConcept>
{
    public void Configure(EntityTypeBuilder<PersonnelFileCompensationConcept> builder)
    {
        builder.ToTable("personnel_file_compensation_concepts", table =>
        {
            table.HasCheckConstraint(
                "ck_personnel_file_compensation_concepts__value_non_negative",
                "value >= 0");
            table.HasCheckConstraint(
                "ck_personnel_file_compensation_concepts__dates",
                "end_date is null or end_date >= start_date");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_compensation_concepts");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");
        builder.Property(item => item.Nature).HasColumnName("nature").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ConceptTypeCode).HasColumnName("concept_type_code").HasMaxLength(80);
        builder.Property(item => item.DeductionClass).HasColumnName("deduction_class").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.CalculationType).HasColumnName("calculation_type").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Value).HasColumnName("value").HasColumnType("numeric(18,8)");
        builder.Property(item => item.CalculationBaseCode).HasColumnName("calculation_base_code").HasMaxLength(40);
        builder.Property(item => item.EmployerRate).HasColumnName("employer_rate").HasColumnType("numeric(11,8)");
        builder.Property(item => item.ContributionCap).HasColumnName("contribution_cap").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(40);
        builder.Property(item => item.PayPeriodCode).HasColumnName("pay_period_code").HasMaxLength(40);
        builder.Property(item => item.CounterpartyName).HasColumnName("counterparty_name").HasMaxLength(200);
        builder.Property(item => item.ExternalReference).HasColumnName("external_reference").HasMaxLength(120);
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.IsSystemSuggested).HasColumnName("is_system_suggested");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_compensation_concepts__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_compensation_concepts__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.Nature, item.IsActive })
            .HasDatabaseName("ix_personnel_file_compensation_concepts__tenant_file_nature_active");
        builder.HasIndex(item => new { item.TenantId, item.AssignedPositionPublicId })
            .HasDatabaseName("ix_personnel_file_compensation_concepts__tenant_assigned_position");
    }
}
