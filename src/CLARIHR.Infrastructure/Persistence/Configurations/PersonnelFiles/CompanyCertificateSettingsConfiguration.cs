using CLARIHR.Domain.Companies;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class CompanyCertificateSettingsConfiguration : IEntityTypeConfiguration<CompanyCertificateSettings>
{
    public void Configure(EntityTypeBuilder<CompanyCertificateSettings> builder)
    {
        builder.ToTable("company_certificate_settings");
        builder.HasKey(settings => settings.Id).HasName("pk_company_certificate_settings");

        builder.Property(settings => settings.Id).HasColumnName("id");
        builder.Property(settings => settings.PublicId).HasColumnName("public_id");
        builder.Property(settings => settings.TenantId).HasColumnName("tenant_id");
        builder.Property(settings => settings.LogoFilePublicId).HasColumnName("logo_file_public_id");
        builder.Property(settings => settings.IssuingCity).HasColumnName("issuing_city").HasMaxLength(120);
        builder.Property(settings => settings.SignatoryName).HasColumnName("signatory_name").HasMaxLength(200);
        builder.Property(settings => settings.SignatoryTitle).HasColumnName("signatory_title").HasMaxLength(200);
        builder.Property(settings => settings.FooterText).HasColumnName("footer_text").HasMaxLength(2000);
        builder.Property(settings => settings.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(settings => settings.CreatedUtc).HasColumnName("created_utc");
        builder.Property(settings => settings.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(settings => settings.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_company_certificate_settings__public_id");
        builder.HasIndex(settings => settings.TenantId)
            .IsUnique()
            .HasDatabaseName("uq_company_certificate_settings__tenant_id");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(settings => settings.TenantId)
            .HasPrincipalKey(company => company.PublicId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_certificate_settings__companies");
    }
}
