using System.Reflection;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Domain.Auditing;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.SalaryTabulator;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider)
    : DbContext(options), IApplicationDbContext
{
    private Guid? CurrentTenantId => tenantContext.TenantId;

    private bool HasTenantScope => CurrentTenantId.HasValue;

    private Guid CurrentTenantIdOrDefault => CurrentTenantId ?? Guid.Empty;

    public DbSet<User> AuthUsers => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanySubscription> CompanySubscriptions => Set<CompanySubscription>();

    public DbSet<UserCompanyMembership> UserCompanyMemberships => Set<UserCompanyMembership>();

    public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();

    public DbSet<InvitationToken> InvitationTokens => Set<InvitationToken>();

    public DbSet<IamUser> IamUsers => Set<IamUser>();

    public DbSet<IamRole> IamRoles => Set<IamRole>();

    public DbSet<IamPermission> IamPermissions => Set<IamPermission>();

    public DbSet<IamUserRoleAssignment> IamUserRoleAssignments => Set<IamUserRoleAssignment>();

    public DbSet<IamRolePermissionAssignment> IamRolePermissionAssignments => Set<IamRolePermissionAssignment>();

    public DbSet<RbacResource> RbacResources => Set<RbacResource>();

    public DbSet<RbacPermissionAuditLog> RbacPermissionAuditLogs => Set<RbacPermissionAuditLog>();

    public DbSet<FieldCatalogEntry> FieldCatalogEntries => Set<FieldCatalogEntry>();

    public DbSet<RoleFieldPermission> RoleFieldPermissions => Set<RoleFieldPermission>();

    public DbSet<FieldPermissionAuditLog> FieldPermissionAuditLogs => Set<FieldPermissionAuditLog>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<LocationHierarchyConfig> LocationHierarchyConfigs => Set<LocationHierarchyConfig>();

    public DbSet<LocationLevel> LocationLevels => Set<LocationLevel>();

    public DbSet<LocationGroup> LocationGroups => Set<LocationGroup>();

    public DbSet<WorkCenterType> WorkCenterTypes => Set<WorkCenterType>();

    public DbSet<WorkCenter> WorkCenters => Set<WorkCenter>();

    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();

    public DbSet<CompanyTypeCatalogItem> CompanyTypeCatalogItems => Set<CompanyTypeCatalogItem>();

    public DbSet<OrgUnitTypeCatalogItem> OrgUnitTypeCatalogItems => Set<OrgUnitTypeCatalogItem>();

    public DbSet<FunctionalAreaCatalogItem> FunctionalAreaCatalogItems => Set<FunctionalAreaCatalogItem>();

    public DbSet<JobProfile> JobProfiles => Set<JobProfile>();

    public DbSet<JobCatalogItem> JobCatalogItems => Set<JobCatalogItem>();

    public DbSet<JobProfileRequirement> JobProfileRequirements => Set<JobProfileRequirement>();

    public DbSet<JobProfileFunction> JobProfileFunctions => Set<JobProfileFunction>();

    public DbSet<JobProfileRelation> JobProfileRelations => Set<JobProfileRelation>();

    public DbSet<JobProfileCompetency> JobProfileCompetencies => Set<JobProfileCompetency>();

    public DbSet<JobProfileTraining> JobProfileTrainings => Set<JobProfileTraining>();

    public DbSet<JobProfileCompensation> JobProfileCompensations => Set<JobProfileCompensation>();

    public DbSet<JobProfileBenefit> JobProfileBenefits => Set<JobProfileBenefit>();

    public DbSet<JobProfileWorkingCondition> JobProfileWorkingConditions => Set<JobProfileWorkingCondition>();

    public DbSet<JobProfileDependentPosition> JobProfileDependentPositions => Set<JobProfileDependentPosition>();

    public DbSet<PositionSlot> PositionSlots => Set<PositionSlot>();

    public DbSet<PositionDescriptionCatalogItem> PositionDescriptionCatalogItems => Set<PositionDescriptionCatalogItem>();

    public DbSet<PositionCategoryClassification> PositionCategoryClassifications => Set<PositionCategoryClassification>();

    public DbSet<PositionCategory> PositionCategories => Set<PositionCategory>();

    public DbSet<CostCenter> CostCenters => Set<CostCenter>();

    public DbSet<LegalRepresentative> LegalRepresentatives => Set<LegalRepresentative>();

    public DbSet<LegalRepresentativeDocumentTypeCatalogItem> LegalRepresentativeDocumentTypeCatalogItems => Set<LegalRepresentativeDocumentTypeCatalogItem>();

    public DbSet<LegalRepresentativePositionTitleCatalogItem> LegalRepresentativePositionTitleCatalogItems => Set<LegalRepresentativePositionTitleCatalogItem>();

    public DbSet<LegalRepresentativeRepresentationTypeCatalogItem> LegalRepresentativeRepresentationTypeCatalogItems => Set<LegalRepresentativeRepresentationTypeCatalogItem>();

    public DbSet<PersonnelFile> PersonnelFiles => Set<PersonnelFile>();

    public DbSet<PersonnelFileIdentification> PersonnelFileIdentifications => Set<PersonnelFileIdentification>();

    public DbSet<PersonnelFileAddress> PersonnelFileAddresses => Set<PersonnelFileAddress>();

    public DbSet<PersonnelFileEmergencyContact> PersonnelFileEmergencyContacts => Set<PersonnelFileEmergencyContact>();

    public DbSet<PersonnelFileFamilyMember> PersonnelFileFamilyMembers => Set<PersonnelFileFamilyMember>();

    public DbSet<PersonnelFileHobby> PersonnelFileHobbies => Set<PersonnelFileHobby>();

    public DbSet<PersonnelFileEmployeeRelation> PersonnelFileEmployeeRelations => Set<PersonnelFileEmployeeRelation>();

    public DbSet<PersonnelFileBankAccount> PersonnelFileBankAccounts => Set<PersonnelFileBankAccount>();

    public DbSet<PersonnelFileAssociation> PersonnelFileAssociations => Set<PersonnelFileAssociation>();

    public DbSet<PersonnelFileEducation> PersonnelFileEducations => Set<PersonnelFileEducation>();

    public DbSet<PersonnelFileLanguage> PersonnelFileLanguages => Set<PersonnelFileLanguage>();

    public DbSet<PersonnelFileTraining> PersonnelFileTrainings => Set<PersonnelFileTraining>();

    public DbSet<PersonnelFilePreviousEmployment> PersonnelFilePreviousEmployments => Set<PersonnelFilePreviousEmployment>();

    public DbSet<PersonnelFileReference> PersonnelFileReferences => Set<PersonnelFileReference>();

    public DbSet<PersonnelFileDocument> PersonnelFileDocuments => Set<PersonnelFileDocument>();

    public DbSet<PersonnelFileCustomFieldDefinition> PersonnelFileCustomFieldDefinitions => Set<PersonnelFileCustomFieldDefinition>();

    public DbSet<PersonnelFileObservation> PersonnelFileObservations => Set<PersonnelFileObservation>();

    public DbSet<PersonnelCatalogItem> PersonnelCatalogItems => Set<PersonnelCatalogItem>();

    public DbSet<PersonnelFileEmployeeProfile> PersonnelFileEmployeeProfiles => Set<PersonnelFileEmployeeProfile>();

    public DbSet<PersonnelFileEmploymentAssignment> PersonnelFileEmploymentAssignments => Set<PersonnelFileEmploymentAssignment>();

    public DbSet<PersonnelFileContractHistory> PersonnelFileContractHistories => Set<PersonnelFileContractHistory>();

    public DbSet<PersonnelFileSalaryItem> PersonnelFileSalaryItems => Set<PersonnelFileSalaryItem>();

    public DbSet<PersonnelFileAdditionalBenefit> PersonnelFileAdditionalBenefits => Set<PersonnelFileAdditionalBenefit>();

    public DbSet<PersonnelFilePaymentMethod> PersonnelFilePaymentMethods => Set<PersonnelFilePaymentMethod>();

    public DbSet<PersonnelFileAuthorizationSubstitution> PersonnelFileAuthorizationSubstitutions => Set<PersonnelFileAuthorizationSubstitution>();

    public DbSet<PersonnelFilePersonnelAction> PersonnelFilePersonnelActions => Set<PersonnelFilePersonnelAction>();

    public DbSet<PersonnelFilePayrollTransaction> PersonnelFilePayrollTransactions => Set<PersonnelFilePayrollTransaction>();

    public DbSet<PersonnelFileAssetAccess> PersonnelFileAssetAccesses => Set<PersonnelFileAssetAccess>();

    public DbSet<PersonnelFileInsurance> PersonnelFileInsurances => Set<PersonnelFileInsurance>();

    public DbSet<PersonnelFileInsuranceBeneficiary> PersonnelFileInsuranceBeneficiaries => Set<PersonnelFileInsuranceBeneficiary>();

    public DbSet<PersonnelFileMedicalClaim> PersonnelFileMedicalClaims => Set<PersonnelFileMedicalClaim>();

    public DbSet<PersonnelFilePerformanceEvaluation> PersonnelFilePerformanceEvaluations => Set<PersonnelFilePerformanceEvaluation>();

    public DbSet<PersonnelFilePositionCompetencyResult> PersonnelFilePositionCompetencyResults => Set<PersonnelFilePositionCompetencyResult>();

    public DbSet<PersonnelFileSelectionContest> PersonnelFileSelectionContests => Set<PersonnelFileSelectionContest>();

    public DbSet<PersonnelFileCurricularCompetency> PersonnelFileCurricularCompetencies => Set<PersonnelFileCurricularCompetency>();

    public DbSet<OccupationalPyramidLevel> OccupationalPyramidLevels => Set<OccupationalPyramidLevel>();

    public DbSet<CompetencyConduct> CompetencyConducts => Set<CompetencyConduct>();

    public DbSet<CompetencyConductBehavior> CompetencyConductBehaviors => Set<CompetencyConductBehavior>();

    public DbSet<JobProfileCompetencyExpectation> JobProfileCompetencyExpectations => Set<JobProfileCompetencyExpectation>();

    public DbSet<JobProfileCompetencyExpectationConduct> JobProfileCompetencyExpectationConducts => Set<JobProfileCompetencyExpectationConduct>();

    public DbSet<SalaryTabulatorLine> SalaryTabulatorLines => Set<SalaryTabulatorLine>();

    public DbSet<SalaryTabulatorChangeRequest> SalaryTabulatorChangeRequests => Set<SalaryTabulatorChangeRequest>();

    public DbSet<SalaryTabulatorChangeRequestItem> SalaryTabulatorChangeRequestItems => Set<SalaryTabulatorChangeRequestItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        ApplyTenantFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditMetadata();

        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditMetadata()
    {
        var utcNow = dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.MarkCreated(utcNow);
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.MarkModified(utcNow);
            }
        }

        foreach (var entry in ChangeTracker.Entries<TenantEntity>().Where(static entry => entry.State == EntityState.Added))
        {
            if (entry.Entity.TenantId != Guid.Empty)
            {
                continue;
            }

            if (!CurrentTenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant-scoped writes require a tenant context.");
            }

            entry.Entity.SetTenantId(CurrentTenantId.Value);
        }
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var setFilterMethod = typeof(ApplicationDbContext)
            .GetMethod(nameof(SetTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Tenant filter method could not be found.");

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            _ = setFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScopedEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => !HasTenantScope || entity.TenantId == CurrentTenantIdOrDefault);
    }
}
