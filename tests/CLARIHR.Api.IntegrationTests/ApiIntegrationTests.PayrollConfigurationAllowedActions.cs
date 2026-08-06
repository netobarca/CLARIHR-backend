using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.Payroll.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// The two payroll-configuration masters carry <c>[ResourceActions]</c> but had no
/// <c>AllowedActionsRegistry</c> entry, so <c>AllowedActionsCoverageIntegrationTests</c> was red.
///
/// That guardrail only checks a key IS registered, never that the entry matches what the gate enforces —
/// and an over-generous entry is worse than none, because the frontend enables buttons off these flags.
///
/// These target PUT deliberately. On the list and the GET detail the handlers build
/// <c>allowedActions</c> themselves through <c>PayrollDefinitionPolicyAdapter</c>, and
/// <c>AllowedActionsResultFilter</c> never overwrites what a handler already set — so those surfaces pass
/// whether or not the resource key is registered. PUT is the surface the filter actually owns, which makes
/// it the only place where the registry entry is what decides.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task PayrollDefinitions_Update_AllowedActions_ShouldComeFromTheRegistry_WithoutExceedingTheGate()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollConfigurationManagerContext(scenario));

        var (definitionId, token) = await CreatePayrollDefinitionForAllowedActionsAsync(manager, scenario.TenantId);

        using var updated = await UpdatePayrollDefinitionAsync(manager, definitionId, token);
        var actions = ReadAllowedActions(updated.RootElement);

        Assert.True(actions.GetProperty("canEdit").GetBoolean());
        Assert.True(actions.GetProperty("canInactivate").GetBoolean());

        // The controller exposes no DELETE and no archive route — advertising either would put a button on
        // an endpoint that does not exist.
        Assert.False(actions.GetProperty("canDelete").GetBoolean());
        Assert.False(actions.GetProperty("canArchive").GetBoolean());
    }

    [Fact]
    public async Task WorkSchedules_Update_AllowedActions_ShouldComeFromTheRegistry_WithoutExceedingTheGate()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollConfigurationManagerContext(scenario));

        var (scheduleId, token) = await CreateWorkScheduleForAllowedActionsAsync(manager, scenario.TenantId);

        using var updated = await UpdateWorkScheduleAsync(manager, scheduleId, token);
        var actions = ReadAllowedActions(updated.RootElement);

        Assert.True(actions.GetProperty("canEdit").GetBoolean());
        Assert.True(actions.GetProperty("canInactivate").GetBoolean());
        Assert.False(actions.GetProperty("canDelete").GetBoolean());
        Assert.False(actions.GetProperty("canArchive").GetBoolean());
    }

    private static TestUserContext PayrollConfigurationManagerContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PayrollConfigurationPermissionCodes.Manage);

    private static async Task<(Guid Id, Guid ConcurrencyToken)> CreatePayrollDefinitionForAllowedActionsAsync(
        HttpClient client,
        Guid companyId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{companyId}/payroll-definitions",
            new
            {
                code = "NOM-AA-001",
                name = "Nómina para allowed-actions",
                payrollTypeCode = "QUINCENAL",
                payPeriodCode = "QUINCENAL",
                totalPeriods = 24,
                currencyCode = "USD",
            });

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"payroll-definition: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (created.RootElement.GetProperty("publicId").GetGuid(),
                created.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<JsonDocument> UpdatePayrollDefinitionAsync(
        HttpClient client,
        Guid definitionId,
        Guid concurrencyToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/payroll-definitions/{definitionId}")
        {
            Content = JsonContent.Create(new
            {
                code = "NOM-AA-001",
                name = "Nómina para allowed-actions (editada)",
                payrollTypeCode = "QUINCENAL",
                payPeriodCode = "QUINCENAL",
                totalPeriods = 24,
                currencyCode = "USD",
            })
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{concurrencyToken}\""));

        var response = await client.SendAsync(request);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"PUT payroll-definition: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<(Guid Id, Guid ConcurrencyToken)> CreateWorkScheduleForAllowedActionsAsync(
        HttpClient client,
        Guid companyId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{companyId}/work-schedules",
            BuildWorkSchedulePayload("Jornada para allowed-actions"));

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"work-schedule: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (created.RootElement.GetProperty("publicId").GetGuid(),
                created.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<JsonDocument> UpdateWorkScheduleAsync(
        HttpClient client,
        Guid scheduleId,
        Guid concurrencyToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/work-schedules/{scheduleId}")
        {
            Content = JsonContent.Create(BuildWorkSchedulePayload("Jornada para allowed-actions (editada)"))
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{concurrencyToken}\""));

        var response = await client.SendAsync(request);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"PUT work-schedule: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Lunes a viernes 8 h efectivas (con hora de comida) + sábado 4 h = las 44 h de ley.</summary>
    private static object BuildWorkSchedulePayload(string name) => new
    {
        code = "JOR-AA-001",
        name,
        scheduleLabel = (string?)null,
        attendanceDateAnchor = "ENTRADA",
        scheduleClass = "ORDINARIA",
        totalWeeklyHours = 44m,
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

    private static JsonElement ReadAllowedActions(JsonElement response)
    {
        Assert.True(
            response.TryGetProperty("allowedActions", out var allowedActions)
                && allowedActions.ValueKind == JsonValueKind.Object,
            "The PUT response carries no 'allowedActions' object. AllowedActionsResultFilter is fail-closed " +
            "on an unregistered resource key, so this is what a missing AllowedActionsRegistry entry looks " +
            $"like from the outside. Payload: {response}");

        return allowedActions;
    }
}
