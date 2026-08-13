using System.Net;
using System.Text.Json;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-29 — la matriz de la planilla: una fila por empleado. Los cinco exports que existían tenían otro grano (una
/// fila por línea, o solo el neto, o solo carga patronal, o mensual en vez de por corrida) y en JSON el único
/// listado por empleado exigía decir de qué empleado, así que armar la matriz de 59 pedía 59 llamadas y pivotear
/// en el navegador.
/// <para>
/// Lo que estos tests fijan: que el pivote **cuadre contra la cabecera de la corrida** —es un reporte fiscal—, que
/// las columnas salgan del eje del catálogo y no de una lista de códigos, que un multi-plaza sea UNA fila con los
/// días del periodo tomados una vez, y que el JSON y el Excel den el mismo número.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task PayrollMatrix_PivotsEveryColumn_AndSquaresAgainstTheRunHeader()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        // Tres empleados de 600: uno con bono, uno con viático, uno pelado. El gestor de RRHH es el solicitante.
        var conBono = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Ana", "Bono", "EMP-MTZ-1", "ana.mtz@empresa.test");
        var conViatico = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Beto", "Viatico", "EMP-MTZ-2", "beto.mtz@empresa.test");
        var pelado = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Caro", "Simple", "EMP-MTZ-3", "caro.mtz@empresa.test");
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Gestora", "Matriz", "EMP-MTZ-4", "gestora.mtz@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        _ = await CreateAndAuthorizeOneTimeIncomeAsync(
            manager, authorizer, conBono, FixedOneTimeIncomeBody(requesterId, amount: 150, conceptTypeCode: "BONO"));
        _ = await CreateAndAuthorizeOneTimeIncomeAsync(
            manager, authorizer, conViatico, FixedOneTimeIncomeBody(requesterId, amount: 80, conceptTypeCode: "VIATICOS"));

        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);
        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var runPayload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {runPayload}");

        Guid runId;
        decimal headerIncome, headerDeductions, headerNet;
        using (var runDoc = JsonDocument.Parse(runPayload))
        {
            var root = runDoc.RootElement;
            runId = root.GetProperty("publicId").GetGuid();
            headerIncome = root.GetProperty("totalIncome").GetDecimal();
            headerDeductions = root.GetProperty("totalDeductions").GetDecimal();
            headerNet = root.GetProperty("totalNet").GetDecimal();
        }

        var matrix = await manager.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/employees/query", new { pageSize = 50 });
        var payload = await matrix.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == matrix.StatusCode, $"matrix: {(int)matrix.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        var response = document.RootElement;
        Assert.Equal(4, response.GetProperty("totalCount").GetInt32());

        var items = response.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(4, items.Length);
        // Orden por nombre del empleado.
        Assert.Equal("Ana Bono", items[0].GetProperty("empleado").GetString());

        // ── Las columnas salen del eje del catálogo ────────────────────────────────────────────────
        var ana = items.Single(row => row.GetProperty("codigoEmpleado").GetString() == "EMP-MTZ-1");
        var beto = items.Single(row => row.GetProperty("codigoEmpleado").GetString() == "EMP-MTZ-2");
        var caro = items.Single(row => row.GetProperty("codigoEmpleado").GetString() == "EMP-MTZ-3");

        Assert.Equal(150m, ana.GetProperty("bonos").GetDecimal());
        Assert.Equal(0m, ana.GetProperty("ingresosAdicionales").GetDecimal());
        Assert.Equal(80m, beto.GetProperty("ingresosAdicionales").GetDecimal());
        Assert.Equal(0m, beto.GetProperty("bonos").GetDecimal());
        Assert.Equal(0m, caro.GetProperty("bonos").GetDecimal());

        // El salario NO cae en «otros»: el motor emite la línea como SALARIO y el catálogo la llama SALARIO_BASE.
        Assert.Equal(300m, caro.GetProperty("salarioDelPeriodo").GetDecimal());
        Assert.Equal(600m, caro.GetProperty("salarioBase").GetDecimal());
        Assert.Equal(0m, caro.GetProperty("otrosIngresos").GetDecimal());
        Assert.Equal(15m, caro.GetProperty("diasPeriodo").GetDecimal());
        Assert.Equal(15m, caro.GetProperty("diasPagadosEquivalentes").GetDecimal());

        // Las retenciones de ley tienen columna propia y no caen en «otros descuentos».
        Assert.True(caro.GetProperty("isss").GetDecimal() > 0m);
        Assert.True(caro.GetProperty("afp").GetDecimal() > 0m);
        Assert.Equal(0m, caro.GetProperty("otrosDescuentos").GetDecimal());

        // Y el viático no cotizó: mismas retenciones que el compañero sin ingreso extra (H-29 D3).
        Assert.Equal(caro.GetProperty("isss").GetDecimal(), beto.GetProperty("isss").GetDecimal());
        Assert.Equal(caro.GetProperty("afp").GetDecimal(), beto.GetProperty("afp").GetDecimal());

        // ── El cuadre, fila por fila y contra la cabecera ──────────────────────────────────────────
        foreach (var row in items)
        {
            Assert.Equal(
                row.GetProperty("ingresoTotal").GetDecimal() - row.GetProperty("totalDescuentos").GetDecimal(),
                row.GetProperty("liquidoAPagar").GetDecimal());
        }

        var totals = response.GetProperty("totales");
        Assert.Equal(headerIncome, totals.GetProperty("ingresoTotal").GetDecimal());
        Assert.Equal(headerDeductions, totals.GetProperty("totalDescuentos").GetDecimal());
        Assert.Equal(headerNet, totals.GetProperty("liquidoAPagar").GetDecimal());
        // Los días del periodo NO se suman por plaza: 4 empleados × 15.
        Assert.Equal(60m, totals.GetProperty("diasPeriodo").GetDecimal());

        // ── El Excel sale de la misma superficie ───────────────────────────────────────────────────
        var export = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/employees/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var csv = await export.Content.ReadAsStringAsync();
        Assert.Contains("EMP-MTZ-1", csv);
        // La última fila del archivo es el TOTAL, para cuadrar sin sumar a mano.
        Assert.Contains("TOTAL", csv);
    }

    /// <summary>
    /// H-29 (D4) — un empleado con DOS plazas es UNA fila, y sus días del periodo se toman una vez. Medido en el
    /// ambiente, sumarlos daba 900 días para 59 empleados. El reporte patronal que ya existía agrupa por nombre +
    /// centro de costo, así que este caso lo partiría en dos filas si tuvieran centros distintos.
    /// </summary>
    [Fact]
    public async Task PayrollMatrix_MultiPlazaEmployee_IsOneRowWithThePeriodDaysCountedOnce()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        var fileId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Doble", "Plaza", "EMP-MTZ-MP", "doble.mtz@empresa.test");
        await SeedSecondPayrollPlazaAsync(scenario.TenantId, fileId, monthlySalary: 300m);

        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);
        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var runPayload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {runPayload}");

        using var runDoc = JsonDocument.Parse(runPayload);
        var runId = runDoc.RootElement.GetProperty("publicId").GetGuid();
        var headerIncome = runDoc.RootElement.GetProperty("totalIncome").GetDecimal();

        var matrix = await manager.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/employees/query", new { });
        var payload = await matrix.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == matrix.StatusCode, $"matrix: {(int)matrix.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        var row = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());

        // UNA fila, con el salario de las DOS plazas sumado…
        Assert.Equal(900m, row.GetProperty("salarioBase").GetDecimal());   // 600 + 300 mensuales
        Assert.Equal(450m, row.GetProperty("salarioDelPeriodo").GetDecimal()); // 300 + 150 de quincena
        Assert.Equal(headerIncome, row.GetProperty("ingresoTotal").GetDecimal());
        // …y los días del periodo contados UNA vez.
        Assert.Equal(15m, row.GetProperty("diasPeriodo").GetDecimal());
    }

    /// <summary>
    /// H-29 (D4) — una SEGUNDA plaza activa para el mismo expediente, con su propio salario. No es primaria (la
    /// regla de una sola primaria activa) y comparte el centro de costo de la primera.
    /// </summary>
    private async Task SeedSecondPayrollPlazaAsync(Guid tenantId, Guid filePublicId, decimal monthlySalary)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // `IgnoreQueryFilters` porque el scope de siembra no tiene contexto de tenant y el filtro global
        // escondería las filas que se acaban de crear.
        var file = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.PublicId == filePublicId);
        var firstAssignment = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .IgnoreQueryFilters()
            .Where(item => item.PersonnelFileId == file.Id)
            .OrderBy(item => item.Id)
            .FirstAsync();

        var assignment = PersonnelFileEmploymentAssignment.Create(
            "INDEFINIDO",
            contractTypeCode: null,
            workdayCode: null,
            payrollTypeCode: "QUINCENAL",
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: firstAssignment.CostCenterPublicId,
            startDate: DateOnly.FromDateTime(PayrollRunHireDate),
            endDate: null,
            isPrimary: false,
            isActive: true,
            notes: "Segunda plaza (multi-plaza).");
        assignment.BindToPersonnelFile(file.Id);
        assignment.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmploymentAssignment>().Add(assignment);
        await dbContext.SaveChangesAsync();

        var salary = PersonnelFileCompensationConcept.Create(
            assignment.PublicId,
            CompensationNature.Ingreso,
            "SALARIO_BASE",
            deductionClass: null,
            CompensationCalculationType.Fixed,
            monthlySalary,
            calculationBaseCode: null,
            employerRate: null,
            contributionCap: null,
            currencyCode: "USD",
            payPeriodCode: "MENSUAL",
            counterpartyName: null,
            externalReference: null,
            startDate: PayrollRunHireDate,
            endDate: null,
            isActive: true,
            isSystemSuggested: false,
            notes: null);
        salary.BindToPersonnelFile(file.Id);
        salary.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileCompensationConcept>().Add(salary);
        await dbContext.SaveChangesAsync();
    }
}
