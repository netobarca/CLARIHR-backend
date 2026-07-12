using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

public interface IPersonnelFileAuthorizationService
{
    Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the caller may authorize the rehire of a "not rehireable" file (D-10). Requires
    /// the dedicated <c>PersonnelFiles.AuthorizeRehire</c> permission (or the IAM super-admin),
    /// deliberately NOT implied by <c>PersonnelFiles.Admin</c>, so a manager who can run rehires
    /// cannot also approve the override of a blocked file.
    /// </summary>
    Task<bool> HasRehireAuthorizationAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// Role gate for reading compensation (salary/ingresos/egresos) of any file in the tenant (D-16):
    /// the dedicated <c>PersonnelFiles.ViewCompensation</c> permission, or Admin / IAM super-admin.
    /// (Employees reading their OWN compensation are allowed by a separate self-service check.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewCompensationAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for authorization substitutions (D-09): the dedicated
    /// <c>PersonnelFiles.ManageSubstitutions</c> permission, or Admin / IAM super-admin. Separate from the
    /// generic manage gate so a non-Admin HR analyst can be granted substitution management on its own.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageSubstitutionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Role gate for reading insurance data (insurances + beneficiaries, PII + insured amounts):
    /// the dedicated <c>PersonnelFiles.ViewInsurance</c> permission, or Admin / IAM super-admin.
    /// No self-service in this phase.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewInsuranceAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Role gate for reading medical claims (diagnosis = special-category health data, D-08): the dedicated
    /// <c>PersonnelFiles.ViewMedicalClaims</c> permission, or Admin / IAM super-admin. (Employees reading their
    /// OWN claims are allowed by a separate self-service check — D-09.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewMedicalClaimsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for medical claims (D-08): the dedicated <c>PersonnelFiles.ManageMedicalClaims</c> permission,
    /// or Admin / IAM super-admin. (Employees registering their OWN claims are allowed by a separate
    /// self-service check — D-09.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageMedicalClaimsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Role gate for reading position-competency results (evaluation scores/gaps): the dedicated
    /// <c>PersonnelFiles.ViewCompetencies</c> permission, or Admin / IAM super-admin. (Employees reading their
    /// OWN competencies are allowed by a separate self-service check — D-09.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewCompetenciesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for position-competency results (D-08): the dedicated <c>PersonnelFiles.ManageCompetencies</c>
    /// permission, or Admin / IAM super-admin. Writes are HR-only (CLARIHR is the source of truth — D-01).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageCompetenciesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Role gate for reading off-payroll transactions (sensitive expense amounts, D-06): the dedicated
    /// <c>PersonnelFiles.ViewOffPayrollTransactions</c> permission, or Admin / IAM super-admin. No self-service.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewOffPayrollTransactionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for off-payroll transactions (D-06): the dedicated
    /// <c>PersonnelFiles.ManageOffPayrollTransactions</c> permission, or Admin / IAM super-admin. HR-only — no
    /// self-service (the employee never writes these).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageOffPayrollTransactionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Role gate for reading economic-aid requests (the emergency reason is sensitive, D-10): the dedicated
    /// <c>PersonnelFiles.ViewEconomicAidRequests</c> permission, or Admin / IAM super-admin. (Employees reading
    /// their OWN requests are allowed by a separate self-service check — D-02.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewEconomicAidRequestsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for economic-aid requests (D-03): the dedicated <c>PersonnelFiles.ManageEconomicAidRequests</c>
    /// permission, or Admin / IAM super-admin. Validation (approve/reject)/disburse/edit/delete are HR-only.
    /// (Employees creating/cancelling their OWN pending requests are allowed by a separate self-service check —
    /// D-02/D-11; validation is never self-service — no self-approval, D-03.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageEconomicAidRequestsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Role gate for reading certificate requests ("constancias"): the dedicated
    /// <c>PersonnelFiles.ViewCertificateRequests</c> permission, or Admin / IAM super-admin. (Employees reading
    /// their OWN requests are allowed by a separate self-service check — D-02.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewCertificateRequestsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for certificate requests (D-04): the dedicated <c>PersonnelFiles.ManageCertificateRequests</c>
    /// permission, or Admin / IAM super-admin. Process/issue/deliver/reject/edit/delete are HR-only. (Employees
    /// creating/cancelling their OWN pending requests are allowed by a separate self-service check — D-02.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageCertificateRequestsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for the exit-interview form builder (D-01): the dedicated
    /// <c>PersonnelFiles.ManageExitInterviewForms</c> permission, or Admin / IAM super-admin. HR-only —
    /// designing/publishing/associating exit-interview forms is not self-service.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageExitInterviewFormsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for exit-interview submissions (D-14, RRHH-only): the dedicated
    /// <c>PersonnelFiles.ViewExitInterviews</c> permission, or Admin / IAM super-admin.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewExitInterviewsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for exit-interview submissions (D-04): the dedicated
    /// <c>PersonnelFiles.ManageExitInterviews</c> permission, or Admin / IAM super-admin. (Employees filling
    /// their OWN interview are allowed by a separate self-service check.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageExitInterviewsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for the HR analytics dashboard: the dedicated <c>PersonnelFiles.ViewReports</c> permission,
    /// or <c>PersonnelFiles.Read</c> / Admin / IAM super-admin (D-09 — a personnel-file reader may also see the
    /// dashboards). Read-only aggregate data; no per-employee sensitive fields.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewReportsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for retirement requests (D-12, RRHH-only — no self-service in Fase 1): the dedicated
    /// <c>PersonnelFiles.ViewRetirements</c> permission, or Admin / IAM super-admin.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewRetirementsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for retirement requests (register/edit/cancel/execute — D-12): the dedicated
    /// <c>PersonnelFiles.ManageRetirements</c> permission, or Admin / IAM super-admin. Authorization and
    /// reversal are NOT covered — see the dedicated gates below.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageRetirementsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Gate for authorizing/rejecting a retirement request (and annulling an authorized one): the dedicated
    /// <c>PersonnelFiles.AuthorizeRetirement</c> permission or IAM super-admin. Like the rehire override,
    /// <c>PersonnelFiles.Admin</c> is deliberately EXCLUDED (D-12/D-13 — separation of duties).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanAuthorizeRetirementAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Gate for reverting an executed retirement: the dedicated <c>PersonnelFiles.RevertRetirement</c>
    /// permission or IAM super-admin. <c>PersonnelFiles.Admin</c> is deliberately EXCLUDED (D-12).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanRevertRetirementAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for settlements (settlement module D-20, HR-only — settlement data exposes salaries;
    /// no self-service in Fase 1): the dedicated <c>PersonnelFiles.ViewSettlements</c> permission, or
    /// Admin / IAM super-admin.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewSettlementsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for settlements (create/edit/issue/annul + scenarios — settlement module D-20): the
    /// dedicated <c>PersonnelFiles.ManageSettlements</c> permission, or Admin / IAM super-admin.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageSettlementsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for the retirement interview tray (RN-008.1): the exit-interview reader permission
    /// (<c>ViewExitInterviews</c>) OR the retirement reader permission (<c>ViewRetirements</c>), or
    /// Admin / IAM super-admin. Reading the interview ANSWERS remains governed by the exit-interview
    /// module (D-14 of the interview analysis).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewRetirementInterviewTrayAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for incapacities and lactation (leave module D-17): the dedicated
    /// <c>PersonnelFiles.ViewIncapacities</c> permission, or Admin / IAM super-admin. The self-service
    /// branch (employee reading their OWN incapacities) is resolved by the handler bases, not here.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewIncapacitiesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for incapacities and lactation (leave module D-17): the dedicated
    /// <c>PersonnelFiles.ManageIncapacities</c> permission, or Admin / IAM super-admin. The self-service
    /// create branch (D-18) is resolved by the handler bases, not here.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageIncapacitiesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for the vacation fund, requests, calendar and annual plan (leave module D-17): the
    /// dedicated <c>PersonnelFiles.ViewVacations</c> permission, or Admin / IAM super-admin. The
    /// self-service branch (employee reading their OWN fund/requests) is resolved by the handler bases.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewVacationsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for the vacation fund (generation), request decision/return and the annual plan
    /// (leave module D-17): the dedicated <c>PersonnelFiles.ManageVacations</c> permission, or
    /// Admin / IAM super-admin. The self-service create/cancel branch (D-18) is resolved by the
    /// handler bases, not here.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageVacationsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for compensatory time (REQ-002 D-13): the dedicated
    /// <c>PersonnelFiles.ViewCompensatoryTime</c> permission, or Admin / IAM super-admin. The
    /// self-service branch (employee reading their OWN fund/statement) is resolved by the handler bases
    /// (PR-3/PR-4), not here.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewCompensatoryTimeAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for compensatory time (REQ-002 D-01/D-13): the dedicated
    /// <c>PersonnelFiles.ManageCompensatoryTime</c> permission, or Admin / IAM super-admin. HR-only in
    /// Fase 1 (no self-service write).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageCompensatoryTimeAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for recognitions (REQ-003 D-05): the dedicated <c>PersonnelFiles.ViewRecognitions</c>
    /// permission, or Admin / IAM super-admin. The self-service branch (employee reading their OWN
    /// applied recognitions) is resolved by the handler bases (PR-3), not here.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewRecognitionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for recognitions (REQ-003 D-05): the dedicated
    /// <c>PersonnelFiles.ManageRecognitions</c> permission, or Admin / IAM super-admin (HR-only).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageRecognitionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Decision/revocation gate for recognitions (REQ-003 D-05): the dedicated
    /// <c>PersonnelFiles.AuthorizeRecognitions</c> permission, or IAM super-admin — <c>Admin</c> is
    /// deliberately excluded (separation of duties, mirrors AuthorizeRetirement).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanAuthorizeRecognitionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for disciplinary actions (REQ-003 D-05): the dedicated
    /// <c>PersonnelFiles.ViewDisciplinaryActions</c> permission, or Admin / IAM super-admin. The
    /// self-service branch (employee reading their OWN applied disciplinary actions) is resolved by the
    /// handler bases (PR-4), not here.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewDisciplinaryActionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for disciplinary actions (REQ-003 D-05): the dedicated
    /// <c>PersonnelFiles.ManageDisciplinaryActions</c> permission, or Admin / IAM super-admin (HR-only).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageDisciplinaryActionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Decision/revocation gate for disciplinary actions (REQ-003 D-05): the dedicated
    /// <c>PersonnelFiles.AuthorizeDisciplinaryActions</c> permission, or IAM super-admin — <c>Admin</c>
    /// is deliberately excluded (separation of duties, mirrors AuthorizeRetirement).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanAuthorizeDisciplinaryActionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for the time-availability query (REQ-003 D-14): the dedicated
    /// <c>PersonnelFiles.ViewTimeAvailability</c> permission, or Admin / IAM super-admin. Corporate read
    /// with no self-service.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewTimeAvailabilityAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for recurring incomes (REQ-005 D-06/P-14): the dedicated
    /// <c>PersonnelFiles.ViewRecurringIncomes</c> permission, or Admin / IAM super-admin. HR-only, no
    /// self-service in Fase 1 (P-11).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewRecurringIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for recurring incomes (register/edit/suspend/close/annul + installments — REQ-005
    /// D-06/P-14): the dedicated <c>PersonnelFiles.ManageRecurringIncomes</c> permission, or Admin / IAM
    /// super-admin (HR-only).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageRecurringIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Decision/revocation gate for recurring incomes (REQ-005 D-06/P-14): the dedicated
    /// <c>PersonnelFiles.AuthorizeRecurringIncomes</c> permission, or IAM super-admin — <c>Admin</c> is
    /// deliberately excluded (separation of duties, mirrors AuthorizeRetirement).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanAuthorizeRecurringIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>Read gate for one-time deductions (REQ-009): the dedicated
    /// <c>PersonnelFiles.ViewOneTimeDeductions</c> permission, or Admin / IAM super-admin. HR-only.</summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewOneTimeDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>Write gate for one-time deductions (register/edit/annul + apply/reverse — REQ-009): the
    /// dedicated <c>PersonnelFiles.ManageOneTimeDeductions</c> permission, or Admin / IAM super-admin.</summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageOneTimeDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>Decision/revocation gate for one-time deductions (REQ-009): the dedicated
    /// <c>PersonnelFiles.AuthorizeOneTimeDeductions</c> permission, or IAM super-admin — <c>Admin</c> is
    /// deliberately excluded (separation of duties).</summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanAuthorizeOneTimeDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for recurring deductions (REQ-008 D-06): the dedicated
    /// <c>PersonnelFiles.ViewRecurringDeductions</c> permission, or Admin / IAM super-admin. HR-only, no
    /// self-service in Fase 1.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewRecurringDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for recurring deductions (register/edit/suspend/close/annul + regular and extraordinary
    /// installments — REQ-008 D-06): the dedicated <c>PersonnelFiles.ManageRecurringDeductions</c>
    /// permission, or Admin / IAM super-admin (HR-only).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageRecurringDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for the indebtedness query and simulation (REQ-010 P-15): the dedicated
    /// <c>PersonnelFiles.ViewIndebtedness</c> permission, or Admin / IAM super-admin. No self-service in Fase 1.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewIndebtednessAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for the company's indebtedness parameters (REQ-010 D-16): the dedicated
    /// <c>PersonnelFiles.ManageIndebtednessParameters</c> permission, or Admin / IAM super-admin.
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageIndebtednessParametersAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>Read gate for not-worked time (REQ-011).</summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewNotWorkedTimesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>Write gate for the not-worked-time records (REQ-011).</summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageNotWorkedTimesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>Write gate for the not-worked-time TYPE master (REQ-011 D-18).</summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageNotWorkedTimeTypesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Decision/revocation gate for recurring deductions (REQ-008 D-06): the dedicated
    /// <c>PersonnelFiles.AuthorizeRecurringDeductions</c> permission, or IAM super-admin — <c>Admin</c> is
    /// deliberately excluded (separation of duties, mirrors AuthorizeRecurringIncomes).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanAuthorizeRecurringDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for one-time incomes (REQ-006 P-01): the dedicated
    /// <c>PersonnelFiles.ViewOneTimeIncomes</c> permission, or Admin / IAM super-admin. HR-only, no
    /// self-service in Fase 1 (P-11).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewOneTimeIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for one-time incomes (register/edit/annul + apply-by-period — REQ-006 P-01): the dedicated
    /// <c>PersonnelFiles.ManageOneTimeIncomes</c> permission, or Admin / IAM super-admin (HR-only).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageOneTimeIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Decision/revocation gate for one-time incomes (REQ-006 P-01/P-02): the dedicated
    /// <c>PersonnelFiles.AuthorizeOneTimeIncomes</c> permission, or IAM super-admin — <c>Admin</c> is
    /// deliberately excluded (separation of duties, mirrors AuthorizeRetirement).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanAuthorizeOneTimeIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Read gate for overtime records (REQ-007 P-01/P-11/P-12): the dedicated
    /// <c>PersonnelFiles.ViewOvertimeRecords</c> permission, or Admin / IAM super-admin. Also the READ gate
    /// of the two overtime-configuration masters. (Employees reading their OWN records are allowed by a
    /// separate self-service check in the record handlers.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewOvertimeRecordsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Write gate for overtime records (register/edit/annul + apply-by-period — REQ-007 P-01/P-07): the
    /// dedicated <c>PersonnelFiles.ManageOvertimeRecords</c> permission, or Admin / IAM super-admin. Also the
    /// MANAGE gate of the two overtime-configuration masters (types/justification types + load-template).
    /// (Employees acting on their OWN EN_REVISION record under the self-service preference are allowed by a
    /// separate self-service check in the record handlers.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanManageOvertimeRecordsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    /// <summary>
    /// Decision/revocation gate for overtime records (REQ-007 P-01/P-06): the dedicated
    /// <c>PersonnelFiles.AuthorizeOvertimeRecords</c> permission, or IAM super-admin — <c>Admin</c> is
    /// deliberately excluded (separation of duties + triple anti-self, mirrors AuthorizeRetirement).
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanAuthorizeOvertimeRecordsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    Error TenantMismatch(RbacPermissionAction action);
}
