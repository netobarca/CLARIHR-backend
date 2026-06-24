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
        SeedGeneralCatalogItems(tenantId);
        SeedIncomeTaxBrackets(tenantId);
        SeedPersonnelEducationCatalogItems();
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

    private SeedCompanyCountry GetSeedCompanyCountry(Guid tenantId)
    {
        var country = dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == tenantId)
            .Select(company => new SeedCompanyCountry(company.CountryCatalogItemId, company.CountryCode))
            .SingleOrDefault();

        return country ?? throw new InvalidOperationException($"Company country could not be resolved for tenant {tenantId}.");
    }

    private void SeedGeneralCatalogItems(Guid tenantId)
    {
        var companyCountry = GetSeedCompanyCountry(tenantId);

        var languages = new (string Code, string Name, int SortOrder)[]
        {
            ("ENGLISH", "Ingles", 10),
            ("SPANISH", "Espanol", 20),
        };

        var languageLevels = new (string Code, string Name, int SortOrder)[]
        {
            ("ADVANCED", "Avanzado", 10),
            ("INTERMEDIATE", "Intermedio", 20),
            ("BASIC", "Basico", 30),
        };

        var trainingTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("COURSE", "Curso", 10),
            ("WORKSHOP", "Taller", 20),
            ("CERTIFICATION", "Certificacion", 30),
        };

        var durationUnits = new (string Code, string Name, int SortOrder)[]
        {
            ("HOUR", "Hora", 10),
            ("DAY", "Dia", 20),
        };

        var referenceTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("PERSONAL", "Personal", 10),
            ("PROFESSIONAL", "Profesional", 20),
        };

        var currencies = new (string Code, string Name, int SortOrder)[]
        {
            ("USD", "Dolar estadounidense", 10),
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
            ("RECARGO_FUNCIONES", "Recargo de funciones", 90),
        };

        foreach (var item in languages)
        {
            var entity = LanguageCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.LanguageCatalogItems.Add(entity);
        }

        foreach (var item in languageLevels)
        {
            var entity = LanguageLevelCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.LanguageLevelCatalogItems.Add(entity);
        }

        foreach (var item in trainingTypes)
        {
            var entity = TrainingTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.TrainingTypeCatalogItems.Add(entity);
        }

        foreach (var item in durationUnits)
        {
            var entity = DurationUnitCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.DurationUnitCatalogItems.Add(entity);
        }

        foreach (var item in referenceTypes)
        {
            var entity = ReferenceTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.ReferenceTypeCatalogItems.Add(entity);
        }

        foreach (var item in currencies)
        {
            var entity = CurrencyCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.CurrencyCatalogItems.Add(entity);
        }

        foreach (var item in assignmentTypes)
        {
            var entity = AssignmentTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.AssignmentTypeCatalogItems.Add(entity);
        }

        var paymentMethods = new (string Code, string Name, int SortOrder)[]
        {
            ("TRANSFERENCIA", "Transferencia bancaria", 10),
            ("CHEQUE", "Cheque", 20),
            ("EFECTIVO", "Efectivo", 30),
        };

        foreach (var item in paymentMethods)
        {
            var entity = PaymentMethodCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.PaymentMethodCatalogItems.Add(entity);
        }

        var substitutionTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("VACACIONES", "Vacaciones", 10),
            ("INCAPACIDAD", "Incapacidad", 20),
            ("PERMISO", "Permiso", 30),
            ("MISION_OFICIAL", "Misión oficial", 40),
            ("LICENCIA", "Licencia", 50),
            ("OTRO", "Otro", 60),
        };

        foreach (var item in substitutionTypes)
        {
            var entity = SubstitutionTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.SubstitutionTypeCatalogItems.Add(entity);
        }

        var assetAccessTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("EQUIPO_COMPUTO", "Equipo de cómputo", 10),
            ("TELEFONO_MOVIL", "Teléfono móvil", 20),
            ("UNIFORME", "Uniforme", 30),
            ("LICENCIA_SOFTWARE", "Licencia de software", 40),
            ("ACCESO_SISTEMA", "Acceso a sistema", 50),
            ("MOBILIARIO", "Mobiliario", 60),
            ("HERRAMIENTA", "Herramienta", 70),
            ("OTRO", "Otro", 80),
        };

        foreach (var item in assetAccessTypes)
        {
            var entity = AssetAccessTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.AssetAccessTypeCatalogItems.Add(entity);
        }

        var deliveryStatuses = new (string Code, string Name, int SortOrder)[]
        {
            ("PENDIENTE", "Pendiente", 10),
            ("ENTREGADO", "Entregado", 20),
            ("EN_USO", "En uso", 30),
            ("DEVUELTO", "Devuelto", 40),
            ("EXTRAVIADO", "Extraviado", 50),
            ("DANADO", "Dañado", 60),
            ("NO_APLICA", "No aplica", 70),
        };

        foreach (var item in deliveryStatuses)
        {
            var entity = DeliveryStatusCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.DeliveryStatusCatalogItems.Add(entity);
        }

        var medicalClaimTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("AMBULATORIO", "Ambulatorio", 10),
            ("HOSPITALARIO", "Hospitalario", 20),
            ("EMERGENCIA", "Emergencia", 30),
            ("FARMACIA", "Farmacia", 40),
            ("LABORATORIO", "Laboratorio", 50),
            ("DENTAL", "Dental", 60),
            ("OFTALMOLOGICO", "Oftalmológico", 70),
            ("MATERNIDAD", "Maternidad", 80),
            ("OTRO", "Otro", 90),
        };

        foreach (var item in medicalClaimTypes)
        {
            var entity = MedicalClaimTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.MedicalClaimTypeCatalogItems.Add(entity);
        }

        var medicalClaimStatuses = new (string Code, string Name, int SortOrder)[]
        {
            ("PRESENTADO", "Presentado", 10),
            ("EN_REVISION", "En revisión", 20),
            ("PENDIENTE_DOCUMENTACION", "Pendiente de documentación", 30),
            ("APROBADO", "Aprobado", 40),
            ("RECHAZADO", "Rechazado", 50),
            ("PAGADO", "Pagado", 60),
            ("PAGO_PARCIAL", "Pago parcial", 70),
            ("ANULADO", "Anulado", 80),
        };

        foreach (var item in medicalClaimStatuses)
        {
            var entity = MedicalClaimStatusCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.MedicalClaimStatusCatalogItems.Add(entity);
        }

        // employment-statuses are seeded globally via GlobalCatalogSeedData.GetEmploymentStatusCatalogItems()
        // (HasData) so they exist in every environment and backfill already-provisioned databases — not here,
        // which would double-insert against the HasData rows on a fresh dev database.

        var insuranceTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("VIDA", "Vida", 10),
            ("MEDICO_HOSPITALARIO", "Médico hospitalario", 20),
            ("GASTOS_MEDICOS", "Gastos médicos", 30),
            ("DENTAL", "Dental", 40),
            ("VISION", "Visión", 50),
            ("ACCIDENTES", "Accidentes personales", 60),
            ("OTRO", "Otro", 70),
        };

        var insuranceTypeEntities = new Dictionary<string, InsuranceTypeCatalogItem>(StringComparer.Ordinal);
        foreach (var item in insuranceTypes)
        {
            var entity = InsuranceTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.InsuranceTypeCatalogItems.Add(entity);
            insuranceTypeEntities[item.Code] = entity;
        }

        // Persist the insurance types so their generated ids are available for the hierarchical ranges
        // below (same intermediate-save pattern SeedLocations uses for its location-group hierarchy).
        dbContext.SaveChanges();

        var insuranceRanges = new (string TypeCode, string Code, string Name, int SortOrder)[]
        {
            ("VIDA", "BASICO", "Básico", 10),
            ("VIDA", "INTERMEDIO", "Intermedio", 20),
            ("VIDA", "PREMIUM", "Premium", 30),
            ("MEDICO_HOSPITALARIO", "BASICO", "Básico", 10),
            ("MEDICO_HOSPITALARIO", "INTERMEDIO", "Intermedio", 20),
            ("MEDICO_HOSPITALARIO", "PREMIUM", "Premium", 30),
        };

        foreach (var item in insuranceRanges)
        {
            var entity = InsuranceRangeCatalogItem.Create(
                companyCountry.CountryCatalogItemId,
                companyCountry.CountryCode,
                item.Code,
                item.Name,
                true,
                item.SortOrder,
                insuranceTypeEntities[item.TypeCode].Id);
            dbContext.InsuranceRangeCatalogItems.Add(entity);
        }

        SeedCompensationCatalogItems(companyCountry);
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

    private void SeedCompensationCatalogItems(SeedCompanyCountry companyCountry)
    {
        // Seed SV por defecto (editable en cualquier momento — D-19). Tasas ISSS/AFP y bases son defaults.
        var conceptTypes = new (string Code, string Name, CompensationNature Nature, bool IsStatutory, DeductionClass? DefaultClass, CompensationCalculationType CalcType, string? BaseCode, decimal? EmployeeRate, decimal? EmployerRate, decimal? Cap, int SortOrder)[]
        {
            ("SALARIO_BASE", "Salario base", CompensationNature.Ingreso, false, null, CompensationCalculationType.Fixed, null, null, null, null, 10),
            ("HORAS_EXTRA", "Horas extra", CompensationNature.Ingreso, false, null, CompensationCalculationType.Fixed, null, null, null, null, 20),
            ("COMISION", "Comision", CompensationNature.Ingreso, false, null, CompensationCalculationType.Percentage, "SALARIO_BASE", null, null, null, 30),
            ("BONO", "Bono", CompensationNature.Ingreso, false, null, CompensationCalculationType.Fixed, null, null, null, null, 40),
            ("VIATICOS", "Viaticos", CompensationNature.Ingreso, false, null, CompensationCalculationType.Fixed, null, null, null, null, 50),
            ("AGUINALDO", "Aguinaldo", CompensationNature.Ingreso, false, null, CompensationCalculationType.Fixed, null, null, null, null, 60),
            ("OTRO_INGRESO", "Otro ingreso", CompensationNature.Ingreso, false, null, CompensationCalculationType.Fixed, null, null, null, null, 70),
            ("ISSS", "ISSS", CompensationNature.Egreso, true, DeductionClass.Ley, CompensationCalculationType.Percentage, "IBC", 3.00m, 7.50m, 1000.00m, 100),
            ("AFP", "AFP", CompensationNature.Egreso, true, DeductionClass.Ley, CompensationCalculationType.Percentage, "IBC", 7.25m, 8.75m, null, 110),
            ("RENTA", "Renta (ISR)", CompensationNature.Egreso, true, DeductionClass.Ley, CompensationCalculationType.Percentage, "SALARIO_BRUTO", null, null, null, 120),
            ("DANO_EQUIPO", "Dano de equipo", CompensationNature.Egreso, false, DeductionClass.Interno, CompensationCalculationType.Fixed, null, null, null, null, 200),
            ("ANTICIPO", "Anticipo", CompensationNature.Egreso, false, DeductionClass.Interno, CompensationCalculationType.Fixed, null, null, null, null, 210),
            ("PRESTAMO_INTERNO", "Prestamo interno", CompensationNature.Egreso, false, DeductionClass.Interno, CompensationCalculationType.Fixed, null, null, null, null, 220),
            ("PRESTAMO_BANCARIO", "Prestamo bancario", CompensationNature.Egreso, false, DeductionClass.Externo, CompensationCalculationType.Fixed, null, null, null, null, 300),
            ("EMBARGO", "Embargo", CompensationNature.Egreso, false, DeductionClass.Externo, CompensationCalculationType.Fixed, null, null, null, null, 310),
            ("CUOTA_ALIMENTICIA", "Cuota alimenticia", CompensationNature.Egreso, false, DeductionClass.Externo, CompensationCalculationType.Fixed, null, null, null, null, 320),
            ("OTRO_EXTERNO", "Otro externo", CompensationNature.Egreso, false, DeductionClass.Externo, CompensationCalculationType.Fixed, null, null, null, null, 330),
        };

        foreach (var item in conceptTypes)
        {
            var entity = CompensationConceptTypeCatalogItem.Create(
                companyCountry.CountryCatalogItemId,
                companyCountry.CountryCode,
                item.Code,
                item.Name,
                item.Nature,
                item.IsStatutory,
                item.DefaultClass,
                item.CalcType,
                item.BaseCode,
                item.EmployeeRate,
                item.EmployerRate,
                item.Cap,
                true,
                item.SortOrder);
            dbContext.CompensationConceptTypeCatalogItems.Add(entity);
        }

        var payPeriods = new (string Code, string Name, int SortOrder)[]
        {
            ("MENSUAL", "Mensual", 10),
            ("QUINCENAL", "Quincenal", 20),
            ("SEMANAL", "Semanal", 30),
            ("UNICA", "Unica", 40),
        };

        foreach (var item in payPeriods)
        {
            var entity = PayPeriodCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.PayPeriodCatalogItems.Add(entity);
        }

        var calculationBases = new (string Code, string Name, int SortOrder)[]
        {
            ("SALARIO_BASE", "Salario base", 10),
            ("SALARIO_BRUTO", "Salario bruto", 20),
            ("IBC", "Ingreso base de cotizacion", 30),
            ("RUBRO_ESPECIFICO", "Rubro especifico", 40),
        };

        foreach (var item in calculationBases)
        {
            var entity = CalculationBaseCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, item.Code, item.Name, true, item.SortOrder);
            dbContext.CalculationBaseCatalogItems.Add(entity);
        }
    }

    private void SeedPersonnelEducationCatalogItems()
    {
        var statuses = new (string Code, string Name, int SortOrder)[]
        {
            ("GRADUATED", "Graduado", 10),
            ("IN_PROGRESS", "En curso", 20),
        };

        var studyTypes = new (string Code, string Name, int SortOrder)[]
        {
            ("BACHELOR", "Licenciatura", 10),
            ("MASTER", "Maestria", 20),
            ("TECHNICAL", "Tecnico", 30),
        };

        var shifts = new (string Code, string Name, int SortOrder)[]
        {
            ("MORNING", "Matutino", 10),
            ("AFTERNOON", "Vespertino", 20),
        };

        var modalities = new (string Code, string Name, int SortOrder)[]
        {
            ("ONSITE", "Presencial", 10),
            ("REMOTE", "Virtual", 20),
        };

        var careers = new (string Code, string Name, int SortOrder)[]
        {
            ("INDUSTRIAL_ENGINEERING", "Ingenieria Industrial", 10),
            ("BUSINESS_ADMINISTRATION", "Administracion de Empresas", 20),
            ("MBA", "Maestria en Administracion de Negocios", 30),
            ("PSYCHOLOGY", "Psicologia", 40),
            ("SYSTEMS_ENGINEERING", "Ingenieria en Sistemas Informaticos", 50),
            ("ACCOUNTING_AUDITING", "Contaduria Publica y Auditoria", 60),
        };

        foreach (var item in statuses)
        {
            dbContext.EducationStatusCatalogItems.Add(EducationStatusCatalogItem.Create(item.Code, item.Name, item.SortOrder));
        }

        foreach (var item in studyTypes)
        {
            dbContext.EducationStudyTypeCatalogItems.Add(EducationStudyTypeCatalogItem.Create(item.Code, item.Name, item.SortOrder));
        }

        foreach (var item in shifts)
        {
            dbContext.EducationShiftCatalogItems.Add(EducationShiftCatalogItem.Create(item.Code, item.Name, item.SortOrder));
        }

        foreach (var item in modalities)
        {
            dbContext.EducationModalityCatalogItems.Add(EducationModalityCatalogItem.Create(item.Code, item.Name, item.SortOrder));
        }

        foreach (var item in careers)
        {
            dbContext.EducationCareerCatalogItems.Add(EducationCareerCatalogItem.Create(item.Code, item.Name, item.SortOrder));
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
            PersonnelFileAddress.Create("Col. Escalon, Calle 5, #123", "SV", "San Salvador", "San Salvador", null, isCurrent: true),
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
            PersonnelFileAddress.Create("Res. San Luis, Psj. 3, #45", "SV", "San Salvador", "Mejicanos", null, isCurrent: true),
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
            PersonnelFileAddress.Create("Urb. Lomas de San Francisco, #78", "SV", "San Salvador", "San Salvador", null, isCurrent: true),
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
            PersonnelFileAddress.Create("Col. Las Mercedes, Av. Norte, #200", "SV", "San Salvador", "Apopa", null, isCurrent: true),
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
            PersonnelFileAddress.Create("Zona 10, 4a Calle, #12", "GT", "Guatemala", "Guatemala City", "01010", isCurrent: true),
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

        return new EducationCatalogIds(
            await ResolveAsync<EducationStatusCatalogItem>("GRADUATED"),
            await ResolveAsync<EducationStatusCatalogItem>("IN_PROGRESS"),
            await ResolveAsync<EducationStudyTypeCatalogItem>("BACHELOR"),
            await ResolveAsync<EducationStudyTypeCatalogItem>("MASTER"),
            await ResolveAsync<EducationShiftCatalogItem>("MORNING"),
            await ResolveAsync<EducationShiftCatalogItem>("AFTERNOON"),
            await ResolveAsync<EducationModalityCatalogItem>("ONSITE"),
            await ResolveAsync<EducationModalityCatalogItem>("REMOTE"),
            await ResolveAsync<EducationCareerCatalogItem>("INDUSTRIAL_ENGINEERING"),
            await ResolveAsync<EducationCareerCatalogItem>("BUSINESS_ADMINISTRATION"),
            await ResolveAsync<EducationCareerCatalogItem>("MBA"),
            await ResolveAsync<EducationCareerCatalogItem>("PSYCHOLOGY"),
            await ResolveAsync<EducationCareerCatalogItem>("SYSTEMS_ENGINEERING"),
            await ResolveAsync<EducationCareerCatalogItem>("ACCOUNTING_AUDITING"));
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

    private sealed record SeedCompanyCountry(long CountryCatalogItemId, string CountryCode);

    private static void StampTenant(IEnumerable<TenantEntity> entities, Guid tenantId)
    {
        foreach (var entity in entities)
        {
            entity.SetTenantId(tenantId);
        }
    }
}
