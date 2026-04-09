using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Platform;

namespace CLARIHR.Api.IntegrationTests;

public sealed class BackofficeCommercialPlansIntegrationTests(BackofficeIntegrationTestWebApplicationFactory factory)
    : IClassFixture<BackofficeIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private static readonly Guid PlatformOperatorUserId = Guid.Parse("90000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task CommercialPlans_Search_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/commercial-plans");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CommercialPlans_Search_WithCoreClientType_ShouldReturnForbidden()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformOperatorUserId,
                "platform.admin@clarihr.test",
                "hashed-password"));
        using var client = factory.CreateClientFor(TestUserContext.AuthenticatedWithoutTenant(PlatformOperatorUserId));

        var response = await client.GetAsync("/api/platform/commercial-plans");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CommercialPlans_CrudLifecycle_WithPlatformOperator_ShouldSucceed()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformOperatorUserId,
                "platform.admin@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin));
        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(PlatformOperatorUserId));

        var initialListResponse = await client.GetAsync("/api/platform/commercial-plans?status=Active&page=1&pageSize=20");
        initialListResponse.EnsureSuccessStatusCode();

        var initialList = await initialListResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<CommercialPlanSummaryEnvelope>>(JsonOptions);
        Assert.NotNull(initialList);
        Assert.Contains(initialList!.Items, static item => item.Code == "FREE" && item.IsSystemPlan);
        Assert.Contains(initialList.Items, static item => item.Code == "MASTER" && item.IsSystemPlan);
        Assert.Contains(initialList.Items, static item => item.Code == "FREE" && item.ModuleCount >= 1);
        Assert.Contains(initialList.Items, static item => item.Code == "MASTER" && item.ModuleCount >= 1);

        var createResponse = await client.PostJsonAsync("/api/platform/commercial-plans", new
        {
            code = "PRO",
            name = "Professional",
            description = "Plan base profesional",
            baseMonthlyFee = 120m,
            pricePerActiveEmployee = 3.5m,
            status = CommercialPlanStatus.Draft,
            moduleKeys = new[]
            {
                CommercialModuleKeys.JobProfiles,
                CommercialModuleKeys.OrgUnits
            },
            limits = new[]
            {
                new { code = "employees", value = 25m },
                new { code = "locations", value = 3m }
            }
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CommercialPlanEnvelope>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("PRO", created!.Code);
        Assert.Equal(CommercialPlanStatus.Draft, created.Status);
        Assert.False(created.IsSystemPlan);
        Assert.Equal(2, created.Limits.Count);
        Assert.Equal(2, created.ModuleCount);
        Assert.Equal(
            [CommercialModuleKeys.JobProfiles, CommercialModuleKeys.OrgUnits],
            created.ModuleKeys.OrderBy(static key => key).ToArray());

        var getResponse = await client.GetAsync($"/api/platform/commercial-plans/{created.Id}");
        getResponse.EnsureSuccessStatusCode();

        var fetched = await getResponse.Content.ReadFromJsonAsync<CommercialPlanEnvelope>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(created.ModuleKeys.OrderBy(static key => key), fetched.ModuleKeys.OrderBy(static key => key));

        var updateResponse = await client.PutJsonAsync($"/api/platform/commercial-plans/{created.Id}", new
        {
            code = "PRO",
            name = "Professional Plus",
            description = "Plan actualizado",
            baseMonthlyFee = 180m,
            pricePerActiveEmployee = 4m,
            moduleKeys = new[]
            {
                CommercialModuleKeys.JobProfiles,
                CommercialModuleKeys.PositionSlots
            },
            limits = new[]
            {
                new { code = "work_centers", value = 10m }
            },
            concurrencyToken = created.ConcurrencyToken
        });

        updateResponse.EnsureSuccessStatusCode();

        var updated = await updateResponse.Content.ReadFromJsonAsync<CommercialPlanEnvelope>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Professional Plus", updated!.Name);
        Assert.Equal(CommercialPlanStatus.Draft, updated.Status);
        var updatedLimit = Assert.Single(updated.Limits);
        Assert.Equal("WORK_CENTERS", updatedLimit.Code);
        Assert.Equal(10m, updatedLimit.Value);
        Assert.Equal(2, updated.ModuleCount);
        Assert.Equal(
            [CommercialModuleKeys.JobProfiles, CommercialModuleKeys.PositionSlots],
            updated.ModuleKeys.OrderBy(static key => key).ToArray());

        var activateResponse = await client.PatchAsJsonAsync(
            $"/api/platform/commercial-plans/{created.Id}/activate",
            new { concurrencyToken = updated.ConcurrencyToken });
        activateResponse.EnsureSuccessStatusCode();

        var activated = await activateResponse.Content.ReadFromJsonAsync<CommercialPlanEnvelope>(JsonOptions);
        Assert.NotNull(activated);
        Assert.Equal(CommercialPlanStatus.Active, activated!.Status);

        var inactivateResponse = await client.PatchAsJsonAsync(
            $"/api/platform/commercial-plans/{created.Id}/inactivate",
            new { concurrencyToken = activated.ConcurrencyToken });
        inactivateResponse.EnsureSuccessStatusCode();

        var inactivated = await inactivateResponse.Content.ReadFromJsonAsync<CommercialPlanEnvelope>(JsonOptions);
        Assert.NotNull(inactivated);
        Assert.Equal(CommercialPlanStatus.Inactive, inactivated!.Status);

        var filteredResponse = await client.GetAsync("/api/platform/commercial-plans?status=Inactive&q=pro&page=1&pageSize=10");
        filteredResponse.EnsureSuccessStatusCode();

        var filtered = await filteredResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<CommercialPlanSummaryEnvelope>>(JsonOptions);
        Assert.NotNull(filtered);
        Assert.Contains(filtered!.Items, item => item.Id == created.Id);
    }

    [Fact]
    public async Task CommercialModules_List_WithPlatformOperator_ShouldReturnCatalog()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformOperatorUserId,
                "platform.admin@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin));
        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(PlatformOperatorUserId));

        var response = await client.GetAsync("/api/platform/commercial-modules");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<CommercialModuleEnvelope>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!, static module => module.Key == CommercialModuleKeys.Rbac);
        Assert.Contains(payload!, static module => module.Key == CommercialModuleKeys.JobProfiles);
        Assert.Contains(payload!, static module => module.Key == CommercialModuleKeys.PersonnelFiles);
    }

    private sealed record PagedResponseEnvelope<TItem>(
        IReadOnlyCollection<TItem> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);

    private sealed record CommercialPlanSummaryEnvelope(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        decimal BaseMonthlyFee,
        decimal PricePerActiveEmployee,
        CommercialPlanStatus Status,
        bool IsSystemPlan,
        int ModuleCount,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record CommercialPlanEnvelope(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        decimal BaseMonthlyFee,
        decimal PricePerActiveEmployee,
        CommercialPlanStatus Status,
        bool IsSystemPlan,
        int ModuleCount,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc,
        IReadOnlyCollection<string> ModuleKeys,
        IReadOnlyCollection<CommercialPlanLimitEnvelope> Limits);

    private sealed record CommercialPlanLimitEnvelope(string Code, decimal Value);

    private sealed record CommercialModuleEnvelope(string Key, string DisplayName, string Description);
}
