using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.CompetencyFramework;

internal sealed class OccupationalPyramidLevelConfiguration : IEntityTypeConfiguration<OccupationalPyramidLevel>
{
    public void Configure(EntityTypeBuilder<OccupationalPyramidLevel> builder)
    {
        builder.ToTable("occupational_pyramid_levels");

        builder.HasKey(level => level.Id)
            .HasName("pk_occupational_pyramid_levels");

        builder.Property(level => level.Id).HasColumnName("id");
        builder.Property(level => level.PublicId).HasColumnName("public_id");
        builder.Property(level => level.TenantId).HasColumnName("tenant_id");

        builder.Property(level => level.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(level => level.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(level => level.Name)
            .HasColumnName("name")
            .HasMaxLength(120);

        builder.Property(level => level.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(120);

        builder.Property(level => level.LevelOrder)
            .HasColumnName("level_order");

        builder.Property(level => level.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(level => level.IsActive)
            .HasColumnName("is_active");

        builder.Property(level => level.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(level => level.CreatedUtc).HasColumnName("created_utc");
        builder.Property(level => level.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(level => level.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_occupational_pyramid_levels__public_id");

        builder.HasIndex(level => new { level.TenantId, level.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_occupational_pyramid_levels__tenant_code");

        builder.HasIndex(level => new { level.TenantId, level.LevelOrder })
            .IsUnique()
            .HasDatabaseName("uq_occupational_pyramid_levels__tenant_level_order");

        builder.HasIndex(level => new { level.TenantId, level.IsActive })
            .HasDatabaseName("ix_occupational_pyramid_levels__tenant_active");

        builder.HasIndex(level => new { level.TenantId, level.NormalizedName })
            .HasDatabaseName("ix_occupational_pyramid_levels__tenant_name");
    }
}

internal sealed class CompetencyConductConfiguration : IEntityTypeConfiguration<CompetencyConduct>
{
    public void Configure(EntityTypeBuilder<CompetencyConduct> builder)
    {
        builder.ToTable("competency_conducts");

        builder.HasKey(conduct => conduct.Id)
            .HasName("pk_competency_conducts");

        builder.Property(conduct => conduct.Id).HasColumnName("id");
        builder.Property(conduct => conduct.PublicId).HasColumnName("public_id");
        builder.Property(conduct => conduct.TenantId).HasColumnName("tenant_id");

        builder.Property(conduct => conduct.CompetencyCatalogItemId)
            .HasColumnName("competency_catalog_item_id");

        builder.Property(conduct => conduct.CompetencyTypeCatalogItemId)
            .HasColumnName("competency_type_catalog_item_id");

        builder.Property(conduct => conduct.BehaviorLevelCatalogItemId)
            .HasColumnName("behavior_level_catalog_item_id");

        builder.Property(conduct => conduct.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(conduct => conduct.NormalizedDescription)
            .HasColumnName("normalized_description")
            .HasMaxLength(1000);

        builder.Property(conduct => conduct.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(conduct => conduct.IsActive)
            .HasColumnName("is_active");

        builder.Property(conduct => conduct.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(conduct => conduct.CreatedUtc).HasColumnName("created_utc");
        builder.Property(conduct => conduct.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(conduct => conduct.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_competency_conducts__public_id");

        builder.HasIndex(conduct => new
            {
                conduct.TenantId,
                conduct.CompetencyCatalogItemId,
                conduct.CompetencyTypeCatalogItemId,
                conduct.BehaviorLevelCatalogItemId,
                conduct.NormalizedDescription
            })
            .IsUnique()
            .HasDatabaseName("uq_competency_conducts__tenant_competency_type_level_desc");

        builder.HasIndex(conduct => new
            {
                conduct.TenantId,
                conduct.CompetencyCatalogItemId,
                conduct.CompetencyTypeCatalogItemId,
                conduct.BehaviorLevelCatalogItemId
            })
            .HasDatabaseName("ix_competency_conducts__tenant_competency_type_level");

        builder.HasIndex(conduct => new { conduct.TenantId, conduct.IsActive })
            .HasDatabaseName("ix_competency_conducts__tenant_active");

        builder.HasOne<JobCatalogItem>()
            .WithMany()
            .HasForeignKey(conduct => conduct.CompetencyCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_competency_conducts__competency_catalog_item");

        builder.HasOne<JobCatalogItem>()
            .WithMany()
            .HasForeignKey(conduct => conduct.CompetencyTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_competency_conducts__competency_type_catalog_item");

        builder.HasOne<JobCatalogItem>()
            .WithMany()
            .HasForeignKey(conduct => conduct.BehaviorLevelCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_competency_conducts__behavior_level_catalog_item");

        builder.HasMany(conduct => conduct.Behaviors)
            .WithOne(behavior => behavior.CompetencyConduct)
            .HasForeignKey(behavior => behavior.CompetencyConductId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_competency_conduct_behaviors__conduct");

        builder.Navigation(conduct => conduct.Behaviors).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CompetencyConductBehaviorConfiguration : IEntityTypeConfiguration<CompetencyConductBehavior>
{
    public void Configure(EntityTypeBuilder<CompetencyConductBehavior> builder)
    {
        builder.ToTable("competency_conduct_behaviors");

        builder.HasKey(behavior => behavior.Id)
            .HasName("pk_competency_conduct_behaviors");

        builder.Property(behavior => behavior.Id).HasColumnName("id");
        builder.Property(behavior => behavior.TenantId).HasColumnName("tenant_id");
        builder.Property(behavior => behavior.CompetencyConductId).HasColumnName("competency_conduct_id");
        builder.Property(behavior => behavior.BehaviorCatalogItemId).HasColumnName("behavior_catalog_item_id");

        builder.Property(behavior => behavior.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(behavior => behavior.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(behavior => behavior.CreatedUtc).HasColumnName("created_utc");
        builder.Property(behavior => behavior.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(behavior => new
            {
                behavior.TenantId,
                behavior.CompetencyConductId,
                behavior.BehaviorCatalogItemId
            })
            .IsUnique()
            .HasDatabaseName("uq_competency_conduct_behaviors__tenant_conduct_behavior");

        builder.HasIndex(behavior => new
            {
                behavior.TenantId,
                behavior.CompetencyConductId,
                behavior.SortOrder
            })
            .HasDatabaseName("ix_competency_conduct_behaviors__tenant_conduct_sort");

        builder.HasOne<JobCatalogItem>()
            .WithMany()
            .HasForeignKey(behavior => behavior.BehaviorCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_competency_conduct_behaviors__behavior_catalog_item");
    }
}

internal sealed class JobProfileCompetencyExpectationConfiguration : IEntityTypeConfiguration<JobProfileCompetencyExpectation>
{
    public void Configure(EntityTypeBuilder<JobProfileCompetencyExpectation> builder)
    {
        builder.ToTable("job_profile_competency_expectations");

        builder.HasKey(expectation => expectation.Id)
            .HasName("pk_job_profile_competency_expectations");

        builder.Property(expectation => expectation.Id).HasColumnName("id");
        builder.Property(expectation => expectation.PublicId).HasColumnName("public_id");
        builder.Property(expectation => expectation.TenantId).HasColumnName("tenant_id");
        builder.Property(expectation => expectation.JobProfileId).HasColumnName("job_profile_id");
        builder.Property(expectation => expectation.OccupationalPyramidLevelId).HasColumnName("occupational_pyramid_level_id");
        builder.Property(expectation => expectation.CompetencyCatalogItemId).HasColumnName("competency_catalog_item_id");
        builder.Property(expectation => expectation.CompetencyTypeCatalogItemId).HasColumnName("competency_type_catalog_item_id");
        builder.Property(expectation => expectation.BehaviorLevelCatalogItemId).HasColumnName("behavior_level_catalog_item_id");

        builder.Property(expectation => expectation.ExpectedEvidence)
            .HasColumnName("expected_evidence")
            .HasMaxLength(1000);

        builder.Property(expectation => expectation.ExpectedValue)
            .HasColumnName("expected_value")
            .HasColumnType("numeric(18,2)");

        builder.Property(expectation => expectation.SortOrder).HasColumnName("sort_order");

        builder.Property(expectation => expectation.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(expectation => expectation.CreatedUtc).HasColumnName("created_utc");
        builder.Property(expectation => expectation.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(expectation => expectation.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_job_profile_competency_expectations__public_id");

        builder.HasIndex(expectation => new
            {
                expectation.TenantId,
                expectation.JobProfileId,
                expectation.CompetencyCatalogItemId,
                expectation.CompetencyTypeCatalogItemId,
                expectation.BehaviorLevelCatalogItemId,
                expectation.OccupationalPyramidLevelId
            })
            .IsUnique()
            .HasDatabaseName("uq_job_profile_competency_expectations__tenant_profile_competency_level");

        builder.HasIndex(expectation => new { expectation.TenantId, expectation.JobProfileId, expectation.SortOrder })
            .HasDatabaseName("ix_job_profile_competency_expectations__tenant_profile_sort");

        builder.HasOne<JobProfile>()
            .WithMany()
            .HasForeignKey(expectation => expectation.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_competency_expectations__job_profile");

        builder.HasOne<OccupationalPyramidLevel>()
            .WithMany()
            .HasForeignKey(expectation => expectation.OccupationalPyramidLevelId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_competency_expectations__pyramid_level");

        builder.HasOne<JobCatalogItem>()
            .WithMany()
            .HasForeignKey(expectation => expectation.CompetencyCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_competency_expectations__competency_catalog_item");

        builder.HasOne<JobCatalogItem>()
            .WithMany()
            .HasForeignKey(expectation => expectation.CompetencyTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_competency_expectations__competency_type_catalog_item");

        builder.HasOne<JobCatalogItem>()
            .WithMany()
            .HasForeignKey(expectation => expectation.BehaviorLevelCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_competency_expectations__behavior_level_catalog_item");

        builder.HasMany(expectation => expectation.Conducts)
            .WithOne(conduct => conduct.JobProfileCompetencyExpectation)
            .HasForeignKey(conduct => conduct.JobProfileCompetencyExpectationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_competency_expectation_conducts__expectation");

        builder.Navigation(expectation => expectation.Conducts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class JobProfileCompetencyExpectationConductConfiguration : IEntityTypeConfiguration<JobProfileCompetencyExpectationConduct>
{
    public void Configure(EntityTypeBuilder<JobProfileCompetencyExpectationConduct> builder)
    {
        builder.ToTable("job_profile_competency_expectation_conducts");

        builder.HasKey(conduct => conduct.Id)
            .HasName("pk_job_profile_competency_expectation_conducts");

        builder.Property(conduct => conduct.Id).HasColumnName("id");
        builder.Property(conduct => conduct.TenantId).HasColumnName("tenant_id");
        builder.Property(conduct => conduct.JobProfileCompetencyExpectationId).HasColumnName("job_profile_competency_expectation_id");
        builder.Property(conduct => conduct.CompetencyConductId).HasColumnName("competency_conduct_id");
        builder.Property(conduct => conduct.SortOrder).HasColumnName("sort_order");
        builder.Property(conduct => conduct.CreatedUtc).HasColumnName("created_utc");
        builder.Property(conduct => conduct.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(conduct => new
            {
                conduct.TenantId,
                conduct.JobProfileCompetencyExpectationId,
                conduct.CompetencyConductId
            })
            .IsUnique()
            .HasDatabaseName("uq_job_profile_competency_expectation_conducts__tenant_expectation_conduct");

        builder.HasIndex(conduct => new
            {
                conduct.TenantId,
                conduct.JobProfileCompetencyExpectationId,
                conduct.SortOrder
            })
            .HasDatabaseName("ix_job_profile_competency_expectation_conducts__tenant_expectation_sort");

        builder.HasOne<CompetencyConduct>()
            .WithMany()
            .HasForeignKey(conduct => conduct.CompetencyConductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_competency_expectation_conducts__competency_conduct");
    }
}

internal sealed class CompetencyRatingScaleConfiguration : IEntityTypeConfiguration<CompetencyRatingScale>
{
    public void Configure(EntityTypeBuilder<CompetencyRatingScale> builder)
    {
        builder.ToTable("competency_rating_scales");

        builder.HasKey(scale => scale.Id)
            .HasName("pk_competency_rating_scales");

        builder.Property(scale => scale.Id).HasColumnName("id");
        builder.Property(scale => scale.PublicId).HasColumnName("public_id");
        builder.Property(scale => scale.TenantId).HasColumnName("tenant_id");

        builder.Property(scale => scale.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(scale => scale.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(scale => scale.Name)
            .HasColumnName("name")
            .HasMaxLength(120);

        builder.Property(scale => scale.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(120);

        builder.Property(scale => scale.ScaleType)
            .HasColumnName("scale_type")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(scale => scale.MinValue)
            .HasColumnName("min_value")
            .HasColumnType("numeric(18,2)");

        builder.Property(scale => scale.MaxValue)
            .HasColumnName("max_value")
            .HasColumnType("numeric(18,2)");

        builder.Property(scale => scale.Decimals)
            .HasColumnName("decimals");

        builder.Property(scale => scale.IsActive)
            .HasColumnName("is_active");

        builder.Property(scale => scale.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(scale => scale.CreatedUtc).HasColumnName("created_utc");
        builder.Property(scale => scale.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(scale => scale.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_competency_rating_scales__public_id");

        builder.HasIndex(scale => new { scale.TenantId, scale.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_competency_rating_scales__tenant_code");

        // At most one active scale per tenant (single source of truth for expected/achieved scoring).
        builder.HasIndex(scale => scale.TenantId)
            .IsUnique()
            .HasFilter("is_active = true")
            .HasDatabaseName("uq_competency_rating_scales__tenant_active");

        builder.HasMany(scale => scale.Levels)
            .WithOne(level => level.CompetencyRatingScale)
            .HasForeignKey(level => level.CompetencyRatingScaleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_competency_rating_scale_levels__scale");

        builder.Navigation(scale => scale.Levels).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CompetencyRatingScaleLevelConfiguration : IEntityTypeConfiguration<CompetencyRatingScaleLevel>
{
    public void Configure(EntityTypeBuilder<CompetencyRatingScaleLevel> builder)
    {
        builder.ToTable("competency_rating_scale_levels");

        builder.HasKey(level => level.Id)
            .HasName("pk_competency_rating_scale_levels");

        builder.Property(level => level.Id).HasColumnName("id");
        builder.Property(level => level.PublicId).HasColumnName("public_id");
        builder.Property(level => level.TenantId).HasColumnName("tenant_id");
        builder.Property(level => level.CompetencyRatingScaleId).HasColumnName("competency_rating_scale_id");

        builder.Property(level => level.Code)
            .HasColumnName("code")
            .HasMaxLength(20);

        builder.Property(level => level.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(20);

        builder.Property(level => level.Label)
            .HasColumnName("label")
            .HasMaxLength(120);

        builder.Property(level => level.Value)
            .HasColumnName("value")
            .HasColumnType("numeric(18,2)");

        builder.Property(level => level.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(level => level.CreatedUtc).HasColumnName("created_utc");
        builder.Property(level => level.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(level => new { level.TenantId, level.CompetencyRatingScaleId, level.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_competency_rating_scale_levels__tenant_scale_code");

        builder.HasIndex(level => new { level.TenantId, level.CompetencyRatingScaleId, level.Value })
            .IsUnique()
            .HasDatabaseName("uq_competency_rating_scale_levels__tenant_scale_value");

        builder.HasIndex(level => new { level.TenantId, level.CompetencyRatingScaleId, level.SortOrder })
            .HasDatabaseName("ix_competency_rating_scale_levels__tenant_scale_sort");
    }
}
