namespace CLARIHR.Domain.Payroll;

/// <summary>
/// Canonical status codes of a payroll RUN ("corrida", REQ-012 §1.5 — catalog
/// <c>payroll-run-statuses</c>). A run is born <see cref="Generada"/> (editable, re-generable), is
/// authorized into <see cref="Autorizada"/> (calculations freeze; the only way back is a return with a
/// reason), closed into <see cref="Cerrada"/> (terminal — the payment cycle ended and the period closes with
/// it) or annulled into <see cref="Anulada"/> (terminal, only pre-closure; releases the one-active-run slot).
/// The run aggregate arrives in M4 (PR-4); the codes are canonical from PR-1 because the country catalog
/// seeds them.
/// </summary>
public static class PayrollRunStatuses
{
    public const string Generada = "GENERADA";
    public const string Autorizada = "AUTORIZADA";
    public const string Cerrada = "CERRADA";
    public const string Anulada = "ANULADA";

    /// <summary>States whose lines admit overrides/inclusion changes and re-generation: only GENERADA.</summary>
    public static readonly IReadOnlyCollection<string> Editable = new[] { Generada };

    /// <summary>States from which the run may be authorized: only GENERADA.</summary>
    public static readonly IReadOnlyCollection<string> Authorizable = new[] { Generada };

    /// <summary>States from which the run may be closed: only AUTORIZADA.</summary>
    public static readonly IReadOnlyCollection<string> Closable = new[] { Autorizada };

    /// <summary>States from which the run may be returned (with a reason) to GENERADA: only AUTORIZADA.</summary>
    public static readonly IReadOnlyCollection<string> Returnable = new[] { Autorizada };

    /// <summary>Closed states — no further transition.</summary>
    public static readonly IReadOnlyCollection<string> Terminal = new[] { Cerrada, Anulada };
}

/// <summary>
/// Canonical status codes of a payroll PERIOD (REQ-012 §1.5 — catalog <c>payroll-period-statuses</c>).
/// A period is born <see cref="Generado"/>, is closed into <see cref="Cerrado"/> by the closure of its run
/// (same transaction) and may be annulled into <see cref="Anulado"/> only while no active run points at it.
/// The period aggregate gains its <c>StatusCode</c> column in M2 (PR-2); the codes are canonical from PR-1
/// because the country catalog seeds them.
/// </summary>
public static class PayrollPeriodStatuses
{
    public const string Generado = "GENERADO";
    public const string Cerrado = "CERRADO";
    public const string Anulado = "ANULADO";

    /// <summary>Closed states — no further transition.</summary>
    public static readonly IReadOnlyCollection<string> Terminal = new[] { Cerrado, Anulado };
}
