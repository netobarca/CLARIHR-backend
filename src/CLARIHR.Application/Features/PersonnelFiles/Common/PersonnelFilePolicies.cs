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

    /// <summary>
    /// Read policy for recognition sub-resources ("reconocimientos" — REQ-003 D-05). Authn-only superset:
    /// the precise check (ViewRecognitions / Admin, or the employee reading their own APLICADAS —
    /// self-service, D-13) lives in the recognition read handlers (PR-3).
    /// </summary>
    public const string ViewRecognitions = "PersonnelFiles.ViewRecognitions";

    /// <summary>
    /// Write policy for recognition sub-resources (register/edit/annul — REQ-003 D-05). Authn-only
    /// superset with fallback Admin/ManageAdministration; the precise HR-only gate lives in the
    /// recognition write handlers (PR-3). Decision/revocation is the dedicated
    /// <see cref="AuthorizeRecognitions"/> grant.
    /// </summary>
    public const string ManageRecognitions = "PersonnelFiles.ManageRecognitions";

    /// <summary>
    /// Write policy for deciding/revoking a recognition (REQ-003 D-05). RequireAssertion over the
    /// dedicated AuthorizeRecognitions grant (or IAM super-admin) — <c>PersonnelFiles.Admin</c> is
    /// deliberately excluded (separation of duties + double anti-self, mirrors AuthorizeRetirement).
    /// </summary>
    public const string AuthorizeRecognitions = "PersonnelFiles.AuthorizeRecognitions";

    /// <summary>
    /// Read policy for disciplinary-action sub-resources ("amonestaciones" — REQ-003 D-05). Authn-only
    /// superset: the precise check (ViewDisciplinaryActions / Admin, or the employee reading their own
    /// APLICADAS — self-service, D-13) lives in the disciplinary-action read handlers (PR-4).
    /// </summary>
    public const string ViewDisciplinaryActions = "PersonnelFiles.ViewDisciplinaryActions";

    /// <summary>
    /// Write policy for disciplinary-action sub-resources (register/edit/annul — REQ-003 D-05). Authn-only
    /// superset with fallback Admin/ManageAdministration; the precise HR-only gate lives in the
    /// disciplinary-action write handlers (PR-4). Decision/revocation is the dedicated
    /// <see cref="AuthorizeDisciplinaryActions"/> grant.
    /// </summary>
    public const string ManageDisciplinaryActions = "PersonnelFiles.ManageDisciplinaryActions";

    /// <summary>
    /// Write policy for deciding/revoking a disciplinary action (REQ-003 D-05). RequireAssertion over the
    /// dedicated AuthorizeDisciplinaryActions grant (or IAM super-admin) — <c>PersonnelFiles.Admin</c> is
    /// deliberately excluded (separation of duties + double anti-self, mirrors AuthorizeRetirement).
    /// </summary>
    public const string AuthorizeDisciplinaryActions = "PersonnelFiles.AuthorizeDisciplinaryActions";

    /// <summary>
    /// Read policy for the time-availability query ("consulta de disponibilidad de tiempos" — REQ-003
    /// D-14). Corporate read with no self-service branch, so it is a RequireAssertion superset of the
    /// precise EnsureCanViewTimeAvailabilityAsync handler gate (the dedicated permission, or Admin / IAM
    /// super-admin).
    /// </summary>
    public const string ViewTimeAvailability = "PersonnelFiles.ViewTimeAvailability";

    /// <summary>
    /// Read policy for recurring-income sub-resources ("planilla ingresos cíclicos" — REQ-005). HR-only with
    /// no self-service in Fase 1 (P-11), so it is a RequireAssertion superset of the precise
    /// EnsureCanViewRecurringIncomesAsync handler gate (ViewRecurringIncomes / Admin / IAM super-admin),
    /// like ViewSettlements would be if it had no self branch.
    /// </summary>
    public const string ViewRecurringIncomes = "PersonnelFiles.ViewRecurringIncomes";

    /// <summary>
    /// Write policy for recurring-income sub-resources (register/edit/suspend/close/annul — REQ-005). HR-only
    /// (no self-service, P-11), so it is a RequireAssertion superset of the precise
    /// EnsureCanManageRecurringIncomesAsync handler gate (the dedicated permission, or Admin / IAM
    /// super-admin), like ManageSettlements. Deciding/revoking is the dedicated
    /// <see cref="AuthorizeRecurringIncomes"/> grant.
    /// </summary>
    public const string ManageRecurringIncomes = "PersonnelFiles.ManageRecurringIncomes";

    /// <summary>
    /// Write policy for deciding/revoking a recurring income (REQ-005 D-06/P-14). RequireAssertion over the
    /// dedicated AuthorizeRecurringIncomes grant (or IAM super-admin) — <c>PersonnelFiles.Admin</c> is
    /// deliberately excluded (separation of duties + double anti-self, mirrors AuthorizeRetirement).
    /// </summary>
    public const string AuthorizeRecurringIncomes = "PersonnelFiles.AuthorizeRecurringIncomes";

    /// <summary>Read policy for one-time-deduction sub-resources ("planilla descuentos eventuales" — REQ-009).
    /// HR-only: a RequireAssertion superset of the precise EnsureCanViewOneTimeDeductionsAsync handler gate.</summary>
    public const string ViewOneTimeDeductions = "PersonnelFiles.ViewOneTimeDeductions";

    /// <summary>Write policy for one-time-deduction sub-resources (register/edit/annul + apply/reverse — REQ-009).
    /// Deciding/revoking is the dedicated <see cref="AuthorizeOneTimeDeductions"/> grant.</summary>
    public const string ManageOneTimeDeductions = "PersonnelFiles.ManageOneTimeDeductions";

    /// <summary>Write policy for deciding/revoking a one-time deduction (REQ-009). RequireAssertion over the
    /// dedicated grant (or IAM super-admin) — <c>PersonnelFiles.Admin</c> is deliberately excluded.</summary>
    public const string AuthorizeOneTimeDeductions = "PersonnelFiles.AuthorizeOneTimeDeductions";

    /// <summary>
    /// Read policy for recurring-deduction sub-resources ("planilla descuentos cíclicos" — REQ-008). HR-only
    /// with no self-service in Fase 1, so it is a RequireAssertion superset of the precise
    /// EnsureCanViewRecurringDeductionsAsync handler gate (ViewRecurringDeductions / Admin / IAM
    /// super-admin). Mirrors <see cref="ViewRecurringIncomes"/>.
    /// </summary>
    public const string ViewRecurringDeductions = "PersonnelFiles.ViewRecurringDeductions";

    /// <summary>
    /// Write policy for recurring-deduction sub-resources (register/edit/suspend/close/annul + installments
    /// and extraordinary payments — REQ-008). HR-only, so it is a RequireAssertion superset of the precise
    /// EnsureCanManageRecurringDeductionsAsync handler gate (the dedicated permission, or Admin / IAM
    /// super-admin). Deciding/revoking is the dedicated <see cref="AuthorizeRecurringDeductions"/> grant.
    /// </summary>
    public const string ManageRecurringDeductions = "PersonnelFiles.ManageRecurringDeductions";

    /// <summary>
    /// Write policy for deciding/revoking a recurring deduction (REQ-008 D-06). RequireAssertion over the
    /// dedicated AuthorizeRecurringDeductions grant (or IAM super-admin) — <c>PersonnelFiles.Admin</c> is
    /// deliberately excluded (separation of duties + double anti-self, mirrors AuthorizeRecurringIncomes).
    /// </summary>
    public const string AuthorizeRecurringDeductions = "PersonnelFiles.AuthorizeRecurringDeductions";

    /// <summary>
    /// Read policy for the indebtedness query and simulation (REQ-010 D-17, P-15). Authn-only at the policy
    /// level; the precise gate (EnsureCanViewIndebtednessAsync) runs per handler. Admin IS a superset here —
    /// unlike the Authorize* grants, this is a plain read permission.
    /// </summary>
    public const string ViewIndebtedness = "PersonnelFiles.ViewIndebtedness";

    /// <summary>
    /// Write policy for the indebtedness parameters (the per-type ceilings — REQ-010 D-16). RequireAssertion
    /// superset of EnsureCanManageIndebtednessParametersAsync (the dedicated permission, or Admin / IAM
    /// super-admin).
    /// </summary>
    public const string ManageIndebtednessParameters = "PersonnelFiles.ManageIndebtednessParameters";

    /// <summary>
    /// Read policy for one-time-income sub-resources ("planilla ingresos eventuales" — REQ-006). HR-only with
    /// no self-service in Fase 1 (P-11), so it is a RequireAssertion superset of the precise
    /// EnsureCanViewOneTimeIncomesAsync handler gate (ViewOneTimeIncomes / Admin / IAM super-admin).
    /// </summary>
    public const string ViewOneTimeIncomes = "PersonnelFiles.ViewOneTimeIncomes";

    /// <summary>
    /// Write policy for one-time-income sub-resources (register/edit/annul + apply-by-period — REQ-006).
    /// HR-only (no self-service, P-11), so it is a RequireAssertion superset of the precise
    /// EnsureCanManageOneTimeIncomesAsync handler gate (the dedicated permission, or Admin / IAM
    /// super-admin). Deciding/revoking is the dedicated <see cref="AuthorizeOneTimeIncomes"/> grant.
    /// </summary>
    public const string ManageOneTimeIncomes = "PersonnelFiles.ManageOneTimeIncomes";

    /// <summary>
    /// Write policy for deciding/revoking a one-time income (REQ-006 P-01/P-02). RequireAssertion over the
    /// dedicated AuthorizeOneTimeIncomes grant (or IAM super-admin) — <c>PersonnelFiles.Admin</c> is
    /// deliberately excluded (separation of duties + triple anti-self, mirrors AuthorizeRetirement).
    /// </summary>
    public const string AuthorizeOneTimeIncomes = "PersonnelFiles.AuthorizeOneTimeIncomes";

    /// <summary>
    /// Read policy for overtime-record sub-resources ("horas extras del empleado" — REQ-007). The module has
    /// a dual channel — HR + employee portal self-service (P-01, preference default-off) and self-read (P-12)
    /// — so this stays AUTHN-ONLY (mirrors the medical-claims / leave read policies, comment §47-53): the
    /// precise check (ViewOvertimeRecords / Admin, or the employee reading their own record) lives in the
    /// overtime read handlers. Kept authn-only — NOT a RequireAssertion — so a self-service employee is not
    /// blocked at the API layer.
    /// </summary>
    public const string ViewOvertimeRecords = "PersonnelFiles.ViewOvertimeRecords";

    /// <summary>
    /// Write policy for overtime-record sub-resources (register/edit/annul + apply-by-period — REQ-007).
    /// AUTHN-ONLY superset (NOT a RequireAssertion) — this is the mechanism that enables the employee portal
    /// self-service channel (P-01): the precise check (ManageOvertimeRecords / Admin, or the employee acting
    /// on their OWN EN_REVISION record when the company self-service preference is enabled) lives in the
    /// overtime write handlers. Deciding/revoking is the dedicated <see cref="AuthorizeOvertimeRecords"/>
    /// grant.
    /// </summary>
    public const string ManageOvertimeRecords = "PersonnelFiles.ManageOvertimeRecords";

    /// <summary>
    /// Write policy for deciding/revoking an overtime record (REQ-007 P-01/P-06). RequireAssertion over the
    /// dedicated AuthorizeOvertimeRecords grant (or IAM super-admin) — <c>PersonnelFiles.Admin</c> is
    /// deliberately excluded (separation of duties + triple anti-self, mirrors AuthorizeRetirement).
    /// </summary>
    public const string AuthorizeOvertimeRecords = "PersonnelFiles.AuthorizeOvertimeRecords";
}
