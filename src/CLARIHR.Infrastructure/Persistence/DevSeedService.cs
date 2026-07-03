using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.Compensation;
using CLARIHR.Domain.GeneralCatalogs;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.EducationCatalogs;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.Preferences;
using CLARIHR.Domain.SalaryTabulator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Persistence;

internal sealed class DevSeedService(
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    ICompanyProvisioningService companyProvisioningService,
    ICompetencyFrameworkSeedService competencyFrameworkSeedService,
    ILogger<DevSeedService> logger)
{
    private const string DevEmail = "dev@clarihr.local";
    private const string DevPassword = "DevPassword123!";
    private const string DevCountry = "SV";
    private static readonly DateTime SeedDate = new(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc);

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var exists = await dbContext.AuthUsers
            .AnyAsync(u => u.NormalizedEmail == DevEmail.ToLowerInvariant(), cancellationToken);

        if (exists)
        {
            await EnsureExistingDevOwnerRbacAsync(cancellationToken);
            logger.LogInformation("Dev seed already exists. Owner RBAC synchronized.");
            return;
        }

        logger.LogInformation("Seeding development data...");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = await SeedAuthUserAsync(cancellationToken);
        var company = await SeedCompanyAsync(user, cancellationToken);
        var tenantId = company.PublicId;

        await SeedRbacAsync(user, company, tenantId, cancellationToken);
        SeedLocations(tenantId);
        // General/compensation/education catalogs are seeded in EVERY environment via GlobalCatalogSeedData
        // HasData (migration pipeline) — no longer per-tenant here. Income-tax brackets stay dev-only sample data.
        SeedIncomeTaxBrackets(tenantId);
        await dbContext.SaveChangesAsync(cancellationToken);

        var orgUnitType = await SeedOrgStructureCatalogsAsync(user.PublicId, tenantId, cancellationToken);
        var orgUnits = await SeedOrgUnitsAsync(tenantId, orgUnitType.Id, cancellationToken);
        var costCenters = await SeedCostCentersAsync(tenantId, cancellationToken);
        var jobProfiles = await SeedJobProfilesAsync(tenantId, orgUnits, cancellationToken);
        await SeedPositionSlotsAsync(tenantId, jobProfiles, orgUnits, cancellationToken);
        await SeedSalaryTabulatorAsync(tenantId, cancellationToken);
        await SeedPersonnelFileAsync(tenantId, orgUnits, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Dev seed completed: user={Email}, company={CompanyName}, tenant={TenantId}.",
            DevEmail,
            company.Name,
            tenantId);
    }

    private async Task EnsureExistingDevOwnerRbacAsync(CancellationToken cancellationToken)
    {
        var user = await dbContext.AuthUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.NormalizedEmail == DevEmail.ToLowerInvariant(), cancellationToken);
        if (user is null)
        {
            return;
        }

        var companyPublicId = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.CreatedByUserPublicId == user.PublicId)
            .OrderBy(company => company.Id)
            .Select(company => company.PublicId)
            .FirstOrDefaultAsync(cancellationToken);
        if (companyPublicId == Guid.Empty)
        {
            logger.LogWarning("Dev seed user {Email} exists but no owned company was found for RBAC synchronization.", DevEmail);
            return;
        }

        var result = await companyProvisioningService.EnsureOwnerAdministrationAsync(
            user.PublicId,
            companyPublicId,
            cancellationToken);
        if (result.IsFailure)
        {
            logger.LogWarning(
                "Dev seed owner RBAC synchronization failed for {Email}: {ErrorCode}",
                DevEmail,
                result.Error.Code);
        }
    }

    private async Task<User> SeedAuthUserAsync(CancellationToken cancellationToken)
    {
        var hash = passwordHasher.Hash(DevPassword);
        var user = User.RegisterLocal("Adam", "Developer", DevEmail, hash, DevCountry, "dev-seed");
        dbContext.AuthUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.UserPreferences.Add(UserPreference.Create(user.Id, "es"));
        await dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    private async Task<Company> SeedCompanyAsync(User user, CancellationToken cancellationToken)
    {
        var countryCatalog = await dbContext.CountryCatalogItems
            .Where(item => item.NormalizedCode == DevCountry)
            .Select(item => new { item.Id })
            .SingleAsync(cancellationToken);

        var company = Company.Create(
            "CLARIHR Dev",
            "clarihr-dev",
            user.PublicId,
            DevCountry,
            countryCatalog.Id);
        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        var companyPreference = CompanyPreference.Create("USD", "America/El_Salvador");
        companyPreference.SetTenantId(company.PublicId);
        dbContext.CompanyPreferences.Add(companyPreference);
        await dbContext.SaveChangesAsync(cancellationToken);

        var freePlan = await dbContext.CommercialPlans
            .Include(plan => plan.Versions)
            .SingleAsync(plan => plan.NormalizedCode == ProvisioningConstants.FreePlanCode, cancellationToken);

        var subscription = CompanySubscription.Activate(
            company.Id,
            freePlan,
            SeedDate);
        dbContext.CompanySubscriptions.Add(subscription);

        var legalRep = LegalRepresentative.Create(
            "Carlos",
            "Representante",
            "DUI",
            "00000000-0",
            "Representante Legal",
            LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            "Representacion completa ante cualquier autoridad.",
            "Acta constitutiva de la sociedad.",
            appointmentDateUtc: SeedDate,
            effectiveFromUtc: SeedDate,
            effectiveToUtc: null,
            "legal@clarihr-dev.test",
            phone: "+503 2222-3333",
            isPrimary: true);
        legalRep.SetTenantId(company.PublicId);
        dbContext.LegalRepresentatives.Add(legalRep);

        await dbContext.SaveChangesAsync(cancellationToken);
        return company;
    }

    private async Task SeedRbacAsync(User user, Company company, Guid tenantId, CancellationToken cancellationToken)
    {
        var ownerPermissions = OwnerPermissionCatalog.CreateDefaultOwnerPermissions(tenantId);
        dbContext.IamPermissions.AddRange(ownerPermissions);
        await dbContext.SaveChangesAsync(cancellationToken);

        var adminRole = IamRole.Create(
            ProvisioningConstants.CompanyAdminRoleName,
            "Rol de administrador con acceso completo a todos los modulos.",
            isSystemRole: true);
        adminRole.SetTenantId(tenantId);

        var standardRole = IamRole.Create(
            ProvisioningConstants.StandardUserRoleName,
            "Rol base de usuario estandar.",
            isSystemRole: true);
        standardRole.SetTenantId(tenantId);

        dbContext.IamRoles.AddRange(adminRole, standardRole);
        await dbContext.SaveChangesAsync(cancellationToken);

        adminRole.SyncPermissions(ownerPermissions);
        StampTenant(adminRole.PermissionAssignments, tenantId);
        await dbContext.SaveChangesAsync(cancellationToken);

        var iamUser = IamUser.CreateLinked(
            user.PublicId,
            user.FirstName,
            user.LastName,
            user.Email,
            isActive: true);
        iamUser.SetTenantId(tenantId);
        iamUser.SyncRoles([adminRole]);
        StampTenant(iamUser.RoleAssignments, tenantId);
        dbContext.IamUsers.Add(iamUser);

        dbContext.UserCompanyMemberships.Add(
            UserCompanyMembership.Create(user.Id, company.Id, adminRole.Id, isPrimary: true));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void SeedLocations(Guid tenantId)
    {
        var config = LocationHierarchyConfig.Create(isMultiLevel: true, "GENERAL", "General");
        config.SetTenantId(tenantId);

        var countryLevel = LocationLevel.Create(1, "Pais", isActive: true, isRequired: true, allowsWorkCenters: false);
        countryLevel.SetTenantId(tenantId);

        var deptLevel = LocationLevel.Create(2, "Departamento", isActive: true, isRequired: false, allowsWorkCenters: false);
        deptLevel.SetTenantId(tenantId);

        var municipalityLevel = LocationLevel.Create(3, "Municipio", isActive: true, isRequired: false, allowsWorkCenters: true);
        municipalityLevel.SetTenantId(tenantId);

        var country = LocationGroup.Create(1, "SV", "El Salvador", parentId: null, description: "Pais", isDefault: false);
        country.SetTenantId(tenantId);

        var dept = LocationGroup.Create(2, "SS", "San Salvador", parentId: null, description: "Departamento", isDefault: false);
        dept.SetTenantId(tenantId);

        var muni1 = LocationGroup.Create(3, "APOPA", "Apopa", parentId: null, description: "Municipio", isDefault: false);
        muni1.SetTenantId(tenantId);

        var muni2 = LocationGroup.Create(3, "MEJICANOS", "Mejicanos", parentId: null, description: "Municipio", isDefault: false);
        muni2.SetTenantId(tenantId);

        dbContext.LocationHierarchyConfigs.Add(config);
        dbContext.LocationLevels.AddRange(countryLevel, deptLevel, municipalityLevel);
        dbContext.LocationGroups.AddRange(country, dept, muni1, muni2);
        dbContext.SaveChanges();

        dept.Move(country.Id);
        muni1.Move(dept.Id);
        muni2.Move(dept.Id);
    }

    private void SeedIncomeTaxBrackets(Guid tenantId)
    {
        // SV monthly ISR retention table (editable — D-19). Quincenal/semanal load with their own values.
        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthly = new (int Order, decimal Lower, decimal? Upper, decimal FixedFee, decimal Rate, decimal Excess)[]
        {
            (1, 0.01m, 472.00m, 0.00m, 0.00m, 0.00m),
            (2, 472.01m, 895.24m, 17.67m, 10.00m, 472.00m),
            (3, 895.25m, 2038.10m, 60.00m, 20.00m, 895.24m),
            (4, 2038.11m, null, 288.57m, 30.00m, 2038.10m),
        };

        foreach (var item in monthly)
        {
            var entity = IncomeTaxWithholdingBracket.Create(
                "MENSUAL",
                item.Order,
                item.Lower,
                item.Upper,
                item.FixedFee,
                item.Rate,
                item.Excess,
                effectiveFrom,
                null,
                true);
            entity.SetTenantId(tenantId);
            dbContext.IncomeTaxWithholdingBrackets.Add(entity);
        }
    }

    private async Task<OrgUnitTypeCatalogItem> SeedOrgStructureCatalogsAsync(
        Guid ownerUserPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var orgUnitTypes = new[]
        {
            OrgUnitTypeCatalogItem.Create("GERENCIA", "Gerencia", null, 10),
            OrgUnitTypeCatalogItem.Create("DEPARTAMENTO", "Departamento", null, 20),
            OrgUnitTypeCatalogItem.Create("UNIDAD", "Unidad", null, 30),
        };
        foreach (var t in orgUnitTypes) t.SetTenantId(tenantId);
        dbContext.OrgUnitTypeCatalogItems.AddRange(orgUnitTypes);

        var functionalAreas = new[]
        {
            FunctionalAreaCatalogItem.Create("ADMIN", "Administracion", null, 10),
            FunctionalAreaCatalogItem.Create("OPS", "Operaciones", null, 20),
            FunctionalAreaCatalogItem.Create("SALES", "Ventas", null, 30),
            FunctionalAreaCatalogItem.Create("HR", "Recursos Humanos", null, 40),
        };
        foreach (var fa in functionalAreas) fa.SetTenantId(tenantId);
        dbContext.FunctionalAreaCatalogItems.AddRange(functionalAreas);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Competency-framework defaults (D-03/D-04): competency-type catalog + default active rating scale.
        await competencyFrameworkSeedService.InitializeDefaultsAsync(tenantId, cancellationToken);
        return orgUnitTypes[0];
    }

    private async Task<OrgUnit[]> SeedOrgUnitsAsync(
        Guid tenantId,
        long gerenciaTypeId,
        CancellationToken cancellationToken)
    {
        var gerencia = OrgUnit.Create(
            "GG-001", "Gerencia General", gerenciaTypeId,
            functionalAreaCatalogItemId: null, parentId: null, sortOrder: 1,
            description: "Direccion general de la empresa.", costCenterCode: null, managerEmployeeId: null);
        gerencia.SetTenantId(tenantId);
        dbContext.OrgUnits.Add(gerencia);
        await dbContext.SaveChangesAsync(cancellationToken);

        var deptoOps = OrgUnit.Create(
            "DO-001", "Departamento de Operaciones", gerenciaTypeId,
            functionalAreaCatalogItemId: null, parentId: gerencia.Id, sortOrder: 1,
            description: "Operaciones y logistica.", costCenterCode: null, managerEmployeeId: null);
        deptoOps.SetTenantId(tenantId);

        var deptoRrhh = OrgUnit.Create(
            "DH-001", "Departamento de RRHH", gerenciaTypeId,
            functionalAreaCatalogItemId: null, parentId: gerencia.Id, sortOrder: 2,
            description: "Gestion del talento humano.", costCenterCode: null, managerEmployeeId: null);
        deptoRrhh.SetTenantId(tenantId);

        dbContext.OrgUnits.AddRange(deptoOps, deptoRrhh);
        await dbContext.SaveChangesAsync(cancellationToken);

        return [gerencia, deptoOps, deptoRrhh];
    }

    private async Task<CostCenter[]> SeedCostCentersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var salaryExpenseType = CostCenterType.Create(
            "SALARY-EXPENSE", "Gasto salarial", "Centros de costo de gasto salarial.");
        salaryExpenseType.SetTenantId(tenantId);

        var mixedType = CostCenterType.Create(
            "MIXED", "Mixto", "Centros de costo mixtos.");
        mixedType.SetTenantId(tenantId);

        dbContext.CostCenterTypes.AddRange(salaryExpenseType, mixedType);
        await dbContext.SaveChangesAsync(cancellationToken);

        var cc1 = CostCenter.Create(
            "CC-001", "Operaciones", salaryExpenseType.Id,
            payrollExpenseAccountCode: "5101", employerContributionAccountCode: "5102",
            provisionAccountCode: "5103", description: "Centro de costo principal.");
        cc1.SetTenantId(tenantId);

        var cc2 = CostCenter.Create(
            "CC-002", "Administracion", mixedType.Id,
            payrollExpenseAccountCode: "5201", employerContributionAccountCode: null,
            provisionAccountCode: null, description: "Centro de costo administrativo.");
        cc2.SetTenantId(tenantId);

        dbContext.CostCenters.AddRange(cc1, cc2);
        await dbContext.SaveChangesAsync(cancellationToken);
        return [cc1, cc2];
    }

    private async Task<JobProfile[]> SeedJobProfilesAsync(Guid tenantId, OrgUnit[] orgUnits, CancellationToken cancellationToken)
    {
        var gerenteGeneral = JobProfile.Create("JP-001", "Gerente General");
        gerenteGeneral.SetTenantId(tenantId);
        gerenteGeneral.UpdateCore(
            "JP-001",
            "Gerente General",
            objective: null,
            orgUnitId: orgUnits[0].Id,
            reportsToJobProfileId: null,
            positionCategoryId: null,
            strategicObjectiveCatalogItemId: null,
            assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: null,
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null,
            bumpVersion: false);

        var analistaRrhh = JobProfile.Create("JP-002", "Analista de RRHH");
        analistaRrhh.SetTenantId(tenantId);
        analistaRrhh.UpdateCore(
            "JP-002",
            "Analista de RRHH",
            objective: null,
            orgUnitId: orgUnits[2].Id,
            reportsToJobProfileId: null,
            positionCategoryId: null,
            strategicObjectiveCatalogItemId: null,
            assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: null,
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null,
            bumpVersion: false);

        dbContext.JobProfiles.AddRange(gerenteGeneral, analistaRrhh);
        await dbContext.SaveChangesAsync(cancellationToken);
        return [gerenteGeneral, analistaRrhh];
    }

    private async Task SeedPositionSlotsAsync(
        Guid tenantId,
        JobProfile[] jobProfiles,
        OrgUnit[] orgUnits,
        CancellationToken cancellationToken)
    {
        var slot1 = PositionSlot.Create(
            "PS-001", "Plaza Gerente General",
            jobProfiles[0].Id,
            roleId: null,
            workCenterId: null,
            directDependencyPositionSlotId: null, functionalDependencyPositionSlotId: null,
            PositionSlotStatus.Vacant, maxEmployees: 1, occupiedEmployees: 0,
            isFixedTerm: false, effectiveFromUtc: SeedDate, effectiveToUtc: null,
            notes: null);
        slot1.SetTenantId(tenantId);

        var slot2 = PositionSlot.Create(
            "PS-002", "Plaza Analista RRHH",
            jobProfiles[1].Id,
            roleId: null,
            workCenterId: null,
            directDependencyPositionSlotId: null, functionalDependencyPositionSlotId: null,
            PositionSlotStatus.Vacant, maxEmployees: 3, occupiedEmployees: 0,
            isFixedTerm: false, effectiveFromUtc: SeedDate, effectiveToUtc: null,
            notes: null);
        slot2.SetTenantId(tenantId);

        dbContext.PositionSlots.AddRange(slot1, slot2);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSalaryTabulatorAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var lines = new[]
        {
            SalaryTabulatorLine.Create(
                "EXEC", "A", "USD", 3500.00m, 3000.00m, 5000.00m,
                SeedDate, effectiveToUtc: null, notes: "Escala ejecutiva."),
            SalaryTabulatorLine.Create(
                "PROF", "A", "USD", 1800.00m, 1500.00m, 2500.00m,
                SeedDate, effectiveToUtc: null, notes: "Escala profesional."),
            SalaryTabulatorLine.Create(
                "TECH", "A", "USD", 1000.00m, 800.00m, 1500.00m,
                SeedDate, effectiveToUtc: null, notes: "Escala tecnica."),
        };
        foreach (var line in lines) line.SetTenantId(tenantId);

        dbContext.SalaryTabulatorLines.AddRange(lines);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPersonnelFileAsync(
        Guid tenantId,
        OrgUnit[] orgUnits,
        CancellationToken cancellationToken)
    {
        var educationCatalogIds = await LoadEducationCatalogIdsAsync(tenantId, cancellationToken);
        var employees = CreateEmployees(tenantId, orgUnits, educationCatalogIds);

        dbContext.PersonnelFiles.AddRange(employees);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PersonnelFile[] CreateEmployees(Guid tenantId, OrgUnit[] orgUnits, EducationCatalogIds catalogIds)
    {
        // orgUnits[0] = Gerencia General, [1] = Operaciones, [2] = RRHH

        var maria = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Maria", "Gonzalez",
            new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: "Soltera", profession: "Ingeniera Industrial",
            nationality: "Salvadorena",
            personalEmail: "maria.gonzalez@gmail.com",
            institutionalEmail: "maria.gonzalez@clarihr-dev.test",
            personalPhone: "+503 7000-1234", institutionalPhone: "+503 2200-0001",
            birthCountry: "SV", birthDepartment: "San Salvador", birthMunicipality: "Apopa",
            photoFilePublicId: null, orgUnitPublicId: orgUnits[1].PublicId);
        maria.SetTenantId(tenantId);
        maria.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DUI", "00000000-0", SeedDate, null, "CNR", isPrimary: true),
        ]);
        maria.ReplaceAddresses([
            PersonnelFileAddress.Create("Col. Escalon, Calle 5, #123", "CASA", "SV", "San Salvador", "San Salvador", null, isCurrent: true),
        ]);
        maria.ReplaceEmergencyContacts([
            PersonnelFileEmergencyContact.Create("Ana Gonzalez", "Madre", "+503 7000-5678", null, null),
        ]);
        maria.AddEducation(
            PersonnelFileEducation.Create(
                catalogIds.StatusGraduatedId,
                "Ing. Industrial",
                catalogIds.StudyTypeBachelorId,
                catalogIds.CareerIndustrialEngineeringId,
                "Universidad de El Salvador", "SV", specialty: null, isCurrentlyStudying: false,
                new DateTime(2008, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2013, 12, 15, 0, 0, 0, DateTimeKind.Utc),
                educationShiftCatalogItemId: catalogIds.ShiftMorningId,
                educationModalityCatalogItemId: catalogIds.ModalityOnsiteId,
                totalSubjects: 60,
                approvedSubjects: 60));
        maria.AddBankAccount(
            PersonnelFileBankAccount.Create(null, "AGRI", "USD", "0001-1234-5678", "SAVINGS", isPrimary: true));

        var carlos = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Carlos", "Ramirez",
            new DateTime(1985, 11, 3, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: "Casado", profession: "Administrador de Empresas",
            nationality: "Salvadoreno",
            personalEmail: "carlos.ramirez@gmail.com",
            institutionalEmail: "carlos.ramirez@clarihr-dev.test",
            personalPhone: "+503 7111-2222", institutionalPhone: "+503 2200-0002",
            birthCountry: "SV", birthDepartment: "San Salvador", birthMunicipality: "Mejicanos",
            photoFilePublicId: null, orgUnitPublicId: orgUnits[0].PublicId);
        carlos.SetTenantId(tenantId);
        carlos.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DUI", "11111111-1", SeedDate, null, "CNR", isPrimary: true),
            PersonnelFileIdentification.Create("NIT", "0614-031185-101-0", null, null, "MH", isPrimary: false),
        ]);
        carlos.ReplaceAddresses([
            PersonnelFileAddress.Create("Res. San Luis, Psj. 3, #45", "CASA", "SV", "San Salvador", "Mejicanos", null, isCurrent: true),
        ]);
        carlos.ReplaceEmergencyContacts([
            PersonnelFileEmergencyContact.Create("Laura de Ramirez", "Esposa", "+503 7111-3333", null, null),
        ]);
        carlos.AddEducation(
            PersonnelFileEducation.Create(
                catalogIds.StatusGraduatedId,
                "Lic. Administracion de Empresas",
                catalogIds.StudyTypeBachelorId,
                catalogIds.CareerBusinessAdministrationId,
                "Universidad Centroamericana Jose Simeon Canas",
                "SV",
                specialty: null, isCurrentlyStudying: false,
                new DateTime(2003, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2008, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                educationShiftCatalogItemId: catalogIds.ShiftMorningId,
                educationModalityCatalogItemId: catalogIds.ModalityOnsiteId,
                totalSubjects: 55,
                approvedSubjects: 55));
        carlos.AddEducation(
            PersonnelFileEducation.Create(
                catalogIds.StatusGraduatedId,
                "MBA",
                catalogIds.StudyTypeMasterId,
                catalogIds.CareerMbaId,
                "INCAE",
                "SV",
                specialty: "Finanzas", isCurrentlyStudying: false,
                new DateTime(2010, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2012, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                educationShiftCatalogItemId: catalogIds.ShiftAfternoonId,
                educationModalityCatalogItemId: catalogIds.ModalityOnsiteId,
                totalSubjects: 20,
                approvedSubjects: 20));
        carlos.AddLanguage(
            PersonnelFileLanguage.Create("ENGLISH", "ADVANCED", speaks: true, writes: true, reads: true));
        carlos.AddBankAccount(
            PersonnelFileBankAccount.Create(null, "DAVI", "USD", "0002-9876-5432", "CHECKING", isPrimary: true));
        carlos.AddPreviousEmployment(
            PersonnelFilePreviousEmployment.Create("Grupo Roble", "San Salvador", "Coordinador Financiero",
                "Roberto Mendez", new DateTime(2008, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2015, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                "+503 2500-0000", "Crecimiento profesional", 1200.00m, 2200.00m, null, "USD"));

        var andrea = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Andrea", "Lopez",
            new DateTime(1993, 8, 22, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: "Soltera", profession: "Psicologa Organizacional",
            nationality: "Salvadorena",
            personalEmail: "andrea.lopez@gmail.com",
            institutionalEmail: "andrea.lopez@clarihr-dev.test",
            personalPhone: "+503 7222-3333", institutionalPhone: "+503 2200-0003",
            birthCountry: "SV", birthDepartment: "San Salvador", birthMunicipality: "San Salvador",
            photoFilePublicId: null, orgUnitPublicId: orgUnits[2].PublicId);
        andrea.SetTenantId(tenantId);
        andrea.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DUI", "22222222-2", SeedDate, null, "CNR", isPrimary: true),
        ]);
        andrea.ReplaceAddresses([
            PersonnelFileAddress.Create("Urb. Lomas de San Francisco, #78", "CASA", "SV", "San Salvador", "San Salvador", null, isCurrent: true),
        ]);
        andrea.ReplaceEmergencyContacts([
            PersonnelFileEmergencyContact.Create("Roberto Lopez", "Padre", "+503 7222-4444", null, null),
        ]);
        andrea.AddEducation(
            PersonnelFileEducation.Create(
                catalogIds.StatusGraduatedId,
                "Lic. Psicologia",
                catalogIds.StudyTypeBachelorId,
                catalogIds.CareerPsychologyId,
                "Universidad Dr. Jose Matias Delgado",
                "SV",
                specialty: "Organizacional",
                isCurrentlyStudying: false,
                new DateTime(2011, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2016, 12, 10, 0, 0, 0, DateTimeKind.Utc),
                educationShiftCatalogItemId: catalogIds.ShiftMorningId,
                educationModalityCatalogItemId: catalogIds.ModalityOnsiteId,
                totalSubjects: 50,
                approvedSubjects: 50));
        andrea.AddLanguage(
            PersonnelFileLanguage.Create("ENGLISH", "INTERMEDIATE", speaks: true, writes: true, reads: true));
        andrea.AddTraining(
            PersonnelFileTraining.Create("Clima Laboral y Bienestar", "COURSE", "Medicion de clima organizacional",
                "Recursos Humanos", "FUNDEMÁS", "Sandra Morales", 95.0m,
                new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                isInternal: false, isLocal: true, "SV", 40, "HOUR", 150.00m, "USD"));
        andrea.AddReference(
            PersonnelFileReference.Create("Patricia Hernandez", "Col. Miramonte, #55", "+503 7888-1111",
                "PROFESSIONAL", "Directora de RRHH", "TalentCorp", "+503 2300-5000", 5));


        var jose = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Jose", "Martinez",
            new DateTime(1988, 2, 10, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: "Casado", profession: "Ingeniero en Sistemas",
            nationality: "Salvadoreno",
            personalEmail: "jose.martinez@gmail.com",
            institutionalEmail: "jose.martinez@clarihr-dev.test",
            personalPhone: "+503 7333-4444", institutionalPhone: "+503 2200-0004",
            birthCountry: "SV", birthDepartment: "San Salvador", birthMunicipality: "Apopa",
            photoFilePublicId: null, orgUnitPublicId: orgUnits[1].PublicId);
        jose.SetTenantId(tenantId);
        jose.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DUI", "33333333-3", SeedDate, null, "CNR", isPrimary: true),
            PersonnelFileIdentification.Create("PASSPORT", "A12345678", SeedDate,
                new DateTime(2030, 12, 31, 0, 0, 0, DateTimeKind.Utc), "MREE", isPrimary: false),
        ]);
        jose.ReplaceAddresses([
            PersonnelFileAddress.Create("Col. Las Mercedes, Av. Norte, #200", "CASA", "SV", "San Salvador", "Apopa", null, isCurrent: true),
        ]);
        jose.ReplaceEmergencyContacts([
            PersonnelFileEmergencyContact.Create("Carmen de Martinez", "Esposa", "+503 7333-5555", null, null),
            PersonnelFileEmergencyContact.Create("Pedro Martinez", "Hermano", "+503 7333-6666", null, "Banco Agricola"),
        ]);
        jose.AddEducation(
            PersonnelFileEducation.Create(
                catalogIds.StatusGraduatedId,
                "Ing. en Sistemas Informaticos",
                catalogIds.StudyTypeBachelorId,
                catalogIds.CareerSystemsEngineeringId,
                "Universidad Don Bosco",
                "SV",
                specialty: null, isCurrentlyStudying: false,
                new DateTime(2006, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2011, 12, 15, 0, 0, 0, DateTimeKind.Utc),
                educationShiftCatalogItemId: catalogIds.ShiftMorningId,
                educationModalityCatalogItemId: catalogIds.ModalityOnsiteId,
                totalSubjects: 58,
                approvedSubjects: 58));
        jose.AddLanguage(
            PersonnelFileLanguage.Create("ENGLISH", "ADVANCED", speaks: true, writes: true, reads: true));
        jose.AddLanguage(
            PersonnelFileLanguage.Create("SPANISH", "ADVANCED", speaks: true, writes: true, reads: true));
        jose.AddBankAccount(
            PersonnelFileBankAccount.Create(null, "AGRI", "USD", "0003-5555-7777", "SAVINGS", isPrimary: true));
        jose.ReplaceFamilyMembers([
            PersonnelFileFamilyMember.Create("Carmen", "Alvarez de Martinez", "Esposa", "Salvadorena",
                new DateTime(1990, 7, 20, 0, 0, 0, DateTimeKind.Utc), PersonnelFamilyMemberSex.Female,
                "Casada", "Contadora", documentType: "DUI", documentNumber: "44444444-4",
                phone: "+503 7333-5555", isStudying: false, studyPlace: null, academicLevel: "Licenciatura",
                isBeneficiary: true, isWorking: true, workplace: "Deloitte", jobTitle: "Auditora Senior",
                workPhone: "+503 2400-0000", salary: 1800.00m, isDeceased: false, deceasedDate: null),
        ]);

        var lucia = PersonnelFile.Create(
            PersonnelFileRecordType.Candidate,
            "Lucia", "Hernandez",
            new DateTime(1997, 12, 5, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: "Soltera", profession: "Contadora Publica",
            nationality: "Guatemalteca",
            personalEmail: "lucia.hernandez@gmail.com",
            institutionalEmail: null,
            personalPhone: "+502 5000-1234", institutionalPhone: null,
            birthCountry: "GT", birthDepartment: "Guatemala", birthMunicipality: "Guatemala City",
            photoFilePublicId: null, orgUnitPublicId: null);
        lucia.SetTenantId(tenantId);
        lucia.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DPI", "1234567890101", SeedDate, null, "RENAP", isPrimary: true),
        ]);
        lucia.ReplaceAddresses([
            PersonnelFileAddress.Create("Zona 10, 4a Calle, #12", "CASA", "GT", "Guatemala", "Guatemala City", "01010", isCurrent: true),
        ]);
        lucia.AddEducation(
            PersonnelFileEducation.Create(
                catalogIds.StatusInProgressId,
                null,
                catalogIds.StudyTypeBachelorId,
                catalogIds.CareerAccountingAuditingId,
                "Universidad Rafael Landivar",
                "GT",
                specialty: null,
                isCurrentlyStudying: true,
                new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc), endDate: null,
                educationShiftCatalogItemId: catalogIds.ShiftAfternoonId,
                educationModalityCatalogItemId: catalogIds.ModalityRemoteId,
                totalSubjects: 45,
                approvedSubjects: 38));
        lucia.AddReference(
            PersonnelFileReference.Create("Marco Estrada", "Zona 14, Guatemala", "+502 5111-2222",
                "PERSONAL", occupation: null, workplace: null, workPhone: null, 3));
        lucia.AddReference(
            PersonnelFileReference.Create("Diana Morales", "Zona 10, Guatemala", "+502 5333-4444",
                "PROFESSIONAL", "Gerente Contable", "PwC Guatemala", "+502 2300-0000", 2));


        return [maria, carlos, andrea, jose, lucia];
    }

    private async Task<EducationCatalogIds> LoadEducationCatalogIdsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        async Task<long> ResolveAsync<TCatalogItem>(string code)
            where TCatalogItem : EducationCatalogItem =>
            await dbContext.Set<TCatalogItem>()
                .Where(item => item.NormalizedCode == code)
                .Select(item => item.Id)
                .SingleAsync(cancellationToken);

        // Careers left the education base type (country-scoped, RF-009); codes stay unique per country
        // (SV-only data), so resolving by code alone remains unambiguous.
        async Task<long> ResolveCareerAsync(string code) =>
            await dbContext.EducationCareerCatalogItems
                .Where(item => item.NormalizedCode == code)
                .Select(item => item.Id)
                .SingleAsync(cancellationToken);

        return new EducationCatalogIds(
            await ResolveAsync<EducationStatusCatalogItem>("GRADUATED"),
            await ResolveAsync<EducationStatusCatalogItem>("IN_PROGRESS"),
            await ResolveAsync<EducationStudyTypeCatalogItem>("UNIVERSITARIA"),
            await ResolveAsync<EducationStudyTypeCatalogItem>("POSGRADO"),
            await ResolveAsync<EducationShiftCatalogItem>("MORNING"),
            await ResolveAsync<EducationShiftCatalogItem>("AFTERNOON"),
            await ResolveAsync<EducationModalityCatalogItem>("ONSITE"),
            await ResolveAsync<EducationModalityCatalogItem>("REMOTE"),
            await ResolveCareerAsync("ING_INDUSTRIAL"),
            await ResolveCareerAsync("LIC_ADMIN"),
            await ResolveCareerAsync("MBA"),
            await ResolveCareerAsync("LIC_PSICOLOGIA"),
            await ResolveCareerAsync("ING_SISTEMAS"),
            await ResolveCareerAsync("LIC_CONTADURIA"));
    }

    private sealed record EducationCatalogIds(
        long StatusGraduatedId,
        long StatusInProgressId,
        long StudyTypeBachelorId,
        long StudyTypeMasterId,
        long ShiftMorningId,
        long ShiftAfternoonId,
        long ModalityOnsiteId,
        long ModalityRemoteId,
        long CareerIndustrialEngineeringId,
        long CareerBusinessAdministrationId,
        long CareerMbaId,
        long CareerPsychologyId,
        long CareerSystemsEngineeringId,
        long CareerAccountingAuditingId);


    private static void StampTenant(IEnumerable<TenantEntity> entities, Guid tenantId)
    {
        foreach (var entity in entities)
        {
            entity.SetTenantId(tenantId);
        }
    }
}
