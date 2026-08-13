using System.Net;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-29 (D3) — los ingresos NO deducibles no cotizan ni tributan. Un viático o un reembolso es plata que se le
/// reintegra al empleado y que está fuera de su salario, así que no entra en la base del ISSS, ni de la AFP, ni de
/// la Renta.
/// <para>
/// El motor siempre supo honrar el eje —<c>PayrollIncomeItem</c> lleva <c>AffectsIsss/Afp/Renta</c> y
/// <c>PayrollCalculation.Rules.cs</c> arma las bases con esas banderas—, pero los dos mapeos de ingresos las
/// fijaban en <c>true</c> a mano y el catálogo de conceptos de planilla no tenía dónde configurarlo (el de
/// finiquitos sí). Resultado: los reembolsos se venían cotizando y gravando.
/// </para>
/// <para>
/// Los dos tests comparan DOS empleados del MISMO salario en la MISMA corrida —uno con el ingreso extra y otro
/// sin él— en vez de dos corridas: una sola generación, sin anular nada, y la diferencia solo puede venir del
/// concepto.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>
    /// Salario 600 → quincena 300 para los dos. Al de los viáticos se le suman 150 de ingreso, pero sus ISSS, AFP y
    /// Renta tienen que quedar **idénticos** a los del compañero. Con las banderas fijas en <c>true</c> cotizaba
    /// sobre 450.
    /// </summary>
    [Fact]
    public async Task Payroll_NonDeductibleIncome_DoesNotEnterTheContributionBases()
    {
        var run = await SeedRunWithExtraIncomeAsync("VIATICOS", "V", "vito");
        using var client = run.Manager;

        var conExtra = await ReadEmployeeTotalsAsync(client, run.TenantId, run.RunId, run.WithExtra);
        var sinExtra = await ReadEmployeeTotalsAsync(client, run.TenantId, run.RunId, run.WithoutExtra);

        // El viático SÍ se paga…
        Assert.Equal(sinExtra.Income + 150m, conExtra.Income);
        // …y NO mueve ninguna de las tres retenciones.
        Assert.Equal(sinExtra.Isss, conExtra.Isss);
        Assert.Equal(sinExtra.Afp, conExtra.Afp);
        Assert.Equal(sinExtra.Renta, conExtra.Renta);

        // Y la línea queda CLASIFICADA para el reporte: el viático como no deducible, el salario como salario
        // (el motor lo emite con el código `SALARIO` y el catálogo lo llama `SALARIO_BASE` — el alias es lo que
        // impide que la línea más grande de la planilla caiga en el bucket «otros»).
        Assert.Equal("NoDeducible", conExtra.IncomeClassByConcept["VIATICOS"]);
        Assert.Equal("Salario", conExtra.IncomeClassByConcept["SALARIO"]);
        Assert.Equal("Ley", conExtra.DeductionClassByConcept["ISSS"]);
        Assert.Equal("Ley", conExtra.DeductionClassByConcept["AFP"]);
        // Los pagos patronales no llevan clase de reporte.
        Assert.Null(conExtra.IncomeClassByConcept["ISSS_PATRONAL"]);
        Assert.Null(conExtra.DeductionClassByConcept["ISSS_PATRONAL"]);
    }

    /// <summary>
    /// El espejo, para que «arreglar» esto poniendo todo en <c>false</c> no pase por bueno: un BONO **sí** cotiza,
    /// así que sube ISSS y AFP respecto del compañero sin bono.
    /// </summary>
    [Fact]
    public async Task Payroll_TaxableIncome_StillEntersTheContributionBases()
    {
        var run = await SeedRunWithExtraIncomeAsync("BONO", "B", "bruno");
        using var client = run.Manager;

        var conBono = await ReadEmployeeTotalsAsync(client, run.TenantId, run.RunId, run.WithExtra);
        var sinBono = await ReadEmployeeTotalsAsync(client, run.TenantId, run.RunId, run.WithoutExtra);

        Assert.Equal(sinBono.Income + 150m, conBono.Income);
        Assert.True(conBono.Isss > sinBono.Isss, $"El bono debe cotizar ISSS: {sinBono.Isss} → {conBono.Isss}");
        Assert.True(conBono.Afp > sinBono.Afp, $"El bono debe cotizar AFP: {sinBono.Afp} → {conBono.Afp}");
        Assert.Equal("Bono", conBono.IncomeClassByConcept["BONO"]);
    }

    /// <summary>
    /// Siembra dos empleados de 600 y un tercero como gestor de RRHH, le registra al primero un ingreso eventual
    /// autorizado del concepto dado (para que el MOTOR lo tome) y genera la corrida.
    /// </summary>
    private async Task<(HttpClient Manager, Guid TenantId, Guid RunId, Guid WithExtra, Guid WithoutExtra)>
        SeedRunWithExtraIncomeAsync(string conceptTypeCode, string suffix, string firstName)
    {
        var scenario = await factory.ResetDatabaseAsync();
        var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var withExtra = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, firstName, "ConExtra", $"EMP-H29-{suffix}1", $"{firstName}.h29@empresa.test");
        var withoutExtra = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, firstName, "SinExtra", $"EMP-H29-{suffix}2", $"{firstName}.sin.h29@empresa.test");
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Gestora", suffix, $"EMP-H29-{suffix}3", $"gestora.{suffix}.h29@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        _ = await CreateAndAuthorizeOneTimeIncomeAsync(
            manager, authorizer, withExtra,
            FixedOneTimeIncomeBody(requesterId, amount: 150, conceptTypeCode: conceptTypeCode));

        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);
        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {payload}");

        using var runDoc = JsonDocument.Parse(payload);
        var runId = runDoc.RootElement.GetProperty("publicId").GetGuid();

        return (manager, scenario.TenantId, runId, withExtra, withoutExtra);
    }

    private static async Task<(decimal Income, decimal Isss, decimal Afp, decimal Renta, IReadOnlyDictionary<string, string?> IncomeClassByConcept, IReadOnlyDictionary<string, string?> DeductionClassByConcept)>
        ReadEmployeeTotalsAsync(HttpClient client, Guid tenantId, Guid runId, Guid fileId)
    {
        var response = await client.GetAsync(
            $"/api/v1/companies/{tenantId}/payroll-runs/{runId}/employees/{fileId}");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"employee lines: {(int)response.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        decimal Deduction(string conceptCode) =>
            root.GetProperty("lines").EnumerateArray()
                .Where(line => line.GetProperty("conceptCode").GetString() == conceptCode
                               && line.GetProperty("lineClass").GetString() == "Descuento"
                               && line.GetProperty("isIncluded").GetBoolean())
                .Sum(line => line.GetProperty("finalAmount").GetDecimal());

        // H-29/H-30 — la clase del reporte, congelada en la línea al generar.
        IReadOnlyDictionary<string, string?> ClassBy(string property) =>
            root.GetProperty("lines").EnumerateArray()
                .GroupBy(line => line.GetProperty("conceptCode").GetString()!)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().GetProperty(property).GetString());

        return (
            root.GetProperty("totalIncome").GetDecimal(),
            Deduction("ISSS"),
            Deduction("AFP"),
            Deduction("RENTA"),
            ClassBy("incomeClass"),
            ClassBy("deductionClass"));
    }
}
