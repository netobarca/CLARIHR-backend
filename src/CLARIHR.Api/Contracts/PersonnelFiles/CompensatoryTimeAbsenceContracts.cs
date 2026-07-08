namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for registering a compensatory-time absence ("ausencia / goce de tiempo compensatorio"). The type travels
/// as a public id (a DEBITA or AMBAS operation is required). The debited hours are re-verified against the fund
/// balance under an advisory lock (the balance can never go negative — R-T1). <c>payrollPeriodPublicId</c> is an
/// optional imputation to a payroll period of the company master (a reference, not a containment — P-14).
/// </summary>
public sealed record AddCompensatoryTimeAbsenceRequest(
    Guid CompensatoryTimeTypePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal HoursDebited,
    string Reason,
    Guid? PayrollPeriodPublicId,
    string? Notes);

/// <summary>Body for editing a compensatory-time absence's business fields (HR).</summary>
public sealed record UpdateCompensatoryTimeAbsenceRequest(
    Guid CompensatoryTimeTypePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal HoursDebited,
    string Reason,
    Guid? PayrollPeriodPublicId,
    string? Notes);

/// <summary>Body for annulling a compensatory-time absence; the reason is mandatory. Annulling restores the debited hours to the fund.</summary>
public sealed record AnnulCompensatoryTimeAbsenceRequest(string Reason);
