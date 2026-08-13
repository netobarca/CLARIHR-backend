using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-11 — reordering a catalog was N patches, and on the occupational pyramid it was worse than N: because
/// <c>levelOrder</c> is UNIQUE per tenant, simply swapping two levels was impossible one call at a time —
/// setting the first to the other's number collides, so a client had to invent a temporary value and make three
/// calls. That is the pain the finding actually described; the uniqueness itself is a real invariant (a pyramid
/// is a strict ranking) and stays.
///
/// The contract avoids the whole class of collision: the client sends the COMPLETE set of ids in the desired
/// order and the server assigns 10, 20, 30… in one transaction. There are no numbers in the request, so a
/// client cannot construct a conflict, and the operation is idempotent.
///
/// Deliberately NO <c>If-Match</c>: there is no single aggregate to hold a token for, and the request carries
/// the full desired order, so the semantics are last-writer-wins by design — the honest reading of a
/// drag-and-drop save. Two people reordering at once means the loser re-drags.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task PyramidLevels_Reorder_ShouldRewriteOrderInTenSteps()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        var first = await CreatePyramidLevelWithOrderAsync(client, scenario.TenantId, "OPL-RO-A", 10);
        var second = await CreatePyramidLevelWithOrderAsync(client, scenario.TenantId, "OPL-RO-B", 20);
        var third = await CreatePyramidLevelWithOrderAsync(client, scenario.TenantId, "OPL-RO-C", 30);

        var response = await ReorderAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/occupational-pyramid-levels/order",
            [third.Id, first.Id, second.Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var byCode = await GetPyramidLevelOrdersAsync(client, scenario.TenantId);
        Assert.Equal(10, byCode["OPL-RO-C"]);
        Assert.Equal(20, byCode["OPL-RO-A"]);
        Assert.Equal(30, byCode["OPL-RO-B"]);
    }

    /// <summary>
    /// THE test of this feature. A straight swap is what the unique index makes impossible to express as
    /// independent updates, and it is also what breaks a naive bulk implementation: EF issues one UPDATE per
    /// row inside the transaction, and a non-deferrable unique index is checked per statement, so the
    /// intermediate state violates it. The handler has to move the whole set to a free band above the current
    /// maximum first, and only then write the final values.
    /// </summary>
    [Fact]
    public async Task PyramidLevels_Reorder_SwappingTwoLevels_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        var first = await CreatePyramidLevelWithOrderAsync(client, scenario.TenantId, "OPL-SWAP-A", 10);
        var second = await CreatePyramidLevelWithOrderAsync(client, scenario.TenantId, "OPL-SWAP-B", 20);

        var response = await ReorderAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/occupational-pyramid-levels/order",
            [second.Id, first.Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var byCode = await GetPyramidLevelOrdersAsync(client, scenario.TenantId);
        Assert.Equal(10, byCode["OPL-SWAP-B"]);
        Assert.Equal(20, byCode["OPL-SWAP-A"]);
    }

    // Partial lists are rejected rather than guessed at: whatever is left out keeps its old number, which can
    // collide with the ones just assigned, and on a strict ranking a half-applied order is meaningless.
    [Fact]
    public async Task PyramidLevels_Reorder_WithIncompleteList_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        var first = await CreatePyramidLevelWithOrderAsync(client, scenario.TenantId, "OPL-INC-A", 10);
        _ = await CreatePyramidLevelWithOrderAsync(client, scenario.TenantId, "OPL-INC-B", 20);

        var response = await ReorderAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/occupational-pyramid-levels/order",
            [first.Id]);

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "OCCUPATIONAL_PYRAMID_LEVEL_ORDER_SET_INCOMPLETE");
    }

    [Fact]
    public async Task PyramidLevels_Reorder_WithDuplicateIds_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        var first = await CreatePyramidLevelWithOrderAsync(client, scenario.TenantId, "OPL-DUP-A", 10);
        _ = await CreatePyramidLevelWithOrderAsync(client, scenario.TenantId, "OPL-DUP-B", 20);

        var response = await ReorderAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/occupational-pyramid-levels/order",
            [first.Id, first.Id]);

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "OCCUPATIONAL_PYRAMID_LEVEL_ORDER_SET_INCOMPLETE");
    }

    [Fact]
    public async Task PositionDescriptionCatalogItems_Reorder_ShouldRewriteOrderInTenSteps()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var first = await EnsurePositionDescriptionCatalogItemAsync(
            client, scenario.TenantId, "work-equipments", "EQ-RO-A");
        var second = await EnsurePositionDescriptionCatalogItemAsync(
            client, scenario.TenantId, "work-equipments", "EQ-RO-B");

        var response = await ReorderAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/position-description-catalogs/work-equipments/items/order",
            [second.Id, first.Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await GetJsonAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/position-description-catalogs/work-equipments/items?page=1&pageSize=50");
        var codes = document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Equal(new[] { "EQ-RO-B", "EQ-RO-A" }, codes);
    }

    [Fact]
    public async Task JobCatalogs_Reorder_ShouldRewriteOrderInTenSteps()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        var first = await CreateJobCatalogItemAsync(
            client, scenario.TenantId, JobCatalogCategory.Training, "TRN-RO-A", "Primero");
        var second = await CreateJobCatalogItemAsync(
            client, scenario.TenantId, JobCatalogCategory.Training, "TRN-RO-B", "Segundo");

        var response = await ReorderAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/job-catalogs/{JobCatalogCategory.Training}/order",
            [second.Id, first.Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await GetJsonAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/job-catalogs/{JobCatalogCategory.Training}?page=1&pageSize=50");
        var codes = document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Equal(new[] { "TRN-RO-B", "TRN-RO-A" }, codes);
    }

    private static Task<HttpResponseMessage> ReorderAsync(HttpClient client, string url, Guid[] orderedPublicIds) =>
        client.PatchJsonAsync(url, new { orderedPublicIds });

    private async Task<OccupationalPyramidLevelItem> CreatePyramidLevelWithOrderAsync(
        HttpClient client,
        Guid tenantId,
        string code,
        int levelOrder)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{tenantId}/occupational-pyramid-levels",
            new { code, name = code, levelOrder, description = (string?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<OccupationalPyramidLevelItem>(JsonOptions))!;
    }

    private async Task<Dictionary<string, int>> GetPyramidLevelOrdersAsync(HttpClient client, Guid tenantId)
    {
        using var document = await GetJsonAsync(
            client, $"/api/v1/companies/{tenantId}/occupational-pyramid-levels?page=1&pageSize=100");

        return document.RootElement.GetProperty("items").EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("code").GetString()!,
                item => item.GetProperty("levelOrder").GetInt32(),
                StringComparer.Ordinal);
    }
}
