using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PositionDescriptionCatalogs;

internal sealed class PositionDescriptionCatalogSeedService(ApplicationDbContext dbContext)
{
    private static readonly SimpleCatalogSeed[] PositionFunctionTypeSeeds =
    [
        new(PositionDescriptionCatalogType.PositionFunctionType, "ADMINISTRATIVO", "Administrativo", "Funcion base para puestos administrativos.", 10)
    ];

    private static readonly SimpleCatalogSeed[] PositionContractTypeSeeds =
    [
        new(PositionDescriptionCatalogType.PositionContractType, "INDEFINIDO", "Indefinido", "Contrato de duracion indefinida.", 10)
    ];

    private static readonly SimpleCatalogSeed[] StrategicObjectiveSeeds =
    [
        new(PositionDescriptionCatalogType.StrategicObjective, "EFICIENCIA_OPERATIVA", "Eficiencia operativa", "Promueve mejoras sostenibles en tiempos, calidad y uso de recursos.", 10),
        new(PositionDescriptionCatalogType.StrategicObjective, "SERVICIO_INTERNO", "Servicio interno", "Fortalece el servicio a las areas internas y la coordinacion transversal.", 20),
        new(PositionDescriptionCatalogType.StrategicObjective, "DESARROLLO_DEL_TALENTO", "Desarrollo del talento", "Impulsa practicas que fortalecen capacidades y continuidad organizacional.", 30)
    ];

    private static readonly SimpleCatalogSeed[] WorkEquipmentSeeds =
    [
        new(PositionDescriptionCatalogType.WorkEquipment, "LAPTOP_CORPORATIVA", "Laptop corporativa", "Equipo de computo asignado para la ejecucion del puesto.", 10),
        new(PositionDescriptionCatalogType.WorkEquipment, "TELEFONO_CORPORATIVO", "Telefono corporativo", "Dispositivo movil corporativo para comunicacion operativa.", 20),
        new(PositionDescriptionCatalogType.WorkEquipment, "KIT_OFICINA", "Kit de oficina", "Equipo basico de oficina para la operacion diaria.", 30)
    ];

    private static readonly SimpleCatalogSeed[] ResponsibilitySeeds =
    [
        new(PositionDescriptionCatalogType.Responsibility, "EJECUCION_DE_PROCESOS", "Ejecucion de procesos", "Responsabilidad sobre la ejecucion oportuna y correcta de procesos del area.", 10),
        new(PositionDescriptionCatalogType.Responsibility, "COORDINACION_DE_EQUIPO", "Coordinacion de equipo", "Responsabilidad sobre la coordinacion de tareas y seguimiento operativo.", 20),
        new(PositionDescriptionCatalogType.Responsibility, "CONTROL_DE_CUMPLIMIENTO", "Control de cumplimiento", "Responsabilidad sobre controles, politicas y cumplimiento normativo interno.", 30)
    ];

    private static readonly OrgUnitTypeSeed DefaultOrgUnitTypeSeed =
        new("DEPARTAMENTO", "Departamento", "Unidad organizativa base para clasificar puestos sembrados por sistema.", 10);

    private static readonly PositionCategoryClassificationSeed[] ClassificationSeeds =
    [
        new(
            "ADMINISTRATIVO_INDEFINIDO_DEPARTAMENTO",
            "Administrativo indefinido departamental",
            "Clasificacion base para puestos administrativos de caracter permanente a nivel departamental.",
            "ADMINISTRATIVO",
            "INDEFINIDO",
            "DEPARTAMENTO",
            10)
    ];

    private static readonly PositionCategorySeed[] PositionCategorySeeds =
    [
        new("ANALISTA", "Analista", "Categoria base para puestos de analisis y soporte especializado.", "ADMINISTRATIVO_INDEFINIDO_DEPARTAMENTO", 10),
        new("COORDINADOR", "Coordinador", "Categoria base para puestos con coordinacion operativa.", "ADMINISTRATIVO_INDEFINIDO_DEPARTAMENTO", 20),
        new("JEFATURA", "Jefatura", "Categoria base para puestos de supervision y liderazgo de area.", "ADMINISTRATIVO_INDEFINIDO_DEPARTAMENTO", 30)
    ];

    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var tenantIds = await dbContext.Companies
            .AsNoTracking()
            .Select(company => company.PublicId)
            .ToListAsync(cancellationToken);

        if (tenantIds.Count == 0)
        {
            return;
        }

        foreach (var tenantId in tenantIds)
        {
            await EnsureTenantSeededAsync(tenantId, cancellationToken);
        }
    }

    private async Task EnsureTenantSeededAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureSimpleCatalogSeedsAsync(tenantId, PositionFunctionTypeSeeds, cancellationToken);
        await EnsureSimpleCatalogSeedsAsync(tenantId, PositionContractTypeSeeds, cancellationToken);
        await EnsureSimpleCatalogSeedsAsync(tenantId, StrategicObjectiveSeeds, cancellationToken);
        await EnsureSimpleCatalogSeedsAsync(tenantId, WorkEquipmentSeeds, cancellationToken);
        await EnsureSimpleCatalogSeedsAsync(tenantId, ResponsibilitySeeds, cancellationToken);

        var orgUnitTypeId = await EnsureOrgUnitTypeAsync(tenantId, DefaultOrgUnitTypeSeed, cancellationToken);

        var simpleCatalogLookup = await dbContext.PositionDescriptionCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .ToDictionaryAsync(
                item => GetSimpleCatalogLookupKey(item.CatalogType, item.NormalizedCode),
                item => item.Id,
                cancellationToken);

        await EnsureClassificationSeedsAsync(tenantId, orgUnitTypeId, simpleCatalogLookup, cancellationToken);

        var classificationLookup = await dbContext.PositionCategoryClassifications
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .ToDictionaryAsync(item => item.NormalizedCode, item => item.Id, cancellationToken);

        await EnsurePositionCategorySeedsAsync(tenantId, classificationLookup, cancellationToken);
    }

    private async Task EnsureSimpleCatalogSeedsAsync(
        Guid tenantId,
        IReadOnlyCollection<SimpleCatalogSeed> seeds,
        CancellationToken cancellationToken)
    {
        if (seeds.Count == 0)
        {
            return;
        }

        var requestedTypes = seeds
            .Select(static seed => seed.CatalogType)
            .Distinct()
            .ToHashSet();

        var existingItems = await dbContext.PositionDescriptionCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new { item.CatalogType, item.NormalizedCode })
            .ToListAsync(cancellationToken);

        var existingKeys = existingItems
            .Where(item => requestedTypes.Contains(item.CatalogType))
            .Select(item => GetSimpleCatalogLookupKey(item.CatalogType, item.NormalizedCode))
            .ToHashSet();

        var missingItems = seeds
            .Where(seed => !existingKeys.Contains(seed.LookupKey))
            .Select(seed =>
            {
                var entity = PositionDescriptionCatalogItem.Create(
                    seed.CatalogType,
                    seed.Code,
                    seed.Name,
                    seed.Description,
                    seed.SortOrder);
                entity.SetTenantId(tenantId);
                return entity;
            })
            .ToArray();

        if (missingItems.Length == 0)
        {
            return;
        }

        dbContext.PositionDescriptionCatalogItems.AddRange(missingItems);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<long> EnsureOrgUnitTypeAsync(
        Guid tenantId,
        OrgUnitTypeSeed seed,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.OrgUnitTypeCatalogItems
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.NormalizedCode == seed.NormalizedCode,
                cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var entity = OrgUnitTypeCatalogItem.Create(
            seed.Code,
            seed.Name,
            seed.Description,
            seed.SortOrder);
        entity.SetTenantId(tenantId);

        dbContext.OrgUnitTypeCatalogItems.Add(entity);
        _ = await dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    private async Task EnsureClassificationSeedsAsync(
        Guid tenantId,
        long defaultOrgUnitTypeId,
        IReadOnlyDictionary<string, long> simpleCatalogLookup,
        CancellationToken cancellationToken)
    {
        var existingCodes = new HashSet<string>(
            await dbContext.PositionCategoryClassifications
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.NormalizedCode)
                .ToListAsync(cancellationToken));

        var missingItems = new List<PositionCategoryClassification>();
        foreach (var seed in ClassificationSeeds)
        {
            if (existingCodes.Contains(seed.NormalizedCode))
            {
                continue;
            }

            var positionFunctionId = ResolveSimpleCatalogId(
                simpleCatalogLookup,
                PositionDescriptionCatalogType.PositionFunctionType,
                seed.PositionFunctionCode);
            var positionContractId = ResolveSimpleCatalogId(
                simpleCatalogLookup,
                PositionDescriptionCatalogType.PositionContractType,
                seed.PositionContractCode);

            var entity = PositionCategoryClassification.Create(
                seed.Code,
                seed.Name,
                seed.Description,
                positionFunctionId,
                positionContractId,
                defaultOrgUnitTypeId,
                seed.SortOrder);
            entity.SetTenantId(tenantId);
            missingItems.Add(entity);
        }

        if (missingItems.Count == 0)
        {
            return;
        }

        dbContext.PositionCategoryClassifications.AddRange(missingItems);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePositionCategorySeedsAsync(
        Guid tenantId,
        IReadOnlyDictionary<string, long> classificationLookup,
        CancellationToken cancellationToken)
    {
        var existingCodes = new HashSet<string>(
            await dbContext.PositionCategories
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.NormalizedCode)
                .ToListAsync(cancellationToken));

        var missingItems = new List<PositionCategory>();
        foreach (var seed in PositionCategorySeeds)
        {
            if (existingCodes.Contains(seed.NormalizedCode))
            {
                continue;
            }

            if (!classificationLookup.TryGetValue(seed.ClassificationCode.Trim().ToUpperInvariant(), out var classificationId))
            {
                throw new InvalidOperationException(
                    $"Position category classification '{seed.ClassificationCode}' is required before seeding category '{seed.Code}'.");
            }

            var entity = PositionCategory.Create(
                seed.Code,
                seed.Name,
                seed.Description,
                classificationId,
                seed.SortOrder);
            entity.SetTenantId(tenantId);
            missingItems.Add(entity);
        }

        if (missingItems.Count == 0)
        {
            return;
        }

        dbContext.PositionCategories.AddRange(missingItems);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static long ResolveSimpleCatalogId(
        IReadOnlyDictionary<string, long> lookup,
        PositionDescriptionCatalogType catalogType,
        string code)
    {
        var key = GetSimpleCatalogLookupKey(catalogType, code.Trim().ToUpperInvariant());
        if (lookup.TryGetValue(key, out var id))
        {
            return id;
        }

        throw new InvalidOperationException(
            $"Catalog item '{catalogType}:{code}' is required before seeding position category classifications.");
    }

    private static string GetSimpleCatalogLookupKey(PositionDescriptionCatalogType catalogType, string normalizedCode) =>
        $"{catalogType}:{normalizedCode}";

    private sealed record SimpleCatalogSeed(
        PositionDescriptionCatalogType CatalogType,
        string Code,
        string Name,
        string? Description,
        int SortOrder)
    {
        public string LookupKey => GetSimpleCatalogLookupKey(CatalogType, Code.Trim().ToUpperInvariant());
    }

    private sealed record OrgUnitTypeSeed(
        string Code,
        string Name,
        string? Description,
        int SortOrder)
    {
        public string NormalizedCode => Code.Trim().ToUpperInvariant();
    }

    private sealed record PositionCategoryClassificationSeed(
        string Code,
        string Name,
        string? Description,
        string PositionFunctionCode,
        string PositionContractCode,
        string OrgUnitTypeCode,
        int SortOrder)
    {
        public string NormalizedCode => Code.Trim().ToUpperInvariant();
    }

    private sealed record PositionCategorySeed(
        string Code,
        string Name,
        string? Description,
        string ClassificationCode,
        int SortOrder)
    {
        public string NormalizedCode => Code.Trim().ToUpperInvariant();
    }
}
