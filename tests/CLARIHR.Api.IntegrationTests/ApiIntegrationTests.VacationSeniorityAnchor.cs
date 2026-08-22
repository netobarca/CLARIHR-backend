using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-28 — el escenario del arranque: una empresa que empieza a usar el sistema hoy y carga empleados que ya llevan
/// años. Sus plazas se registran hoy, y otra regla (correcta por su lado) impide que una asignación empiece antes
/// de que la plaza exista. Si la antigüedad se mide desde la plaza, nadie tiene vacaciones hasta dentro de 12
/// meses, aunque lleve 2.5 años en la empresa. Medido en la corrida: 0 de 59 elegibles.
/// <para>
/// Todos estos tests separan a propósito el <c>hireDate</c> del <c>startDate</c> de la plaza. Los tests de
/// vacaciones que ya existían sembraban las dos fechas IGUALES, así que pasaban con cualquiera de los dos anclajes
/// — no podían ver el defecto (instancia de H-33).
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    // Plazas registradas "hoy", como en una empresa que arranca.
    private static DateOnly PlazaRegisteredToday => DateOnly.FromDateTime(DateTime.UtcNow.Date);

    private static DateTime HireYearsAgo(int years, int month = 2, int day = 1) =>
        new(DateTime.UtcNow.Year - years, month, day, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// El caso masivo del hallazgo. Tres empleados con plazas registradas hoy: 2.5 años, 13 meses y 4 meses de
    /// antigüedad real. Deben crearse DOS periodos y quedar UNO inelegible. Anclando en la plaza salían
    /// <c>created: 0</c> y tres errores.
    /// </summary>
    [Fact]
    public async Task Vacation_MassGeneration_AnchorsOnHireDateNotPlazaStart()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var hr = factory.CreateClientFor(CreateVacationManagerContext(scenario));

        var year = DateTime.UtcNow.Year;
        await SeedSettlementCandidateAsync(
            scenario.TenantId, "Jose", "Antiguo", "EMP-H28-1", "jose.h28@empresa.test",
            plazaStartDate: PlazaRegisteredToday, hireDate: HireYearsAgo(2));
        await SeedSettlementCandidateAsync(
            scenario.TenantId, "Marta", "UnAnio", "EMP-H28-2", "marta.h28@empresa.test",
            plazaStartDate: PlazaRegisteredToday, hireDate: HireYearsAgo(1).AddMonths(-1));
        await SeedSettlementCandidateAsync(
            scenario.TenantId, "Luis", "Reciente", "EMP-H28-3", "luis.h28@empresa.test",
            plazaStartDate: PlazaRegisteredToday, hireDate: DateTime.UtcNow.Date.AddMonths(-4));

        var generate = await hr.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/vacation-periods/generate", new { year });
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);

        var summary = await generate.Content.ReadFromJsonAsync<VacationPeriodGenerationSummary>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(3, summary!.TotalEmployees);
        Assert.Equal(2, summary.Created);
        var error = Assert.Single(summary.Errors);
        Assert.Equal("VACATION_ELIGIBILITY_NOT_MET", error.Code);
        Assert.Contains("Luis", error.EmployeeFullName);
    }

    /// <summary>
    /// El caso individual del hallazgo (José Hernández: ingreso 2.5 años atrás, plaza de una semana → <c>422</c>) y
    /// además que la ventana del periodo corra sobre el aniversario de INGRESO, no el de la plaza.
    /// </summary>
    [Fact]
    public async Task Vacation_ManualPeriod_AnchorsWindowOnTheHireAnniversary()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var hr = factory.CreateClientFor(CreateVacationManagerContext(scenario));

        var hire = HireYearsAgo(2, month: 4, day: 15);
        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Ana", "Aniversario", "EMP-H28-4", "ana.h28@empresa.test",
            plazaStartDate: PlazaRegisteredToday, hireDate: hire);

        var year = DateTime.UtcNow.Year;
        var response = await hr.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/vacation-periods",
            new { periodYear = year, useAnniversary = true });

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"{(int)response.StatusCode}: {payload}");

        using var document = JsonDocument.Parse(payload);
        Assert.Equal(
            $"{year:D4}-04-15",
            document.RootElement.GetProperty("periodStartDate").GetString());
        Assert.Equal(
            $"{year + 1:D4}-04-14",
            document.RootElement.GetProperty("periodEndDate").GetString());
    }

    /// <summary>
    /// Modo AÑO CALENDARIO: quien cumple su año a mitad del periodo tiene derecho a ese fondo. La elegibilidad se
    /// mide contra el FIN del periodo; midiéndola contra el inicio, este empleado tenía que esperar al año
    /// siguiente.
    /// </summary>
    [Fact]
    public async Task Vacation_CalendarYearMode_GrantsThePeriodContainingTheFirstAnniversary()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var hr = factory.CreateClientFor(CreateVacationManagerContext(scenario));

        // Ingresó en JULIO del año pasado: cumple su año en julio de este año, o sea dentro del calendario actual.
        var year = DateTime.UtcNow.Year;
        var hire = new DateTime(year - 1, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Carlos", "Calendario", "EMP-H28-5", "carlos.h28@empresa.test",
            plazaStartDate: PlazaRegisteredToday, hireDate: hire);

        var response = await hr.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/vacation-periods",
            new { periodYear = year, useAnniversary = false });

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"{(int)response.StatusCode}: {payload}");

        using var document = JsonDocument.Parse(payload);
        Assert.Equal($"{year:D4}-01-01", document.RootElement.GetProperty("periodStartDate").GetString());
        Assert.Equal($"{year:D4}-12-31", document.RootElement.GetProperty("periodEndDate").GetString());
    }

    /// <summary>
    /// Y el corte sigue existiendo: quien ingresó hace 4 meses no es elegible en ningún modo. Sin este test, "arreglar"
    /// H-28 quitando la validación pasaría por bueno.
    /// </summary>
    [Fact]
    public async Task Vacation_ManualPeriod_WithLessThanOneYearOfService_IsStillRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var hr = factory.CreateClientFor(CreateVacationManagerContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Nuevo", "Ingreso", "EMP-H28-6", "nuevo.h28@empresa.test",
            plazaStartDate: PlazaRegisteredToday, hireDate: DateTime.UtcNow.Date.AddMonths(-4));

        var response = await hr.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/vacation-periods",
            new { periodYear = DateTime.UtcNow.Year, useAnniversary = true });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("VACATION_ELIGIBILITY_NOT_MET", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// H-28 · decisión 2 del usuario: <c>generate</c> queda ABIERTO a cualquier año y la empresa ajusta los días
    /// con el <c>PUT</c> que ya existe. Este test fija esa decisión como comportamiento: generar un año pasado
    /// crea el periodo (no lo bloquea), y los días otorgados se pueden bajar al saldo real.
    /// </summary>
    [Fact]
    public async Task Vacation_MassGeneration_ForAPastYear_IsAllowedAndGrantsAreAdjustable()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var hr = factory.CreateClientFor(CreateVacationManagerContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Historico", "Saldo", "EMP-H28-7", "historico.h28@empresa.test",
            plazaStartDate: PlazaRegisteredToday, hireDate: HireYearsAgo(3));

        var pastYear = DateTime.UtcNow.Year - 1;
        var generate = await hr.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/vacation-periods/generate", new { year = pastYear });
        var summary = await generate.Content.ReadFromJsonAsync<VacationPeriodGenerationSummary>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.Created);

        // La empresa baja los 15 días de ley al saldo que realmente quedó pendiente de ese año.
        var periods = await hr.GetAsync($"/api/v1/personnel-files/{employeeId}/vacation-periods");
        var list = await periods.Content.ReadFromJsonAsync<PersonnelFileVacationPeriodResponse[]>(JsonOptions);
        var period = Assert.Single(list!);

        var adjust = await SendSettlementAsync(
            hr, HttpMethod.Put, $"/api/v1/personnel-files/{employeeId}/vacation-periods/{period.Id}",
            period.ConcurrencyToken, new { legalDaysGranted = 3, benefitDaysGranted = 0 });
        Assert.Equal(HttpStatusCode.OK, adjust.StatusCode);

        var adjusted = await adjust.Content.ReadFromJsonAsync<PersonnelFileVacationPeriodResponse>(JsonOptions);
        Assert.Equal(3, adjusted!.LegalDaysGranted);
    }

    /// <summary>
    /// H-28 · decisión 1 del usuario: el <b>finiquito</b> también mide la antigüedad desde el ingreso. Este es el
    /// guardarraíl del cableado, que ningún test podía dar: los golden del motor reciben la fecha explícita y todos
    /// los tests de liquidación sembraban el ingreso y el inicio de plaza IGUALES.
    /// <para>
    /// Escenario: empleado con 4 años en la empresa cuya plaza se registró hace 20 días. Anclado en la plaza, su
    /// prestación por renuncia era <b>0</b> —no llegaba a los 2 años de servicio mínimos— y el aguinaldo caía en el
    /// tramo de 15 días. Anclado en el ingreso cobra lo que le toca.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Settlement_Seniority_AnchorsOnHireDateNotPlazaStart()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rodrigo", "Antiguo", "EMP-H28-LQ", "rodrigo.h28@empresa.test",
            plazaStartDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-20)),
            hireDate: HireYearsAgo(4));
        // El solicitante del finiquito tiene que ser de RRHH (SETTLEMENT_REQUESTER_NOT_HR).
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "H28", "EMP-H28-LQ2", "gestora.h28@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements/scenarios",
            new
            {
                assignedPositionPublicId = plazaId,
                estimatedRetirementDate = DateTime.UtcNow.Date,
                retirementCategoryCode = "VOLUNTARIA",
                retirementReasonCode = "MOTIVOS_PERSONALES",
                requestDate = DateTime.UtcNow.Date,
            });

        var payload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"{(int)created.StatusCode}: {payload}");

        using var document = JsonDocument.Parse(payload);
        var benefit = document.RootElement.GetProperty("lines").EnumerateArray()
            .Single(line => line.GetProperty("conceptCode").GetString() == "RENUNCIA_VOLUNTARIA");
        // 4 años de servicio → supera el mínimo de 2 y la prestación es > 0. Con la plaza de 20 días era 0.
        Assert.True(
            benefit.GetProperty("finalAmount").GetDecimal() > 0m,
            $"La prestación quedó en 0: la antigüedad se está midiendo desde la plaza. {benefit}");
    }

    /// <summary>
    /// H-28 — el sitio que se me quedó fuera al mover el ancla. La antigüedad y la vacación proporcional pasaron al
    /// <c>SeniorityStartDate</c>, pero el **aguinaldo proporcional** siguió contando desde el
    /// <c>PlazaStartDate</c>: <c>DaysInAguinaldoPeriod</c> arranca el devengo en
    /// <c>max(12-dic anterior, inicio)</c>, así que con una plaza registrada hace 20 días el devengo empezaba hace
    /// 20 días en vez del 12 de diciembre.
    /// <para>
    /// Medido sobre los datos del ambiente antes del arreglo: 12 días en lugar de 244, es decir un aguinaldo
    /// proporcional subestimado en un 95 % en código certificado que paga dinero real.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Settlement_AguinaldoProporcional_AccruesFromTheHireDateNotThePlaza()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        // Ingresó hace 4 años; su plaza se registró hace 20 días (la empresa que arranca con empleados antiguos).
        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Aguinaldo", "Antiguo", "EMP-AGU-1", "aguinaldo.antiguo@empresa.test",
            plazaStartDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-20)),
            hireDate: HireYearsAgo(4));
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "Aguinaldo", "EMP-AGU-2", "gestora.aguinaldo@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var today = DateTime.UtcNow.Date;
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements/scenarios",
            new
            {
                assignedPositionPublicId = plazaId,
                estimatedRetirementDate = today,
                retirementCategoryCode = "VOLUNTARIA",
                retirementReasonCode = "MOTIVOS_PERSONALES",
                requestDate = today,
            });

        var payload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"{(int)created.StatusCode}: {payload}");

        using var document = JsonDocument.Parse(payload);
        var line = document.RootElement.GetProperty("lines").EnumerateArray()
            .Single(item => item.GetProperty("conceptCode").GetString() == "AGUINALDO_PROPORCIONAL");

        // El devengo arranca el 12 de diciembre anterior, no en la fecha de la plaza.
        var expectedStart = today >= new DateTime(today.Year, 12, 12)
            ? new DateTime(today.Year, 12, 12)
            : new DateTime(today.Year - 1, 12, 12);
        var expectedDays = (decimal)(today - expectedStart).Days;

        Assert.Equal(expectedDays, line.GetProperty("unitsOrDays").GetDecimal());
        Assert.True(
            line.GetProperty("finalAmount").GetDecimal() > 0m,
            $"El aguinaldo proporcional quedó en 0: el devengo se está contando desde la plaza. {line}");
    }
}
