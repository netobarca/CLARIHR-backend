using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.Abstractions.Payroll;

/// <summary>One eligible plaza of the run's population (REQ-012 §3.4) with its MONTHLYIZED base salary.</summary>
public sealed record PayrollPopulationRow(
    long PersonnelFileId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    Guid AssignedPositionPublicId,
    bool IsPrimary,
    string? CostCenterName,
    decimal? MinimumMonthlyWage,
    decimal MonthlyBaseSalary);

/// <summary>ISSS/AFP rates + MONTHLY cap resolved country-default → instance-override (molde settlement).</summary>
public sealed record PayrollLegalScheme(
    decimal EmployeeRatePercent,
    decimal EmployerRatePercent,
    decimal? MonthlyContributionCap);

/// <summary>A VIGENTE recurring income of the population with its plan carrier (the public scan item).</summary>
public sealed record PayrollRecurringIncomeRow(
    long InternalId,
    Guid PublicId,
    Guid PersonnelFilePublicId,
    Guid AssignedPositionPublicId,
    string ConceptCode,
    string ConceptName,
    RecurringIncomeBatchScanItem Plan);

/// <summary>A VIGENTE recurring deduction of the population with its plan carrier (the public scan item).</summary>
public sealed record PayrollRecurringDeductionRow(
    long InternalId,
    Guid PublicId,
    Guid PersonnelFilePublicId,
    Guid AssignedPositionPublicId,
    string ConceptCode,
    string ConceptName,
    RecurringDeductionBatchScanItem Plan);

/// <summary>
/// A registro-sourced input row (TNT / disciplinary / incapacity — snapshots, the engine takes the amount
/// as-is). <paramref name="IsCarryover"/> marks a lagged TNT/disciplinary record from BEFORE the period not
/// yet consumed by a non-annulled run (REQ-014 P-03).
/// </summary>
public sealed record PayrollRegistroRow(
    Guid RecordPublicId,
    Guid PersonnelFilePublicId,
    string ConceptCode,
    string ConceptName,
    decimal Amount,
    decimal EmployerAmount,
    bool IsCarryover);

/// <summary>
/// Everything the generation needs, resolved raw (the assembler in the handlers turns it into the pure
/// engine's <c>PayrollCalculationInput</c> + the pool application plan). One-time/overtime rows are the
/// public pending-tray records; recurring rows carry the public scan items (the installment derivation
/// happens in the Application layer with the modules' own pure rules — never re-implemented).
/// </summary>
public sealed record PayrollRunSourceData(
    IReadOnlyList<PayrollPopulationRow> Population,
    PayrollLegalScheme Isss,
    PayrollLegalScheme Afp,
    decimal IncafRatePercent,
    IReadOnlyList<PayrollTaxBracket> RentaBrackets,
    IReadOnlyList<PayrollRecurringIncomeRow> RecurringIncomes,
    IReadOnlyList<OneTimeIncomePendingData> OneTimeIncomes,
    IReadOnlyList<OvertimePendingData> OvertimeRecords,
    IReadOnlyList<PayrollRecurringDeductionRow> RecurringDeductions,
    IReadOnlyList<OneTimeDeductionPendingData> OneTimeDeductions,
    IReadOnlyList<PayrollRegistroRow> NotWorkedTimes,
    IReadOnlyList<PayrollRegistroRow> DisciplinaryActions,
    IReadOnlyList<PayrollRegistroRow> Incapacities);

/// <summary>
/// Resolves the raw generation data of a Nómina × period (REQ-012 §3.4, molde
/// <c>LeaveCalculationDataProvider</c>): the population (ACTIVE plazas whose payroll type matches the
/// Nómina, of COMPLETED employees, excluding RETIRADO profiles and employees with an EMITIDA settlement
/// whose retirement date falls in the period) with the plaza's own MONTHLYIZED base (never the settlement's
/// raw figure), the legal schemes, the EFFECTIVE Renta table of the frequency, the 5 pools' candidates and
/// the registro inputs of the range INCLUDING the REQ-014 lagged carryovers.
/// </summary>
public interface IPayrollCalculationDataProvider
{
    Task<PayrollRunSourceData> BuildAsync(
        Guid tenantId,
        PayrollDefinition definition,
        PayrollPeriodDefinition period,
        IReadOnlyCollection<Guid>? employeeIds,
        DateOnly today,
        CancellationToken cancellationToken);
}
