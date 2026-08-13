using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-08 — the salary tabulator line stores its class as a normalized **string code**
/// (<c>SalaryTabulatorLine.SalaryClassCode</c>, and the unique index is
/// <c>(TenantId, NormalizedSalaryClassCode, NormalizedSalaryScaleCode, EffectiveFromUtc)</c>): there is no FK.
/// Every response dropped that code and published a <c>salaryClassPublicId</c> **derived at read time** by a
/// correlated subquery that matched on code AND required the catalog item to be active.
///
/// So the only identification of a line's class was a join that two supported operations break — renaming the
/// class code, or inactivating the class — after which the line reported no class at all and could not be
/// grouped, filtered or exported.
///
/// These tests pin both halves: the code and name now travel with the line, and the class's code is no longer
/// free to move out from under the lines that point at it.
///
/// The contract assertions read the raw JSON on purpose: what matters is the wire property name the frontend
/// binds to, not that a value survives a round-trip through DTOs this test file defines itself.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private const string SalaryClassCodeProperty = "salaryClassCode";
    private const string SalaryClassNameProperty = "salaryClassName";

    [Fact]
    public async Task SalaryTabulator_Lines_ShouldExposeSalaryClassCodeAndName()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));

        var salaryClass = await ApproveSalaryTabulatorLineAsync(
            requesterClient, approverClient, scenario.TenantId, "CLS-H08-LIST", "Clase Lista", "S1", 1200m);

        using var document = await GetJsonAsync(
            approverClient,
            $"/api/v1/companies/{scenario.TenantId}/salary-tabulator/lines?page=1&pageSize=20");

        var line = document.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("CLS-H08-LIST", line.GetProperty(SalaryClassCodeProperty).GetString());
        Assert.Equal("Clase Lista", line.GetProperty(SalaryClassNameProperty).GetString());
        Assert.Equal(salaryClass.Id, line.GetProperty("salaryClassPublicId").GetGuid());
    }

    [Fact]
    public async Task SalaryTabulator_LineById_ShouldExposeSalaryClassCodeAndName()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));

        var salaryClass = await ApproveSalaryTabulatorLineAsync(
            requesterClient, approverClient, scenario.TenantId, "CLS-H08-BYID", "Clase Detalle", "S1", 1300m);
        var lineId = await GetSalaryTabulatorLineIdAsync(scenario.TenantId, salaryClass.Id, "S1");

        using var document = await GetJsonAsync(approverClient, $"/api/v1/salary-tabulator/lines/{lineId}");

        Assert.Equal("CLS-H08-BYID", document.RootElement.GetProperty(SalaryClassCodeProperty).GetString());
        Assert.Equal("Clase Detalle", document.RootElement.GetProperty(SalaryClassNameProperty).GetString());
    }

    [Fact]
    public async Task SalaryTabulator_ChangeRequestItems_ShouldExposeSalaryClassCodeAndName()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));

        var created = await CreateSalaryTabulatorRequestAsync(
            requesterClient, scenario.TenantId, "CLS-H08-REQ", "S1", 1400m);

        using var document = await GetJsonAsync(
            requesterClient, $"/api/v1/salary-tabulator/change-requests/{created.Id}");

        var item = document.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("CLS-H08-REQ", item.GetProperty(SalaryClassCodeProperty).GetString());
        Assert.Equal("CLS-H08-REQ", item.GetProperty(SalaryClassNameProperty).GetString());
    }

    // The band the tabulator publishes is keyed by this code, so letting it move is a silent disconnect:
    // every line keeps the old code and the class answers to the new one.
    [Fact]
    public async Task SalaryClass_RenameCode_WithTabulatorLines_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));

        var salaryClass = await ApproveSalaryTabulatorLineAsync(
            requesterClient, approverClient, scenario.TenantId, "CLS-H08-REN", "Clase Renombre", "S1", 1500m);

        var response = await PatchSalaryClassAsync(
            requesterClient,
            salaryClass.Id,
            await GetSalaryClassConcurrencyTokenAsync(requesterClient, scenario.TenantId, "CLS-H08-REN"),
            new { op = "replace", path = "/code", value = "CLS-H08-OTRO" });

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.Conflict, "POSITION_DESCRIPTION_CATALOG_CODE_IN_USE");
    }

    [Fact]
    public async Task SalaryClass_Inactivate_WithActiveTabulatorLines_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));

        var salaryClass = await ApproveSalaryTabulatorLineAsync(
            requesterClient, approverClient, scenario.TenantId, "CLS-H08-INA", "Clase Inactivar", "S1", 1600m);

        var response = await PatchSalaryClassAsync(
            requesterClient,
            salaryClass.Id,
            await GetSalaryClassConcurrencyTokenAsync(requesterClient, scenario.TenantId, "CLS-H08-INA"),
            new { op = "replace", path = "/isActive", value = false });

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.Conflict, "POSITION_DESCRIPTION_CATALOG_IN_USE");
    }

    // The guard must key off the code actually changing. `entity.Update` is called with the whole scalar set
    // on every scalar mutation, and the patch state is seeded from the current values, so a name-only patch
    // rewrites the same code — a guard hung on "has scalar mutation" would block renaming the label too.
    [Fact]
    public async Task SalaryClass_ChangeNameOnly_WithTabulatorLines_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));

        _ = await ApproveSalaryTabulatorLineAsync(
            requesterClient, approverClient, scenario.TenantId, "CLS-H08-NAME", "Nombre Viejo", "S1", 1700m);

        var response = await PatchSalaryClassAsync(
            requesterClient,
            (await GetSalaryClassAsync(requesterClient, scenario.TenantId, "CLS-H08-NAME")).Id,
            await GetSalaryClassConcurrencyTokenAsync(requesterClient, scenario.TenantId, "CLS-H08-NAME"),
            new { op = "replace", path = "/name", value = "Nombre Nuevo" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var patched = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Nombre Nuevo", patched.RootElement.GetProperty("name").GetString());
        Assert.Equal("CLS-H08-NAME", patched.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SalaryClass_RenameCode_WithoutTabulatorLines_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));

        var salaryClass = await EnsureSalaryClassAsync(client, scenario.TenantId, "CLS-H08-FREE");

        var response = await PatchSalaryClassAsync(
            client,
            salaryClass.Id,
            salaryClass.ConcurrencyToken,
            new { op = "replace", path = "/code", value = "CLS-H08-FREE2" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var patched = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CLS-H08-FREE2", patched.RootElement.GetProperty("code").GetString());
    }

    // Only *active* lines may block: a class whose lines are all historically closed has to stay retirable,
    // otherwise no class could ever be taken out of circulation.
    [Fact]
    public async Task SalaryClass_Inactivate_WithOnlyClosedLines_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));

        var salaryClass = await ApproveSalaryTabulatorLineAsync(
            requesterClient, approverClient, scenario.TenantId, "CLS-H08-CLOSED", "Clase Cerrada", "S1", 1800m);

        await ApproveSalaryTabulatorChangeAsync(
            requesterClient, approverClient, scenario.TenantId, salaryClass.Id, "S1", "Inactivate", 1800m);

        var response = await PatchSalaryClassAsync(
            requesterClient,
            salaryClass.Id,
            await GetSalaryClassConcurrencyTokenAsync(requesterClient, scenario.TenantId, "CLS-H08-CLOSED"),
            new { op = "replace", path = "/isActive", value = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Pre-existing data: classes inactivated before the guard existed. The derived id stays null by design,
    // so the code and name are the only thing keeping those lines attributable.
    [Fact]
    public async Task SalaryTabulator_Lines_ShouldExposeCodeAndName_WhenClassInactive()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));

        _ = await ApproveSalaryTabulatorLineAsync(
            requesterClient, approverClient, scenario.TenantId, "CLS-H08-DEAD", "Clase Muerta", "S1", 1900m);

        await InactivateSalaryClassDirectlyAsync(scenario.TenantId, "CLS-H08-DEAD");

        using var document = await GetJsonAsync(
            approverClient,
            $"/api/v1/companies/{scenario.TenantId}/salary-tabulator/lines?page=1&pageSize=20");

        var line = document.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("CLS-H08-DEAD", line.GetProperty(SalaryClassCodeProperty).GetString());
        Assert.Equal("Clase Muerta", line.GetProperty(SalaryClassNameProperty).GetString());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("salaryClassPublicId").ValueKind);
    }

    private async Task<PositionDescriptionCatalogItemProbe> ApproveSalaryTabulatorLineAsync(
        HttpClient requesterClient,
        HttpClient approverClient,
        Guid companyId,
        string salaryClassCode,
        string salaryClassName,
        string salaryScaleCode,
        decimal baseAmount)
    {
        var salaryClass = await EnsurePositionDescriptionCatalogItemAsync(
            requesterClient, companyId, "salary-classes", salaryClassCode, salaryClassName);

        await ApproveSalaryTabulatorChangeAsync(
            requesterClient, approverClient, companyId, salaryClass.Id, salaryScaleCode, "Create", baseAmount);

        return new PositionDescriptionCatalogItemProbe(salaryClass.Id, salaryClass.ConcurrencyToken);
    }

    private async Task ApproveSalaryTabulatorChangeAsync(
        HttpClient requesterClient,
        HttpClient approverClient,
        Guid companyId,
        Guid salaryClassId,
        string salaryScaleCode,
        string changeType,
        decimal proposedBaseAmount)
    {
        var createResponse = await requesterClient.PostJsonAsync(
            $"/api/v1/companies/{companyId}/salary-tabulator/change-requests",
            new
            {
                effectiveFromUtc = DateTime.UtcNow.Date,
                effectiveToUtc = (DateTime?)null,
                items = new[]
                {
                    new
                    {
                        salaryClassPublicId = salaryClassId,
                        salaryScaleCode,
                        currencyCode = "USD",
                        changeType,
                        proposedBaseAmount,
                        proposedMinAmount = (decimal?)null,
                        proposedMaxAmount = (decimal?)null,
                        notes = (string?)null
                    }
                }
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(created);

        var submitResponse = await requesterClient.PatchJsonAsync(
            $"/api/v1/salary-tabulator/change-requests/{created!.Id}/submit",
            new { concurrencyToken = created.ConcurrencyToken });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submitted);

        var approveResponse = await approverClient.PatchJsonAsync(
            $"/api/v1/salary-tabulator/change-requests/{created.Id}/approve",
            new { decisionComment = "Aprobado", concurrencyToken = submitted!.ConcurrencyToken });
        approveResponse.EnsureSuccessStatusCode();
    }

    private Task<PositionDescriptionCatalogItem> GetSalaryClassAsync(
        HttpClient client,
        Guid companyId,
        string code) =>
        EnsurePositionDescriptionCatalogItemAsync(client, companyId, "salary-classes", code);

    private async Task<Guid> GetSalaryClassConcurrencyTokenAsync(
        HttpClient client,
        Guid companyId,
        string code) =>
        (await GetSalaryClassAsync(client, companyId, code)).ConcurrencyToken;

    private static async Task<HttpResponseMessage> PatchSalaryClassAsync(
        HttpClient client,
        Guid salaryClassId,
        Guid concurrencyToken,
        object operation)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/position-description-catalogs/salary-classes/items/{salaryClassId}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new[] { operation }),
                Encoding.UTF8,
                "application/json-patch+json")
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{concurrencyToken}\"");

        return await client.SendAsync(request);
    }

    private async Task InactivateSalaryClassDirectlyAsync(Guid companyId, string code)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var normalizedCode = code.Trim().ToUpperInvariant();
        var item = await dbContext.PositionDescriptionCatalogItems
            // Intentional tenant filter bypass: the test scope carries no tenant.
            .IgnoreQueryFilters()
            .SingleAsync(candidate =>
                candidate.TenantId == companyId &&
                candidate.CatalogType == PositionDescriptionCatalogType.SalaryClass &&
                candidate.NormalizedCode == normalizedCode);

        item.Inactivate();
        _ = await dbContext.SaveChangesAsync();
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private sealed record PositionDescriptionCatalogItemProbe(Guid Id, Guid ConcurrencyToken);
}
