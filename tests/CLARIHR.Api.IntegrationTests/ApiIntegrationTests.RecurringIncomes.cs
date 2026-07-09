using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the recurring-income ("planilla — ingresos cíclicos", REQ-005 PR-3) CRUD + resolution
/// slice: the create round-trip (finite + indefinite → EN_REVISION), the write guards (incoherent plan → 422,
/// plaza without a cost center → 422 P-15, PAGAR_SALDO × indefinite → 422, retired profile → 422), the authorizer
/// resolution with the DOUBLE anti-self (registrar → 403, subject → 403, Admin without the dedicated grant → 403,
/// a third authorized user → VIGENTE), the reject-without-note guard (422), and the VIGENTE lifecycle
/// (suspend/resume, manual closure of an indefinite income, authorizer revocation). Employees are seeded directly
/// (profile + primary assignment, optionally with a cost center).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static readonly DateTime RecurringIncomeHireDate = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static TestUserContext RecurringIncomeManagerContext(IntegrationTestScenario scenario, Guid? userId = null) =>
        TestUserContext.Authenticated(userId ?? scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.Admin);

    private static TestUserContext RecurringIncomeAuthorizerContext(IntegrationTestScenario scenario, Guid userId) =>
        TestUserContext.Authenticated(userId, scenario.TenantId, PersonnelFilePermissionCodes.AuthorizeRecurringIncomes);

    private async Task<Guid> SeedRecurringIncomeCandidateAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail,
        Guid? linkedUserPublicId = null,
        bool withCostCenter = true,
        bool retired = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Guid? costCenterPublicId = null;
        if (withCostCenter)
        {
            var costCenterType = CostCenterType.Create($"CCT-{employeeCode}", $"Tipo {employeeCode}", null);
            costCenterType.SetTenantId(tenantId);
            dbContext.Set<CostCenterType>().Add(costCenterType);
            await dbContext.SaveChangesAsync();

            var costCenter = CostCenter.Create($"CC-{employeeCode}", $"Centro de costo {employeeCode}", costCenterType.Id, null, null, null, null);
            costCenter.SetTenantId(tenantId);
            dbContext.Set<CostCenter>().Add(costCenter);
            await dbContext.SaveChangesAsync();
            costCenterPublicId = costCenter.PublicId;
        }

        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            firstName,
            lastName,
            new DateTime(1990, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: null,
            profession: null,
            nationality: "SV",
            personalEmail: null,
            institutionalEmail: institutionalEmail,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null);
        file.SetTenantId(tenantId);
        if (linkedUserPublicId is { } linked)
        {
            file.Complete(linked);
        }
        else
        {
            file.CompleteWithoutLinkedUser();
        }

        dbContext.Set<PersonnelFile>().Add(file);
        await dbContext.SaveChangesAsync();

        var status = retired ? PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode : "ACTIVO";
        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, status, RecurringIncomeHireDate);
        profile.BindToPersonnelFile(file.Id);
        profile.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmployeeProfile>().Add(profile);

        var assignment = PersonnelFileEmploymentAssignment.Create(
            "INDEFINIDO",
            contractTypeCode: null,
            workdayCode: null,
            payrollTypeCode: null,
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: costCenterPublicId,
            startDate: RecurringIncomeHireDate,
            endDate: null,
            isPrimary: true,
            isActive: true,
            notes: null);
        assignment.BindToPersonnelFile(file.Id);
        assignment.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmploymentAssignment>().Add(assignment);

        await dbContext.SaveChangesAsync();
        return file.PublicId;
    }

    private static object FiniteRecurringIncomeBody(
        int installmentValue = 100,
        int installmentCount = 3,
        string settlementActionCode = "PAGAR_SALDO",
        Guid? assignedPositionPublicId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            registrationDate = today,
            reference = "AYUDA-ALIM-2026",
            recurringIncomeTypeCode = "AYUDA_ALIMENTACION",
            conceptTypeCode = "BONO",
            observations = (string?)null,
            assignedPositionPublicId,
            installmentStartDate = today,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            isIndefinite = false,
            installmentValue,
            installmentCount = (int?)installmentCount,
            totalAmount = (decimal?)null,
            settlementActionCode
        };
    }

    private static object IndefiniteRecurringIncomeBody(string settlementActionCode = "CANCELAR")
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            registrationDate = today,
            reference = "COMBUSTIBLE",
            recurringIncomeTypeCode = "COMBUSTIBLE",
            conceptTypeCode = "BONO",
            observations = "Asignación mensual de combustible",
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = today,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            isIndefinite = true,
            installmentValue = 75,
            installmentCount = (int?)null,
            totalAmount = (decimal?)null,
            settlementActionCode
        };
    }

    private static async Task<(Guid IncomeId, Guid Token)> CreateRecurringIncomeAsync(
        HttpClient client, Guid fileId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/recurring-incomes", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return (
            doc.RootElement.GetProperty("recurringIncomePublicId").GetGuid(),
            doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> PatchRecurringIncomeAsync(
        HttpClient client, Guid fileId, Guid incomeId, string action, Guid token, object body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/recurring-incomes/{incomeId}/{action}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task RecurringIncomes_CreateFinite_ReturnsEnRevisionWithDerivedCostCenter()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringIncomeManagerContext(scenario));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Rita", "Cíclica", "EMP-RI-A", "rita.ri.a@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/recurring-incomes", FiniteRecurringIncomeBody());
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.Equal("EN_REVISION", root.GetProperty("statusCode").GetString());
        Assert.Equal("Bono", root.GetProperty("conceptNameSnapshot").GetString());
        Assert.Equal(300m, root.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(3, root.GetProperty("installmentCount").GetInt32());
        Assert.False(root.GetProperty("isIndefinite").GetBoolean());
        Assert.NotEqual(Guid.Empty, root.GetProperty("costCenterPublicId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("costCenterNameSnapshot").GetString()));
    }

    [Fact]
    public async Task RecurringIncomes_CreateIndefinite_ReturnsEnRevision()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringIncomeManagerContext(scenario));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Iván", "Indefinido", "EMP-RI-B", "ivan.ri.b@empresa.test");

        var (_, _) = await CreateRecurringIncomeAsync(client, fileId, IndefiniteRecurringIncomeBody());

        var listResponse = await client.GetAsync($"/api/v1/personnel-files/{fileId}/recurring-incomes");
        listResponse.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var item = doc.RootElement.EnumerateArray().Single();
        Assert.True(item.GetProperty("isIndefinite").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("installmentCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("totalAmount").ValueKind);
    }

    [Fact]
    public async Task RecurringIncomes_CreateWithIncoherentPlan_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringIncomeManagerContext(scenario));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Petra", "Plan", "EMP-RI-C", "petra.ri.c@empresa.test");

        // Indefinite plan that also carries a count → incoherent (RN-05).
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var body = new
        {
            registrationDate = today,
            reference = (string?)null,
            recurringIncomeTypeCode = "AYUDA_ALIMENTACION",
            conceptTypeCode = "BONO",
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = today,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            isIndefinite = true,
            installmentValue = 100,
            installmentCount = (int?)5,
            totalAmount = (decimal?)null,
            settlementActionCode = "CANCELAR"
        };

        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/recurring-incomes", body);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_INCOME_PLAN_INDEFINITE_WITH_LIMITS");
    }

    [Fact]
    public async Task RecurringIncomes_CreateWithPlazaWithoutCostCenter_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringIncomeManagerContext(scenario));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Sara", "SinCentro", "EMP-RI-D", "sara.ri.d@empresa.test", withCostCenter: false);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/recurring-incomes", FiniteRecurringIncomeBody());
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_INCOME_COST_CENTER_MISSING");
    }

    [Fact]
    public async Task RecurringIncomes_CreatePagarSaldoIndefinite_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringIncomeManagerContext(scenario));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Pablo", "PagarSaldo", "EMP-RI-E", "pablo.ri.e@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/recurring-incomes", IndefiniteRecurringIncomeBody(settlementActionCode: "PAGAR_SALDO"));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_INCOME_SETTLEMENT_ACTION_INDEFINITE");
    }

    [Fact]
    public async Task RecurringIncomes_CreateOnRetiredProfile_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringIncomeManagerContext(scenario));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Renato", "Retirado", "EMP-RI-F", "renato.ri.f@empresa.test", retired: true);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/recurring-incomes", FiniteRecurringIncomeBody());
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "EMPLOYEE_PROFILE_RETIRED_LOCKED");
    }

    [Fact]
    public async Task RecurringIncomes_Resolution_EnforcesDoubleAntiSelf_AndAuthorizesThirdParty()
    {
        var scenario = await factory.ResetDatabaseAsync();

        var registrarUserId = Guid.NewGuid();
        var subjectUserId = Guid.NewGuid();
        var thirdUserId = Guid.NewGuid();

        // Subject employee linked to subjectUserId; a DIFFERENT user registers the income.
        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Susana", "Sujeta", "EMP-RI-G", "susana.ri.g@empresa.test", linkedUserPublicId: subjectUserId);

        using var registrarClient = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, registrarUserId));
        var (incomeId, token) = await CreateRecurringIncomeAsync(registrarClient, fileId, FiniteRecurringIncomeBody());

        var authorizeBody = new { targetStatusCode = "VIGENTE", note = (string?)null };

        // (a) The REGISTRAR (with the Authorize grant) cannot authorize their own registration → 403.
        using (var registrarAuthorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, registrarUserId)))
        {
            var response = await PatchRecurringIncomeAsync(registrarAuthorizer, fileId, incomeId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RECURRING_INCOME_SELF_APPROVAL_FORBIDDEN");
        }

        // (b) The SUBJECT (with the Authorize grant) cannot authorize their own income → 403.
        using (var subjectAuthorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, subjectUserId)))
        {
            var response = await PatchRecurringIncomeAsync(subjectAuthorizer, fileId, incomeId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RECURRING_INCOME_SELF_APPROVAL_FORBIDDEN");
        }

        // (c) A pure Admin (no AuthorizeRecurringIncomes grant) is blocked by the policy → 403.
        using (var adminOnly = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, thirdUserId)))
        {
            var response = await PatchRecurringIncomeAsync(adminOnly, fileId, incomeId, "resolution", token, authorizeBody);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // (d) A third authorized user (neither subject nor registrar) authorizes → VIGENTE.
        using (var thirdAuthorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, thirdUserId)))
        {
            var response = await PatchRecurringIncomeAsync(thirdAuthorizer, fileId, incomeId, "resolution", token, authorizeBody);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("VIGENTE", doc.RootElement.GetProperty("statusCode").GetString());
            // Guid `XxxId` serializes as `xxxPublicId` (public-contract naming convention).
            Assert.Equal(thirdUserId, doc.RootElement.GetProperty("decidedByUserPublicId").GetGuid());
        }
    }

    [Fact]
    public async Task RecurringIncomes_RejectWithoutNote_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Nora", "SinNota", "EMP-RI-H", "nora.ri.h@empresa.test");

        using var registrarClient = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, registrarUserId));
        var (incomeId, token) = await CreateRecurringIncomeAsync(registrarClient, fileId, FiniteRecurringIncomeBody());

        using var authorizerClient = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, authorizerUserId));
        var response = await PatchRecurringIncomeAsync(
            authorizerClient, fileId, incomeId, "resolution", token, new { targetStatusCode = "RECHAZADO", note = (string?)null });
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_INCOME_DECISION_NOTE_REQUIRED");
    }

    [Fact]
    public async Task RecurringIncomes_SuspendResumeAndRevoke_FullLifecycle()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Vera", "Vigente", "EMP-RI-I", "vera.ri.i@empresa.test");

        using var managerClient = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, registrarUserId));
        using var authorizerClient = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, authorizerUserId));

        var (incomeId, createToken) = await CreateRecurringIncomeAsync(managerClient, fileId, FiniteRecurringIncomeBody());

        // Authorize → VIGENTE.
        var authorizeResponse = await PatchRecurringIncomeAsync(
            authorizerClient, fileId, incomeId, "resolution", createToken, new { targetStatusCode = "VIGENTE", note = (string?)null });
        authorizeResponse.EnsureSuccessStatusCode();
        var vigenteToken = await ReadTokenAsync(authorizeResponse);

        // Suspend (Manage) → SUSPENDIDO.
        var suspendResponse = await PatchRecurringIncomeAsync(
            managerClient, fileId, incomeId, "suspension", vigenteToken, new { suspend = true, note = "Pausa temporal" });
        suspendResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await suspendResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("SUSPENDIDO", doc.RootElement.GetProperty("statusCode").GetString());
        }
        var suspendedToken = await ReadTokenAsync(suspendResponse);

        // Resume (Manage) → VIGENTE.
        var resumeResponse = await PatchRecurringIncomeAsync(
            managerClient, fileId, incomeId, "suspension", suspendedToken, new { suspend = false, note = (string?)null });
        resumeResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await resumeResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("VIGENTE", doc.RootElement.GetProperty("statusCode").GetString());
        }
        var resumedToken = await ReadTokenAsync(resumeResponse);

        // Revoke (Authorize) from VIGENTE → ANULADO.
        var revokeResponse = await PatchRecurringIncomeAsync(
            authorizerClient, fileId, incomeId, "revocation", resumedToken, new { reason = "Ya no aplica" });
        revokeResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await revokeResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("ANULADO", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.False(doc.RootElement.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task RecurringIncomes_CloseIndefinite_ReturnsFinalizado()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Cira", "Cierre", "EMP-RI-J", "cira.ri.j@empresa.test");

        using var managerClient = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, registrarUserId));
        using var authorizerClient = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, authorizerUserId));

        var (incomeId, createToken) = await CreateRecurringIncomeAsync(managerClient, fileId, IndefiniteRecurringIncomeBody());

        var authorizeResponse = await PatchRecurringIncomeAsync(
            authorizerClient, fileId, incomeId, "resolution", createToken, new { targetStatusCode = "VIGENTE", note = (string?)null });
        authorizeResponse.EnsureSuccessStatusCode();
        var vigenteToken = await ReadTokenAsync(authorizeResponse);

        var closeResponse = await PatchRecurringIncomeAsync(
            managerClient, fileId, incomeId, "closure", vigenteToken, new { reason = "Fin de la asignación" });
        closeResponse.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await closeResponse.Content.ReadAsStringAsync());
        Assert.Equal("FINALIZADO", doc.RootElement.GetProperty("statusCode").GetString());
    }
}
