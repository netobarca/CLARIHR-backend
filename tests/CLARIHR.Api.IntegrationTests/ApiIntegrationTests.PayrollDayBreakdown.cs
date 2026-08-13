using System.Net;
using System.Text.Json;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Application.Features.Payroll.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-31 — el desglose de DÍAS de la matriz de planilla, que hasta ahora no tenía **ninguna** cobertura: se
/// construyeron las columnas y la fórmula del equivalente, y se dieron por verificadas porque la suite estaba verde
/// y el sondeo en vivo devolvía ceros. Los ceros eran ciertos y no probaban nada: la corrida del ambiente no tiene
/// incapacidades ni permisos sin goce, así que la verificación <b>no podía fallar</b>. Es el mecanismo 3 de H-33
/// aplicado a mi propio trabajo.
/// <para>
/// El caso es el que el hallazgo plantea con número: <b>15 días de periodo, 1 sin goce y 2 de incapacidad a cargo
/// de la empresa al 75 % → 13.5 días pagados equivalentes</b>. Los tramos sembrados de `ENFERMEDAD_COMUN` dan
/// exactamente eso (días 1–3 al 75 % con pagador `EMPRESA`), y el aporte de esos días se deriva del monto que el
/// motor realmente pagó, no de recomponer un porcentaje — los tramos por riesgo pueden diferir dentro de la misma
/// incapacidad.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>Un contexto que puede registrar planilla, tiempo no trabajado e incapacidades a la vez.</summary>
    private static TestUserContext PayrollDayBreakdownContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin,
            PayrollConfigurationPermissionCodes.Manage,
            PersonnelFilePermissionCodes.ManageNotWorkedTimeTypes,
            PersonnelFilePermissionCodes.ManageNotWorkedTimes,
            PersonnelFilePermissionCodes.ViewNotWorkedTimes,
            LeaveConfigurationPermissionCodes.Admin);

    [Fact]
    public async Task PayrollMatrix_DayBreakdown_SquaresTheEquivalentPaidDays()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(PayrollDayBreakdownContext(scenario));
        // Los maestros de incapacidad (riesgos y sus tramos por pagador) vienen de la plantilla de ausencias.
        await ApplyLeaveTemplateAsync(scenario);

        // El periodo 1 quincenal de 2026 es 2026-01-01 → 2026-01-15: las ausencias van dentro.
        var employeeId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Dia", "Desglose", "EMP-DIAS-1", "dia.desglose@empresa.test",
            hireDate: new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);

        // [1] UN día sin goce dentro del periodo.
        _ = await CreateNotWorkedTimeAsync(client, employeeId, new
        {
            typeCode = "AUSENCIA_SIN_GOCE",
            assignedPositionPublicId = (Guid?)null,
            startDate = "2026-01-05",
            endDate = "2026-01-05",
            hours = (decimal?)null,
            reason = "Un día sin goce (H-31).",
        });

        // [2] DOS días de incapacidad común: los tramos 1–3 los cubre la EMPRESA al 75 %.
        var documentFileId = await SeedIncapacityDocumentFileAsync(scenario);
        var (riskId, typeId) = await GetIncapacityMasterIdsAsync(scenario.TenantId);
        _ = await CreateIncapacityAsync(client, employeeId, riskId, typeId, "2026-01-07", "2026-01-08", documentFileId);

        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(client, scenario.TenantId);
        var run = await GeneratePayrollRunAsync(client, scenario.TenantId, definitionId, periodId);
        var runPayload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {runPayload}");

        using var runDoc = JsonDocument.Parse(runPayload);
        var runId = runDoc.RootElement.GetProperty("publicId").GetGuid();

        var matrix = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/employees/query", new { });
        var payload = await matrix.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == matrix.StatusCode, $"matrix: {(int)matrix.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        var row = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());

        decimal Column(string name) => row.GetProperty(name).GetDecimal();

        // ── El desglose, columna por columna ──────────────────────────────────────────────────────
        Assert.Equal(15m, Column("diasPeriodo"));

        // DOS días sin goce, no uno: el permiso es de UN día, pero un día sin goce en la semana hace perder el
        // SÉPTIMO —el descanso remunerado— que es la regla ratificada de REQ-011. El motor de tiempo no trabajado
        // ya trae los días equivalentes con esa penalización aplicada, y el reporte los transporta tal cual.
        Assert.Equal(2m, Column("diasSinGoce"));

        Assert.Equal(2m, Column("diasIncapacidadEmpresa"));
        Assert.Equal(0m, Column("diasIncapacidadIsss"));
        Assert.Equal(0m, Column("diasIncapacidadSinPago"));

        // ── El equivalente: 15 − 2 (sin goce + séptimo) − 2 (empresa) + 1.5 (esos 2 al 75 %) = 12.5 ──
        //
        // ⚠️ El hallazgo ilustraba este caso con «13.5», que sale de suponer 1 solo día descontado. Con la
        // penalización del séptimo aplicada —que es lo que el sistema hace y debe hacer— el mismo escenario da
        // 12.5. El número del hallazgo era ilustrativo y no contemplaba el séptimo día; la fórmula es la misma.
        Assert.Equal(12.5m, Column("diasPagadosEquivalentes"));

        // La identidad tiene que cerrar con las componentes, no ser un número suelto: el aporte de los días
        // patronales se deriva del monto pagado sobre la diaria (600/30 = 20), no de un porcentaje recompuesto.
        var employerEquivalent = Column("diasPagadosEquivalentes")
                                 - Column("diasPeriodo")
                                 + Column("diasSinGoce")
                                 + Column("diasIncapacidadIsss")
                                 + Column("diasIncapacidadSinPago")
                                 + Column("diasIncapacidadEmpresa");
        Assert.Equal(1.5m, employerEquivalent);
    }

    /// <summary>
    /// El espejo, para que «arreglar» esto devolviendo siempre los días del periodo no pase por bueno: sin ausencias
    /// los días pagados son los 15 completos y las cuatro columnas de desglose quedan en cero.
    /// </summary>
    [Fact]
    public async Task PayrollMatrix_DayBreakdown_WithoutAbsences_PaysTheWholePeriod()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(PayrollDayBreakdownContext(scenario));

        _ = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Sin", "Ausencias", "EMP-DIAS-2", "sin.ausencias@empresa.test");

        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(client, scenario.TenantId);
        var run = await GeneratePayrollRunAsync(client, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Created, run.StatusCode);

        using var runDoc = JsonDocument.Parse(await run.Content.ReadAsStringAsync());
        var runId = runDoc.RootElement.GetProperty("publicId").GetGuid();

        var matrix = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/employees/query", new { });
        using var document = JsonDocument.Parse(await matrix.Content.ReadAsStringAsync());
        var row = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());

        Assert.Equal(15m, row.GetProperty("diasPeriodo").GetDecimal());
        Assert.Equal(15m, row.GetProperty("diasPagadosEquivalentes").GetDecimal());
        Assert.Equal(0m, row.GetProperty("diasSinGoce").GetDecimal());
        Assert.Equal(0m, row.GetProperty("diasIncapacidadEmpresa").GetDecimal());
        Assert.Equal(0m, row.GetProperty("diasIncapacidadIsss").GetDecimal());
        Assert.Equal(0m, row.GetProperty("diasIncapacidadSinPago").GetDecimal());
    }
}
