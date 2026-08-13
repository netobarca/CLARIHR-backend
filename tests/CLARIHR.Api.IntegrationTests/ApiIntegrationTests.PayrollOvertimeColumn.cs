using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-29 — la columna `horasExtras` de la matriz, la última que quedaba sin ejercitar. Al cerrar el hueco de las
/// otras columnas dije que ésta «requiere sembrar la cadena de horario y jornada»; **eso era inexacto**: el horario
/// es OPCIONAL —el propio motor dice que «un día de la semana ausente del horario es un día LIBRE, no un dato
/// faltante», así que sin turno toda hora es extra—. Lo que sí es obligatorio es una <b>plaza real</b>, porque la
/// elegibilidad de horas extras vive en <c>position_slots.generates_overtime</c> (H-19) y el candidato de planilla
/// se sembraba con la asignación SIN plaza. Ése era el bloqueo, y es de una línea.
/// <para>
/// El número no se elige: salario 600 → diaria 20 → hora 2.50; un registro de 2 h 30 min al factor 2.00 son
/// <c>2.5 × 2.00 = 5</c> horas-factor × 2.50 = <b>12.50</b>. Es el mismo aritmético del golden 2 del motor.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task PayrollMatrix_OvertimeColumn_IsFedByTheAuthorizedRecord()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var overtimeManager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var overtimeAuthorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, Guid.NewGuid()));
        // La cadena organizativa tiene su propio permiso: el contexto de nómina no crea unidades ni plazas.
        using var structure = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        // La cadena: unidad → perfil publicado → plaza que SÍ genera horas extras.
        var orgUnit = await CreateOrgUnitAsync(structure, scenario.TenantId, "DIR-OT", "Direccion OT", "Direccion");
        var profile = await CreateJobProfileAsync(structure, scenario.TenantId, "JP-OT", "Perfil OT", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(structure, scenario.TenantId, "PS-OT", "Plaza OT", profile.Id, maxEmployees: 2);

        var employeeId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Hora", "Extra", "EMP-OT-MTZ", "hora.extra@empresa.test",
            positionSlotPublicId: slot.Id);
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Gestora", "Overtime", "EMP-OT-MTZ2", "gestora.ot@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var (typeId, justificationId) = await SeedOvertimeMastersAsync(scenario.TenantId);

        // 2 h 30 min al factor 2.00, dentro de la banda nocturna que el helper usa por defecto.
        var (recordId, token) = await CreateOvertimeAsync(
            overtimeManager, employeeId,
            OvertimeBody(typeId, justificationId, requesterId, durationHours: 2, durationMinutes: 30, workDateOffsetDays: 0));

        var authorized = await PatchOvertimeAsync(
            overtimeAuthorizer, employeeId, recordId, "resolution", token,
            new { targetStatusCode = "AUTORIZADA", note = (string?)null });
        var authorizedPayload = await authorized.Content.ReadAsStringAsync();
        Assert.True(
            authorized.IsSuccessStatusCode,
            $"authorize: {(int)authorized.StatusCode} {authorizedPayload}");

        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);
        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var runPayload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {runPayload}");

        using var runDoc = JsonDocument.Parse(runPayload);
        var runId = runDoc.RootElement.GetProperty("publicId").GetGuid();

        var matrix = await manager.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/employees/query", new { pageSize = 50 });
        var payload = await matrix.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == matrix.StatusCode, $"matrix: {(int)matrix.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        var row = document.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("codigoEmpleado").GetString() == "EMP-OT-MTZ");

        // 2.5 h × factor 2.00 = 5 horas-factor × hora 2.50 = 12.50
        Assert.Equal(12.50m, row.GetProperty("horasExtras").GetDecimal());
        // Cae en SU columna, no en «otros».
        Assert.Equal(0m, row.GetProperty("otrosIngresos").GetDecimal());
        Assert.Equal(312.50m, row.GetProperty("ingresoTotal").GetDecimal());   // 300 de quincena + 12.50
    }

    /// <summary>
    /// H-19 — el espejo, que es lo que le da valor al test de arriba: una plaza declarada <b>exenta</b> no acumula
    /// horas extras, y la exención es de la PLAZA, no de la persona. Sin este test, «horasExtras funciona» no dice
    /// nada sobre la regla que H-19 construyó.
    /// </summary>
    [Fact]
    public async Task Overtime_OnAnExemptPosition_IsRejectedWhoeverHoldsIt()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var overtimeManager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var structure = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(structure, scenario.TenantId, "DIR-EX", "Direccion Exenta", "Direccion");
        var profile = await CreateJobProfileAsync(structure, scenario.TenantId, "JP-EX", "Perfil Exento", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(
            structure, scenario.TenantId, "PS-EX", "Plaza Exenta", profile.Id, maxEmployees: 1,
            generatesOvertime: false);

        var employeeId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Director", "Exento", "EMP-OT-EX", "director.exento@empresa.test",
            positionSlotPublicId: slot.Id);
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Gestora", "Exenta", "EMP-OT-EX2", "gestora.exenta@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var (typeId, justificationId) = await SeedOvertimeMastersAsync(scenario.TenantId);

        var response = await overtimeManager.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/overtime-records",
            OvertimeBody(typeId, justificationId, requesterId, durationHours: 2, durationMinutes: 30, workDateOffsetDays: 0));

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "OVERTIME_POSITION_EXEMPT");
    }
}
