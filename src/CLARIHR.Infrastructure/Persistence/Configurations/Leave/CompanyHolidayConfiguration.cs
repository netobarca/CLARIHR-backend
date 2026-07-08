using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Leave;

internal sealed class CompanyHolidayConfiguration : IEntityTypeConfiguration<CompanyHoliday>
{
    public void Configure(EntityTypeBuilder<CompanyHoliday> builder)
    {
        builder.ToTable("company_holidays");

        builder.HasKey(holiday => holiday.Id)
            .HasName("pk_company_holidays");

        builder.Property(holiday => holiday.Id)
            .HasColumnName("id");

        builder.Property(holiday => holiday.PublicId)
            .HasColumnName("public_id");

        builder.Property(holiday => holiday.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(holiday => holiday.Date)
            .HasColumnName("date");

        builder.Property(holiday => holiday.Description)
            .HasColumnName("description")
            .HasMaxLength(CompanyHoliday.MaxDescriptionLength);

        builder.Property(holiday => holiday.ScopeCode)
            .HasColumnName("scope_code")
            .HasMaxLength(20);

        builder.Property(holiday => holiday.IsActive)
            .HasColumnName("is_active");

        builder.Property(holiday => holiday.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(holiday => holiday.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(holiday => holiday.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(holiday => holiday.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_company_holidays__public_id");

        builder.HasIndex(holiday => new { holiday.TenantId, holiday.Date })
            .IsUnique()
            .HasDatabaseName(LeaveMasterConstraintNames.CompanyHolidayDateUnique);
    }
}
