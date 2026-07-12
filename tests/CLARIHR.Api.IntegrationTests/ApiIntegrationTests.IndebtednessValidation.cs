using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// The indebtedness validation end to end (REQ-010 PR-2). The behaviour the levantamiento demands is literal:
/// <b>warn, never block</b>. So the 422 is RETRYABLE — re-sending the same request with
/// <c>acknowledgeIndebtednessExceeded</c> registers the credit and leaves an audited footprint of who accepted it
/// and with which figures. And the property that keeps REQ-008 intact is asserted too: with no parameters
/// configured, the check does not exist.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>A credit whose installment is <paramref name="installment"/> per month for 12 months.</summary>
    private static object IndebtednessCreditBody(decimal installment) =>
        SegmentedRecurringDeductionBody(
            segments: [new { fromInstallment = 1, toInstallment = (int?)12, installmentValue = installment }]);

    private async Task SetIndebtednessCeilingAsync(HttpClient client, Guid companyId, decimal percent)
    {
        var current = await client.GetAsync($"/api/v1/companies/{companyId}/preferences");
        current.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/companies/{companyId}/preferences")
        {
            Content = JsonContent.Create(new { currencyCode = "USD", timeZone = "UTC", maxIndebtednessPercent = percent })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"Setting the ceiling failed: {(int)response.StatusCode} {payload}");
    }

    /// <summary>The manager client, which also carries the preferences grant (the global ceiling lives there).</summary>
    private static TestUserContext IndebtednessFlowContext(IntegrationTestScenario scenario, Guid? userId = null) =>
        TestUserContext.Authenticated(
            userId ?? scenario.ActorUserId,
            scenario.TenantId,
            CLARIHR.Application.Features.PersonnelFiles.Common.PersonnelFilePermissionCodes.Admin,
            CLARIHR.Application.Features.PersonnelFiles.Common.PersonnelFilePermissionCodes.ManageIndebtednessParameters,
            "CompanyPreferences.Admin");

    [Fact]
    public async Task IndebtednessValidation_WithoutParameters_TheCreditIsRegisteredWithNoWarning()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessFlowContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Sara", "SinControl", "EMP-IND-A", "sara.ind.a@empresa.test", monthlySalary: 1000m);

        // A $900 installment against a $1,000 salary is 90% of the income — and it registers WITHOUT a warning,
        // because the company configured no ceiling. This is the property that keeps REQ-008 retrocompatible.
        var (deductionId, _) = await CreateRecurringDeductionAsync(client, employeeId, IndebtednessCreditBody(900m));

        var response = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/recurring-deductions/{deductionId}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("EN_REVISION", doc.RootElement.GetProperty("statusCode").GetString());
        Assert.Empty(doc.RootElement.GetProperty("indebtednessOverrides").EnumerateArray());
    }

    [Fact]
    public async Task IndebtednessValidation_OverTheCeiling_Returns422WithTheBreakdown_AndTheConfirmationRegistersIt()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessFlowContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Elsa", "Excedida", "EMP-IND-B", "elsa.ind.b@empresa.test", monthlySalary: 1000m);

        await SetIndebtednessCeilingAsync(client, scenario.TenantId, 30m);

        // $400 of a $1,000 salary is 40% — over the 30% ceiling.
        var refused = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/recurring-deductions",
            IndebtednessCreditBody(400m));
        var refusedPayload = await refused.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.UnprocessableEntity == refused.StatusCode, refusedPayload);

        using (var problem = JsonDocument.Parse(refusedPayload))
        {
            var root = problem.RootElement;
            Assert.Equal("INDEBTEDNESS_LIMIT_EXCEEDED", root.GetProperty("code").GetString());

            // The breakdown rides as ROOT members of the ProblemDetails, NOT in `detail`: the localizer overwrites
            // `detail` with the catalogued message, so any figure written there would be lost. The client renders
            // its confirmation dialog from exactly these numbers.
            Assert.Equal(1000m, root.GetProperty("baseIncome").GetDecimal());
            Assert.Equal(0m, root.GetProperty("currentLoad").GetDecimal());
            Assert.Equal(400m, root.GetProperty("newInstallment").GetDecimal());
            Assert.Equal(40m, root.GetProperty("projectedPercent").GetDecimal());
            Assert.Equal(30m, root.GetProperty("limitPercent").GetDecimal());
            Assert.Equal("GLOBAL", root.GetProperty("limitSource").GetString());
        }

        // Nothing was persisted by the refusal.
        var listAfterRefusal = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/recurring-deductions");
        using (var doc = JsonDocument.Parse(await listAfterRefusal.Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, doc.RootElement.GetArrayLength());
        }

        // The SAME request, re-sent with the confirmation, registers the credit. Warn — never block.
        var body = IndebtednessCreditBody(400m);
        var confirmed = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/recurring-deductions",
            MergeAcknowledge(body));
        var confirmedPayload = await confirmed.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == confirmed.StatusCode, confirmedPayload);

        using var created = JsonDocument.Parse(confirmedPayload);
        var overrides = created.RootElement.GetProperty("indebtednessOverrides").EnumerateArray().ToArray();

        // And it leaves a FOOTPRINT: who accepted it, when, and the figures that were on screen at that moment.
        // The parameters and the employee's other credits will move; this row must not.
        Assert.Single(overrides);
        Assert.Equal("CREACION", overrides[0].GetProperty("stage").GetString());
        Assert.Equal(1000m, overrides[0].GetProperty("baseIncome").GetDecimal());
        Assert.Equal(400m, overrides[0].GetProperty("newInstallment").GetDecimal());
        Assert.Equal(40m, overrides[0].GetProperty("projectedPercent").GetDecimal());
        Assert.Equal(30m, overrides[0].GetProperty("limitPercent").GetDecimal());
        // A Guid-typed `…Id` property serializes as `…PublicId` (PublicContractNaming) — the wire key is
        // acknowledgedByUserPublicId, not acknowledgedByUserId.
        Assert.NotEqual(Guid.Empty, overrides[0].GetProperty("acknowledgedByUserPublicId").GetGuid());
    }

    [Fact]
    public async Task IndebtednessValidation_UnderTheCeiling_RegistersWithNoFootprint()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessFlowContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Dina", "Dentro", "EMP-IND-C", "dina.ind.c@empresa.test", monthlySalary: 1000m);

        await SetIndebtednessCeilingAsync(client, scenario.TenantId, 30m);

        // $300 of $1,000 is exactly 30%: sitting ON the ceiling is not crossing it.
        var (deductionId, _) = await CreateRecurringDeductionAsync(client, employeeId, IndebtednessCreditBody(300m));

        var response = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/recurring-deductions/{deductionId}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Empty(doc.RootElement.GetProperty("indebtednessOverrides").EnumerateArray());
    }

    [Fact]
    public async Task IndebtednessValidation_ThePerTypeCeiling_PrevailsOverTheGlobalOne()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessFlowContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Tina", "PorTipo", "EMP-IND-D", "tina.ind.d@empresa.test", monthlySalary: 1000m);

        await SetIndebtednessCeilingAsync(client, scenario.TenantId, 30m);
        await PutIndebtednessLimitsAsync(client, scenario.TenantId, new
        {
            limits = new[] { new { recurringDeductionTypeCode = "PRESTAMO_BANCARIO", maxPercent = 20m } }
        });

        // 25% is comfortably under the company's 30%, but over the bank loan's own 20% — and the loan's ceiling is
        // the one that governs a loan.
        var refused = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/recurring-deductions",
            IndebtednessCreditBody(250m));
        var payload = await refused.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.UnprocessableEntity == refused.StatusCode, payload);

        using var problem = JsonDocument.Parse(payload);
        Assert.Equal(20m, problem.RootElement.GetProperty("limitPercent").GetDecimal());
        Assert.Equal("TIPO", problem.RootElement.GetProperty("limitSource").GetString());
    }

    [Fact]
    public async Task IndebtednessValidation_RunsAgainAtAuthorization_BecauseTheLoadMovesInBetween()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessFlowContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Ada", "Autorizada", "EMP-IND-E", "ada.ind.e@empresa.test", monthlySalary: 1000m);

        await SetIndebtednessCeilingAsync(client, scenario.TenantId, 30m);

        // [1] A $200 credit — 20% of the salary. Registers cleanly.
        var (firstId, firstToken) = await CreateRecurringDeductionAsync(client, employeeId, IndebtednessCreditBody(200m));

        // [2] A $150 credit. At THIS moment the first one is still EN_REVISION, so it is not load yet: the
        //     projection is 15%, well under the ceiling. It registers cleanly too — and with no footprint.
        var (secondId, secondToken) = await CreateRecurringDeductionAsync(client, employeeId, IndebtednessCreditBody(150m));

        // [3] The first credit is authorized. It is VIGENTE now, and therefore real debt.
        using var authorizer = factory.CreateClientFor(RecurringDeductionAuthorizerContext(scenario, Guid.NewGuid()));
        var authorizedFirst = await PatchRecurringDeductionAsync(
            authorizer, employeeId, firstId, "resolution", firstToken,
            new { targetStatusCode = "VIGENTE", note = (string?)null });
        authorizedFirst.EnsureSuccessStatusCode();

        // [4] And THAT is why the check runs twice. The second credit fit when it was registered and does not fit
        //     now: 200 (live) + 150 (new) = 350 of 1,000 = 35%, over the 30% ceiling. Trusting the verdict from
        //     registration time would let the employee sail past the limit.
        var refused = await PatchRecurringDeductionAsync(
            authorizer, employeeId, secondId, "resolution", secondToken,
            new { targetStatusCode = "VIGENTE", note = (string?)null });
        var payload = await refused.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.UnprocessableEntity == refused.StatusCode, payload);

        using (var problem = JsonDocument.Parse(payload))
        {
            Assert.Equal("INDEBTEDNESS_LIMIT_EXCEEDED", problem.RootElement.GetProperty("code").GetString());
            Assert.Equal(200m, problem.RootElement.GetProperty("currentLoad").GetDecimal());
            Assert.Equal(150m, problem.RootElement.GetProperty("newInstallment").GetDecimal());
            Assert.Equal(35m, problem.RootElement.GetProperty("projectedPercent").GetDecimal());
        }

        // [5] The authorizer confirms, and it is THEIR footprint that gets stamped — at the AUTORIZACION stage,
        //     with the figures of today, not the ones the registrar saw.
        var confirmed = await PatchRecurringDeductionAsync(
            authorizer, employeeId, secondId, "resolution", secondToken,
            new { targetStatusCode = "VIGENTE", note = (string?)null, acknowledgeIndebtednessExceeded = true });
        var confirmedPayload = await confirmed.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == confirmed.StatusCode, confirmedPayload);

        using var doc = JsonDocument.Parse(confirmedPayload);
        Assert.Equal("VIGENTE", doc.RootElement.GetProperty("statusCode").GetString());

        var overrides = doc.RootElement.GetProperty("indebtednessOverrides").EnumerateArray().ToArray();
        Assert.Single(overrides);
        Assert.Equal("AUTORIZACION", overrides[0].GetProperty("stage").GetString());
        Assert.Equal(200m, overrides[0].GetProperty("monthlyLoad").GetDecimal());
        Assert.Equal(35m, overrides[0].GetProperty("projectedPercent").GetDecimal());
        Assert.Equal(30m, overrides[0].GetProperty("limitPercent").GetDecimal());
    }

    /// <summary>Re-sends a creation body with the confirmation flag on (the FE flow: same request, one more field).</summary>
    private static Dictionary<string, object?> MergeAcknowledge(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var map = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        map["acknowledgeIndebtednessExceeded"] = true;
        return map;
    }
}
