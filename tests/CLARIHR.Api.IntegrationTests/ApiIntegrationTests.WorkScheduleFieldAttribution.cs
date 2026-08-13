using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-18(b) — an invalid `scheduleClass` or `attendanceDateAnchor` was reported as `422
/// WORK_SCHEDULE_DAY_INVALID`, whose message reads *"the days are not valid (weekday, shift times or meal
/// break)"*. The days were fine; the reader would go audit the shifts while the problem sat in another field.
///
/// The cause was a broad `catch (ArgumentException or ArgumentOutOfRangeException)` around
/// <c>WorkSchedule.Create</c>: it exists to turn genuine day-set invariants into a clean 422 instead of a 500,
/// but `SetScheduleClass` and `SetAttendanceDateAnchor` throw the same exception types and were swept in.
///
/// Fixed in the VALIDATOR rather than with new 422 codes, because the module's other three invalid-value cases
/// (`dayOfWeek: 7`, `totalWeeklyHours: 200`, `days: []`) already answer `400` naming the field, and a bad enum
/// is exactly the kind of thing a validator can see. The `catch` stays for what only the aggregate can judge —
/// duplicated weekday, meal break on a night shift, zero-length shift.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Theory]
    [InlineData("scheduleClass", "NOCTURNA")]
    [InlineData("attendanceDateAnchor", "MEDIO")]
    public async Task WorkSchedules_Create_WithInvalidEnumValue_ShouldReturn400NamingTheField(
        string field,
        string badValue)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(PayrollConfigurationManagerContext(scenario));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/work-schedules",
            BuildWorkSchedulePayloadWith(field, badValue));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var payload = problem.RootElement.GetRawText();

        // The point of the finding: the response must point at the offending field, not at the days.
        Assert.Contains(field, payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WORK_SCHEDULE_DAY_INVALID", payload, StringComparison.Ordinal);
    }

    /// <summary>
    /// Non-regression for the `catch` that stays: a duplicated weekday is a day-set invariant only the
    /// aggregate can judge, so it must keep answering `422 WORK_SCHEDULE_DAY_INVALID` — the code is correct
    /// there, and narrowing the catch must not turn this into a 500.
    /// </summary>
    [Fact]
    public async Task WorkSchedules_Create_WithDuplicatedWeekday_ShouldStillReturn422DayInvalid()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(PayrollConfigurationManagerContext(scenario));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/work-schedules",
            new
            {
                code = "JOR-H18-DUP",
                name = "Jornada con lunes repetido",
                scheduleLabel = (string?)null,
                attendanceDateAnchor = "ENTRADA",
                scheduleClass = "ORDINARIA",
                totalWeeklyHours = (decimal?)null,
                days = new[]
                {
                    new { dayOfWeek = 1, startTime = "08:00:00", endTime = "17:00:00", mealStart = (string?)"12:00:00", mealEnd = (string?)"13:00:00" },
                    new { dayOfWeek = 1, startTime = "08:00:00", endTime = "12:00:00", mealStart = (string?)null, mealEnd = (string?)null },
                }
            });

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "WORK_SCHEDULE_DAY_INVALID");
    }

    [Fact]
    public async Task WorkSchedules_Create_WithValidEnumValues_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(PayrollConfigurationManagerContext(scenario));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/work-schedules",
            BuildWorkSchedulePayloadWith("scheduleClass", "EXTRAORDINARIA"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>The 44-hour legal week, with one field overridden by the caller.</summary>
    private static object BuildWorkSchedulePayloadWith(string field, string value) => new
    {
        code = "JOR-H18-001",
        name = "Jornada H18",
        scheduleLabel = (string?)null,
        attendanceDateAnchor = field == "attendanceDateAnchor" ? value : "ENTRADA",
        scheduleClass = field == "scheduleClass" ? value : "ORDINARIA",
        totalWeeklyHours = (decimal?)null,
        days = new[]
        {
            new { dayOfWeek = 1, startTime = "08:00:00", endTime = "17:00:00", mealStart = (string?)"12:00:00", mealEnd = (string?)"13:00:00" },
            new { dayOfWeek = 2, startTime = "08:00:00", endTime = "17:00:00", mealStart = (string?)"12:00:00", mealEnd = (string?)"13:00:00" },
            new { dayOfWeek = 3, startTime = "08:00:00", endTime = "17:00:00", mealStart = (string?)"12:00:00", mealEnd = (string?)"13:00:00" },
            new { dayOfWeek = 4, startTime = "08:00:00", endTime = "17:00:00", mealStart = (string?)"12:00:00", mealEnd = (string?)"13:00:00" },
            new { dayOfWeek = 5, startTime = "08:00:00", endTime = "17:00:00", mealStart = (string?)"12:00:00", mealEnd = (string?)"13:00:00" },
            new { dayOfWeek = 6, startTime = "08:00:00", endTime = "12:00:00", mealStart = (string?)null, mealEnd = (string?)null },
        }
    };
}
