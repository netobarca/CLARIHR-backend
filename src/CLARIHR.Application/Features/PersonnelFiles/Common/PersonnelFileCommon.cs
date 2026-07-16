using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.Common;

public static partial class PersonnelFileValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxDocumentFileSizeBytes = 10 * 1024 * 1024;

    // Free-text search guardrail (§PF1): PersonnelFileRepository.ApplySearch fans a
    // non-sargable LIKE '%x%' over NormalizedFullName (+ NormalizedIdentificationNumber when
    // includeIdentificationMatch) across the 4 search surfaces (Search / Export /
    // DynamicQuery / Analytics). Empty/whitespace `q` = "no filter" (valid); otherwise enforce
    // a minimum trimmed length in the validator (rejected 400 before cache/DB). Threshold
    // aligned with the PositionSlots §PS2 / PDC §P2 / Internal Catalogs precedent (2). Scale
    // assumption: personnel files per tenant are bounded, so the (TenantId, …) scan + min
    // length is acceptable; escalate to pg_trgm GIN + EF.Functions.ILike if the search p95 or
    // rows/tenant exceed it. See project-foundation.md §12.8 / ADR-0002.
    public const int MinSearchLength = 2;
    public const int MaxSearchLength = 150;

    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;

    public static readonly IReadOnlyDictionary<string, string> AllowedDocumentContentTypesByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

    public static bool IsValidName(string value) =>
        NameRegex().IsMatch(value.Trim());

    public static bool IsValidCode(string value) =>
        CodeRegex().IsMatch(value.Trim());

    public static bool IsValidPhone(string value) =>
        PhoneRegex().IsMatch(value.Trim());

    public static bool IsAllowedDocumentExtension(string fileName) =>
        AllowedDocumentContentTypesByExtension.ContainsKey(Path.GetExtension(fileName));

    public static bool IsAllowedDocumentContentType(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        return AllowedDocumentContentTypesByExtension.TryGetValue(extension, out var expectedContentType) &&
               string.Equals(expectedContentType, contentType.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^[\p{L}][\p{L}\p{N} '.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"^[+0-9][0-9\- ]{5,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    public static int CalculateAge(DateTime birthDate, DateTime utcNow)
    {
        var today = utcNow.Date;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }


}

public static class PersonnelFilePermissionCodes
{
    public const string Read = "PersonnelFiles.Read";
    public const string Admin = "PersonnelFiles.Admin";

    /// <summary>
    /// Dedicated permission (D-10) to authorize the rehire of a "not rehireable" file. Distinct
    /// from <see cref="Admin"/> (regular manage) so a rehire analyst cannot self-approve the override.
    /// </summary>
    public const string AuthorizeRehire = "PersonnelFiles.AuthorizeRehire";

    /// <summary>
    /// Dedicated permission (D-16) to read compensation data (salary, ingresos/egresos). Lets configurable
    /// roles view another employee's compensation; the employee can always view their own via the
    /// self-service check in the compensation read handlers.
    /// </summary>
    public const string ViewCompensation = "PersonnelFiles.ViewCompensation";

    /// <summary>
    /// Dedicated permission (D-09) to manage authorization substitutions (designar/editar/activar/eliminar
    /// el sustituto de un empleado en ausencia). Separate from <see cref="Admin"/> (generic manage) so an HR
    /// analyst can be granted substitution management without full personnel administration; reads stay on
    /// <see cref="Read"/>. Follows the <see cref="ViewCompensation"/> precedent (Admin is a superset).
    /// </summary>
    public const string ManageSubstitutions = "PersonnelFiles.ManageSubstitutions";

    /// <summary>
    /// Dedicated permission to read insurance data (insurances + beneficiaries, which carry PII and
    /// insured amounts). Lets configurable roles view another employee's insurances; Admin is a superset.
    /// No self-service in this phase (the employee cannot view their own insurances).
    /// </summary>
    public const string ViewInsurance = "PersonnelFiles.ViewInsurance";

    /// <summary>
    /// Dedicated permission to read medical claims, whose diagnosis is special-category health data (D-08).
    /// Lets configurable roles view another employee's claims; Admin is a superset. Employees may read their
    /// OWN claims via a separate self-service check (D-09).
    /// </summary>
    public const string ViewMedicalClaims = "PersonnelFiles.ViewMedicalClaims";

    /// <summary>
    /// Dedicated permission to manage (create/edit/delete) medical claims of any employee (D-08). Admin is a
    /// superset. Employees may register (create) their OWN claims via a separate self-service check (D-09).
    /// </summary>
    public const string ManageMedicalClaims = "PersonnelFiles.ManageMedicalClaims";

    /// <summary>
    /// Dedicated permission to read position-competency results ("Competencias del puesto" — evaluation
    /// scores and gaps, sensitive talent data). Lets configurable roles view another employee's competencies;
    /// Admin is a superset. The employee can always view their own via the self-service check in the
    /// competency read handlers (D-09).
    /// </summary>
    public const string ViewCompetencies = "PersonnelFiles.ViewCompetencies";

    /// <summary>
    /// Dedicated permission to manage (record/edit/delete) position-competency results. Separate from
    /// <see cref="Admin"/> so an HR analyst can be granted competency management without full personnel
    /// administration; reads stay on <see cref="ViewCompetencies"/>. Admin is a superset.
    /// </summary>
    public const string ManageCompetencies = "PersonnelFiles.ManageCompetencies";

    /// <summary>
    /// Dedicated permission to read off-payroll transactions ("transacciones fuera de nómina" — company
    /// expenses on an employee: tools, PPE, uniforms, gifts…). Amounts (gifts/recognitions) are sensitive,
    /// so reads are HR-only with no self-service (D-06); Admin is a superset.
    /// </summary>
    public const string ViewOffPayrollTransactions = "PersonnelFiles.ViewOffPayrollTransactions";

    /// <summary>
    /// Dedicated permission to manage (create/edit/delete) off-payroll transactions and their attachments
    /// (D-06, HR-only — no self-service). Admin is a superset.
    /// </summary>
    public const string ManageOffPayrollTransactions = "PersonnelFiles.ManageOffPayrollTransactions";

    /// <summary>
    /// Dedicated permission to read employee economic-aid requests ("ayuda económica" — the emergency reason is
    /// sensitive data, D-10). Lets configurable roles view another employee's requests; Admin is a superset.
    /// Employees may read their OWN requests via a separate self-service check (D-02).
    /// </summary>
    public const string ViewEconomicAidRequests = "PersonnelFiles.ViewEconomicAidRequests";

    /// <summary>
    /// Dedicated permission to validate (approve/reject), disburse, edit and delete economic-aid requests
    /// (D-03). Admin is a superset. Employees may CREATE and CANCEL their OWN pending requests via a separate
    /// self-service check (D-02/D-11); validation is never self-service (no self-approval, D-03).
    /// </summary>
    public const string ManageEconomicAidRequests = "PersonnelFiles.ManageEconomicAidRequests";

    /// <summary>
    /// Dedicated permission to read employee certificate requests ("constancias": salario/laboral/embajada).
    /// Lets configurable roles view another employee's requests and the company-wide bandeja; Admin is a superset.
    /// Employees may read their OWN requests via a separate self-service check (D-02).
    /// </summary>
    public const string ViewCertificateRequests = "PersonnelFiles.ViewCertificateRequests";

    /// <summary>
    /// Dedicated permission to process, issue, deliver, reject, edit and delete certificate requests, plus the
    /// company certificate settings (D-04/D-17). Admin is a superset. Employees may CREATE and CANCEL their OWN
    /// pending requests via a separate self-service check (D-02). Issuing a salary-printing certificate
    /// additionally requires <see cref="ViewCompensation"/> (D-20).
    /// </summary>
    public const string ManageCertificateRequests = "PersonnelFiles.ManageCertificateRequests";

    /// <summary>
    /// Dedicated permission to design/publish/associate exit-interview forms (D-01). HR-only (no
    /// self-service — form building is design-time); Admin is a superset.
    /// </summary>
    public const string ManageExitInterviewForms = "PersonnelFiles.ManageExitInterviewForms";

    /// <summary>Dedicated permission to read exit-interview submissions (D-14, RRHH-only). Admin is a superset.</summary>
    public const string ViewExitInterviews = "PersonnelFiles.ViewExitInterviews";

    /// <summary>
    /// Dedicated permission to capture/manage exit-interview submissions of any employee (D-04). Admin is a
    /// superset. The employee may fill their OWN interview via a separate self-service check.
    /// </summary>
    public const string ManageExitInterviews = "PersonnelFiles.ManageExitInterviews";

    /// <summary>
    /// Dedicated permission to read retirement requests ("retiro definitivo"): the per-file requests, the
    /// company-wide bandeja and the interview tray (D-12, RRHH-only — no self-service in Fase 1). Admin is
    /// a superset.
    /// </summary>
    public const string ViewRetirements = "PersonnelFiles.ViewRetirements";

    /// <summary>
    /// Dedicated permission to register, edit, cancel (SOLICITADA) and EXECUTE retirement requests (D-12).
    /// Admin is a superset. Authorization and reversal are NOT included — they require the dedicated
    /// <see cref="AuthorizeRetirement"/> / <see cref="RevertRetirement"/> grants.
    /// </summary>
    public const string ManageRetirements = "PersonnelFiles.ManageRetirements";

    /// <summary>
    /// Dedicated permission to authorize/reject a retirement request (and annul an authorized one).
    /// Like <see cref="AuthorizeRehire"/>, <c>PersonnelFiles.Admin</c> is deliberately NOT a superset
    /// (D-12 — separation of duties); only the IAM super-admin remains a universal fallback.
    /// </summary>
    public const string AuthorizeRetirement = "PersonnelFiles.AuthorizeRetirement";

    /// <summary>
    /// Dedicated permission to revert an executed retirement (D-12). Like <see cref="AuthorizeRehire"/>,
    /// <c>PersonnelFiles.Admin</c> is deliberately NOT a superset; only the IAM super-admin remains a
    /// universal fallback.
    /// </summary>
    public const string RevertRetirement = "PersonnelFiles.RevertRetirement";

    /// <summary>
    /// Dedicated permission to read settlements ("liquidaciones" — per-file detail, the company bandeja
    /// and the individual exports). Settlement data exposes salaries, so reads are HR-only with no
    /// self-service in Fase 1 (settlement module D-20); Admin is a superset.
    /// </summary>
    public const string ViewSettlements = "PersonnelFiles.ViewSettlements";

    /// <summary>
    /// Dedicated permission to create/edit/issue/annul settlements and to manage settlement scenarios
    /// (settlement module D-20). Admin is a superset. The subject employee can never manage their own
    /// settlement (anti-self gate in the handlers).
    /// </summary>
    public const string ManageSettlements = "PersonnelFiles.ManageSettlements";

    /// <summary>
    /// Dedicated permission to read incapacities and lactation periods of any personnel file (leave
    /// module D-17). Health data: an employee always reads their OWN incapacities without this
    /// permission (self-service gate in the handlers); Admin is a superset.
    /// </summary>
    public const string ViewIncapacities = "PersonnelFiles.ViewIncapacities";

    /// <summary>
    /// Dedicated permission to register/confirm/close/annul/extend incapacities and to manage lactation
    /// periods (leave module D-17). Admin is a superset. Self-service employees register their own
    /// incapacity in <c>EN_REVISION</c> without this permission (D-18); confirming your own incapacity
    /// is forbidden (anti-self gate in the handlers).
    /// </summary>
    public const string ManageIncapacities = "PersonnelFiles.ManageIncapacities";

    /// <summary>
    /// Dedicated permission to read the vacation fund, balances, requests, calendar and annual plan of
    /// any personnel file (leave module D-17). An employee always reads their OWN fund and requests
    /// without this permission (self-service gate in the handlers); Admin is a superset.
    /// </summary>
    public const string ViewVacations = "PersonnelFiles.ViewVacations";

    /// <summary>
    /// Dedicated permission to manage the vacation fund (generation), decide/return vacation requests
    /// and administer the annual plan (leave module D-17). Admin is a superset. Self-service employees
    /// create/cancel their own request without this permission (D-18); deciding a request of your own
    /// personnel file is forbidden (anti-self gate in the handlers, RN-17).
    /// </summary>
    public const string ManageVacations = "PersonnelFiles.ManageVacations";

    /// <summary>
    /// Dedicated permission to read the compensatory-time fund, statement, credits and absences of any
    /// personnel file (REQ-002 D-13). An employee always reads their OWN fund/statement without this
    /// permission (self-service gate in the handlers, PR-3/PR-4); Admin is a superset.
    /// </summary>
    public const string ViewCompensatoryTime = "PersonnelFiles.ViewCompensatoryTime";

    /// <summary>
    /// Dedicated permission to register/edit/annul compensatory-time credits and absences (REQ-002
    /// D-01/D-13). HR-only in Fase 1 (no self-service write, D-01); Admin is a superset.
    /// </summary>
    public const string ManageCompensatoryTime = "PersonnelFiles.ManageCompensatoryTime";

    /// <summary>
    /// Dedicated permission to read the HR analytics dashboard (aggregate indicators over the personnel
    /// padrón). Lets configurable roles see the dashboards without full personnel-file read; the regular
    /// <see cref="Read"/> permission and <see cref="Admin"/> are supersets. The dashboard is read-only and
    /// never exposes the per-employee sensitive data guarded by the dedicated View* permissions.
    /// </summary>
    public const string ViewReports = "PersonnelFiles.ViewReports";

    /// <summary>
    /// Dedicated permission to read employee recognitions ("reconocimientos" — REQ-003 D-05). An employee
    /// always reads their OWN applied recognitions without this permission (self-service gate in the
    /// handlers, PR-3); Admin is a superset.
    /// </summary>
    public const string ViewRecognitions = "PersonnelFiles.ViewRecognitions";

    /// <summary>
    /// Dedicated permission to register/edit/annul (EN_REVISION) recognitions (REQ-003 D-05). Admin is a
    /// superset. Deciding/revoking a recognition requires the dedicated <see cref="AuthorizeRecognitions"/>
    /// grant (double anti-self on decision/revocation).
    /// </summary>
    public const string ManageRecognitions = "PersonnelFiles.ManageRecognitions";

    /// <summary>
    /// Dedicated permission to decide/revoke a recognition (REQ-003 D-05). Like
    /// <see cref="AuthorizeRetirement"/>, <c>PersonnelFiles.Admin</c> is deliberately NOT a superset
    /// (separation of duties); only the IAM super-admin remains a universal fallback.
    /// </summary>
    public const string AuthorizeRecognitions = "PersonnelFiles.AuthorizeRecognitions";

    /// <summary>
    /// Dedicated permission to read employee disciplinary actions ("amonestaciones" — REQ-003 D-05). An
    /// employee always reads their OWN applied disciplinary actions without this permission (self-service
    /// gate in the handlers, PR-4); Admin is a superset.
    /// </summary>
    public const string ViewDisciplinaryActions = "PersonnelFiles.ViewDisciplinaryActions";

    /// <summary>
    /// Dedicated permission to register/edit/annul (EN_REVISION) disciplinary actions (REQ-003 D-05).
    /// Admin is a superset. Deciding/revoking a disciplinary action requires the dedicated
    /// <see cref="AuthorizeDisciplinaryActions"/> grant (double anti-self on decision/revocation).
    /// </summary>
    public const string ManageDisciplinaryActions = "PersonnelFiles.ManageDisciplinaryActions";

    /// <summary>
    /// Dedicated permission to decide/revoke a disciplinary action (REQ-003 D-05). Like
    /// <see cref="AuthorizeRetirement"/>, <c>PersonnelFiles.Admin</c> is deliberately NOT a superset
    /// (separation of duties); only the IAM super-admin remains a universal fallback.
    /// </summary>
    public const string AuthorizeDisciplinaryActions = "PersonnelFiles.AuthorizeDisciplinaryActions";

    /// <summary>
    /// Dedicated permission to read the time-availability query ("consulta de disponibilidad de tiempos" —
    /// REQ-003 D-14): a corporate planning view with a minimal payload (no cause/facts/amounts). Corporate
    /// read with no self-service; Admin is a superset.
    /// </summary>
    public const string ViewTimeAvailability = "PersonnelFiles.ViewTimeAvailability";

    /// <summary>
    /// Dedicated permission to read recurring incomes ("planilla ingresos cíclicos" — REQ-005 D-06/P-14): the
    /// per-file detail, the company bandeja and the payroll-input exports. HR-only with no self-service in
    /// Fase 1 (P-11); Admin is a superset.
    /// </summary>
    public const string ViewRecurringIncomes = "PersonnelFiles.ViewRecurringIncomes";

    /// <summary>
    /// Dedicated permission to register/edit/suspend/close/annul (EN_REVISION and the operational lifecycle)
    /// recurring incomes and to apply their installments (REQ-005 D-06/P-14). Admin is a superset.
    /// Deciding/revoking a recurring income requires the dedicated <see cref="AuthorizeRecurringIncomes"/>
    /// grant (double anti-self on decision/revocation).
    /// </summary>
    public const string ManageRecurringIncomes = "PersonnelFiles.ManageRecurringIncomes";

    /// <summary>
    /// Dedicated permission to decide (authorize/reject) and revoke a recurring income (REQ-005 D-06/P-14).
    /// Like <see cref="AuthorizeRetirement"/>, <c>PersonnelFiles.Admin</c> is deliberately NOT a superset
    /// (separation of duties); only the IAM super-admin remains a universal fallback.
    /// </summary>
    public const string AuthorizeRecurringIncomes = "PersonnelFiles.AuthorizeRecurringIncomes";

    /// <summary>Dedicated permission to read one-time deductions ("planilla descuentos eventuales" — REQ-009):
    /// the per-file detail, the company bandeja and the payroll-input export. HR-only; Admin is a superset.</summary>
    public const string ViewOneTimeDeductions = "PersonnelFiles.ViewOneTimeDeductions";

    /// <summary>Dedicated permission to register/edit/annul one-time deductions and to apply (or reverse) them
    /// (REQ-009). Admin is a superset; deciding requires <see cref="AuthorizeOneTimeDeductions"/>.</summary>
    public const string ManageOneTimeDeductions = "PersonnelFiles.ManageOneTimeDeductions";

    /// <summary>Dedicated permission to decide (authorize/reject) and revoke a one-time deduction (REQ-009).
    /// <c>PersonnelFiles.Admin</c> is deliberately NOT a superset (separation of duties + TRIPLE anti-self).</summary>
    public const string AuthorizeOneTimeDeductions = "PersonnelFiles.AuthorizeOneTimeDeductions";

    /// <summary>
    /// Dedicated permission to read recurring deductions ("planilla descuentos cíclicos" — REQ-008 D-06): the
    /// per-file detail, the amortization schedule, the company bandeja and the payroll-input exports. HR-only
    /// with no self-service in Fase 1; Admin is a superset.
    /// </summary>
    public const string ViewRecurringDeductions = "PersonnelFiles.ViewRecurringDeductions";

    /// <summary>
    /// Dedicated permission to register/edit/suspend/close/annul recurring deductions and to apply their
    /// installments — regular and extraordinary (REQ-008 D-06). Admin is a superset. Deciding/revoking a
    /// recurring deduction requires the dedicated <see cref="AuthorizeRecurringDeductions"/> grant (double
    /// anti-self on decision/revocation).
    /// </summary>
    public const string ManageRecurringDeductions = "PersonnelFiles.ManageRecurringDeductions";

    /// <summary>
    /// Dedicated permission to decide (authorize/reject) and revoke a recurring deduction (REQ-008 D-06).
    /// Like <see cref="AuthorizeRecurringIncomes"/>, <c>PersonnelFiles.Admin</c> is deliberately NOT a
    /// superset (separation of duties); only the IAM super-admin remains a universal fallback.
    /// </summary>
    public const string AuthorizeRecurringDeductions = "PersonnelFiles.AuthorizeRecurringDeductions";

    /// <summary>
    /// Dedicated permission to read an employee's indebtedness level and run the simulation (REQ-010 P-15):
    /// it is an aggregated, sensitive figure, so it does not ride on the generic file-read permission.
    /// <c>PersonnelFiles.Admin</c> IS a superset (this is a read grant, not an Authorize* separation-of-duties one).
    /// </summary>
    public const string ViewIndebtedness = "PersonnelFiles.ViewIndebtedness";

    /// <summary>
    /// Dedicated permission to configure the company's per-type indebtedness ceilings (REQ-010 D-16).
    /// <c>PersonnelFiles.Admin</c> is a superset.
    /// </summary>
    public const string ManageIndebtednessParameters = "PersonnelFiles.ManageIndebtednessParameters";

    /// <summary>Read the not-worked-time records, the company bandeja and the payroll input (REQ-011).</summary>
    public const string ViewNotWorkedTimes = "PersonnelFiles.ViewNotWorkedTimes";

    /// <summary>Register and annul not-worked time (REQ-011). No Authorize* counterpart: the flow has no decision
    /// step (P-16) — the absence already occurred.</summary>
    public const string ManageNotWorkedTimes = "PersonnelFiles.ManageNotWorkedTimes";

    /// <summary>Configure the company's not-worked-time TYPE master (REQ-011 D-18).</summary>
    public const string ManageNotWorkedTimeTypes = "PersonnelFiles.ManageNotWorkedTimeTypes";

    /// <summary>
    /// Dedicated permission to read one-time incomes ("planilla ingresos eventuales" — REQ-006 P-01): the
    /// per-file detail, the company bandeja and the payroll-input exports. HR-only with no self-service in
    /// Fase 1 (P-11); Admin is a superset.
    /// </summary>
    public const string ViewOneTimeIncomes = "PersonnelFiles.ViewOneTimeIncomes";

    /// <summary>
    /// Dedicated permission to register/edit/annul (EN_REVISION) one-time incomes and to apply them by period
    /// (unitary or batch — REQ-006 P-01). Admin is a superset. Deciding/revoking a one-time income requires
    /// the dedicated <see cref="AuthorizeOneTimeIncomes"/> grant (triple anti-self on decision/revocation).
    /// </summary>
    public const string ManageOneTimeIncomes = "PersonnelFiles.ManageOneTimeIncomes";

    /// <summary>
    /// Dedicated permission to decide (authorize/reject) and revoke a one-time income (REQ-006 P-01/P-02).
    /// Like <see cref="AuthorizeRetirement"/>, <c>PersonnelFiles.Admin</c> is deliberately NOT a superset
    /// (separation of duties); only the IAM super-admin remains a universal fallback.
    /// </summary>
    public const string AuthorizeOneTimeIncomes = "PersonnelFiles.AuthorizeOneTimeIncomes";

    /// <summary>
    /// Dedicated permission to read overtime records ("horas extras del empleado" — REQ-007 P-01/P-11/P-12):
    /// the per-file detail (including own records via the portal channel), the company bandeja and the
    /// payroll-input exports. Also the READ side of the two overtime-configuration masters (overtime types /
    /// justification types). Admin is a superset.
    /// </summary>
    public const string ViewOvertimeRecords = "PersonnelFiles.ViewOvertimeRecords";

    /// <summary>
    /// Dedicated permission to register/edit/annul (EN_REVISION) overtime records and to apply them by
    /// period (unitary or batch — REQ-007 P-01/P-07). Also the MANAGE side of the two overtime-configuration
    /// masters (overtime types / justification types + load-template). Admin is a superset. The employee
    /// portal self-service channel (P-01) does NOT require this permission — it is gated per-handler by the
    /// company self-service preference. Deciding/revoking an overtime record requires the dedicated
    /// <see cref="AuthorizeOvertimeRecords"/> grant (triple anti-self on decision/revocation).
    /// </summary>
    public const string ManageOvertimeRecords = "PersonnelFiles.ManageOvertimeRecords";

    /// <summary>
    /// Dedicated permission to decide (authorize/reject) and revoke an overtime record (REQ-007 P-01/P-06).
    /// Like <see cref="AuthorizeRetirement"/>, <c>PersonnelFiles.Admin</c> is deliberately NOT a superset
    /// (separation of duties + triple anti-self); only the IAM super-admin remains a universal fallback.
    /// </summary>
    public const string AuthorizeOvertimeRecords = "PersonnelFiles.AuthorizeOvertimeRecords";

    /// <summary>
    /// Dedicated permission to read payroll runs ("corridas de planilla" — REQ-012 §4): the company bandeja,
    /// the run detail with its per-employee drill, the exports and the corporate employee-history query.
    /// Payroll data exposes salaries, so corporate reads are HR-only; the REQ-015 self-service branch (the
    /// employee reading their OWN history) is resolved by a self-or-view handler gate, never by this
    /// permission alone. Admin is a superset.
    /// </summary>
    public const string ViewPayrollRuns = "PersonnelFiles.ViewPayrollRuns";

    /// <summary>
    /// Dedicated permission to generate, adjust (overrides/inclusion), recalculate, regenerate, close and
    /// annul payroll runs (REQ-012 §4). Admin is a superset. Authorizing/returning a run requires the
    /// dedicated <see cref="AuthorizePayrollRuns"/> grant (double anti-self on the decision).
    /// </summary>
    public const string ManagePayrollRuns = "PersonnelFiles.ManagePayrollRuns";

    /// <summary>
    /// Dedicated permission to authorize a payroll run or return it with a reason (REQ-012 §4). Like
    /// <see cref="AuthorizeRetirement"/>, <c>PersonnelFiles.Admin</c> is deliberately NOT a superset
    /// (separation of duties + double anti-self); only the IAM super-admin remains a universal fallback.
    /// </summary>
    public const string AuthorizePayrollRuns = "PersonnelFiles.AuthorizePayrollRuns";

    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "PERSONNEL_FILES";
}

public static class PersonnelFileErrors
{
    public static readonly Error Forbidden = new(
        "PERSONNEL_FILES_FORBIDDEN",
        "You do not have permission to access personnel file administration.",
        ErrorType.Forbidden);

    public static readonly Error NotFound = new(
        "PERSONNEL_FILE_NOT_FOUND",
        "The personnel file could not be found.",
        ErrorType.NotFound);

    public static readonly Error IdentificationConflict = new(
        "PERSONNEL_FILE_IDENTIFICATION_CONFLICT",
        "Another personnel file already uses the requested identification.",
        ErrorType.Conflict);

    public static readonly Error IdentificationNumberFormatInvalid = new(
        "PERSONNEL_FILE_IDENTIFICATION_NUMBER_FORMAT_INVALID",
        "The identification number does not match the format configured for the identification type.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error StateRuleViolation = new(
        "PERSONNEL_FILE_STATE_RULE_VIOLATION",
        "The requested operation is not allowed for the current personnel file state.",
        ErrorType.UnprocessableEntity);

    /// <summary>
    /// A retired employee's profile is frozen: modules whose records feed the settlement snapshot (e.g. the
    /// compensatory-time fund — REQ-002 aclaración №9) reject every write while the profile is RETIRADO so the
    /// already-computed settlement stays consistent. Reversing the retirement (30-day window) reopens the profile.
    /// </summary>
    public static readonly Error ProfileRetiredLocked = new(
        "EMPLOYEE_PROFILE_RETIRED_LOCKED",
        "The employee profile is retired; this record can no longer be created, edited or annulled.",
        ErrorType.UnprocessableEntity);

    public static readonly Error RecordTypeTransitionNotAllowed = new(
        "PERSONNEL_FILE_RECORD_TYPE_TRANSITION_NOT_ALLOWED",
        "Personnel file record type transitions are not allowed in this module.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ProvisioningFieldsLocked = new(
        "PERSONNEL_FILE_PROVISIONING_FIELDS_LOCKED",
        "Assigned position slot and institutional email cannot be changed after completion.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FinalizeRequiresInstitutionalEmail = new(
        "PERSONNEL_FILE_FINALIZE_REQUIRES_INSTITUTIONAL_EMAIL",
        "An institutional email is required to finalize the personnel file.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FinalizeRequiresAssignedPositionSlot = new(
        "PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT",
        "An assigned position slot is required to finalize the personnel file.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FinalizeRequiresPositionSlotRole = new(
        "PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT_ROLE",
        "The assigned position slot must have a valid role configured before finalizing the personnel file.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FinalizeOnlyEmployee = new(
        "PERSONNEL_FILE_FINALIZE_ONLY_EMPLOYEE",
        "Only employee personnel files can be finalized.",
        ErrorType.UnprocessableEntity);

    public static readonly Error LinkedUserConflict = new(
        "PERSONNEL_FILE_LINKED_USER_CONFLICT",
        "The institutional email is already linked to a different personnel file.",
        ErrorType.Conflict);

    public static readonly Error EffectiveDatesInvalid = new(
        "PERSONNEL_FILE_EFFECTIVE_DATES_INVALID",
        "The date range is invalid.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ItemNotFound = new(
        "PERSONNEL_FILE_ITEM_NOT_FOUND",
        "The requested item could not be found in this personnel file.",
        ErrorType.NotFound);

    public static readonly Error DocumentNotFound = new(
        "PERSONNEL_FILE_DOCUMENT_NOT_FOUND",
        "The personnel file document could not be found.",
        ErrorType.NotFound);

    public static readonly Error DocumentFileRequired = new(
        "PERSONNEL_FILE_DOCUMENT_FILE_REQUIRED",
        "A document file is required.",
        ErrorType.Validation);

    public static readonly Error DocumentFileTooLarge = new(
        "PERSONNEL_FILE_DOCUMENT_TOO_LARGE",
        "The document file exceeds the maximum allowed size.",
        ErrorType.PayloadTooLarge);

    public static readonly Error DocumentContentTypeUnsupported = new(
        "PERSONNEL_FILE_DOCUMENT_CONTENT_TYPE_UNSUPPORTED",
        "The document file type is not supported.",
        ErrorType.Validation);

    public static readonly Error DocumentStorageNotConfigured = new(
        "PERSONNEL_FILE_DOCUMENT_STORAGE_NOT_CONFIGURED",
        "Personnel file document storage is not configured.",
        ErrorType.ServiceUnavailable);



    public static readonly Error ExportFormatInvalid = new(
        "PERSONNEL_FILE_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);



    public static readonly Error FamilyMemberRuleViolation = new(
        "PERSONNEL_FILE_FAMILY_MEMBER_RULE_VIOLATION",
        "Family member conditional fields are invalid.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(PersonnelFilePermissionCodes.ResourceKey, action);
}
