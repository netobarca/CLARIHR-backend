using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auditing;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace CLARIHR.Api.IntegrationTests;

internal static class IntegrationTestSeeder
{
    public static async Task<IntegrationTestScenario> SeedAsync(ApplicationDbContext dbContext)
    {
        var actorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var countryCatalogItemId = await dbContext.CountryCatalogItems
            .Where(item => item.NormalizedCode == "SV")
            .Select(item => item.Id)
            .SingleAsync();
        var freePlan = await dbContext.CommercialPlans
            .Include(plan => plan.Versions)
            .SingleAsync(plan => plan.NormalizedCode == ProvisioningConstants.FreePlanCode);

        var companyA = Company.Create("Acme One", "acme-one", actorUserId, "SV", countryCatalogItemId);
        var companyB = Company.Create("Acme Two", "acme-two", actorUserId, "SV", countryCatalogItemId);
        dbContext.Companies.AddRange(companyA, companyB);
        await dbContext.SaveChangesAsync();

        var tenantA = companyA.PublicId;
        var tenantB = companyB.PublicId;

        dbContext.LegalRepresentatives.AddRange(
            CreateLegalRepresentative(
                tenantA,
                "Security",
                "Representative",
                "security.representative@acme-one.test",
                "A-HU016-0001"),
            CreateLegalRepresentative(
                tenantB,
                "Audit",
                "Representative",
                "audit.representative@acme-two.test",
                "B-HU016-0001"));
        await dbContext.SaveChangesAsync();

        dbContext.CompanySubscriptions.AddRange(
            CompanySubscription.Activate(companyA.Id, freePlan, DateTime.UtcNow.Date),
            CompanySubscription.Activate(companyB.Id, freePlan, DateTime.UtcNow.Date));
        await dbContext.SaveChangesAsync();

        SeedDefaultLocations(dbContext, tenantA);
        SeedDefaultLocations(dbContext, tenantB);
        SeedPersonnelCatalogItems(dbContext, tenantA);
        SeedPersonnelCatalogItems(dbContext, tenantB);
        SeedPersonnelEducationCatalogItems(dbContext, tenantA);
        SeedPersonnelEducationCatalogItems(dbContext, tenantB);
        await dbContext.SaveChangesAsync();

        var actorRole = IamRole.Create("Security Operator", "Can read and update company users plus audit logs.");
        actorRole.SetTenantId(tenantA);

        var securityAdminRole = IamRole.Create("Security Admin", "Can manage RBAC security.");
        securityAdminRole.SetTenantId(tenantA);

        var targetRole = IamRole.Create("Employee", "Regular company employee.");
        targetRole.SetTenantId(tenantA);

        var tenantBRole = IamRole.Create("Auditor B", "Tenant B role.");
        tenantBRole.SetTenantId(tenantB);

        dbContext.IamRoles.AddRange(actorRole, securityAdminRole, targetRole, tenantBRole);
        await dbContext.SaveChangesAsync();

        var actorPermissions = new[]
        {
            CreatePermission(tenantA, RbacPermissionScreen.Users, RbacPermissionAction.Access),
            CreatePermission(tenantA, RbacPermissionScreen.Users, RbacPermissionAction.Read),
            CreatePermission(tenantA, RbacPermissionScreen.Users, RbacPermissionAction.Update),
            CreatePermission(tenantA, RbacPermissionScreen.AuditLogs, RbacPermissionAction.Access),
            CreatePermission(tenantA, RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read)
        };

        var securityAdminPermissions = new[]
        {
            IamPermission.CreateScreenAction(
                IdentityPermissionCodes.ManageAdministration,
                "Manage Administration",
                "Can manage tenant administration flows.",
                "RBAC",
                "Administration",
                "Manage"),
            CreatePermission(tenantA, RbacPermissionScreen.Roles, RbacPermissionAction.Access),
            CreatePermission(tenantA, RbacPermissionScreen.Roles, RbacPermissionAction.Update),
            CreatePermission(tenantA, RbacPermissionScreen.Permissions, RbacPermissionAction.Access),
            CreatePermission(tenantA, RbacPermissionScreen.Permissions, RbacPermissionAction.Update)
        };

        foreach (var permission in securityAdminPermissions)
        {
            permission.SetTenantId(tenantA);
        }

        dbContext.IamPermissions.AddRange(actorPermissions);
        dbContext.IamPermissions.AddRange(securityAdminPermissions);
        await dbContext.SaveChangesAsync();

        actorRole.SyncPermissions(actorPermissions);
        StampTenant(actorRole.PermissionAssignments, tenantA);
        securityAdminRole.SyncPermissions(securityAdminPermissions);
        StampTenant(securityAdminRole.PermissionAssignments, tenantA);
        await dbContext.SaveChangesAsync();

        var actorIamUser = IamUser.CreateLinked(actorUserId, "Security", "Operator", "security.operator@acme-one.test", isActive: true);
        actorIamUser.SetTenantId(tenantA);
        actorIamUser.SyncRoles([actorRole]);
        StampTenant(actorIamUser.RoleAssignments, tenantA);
        dbContext.IamUsers.Add(actorIamUser);

        var securityAdminUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var securityAdminIamUser = IamUser.CreateLinked(
            securityAdminUserId,
            "Tenant",
            "SecurityAdmin",
            "tenant.security.admin@acme-one.test",
            isActive: true);
        securityAdminIamUser.SetTenantId(tenantA);
        securityAdminIamUser.SyncRoles([securityAdminRole]);
        StampTenant(securityAdminIamUser.RoleAssignments, tenantA);
        dbContext.IamUsers.Add(securityAdminIamUser);

        var actorAuthUser = User.RegisterLocal(
            "Security",
            "Operator",
            "security.operator@acme-one.test",
            "hashed-password",
            country: "SV",
            source: "integration-tests");
        SetPublicId(actorAuthUser, actorUserId);
        dbContext.AuthUsers.Add(actorAuthUser);

        var securityAdminAuthUser = User.RegisterLocal(
            "Tenant",
            "SecurityAdmin",
            "tenant.security.admin@acme-one.test",
            "hashed-password",
            country: "SV",
            source: "integration-tests");
        SetPublicId(securityAdminAuthUser, securityAdminUserId);
        dbContext.AuthUsers.Add(securityAdminAuthUser);

        var targetAuthUser = User.RegisterLocal(
            "Target",
            "User",
            "target.user@acme-one.test",
            "hashed-password",
            country: "SV",
            source: "integration-tests");
        dbContext.AuthUsers.Add(targetAuthUser);
        await dbContext.SaveChangesAsync();

        dbContext.UserCompanyMemberships.Add(UserCompanyMembership.Create(
            actorAuthUser.Id,
            companyA.Id,
            actorRole.Id,
            isPrimary: true));

        dbContext.UserCompanyMemberships.Add(UserCompanyMembership.Create(
            securityAdminAuthUser.Id,
            companyA.Id,
            securityAdminRole.Id,
            isPrimary: false));

        dbContext.UserCompanyMemberships.Add(UserCompanyMembership.Create(
            targetAuthUser.Id,
            companyA.Id,
            targetRole.Id,
            isPrimary: false));

        var hiddenEmail = RoleFieldPermission.Create(
            actorRole.Id,
            CompanyUserFieldKeys.Email,
            isVisible: false,
            isEditable: false,
            isRequired: false,
            isMasked: false,
            updatedByUserId: actorUserId,
            updatedAtUtc: DateTime.UtcNow);
        hiddenEmail.SetTenantId(tenantA);

        var firstNameReadOnly = RoleFieldPermission.Create(
            actorRole.Id,
            CompanyUserFieldKeys.FirstName,
            isVisible: true,
            isEditable: false,
            isRequired: false,
            isMasked: false,
            updatedByUserId: actorUserId,
            updatedAtUtc: DateTime.UtcNow);
        firstNameReadOnly.SetTenantId(tenantA);

        dbContext.RoleFieldPermissions.AddRange(hiddenEmail, firstNameReadOnly);

        var tenantBAuditLog = AuditLog.Create(
            actorUserId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            actorEmail: "auditor@acme-two.test",
            eventType: "ROLE_UPDATED",
            entityType: "Role",
            entityId: tenantBRole.PublicId,
            entityKey: "RBAC_ROLES",
            action: "Update",
            summary: "Updated role in tenant B.",
            beforeJson: "{\"name\":\"Old Role\"}",
            afterJson: "{\"name\":\"New Role\"}",
            diffJson: "{\"name\":{\"before\":\"Old Role\",\"after\":\"New Role\"}}",
            ipAddress: "127.0.0.1",
            userAgent: "integration-tests");
        tenantBAuditLog.SetTenantId(tenantB);
        dbContext.AuditLogs.Add(tenantBAuditLog);

        var tenantAAuditLog = AuditLog.Create(
            actorUserId,
            "security.operator@acme-one.test",
            eventType: "USER_UPDATED",
            entityType: "User",
            entityId: targetAuthUser.PublicId,
            entityKey: targetAuthUser.Email,
            action: "Update",
            summary: "Updated user in tenant A.",
            beforeJson: "{\"firstName\":\"Target\"}",
            afterJson: "{\"firstName\":\"Target\"}",
            diffJson: "{}",
            ipAddress: "127.0.0.1",
            userAgent: "integration-tests");
        tenantAAuditLog.SetTenantId(tenantA);
        dbContext.AuditLogs.Add(tenantAAuditLog);

        var tenantARbacAuditLogOlder = RbacPermissionAuditLog.Create(
            actorRole.PublicId,
            "RBAC_USERS",
            actorUserId,
            RbacPermissionAuditChangeType.Upsert,
            """{"hasAccess":false,"canRead":false,"canCreate":false,"canUpdate":false,"canDelete":false}""",
            """{"hasAccess":true,"canRead":true,"canCreate":false,"canUpdate":true,"canDelete":false}""",
            DateTime.Parse("2026-02-28T10:00:00Z").ToUniversalTime());
        tenantARbacAuditLogOlder.SetTenantId(tenantA);
        dbContext.RbacPermissionAuditLogs.Add(tenantARbacAuditLogOlder);

        var tenantARbacAuditLogLatest = RbacPermissionAuditLog.Create(
            actorRole.PublicId,
            "RBAC_USERS",
            actorUserId,
            RbacPermissionAuditChangeType.Disable,
            """{"hasAccess":true,"canRead":true,"canCreate":false,"canUpdate":true,"canDelete":false}""",
            """{"hasAccess":true,"canRead":true,"canCreate":false,"canUpdate":false,"canDelete":false}""",
            DateTime.Parse("2026-02-28T11:00:00Z").ToUniversalTime());
        tenantARbacAuditLogLatest.SetTenantId(tenantA);
        dbContext.RbacPermissionAuditLogs.Add(tenantARbacAuditLogLatest);

        var tenantBRbacAuditLog = RbacPermissionAuditLog.Create(
            tenantBRole.PublicId,
            "RBAC_ROLES",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RbacPermissionAuditChangeType.Upsert,
            """{"hasAccess":false,"canRead":false,"canCreate":false,"canUpdate":false,"canDelete":false}""",
            """{"hasAccess":true,"canRead":true,"canCreate":true,"canUpdate":false,"canDelete":false}""",
            DateTime.Parse("2026-02-28T12:00:00Z").ToUniversalTime());
        tenantBRbacAuditLog.SetTenantId(tenantB);
        dbContext.RbacPermissionAuditLogs.Add(tenantBRbacAuditLog);

        await dbContext.SaveChangesAsync();

        return new IntegrationTestScenario(
            tenantA,
            tenantB,
            actorUserId,
            securityAdminUserId,
            actorRole.PublicId,
            tenantBRole.PublicId,
            actorPermissions[0].PublicId,
            targetRole.PublicId,
            targetAuthUser.PublicId,
            tenantAAuditLog.PublicId,
            tenantBAuditLog.PublicId,
            tenantARbacAuditLogLatest.Id,
            tenantBRbacAuditLog.Id);
    }

    private static IamPermission CreatePermission(Guid tenantId, RbacPermissionScreen screen, RbacPermissionAction action)
    {
        var permission = PermissionMatrixCatalog.CreatePermission(screen, action);
        permission.SetTenantId(tenantId);
        return permission;
    }

    private static LegalRepresentative CreateLegalRepresentative(
        Guid tenantId,
        string firstName,
        string lastName,
        string email,
        string documentNumber)
    {
        var legalRepresentative = LegalRepresentative.Create(
            firstName,
            lastName,
            LegalRepresentativeDocumentType.Other,
            documentNumber,
            "Representante Legal",
            LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            "Integration test seed representative.",
            "Integration test seed instrument.",
            appointmentDateUtc: DateTime.UtcNow.Date,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            email,
            phone: null,
            isPrimary: true);
        legalRepresentative.SetTenantId(tenantId);
        return legalRepresentative;
    }

    private static void StampTenant(IEnumerable<TenantEntity> entities, Guid tenantId)
    {
        foreach (var entity in entities)
        {
            entity.SetTenantId(tenantId);
        }
    }

    private static void SetPublicId(object entity, Guid publicId)
    {
        entity.GetType()
            .GetProperty("PublicId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [publicId]);
    }

    private static void SeedDefaultLocations(ApplicationDbContext dbContext, Guid tenantId)
    {
        var config = LocationHierarchyConfig.Create(
            isMultiLevel: true,
            "GENERAL",
            "General");
        config.SetTenantId(tenantId);

        var countryLevel = LocationLevel.Create(
            levelOrder: 1,
            "Pais",
            isActive: true,
            isRequired: true,
            allowsWorkCenters: false);
        countryLevel.SetTenantId(tenantId);

        var departmentLevel = LocationLevel.Create(
            levelOrder: 2,
            "Departamento",
            isActive: true,
            isRequired: false,
            allowsWorkCenters: false);
        departmentLevel.SetTenantId(tenantId);

        var municipalityLevel = LocationLevel.Create(
            levelOrder: 3,
            "Municipio",
            isActive: true,
            isRequired: false,
            allowsWorkCenters: true);
        municipalityLevel.SetTenantId(tenantId);

        var countryGroup = LocationGroup.Create(
            levelOrder: 1,
            "SV",
            "El Salvador",
            parentId: null,
            description: "Pais",
            isDefault: false);
        countryGroup.SetTenantId(tenantId);

        var departmentGroup = LocationGroup.Create(
            levelOrder: 2,
            "SS",
            "San Salvador",
            parentId: null,
            description: "Departamento",
            isDefault: false);
        departmentGroup.SetTenantId(tenantId);

        var municipalityGroupA = LocationGroup.Create(
            levelOrder: 3,
            "APOPA",
            "Apopa",
            parentId: null,
            description: "Municipio",
            isDefault: false);
        municipalityGroupA.SetTenantId(tenantId);

        var municipalityGroupB = LocationGroup.Create(
            levelOrder: 3,
            "MEJICANOS",
            "Mejicanos",
            parentId: null,
            description: "Municipio",
            isDefault: false);
        municipalityGroupB.SetTenantId(tenantId);

        dbContext.LocationHierarchyConfigs.Add(config);
        dbContext.LocationLevels.AddRange(countryLevel, departmentLevel, municipalityLevel);
        dbContext.LocationGroups.AddRange(countryGroup, departmentGroup, municipalityGroupA, municipalityGroupB);
        dbContext.SaveChanges();

        departmentGroup.Move(countryGroup.Id);
        municipalityGroupA.Move(departmentGroup.Id);
        municipalityGroupB.Move(departmentGroup.Id);
    }

    private static void SeedPersonnelCatalogItems(ApplicationDbContext dbContext, Guid tenantId)
    {
        var catalogItems = new (string Category, string Code, string Name, int SortOrder)[]
        {
            ("CurriculumLanguage", "ENGLISH", "English", 10),
            ("CurriculumLanguageLevel", "ADVANCED", "Advanced", 10),
            ("CurriculumTrainingType", "COURSE", "Course", 10),
            ("CurriculumDurationUnit", "HOUR", "Hour", 10),
            ("CurriculumReferenceType", "PERSONAL", "Personal", 10),
            ("Country", "SV", "El Salvador", 10),
            ("Currency", "USD", "US Dollar", 10)
        };

        foreach (var item in catalogItems)
        {
            var catalogItem = PersonnelCatalogItem.Create(
                item.Category,
                item.Code,
                item.Name,
                isSystem: true,
                isActive: true,
                item.SortOrder);
            catalogItem.SetTenantId(tenantId);
            dbContext.PersonnelCatalogItems.Add(catalogItem);
        }
    }

    private static void SeedPersonnelEducationCatalogItems(ApplicationDbContext dbContext, Guid tenantId)
    {
        var statuses = new (string Code, string Name, int SortOrder)[]
        {
            ("GRADUATED", "Graduated", 10),
            ("IN_PROGRESS", "In progress", 20)
        };

        var studyTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("BACHELOR", "Bachelor", 10),
            ("MASTER", "Master", 20),
            ("TECHNICAL", "Technical", 30)
        };

        var shifts = new (string Code, string Name, int SortOrder)[]
        {
            ("MORNING", "Morning", 10),
            ("AFTERNOON", "Afternoon", 20)
        };

        var modalities = new (string Code, string Name, int SortOrder)[]
        {
            ("ONSITE", "Onsite", 10),
            ("REMOTE", "Remote", 20)
        };

        var careers = new (string Code, string Name, int SortOrder)[]
        {
            ("SOFTWARE_ENGINEERING", "Ingenieria de Software", 10),
            ("BUSINESS_ADMINISTRATION", "Administracion de Empresas", 20),
            ("PSYCHOLOGY", "Psicologia", 30)
        };

        foreach (var item in statuses)
        {
            var entity = EducationStatusCatalogItem.Create(item.Code, item.Name, item.SortOrder);
            entity.SetTenantId(tenantId);
            dbContext.EducationStatusCatalogItems.Add(entity);
        }

        foreach (var item in studyTypes)
        {
            var entity = EducationStudyTypeCatalogItem.Create(item.Code, item.Name, item.SortOrder);
            entity.SetTenantId(tenantId);
            dbContext.EducationStudyTypeCatalogItems.Add(entity);
        }

        foreach (var item in shifts)
        {
            var entity = EducationShiftCatalogItem.Create(item.Code, item.Name, item.SortOrder);
            entity.SetTenantId(tenantId);
            dbContext.EducationShiftCatalogItems.Add(entity);
        }

        foreach (var item in modalities)
        {
            var entity = EducationModalityCatalogItem.Create(item.Code, item.Name, item.SortOrder);
            entity.SetTenantId(tenantId);
            dbContext.EducationModalityCatalogItems.Add(entity);
        }

        foreach (var item in careers)
        {
            var entity = EducationCareerCatalogItem.Create(item.Code, item.Name, item.SortOrder);
            entity.SetTenantId(tenantId);
            dbContext.EducationCareerCatalogItems.Add(entity);
        }
    }
}

internal sealed record IntegrationTestScenario(
    Guid TenantId,
    Guid OtherTenantId,
    Guid ActorUserId,
    Guid SecurityAdminUserId,
    Guid ActorRoleId,
    Guid OtherTenantRoleId,
    Guid ActorPermissionId,
    Guid TargetRoleId,
    Guid TargetUserId,
    Guid AuditLogId,
    Guid OtherTenantAuditLogId,
    long RbacAuditLogId,
    long OtherTenantRbacAuditLogId);
