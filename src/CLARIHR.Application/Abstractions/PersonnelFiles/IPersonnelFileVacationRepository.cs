using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>
/// One active vacation period of an employee together with its net consumed days (Σ allocations − Σ returns of
/// the fund-consuming requests). Feeds the fund detail, the profile balance and the consumption guards.
/// </summary>
public sealed record VacationPeriodConsumptionRow(
    long PeriodId,
    Guid PublicId,
    int PeriodYear,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    int LegalDaysGranted,
    int BenefitDaysGranted,
    bool GeneratesEnjoymentDays,
    bool UsedAnniversary,
    string SourceCode,
    int NetConsumedDays);

/// <summary>One active employee considered by the mass generation: anchor date = primary-plaza start or hire date.</summary>
public sealed record VacationGenerationCandidate(
    long PersonnelFileId,
    Guid PublicId,
    string FullName,
    string? EmployeeCode,
    DateOnly AnchorDate);

/// <summary>
/// Persistence port of the vacation fund vertical (leave module PR-7): period CRUD reads/writes, the derived
/// consumption used by the fund detail / profile balance / edit-delete guards, the per-employee base-salary
/// resolution (salary/30 daily convention), the Finanzas provision export and the mass-generation candidate
/// scans. The request/plan writes belong to PR-8/PR-9 (the tables already ship with M4).
/// </summary>
public interface IPersonnelFileVacationRepository
{
    // ── Period reads/writes ───────────────────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<PersonnelFileVacationPeriodResponse>> GetPeriodResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken);

    Task<PersonnelFileVacationPeriodResponse?> GetPeriodResponseAsync(
        Guid personnelFilePublicId, Guid vacationPeriodPublicId, CancellationToken cancellationToken);

    /// <summary>Tracked entity for the domain guards to mutate (edit grants / soft delete).</summary>
    Task<PersonnelFileVacationPeriod?> GetPeriodEntityAsync(
        Guid personnelFilePublicId, Guid vacationPeriodPublicId, CancellationToken cancellationToken);

    /// <summary>True when an active period already exists for the employee-year (RN-19 duplicate guard).</summary>
    Task<bool> HasActivePeriodForYearAsync(
        long personnelFileId, int year, long? excludePeriodId, CancellationToken cancellationToken);

    /// <summary>True when a fund-consuming request drew days from the period (RF-016 edit/delete guard).</summary>
    Task<bool> HasConsumptionAsync(long vacationPeriodId, CancellationToken cancellationToken);

    void AddPeriod(PersonnelFileVacationPeriod entity);

    /// <summary>
    /// The anchor date for the period bounds / eligibility of one employee: the primary-plaza start (IsPrimary
    /// among active assignments, oldest StartDate when none) or the hire date when there is no active plaza.
    /// Null when neither is resolvable.
    /// </summary>
    Task<DateOnly?> GetAnchorDateAsync(long personnelFileId, CancellationToken cancellationToken);

    // ── Fund detail / balance ─────────────────────────────────────────────────────────────────────

    /// <summary>Active periods of the employee with their net consumed days (allocations − returns).</summary>
    Task<IReadOnlyCollection<VacationPeriodConsumptionRow>> GetActivePeriodConsumptionsAsync(
        long personnelFileId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the monthly base salary of the employee's primary plaza (settlement/leave data-provider
    /// criterion). Null when no base salary is resolvable. Used for the daily-salary provision (salary/30).
    /// </summary>
    Task<decimal?> GetMonthlyBaseSalaryAsync(
        Guid tenantId, long personnelFileId, CancellationToken cancellationToken);

    // ── Provision export (Finanzas, D-25) ─────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<FondoProvisionExportRow>> GetFundProvisionRowsAsync(
        Guid tenantId, int? year, int? maxRows, CancellationToken cancellationToken);

    // ── Mass generation ───────────────────────────────────────────────────────────────────────────

    /// <summary>Active (non-retired) completed employees of the tenant, optionally restricted to a filter set.</summary>
    Task<IReadOnlyCollection<VacationGenerationCandidate>> GetGenerationCandidatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid>? employeeFilter, CancellationToken cancellationToken);

    /// <summary>The personnel file ids that already have an active period for the year (idempotency scan).</summary>
    Task<IReadOnlySet<long>> GetPersonnelFileIdsWithActivePeriodForYearAsync(
        Guid tenantId, int year, CancellationToken cancellationToken);
}
