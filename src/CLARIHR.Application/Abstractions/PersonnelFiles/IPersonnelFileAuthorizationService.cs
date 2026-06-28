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

    Error TenantMismatch(RbacPermissionAction action);
}
