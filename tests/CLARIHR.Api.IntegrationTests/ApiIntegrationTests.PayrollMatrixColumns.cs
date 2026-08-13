using System.Net;
using System.Text.Json;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Compensation;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-29/H-30 — las columnas del reporte que se habían construido y **nunca se ejercitaron**. El plan aprobado pedía
/// una corrida con bono, comisión, viáticos, aguinaldo y un descuento externo y uno interno; se entregó con bono y
/// viáticos nada más, así que `descuentosExternos` y `descuentosInternos` —las dos columnas para las que existe
/// H-30— tenían <b>cero</b> tests. El sondeo en vivo tampoco las cubría: la corrida del ambiente no tiene esos
/// insumos, así que devolvían 0 y ese 0 se leyó como señal buena.
/// <para>
/// Acá se siembran los insumos y se afirma columna por columna, y además se fija <b>la razón de existir de
/// H-30</b>: reclasificar el concepto en el catálogo DESPUÉS de generar no mueve el reporte. Si la clase se
/// resolviera con un join en tiempo de lectura, el reporte de un periodo ya pagado cambiaría.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task PayrollMatrix_EveryColumn_IsFedByItsOwnConcept()
    {
        var (client, tenantId, runId, employeeId) = await SeedRunWithEveryConceptAsync();
        using var manager = client;

        var row = await ReadMatrixRowAsync(manager, tenantId, runId, employeeId);
        decimal Column(string name) => row.GetProperty(name).GetDecimal();

        // ── Ingresos: cada concepto en SU columna, y nada en «otros» ──────────────────────────────
        Assert.Equal(300m, Column("salarioDelPeriodo"));   // 600 mensual / 2
        Assert.Equal(150m, Column("bonos"));
        Assert.Equal(120m, Column("comisiones"));
        Assert.Equal(80m, Column("ingresosAdicionales"));  // VIATICOS: no deducible
        Assert.Equal(200m, Column("aguinaldo"));           // columna propia, no «ingreso adicional»
        Assert.Equal(0m, Column("otrosIngresos"));
        Assert.Equal(850m, Column("ingresoTotal"));

        // ── Descuentos: las dos columnas de H-30, que no tenían ninguna cobertura ─────────────────
        Assert.Equal(75m, Column("descuentosInternos"));   // DANO_EQUIPO
        Assert.Equal(90m, Column("descuentosExternos"));   // PRESTAMO_BANCARIO
        Assert.True(Column("isss") > 0m);
        Assert.True(Column("afp") > 0m);
        Assert.Equal(0m, Column("otrosDescuentos"));

        // ── Y el cuadre de la fila ────────────────────────────────────────────────────────────────
        Assert.Equal(Column("ingresoTotal") - Column("totalDescuentos"), Column("liquidoAPagar"));

        // El aguinaldo y los viáticos NO cotizan; el bono y la comisión SÍ. La base previsional es entonces
        // 300 + 150 + 120 = 570, no los 850 del ingreso total. La AFP lo prueba al centavo: 7.25 % de 570 = 41.33.
        // (El ISSS no se afirma exacto porque su tope entra en juego y ese no es lo que este test fija.)
        Assert.Equal(41.33m, Column("afp"));
    }

    /// <summary>
    /// H-30 — la razón de existir del snapshot: se reclasifica `PRESTAMO_BANCARIO` de <c>Externo</c> a
    /// <c>Interno</c> en el catálogo **después** de generar, y el reporte no se mueve. Con un join en tiempo de
    /// lectura, los $90 saltarían de columna en un periodo ya pagado y declarado.
    /// </summary>
    [Fact]
    public async Task PayrollMatrix_ReclassifyingTheConceptAfterGenerating_DoesNotMoveTheReport()
    {
        var (client, tenantId, runId, employeeId) = await SeedRunWithEveryConceptAsync();
        using var manager = client;

        var before = await ReadMatrixRowAsync(manager, tenantId, runId, employeeId);
        Assert.Equal(90m, before.GetProperty("descuentosExternos").GetDecimal());
        Assert.Equal(75m, before.GetProperty("descuentosInternos").GetDecimal());

        // El catálogo cambia de opinión seis meses después.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE compensation_concept_type_catalog_items SET default_deduction_class = 'Interno' WHERE code = 'PRESTAMO_BANCARIO';");
        }

        var after = await ReadMatrixRowAsync(manager, tenantId, runId, employeeId);

        // Idéntico: la clase viajó congelada en la línea al generar.
        Assert.Equal(90m, after.GetProperty("descuentosExternos").GetDecimal());
        Assert.Equal(75m, after.GetProperty("descuentosInternos").GetDecimal());
        Assert.Equal(
            before.GetProperty("totalDescuentos").GetDecimal(),
            after.GetProperty("totalDescuentos").GetDecimal());
    }

    /// <summary>
    /// Un empleado de 600 con un ingreso de cada clase y un descuento de cada clase, y la corrida generada.
    /// </summary>
    private async Task<(HttpClient Manager, Guid TenantId, Guid RunId, Guid EmployeeId)> SeedRunWithEveryConceptAsync()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var incomeAuthorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var employeeId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Todas", "Columnas", "EMP-COL-1", "todas.columnas@empresa.test");
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Gestora", "Columnas", "EMP-COL-2", "gestora.columnas@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // Un ingreso de cada clase del eje.
        foreach (var (code, amount) in new[] { ("BONO", 150), ("COMISION", 120), ("VIATICOS", 80), ("AGUINALDO", 200) })
        {
            _ = await CreateAndAuthorizeOneTimeIncomeAsync(
                manager, incomeAuthorizer, employeeId,
                FixedOneTimeIncomeBody(requesterId, amount: amount, conceptTypeCode: code));
        }

        // Un descuento INTERNO y uno EXTERNO — las dos columnas de H-30.
        _ = await CreateAndAuthorizeOneTimeDeductionAsync(
            scenario, manager, employeeId, requesterId,
            FixedOneTimeDeductionBody(requesterId, amount: 75m, conceptTypeCode: "DANO_EQUIPO", payrollTypeCode: "QUINCENAL"));
        _ = await CreateAndAuthorizeOneTimeDeductionAsync(
            scenario, manager, employeeId, requesterId,
            FixedOneTimeDeductionBody(requesterId, amount: 90m, conceptTypeCode: "PRESTAMO_BANCARIO", payrollTypeCode: "QUINCENAL"));

        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);
        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {payload}");

        using var runDoc = JsonDocument.Parse(payload);
        return (manager, scenario.TenantId, runDoc.RootElement.GetProperty("publicId").GetGuid(), employeeId);
    }

    private static async Task<JsonElement> ReadMatrixRowAsync(
        HttpClient client, Guid tenantId, Guid runId, Guid employeeId)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{tenantId}/payroll-runs/{runId}/employees/query", new { pageSize = 50 });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"matrix: {(int)response.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("codigoEmpleado").GetString() == "EMP-COL-1")
            .Clone();
    }
}
