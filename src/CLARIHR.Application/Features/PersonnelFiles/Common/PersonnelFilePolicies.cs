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
    /// Read policy for economic-aid sub-resources ("ayuda económica"). Authn-only superset: the precise check
    /// (ViewEconomicAidRequests / Admin, or the employee reading their own requests) lives in the economic-aid
    /// read handlers (self-service, D-02).
    /// </summary>
    public const string ViewEconomicAidRequests = "PersonnelFiles.ViewEconomicAidRequests";

    /// <summary>
    /// Write policy for economic-aid sub-resources. Authn-only superset: the precise check
    /// (ManageEconomicAidRequests / Admin, or the employee creating/cancelling their own request) lives in the
    /// economic-aid handlers. Kept authn-only — NOT a RequireAssertion — so a self-service employee is not
    /// blocked at the API layer (validation stays manager-only via the handler gate, D-03).
    /// </summary>
    public const string ManageEconomicAidRequests = "PersonnelFiles.ManageEconomicAidRequests";

    /// <summary>
    /// Read policy for certificate sub-resources ("constancias") and the company-wide bandeja. Authn-only
    /// superset: the precise check (ViewCertificateRequests / Admin, or the employee reading their own requests)
    /// lives in the certificate read handlers (self-service, D-02).
    /// </summary>
    public const string ViewCertificateRequests = "PersonnelFiles.ViewCertificateRequests";

    /// <summary>
    /// Write policy for certificate sub-resources. Authn-only superset: the precise check
    /// (ManageCertificateRequests / Admin, or the employee creating/cancelling their own request) lives in the
    /// certificate handlers. Kept authn-only — NOT a RequireAssertion — so a self-service employee is not blocked
    /// at the API layer (processing/issuance stay manager-only via the handler gate, D-04; salary printing also
    /// requires ViewCompensation, D-20).
    /// </summary>
    public const string ManageCertificateRequests = "PersonnelFiles.ManageCertificateRequests";

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

    /// <summary>
    /// Read policy for retirement requests (D-12). Authn-only superset; the precise RRHH-only gate
    /// (ViewRetirements / Admin) lives in the retirement read handlers. No self-service in Fase 1 (D-03).
    /// </summary>
    public const string ViewRetirements = "PersonnelFiles.ViewRetirements";

    /// <summary>
    /// Write policy for retirement requests (register/edit/cancel/execute — D-12). HR-only (no
    /// self-service), so it uses a RequireAssertion like ManageSubstitutions, kept a superset of the
    /// precise EnsureCanManageRetirementsAsync handler gate.
    /// </summary>
    public const string ManageRetirements = "PersonnelFiles.ManageRetirements";

    /// <summary>
    /// Write policy for authorizing/rejecting a retirement request (and annulling an authorized one).
    /// RequireAssertion over the dedicated AuthorizeRetirement grant (or IAM super-admin) —
    /// <c>PersonnelFiles.Admin</c> is deliberately excluded (D-12/D-13, separation of duties; mirrors
    /// the AuthorizeRehire handler gate).
    /// </summary>
    public const string AuthorizeRetirement = "PersonnelFiles.AuthorizeRetirement";

    /// <summary>
    /// Write policy for reverting an executed retirement. RequireAssertion over the dedicated
    /// RevertRetirement grant (or IAM super-admin) — <c>PersonnelFiles.Admin</c> is deliberately
    /// excluded (D-12).
    /// </summary>
    public const string RevertRetirement = "PersonnelFiles.RevertRetirement";

    /// <summary>
    /// Read policy for settlements (settlement module D-20). Authn-only superset; the precise HR-only
    /// gate (ViewSettlements / Admin) lives in the settlement read handlers. No self-service in Fase 1.
    /// </summary>
    public const string ViewSettlements = "PersonnelFiles.ViewSettlements";

    /// <summary>
    /// Write policy for settlements (create/edit/issue/annul + scenarios — settlement module D-20).
    /// HR-only (no self-service), so it uses a RequireAssertion like ManageRetirements, kept a superset
    /// of the precise EnsureCanManageSettlementsAsync handler gate.
    /// </summary>
    public const string ManageSettlements = "PersonnelFiles.ManageSettlements";

    /// <summary>
    /// Read policy for incapacity sub-resources (leave module D-17/D-18). Authn-only superset: the precise
    /// check (ViewIncapacities / Admin, or the employee reading their own incapacities — health data, 403
    /// without masking) lives in the incapacity read handlers (self-service, D-18).
    /// </summary>
    public const string ViewIncapacities = "PersonnelFiles.ViewIncapacities";

    /// <summary>
    /// Write policy for incapacity and lactation sub-resources (leave module D-17). Authn-only superset —
    /// kept NOT a RequireAssertion so a self-service employee registering their own incapacity
    /// (<c>EN_REVISION</c>, D-18) is not blocked at the API layer; confirmation/closure/annulment and
    /// lactation stay manager-only via the precise handler gate.
    /// </summary>
    public const string ManageIncapacities = "PersonnelFiles.ManageIncapacities";

    /// <summary>
    /// Read policy for vacation sub-resources (fund, balances, requests, plan — leave module D-17).
    /// Authn-only superset: the precise check (ViewVacations / Admin, or the employee reading their own
    /// fund/requests) lives in the vacation read handlers (self-service, D-18).
    /// </summary>
    public const string ViewVacations = "PersonnelFiles.ViewVacations";

    /// <summary>
    /// Write policy for vacation sub-resources (leave module D-17). Authn-only superset — kept NOT a
    /// RequireAssertion so a self-service employee creating/cancelling their own request (D-18) is not
    /// blocked at the API layer; decision/return/fund generation/plan stay manager-only via the precise
    /// handler gate (anti-self on decision, RN-17).
    /// </summary>
    public const string ManageVacations = "PersonnelFiles.ManageVacations";

    /// <summary>
    /// Read policy for compensatory-time sub-resources (statement, credits, absences — REQ-002 D-13).
    /// Authn-only superset: the precise check (ViewCompensatoryTime / Admin, or the employee reading
    /// their own fund/statement — self-service) lives in the compensatory-time read handlers (PR-3/PR-4).
    /// </summary>
    public const string ViewCompensatoryTime = "PersonnelFiles.ViewCompensatoryTime";

    /// <summary>
    /// Write policy for compensatory-time sub-resources (credits, absences — REQ-002 D-01/D-13). HR-only
    /// (no self-service write in Fase 1, D-01), so it is a RequireAssertion superset of the precise
    /// EnsureCanManageCompensatoryTimeAsync handler gate (the dedicated permission, or Admin / IAM
    /// super-admin), like ManageSettlements.
    /// </summary>
    public const string ManageCompensatoryTime = "PersonnelFiles.ManageCompensatoryTime";
}
