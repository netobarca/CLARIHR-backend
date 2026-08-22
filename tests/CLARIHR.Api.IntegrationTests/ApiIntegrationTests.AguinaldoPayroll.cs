using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Compensation;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// La nómina ANUAL de aguinaldo, punta a punta. El requerimiento (2026-08-12) pide seis cosas y cada test fija
/// una: proporcionalidad por fecha de ingreso, año completo, tramos por antigüedad, exención parcial de Renta,
/// ventana legal de pago (20-oct → 20-dic) y que la corrida <b>solo</b> muestre el aguinaldo.
/// <para>
/// Los montos no se eligen: salen de la ley y de la aritmética fijada en REQ-012 (diaria = mensual/30, divisor
/// anual 365, tramos 15/19/21 del Art. 198).
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private const int AguinaldoYear = 2026;

    /// <summary>
    /// El corazón del requerimiento §6: la corrida de aguinaldo <b>solo</b> paga aguinaldo. Ni salario del
    /// periodo ni los cinco pools — y el descuento sembrado, que en una quincenal sí se cobraría, queda
    /// intacto para su nómina ordinaria.
    /// </summary>
    [Fact]
    public async Task AguinaldoRun_PaysTheAguinaldoAndNothingElse()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        // Ingreso en 2020: el año 2026 se devenga completo y la antigüedad cae en el tramo de 19 días.
        var employeeId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Aguinaldo", "Completo", "EMP-AGU-1", "aguinaldo.completo@empresa.test",
            hireDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Gestora", "Aguinaldo", "EMP-AGU-1B", "gestora.aguinaldo@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // Un descuento AUTORIZADO y pendiente. En una corrida quincenal se cobraría; en la de aguinaldo no
        // debe ni aparecer ni consumirse. Sembrarlo es lo que le da valor al test: sin él, «no hay pools»
        // sería cierto por ausencia de insumos y no por la regla.
        var (deductionId, _) = await CreateAndAuthorizeOneTimeDeductionAsync(
            scenario, manager, employeeId, requesterId,
            FixedOneTimeDeductionBody(requesterId, amount: 75m, conceptTypeCode: "DANO_EQUIPO", payrollTypeCode: "QUINCENAL"));

        await SetAguinaldoPaymentDateAsync(scenario.TenantId, month: 10, day: 25);
        var (definitionId, periodId) = await CreateAguinaldoPayrollAsync(manager, scenario.TenantId);

        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {payload}");

        var lines = await ReadRunLinesAsync(manager, scenario.TenantId, payload, employeeId);

        // (600/30) × 19 días × 365/365 = 380.00
        var aguinaldo = Assert.Single(lines, line => line.GetProperty("conceptCode").GetString() == "AGUINALDO");
        Assert.Equal(380m, aguinaldo.GetProperty("calculatedAmount").GetDecimal());
        Assert.Equal(365m, aguinaldo.GetProperty("units").GetDecimal());   // los días devengados justifican el monto

        // Y lo que NO debe estar.
        Assert.DoesNotContain(lines, line => line.GetProperty("conceptCode").GetString() == "SALARIO");
        Assert.DoesNotContain(lines, line => line.GetProperty("conceptCode").GetString() == "ISSS");
        Assert.DoesNotContain(lines, line => line.GetProperty("conceptCode").GetString() == "AFP");
        Assert.DoesNotContain(lines, line => line.GetProperty("conceptCode").GetString() == "DANO_EQUIPO");

        // Y el descuento sigue INTACTO: no basta con que no salga en la corrida, tiene que quedar disponible
        // para su nómina ordinaria. Si la corrida de aguinaldo lo hubiera consumido, aquí diría APLICADO.
        var deduction = await manager.GetAsync(
            $"/api/v1/personnel-files/{employeeId}/one-time-deductions/{deductionId}");
        deduction.EnsureSuccessStatusCode();
        using var deductionDoc = JsonDocument.Parse(await deduction.Content.ReadAsStringAsync());
        Assert.Equal("AUTORIZADO", deductionDoc.RootElement.GetProperty("statusCode").GetString());
    }

    /// <summary>
    /// Regenerar la MISMA corrida no paga el aguinaldo dos veces. Es un camino distinto al del candado de la
    /// segunda nómina —pasa por el handler de revisión, que vuelve a crear las líneas— y por eso necesita su
    /// propio test: ahí es donde una duplicación pasaría inadvertida.
    /// </summary>
    [Fact]
    public async Task AguinaldoRun_Regenerated_StillPaysExactlyOnce()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        var employeeId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Aguinaldo", "Regenerado", "EMP-AGU-7", "aguinaldo.regenerado@empresa.test",
            hireDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await SetAguinaldoPaymentDateAsync(scenario.TenantId, month: 10, day: 25);
        var (definitionId, periodId) = await CreateAguinaldoPayrollAsync(manager, scenario.TenantId);

        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {payload}");

        Guid runId;
        Guid token;
        decimal firstTotal;
        using (var document = JsonDocument.Parse(payload))
        {
            runId = document.RootElement.GetProperty("publicId").GetGuid();
            token = document.RootElement.GetProperty("concurrencyToken").GetGuid();
            firstTotal = document.RootElement.GetProperty("totalIncome").GetDecimal();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/regeneration");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        var regenerated = await manager.SendAsync(request);
        var regeneratedPayload = await regenerated.Content.ReadAsStringAsync();
        Assert.True(
            HttpStatusCode.OK == regenerated.StatusCode,
            $"regenerate: {(int)regenerated.StatusCode} {regeneratedPayload}");

        // El total no se movió y sigue habiendo UNA sola línea de aguinaldo.
        using (var document = JsonDocument.Parse(regeneratedPayload))
        {
            Assert.Equal(firstTotal, document.RootElement.GetProperty("totalIncome").GetDecimal());
        }

        var lines = await ReadRunLinesAsync(manager, scenario.TenantId, regeneratedPayload, employeeId);
        var aguinaldo = Assert.Single(lines, line => line.GetProperty("conceptCode").GetString() == "AGUINALDO");
        Assert.Equal(380m, aguinaldo.GetProperty("calculatedAmount").GetDecimal());
    }

    /// <summary>
    /// Requerimiento §2: quien ingresó a mitad de año cobra la parte proporcional. Del 1 de agosto al 12 de
    /// diciembre hay 133 días; con menos de un año el tramo es de 15: 20 × 15 × 133/365 = <b>109.32</b>.
    /// </summary>
    [Fact]
    public async Task AguinaldoRun_MidYearHire_IsProportionalToTheDaysWorked()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        var employeeId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Aguinaldo", "Proporcional", "EMP-AGU-2", "aguinaldo.proporcional@empresa.test",
            hireDate: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        await SetAguinaldoPaymentDateAsync(scenario.TenantId, month: 10, day: 25);
        var (definitionId, periodId) = await CreateAguinaldoPayrollAsync(manager, scenario.TenantId);

        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {payload}");

        var lines = await ReadRunLinesAsync(manager, scenario.TenantId, payload, employeeId);
        var aguinaldo = Assert.Single(lines, line => line.GetProperty("conceptCode").GetString() == "AGUINALDO");

        Assert.Equal(109.32m, aguinaldo.GetProperty("calculatedAmount").GetDecimal());
        Assert.Equal(133m, aguinaldo.GetProperty("units").GetDecimal());
    }

    /// <summary>
    /// Requerimiento §4 con sus propios números: aguinaldo de <b>1,600</b> y exención de <b>1,500</b> → la
    /// Renta cae sobre <b>100</b>, no sobre 1,600. El salario sale de despejar 15 días de (X/30) = 1,600.
    /// <para>
    /// La aserción fuerte no es el monto exento sino la <b>base de la retención</b>: es lo único que prueba
    /// que la exención llegó hasta el cálculo del impuesto y no se quedó en un campo decorativo.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AguinaldoRun_TaxesOnlyTheExcessOverTheExemption()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        var employeeId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Aguinaldo", "Gravado", "EMP-AGU-3", "aguinaldo.gravado@empresa.test",
            monthlySalary: 3200m,
            hireDate: new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        await SeedAguinaldoExemptionAsync(scenario.TenantId, AguinaldoYear, 1500m);
        await SeedFlatRentaTableAsync(manager);
        await SetAguinaldoPaymentDateAsync(scenario.TenantId, month: 10, day: 25);
        var (definitionId, periodId) = await CreateAguinaldoPayrollAsync(manager, scenario.TenantId);

        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {payload}");

        var lines = await ReadRunLinesAsync(manager, scenario.TenantId, payload, employeeId);

        var aguinaldo = Assert.Single(lines, line => line.GetProperty("conceptCode").GetString() == "AGUINALDO");
        Assert.Equal(1600m, aguinaldo.GetProperty("calculatedAmount").GetDecimal());
        Assert.Equal(1500m, aguinaldo.GetProperty("exemptAmount").GetDecimal());

        // La base de Renta es el excedente: 1,600 − 1,500 = 100. Sin ISSS ni AFP que restar, porque el
        // aguinaldo no cotiza.
        var renta = Assert.Single(lines, line => line.GetProperty("conceptCode").GetString() == "RENTA");
        Assert.Equal(100m, renta.GetProperty("baseAmount").GetDecimal());
        Assert.Equal(10m, renta.GetProperty("calculatedAmount").GetDecimal());   // 10 % de 100
    }

    /// <summary>
    /// Sin exención registrada se grava TODO y la corrida lo dice. El espejo del test anterior: prueba que el
    /// 100 de allá viene de la exención y no de una casualidad del tramo.
    /// </summary>
    [Fact]
    public async Task AguinaldoRun_WithoutAnExemption_TaxesEverythingAndWarns()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        var employeeId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Aguinaldo", "SinExencion", "EMP-AGU-4", "aguinaldo.sinexencion@empresa.test",
            monthlySalary: 3200m,
            hireDate: new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        await SeedFlatRentaTableAsync(manager);
        await SetAguinaldoPaymentDateAsync(scenario.TenantId, month: 10, day: 25);
        var (definitionId, periodId) = await CreateAguinaldoPayrollAsync(manager, scenario.TenantId);

        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {payload}");

        var lines = await ReadRunLinesAsync(manager, scenario.TenantId, payload, employeeId);
        var aguinaldo = Assert.Single(lines, line => line.GetProperty("conceptCode").GetString() == "AGUINALDO");
        Assert.Equal(0m, aguinaldo.GetProperty("exemptAmount").GetDecimal());

        var renta = Assert.Single(lines, line => line.GetProperty("conceptCode").GetString() == "RENTA");
        Assert.Equal(1600m, renta.GetProperty("baseAmount").GetDecimal());
        Assert.Equal(160m, renta.GetProperty("calculatedAmount").GetDecimal());   // 10 % de 1,600

        using var runDoc = JsonDocument.Parse(payload);
        var warnings = runDoc.RootElement.GetProperty("warnings").EnumerateArray()
            .Select(warning => warning.GetProperty("code").GetString())
            .ToArray();
        Assert.Contains("PAYROLL_WARNING_AGUINALDO_NO_EXEMPTION", warnings);
    }

    /// <summary>
    /// La población de una nómina de aguinaldo son TODOS los activos, sin importar el tipo de planilla de su
    /// plaza. Sin esto, el mensual y el quincenal necesitarían dos nóminas de aguinaldo y cada una pagaría a
    /// medias la lista.
    /// </summary>
    [Fact]
    public async Task AguinaldoRun_CoversEveryActiveEmployee_WhateverTheirOrdinaryPayrollType()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        // El helper siembra la plaza con tipo de planilla QUINCENAL; la nómina de aguinaldo declara MENSUAL.
        var quincenal = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Quincenal", "Incluido", "EMP-AGU-5", "quincenal.incluido@empresa.test",
            hireDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await SetAguinaldoPaymentDateAsync(scenario.TenantId, month: 12, day: 15);
        var (definitionId, periodId) = await CreateAguinaldoPayrollAsync(
            manager, scenario.TenantId, payrollTypeCode: "MENSUAL");

        var run = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await run.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == run.StatusCode, $"generate: {(int)run.StatusCode} {payload}");

        var lines = await ReadRunLinesAsync(manager, scenario.TenantId, payload, quincenal);
        Assert.Single(lines, line => line.GetProperty("conceptCode").GetString() == "AGUINALDO");
    }

    /// <summary>
    /// El candado anti-pago-doble: una segunda nómina de aguinaldo del MISMO año no genera. El aguinaldo es
    /// uno por empleado y por año, y las dos corridas serían válidas por separado.
    /// </summary>
    [Fact]
    public async Task AguinaldoRun_ASecondPayrollForTheSameYear_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        _ = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Aguinaldo", "Doble", "EMP-AGU-6", "aguinaldo.doble@empresa.test",
            hireDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await SetAguinaldoPaymentDateAsync(scenario.TenantId, month: 10, day: 25);

        var (firstDefinition, firstPeriod) = await CreateAguinaldoPayrollAsync(manager, scenario.TenantId);
        var first = await GeneratePayrollRunAsync(manager, scenario.TenantId, firstDefinition, firstPeriod);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Otra nómina de aguinaldo, del mismo año, con su propio calendario.
        var (secondDefinition, secondPeriod) = await CreateAguinaldoPayrollAsync(
            manager, scenario.TenantId, code: "NOM-AGUINALDO-2");
        var second = await GeneratePayrollRunAsync(manager, scenario.TenantId, secondDefinition, secondPeriod);

        await AssertProblemDetailsAsync(second, HttpStatusCode.Conflict, "PAYROLL_AGUINALDO_ALREADY_PAID_FOR_YEAR");
    }

    /// <summary>Requerimiento §5: una fecha de pago fuera de la ventana legal 20-oct → 20-dic se rechaza.</summary>
    [Theory]
    [InlineData(10, 19)]
    [InlineData(12, 21)]
    [InlineData(1, 15)]
    public async Task AguinaldoPaymentDate_OutsideTheLegalWindow_IsRejected(int month, int day)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(AguinaldoPreferencesContext(scenario));

        var response = await PutAguinaldoPaymentDateAsync(manager, scenario.TenantId, month, day);

        // 400 y `common.validation`: es un fallo de VALIDACIÓN de la petición (FluentValidation), no una regla
        // de negocio que dependa del estado del sistema. El código específico viaja en `errors`.
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "common.validation");
        // El código del validador no viaja en `code` (ahí va `common.validation`); lo identificable es el
        // mensaje, que es el mismo texto registrado en los dos .resx.
        Assert.Contains(
            "The aguinaldo payment date must fall between October 20 and December 20.",
            await response.Content.ReadAsStringAsync());
    }

    /// <summary>Y el 20 de octubre —el primer día de la ventana— sí se acepta: el borde no es exclusivo.</summary>
    [Fact]
    public async Task AguinaldoPaymentDate_OnTheFirstDayOfTheWindow_IsAccepted()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(AguinaldoPreferencesContext(scenario));

        var response = await PutAguinaldoPaymentDateAsync(manager, scenario.TenantId, month: 10, day: 20);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"preferences: {(int)response.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        Assert.Equal(10, document.RootElement.GetProperty("aguinaldoPaymentMonth").GetInt32());
        Assert.Equal(20, document.RootElement.GetProperty("aguinaldoPaymentDay").GetInt32());
    }

    /// <summary>
    /// Generar el calendario de una nómina de aguinaldo sin fecha de pago configurada falla con un código
    /// propio, en vez de producir un periodo que paga el 31 de diciembre — fuera de la ventana legal.
    /// </summary>
    [Fact]
    public async Task AguinaldoCalendar_WithoutAConfiguredPaymentDate_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        var definitionId = await CreateAguinaldoDefinitionAsync(manager, scenario.TenantId, "NOM-AGU-SINFECHA");
        var calendar = await manager.PostAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-definitions/{definitionId}/periods/generate?year={AguinaldoYear}",
            content: null);

        await AssertProblemDetailsAsync(
            calendar, HttpStatusCode.UnprocessableEntity, "AGUINALDO_PAYMENT_DATE_NOT_CONFIGURED");
    }

    /// <summary>Una nómina de aguinaldo con frecuencia quincenal se rechaza: generaría 24 aguinaldos.</summary>
    [Fact]
    public async Task AguinaldoPayroll_WithANonAnnualFrequency_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        var response = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-definitions",
            new
            {
                code = "NOM-AGU-MALA",
                name = "Aguinaldo mal configurado",
                payrollTypeCode = "QUINCENAL",
                payPeriodCode = "QUINCENAL",
                totalPeriods = 24,
                currencyCode = "USD",
                purposeCode = "AGUINALDO",
            });

        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "common.validation");
        Assert.Contains(
            "An aguinaldo payroll must use the ANUAL pay period.",
            await response.Content.ReadAsStringAsync());
    }

    // ── Andamiaje ────────────────────────────────────────────────────────────────────────────────

    /// <summary>La fecha de pago del aguinaldo, por la vía pública (ejercita el validador de la ventana).</summary>
    private static async Task<HttpResponseMessage> PutAguinaldoPaymentDateAsync(
        HttpClient client, Guid tenantId, int month, int day)
    {
        var current = await client.GetAsync($"/api/v1/companies/{tenantId}/preferences");
        current.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("concurrencyToken").GetGuid();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/companies/{tenantId}/preferences")
        {
            Content = JsonContent.Create(new
            {
                currencyCode = "USD",
                timeZone = "America/El_Salvador",
                aguinaldoPaymentMonth = month,
                aguinaldoPaymentDay = day,
            }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    /// <summary>El contexto de nómina más el grant de preferencias, que vive en otra política.</summary>
    private static TestUserContext AguinaldoPreferencesContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            CLARIHR.Application.Features.PersonnelFiles.Common.PersonnelFilePermissionCodes.Admin,
            "CompanyPreferences.Admin");

    /// <summary>
    /// Una tabla de Renta MENSUAL de un solo tramo al 10 %. No busca replicar la tabla de Hacienda: busca que
    /// la retención sea una función CONOCIDA de la base, para que el número pruebe de dónde salió esa base.
    /// </summary>
    private static async Task SeedFlatRentaTableAsync(HttpClient client)
    {
        var response = await client.PutAsJsonAsync(
            "/api/v1/income-tax-brackets",
            new
            {
                payPeriodCode = "MENSUAL",
                brackets = new[]
                {
                    new
                    {
                        bracketOrder = 1,
                        lowerBound = 0m,
                        upperBound = (decimal?)null,
                        fixedFee = 0m,
                        ratePercent = 10m,
                        excessOver = 0m,
                        effectiveFromUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        effectiveToUtc = (DateTime?)null,
                        isActive = true,
                    },
                },
            });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"renta table: {(int)response.StatusCode} {payload}");
    }

    /// <summary>La misma configuración, directa: los tests de la corrida no vienen a probar el endpoint.</summary>
    private async Task SetAguinaldoPaymentDateAsync(Guid tenantId, int month, int day)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE company_preferences SET aguinaldo_payment_month = {0}, aguinaldo_payment_day = {1} WHERE tenant_id = {2};",
            month, day, tenantId);
    }

    private async Task SeedAguinaldoExemptionAsync(Guid tenantId, int year, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exemption = AguinaldoExemption.Create(year, amount, isActive: true);
        exemption.SetTenantId(tenantId);
        dbContext.Set<AguinaldoExemption>().Add(exemption);
        _ = await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> CreateAguinaldoDefinitionAsync(
        HttpClient manager, Guid companyId, string code, string payrollTypeCode = "QUINCENAL")
    {
        var response = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{companyId}/payroll-definitions",
            new
            {
                code,
                name = $"Aguinaldo {code}",
                payrollTypeCode,
                payPeriodCode = "ANUAL",
                totalPeriods = 1,
                currencyCode = "USD",
                purposeCode = "AGUINALDO",
            });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"definition: {(int)response.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("publicId").GetGuid();
    }

    /// <summary>La nómina de aguinaldo con su calendario de UN periodo, listo para generar.</summary>
    private static async Task<(Guid DefinitionId, Guid PeriodId)> CreateAguinaldoPayrollAsync(
        HttpClient manager,
        Guid companyId,
        string code = "NOM-AGUINALDO",
        string payrollTypeCode = "QUINCENAL")
    {
        var definitionId = await CreateAguinaldoDefinitionAsync(manager, companyId, code, payrollTypeCode);

        var calendar = await manager.PostAsync(
            $"/api/v1/companies/{companyId}/payroll-definitions/{definitionId}/periods/generate?year={AguinaldoYear}",
            content: null);
        var calendarPayload = await calendar.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == calendar.StatusCode, $"calendar: {(int)calendar.StatusCode} {calendarPayload}");
        using (var document = JsonDocument.Parse(calendarPayload))
        {
            Assert.Equal(1, document.RootElement.GetProperty("created").GetInt32());
        }

        var periods = await manager.GetAsync(
            $"/api/v1/companies/{companyId}/payroll-periods?payPeriodTypeCode=ANUAL&year={AguinaldoYear}&pageSize=30");
        periods.EnsureSuccessStatusCode();
        using var periodsDoc = JsonDocument.Parse(await periods.Content.ReadAsStringAsync());
        var period = periodsDoc.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("payrollDefinitionPublicId").GetGuid() == definitionId);

        return (definitionId, period.GetProperty("publicId").GetGuid());
    }

    /// <summary>Las líneas de un empleado dentro de la corrida recién generada.</summary>
    private static async Task<IReadOnlyList<JsonElement>> ReadRunLinesAsync(
        HttpClient manager, Guid companyId, string runPayload, Guid employeeId)
    {
        Guid runId;
        using (var document = JsonDocument.Parse(runPayload))
        {
            runId = document.RootElement.GetProperty("publicId").GetGuid();
        }

        var response = await manager.GetAsync(
            $"/api/v1/companies/{companyId}/payroll-runs/{runId}/employees/{employeeId}");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"employee lines: {(int)response.StatusCode} {payload}");

        using var linesDoc = JsonDocument.Parse(payload);
        return linesDoc.RootElement.GetProperty("lines").EnumerateArray()
            .Select(item => item.Clone())
            .ToArray();
    }
}
