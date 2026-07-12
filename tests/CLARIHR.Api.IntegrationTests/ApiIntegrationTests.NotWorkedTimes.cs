using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Not-worked time end to end (REQ-011). The load-bearing assertion is the SEVENTH DAY: a Monday-to-Friday absence
/// discounts SIX days, not five — the employee who missed the whole week did not earn their paid day of rest either.
/// It is the one rule of this module with no precedent anywhere in the product, so it is the one that must be
/// pinned by a test.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static TestUserContext NotWorkedTimeContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageNotWorkedTimeTypes,
            PersonnelFilePermissionCodes.ManageNotWorkedTimes,
            PersonnelFilePermissionCodes.ViewNotWorkedTimes,
            // The availability view (REQ-003) has its own dedicated grant.
            PersonnelFilePermissionCodes.ViewTimeAvailability,
            // Declaring a company holiday (an input of the scan) lives in the leave-configuration domain.
            LeaveConfigurationPermissionCodes.Admin);

    private static async Task<JsonElement> LoadNotWorkedTimeTemplateAsync(HttpClient client, Guid companyId)
    {
        var response = await client.PostAsync(
            $"/api/v1/companies/{companyId}/not-worked-time-configuration/load-template", content: null);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"load-template failed: {(int)response.StatusCode} {payload}");
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    private static async Task<JsonElement> CreateNotWorkedTimeAsync(
        HttpClient client, Guid employeeId, object body, HttpStatusCode expected = HttpStatusCode.Created)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{employeeId}/not-worked-times", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(expected == response.StatusCode, $"Expected {expected}, got {(int)response.StatusCode}: {payload}");
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    // 2026-07-06 is a Monday.
    private static readonly DateOnly NotWorkedMonday = new(2026, 7, 6);

    [Fact]
    public async Task NotWorkedTimeTemplate_IsIdempotent_AndNeverOverwrites()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));

        var first = await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);
        Assert.Equal(4, first.GetProperty("typesCreated").GetInt32());
        Assert.Equal(0, first.GetProperty("typesSkipped").GetInt32());

        // Running it again creates nothing and overwrites nothing — that is what makes it safe to call on every
        // provisioning and from the settings screen.
        var second = await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);
        Assert.Equal(0, second.GetProperty("typesCreated").GetInt32());
        Assert.Equal(4, second.GetProperty("typesSkipped").GetInt32());

        var list = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/not-worked-time-types");
        list.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var types = doc.RootElement.EnumerateArray().ToArray();

        Assert.Equal(4, types.Length);

        var unpaid = types.Single(item => item.GetProperty("code").GetString() == "AUSENCIA_SIN_GOCE");
        Assert.Equal(100m, unpaid.GetProperty("discountPercent").GetDecimal());
        Assert.True(unpaid.GetProperty("countsSeventhDayPenalty").GetBoolean());

        var paid = types.Single(item => item.GetProperty("code").GetString() == "AUSENCIA_CON_GOCE");
        Assert.Equal(0m, paid.GetProperty("discountPercent").GetDecimal());

        var late = types.Single(item => item.GetProperty("code").GetString() == "LLEGADA_TARDIA");
        Assert.True(late.GetProperty("usesWorkSchedule").GetBoolean());
    }

    [Fact]
    public async Task NotWorkedTimeTypes_ADiscountWithNowhereToLand_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/not-worked-time-types",
            new
            {
                code = "SIN_CONCEPTO",
                name = "Sin concepto",
                appliesToPermission = false,
                usesWorkSchedule = false,
                countsHoliday = false,
                countsSaturday = false,
                countsRestDay = false,
                countsSeventhDayPenalty = true,
                discountPercent = 100m,
                deductionConceptTypeCode = (string?)null,
                incomeConceptTypeCode = (string?)null,
            });

        // A discount that points nowhere would silently vanish from the payroll input.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "NOT_WORKED_TIME_TYPE_DEDUCTION_CONCEPT_REQUIRED",
            problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task NotWorkedTime_AFullWorkWeek_DiscountsSixDays_NotFive()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));

        await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Sara", "Semana", "EMP-NWT-A", "sara.nwt.a@empresa.test", monthlySalary: 900m);

        var created = await CreateNotWorkedTimeAsync(client, employeeId, new
        {
            typeCode = "AUSENCIA_SIN_GOCE",
            assignedPositionPublicId = (Guid?)null,
            startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            endDate = NotWorkedMonday.AddDays(4).ToString("yyyy-MM-dd"),
            hours = (decimal?)null,
            reason = "No se presentó",
        });

        // THE assertion. Five worked days missed + ONE seventh day = six days discounted. Salary $900 ⇒ $30/day ⇒
        // $180. Discounting only the five ($150) would hand the employee back the paid rest they did not earn.
        Assert.Equal(5, created.GetProperty("calendarDays").GetInt32());
        Assert.Equal(5, created.GetProperty("computableDays").GetInt32());
        Assert.Equal(1, created.GetProperty("seventhDayPenaltyDays").GetInt32());
        Assert.Equal(6m, created.GetProperty("discountedDays").GetDecimal());
        Assert.Equal(30m, created.GetProperty("dailySalary").GetDecimal());
        Assert.Equal(180m, created.GetProperty("discountAmount").GetDecimal());
        Assert.Equal("REGISTRADO", created.GetProperty("statusCode").GetString());

        // No decision step (P-16): it is born registered, and it lands in the personnel-actions journal at once.
        var journal = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions");
        journal.EnsureSuccessStatusCode();
        using var journalDoc = JsonDocument.Parse(await journal.Content.ReadAsStringAsync());
        Assert.Contains(
            journalDoc.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("actionTypeCode").GetString() == "TIEMPO_NO_TRABAJADO");
    }

    [Fact]
    public async Task NotWorkedTime_APaidAbsence_RecordsTheDaysButTouchesNoMoney()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));

        await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Goce", "Pagada", "EMP-NWT-B", "goce.nwt.b@empresa.test", monthlySalary: 900m);

        var created = await CreateNotWorkedTimeAsync(client, employeeId, new
        {
            typeCode = "AUSENCIA_CON_GOCE",
            assignedPositionPublicId = (Guid?)null,
            startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            endDate = NotWorkedMonday.AddDays(4).ToString("yyyy-MM-dd"),
            hours = (decimal?)null,
            reason = "Permiso con goce",
        });

        Assert.Equal(5, created.GetProperty("computableDays").GetInt32());
        Assert.Equal(0m, created.GetProperty("discountAmount").GetDecimal());
    }

    [Fact]
    public async Task NotWorkedTime_ALateArrival_IsMeasuredInHours()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));

        await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Tarde", "Llegada", "EMP-NWT-C", "tarde.nwt.c@empresa.test", monthlySalary: 900m);

        // Two late hours of an eight-hour day are a QUARTER of a day: $30 × 0.25 = $7.50. Charging a whole day
        // would be a punishment, not a discount.
        var created = await CreateNotWorkedTimeAsync(client, employeeId, new
        {
            typeCode = "LLEGADA_TARDIA",
            assignedPositionPublicId = (Guid?)null,
            startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            endDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            hours = 2m,
            reason = "Llegó dos horas tarde",
        });

        Assert.Equal(0.25m, created.GetProperty("discountedDays").GetDecimal());
        Assert.Equal(7.50m, created.GetProperty("discountAmount").GetDecimal());

        // And the hours are MANDATORY for this type — without them the server would silently discount zero.
        var withoutHours = await CreateNotWorkedTimeAsync(
            client, employeeId,
            new
            {
                typeCode = "LLEGADA_TARDIA",
                assignedPositionPublicId = (Guid?)null,
                startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
                endDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
                hours = (decimal?)null,
                reason = (string?)null,
            },
            HttpStatusCode.UnprocessableEntity);
        Assert.Equal("NOT_WORKED_TIME_HOURS_REQUIRED", withoutHours.GetProperty("code").GetString());

        // …and they are REJECTED on a day-based type, or a two-hour absence would be discounted as a whole day.
        var hoursOnDayType = await CreateNotWorkedTimeAsync(
            client, employeeId,
            new
            {
                typeCode = "AUSENCIA_SIN_GOCE",
                assignedPositionPublicId = (Guid?)null,
                startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
                endDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
                hours = 2m,
                reason = (string?)null,
            },
            HttpStatusCode.UnprocessableEntity);
        Assert.Equal("NOT_WORKED_TIME_HOURS_NOT_APPLICABLE", hoursOnDayType.GetProperty("code").GetString());
    }

    [Fact]
    public async Task NotWorkedTime_TheHolidayInTheMiddle_IsNotDiscounted()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));

        await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Asueto", "Medio", "EMP-NWT-D", "asueto.nwt.d@empresa.test", monthlySalary: 900m);

        // Declare the Wednesday a company holiday.
        var holiday = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/company-holidays",
            new
            {
                date = NotWorkedMonday.AddDays(2).ToString("yyyy-MM-dd"),
                description = "Asueto de prueba",
                scopeCode = "NACIONAL",
            });
        var holidayPayload = await holiday.Content.ReadAsStringAsync();
        Assert.True(
            holiday.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Holiday creation failed: {(int)holiday.StatusCode} {holidayPayload}");

        var created = await CreateNotWorkedTimeAsync(client, employeeId, new
        {
            typeCode = "AUSENCIA_SIN_GOCE",
            assignedPositionPublicId = (Guid?)null,
            startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            endDate = NotWorkedMonday.AddDays(4).ToString("yyyy-MM-dd"),
            hours = (decimal?)null,
            reason = (string?)null,
        });

        // The holiday is not a worked day, so it is not discounted: 4 computable + 1 seventh = 5 days = $150.
        Assert.Equal(5, created.GetProperty("calendarDays").GetInt32());
        Assert.Equal(4, created.GetProperty("computableDays").GetInt32());
        Assert.Equal(5m, created.GetProperty("discountedDays").GetDecimal());
        Assert.Equal(150m, created.GetProperty("discountAmount").GetDecimal());
    }

    [Fact]
    public async Task NotWorkedTime_CanBeAnnulled_ButOnlyOnce()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));

        await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Anula", "Registro", "EMP-NWT-E", "anula.nwt.e@empresa.test", monthlySalary: 900m);

        var created = await CreateNotWorkedTimeAsync(client, employeeId, new
        {
            typeCode = "AUSENCIA_SIN_GOCE",
            assignedPositionPublicId = (Guid?)null,
            startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            endDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            hours = (decimal?)null,
            reason = (string?)null,
        });

        var recordId = created.GetProperty("notWorkedTimePublicId").GetGuid();
        var token = created.GetProperty("concurrencyToken").GetGuid();

        using var annulRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/not-worked-times/{recordId}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Se registró por error" })
        };
        annulRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");

        var annulled = await client.SendAsync(annulRequest);
        var annulledPayload = await annulled.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == annulled.StatusCode, $"Annul failed: {(int)annulled.StatusCode} {annulledPayload}");

        using var annulledDoc = JsonDocument.Parse(annulledPayload);
        Assert.Equal("ANULADO", annulledDoc.RootElement.GetProperty("statusCode").GetString());
        Assert.Equal("Se registró por error", annulledDoc.RootElement.GetProperty("annulmentReason").GetString());

        // A second annulment is a 422, not a silent no-op.
        var newToken = annulledDoc.RootElement.GetProperty("concurrencyToken").GetGuid();
        using var again = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/not-worked-times/{recordId}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Otra vez" })
        };
        again.Headers.TryAddWithoutValidation("If-Match", $"\"{newToken}\"");

        var second = await client.SendAsync(again);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        using var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("NOT_WORKED_TIME_ALREADY_ANNULLED", problem.RootElement.GetProperty("code").GetString());
    }
}
