using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.Common;
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
    bool IsCarryover,
    // H-31 — el desglose de DÍAS, que antes no cruzaba la frontera: la corrida solo recibía dinero, así que no
    // había forma de justificar por qué a alguien se le pagaron 13.5 días y no 15 sin volver a consultar los
    // módulos origen — y para una corrida CERRADA eso significa que editar el registro después movería el
    // reporte de un periodo ya pagado.
    decimal? UnpaidDays = null,
    decimal? EmployerPaidDays = null,
    decimal? SubsidizedDays = null);

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
    IReadOnlyList<PayrollRegistroRow> Incapacities,
    IReadOnlyList<PayrollComplianceExclusion> ComplianceExclusions,
    // H-29/H-30 — por código de concepto, leído UNA vez por corrida.
    IReadOnlyDictionary<string, PayrollConceptClassification> ConceptClassifications);

/// <summary>
/// REQ-016 Gate B (ratified P-11/P-12): an otherwise-eligible employee left OUT of the population because
/// the tenant's payroll compliance gates are on (<c>CompanyPreference.PayrollComplianceGatesEnabled</c>) and
/// the employee is missing NUP ISSS and/or the AFP account number. Only this employee's line is skipped —
/// the rest of the run proceeds normally — surfaced as a header-level warning (no line exists to carry it).
/// </summary>
public sealed record PayrollComplianceExclusion(Guid PersonnelFilePublicId, string EmployeeFullName);

/// <summary>
/// H-29/H-30 — la clasificación del concepto tal como estaba en el catálogo <b>al generar la corrida</b>: el eje
/// del reporte (clase de ingreso o de egreso) y la matriz de afectación a las bases de ISSS/AFP/Renta.
/// <para>
/// Es un SNAPSHOT a propósito. Si el reporte resolviera la clase con un join al catálogo en tiempo de lectura,
/// reclasificar un concepto seis meses después movería los reportes de periodos ya pagados y declarados. Es el
/// mismo criterio que ya se aplicaba al nombre (<c>concept_name_snapshot</c>) y que a la clase se le había
/// olvidado. Y hay una segunda razón: el catálogo está indexado por <c>(country, code)</c>, así que un join solo
/// por código multiplica filas en cuanto exista un segundo país.
/// </para>
/// </summary>
public sealed record PayrollConceptClassification(
    IncomeClass? IncomeClass,
    DeductionClass? DeductionClass,
    bool AffectsIsss,
    bool AffectsAfp,
    bool AffectsRenta)
{
    /// <summary>
    /// Lo que se asume para un concepto que no está en el catálogo. Grava y cotiza: dejar de retener por un código
    /// desconocido es una contingencia legal, retener de más es conservador y visible. La clase queda nula y el
    /// monto cae en el bucket «otros» del reporte, donde se nota.
    /// </summary>
    public static readonly PayrollConceptClassification Unknown =
        new(null, null, AffectsIsss: true, AffectsAfp: true, AffectsRenta: true);

    /// <summary>
    /// Traduce el código del concepto al del catálogo. El motor emite su línea de salario como <c>SALARIO</c>
    /// («Salario del periodo») mientras el catálogo la llama <c>SALARIO_BASE</c>; sin este alias la línea más
    /// grande de la planilla caería en el bucket «otros» del reporte. Los demás códigos del motor coinciden
    /// (<c>HORAS_EXTRA</c>, <c>ISSS</c>, <c>AFP</c>, <c>RENTA</c>) o son pagos patronales, que no llevan clase.
    /// </summary>
    public static string CatalogCodeFor(string conceptCode) =>
        string.Equals(conceptCode, "SALARIO", StringComparison.OrdinalIgnoreCase) ? "SALARIO_BASE" : conceptCode;

    /// <summary>Resuelve la clasificación de un concepto contra el snapshot, con el alias y el default aplicados.</summary>
    public static PayrollConceptClassification Resolve(
        IReadOnlyDictionary<string, PayrollConceptClassification> snapshot, string conceptCode) =>
        snapshot.TryGetValue(CatalogCodeFor(conceptCode), out var found) ? found : Unknown;
}

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
