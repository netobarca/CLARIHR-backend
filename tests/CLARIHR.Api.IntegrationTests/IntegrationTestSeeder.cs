using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auditing;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.EducationCatalogs;
using CLARIHR.Domain.GeneralCatalogs;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.Preferences;
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

        var companyPreferenceA = CompanyPreference.Create("USD", "UTC");
        companyPreferenceA.SetTenantId(tenantA);
        dbContext.CompanyPreferences.Add(companyPreferenceA);

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
        SeedGeneralCatalogItems(dbContext, tenantA);
        SeedPersonnelEducationCatalogItems(dbContext, tenantA);
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
            tenantBAuditLog.PublicId);
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
            "OTHER",
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

    private static void SeedGeneralCatalogItems(ApplicationDbContext dbContext, Guid tenantId)
    {
        var companyCountry = GetSeedCompanyCountry(dbContext, tenantId);

        var languages = new (string Code, string Name, int SortOrder)[]
        {
            ("ENGLISH", "English", 10),
            ("SPANISH", "Spanish", 20)
        };

        var languageLevels = new (string Code, string Name, int SortOrder)[]
        {
            ("ADVANCED", "Advanced", 10),
            ("INTERMEDIATE", "Intermediate", 20),
            ("BASIC", "Basic", 30)
        };

        var trainingTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("COURSE", "Course", 10),
            ("WORKSHOP", "Workshop", 20),
            ("CERTIFICATION", "Certification", 30)
        };

        var durationUnits = new (string Code, string Name, int SortOrder)[]
        {
            ("HOUR", "Hour", 10),
            ("DAY", "Day", 20)
        };

        var referenceTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("PERSONAL", "Personal", 10),
            ("PROFESSIONAL", "Professional", 20)
        };

        var currencies = new (string Code, string Name, int SortOrder)[]
        {
            ("USD", "US Dollar", 10)
        };

        var assignmentTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("LEY_SALARIOS", "Ley de Salarios", 10),
            ("CONTRATO", "Contrato", 20),
            ("INDEFINIDO", "Tiempo indefinido", 30),
            ("PLAZO_FIJO", "Plazo fijo", 40),
            ("INTERINO", "Interinato", 50),
            ("POR_OBRA", "Por obra o servicio", 60),
            ("AD_HONOREM", "Ad honorem", 70),
            ("SERVICIOS_PROFESIONALES", "Servicios profesionales", 80),
            ("RECARGO_FUNCIONES", "Recargo de funciones", 90)
        };

        var substitutionTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("VACACIONES", "Vacaciones", 10),
            ("INCAPACIDAD", "Incapacidad", 20),
            ("PERMISO", "Permiso", 30),
            ("MISION_OFICIAL", "Misión oficial", 40),
            ("LICENCIA", "Licencia", 50),
            ("OTRO", "Otro", 60)
        };

        var employmentStatuses = new (string Code, string Name, int SortOrder)[]
        {
            ("ACTIVO", "Activo", 10),
            ("SUSPENDIDO", "Suspendido", 20),
            ("LICENCIA", "Licencia", 30),
            ("INCAPACIDAD", "Incapacidad", 40),
            ("RETIRADO", "Retirado", 50)
        };

        foreach (var item in languages)
        {
            if (!dbContext.LanguageCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == companyCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = LanguageCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
                dbContext.LanguageCatalogItems.Add(entity);
            }
        }

        foreach (var item in languageLevels)
        {
            if (!dbContext.LanguageLevelCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == companyCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = LanguageLevelCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
                dbContext.LanguageLevelCatalogItems.Add(entity);
            }
        }

        foreach (var item in trainingTypes)
        {
            if (!dbContext.TrainingTypeCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == companyCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = TrainingTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
                dbContext.TrainingTypeCatalogItems.Add(entity);
            }
        }

        foreach (var item in durationUnits)
        {
            if (!dbContext.DurationUnitCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == companyCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = DurationUnitCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
                dbContext.DurationUnitCatalogItems.Add(entity);
            }
        }

        foreach (var item in referenceTypes)
        {
            if (!dbContext.ReferenceTypeCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == companyCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = ReferenceTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
                dbContext.ReferenceTypeCatalogItems.Add(entity);
            }
        }

        foreach (var item in currencies)
        {
            if (!dbContext.CurrencyCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == companyCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = CurrencyCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
                dbContext.CurrencyCatalogItems.Add(entity);
            }
        }

        foreach (var item in assignmentTypes)
        {
            if (!dbContext.AssignmentTypeCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == companyCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = AssignmentTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
                dbContext.AssignmentTypeCatalogItems.Add(entity);
            }
        }

        foreach (var item in substitutionTypes)
        {
            if (!dbContext.SubstitutionTypeCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == companyCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = SubstitutionTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
                dbContext.SubstitutionTypeCatalogItems.Add(entity);
            }
        }

        foreach (var item in employmentStatuses)
        {
            if (!dbContext.EmploymentStatusCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == companyCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = EmploymentStatusCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
                dbContext.EmploymentStatusCatalogItems.Add(entity);
            }
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
            if (!dbContext.EducationStatusCatalogItems.Any(entity =>
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = EducationStatusCatalogItem.Create(item.Code, item.Name, item.SortOrder);
                dbContext.EducationStatusCatalogItems.Add(entity);
            }
        }

        foreach (var item in studyTypes)
        {
            if (!dbContext.EducationStudyTypeCatalogItems.Any(entity =>
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = EducationStudyTypeCatalogItem.Create(item.Code, item.Name, item.SortOrder);
                dbContext.EducationStudyTypeCatalogItems.Add(entity);
            }
        }

        foreach (var item in shifts)
        {
            if (!dbContext.EducationShiftCatalogItems.Any(entity =>
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = EducationShiftCatalogItem.Create(item.Code, item.Name, item.SortOrder);
                dbContext.EducationShiftCatalogItems.Add(entity);
            }
        }

        foreach (var item in modalities)
        {
            if (!dbContext.EducationModalityCatalogItems.Any(entity =>
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = EducationModalityCatalogItem.Create(item.Code, item.Name, item.SortOrder);
                dbContext.EducationModalityCatalogItems.Add(entity);
            }
        }

        // Careers are COUNTRY-scoped + enriched since RF-009/DP-06: they need the company country and a
        // study-type FK (the HasData UNIVERSITARIA row, falling back to any existing study type).
        var careerCountry = GetSeedCompanyCountry(dbContext, tenantId);
        var careerStudyTypeId = dbContext.EducationStudyTypeCatalogItems
            .Where(entity => entity.NormalizedCode == "UNIVERSITARIA")
            .Select(entity => entity.Id)
            .FirstOrDefault();
        if (careerStudyTypeId == 0)
        {
            careerStudyTypeId = dbContext.EducationStudyTypeCatalogItems
                .Select(entity => entity.Id)
                .First();
        }

        foreach (var item in careers)
        {
            if (!dbContext.EducationCareerCatalogItems.Any(entity =>
                    entity.CountryCatalogItemId == careerCountry.CountryCatalogItemId &&
                    entity.NormalizedCode == item.Code.ToUpperInvariant()))
            {
                var entity = EducationCareerCatalogItem.Create(
                    careerCountry.CountryCatalogItemId,
                    careerCountry.CountryCode,
                    item.Code,
                    item.Name,
                    true,
                    item.SortOrder,
                    abbreviation: null,
                    increment: 0m,
                    isRecognized: true,
                    educationStudyTypeCatalogItemId: careerStudyTypeId);
                dbContext.EducationCareerCatalogItems.Add(entity);
            }
        }
    }

    private static SeedCompanyCountry GetSeedCompanyCountry(ApplicationDbContext dbContext, Guid tenantId)
    {
        var country = dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == tenantId)
            .Select(company => new SeedCompanyCountry(company.CountryCatalogItemId, company.CountryCode))
            .SingleOrDefault();

        return country ?? throw new InvalidOperationException($"Company country could not be resolved for tenant {tenantId}.");
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
    Guid OtherTenantAuditLogId);

internal sealed record SeedCompanyCountry(long CountryCatalogItemId, string CountryCode);
