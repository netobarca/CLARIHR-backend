namespace CLARIHR.Application.Features.PersonnelFiles.Common;

/// <summary>
/// Declarative authorization policy names for the Personnel Files shell controller,
/// assigned per HTTP verb by <c>AuthorizationPolicyConvention</c> via
/// <c>[AuthorizationPolicySet(Read, Manage)]</c> as defense-in-depth on top of the
/// class-level <c>[Authorize]</c>. The policies registered under these names in
/// <c>Program.cs</c> are kept a <b>superset</b> of the precise
/// <see cref="CLARIHR.Application.Abstractions.PersonnelFiles.IPersonnelFileAuthorizationService"/>
/// handler gate (<c>EnsureCanReadAsync</c> / <c>EnsureCanManageAsync</c>), so a legitimate
/// reader/manager is never falsely 403'd. The handler remains the precise gate for
/// tenant / entitlement / membership.
/// </summary>
public static class PersonnelFilePolicies
{
    public const string Read = "PersonnelFiles.Read";
    public const string Manage = "PersonnelFiles.Manage";

    /// <summary>
    /// Read policy for compensation sub-resources (D-16). Superset gate: the precise self-service /
    /// role check lives in the compensation read handlers.
    /// </summary>
    public const string ViewCompensation = "PersonnelFiles.ViewCompensation";

    /// <summary>
    /// Write policy for authorization substitutions (D-09): the dedicated
    /// <c>PersonnelFiles.ManageSubstitutions</c> permission, or Admin / IAM super-admin. Assigned to the
    /// write verbs of <c>PersonnelFileAuthorizationSubstitutionController</c>; reads use <see cref="Read"/>.
    /// Kept a superset of the precise <c>EnsureCanManageSubstitutionsAsync</c> handler gate so a legitimate
    /// manager is never falsely 403'd.
    /// </summary>
    public const string ManageSubstitutions = "PersonnelFiles.ManageSubstitutions";

    /// <summary>
    /// Read policy for insurance sub-resources. Authn-only superset: the precise role check
    /// (ViewInsurance / Admin) lives in the insurance read handlers (no self-service in this phase).
    /// </summary>
    public const string ViewInsurance = "PersonnelFiles.ViewInsurance";

    /// <summary>
    /// Read policy for medical-claim sub-resources. Authn-only superset: the precise check
    /// (ViewMedicalClaims / Admin, or the employee reading their own claims) lives in the medical-claim read
    /// handlers (self-service, D-09).
    /// </summary>
    public const string ViewMedicalClaims = "PersonnelFiles.ViewMedicalClaims";

    /// <summary>
    /// Write policy for medical-claim sub-resources. Authn-only superset: the precise check
    /// (ManageMedicalClaims / Admin, or the employee creating their own claim) lives in the medical-claim
    /// write handlers (self-service create, D-09). Kept authn-only — NOT a RequireAssertion — so a
    /// self-service employee creating their own claim is not blocked at the API layer.
    /// </summary>
    public const string ManageMedicalClaims = "PersonnelFiles.ManageMedicalClaims";

    /// <summary>
    /// Read policy for position-competency sub-resources ("Competencias del puesto"). Authn-only superset:
    /// the precise check (ViewCompetencies / Admin, or the employee reading their own competencies) lives in
    /// the competency read handlers (self-service, D-09).
    /// </summary>
    public const string ViewCompetencies = "PersonnelFiles.ViewCompetencies";

    /// <summary>
    /// Write policy for position-competency sub-resources (D-08): the dedicated
    /// <c>PersonnelFiles.ManageCompetencies</c> permission, or Admin / IAM super-admin. Writes are HR-only
    /// (CLARIHR is the source of truth — D-01); self-service is read-only. Kept a superset of the precise
    /// <c>EnsureCanManageCompetenciesAsync</c> handler gate so a legitimate manager is never falsely 403'd.
    /// </summary>
    public const string ManageCompetencies = "PersonnelFiles.ManageCompetencies";

    /// <summary>
    /// Read policy for off-payroll-transaction sub-resources ("transacciones fuera de nómina"). Authn-only
    /// superset: the precise check (ViewOffPayrollTransactions / Admin) lives in the off-payroll read handlers.
    /// HR-only — no self-service (D-06).
    /// </summary>
    public const string ViewOffPayrollTransactions = "PersonnelFiles.ViewOffPayrollTransactions";

    /// <summary>
    /// Write policy for off-payroll-transaction sub-resources. Authn-only superset: the precise check
    /// (ManageOffPayrollTransactions / Admin) lives in the off-payroll write handlers. HR-only — no
    /// self-service (D-06). Kept a superset of the precise <c>EnsureCanManageOffPayrollTransactionsAsync</c>
    /// handler gate so a legitimate manager is never falsely 403'd.
    /// </summary>
    public const string ManageOffPayrollTransactions = "PersonnelFiles.ManageOffPayrollTransactions";

    /// <summary>
    /// Write policy for the exit-interview form builder (D-01/D-14): the dedicated
    /// <c>PersonnelFiles.ManageExitInterviewForms</c> permission, or Admin / IAM super-admin. HR-only —
    /// designing/publishing/associating exit-interview forms is not self-service. Assigned to both the read
    /// and write verbs of <c>ExitInterviewFormsController</c> (form definitions are HR design-time data).
    /// Kept a superset of the precise <c>EnsureCanManageExitInterviewFormsAsync</c> handler gate.
    /// </summary>
    public const string ManageExitInterviewForms = "PersonnelFiles.ManageExitInterviewForms";

    /// <summary>
    /// Read policy for exit-interview submissions (D-14). Authn-only superset; the precise gate
    /// (ViewExitInterviews / Admin — RRHH only, plus the employee resolving/reading their own draft) lives
    /// in the submission handlers.
    /// </summary>
    public const string ViewExitInterviews = "PersonnelFiles.ViewExitInterviews";

    /// <summary>
    /// Write policy for exit-interview submissions (D-04). Authn-only superset — kept NOT a RequireAssertion
    /// so a self-service employee filling their own interview is never blocked at the API layer; the precise
    /// check (ManageExitInterviews / Admin, or the employee on their own file) lives in the handlers.
    /// </summary>
    public const string ManageExitInterviews = "PersonnelFiles.ManageExitInterviews";
}
