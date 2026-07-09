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
/// Integration coverage for the one-time-income ("planilla — ingresos eventuales", REQ-006 PR-3) CRUD + resolution
/// slice: the create round-trip (fixed + computed-by-factors → EN_REVISION with the derived cost center and the
/// requester trío), the write guards (component mismatch → 422, plaza without a cost center → 422 P-15, egress /
/// base-salary concept → 422 D-03, re-target on a non-AUTORIZADO income → 422), the authorizer resolution with the
/// TRIPLE anti-self (registrar → 403, subject → 403, REQUESTER with a linked login → 403, Admin without the
/// dedicated grant → 403, a fourth authorized user → AUTORIZADO), the reject-without-note guard (422), the
/// AUTORIZADO re-target + revocation, and the If-Match concurrency guard (missing → 400, stale → 409). Employees are
/// seeded directly (profile + primary assignment, optionally with a cost center and/or a linked login).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static readonly DateTime OneTimeIncomeHireDate = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static TestUserContext OneTimeIncomeManagerContext(IntegrationTestScenario scenario, Guid? userId = null) =>
        TestUserContext.Authenticated(userId ?? scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.Admin);

    private static TestUserContext OneTimeIncomeAuthorizerContext(IntegrationTestScenario scenario, Guid userId) =>
        TestUserContext.Authenticated(userId, scenario.TenantId, PersonnelFilePermissionCodes.AuthorizeOneTimeIncomes);

    private async Task<Guid> SeedOneTimeIncomeCandidateAsync(
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
        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, status, OneTimeIncomeHireDate);
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
            startDate: OneTimeIncomeHireDate,
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

    private static object FixedOneTimeIncomeBody(
        Guid requesterFilePublicId,
        int amount = 150,
        string conceptTypeCode = "BONO")
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            incomeDate = today,
            reference = "COMISION-2026",
            conceptTypeCode,
            observations = (string?)null,
            isFixedValue = true,
            calculationMethod = (string?)null,
            quantity = (decimal?)null,
            unitValue = (decimal?)null,
            multiplier = (decimal?)null,
            percentage = (decimal?)null,
            baseAmount = (decimal?)null,
            amount = (decimal?)amount,
            currencyCode = "USD",
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId,
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            payrollPeriodEndDate = (string?)null
        };
    }

    private static object FactorOneTimeIncomeBody(
        Guid requesterFilePublicId,
        decimal quantity = 10m,
        decimal unitValue = 2.50m,
        decimal multiplier = 1.5m,
        decimal? amount = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            incomeDate = today,
            reference = "HORAS-EXTRA-2026",
            conceptTypeCode = "HORAS_EXTRA",
            observations = (string?)null,
            isFixedValue = false,
            calculationMethod = "CANTIDAD_POR_VALOR",
            quantity = (decimal?)quantity,
            unitValue = (decimal?)unitValue,
            multiplier = (decimal?)multiplier,
            percentage = (decimal?)null,
            baseAmount = (decimal?)null,
            amount,
            currencyCode = "USD",
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId,
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            payrollPeriodEndDate = (string?)null
        };
    }

    private static async Task<(Guid IncomeId, Guid Token)> CreateOneTimeIncomeAsync(
        HttpClient client, Guid fileId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return (
            doc.RootElement.GetProperty("oneTimeIncomePublicId").GetGuid(),
            doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> PatchOneTimeIncomeAsync(
        HttpClient client, Guid fileId, Guid incomeId, string action, Guid? token, object body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}/{action}")
        {
            Content = JsonContent.Create(body)
        };
        if (token is { } value)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{value}\"");
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task OneTimeIncomes_CreateFixed_ReturnsEnRevisionWithDerivedCostCenter()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Elsa", "Eventual", "EMP-OTI-A", "elsa.oti.a@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rodrigo", "Solicitante", "EMP-OTI-A2", "rodrigo.oti.a@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-incomes", FixedOneTimeIncomeBody(requesterId));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.Equal("EN_REVISION", root.GetProperty("statusCode").GetString());
        Assert.Equal("Bono", root.GetProperty("conceptNameSnapshot").GetString());
        Assert.Equal(150m, root.GetProperty("amount").GetDecimal());
        Assert.True(root.GetProperty("isFixedValue").GetBoolean());
        Assert.NotEqual(Guid.Empty, root.GetProperty("costCenterPublicId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("costCenterNameSnapshot").GetString()));
        Assert.Equal(requesterId, root.GetProperty("requesterFilePublicId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("requesterNameSnapshot").GetString()));
    }

    [Fact]
    public async Task OneTimeIncomes_CreateByFactors_ComputesAmount()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Félix", "Factores", "EMP-OTI-B", "felix.oti.b@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rita", "Solicitante", "EMP-OTI-B2", "rita.oti.b@empresa.test");

        // 10 × $2.50 × 1.5 = $37.50, amount omitted (server resolves it).
        var (incomeId, _) = await CreateOneTimeIncomeAsync(client, fileId, FactorOneTimeIncomeBody(requesterId));

        var detail = await client.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        detail.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.False(root.GetProperty("isFixedValue").GetBoolean());
        Assert.Equal("CANTIDAD_POR_VALOR", root.GetProperty("calculationMethod").GetString());
        Assert.Equal(37.50m, root.GetProperty("amount").GetDecimal());
        Assert.Equal(10m, root.GetProperty("quantity").GetDecimal());
        Assert.Equal(2.50m, root.GetProperty("unitValue").GetDecimal());
        Assert.Equal(1.5m, root.GetProperty("multiplier").GetDecimal());
    }

    [Fact]
    public async Task OneTimeIncomes_CreateByFactorsWithMismatch_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Mario", "Mismatch", "EMP-OTI-C", "mario.oti.c@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rosa", "Solicitante", "EMP-OTI-C2", "rosa.oti.c@empresa.test");

        // Supplied amount ($99) does not match 10 × $2.50 × 1.5 = $37.50 → 422 with the expected breakdown.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-incomes", FactorOneTimeIncomeBody(requesterId, amount: 99m));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_INCOME_AMOUNT_MISMATCH");
    }

    [Fact]
    public async Task OneTimeIncomes_CreateWithPlazaWithoutCostCenter_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Sonia", "SinCentro", "EMP-OTI-D", "sonia.oti.d@empresa.test", withCostCenter: false);
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Raúl", "Solicitante", "EMP-OTI-D2", "raul.oti.d@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-incomes", FixedOneTimeIncomeBody(requesterId));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_INCOME_COST_CENTER_MISSING");
    }

    [Fact]
    public async Task OneTimeIncomes_CreateWithEgressConcept_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Genaro", "Egreso", "EMP-OTI-E", "genaro.oti.e@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rebeca", "Solicitante", "EMP-OTI-E2", "rebeca.oti.e@empresa.test");

        // ANTICIPO is an Egreso concept → 422 (D-03: only Nature = Ingreso).
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-incomes", FixedOneTimeIncomeBody(requesterId, conceptTypeCode: "ANTICIPO"));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_INCOME_CONCEPT_NOT_INCOME");
    }

    [Fact]
    public async Task OneTimeIncomes_CreateWithBaseSalaryConcept_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Berta", "Base", "EMP-OTI-F", "berta.oti.f@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Ramiro", "Solicitante", "EMP-OTI-F2", "ramiro.oti.f@empresa.test");

        // SALARIO_BASE is Nature = Ingreso but IsBaseSalary → 422 (D-03).
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-incomes", FixedOneTimeIncomeBody(requesterId, conceptTypeCode: "SALARIO_BASE"));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_INCOME_CONCEPT_IS_BASE_SALARY");
    }

    [Fact]
    public async Task OneTimeIncomes_Resolution_EnforcesTripleAntiSelf_AndAuthorizesFourthParty()
    {
        var scenario = await factory.ResetDatabaseAsync();

        var registrarUserId = Guid.NewGuid();
        var subjectUserId = Guid.NewGuid();
        var requesterUserId = Guid.NewGuid();
        var fourthUserId = Guid.NewGuid();

        // Subject employee linked to subjectUserId; requester file linked to requesterUserId; a DIFFERENT user registers.
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Sujeta", "Empleada", "EMP-OTI-G", "sujeta.oti.g@empresa.test", linkedUserPublicId: subjectUserId);
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Solicitante", "Vinculado", "EMP-OTI-G2", "solicitante.oti.g@empresa.test", linkedUserPublicId: requesterUserId);

        using var registrarClient = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario, registrarUserId));
        var (incomeId, token) = await CreateOneTimeIncomeAsync(registrarClient, fileId, FixedOneTimeIncomeBody(requesterId));

        var authorizeBody = new { targetStatusCode = "AUTORIZADO", note = (string?)null };

        // (a) The REGISTRAR (with the Authorize grant) cannot authorize their own registration → 403.
        using (var registrarAuthorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, registrarUserId)))
        {
            var response = await PatchOneTimeIncomeAsync(registrarAuthorizer, fileId, incomeId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "ONE_TIME_INCOME_SELF_APPROVAL_FORBIDDEN");
        }

        // (b) The SUBJECT (with the Authorize grant) cannot authorize their own income → 403.
        using (var subjectAuthorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, subjectUserId)))
        {
            var response = await PatchOneTimeIncomeAsync(subjectAuthorizer, fileId, incomeId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "ONE_TIME_INCOME_SELF_APPROVAL_FORBIDDEN");
        }

        // (c) The REQUESTER with a linked login (the NEW third leg) cannot authorize → 403.
        using (var requesterAuthorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, requesterUserId)))
        {
            var response = await PatchOneTimeIncomeAsync(requesterAuthorizer, fileId, incomeId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "ONE_TIME_INCOME_SELF_APPROVAL_FORBIDDEN");
        }

        // (d) A pure Admin (no AuthorizeOneTimeIncomes grant) is blocked by the policy → 403.
        using (var adminOnly = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario, fourthUserId)))
        {
            var response = await PatchOneTimeIncomeAsync(adminOnly, fileId, incomeId, "resolution", token, authorizeBody);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // (e) A fourth authorized user (subject/registrar/requester → none) authorizes → AUTORIZADO.
        using (var fourthAuthorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, fourthUserId)))
        {
            var response = await PatchOneTimeIncomeAsync(fourthAuthorizer, fileId, incomeId, "resolution", token, authorizeBody);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("AUTORIZADO", doc.RootElement.GetProperty("statusCode").GetString());
            // Guid `XxxId` serializes as `xxxPublicId` (public-contract naming convention).
            Assert.Equal(fourthUserId, doc.RootElement.GetProperty("decidedByUserPublicId").GetGuid());
        }
    }

    [Fact]
    public async Task OneTimeIncomes_RejectWithoutNote_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Nadia", "SinNota", "EMP-OTI-H", "nadia.oti.h@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Regina", "Solicitante", "EMP-OTI-H2", "regina.oti.h@empresa.test");

        using var registrarClient = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario, registrarUserId));
        var (incomeId, token) = await CreateOneTimeIncomeAsync(registrarClient, fileId, FixedOneTimeIncomeBody(requesterId));

        using var authorizerClient = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, authorizerUserId));
        var response = await PatchOneTimeIncomeAsync(
            authorizerClient, fileId, incomeId, "resolution", token, new { targetStatusCode = "RECHAZADO", note = (string?)null });
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_INCOME_DECISION_NOTE_REQUIRED");
    }

    [Fact]
    public async Task OneTimeIncomes_RetargetOnEnRevision_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Tomás", "Temprano", "EMP-OTI-I", "tomas.oti.i@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Renata", "Solicitante", "EMP-OTI-I2", "renata.oti.i@empresa.test");

        var (incomeId, token) = await CreateOneTimeIncomeAsync(client, fileId, FixedOneTimeIncomeBody(requesterId));

        // Re-target ("enviar a otro periodo") is only valid on AUTORIZADO — EN_REVISION → 422.
        var response = await PatchOneTimeIncomeAsync(
            client, fileId, incomeId, "period", token,
            new { payrollTypeCode = "QUINCENAL", payrollPeriodPublicId = (Guid?)null, payrollPeriodLabel = "Quincena 14/2026", payrollPeriodEndDate = (string?)null });
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_INCOME_NOT_RETARGETABLE");
    }

    [Fact]
    public async Task OneTimeIncomes_AuthorizeRetargetAndRevoke_FullLifecycle()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Vero", "Vigente", "EMP-OTI-J", "vero.oti.j@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Ronaldo", "Solicitante", "EMP-OTI-J2", "ronaldo.oti.j@empresa.test");

        using var managerClient = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario, registrarUserId));
        using var authorizerClient = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, authorizerUserId));

        var (incomeId, createToken) = await CreateOneTimeIncomeAsync(managerClient, fileId, FixedOneTimeIncomeBody(requesterId));

        // Authorize → AUTORIZADO.
        var authorizeResponse = await PatchOneTimeIncomeAsync(
            authorizerClient, fileId, incomeId, "resolution", createToken, new { targetStatusCode = "AUTORIZADO", note = (string?)null });
        authorizeResponse.EnsureSuccessStatusCode();
        var authorizedToken = await ReadTokenAsync(authorizeResponse);

        // Re-target (Manage) the AUTORIZADO income to the next period → still AUTORIZADO with the new label.
        var retargetResponse = await PatchOneTimeIncomeAsync(
            managerClient, fileId, incomeId, "period", authorizedToken,
            new { payrollTypeCode = "QUINCENAL", payrollPeriodPublicId = (Guid?)null, payrollPeriodLabel = "Quincena 14/2026", payrollPeriodEndDate = (string?)null });
        retargetResponse.EnsureSuccessStatusCode();
        Guid retargetedToken;
        using (var doc = JsonDocument.Parse(await retargetResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("AUTORIZADO", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.Equal("Quincena 14/2026", doc.RootElement.GetProperty("payrollPeriodLabel").GetString());
            retargetedToken = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        // Revoke (Authorize) from AUTORIZADO → ANULADO.
        var revokeResponse = await PatchOneTimeIncomeAsync(
            authorizerClient, fileId, incomeId, "revocation", retargetedToken, new { reason = "Ya no aplica" });
        revokeResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await revokeResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("ANULADO", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.False(doc.RootElement.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task OneTimeIncomes_ResolutionWithoutIfMatch_Returns400()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Íngrid", "IfMatch", "EMP-OTI-K", "ingrid.oti.k@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Renzo", "Solicitante", "EMP-OTI-K2", "renzo.oti.k@empresa.test");

        using var registrarClient = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario, registrarUserId));
        var (incomeId, token) = await CreateOneTimeIncomeAsync(registrarClient, fileId, FixedOneTimeIncomeBody(requesterId));

        using var authorizerClient = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, authorizerUserId));

        // Missing If-Match → 400.
        var missing = await PatchOneTimeIncomeAsync(
            authorizerClient, fileId, incomeId, "resolution", token: null, new { targetStatusCode = "AUTORIZADO", note = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // Stale If-Match → 409.
        var stale = await PatchOneTimeIncomeAsync(
            authorizerClient, fileId, incomeId, "resolution", Guid.NewGuid(), new { targetStatusCode = "AUTORIZADO", note = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }
}
