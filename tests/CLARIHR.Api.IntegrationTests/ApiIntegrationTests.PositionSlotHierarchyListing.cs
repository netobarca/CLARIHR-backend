using System.Net;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-16 — the slot hierarchy is persisted correctly and readable from `/graph` and from the detail, but the
/// LISTING omitted it: 32 fields per item, including denormalised `jobProfileCode`, `orgUnitName`,
/// `workCenterCode` and `positionCategoryName`, and not one of the four dependency fields.
///
/// That is not merely inconvenient — a client cannot tell "this slot has no parent" from "the listing does not
/// say". Verified live against the running API on the real company: `/graph` answered 33 nodes / 32 direct
/// edges / 1 root while the listing made all 33 look like roots, and the playbook's own §4.4 check reported
/// `plazas raíz: 33 → REVISAR` on a tree that was perfect.
///
/// The query already paid for this: `BuildJoinedQuery` has had both dependency LEFT JOINs all along (the export
/// consumes them). Only the projection left them out.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task PositionSlots_List_ShouldExposeDirectDependency()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var (parent, child) = await CreateSlotPairAsync(client, scenario.TenantId, "H16A");

        var items = await GetPositionSlotListAsync(client, scenario.TenantId);
        var childItem = items.Single(item => item.GetProperty("code").GetString() == child);

        Assert.Equal(parent.Id, childItem.GetProperty("directDependencyPositionSlotPublicId").GetGuid());
        Assert.Equal(parent.Code, childItem.GetProperty("directDependencyPositionSlotCode").GetString());
    }

    /// <summary>
    /// The half that makes the field trustworthy: the root must report `null`, not be absent. Absence is what
    /// made every slot look like a root.
    /// </summary>
    [Fact]
    public async Task PositionSlots_List_RootSlot_ShouldReportNullDependencies()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var (parent, _) = await CreateSlotPairAsync(client, scenario.TenantId, "H16B");

        var items = await GetPositionSlotListAsync(client, scenario.TenantId);
        var rootItem = items.Single(item => item.GetProperty("code").GetString() == parent.Code);

        Assert.Equal(JsonValueKind.Null, rootItem.GetProperty("directDependencyPositionSlotPublicId").ValueKind);
        Assert.Equal(JsonValueKind.Null, rootItem.GetProperty("directDependencyPositionSlotCode").ValueKind);
        // The functional dependency is unused in the real data (0 rows), but it is published for symmetry with
        // the detail and the export — a half-covered shape is what this finding is about.
        Assert.Equal(JsonValueKind.Null, rootItem.GetProperty("functionalDependencyPositionSlotPublicId").ValueKind);
        Assert.Equal(JsonValueKind.Null, rootItem.GetProperty("functionalDependencyPositionSlotCode").ValueKind);
    }

    /// <summary>
    /// Reproduces the playbook §4.4 false negative. Counting roots over the listing is exactly what the
    /// verification script does, and it reported 33 instead of 1 because the field did not exist.
    /// </summary>
    [Fact]
    public async Task PositionSlots_List_CountingRootsOverTheListing_ShouldMatchTheGraph()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var (_, _) = await CreateSlotPairAsync(client, scenario.TenantId, "H16C");

        var items = await GetPositionSlotListAsync(client, scenario.TenantId);
        var rootsFromListing = items.Count(item =>
            item.GetProperty("directDependencyPositionSlotPublicId").ValueKind == JsonValueKind.Null);

        using var graph = await GetJsonAsync(
            client, $"/api/v1/companies/{scenario.TenantId}/position-slots/graph");
        var nodes = graph.RootElement.GetProperty("nodes").GetArrayLength();
        var directEdges = graph.RootElement.GetProperty("edges").EnumerateArray()
            .Count(edge => edge.GetProperty("relationType").GetString() == "Direct");

        Assert.Equal(nodes - directEdges, rootsFromListing);
        Assert.Equal(1, rootsFromListing);
    }

    [Fact]
    public async Task PositionSlots_List_FilteredByDirectDependency_ShouldReturnOnlyChildren()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var (parent, child) = await CreateSlotPairAsync(client, scenario.TenantId, "H16D");

        using var document = await GetJsonAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/position-slots" +
            $"?directDependencyPositionSlotPublicId={parent.Id}&page=1&pageSize=50");

        var codes = document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Equal(new[] { child }, codes);
    }

    private async Task<(PositionSlotItem Parent, string ChildCode)> CreateSlotPairAsync(
        HttpClient client,
        Guid companyId,
        string tag)
    {
        var orgUnit = await CreateOrgUnitAsync(client, companyId, $"DIR-{tag}", $"Direccion {tag}", "Direccion");
        var profile = await CreateJobProfileAsync(client, companyId, $"JP-{tag}", $"Perfil {tag}", orgUnit.Id);
        var parent = await CreatePositionSlotAsync(client, companyId, $"PS-{tag}-PADRE", "Plaza padre", profile.Id, maxEmployees: 1);

        var childCode = $"PS-{tag}-HIJA";
        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/position-slots", new
        {
            code = childCode,
            title = "Plaza hija",
            jobProfilePublicId = profile.Id,
            workCenterPublicId = (Guid?)null,
            directDependencyPositionSlotPublicId = parent.Id,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            occupiedEmployees = 0,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            notes = (string?)null
        });
        response.EnsureSuccessStatusCode();

        return (parent, childCode);
    }

    private async Task<JsonElement[]> GetPositionSlotListAsync(HttpClient client, Guid companyId)
    {
        using var document = await GetJsonAsync(
            client, $"/api/v1/companies/{companyId}/position-slots?page=1&pageSize=100");

        return document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.Clone())
            .ToArray();
    }
}
