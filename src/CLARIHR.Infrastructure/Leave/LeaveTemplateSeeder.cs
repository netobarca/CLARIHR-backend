using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

/// <summary>
/// El Salvador leave-configuration template (Anexo A.2 risks + D-08 types + Anexo A.3 holidays).
/// Mirrors <see cref="CompetencyFramework.CompetencyFrameworkSeedService"/>: guarded existence
/// checks make it idempotent so it is safe to run on every provisioning and on every
/// <c>load-template</c> call. The guards key on the tenant's <c>NormalizedCode</c> (risks/types)
/// or <c>Date</c> (holidays): an existing row is skipped even when it was edited or inactivated,
/// so the seeder never overwrites tenant edits — it only creates the missing template rows.
/// </summary>
internal sealed class LeaveTemplateSeeder(
    ApplicationDbContext dbContext,
    AmbientTenantContext ambientTenantContext) : ILeaveTemplateSeeder
{
    public async Task<LeaveTemplateSeedResult> ApplyTemplateAsync(
        Guid tenantId,
        int? holidayYear,
        CancellationToken cancellationToken)
    {
        // Push the ambient tenant so the fail-closed global query filter scopes the idempotency
        // guards below to this tenant in every call path (the provisioning hook runs without an
        // HTTP tenant claim). Mirrors CompetencyFrameworkSeedService.EnsureSeededAsync.
        using var tenantScope = ambientTenantContext.Push(tenantId);

        var (risksCreated, riskParametersCreated, risksSkipped) =
            await ApplyRiskTemplatesAsync(tenantId, cancellationToken);
        var (typesCreated, typesSkipped) = await ApplyTypeTemplatesAsync(tenantId, cancellationToken);
        var (holidaysCreated, holidaysSkipped) = holidayYear is { } year
            ? await ApplyHolidayTemplatesAsync(tenantId, year, cancellationToken)
            : (0, 0);

        if (risksCreated + typesCreated + holidaysCreated > 0)
        {
            _ = await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new LeaveTemplateSeedResult(
            risksCreated,
            riskParametersCreated,
            typesCreated,
            holidaysCreated,
            risksSkipped,
            typesSkipped,
            holidaysSkipped);
    }

    private async Task<(int Created, int ParametersCreated, int Skipped)> ApplyRiskTemplatesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.IncapacityRisks
                .AsNoTracking()
                .Where(risk => risk.TenantId == tenantId)
                .Select(risk => risk.NormalizedCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var parametersCreated = 0;
        var skipped = 0;

        foreach (var template in RiskTemplates)
        {
            if (existingCodes.Contains(template.Code))
            {
                skipped++;
                continue;
            }

            var risk = IncapacityRisk.Create(
                template.Code,
                template.Name,
                template.CountsSeventhDay,
                template.CountsSaturday,
                template.CountsHoliday,
                template.UsesWorkSchedule,
                template.AllowsIndefinite,
                template.AllowsExtension,
                template.UsesFund,
                template.HasSubsidy);
            risk.ReplaceParameters(template.Parameters);
            risk.SetTenantId(tenantId);
            foreach (var parameter in risk.Parameters)
            {
                parameter.SetTenantId(tenantId);
            }

            dbContext.IncapacityRisks.Add(risk);
            created++;
            parametersCreated += risk.Parameters.Count;
        }

        return (created, parametersCreated, skipped);
    }

    private async Task<(int Created, int Skipped)> ApplyTypeTemplatesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.IncapacityTypes
                .AsNoTracking()
                .Where(type => type.TenantId == tenantId)
                .Select(type => type.NormalizedCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;

        foreach (var template in TypeTemplates)
        {
            if (existingCodes.Contains(template.Code))
            {
                skipped++;
                continue;
            }

            var incapacityType = IncapacityType.Create(
                template.Code,
                template.Name,
                deductionTypeText: null,
                incomeTypeText: null,
                template.AppliesToWorkAccident);
            incapacityType.SetTenantId(tenantId);

            dbContext.IncapacityTypes.Add(incapacityType);
            created++;
        }

        return (created, skipped);
    }

    private async Task<(int Created, int Skipped)> ApplyHolidayTemplatesAsync(
        Guid tenantId,
        int year,
        CancellationToken cancellationToken)
    {
        var templates = BuildHolidayTemplates(year);
        var templateDates = templates.Select(static template => template.Date).ToArray();
        var existingDates = (await dbContext.CompanyHolidays
                .AsNoTracking()
                .Where(holiday => holiday.TenantId == tenantId && templateDates.Contains(holiday.Date))
                .Select(holiday => holiday.Date)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var created = 0;
        var skipped = 0;

        foreach (var (date, description) in templates)
        {
            if (existingDates.Contains(date))
            {
                skipped++;
                continue;
            }

            var holiday = CompanyHoliday.Create(date, description, CompanyHolidayScopes.Nacional);
            holiday.SetTenantId(tenantId);

            dbContext.CompanyHolidays.Add(holiday);
            created++;
        }

        return (created, skipped);
    }

    // ---------------------------------------------------------------------------------------
    // Template data — El Salvador (ratified Anexo A.2 / D-08 / Anexo A.3).
    // ---------------------------------------------------------------------------------------

    private static readonly IncapacityRiskParameterInput[] CommonSubsidyTranches =
    [
        new(DayFrom: 1, DayTo: 3, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Empresa),
        new(DayFrom: 4, DayTo: null, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Isss),
    ];

    private static readonly IncapacityRiskParameterInput[] OccupationalSubsidyTranches =
    [
        new(DayFrom: 1, DayTo: null, SubsidyPercent: 100m, PayerCode: IncapacityPayerCodes.Isss),
    ];

    private static readonly IncapacityRiskParameterInput[] MaternitySubsidyTranches =
    [
        new(DayFrom: 1, DayTo: 112, SubsidyPercent: 100m, PayerCode: IncapacityPayerCodes.Isss),
    ];

    private static readonly RiskTemplate[] RiskTemplates =
    [
        new(
            "ENFERMEDAD_COMUN",
            "Enfermedad común",
            CountsSeventhDay: true,
            CountsSaturday: true,
            CountsHoliday: true,
            UsesWorkSchedule: false,
            AllowsIndefinite: false,
            AllowsExtension: true,
            UsesFund: true,
            HasSubsidy: true,
            CommonSubsidyTranches),
        new(
            "ACCIDENTE_COMUN",
            "Accidente común",
            CountsSeventhDay: true,
            CountsSaturday: true,
            CountsHoliday: true,
            UsesWorkSchedule: false,
            AllowsIndefinite: false,
            AllowsExtension: true,
            UsesFund: true,
            HasSubsidy: true,
            CommonSubsidyTranches),
        new(
            "ACCIDENTE_TRABAJO",
            "Accidente de trabajo",
            CountsSeventhDay: true,
            CountsSaturday: true,
            CountsHoliday: true,
            UsesWorkSchedule: false,
            AllowsIndefinite: false,
            AllowsExtension: true,
            UsesFund: false,
            HasSubsidy: true,
            OccupationalSubsidyTranches),
        new(
            "ENFERMEDAD_PROFESIONAL",
            "Enfermedad profesional",
            CountsSeventhDay: true,
            CountsSaturday: true,
            CountsHoliday: true,
            UsesWorkSchedule: false,
            AllowsIndefinite: false,
            AllowsExtension: true,
            UsesFund: false,
            HasSubsidy: true,
            OccupationalSubsidyTranches),
        new(
            "MATERNIDAD",
            "Maternidad",
            CountsSeventhDay: true,
            CountsSaturday: true,
            CountsHoliday: true,
            UsesWorkSchedule: false,
            AllowsIndefinite: false,
            AllowsExtension: false,
            UsesFund: false,
            HasSubsidy: true,
            MaternitySubsidyTranches),
    ];

    private static readonly TypeTemplate[] TypeTemplates =
    [
        new("ENFERMEDAD", "Enfermedad", AppliesToWorkAccident: false),
        new("ACCIDENTE_COMUN", "Accidente común", AppliesToWorkAccident: false),
        new("ACCIDENTE_TRABAJO", "Accidente de trabajo", AppliesToWorkAccident: true),
        new("ENFERMEDAD_PROFESIONAL", "Enfermedad profesional", AppliesToWorkAccident: true),
        new("MATERNIDAD", "Maternidad", AppliesToWorkAccident: false),
        new("LACTANCIA", "Lactancia", AppliesToWorkAccident: false),
    ];

    /// <summary>
    /// National holidays of Art. 190 CT (Anexo A.3) for the given year. The local 3/5-Aug
    /// Fiestas Agostinas days are deliberately NOT seeded: they are LOCAL-scope and each company
    /// adds its own. Semana Santa is derived from Easter Sunday (Thursday = −3, Friday = −2,
    /// Saturday = −1).
    /// </summary>
    private static IReadOnlyList<(DateOnly Date, string Description)> BuildHolidayTemplates(int year)
    {
        var easterSunday = CalculateEasterSunday(year);

        return
        [
            (new DateOnly(year, 1, 1), "Año Nuevo"),
            (easterSunday.AddDays(-3), "Jueves Santo"),
            (easterSunday.AddDays(-2), "Viernes Santo"),
            (easterSunday.AddDays(-1), "Sábado Santo"),
            (new DateOnly(year, 5, 1), "Día del Trabajo"),
            (new DateOnly(year, 5, 10), "Día de la Madre"),
            (new DateOnly(year, 6, 17), "Día del Padre"),
            (new DateOnly(year, 8, 6), "Fiestas Agostinas (día principal)"),
            (new DateOnly(year, 9, 15), "Día de la Independencia"),
            (new DateOnly(year, 11, 2), "Día de los Difuntos"),
            (new DateOnly(year, 12, 25), "Navidad"),
        ];
    }

    /// <summary>
    /// Gregorian Easter Sunday via the Anonymous Gregorian ("Gauss/Meeus–Jones–Butcher")
    /// computus. Valid for every Gregorian year (e.g. 2026 → April 5).
    /// </summary>
    private static DateOnly CalculateEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(year, month, day);
    }

    private sealed record RiskTemplate(
        string Code,
        string Name,
        bool CountsSeventhDay,
        bool CountsSaturday,
        bool CountsHoliday,
        bool UsesWorkSchedule,
        bool AllowsIndefinite,
        bool AllowsExtension,
        bool UsesFund,
        bool HasSubsidy,
        IReadOnlyList<IncapacityRiskParameterInput> Parameters);

    private sealed record TypeTemplate(string Code, string Name, bool AppliesToWorkAccident);
}
