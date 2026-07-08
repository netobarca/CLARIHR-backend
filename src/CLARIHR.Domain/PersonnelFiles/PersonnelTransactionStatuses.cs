namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical status codes for the "otras transacciones de personal" records — recognitions and
/// disciplinary actions share a single lifecycle (REQ-003 D-15). Hybrid model: these constants are
/// the source of truth; the country-scoped <c>personnel-transaction-statuses</c> general catalog only
/// backs i18n/UI. The one-decision flow is <c>EN_REVISION → APLICADA/RECHAZADA</c>, plus
/// <c>ANULADA</c> (annulment from EN_REVISION or revocation from APLICADA — the record/transition
/// rules live in <c>PersonnelTransactionRules</c>, PR-2).
/// </summary>
public static class PersonnelTransactionStatuses
{
    public const string EnRevision = "EN_REVISION";
    public const string Aplicada = "APLICADA";
    public const string Rechazada = "RECHAZADA";
    public const string Anulada = "ANULADA";

    /// <summary>Statuses whose record is still editable (only EN_REVISION).</summary>
    public static readonly IReadOnlyCollection<string> Editable = new[] { EnRevision };

    /// <summary>Statuses that keep the record "live" (visible in the trays / feeding downstream inputs).</summary>
    public static readonly IReadOnlyCollection<string> Vigentes = new[] { EnRevision, Aplicada };

    public static bool IsEditable(string status) => status == EnRevision;

    public static bool IsVigente(string status) => status is EnRevision or Aplicada;
}
