using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.SalaryTabulator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Persistence;

internal sealed class DevSeedService(
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
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
            logger.LogInformation("Dev seed already exists. Skipping.");
            return;
        }

        logger.LogInformation("Seeding development data...");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = await SeedAuthUserAsync(cancellationToken);
        var company = await SeedCompanyAsync(user, cancellationToken);
        var tenantId = company.PublicId;

        await SeedRbacAsync(user, company, tenantId, cancellationToken);
        SeedLocations(tenantId);
        SeedPersonnelCatalogItems(tenantId);
        await dbContext.SaveChangesAsync(cancellationToken);

        var orgUnitType = await SeedOrgStructureCatalogsAsync(user.PublicId, tenantId, cancellationToken);
        var orgUnits = await SeedOrgUnitsAsync(tenantId, orgUnitType.Id, cancellationToken);
        var costCenters = await SeedCostCentersAsync(tenantId, cancellationToken);
        var jobProfiles = await SeedJobProfilesAsync(tenantId, cancellationToken);
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

    private async Task<User> SeedAuthUserAsync(CancellationToken cancellationToken)
    {
        var hash = passwordHasher.Hash(DevPassword);
        var user = User.RegisterLocal("Adam", "Developer", DevEmail, hash, DevCountry, "dev-seed");
        dbContext.AuthUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<Company> SeedCompanyAsync(User user, CancellationToken cancellationToken)
    {
        var company = Company.Create("CLARIHR Dev", "clarihr-dev", user.PublicId, DevCountry);
        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        var subscription = CompanySubscription.Activate(
            company.Id,
            ProvisioningConstants.EnterprisePlanCode,
            SeedDate);
        dbContext.CompanySubscriptions.Add(subscription);

        var legalRep = LegalRepresentative.Create(
            "Carlos",
            "Representante",
            LegalRepresentativeDocumentType.NationalId,
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
        var adminPermissions = new List<IamPermission>();
        foreach (var definition in ProvisioningConstants.CompanyAdminPermissions)
        {
            var permission = IamPermission.CreateScreenAction(
                definition.Code,
                definition.Name,
                definition.Description,
                definition.Module,
                definition.Screen,
                definition.Action);
            permission.SetTenantId(tenantId);
            adminPermissions.Add(permission);
        }

        var matrixPermissions = new List<IamPermission>();
        foreach (var screen in PermissionMatrixCatalog.Screens)
        {
            foreach (var action in screen.SupportedActions)
            {
                var permission = PermissionMatrixCatalog.CreatePermission(screen.ScreenKey, action);
                permission.SetTenantId(tenantId);
                matrixPermissions.Add(permission);
            }
        }

        dbContext.IamPermissions.AddRange(adminPermissions);
        dbContext.IamPermissions.AddRange(matrixPermissions);
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

        var allPermissions = adminPermissions.Concat(matrixPermissions).ToArray();
        adminRole.SyncPermissions(allPermissions);
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

    private void SeedPersonnelCatalogItems(Guid tenantId)
    {
        var items = new (string Category, string Code, string Name, int SortOrder)[]
        {
            ("CurriculumEducationStatus", "GRADUATED", "Graduado", 10),
            ("CurriculumEducationStatus", "IN_PROGRESS", "En curso", 20),
            ("CurriculumStudyType", "BACHELOR", "Licenciatura", 10),
            ("CurriculumStudyType", "MASTER", "Maestria", 20),
            ("CurriculumStudyType", "TECHNICAL", "Tecnico", 30),
            ("CurriculumShift", "MORNING", "Matutino", 10),
            ("CurriculumShift", "AFTERNOON", "Vespertino", 20),
            ("CurriculumModality", "ONSITE", "Presencial", 10),
            ("CurriculumModality", "REMOTE", "Virtual", 20),
            ("CurriculumLanguage", "ENGLISH", "Ingles", 10),
            ("CurriculumLanguage", "SPANISH", "Espanol", 20),
            ("CurriculumLanguageLevel", "ADVANCED", "Avanzado", 10),
            ("CurriculumLanguageLevel", "INTERMEDIATE", "Intermedio", 20),
            ("CurriculumLanguageLevel", "BASIC", "Basico", 30),
            ("CurriculumTrainingType", "COURSE", "Curso", 10),
            ("CurriculumTrainingType", "WORKSHOP", "Taller", 20),
            ("CurriculumTrainingType", "CERTIFICATION", "Certificacion", 30),
            ("CurriculumDurationUnit", "HOUR", "Hora", 10),
            ("CurriculumDurationUnit", "DAY", "Dia", 20),
            ("CurriculumReferenceType", "PERSONAL", "Personal", 10),
            ("CurriculumReferenceType", "PROFESSIONAL", "Profesional", 20),
            ("Country", "SV", "El Salvador", 10),
            ("Country", "GT", "Guatemala", 20),
            ("Currency", "USD", "Dolar estadounidense", 10),
        };

        foreach (var item in items)
        {
            var catalogItem = PersonnelCatalogItem.Create(
                item.Category, item.Code, item.Name, isSystem: true, isActive: true, item.SortOrder);
            catalogItem.SetTenantId(tenantId);
            dbContext.PersonnelCatalogItems.Add(catalogItem);
        }
    }

    private async Task<OrgUnitTypeCatalogItem> SeedOrgStructureCatalogsAsync(
        Guid ownerUserPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var companyTypes = new[]
        {
            CompanyTypeCatalogItem.Create(ownerUserPublicId, "SA_DE_CV", "Sociedad Anonima de C.V.", null, 10),
            CompanyTypeCatalogItem.Create(ownerUserPublicId, "COOPERATIVE", "Cooperativa", null, 20),
            CompanyTypeCatalogItem.Create(ownerUserPublicId, "ASSOCIATION", "Asociacion sin fines de lucro", null, 30),
        };
        dbContext.CompanyTypeCatalogItems.AddRange(companyTypes);

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
        var cc1 = CostCenter.Create(
            "CC-001", "Operaciones", CostCenterType.SalaryExpense,
            payrollExpenseAccountCode: "5101", employerContributionAccountCode: "5102",
            provisionAccountCode: "5103", description: "Centro de costo principal.");
        cc1.SetTenantId(tenantId);

        var cc2 = CostCenter.Create(
            "CC-002", "Administracion", CostCenterType.Mixed,
            payrollExpenseAccountCode: "5201", employerContributionAccountCode: null,
            provisionAccountCode: null, description: "Centro de costo administrativo.");
        cc2.SetTenantId(tenantId);

        dbContext.CostCenters.AddRange(cc1, cc2);
        await dbContext.SaveChangesAsync(cancellationToken);
        return [cc1, cc2];
    }

    private async Task<JobProfile[]> SeedJobProfilesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var gerenteGeneral = JobProfile.Create("JP-001", "Gerente General");
        gerenteGeneral.SetTenantId(tenantId);

        var analistaRrhh = JobProfile.Create("JP-002", "Analista de RRHH");
        analistaRrhh.SetTenantId(tenantId);

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
            jobProfiles[0].Id, orgUnits[0].Id,
            workCenterId: null, costCenterCode: "CC-001",
            directDependencyPositionSlotId: null, functionalDependencyPositionSlotId: null,
            PositionSlotStatus.Vacant, maxEmployees: 1, occupiedEmployees: 0,
            isFixedTerm: false, effectiveFromUtc: SeedDate, effectiveToUtc: null,
            notes: null);
        slot1.SetTenantId(tenantId);

        var slot2 = PositionSlot.Create(
            "PS-002", "Plaza Analista RRHH",
            jobProfiles[1].Id, orgUnits[2].Id,
            workCenterId: null, costCenterCode: "CC-002",
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
        var employees = CreateEmployees(tenantId, orgUnits);

        dbContext.PersonnelFiles.AddRange(employees);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PersonnelFile[] CreateEmployees(Guid tenantId, OrgUnit[] orgUnits)
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
            photoUrl: null, orgUnitPublicId: orgUnits[1].PublicId, customDataJson: null);
        maria.SetTenantId(tenantId);
        maria.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DUI", "00000000-0", SeedDate, null, "CNR", isPrimary: true),
        ]);
        maria.ReplaceAddresses([
            PersonnelFileAddress.Create("Col. Escalon, Calle 5, #123", "SV", "San Salvador", "San Salvador", null, isCurrent: true),
        ]);
        maria.ReplaceEmergencyContacts([
            PersonnelFileEmergencyContact.Create("Ana Gonzalez", "Madre", "+503 7000-5678", null, null),
        ]);
        maria.ReplaceEducations([
            PersonnelFileEducation.Create("GRADUATED", "Ing. Industrial", "BACHELOR", "Ingenieria Industrial",
                "Universidad de El Salvador", "SV", specialty: null, isCurrentlyStudying: false,
                new DateTime(2008, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2013, 12, 15, 0, 0, 0, DateTimeKind.Utc),
                shiftCode: "MORNING", modalityCode: "ONSITE", totalSubjects: 60, approvedSubjects: 60),
        ]);
        maria.ReplaceBankAccounts([
            PersonnelFileBankAccount.Create("AGRI", "USD", "0001-1234-5678", "SAVINGS", isPrimary: true),
        ]);

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
            photoUrl: null, orgUnitPublicId: orgUnits[0].PublicId, customDataJson: null);
        carlos.SetTenantId(tenantId);
        carlos.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DUI", "11111111-1", SeedDate, null, "CNR", isPrimary: true),
            PersonnelFileIdentification.Create("NIT", "0614-031185-101-0", null, null, "MH", isPrimary: false),
        ]);
        carlos.ReplaceAddresses([
            PersonnelFileAddress.Create("Res. San Luis, Psj. 3, #45", "SV", "San Salvador", "Mejicanos", null, isCurrent: true),
        ]);
        carlos.ReplaceEmergencyContacts([
            PersonnelFileEmergencyContact.Create("Laura de Ramirez", "Esposa", "+503 7111-3333", null, null),
        ]);
        carlos.ReplaceEducations([
            PersonnelFileEducation.Create("GRADUATED", "Lic. Administracion de Empresas", "BACHELOR",
                "Administracion de Empresas", "Universidad Centroamericana Jose Simeon Canas", "SV",
                specialty: null, isCurrentlyStudying: false,
                new DateTime(2003, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2008, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                shiftCode: "MORNING", modalityCode: "ONSITE", totalSubjects: 55, approvedSubjects: 55),
            PersonnelFileEducation.Create("GRADUATED", "MBA", "MASTER",
                "Maestria en Administracion de Negocios", "INCAE", "SV",
                specialty: "Finanzas", isCurrentlyStudying: false,
                new DateTime(2010, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2012, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                shiftCode: "AFTERNOON", modalityCode: "ONSITE", totalSubjects: 20, approvedSubjects: 20),
        ]);
        carlos.ReplaceLanguages([
            PersonnelFileLanguage.Create("ENGLISH", "ADVANCED", speaks: true, writes: true, reads: true),
        ]);
        carlos.ReplaceBankAccounts([
            PersonnelFileBankAccount.Create("DAVI", "USD", "0002-9876-5432", "CHECKING", isPrimary: true),
        ]);
        carlos.ReplacePreviousEmployments([
            PersonnelFilePreviousEmployment.Create("Grupo Roble", "San Salvador", "Coordinador Financiero",
                "Roberto Mendez", new DateTime(2008, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2015, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                "+503 2500-0000", "Crecimiento profesional", 1200.00m, 2200.00m, null, "USD"),
        ]);

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
            photoUrl: null, orgUnitPublicId: orgUnits[2].PublicId, customDataJson: null);
        andrea.SetTenantId(tenantId);
        andrea.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DUI", "22222222-2", SeedDate, null, "CNR", isPrimary: true),
        ]);
        andrea.ReplaceAddresses([
            PersonnelFileAddress.Create("Urb. Lomas de San Francisco, #78", "SV", "San Salvador", "San Salvador", null, isCurrent: true),
        ]);
        andrea.ReplaceEmergencyContacts([
            PersonnelFileEmergencyContact.Create("Roberto Lopez", "Padre", "+503 7222-4444", null, null),
        ]);
        andrea.ReplaceEducations([
            PersonnelFileEducation.Create("GRADUATED", "Lic. Psicologia", "BACHELOR", "Psicologia",
                "Universidad Dr. Jose Matias Delgado", "SV", specialty: "Organizacional",
                isCurrentlyStudying: false,
                new DateTime(2011, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2016, 12, 10, 0, 0, 0, DateTimeKind.Utc),
                shiftCode: "MORNING", modalityCode: "ONSITE", totalSubjects: 50, approvedSubjects: 50),
        ]);
        andrea.ReplaceLanguages([
            PersonnelFileLanguage.Create("ENGLISH", "INTERMEDIATE", speaks: true, writes: true, reads: true),
        ]);
        andrea.ReplaceTrainings([
            PersonnelFileTraining.Create("Clima Laboral y Bienestar", "COURSE", "Medicion de clima organizacional",
                "Recursos Humanos", "FUNDEMÁS", "Sandra Morales", 95.0m,
                new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                isInternal: false, isLocal: true, "SV", 40, "HOUR", 150.00m, "USD"),
        ]);
        andrea.ReplaceReferences([
            PersonnelFileReference.Create("Patricia Hernandez", "Col. Miramonte, #55", "+503 7888-1111",
                "PROFESSIONAL", "Directora de RRHH", "TalentCorp", "+503 2300-5000", 5),
        ]);

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
            photoUrl: null, orgUnitPublicId: orgUnits[1].PublicId, customDataJson: null);
        jose.SetTenantId(tenantId);
        jose.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DUI", "33333333-3", SeedDate, null, "CNR", isPrimary: true),
            PersonnelFileIdentification.Create("PASSPORT", "A12345678", SeedDate,
                new DateTime(2030, 12, 31, 0, 0, 0, DateTimeKind.Utc), "MREE", isPrimary: false),
        ]);
        jose.ReplaceAddresses([
            PersonnelFileAddress.Create("Col. Las Mercedes, Av. Norte, #200", "SV", "San Salvador", "Apopa", null, isCurrent: true),
        ]);
        jose.ReplaceEmergencyContacts([
            PersonnelFileEmergencyContact.Create("Carmen de Martinez", "Esposa", "+503 7333-5555", null, null),
            PersonnelFileEmergencyContact.Create("Pedro Martinez", "Hermano", "+503 7333-6666", null, "Banco Agricola"),
        ]);
        jose.ReplaceEducations([
            PersonnelFileEducation.Create("GRADUATED", "Ing. en Sistemas Informaticos", "BACHELOR",
                "Ingenieria en Sistemas Informaticos", "Universidad Don Bosco", "SV",
                specialty: null, isCurrentlyStudying: false,
                new DateTime(2006, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2011, 12, 15, 0, 0, 0, DateTimeKind.Utc),
                shiftCode: "MORNING", modalityCode: "ONSITE", totalSubjects: 58, approvedSubjects: 58),
        ]);
        jose.ReplaceLanguages([
            PersonnelFileLanguage.Create("ENGLISH", "ADVANCED", speaks: true, writes: true, reads: true),
            PersonnelFileLanguage.Create("SPANISH", "ADVANCED", speaks: true, writes: true, reads: true),
        ]);
        jose.ReplaceBankAccounts([
            PersonnelFileBankAccount.Create("AGRI", "USD", "0003-5555-7777", "SAVINGS", isPrimary: true),
        ]);
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
            photoUrl: null, orgUnitPublicId: null, customDataJson: null);
        lucia.SetTenantId(tenantId);
        lucia.ReplaceIdentifications([
            PersonnelFileIdentification.Create("DPI", "1234567890101", SeedDate, null, "RENAP", isPrimary: true),
        ]);
        lucia.ReplaceAddresses([
            PersonnelFileAddress.Create("Zona 10, 4a Calle, #12", "GT", "Guatemala", "Guatemala City", "01010", isCurrent: true),
        ]);
        lucia.ReplaceEducations([
            PersonnelFileEducation.Create("IN_PROGRESS", null, "BACHELOR", "Contaduria Publica y Auditoria",
                "Universidad Rafael Landivar", "GT", specialty: null, isCurrentlyStudying: true,
                new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc), endDate: null,
                shiftCode: "AFTERNOON", modalityCode: "REMOTE", totalSubjects: 45, approvedSubjects: 38),
        ]);
        lucia.ReplaceReferences([
            PersonnelFileReference.Create("Marco Estrada", "Zona 14, Guatemala", "+502 5111-2222",
                "PERSONAL", occupation: null, workplace: null, workPhone: null, 3),
            PersonnelFileReference.Create("Diana Morales", "Zona 10, Guatemala", "+502 5333-4444",
                "PROFESSIONAL", "Gerente Contable", "PwC Guatemala", "+502 2300-0000", 2),
        ]);

        return [maria, carlos, andrea, jose, lucia];
    }

    private static void StampTenant(IEnumerable<TenantEntity> entities, Guid tenantId)
    {
        foreach (var entity in entities)
        {
            entity.SetTenantId(tenantId);
        }
    }
}
