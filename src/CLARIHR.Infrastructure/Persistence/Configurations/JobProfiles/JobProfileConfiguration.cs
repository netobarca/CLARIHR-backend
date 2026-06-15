using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.SalaryTabulator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.JobProfiles;

internal sealed class JobProfileConfiguration : IEntityTypeConfiguration<JobProfile>
{
    public void Configure(EntityTypeBuilder<JobProfile> builder)
    {
        builder.ToTable("job_profiles");

        builder.HasKey(profile => profile.Id)
            .HasName("pk_job_profiles");

        builder.Property(profile => profile.Id)
            .HasColumnName("id");

        builder.Property(profile => profile.PublicId)
            .HasColumnName("public_id");

        builder.Property(profile => profile.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(profile => profile.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(profile => profile.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(profile => profile.Title)
            .HasColumnName("title")
            .HasMaxLength(180);

        builder.Property(profile => profile.NormalizedTitle)
            .HasColumnName("normalized_title")
            .HasMaxLength(180);

        builder.Property(profile => profile.Objective)
            .HasColumnName("objective")
            .HasMaxLength(4000);

        builder.Property(profile => profile.OrgUnitId)
            .HasColumnName("org_unit_id")
            .IsRequired();

        builder.Property(profile => profile.ReportsToJobProfileId)
            .HasColumnName("reports_to_job_profile_id");

        builder.Property(profile => profile.PositionCategoryId)
            .HasColumnName("position_category_id");

        builder.Property(profile => profile.StrategicObjectiveCatalogItemId)
            .HasColumnName("strategic_objective_catalog_item_id");

        builder.Property(profile => profile.AssignedWorkEquipmentCatalogItemId)
            .HasColumnName("assigned_work_equipment_catalog_item_id");

        builder.Property(profile => profile.ResponsibilityCatalogItemId)
            .HasColumnName("responsibility_catalog_item_id");

        builder.Property(profile => profile.DecisionScope)
            .HasColumnName("decision_scope")
            .HasMaxLength(4000);

        builder.Property(profile => profile.AssignedResources)
            .HasColumnName("assigned_resources")
            .HasMaxLength(4000);

        builder.Property(profile => profile.Responsibilities)
            .HasColumnName("responsibilities")
            .HasMaxLength(4000);

        builder.Property(profile => profile.MarketSalaryReference)
            .HasColumnName("market_salary_reference")
            .HasMaxLength(4000);

        builder.Property(profile => profile.ValuationNotes)
            .HasColumnName("valuation_notes")
            .HasMaxLength(4000);

        builder.Property(profile => profile.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(profile => profile.Version)
            .HasColumnName("version");

        builder.Property(profile => profile.EffectiveFromUtc)
            .HasColumnName("effective_from_utc");

        builder.Property(profile => profile.EffectiveToUtc)
            .HasColumnName("effective_to_utc");

        builder.Property(profile => profile.IsActive)
            .HasColumnName("is_active");

        builder.Property(profile => profile.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(profile => profile.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(profile => profile.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(profile => profile.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_job_profiles__public_id");

        builder.HasIndex(profile => new { profile.TenantId, profile.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_job_profiles__tenant_code");

        builder.HasIndex(profile => new { profile.TenantId, profile.Status })
            .HasDatabaseName("ix_job_profiles__tenant_status");

        builder.HasIndex(profile => new { profile.TenantId, profile.OrgUnitId })
            .HasDatabaseName("ix_job_profiles__tenant_org_unit");

        builder.HasIndex(profile => new { profile.TenantId, profile.PositionCategoryId })
            .HasDatabaseName("ix_job_profiles__tenant_position_category");

        builder.HasIndex(profile => new { profile.TenantId, profile.NormalizedTitle })
            .HasDatabaseName("ix_job_profiles__tenant_title");

        builder.HasOne<OrgUnit>()
            .WithMany()
            .HasForeignKey(profile => profile.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profiles__org_unit");

        builder.HasOne<JobProfile>()
            .WithMany()
            .HasForeignKey(profile => profile.ReportsToJobProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profiles__reports_to");

        builder.HasOne<PositionCategory>()
            .WithMany()
            .HasForeignKey(profile => profile.PositionCategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profiles__position_category");

        builder.HasOne<PositionDescriptionCatalogItem>()
            .WithMany()
            .HasForeignKey(profile => profile.StrategicObjectiveCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profiles__strategic_objective_catalog_item");

        builder.HasOne<PositionDescriptionCatalogItem>()
            .WithMany()
            .HasForeignKey(profile => profile.AssignedWorkEquipmentCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profiles__assigned_work_equipment_catalog_item");

        builder.HasOne<PositionDescriptionCatalogItem>()
            .WithMany()
            .HasForeignKey(profile => profile.ResponsibilityCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profiles__responsibility_catalog_item");

        builder.HasMany(profile => profile.Requirements)
            .WithOne(requirement => requirement.JobProfile)
            .HasForeignKey(requirement => requirement.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_requirements__job_profile");

        builder.HasMany(profile => profile.Functions)
            .WithOne(function => function.JobProfile)
            .HasForeignKey(function => function.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_functions__job_profile");

        builder.HasMany(profile => profile.Relations)
            .WithOne(relation => relation.JobProfile)
            .HasForeignKey(relation => relation.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_relations__job_profile");

        builder.HasMany(profile => profile.Competencies)
            .WithOne(competency => competency.JobProfile)
            .HasForeignKey(competency => competency.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_competencies__job_profile");

        builder.HasMany(profile => profile.Trainings)
            .WithOne(training => training.JobProfile)
            .HasForeignKey(training => training.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_trainings__job_profile");

        builder.HasMany(profile => profile.Benefits)
            .WithOne(benefit => benefit.JobProfile)
            .HasForeignKey(benefit => benefit.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_benefits__job_profile");

        builder.HasMany(profile => profile.WorkingConditions)
            .WithOne(condition => condition.JobProfile)
            .HasForeignKey(condition => condition.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_working_conditions__job_profile");

        builder.HasMany(profile => profile.DependentPositions)
            .WithOne(position => position.JobProfile)
            .HasForeignKey(position => position.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_dependent_positions__job_profile");

        builder.Navigation(profile => profile.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Functions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Relations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Competencies).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Trainings).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Benefits).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.WorkingConditions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.DependentPositions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class JobCatalogItemConfiguration : IEntityTypeConfiguration<JobCatalogItem>
{
    public void Configure(EntityTypeBuilder<JobCatalogItem> builder)
    {
        builder.ToTable("job_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_catalog_items");

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.PublicId)
            .HasColumnName("public_id");

        builder.Property(item => item.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(item => item.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(item => item.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(120);

        builder.Property(item => item.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(120);

        builder.Property(item => item.IsSystem)
            .HasColumnName("is_system");

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(item => item.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(item => item.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_job_catalog_items__public_id");

        builder.HasIndex(item => new { item.TenantId, item.Category, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_job_catalog_items__tenant_category_code");

        builder.HasIndex(item => new { item.TenantId, item.Category, item.NormalizedName })
            .HasDatabaseName("ix_job_catalog_items__tenant_category_name");

        builder.HasIndex(item => new { item.TenantId, item.Category, item.IsActive })
            .HasDatabaseName("ix_job_catalog_items__tenant_category_active");
    }
}

internal sealed class JobProfileRequirementConfiguration : IEntityTypeConfiguration<JobProfileRequirement>
{
    public void Configure(EntityTypeBuilder<JobProfileRequirement> builder)
    {
        builder.ToTable("job_profile_requirements");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_profile_requirements");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.JobProfileId).HasColumnName("job_profile_id");

        builder.Property(item => item.RequirementType)
            .HasColumnName("requirement_type")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(item => item.RequirementTypeCatalogItemId).HasColumnName("requirement_type_catalog_item_id");

        builder.Property(item => item.CatalogItemId).HasColumnName("catalog_item_id");

        builder.Property(item => item.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => new { item.TenantId, item.JobProfileId, item.SortOrder })
            .HasDatabaseName("ix_job_profile_requirements__tenant_profile_sort");

        builder.HasOne(item => item.CatalogItem)
            .WithMany()
            .HasForeignKey(item => item.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_requirements__catalog_item");

        builder.HasOne<PositionDescriptionCatalogItem>()
            .WithMany()
            .HasForeignKey(item => item.RequirementTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_requirements__requirement_type_catalog_item");
    }
}

internal sealed class JobProfileFunctionConfiguration : IEntityTypeConfiguration<JobProfileFunction>
{
    public void Configure(EntityTypeBuilder<JobProfileFunction> builder)
    {
        builder.ToTable("job_profile_functions");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_profile_functions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.JobProfileId).HasColumnName("job_profile_id");

        builder.Property(item => item.FunctionType)
            .HasColumnName("function_type")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(item => item.FrequencyCatalogItemId).HasColumnName("frequency_catalog_item_id");

        builder.Property(item => item.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => new { item.TenantId, item.JobProfileId, item.SortOrder })
            .HasDatabaseName("ix_job_profile_functions__tenant_profile_sort");

        builder.HasOne<PositionDescriptionCatalogItem>()
            .WithMany()
            .HasForeignKey(item => item.FrequencyCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_functions__frequency_catalog_item");
    }
}

internal sealed class JobProfileRelationConfiguration : IEntityTypeConfiguration<JobProfileRelation>
{
    public void Configure(EntityTypeBuilder<JobProfileRelation> builder)
    {
        builder.ToTable("job_profile_relations");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_profile_relations");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.JobProfileId).HasColumnName("job_profile_id");

        builder.Property(item => item.RelationType)
            .HasColumnName("relation_type")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(item => item.Counterpart)
            .HasColumnName("counterpart")
            .HasMaxLength(500);

        builder.Property(item => item.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.Property(item => item.CatalogItemId).HasColumnName("catalog_item_id");

        builder.HasIndex(item => new { item.TenantId, item.JobProfileId, item.SortOrder })
            .HasDatabaseName("ix_job_profile_relations__tenant_profile_sort");

        builder.HasOne(item => item.CatalogItem)
            .WithMany()
            .HasForeignKey(item => item.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_relations__catalog_item");
    }
}

internal sealed class JobProfileCompetencyConfiguration : IEntityTypeConfiguration<JobProfileCompetency>
{
    public void Configure(EntityTypeBuilder<JobProfileCompetency> builder)
    {
        builder.ToTable("job_profile_competencies");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_profile_competencies");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.JobProfileId).HasColumnName("job_profile_id");
        builder.Property(item => item.CatalogItemId).HasColumnName("catalog_item_id");

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(300);

        builder.Property(item => item.ExpectedLevel)
            .HasColumnName("expected_level")
            .HasMaxLength(150);

        builder.Property(item => item.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => new { item.TenantId, item.JobProfileId, item.SortOrder })
            .HasDatabaseName("ix_job_profile_competencies__tenant_profile_sort");

        builder.HasOne(item => item.CatalogItem)
            .WithMany()
            .HasForeignKey(item => item.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_competencies__catalog_item");
    }
}

internal sealed class JobProfileTrainingConfiguration : IEntityTypeConfiguration<JobProfileTraining>
{
    public void Configure(EntityTypeBuilder<JobProfileTraining> builder)
    {
        builder.ToTable("job_profile_trainings");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_profile_trainings");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.JobProfileId).HasColumnName("job_profile_id");
        builder.Property(item => item.CatalogItemId).HasColumnName("catalog_item_id");

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(300);

        builder.Property(item => item.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => new { item.TenantId, item.JobProfileId, item.SortOrder })
            .HasDatabaseName("ix_job_profile_trainings__tenant_profile_sort");

        builder.HasOne(item => item.CatalogItem)
            .WithMany()
            .HasForeignKey(item => item.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_trainings__catalog_item");
    }
}

internal sealed class JobProfileBenefitConfiguration : IEntityTypeConfiguration<JobProfileBenefit>
{
    public void Configure(EntityTypeBuilder<JobProfileBenefit> builder)
    {
        builder.ToTable("job_profile_benefits");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_profile_benefits");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.JobProfileId).HasColumnName("job_profile_id");
        builder.Property(item => item.CatalogItemId).HasColumnName("catalog_item_id");

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(300);

        builder.Property(item => item.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => new { item.TenantId, item.JobProfileId, item.SortOrder })
            .HasDatabaseName("ix_job_profile_benefits__tenant_profile_sort");

        builder.HasOne(item => item.CatalogItem)
            .WithMany()
            .HasForeignKey(item => item.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_benefits__catalog_item");
    }
}

internal sealed class JobProfileWorkingConditionConfiguration : IEntityTypeConfiguration<JobProfileWorkingCondition>
{
    public void Configure(EntityTypeBuilder<JobProfileWorkingCondition> builder)
    {
        builder.ToTable("job_profile_working_conditions");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_profile_working_conditions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.JobProfileId).HasColumnName("job_profile_id");
        builder.Property(item => item.WorkConditionTypeCatalogItemId).HasColumnName("work_condition_type_catalog_item_id");
        builder.Property(item => item.CatalogItemId).HasColumnName("catalog_item_id");

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(300);

        builder.Property(item => item.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => new { item.TenantId, item.JobProfileId, item.SortOrder })
            .HasDatabaseName("ix_job_profile_working_conditions__tenant_profile_sort");

        builder.HasOne<PositionDescriptionCatalogItem>()
            .WithMany()
            .HasForeignKey(item => item.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_working_conditions__catalog_item");

        builder.HasOne<PositionDescriptionCatalogItem>()
            .WithMany()
            .HasForeignKey(item => item.WorkConditionTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_working_conditions__work_condition_type_catalog_item");
    }
}

internal sealed class JobProfileDependentPositionConfiguration : IEntityTypeConfiguration<JobProfileDependentPosition>
{
    public void Configure(EntityTypeBuilder<JobProfileDependentPosition> builder)
    {
        builder.ToTable("job_profile_dependent_positions");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_profile_dependent_positions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.JobProfileId).HasColumnName("job_profile_id");
        builder.Property(item => item.DependentJobProfileId).HasColumnName("dependent_job_profile_id");

        builder.Property(item => item.Quantity).HasColumnName("quantity");

        builder.Property(item => item.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => new { item.TenantId, item.JobProfileId, item.DependentJobProfileId })
            .HasDatabaseName("ix_job_profile_dependent_positions__tenant_profile_dep");

        builder.HasOne(item => item.DependentJobProfile)
            .WithMany()
            .HasForeignKey(item => item.DependentJobProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_dependent_positions__dependent_profile");
    }
}

internal sealed class JobProfileCompensationConfiguration : IEntityTypeConfiguration<JobProfileCompensation>
{
    public void Configure(EntityTypeBuilder<JobProfileCompensation> builder)
    {
        builder.ToTable("job_profile_compensations");

        builder.HasKey(item => item.Id)
            .HasName("pk_job_profile_compensations");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.JobProfileId).HasColumnName("job_profile_id");
        builder.Property(item => item.SalaryTabulatorLineId).HasColumnName("salary_tabulator_line_id");

        builder.Property(item => item.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_job_profile_compensations__public_id");

        builder.HasIndex(item => item.JobProfileId)
            .IsUnique()
            .HasDatabaseName("uq_job_profile_compensations__job_profile_id");

        builder.HasIndex(item => item.SalaryTabulatorLineId)
            .HasDatabaseName("ix_job_profile_compensations__salary_tabulator_line");

        builder.HasOne<JobProfile>()
            .WithOne()
            .HasForeignKey<JobProfileCompensation>(item => item.JobProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_job_profile_compensations__job_profile");

        builder.HasOne<SalaryTabulatorLine>()
            .WithMany()
            .HasForeignKey(item => item.SalaryTabulatorLineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_job_profile_compensations__salary_tabulator_line");
    }
}
