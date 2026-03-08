using System.Reflection;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Domain.Auditing;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.OrgUnits;
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

    public DbSet<CostCenter> CostCenters => Set<CostCenter>();

    public DbSet<LegalRepresentative> LegalRepresentatives => Set<LegalRepresentative>();

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
