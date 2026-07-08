using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>One fund period with its derived balances and the Finanzas provision (D-25).</summary>
public sealed record VacationFundPeriodResponse(
    Guid VacationPeriodPublicId,
    int PeriodYear,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    int LegalDaysGranted,
    int BenefitDaysGranted,
    int TotalDaysGranted,
    bool GeneratesEnjoymentDays,
    bool UsedAnniversary,
    string SourceCode,
    int EnjoyedDays,
    int PendingDays,
    decimal? ProvisionAmount);

/// <summary>
/// The vacation fund detail of one employee (leave module §3.6/§3.10): the per-period granted/enjoyed/pending
/// days plus the financial provision (pending × daily × 1.30, D-25). The daily salary is <c>salary / 30</c>;
/// null when no base salary is resolvable (the provision columns are then null too).
/// </summary>
public sealed record VacationFundResponse(
    Guid PersonnelFilePublicId,
    decimal? DailySalary,
    int TotalGrantedDays,
    int TotalEnjoyedDays,
    int TotalPendingDays,
    decimal? TotalProvisionAmount,
    IReadOnlyCollection<VacationFundPeriodResponse> Periods)
{
    [JsonIgnore]
    public Guid Id => PersonnelFilePublicId;
}

/// <summary>
/// A Finanzas provision export row (D-25): one active enjoyment period per employee, with the granted days
/// split into legal/benefit, the enjoyed and pending days, the daily salary (salary/30) and the provision
/// (pending × daily × 1.30). Property names are the Spanish headers (reflection-driven export writer).
/// </summary>
public sealed record FondoProvisionExportRow(
    string Empleado,
    string? Codigo,
    int Periodo,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    int DiasLey,
    int DiasBeneficio,
    int DiasGozados,
    int DiasPendientes,
    decimal SalarioDiario,
    decimal Provision);

public sealed record GetPersonnelFileVacationFundQuery(Guid PersonnelFileId)
    : IQuery<VacationFundResponse>;

public sealed record ExportVacationFundQuery(Guid CompanyId, int? Year, int? MaxRows)
    : IQuery<IReadOnlyCollection<FondoProvisionExportRow>>;

/// <summary>
/// Shared vacation fund arithmetic. The provision multiplier (1.30) folds the statutory 30% vacation premium
/// (Art. 177) into the financial reserve — provision = pending × daily × 1.30 (D-25).
/// </summary>
public static class VacationFundMath
{
    /// <summary>Provision multiplier (D-25): pending × daily × 1.30 (base value + 30% vacation premium).</summary>
    public const decimal ProvisionMultiplier = 1.30m;

    private static readonly JsonSerializerOptions DistributionOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Daily salary convention (D-21): monthly base over 30, half-up to 2 decimals. Null when no salary.</summary>
    public static decimal? DailySalary(decimal? monthlyBaseSalary) =>
        monthlyBaseSalary is { } salary ? Math.Round(salary / 30m, 2, MidpointRounding.AwayFromZero) : null;

    /// <summary>Provision (D-25): pending × daily × 1.30, half-up to 2 decimals. Null when no daily salary.</summary>
    public static decimal? Provision(int pendingDays, decimal? dailySalary) =>
        dailySalary is { } daily ? Math.Round(pendingDays * daily * ProvisionMultiplier, 2, MidpointRounding.AwayFromZero) : null;

    /// <summary>
    /// Net consumed days per period across a file's fund-consuming requests: Σ allocations − Σ returns, keyed by
    /// period internal id. <paramref name="allocations"/> and <paramref name="returnDistributions"/> are already
    /// filtered to requests in a fund-consuming state (<see cref="VacationRequestStatuses.ConsumesFund"/>).
    /// </summary>
    public static IReadOnlyDictionary<long, int> NetConsumedByPeriod(
        IEnumerable<(long PeriodId, int Days)> allocations,
        IEnumerable<string> returnDistributions)
    {
        var net = new Dictionary<long, int>();
        foreach (var (periodId, days) in allocations)
        {
            net[periodId] = net.GetValueOrDefault(periodId) + days;
        }

        foreach (var distributionJson in returnDistributions)
        {
            foreach (var entry in Deserialize(distributionJson))
            {
                net[entry.VacationPeriodId] = net.GetValueOrDefault(entry.VacationPeriodId) - entry.Days;
            }
        }

        return net;
    }

    private static IReadOnlyList<VacationReturnDistributionInput> Deserialize(string? distributionJson)
    {
        if (string.IsNullOrWhiteSpace(distributionJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<VacationReturnDistributionInput>>(distributionJson, DistributionOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

internal sealed class GetPersonnelFileVacationFundQueryValidator : AbstractValidator<GetPersonnelFileVacationFundQuery>
{
    public GetPersonnelFileVacationFundQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class ExportVacationFundQueryValidator : AbstractValidator<ExportVacationFundQuery>
{
    public ExportVacationFundQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Year).InclusiveBetween(2000, 2100).When(query => query.Year.HasValue);
    }
}
