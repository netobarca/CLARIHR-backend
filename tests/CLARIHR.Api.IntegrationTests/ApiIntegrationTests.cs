using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.OrgUnits;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Api.IntegrationTests;

public sealed class ApiIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    static ApiIntegrationTests()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public async Task Register_ShouldReturnCreatedAndTokens()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Admin",
            lastName = "Local",
            email = "admin.local@test.com",
            password = "StrongPass123!",
            companyName = "Acme Local",
            country = "SV",
            source = "integration-tests"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.Equal("admin.local@test.com", payload.User.Email);
    }

    [Fact]
    public async Task AccountCompanies_List_ShouldReturnOwnedCompaniesOnly()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync("/api/account/companies?page=1&pageSize=20");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<AccountCompanyItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.TotalCount);
        Assert.Contains(payload.Items, static item => item.Name == "Acme One" && item.IsActiveContext);
        Assert.Contains(payload.Items, static item => item.Name == "Acme Two" && !item.IsActiveContext);
    }

    [Fact]
    public async Task AccountCompanies_Create_WhenBelowLimit_ShouldReturnCreatedCompanyWithoutSwitchingContext()
    {
        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            var companyToArchive = dbContext.Companies.Single(company => company.Slug == "acme-two");
            companyToArchive.Archive();
            await dbContext.SaveChangesAsync();
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PostAsJsonAsync("/api/account/companies", new
        {
            name = "Acme Three"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Acme Three", payload!.Name);
        Assert.Equal("FREE", payload.PlanCode);
        Assert.False(payload.IsActiveContext);
        Assert.Equal("Active", payload.Status);
    }

    [Fact]
    public async Task AccountCompanies_Create_ShouldSeedDefaultLocationsForNewTenant()
    {
        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            var companyToArchive = dbContext.Companies.Single(company => company.Slug == "acme-two");
            companyToArchive.Archive();
            await dbContext.SaveChangesAsync();
        });

        using var accountClient = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var createResponse = await accountClient.PostAsJsonAsync("/api/account/companies", new
        {
            name = "Acme Three"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var company = await createResponse.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(company);

        using var locationClient = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, company!.CompanyId, LocationPermissionCodes.Read));

        var hierarchyResponse = await locationClient.GetAsync($"/api/v1/companies/{company.CompanyId}/location-hierarchy");

        hierarchyResponse.EnsureSuccessStatusCode();

        var hierarchy = await hierarchyResponse.Content.ReadFromJsonAsync<LocationHierarchyItem>(JsonOptions);
        Assert.NotNull(hierarchy);
        Assert.False(hierarchy!.IsMultiLevel);
        Assert.Equal("GENERAL", hierarchy.DefaultGroupCode);
        Assert.Equal("General", hierarchy.DefaultGroupName);
    }

    [Fact]
    public async Task AccountCompanies_Create_WhenLimitReached_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PostAsJsonAsync("/api/account/companies", new
        {
            name = "Acme Three"
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "COMPANY_LIMIT_REACHED");
    }

    [Fact]
    public async Task AccountCompanies_Update_ShouldRenameOwnedCompany()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PutAsJsonAsync($"/api/account/companies/{scenario.OtherTenantId}", new
        {
            name = "Acme Two Updated"
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Acme Two Updated", payload!.Name);
        Assert.Equal("acme-two", payload.Slug);
    }

    [Fact]
    public async Task AccountCompanies_Archive_CurrentActiveCompany_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PatchAsync($"/api/account/companies/{scenario.TenantId}/archive", content: null);

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "ACTIVE_COMPANY_ARCHIVE_FORBIDDEN");
    }

    [Fact]
    public async Task AccountCompanies_Reactivate_ArchivedCompany_ShouldReturnActiveCompany()
    {
        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            var companyToArchive = dbContext.Companies.Single(company => company.Slug == "acme-two");
            companyToArchive.Archive();
            await dbContext.SaveChangesAsync();
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PatchAsync($"/api/account/companies/{scenario.OtherTenantId}/reactivate", content: null);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Active", payload!.Status);
        Assert.Equal("Acme Two", payload.Name);
    }

    [Fact]
    public async Task AccountCompanies_GetById_ForUnownedCompany_ShouldReturn403()
    {
        Guid foreignCompanyId = Guid.Empty;
        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            var foreignCompany = Company.Create(
                "Foreign Company",
                "foreign-company",
                Guid.Parse("99999999-9999-9999-9999-999999999999"));
            dbContext.Companies.Add(foreignCompany);
            await dbContext.SaveChangesAsync();
            foreignCompanyId = foreignCompany.PublicId;
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/account/companies/{foreignCompanyId}");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "COMPANY_OWNERSHIP_FORBIDDEN");
    }

    [Fact]
    public async Task AccountCompanies_Switch_ShouldReturnTokenWithSelectedTenant()
    {
        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            var actorUser = dbContext.AuthUsers.Single(user => user.PublicId == Guid.Parse("11111111-1111-1111-1111-111111111111"));
            var companyA = dbContext.Companies.Single(company => company.Slug == "acme-one");
            var companyB = dbContext.Companies.Single(company => company.Slug == "acme-two");
            var roleA = dbContext.IamRoles.IgnoreQueryFilters().Single(role => role.Name == "Security Operator");
            var roleB = dbContext.IamRoles.IgnoreQueryFilters().Single(role => role.Name == "Auditor B");

            dbContext.UserCompanyMemberships.Add(UserCompanyMembership.Create(actorUser.Id, companyA.Id, roleA.Id, isPrimary: true));
            dbContext.UserCompanyMemberships.Add(UserCompanyMembership.Create(actorUser.Id, companyB.Id, roleB.Id, isPrimary: false));
            await dbContext.SaveChangesAsync();
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PostAsync($"/api/account/companies/{scenario.OtherTenantId}/switch", content: null);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SwitchActiveCompanyItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(scenario.OtherTenantId, payload!.ActiveCompany.CompanyId);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        var tid = token.Claims.Single(claim => claim.Type == "tid").Value;
        Assert.Equal(scenario.OtherTenantId.ToString(), tid);
    }

    [Fact]
    public async Task LocationHierarchy_Get_ShouldReturnSeededDefaults()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/location-hierarchy");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LocationHierarchyItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload!.IsMultiLevel);
        Assert.Equal("GENERAL", payload.DefaultGroupCode);
        Assert.Equal("General", payload.DefaultGroupName);
    }

    [Fact]
    public async Task LocationHierarchy_Update_WithValidToken_ShouldReturnUpdatedConfig()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var current = await GetLocationHierarchyAsync(client, scenario.TenantId);

        var response = await client.PutAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/location-hierarchy", new
        {
            isMultiLevel = true,
            concurrencyToken = current.ConcurrencyToken
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LocationHierarchyItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.True(payload!.IsMultiLevel);
        Assert.NotEqual(current.ConcurrencyToken, payload.ConcurrencyToken);
    }

    [Fact]
    public async Task LocationHierarchy_Update_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var response = await client.PutAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/location-hierarchy", new
        {
            isMultiLevel = true,
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task LocationHierarchy_WithTenantMismatch_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.OtherTenantId}/location-hierarchy");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task LocationLevels_List_ShouldReturnSeededGeneralLevel()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/location-levels");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<LocationLevelItem>>(JsonOptions);
        Assert.NotNull(payload);
        var level = Assert.Single(payload!);
        Assert.Equal(1, level.LevelOrder);
        Assert.Equal("General", level.DisplayName);
        Assert.True(level.IsActive);
        Assert.True(level.IsRequired);
        Assert.True(level.AllowsWorkCenters);
    }

    [Fact]
    public async Task LocationGroups_Create_ShouldReturnCreatedGroup()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/location-groups", new
        {
            levelOrder = 1,
            code = "WEST",
            name = "West",
            parentId = (Guid?)null,
            description = "Western location group"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LocationGroupItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("WEST", payload!.Code);
        Assert.Equal("West", payload.Name);
        Assert.True(payload.IsActive);
        Assert.False(payload.IsDefault);
    }

    [Fact]
    public async Task WorkCenters_Create_WithAddressRequirementSatisfied_ShouldReturnCreatedCenter()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var defaultGroup = await GetDefaultLocationGroupAsync(client, scenario.TenantId);

        var typeResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        Assert.Equal(HttpStatusCode.Created, typeResponse.StatusCode);
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro General",
            workCenterTypeId = workCenterType!.Id,
            locationGroupId = defaultGroup.Id,
            address = "San Salvador",
            geoLat = (decimal?)null,
            geoLong = (decimal?)null,
            phone = "2222-2222",
            email = "centro@acme-one.test",
            notes = "Centro inicial"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<WorkCenterItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("CEN-001", payload!.Code);
        Assert.Equal("Agency", payload.WorkCenterTypeName);
        Assert.Equal(defaultGroup.Id, payload.LocationGroupId);
        Assert.True(payload.IsActive);
    }

    [Fact]
    public async Task WorkCenters_Create_WhenAddressIsMissingForRequiredType_ShouldReturn400()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var defaultGroup = await GetDefaultLocationGroupAsync(client, scenario.TenantId);

        var typeResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro General",
            workCenterTypeId = workCenterType!.Id,
            locationGroupId = defaultGroup.Id,
            address = (string?)null,
            geoLat = (decimal?)null,
            geoLong = (decimal?)null,
            phone = "2222-2222",
            email = "centro@acme-one.test",
            notes = "Centro inicial"
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "WORK_CENTER_ADDRESS_REQUIRED");
    }

    [Fact]
    public async Task LocationGroups_Inactivate_WhenActiveWorkCentersExist_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var groupResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/location-groups", new
        {
            levelOrder = 1,
            code = "WEST",
            name = "West",
            parentId = (Guid?)null,
            description = "Western location group"
        });
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<LocationGroupItem>(JsonOptions);

        var typeResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var createCenterResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro West",
            workCenterTypeId = workCenterType!.Id,
            locationGroupId = group!.Id,
            address = "San Salvador",
            geoLat = (decimal?)null,
            geoLong = (decimal?)null,
            phone = "2222-2222",
            email = "west@acme-one.test",
            notes = "Centro West"
        });
        createCenterResponse.EnsureSuccessStatusCode();

        var response = await client.PatchAsJsonAsync($"/api/v1/location-groups/{group.Id}/inactivate", new
        {
            concurrencyToken = group.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "LOCATION_GROUP_HAS_ACTIVE_WORK_CENTERS");
    }

    [Fact]
    public async Task WorkCenterTypes_Inactivate_WhenTypeIsInUse_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var defaultGroup = await GetDefaultLocationGroupAsync(client, scenario.TenantId);

        var typeResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var createCenterResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro General",
            workCenterTypeId = workCenterType!.Id,
            locationGroupId = defaultGroup.Id,
            address = "San Salvador",
            geoLat = (decimal?)null,
            geoLong = (decimal?)null,
            phone = "2222-2222",
            email = "centro@acme-one.test",
            notes = "Centro inicial"
        });
        createCenterResponse.EnsureSuccessStatusCode();

        var response = await client.PatchAsJsonAsync($"/api/v1/work-center-types/{workCenterType.Id}/inactivate", new
        {
            concurrencyToken = workCenterType.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "WORK_CENTER_TYPE_IN_USE");
    }

    [Fact]
    public async Task WorkCenters_Inactivate_ShouldReturnInactiveCenter()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var defaultGroup = await GetDefaultLocationGroupAsync(client, scenario.TenantId);

        var typeResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var createCenterResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro General",
            workCenterTypeId = workCenterType!.Id,
            locationGroupId = defaultGroup.Id,
            address = "San Salvador",
            geoLat = (decimal?)null,
            geoLong = (decimal?)null,
            phone = "2222-2222",
            email = "centro@acme-one.test",
            notes = "Centro inicial"
        });
        createCenterResponse.EnsureSuccessStatusCode();
        var workCenter = await createCenterResponse.Content.ReadFromJsonAsync<WorkCenterItem>(JsonOptions);

        var response = await client.PatchAsJsonAsync($"/api/v1/work-centers/{workCenter!.Id}/inactivate", new
        {
            concurrencyToken = workCenter.ConcurrencyToken
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WorkCenterItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload!.IsActive);
    }

    [Fact]
    public async Task OrgUnits_Create_ShouldReturnCreatedUnit()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-001",
            name = "Direccion General",
            unitType = "Direccion",
            parentId = (Guid?)null,
            sortOrder = 1,
            description = "Direccion principal",
            costCenterCode = "CC-01",
            managerEmployeeId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<OrgUnitItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("DIR-001", payload!.Code);
        Assert.Equal(OrgUnitType.Direccion, payload.UnitType);
        Assert.Null(payload.ParentId);
        Assert.True(payload.IsActive);
    }

    [Fact]
    public async Task OrgUnits_Update_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var unit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-001", "Direccion General", "Direccion");

        var response = await client.PutAsJsonAsync($"/api/v1/org-units/{unit.Id}", new
        {
            code = "DIR-001",
            name = "Direccion Actualizada",
            unitType = "Direccion",
            sortOrder = 1,
            description = "Actualizada",
            costCenterCode = "CC-01",
            managerEmployeeId = (Guid?)null,
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task OrgUnits_Move_WhenCycleDetected_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var root = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-001", "Direccion General", "Direccion");
        var child = await CreateOrgUnitAsync(client, scenario.TenantId, "GER-001", "Gerencia Finanzas", "Gerencia", root.Id);

        var response = await client.PatchAsJsonAsync($"/api/v1/org-units/{root.Id}/move", new
        {
            newParentId = child.Id,
            sortOrder = (int?)null,
            concurrencyToken = root.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "ORG_UNIT_CYCLE_DETECTED");
    }

    [Fact]
    public async Task OrgUnits_Inactivate_WhenHasActiveChildren_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var root = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-001", "Direccion General", "Direccion");
        _ = await CreateOrgUnitAsync(client, scenario.TenantId, "GER-001", "Gerencia Finanzas", "Gerencia", root.Id);

        var response = await client.PatchAsJsonAsync($"/api/v1/org-units/{root.Id}/inactivate", new
        {
            concurrencyToken = root.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "ORG_UNIT_HAS_ACTIVE_CHILDREN");
    }

    [Fact]
    public async Task OrgUnits_TreeAndGraph_ShouldReturnCreatedHierarchy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var root = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-001", "Direccion General", "Direccion");
        var child = await CreateOrgUnitAsync(client, scenario.TenantId, "GER-001", "Gerencia Finanzas", "Gerencia", root.Id);

        var treeResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units/tree");
        treeResponse.EnsureSuccessStatusCode();

        var tree = await treeResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<OrgUnitTreeNodeItem>>(JsonOptions);
        Assert.NotNull(tree);
        var rootNode = Assert.Single(tree!);
        Assert.Equal(root.Id, rootNode.Id);
        var childNode = Assert.Single(rootNode.Children);
        Assert.Equal(child.Id, childNode.Id);

        var graphResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units/graph");
        graphResponse.EnsureSuccessStatusCode();

        var graph = await graphResponse.Content.ReadFromJsonAsync<OrgUnitGraphItem>(JsonOptions);
        Assert.NotNull(graph);
        Assert.Equal(2, graph!.Nodes.Count);
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(root.Id, edge.FromId);
        Assert.Equal(child.Id, edge.ToId);
    }

    [Fact]
    public async Task OrgUnits_List_WithTenantMismatch_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.OtherTenantId}/org-units?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task OrgUnits_List_WithoutPermission_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "ORG_UNITS_FORBIDDEN");
    }

    [Fact]
    public async Task OrgUnits_Create_ShouldWriteAuditEvent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminWithAuditContext(scenario));

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-001",
            name = "Direccion General",
            unitType = "Direccion",
            parentId = (Guid?)null,
            sortOrder = 1,
            description = "Direccion principal",
            costCenterCode = "CC-01",
            managerEmployeeId = (Guid?)null
        });
        createResponse.EnsureSuccessStatusCode();

        var auditResponse = await client.GetAsync("/api/audit/logs?page=1&pageSize=50");
        auditResponse.EnsureSuccessStatusCode();

        var payload = await auditResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, static item => item.EventType == "ORG_UNIT_CREATED" && item.EntityType == "OrgUnit");
    }

    [Fact]
    public async Task JobProfiles_FullFlow_ShouldCreateUpdatePublishPrintAndExport()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-001",
            title = "Analista de Nomina",
            objective = "Garantizar el proceso de nomina.",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo RRHH",
            responsibilities = "Ejecutar nomina mensual.",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado local",
            valuationNotes = "Valuado 2026",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType = "Experience",
                    catalogItemId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description = "3 anios de experiencia",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Gestionar proceso completo de planilla",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(JobProfileStatus.Draft, created!.Status);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/job-profiles/{created.Id}", new
        {
            code = "JP-001",
            title = "Analista de Nomina Senior",
            objective = "Garantizar el proceso de nomina.",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo RRHH",
            responsibilities = "Ejecutar nomina mensual.",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado local",
            valuationNotes = "Valuado 2026",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType = "Experience",
                    catalogItemId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description = "3 anios de experiencia",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Gestionar proceso completo de planilla",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>(),
            concurrencyToken = created.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Analista de Nomina Senior", updated!.Title);

        var publishResponse = await client.PatchAsJsonAsync($"/api/v1/job-profiles/{updated.Id}/publish", new
        {
            concurrencyToken = updated.ConcurrencyToken
        });
        publishResponse.EnsureSuccessStatusCode();

        var published = await publishResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(published);
        Assert.Equal(JobProfileStatus.Published, published!.Status);

        var printResponse = await client.GetAsync($"/api/v1/job-profiles/{published.Id}/print");
        printResponse.EnsureSuccessStatusCode();

        var printPayload = await printResponse.Content.ReadFromJsonAsync<JobProfilePrintItem>(JsonOptions);
        Assert.NotNull(printPayload);
        Assert.Equal(published.Id, printPayload!.Profile.Id);

        var exportJsonResponse = await client.GetAsync($"/api/v1/job-profiles/{published.Id}/export?format=json");
        exportJsonResponse.EnsureSuccessStatusCode();

        var exportCsvResponse = await client.GetAsync($"/api/v1/job-profiles/{published.Id}/export?format=csv");
        exportCsvResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", exportCsvResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task JobProfiles_Update_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-001", "Analista");

        var response = await client.PutAsJsonAsync($"/api/v1/job-profiles/{profile.Id}", new
        {
            code = "JP-001",
            title = "Analista Actualizado",
            objective = "Objetivo",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType = "Experience",
                    catalogItemId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description = "3 anios",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Funcion",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>(),
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task JobProfiles_Create_WithDuplicateCode_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        _ = await CreateJobProfileAsync(client, scenario.TenantId, "JP-001", "Analista");

        var duplicateResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-001",
            title = "Analista 2",
            objective = "Objetivo",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType = "Experience",
                    catalogItemId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description = "2 anios",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Funcion",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>()
        });

        await AssertProblemDetailsAsync(duplicateResponse, HttpStatusCode.Conflict, "JOB_PROFILE_CODE_CONFLICT");
    }

    [Fact]
    public async Task JobProfiles_Update_WhenDependencyCycleDetected_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profileA = await CreateJobProfileAsync(client, scenario.TenantId, "JP-A", "Perfil A");
        var profileB = await CreateJobProfileAsync(client, scenario.TenantId, "JP-B", "Perfil B");

        var updateAResponse = await client.PutAsJsonAsync($"/api/v1/job-profiles/{profileA.Id}", new
        {
            code = profileA.Code,
            title = profileA.Title,
            objective = "Objetivo A",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades A",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType = "Experience",
                    catalogItemId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description = "2 anios",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Funcion A",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = new[]
            {
                new { dependentJobProfileId = profileB.Id, quantity = 1, notes = "dep" }
            },
            concurrencyToken = profileA.ConcurrencyToken
        });
        updateAResponse.EnsureSuccessStatusCode();
        var updatedA = await updateAResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(updatedA);

        var updateBResponse = await client.PutAsJsonAsync($"/api/v1/job-profiles/{profileB.Id}", new
        {
            code = profileB.Code,
            title = profileB.Title,
            objective = "Objetivo B",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades B",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType = "Experience",
                    catalogItemId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description = "2 anios",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Funcion B",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = new[]
            {
                new { dependentJobProfileId = profileA.Id, quantity = 1, notes = "dep" }
            },
            concurrencyToken = profileB.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(updateBResponse, HttpStatusCode.Conflict, "JOB_PROFILE_DEPENDENCY_CYCLE");
    }

    [Fact]
    public async Task JobProfiles_List_WithTenantMismatch_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.OtherTenantId}/job-profiles?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task JobProfiles_List_WithoutPermission_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "JOB_PROFILES_FORBIDDEN");
    }

    [Fact]
    public async Task JobProfiles_Create_WithInlineCatalogWithoutPermission_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-001",
            title = "Analista",
            objective = "Objetivo",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = true,
            requirements = new[]
            {
                new
                {
                    requirementType = "Education",
                    catalogItemId = (Guid?)null,
                    catalogCode = "EDU-LIC",
                    catalogName = "Licenciatura",
                    description = "Licenciatura",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Funcion",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "JOB_CATALOG_INLINE_CREATE_FORBIDDEN");
    }

    [Fact]
    public async Task JobProfiles_Update_WithInlineCatalogAndPermission_ShouldCreateCatalogItem()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminWithCatalogContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-001", "Analista");

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/job-profiles/{profile.Id}", new
        {
            code = "JP-001",
            title = "Analista",
            objective = "Objetivo",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = true,
            requirements = new[]
            {
                new
                {
                    requirementType = "Education",
                    catalogItemId = (Guid?)null,
                    catalogCode = "EDU-LIC",
                    catalogName = "Licenciatura",
                    description = "Licenciatura",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Funcion",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>(),
            concurrencyToken = profile.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();

        var catalogResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/job-catalogs/EducationLevel?page=1&pageSize=20");
        catalogResponse.EnsureSuccessStatusCode();

        var catalogPayload = await catalogResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<JobCatalogItemItem>>(JsonOptions);
        Assert.NotNull(catalogPayload);
        Assert.Contains(catalogPayload!.Items, static item => item.Code == "EDU-LIC" && item.Name == "Licenciatura");
    }

    [Fact]
    public async Task JobProfiles_Create_WithAuditPermission_ShouldWriteAuditEvent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminWithAuditContext(scenario));

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-001",
            title = "Analista",
            objective = "Objetivo",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType = "Experience",
                    catalogItemId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description = "2 anios",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Funcion",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>()
        });
        response.EnsureSuccessStatusCode();

        var auditResponse = await client.GetAsync("/api/audit/logs?page=1&pageSize=50");
        auditResponse.EnsureSuccessStatusCode();

        var payload = await auditResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, static item => item.EventType == "JOB_PROFILE_CREATED" && item.EntityType == "JobProfile");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAuthentication_ShouldReturn401()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/company/users");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
    }

    [Fact]
    public async Task RbacEndpoint_WithoutPermission_ShouldReturn403RbacDenied()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId));

        var response = await client.GetAsync("/api/rbac/resources");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RBAC_DENIED");
    }

    [Fact]
    public async Task AuditLogs_WithoutPermission_ShouldReturn403RbacDenied()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                scenario.TenantId,
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Read)));

        var response = await client.GetAsync("/api/audit/logs");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RBAC_DENIED");
    }

    [Fact]
    public async Task AuditLogDetail_ForOtherTenant_ShouldReturn403TenantMismatch()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Access),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read)));

        var response = await client.GetAsync($"/api/audit/logs/{scenario.OtherTenantAuditLogId}");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task CompanyUsers_List_ShouldApplyFieldVisibilityRules()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Read)));

        var response = await client.GetAsync("/api/company/users?page=1&pageSize=20");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedCompanyUserResponse>(JsonOptions);
        Assert.NotNull(payload);
        var user = Assert.Single(payload!.Items);
        Assert.Null(user.Email);
        Assert.Equal("Target", user.FirstName);
    }

    [Fact]
    public async Task CompanyUsers_Update_WhenFieldIsNotEditable_ShouldReturn403FieldEditForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Read),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Update)));

        var response = await client.PutAsJsonAsync($"/api/company/users/{scenario.TargetUserId}", new
        {
            firstName = "Blocked",
            lastName = "User",
            roleId = scenario.TargetRoleId
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "FIELD_EDIT_FORBIDDEN");
    }

    [Fact]
    public async Task CompanyUsers_Update_WhenFieldsAreAllowed_ShouldReturnUpdatedUser()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Read),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Update)));

        var response = await client.PutAsJsonAsync($"/api/company/users/{scenario.TargetUserId}", new
        {
            firstName = "Target",
            lastName = "Updated",
            roleId = scenario.TargetRoleId
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CompanyUserItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Updated", payload!.LastName);
        Assert.Equal("Target", payload.FirstName);
    }

    [Fact]
    public async Task CompanyUsers_Deactivate_WithUpdatePermission_ShouldReturnInactiveUser()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Read),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Update)));

        var response = await client.PatchAsync($"/api/company/users/{scenario.TargetUserId}/deactivate", content: null);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CompanyUserItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Inactive", payload!.Status);
    }

    [Fact]
    public async Task CompanyUsers_Deactivate_WithDeleteOnly_ShouldReturn403RbacDenied()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                scenario.TenantId,
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access),
                PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Delete)));

        var response = await client.PatchAsync($"/api/company/users/{scenario.TargetUserId}/deactivate", content: null);

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RBAC_DENIED");
    }

    [Fact]
    public async Task IamUsers_List_WithPermission_ShouldReturnPagedUsers()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Users, RbacPermissionAction.Access),
            (RbacPermissionScreen.Users, RbacPermissionAction.Read)));

        var response = await client.GetAsync("/api/iam/users?pageNumber=1&pageSize=20");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<IamUserSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.PageNumber);
        Assert.Equal(20, payload.PageSize);
        Assert.Equal(2, payload.TotalCount);
        Assert.Contains(payload.Items, static user => user.Email == "security.operator@acme-one.test");
        Assert.Contains(payload.Items, static user => user.Email == "tenant.security.admin@acme-one.test");
    }

    [Fact]
    public async Task IamRoles_List_WithPermission_ShouldReturnTenantRolesOnly()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Access),
            (RbacPermissionScreen.Roles, RbacPermissionAction.Read)));

        var response = await client.GetAsync("/api/iam/roles?pageNumber=1&pageSize=20");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<IamRoleSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(3, payload!.TotalCount);
        Assert.Contains(payload.Items, static role => role.Name == "Security Operator");
        Assert.Contains(payload.Items, static role => role.Name == "Security Admin");
        Assert.Contains(payload.Items, static role => role.Name == "Employee");
        Assert.DoesNotContain(payload.Items, static role => role.Name == "Auditor B");
    }

    [Fact]
    public async Task IamRoles_GetById_ForOtherTenantRole_ShouldReturn403TenantMismatch()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Access),
            (RbacPermissionScreen.Roles, RbacPermissionAction.Read)));

        var response = await client.GetAsync($"/api/iam/roles/{scenario.OtherTenantRoleId}");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task IamPermissions_GetById_WithPermission_ShouldReturnPermissionContract()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Access),
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Read)));

        var response = await client.GetAsync($"/api/iam/permissions/{scenario.ActorPermissionId}");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IamPermissionItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access), payload!.Code);
        Assert.Equal("RBAC", payload.Module);
        Assert.Equal("Users", payload.Screen);
    }

    [Fact]
    public async Task RbacResources_WithPermission_ShouldReturnKnownResources()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Access),
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Read)));

        var response = await client.GetAsync("/api/rbac/resources");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RbacResourcesPayload>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, static item => item.ResourceKey == "RBAC_USERS");
        Assert.Contains(payload.Items, static item => item.ResourceKey == "RBAC_ROLES");
        Assert.Contains(payload.Items, static item => item.ResourceKey == "RBAC_PERMISSIONS");
    }

    [Fact]
    public async Task RbacRolePermissions_WithPermission_ShouldReturnGrantedActions()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Access),
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Read)));

        var response = await client.GetAsync($"/api/rbac/roles/{scenario.ActorRoleId}/permissions");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RbacRolePermissionsPayload>(JsonOptions);
        Assert.NotNull(payload);
        var usersPermission = Assert.Single(payload!.Permissions, static permission => permission.ResourceKey == "RBAC_USERS");
        Assert.True(usersPermission.HasAccess);
        Assert.True(usersPermission.CanRead);
        Assert.True(usersPermission.CanUpdate);
        Assert.False(usersPermission.CanCreate);
    }

    [Fact]
    public async Task RbacRoleFieldPermissions_WithPermission_ShouldReturnConfiguredOverrides()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Access),
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Read)));

        var response = await client.GetAsync($"/api/rbac/roles/{scenario.ActorRoleId}/field-permissions?resourceKey=RBAC_USERS");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RoleFieldPermissionsPayload>(JsonOptions);
        Assert.NotNull(payload);

        var email = Assert.Single(payload!.Fields, static field => field.FieldKey == "RBAC_USERS.EMAIL");
        Assert.False(email.IsVisible);
        Assert.False(email.IsEditable);

        var firstName = Assert.Single(payload.Fields, static field => field.FieldKey == "RBAC_USERS.FIRST_NAME");
        Assert.True(firstName.IsVisible);
        Assert.False(firstName.IsEditable);
        Assert.True(firstName.IsReadOnly);
    }

    [Fact]
    public async Task RbacAudit_WithPermission_ShouldReturnPagedTenantScopedEntries()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Access),
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Read)));

        var response = await client.GetAsync("/api/rbac/audit?resourceKey=RBAC_USERS&page=2&pageSize=1");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<RbacPermissionAuditItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.PageNumber);
        Assert.Equal(1, payload.PageSize);
        Assert.Equal(2, payload.TotalCount);
        var entry = Assert.Single(payload.Items);
        Assert.Equal("RBAC_USERS", entry.ResourceKey);
        Assert.Equal("Upsert", entry.ChangeType);
    }

    [Fact]
    public async Task RbacAudit_WithoutPermission_ShouldReturn403RbacDenied()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Users, RbacPermissionAction.Access),
            (RbacPermissionScreen.Users, RbacPermissionAction.Read)));

        var response = await client.GetAsync("/api/rbac/audit?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RBAC_DENIED");
    }

    [Fact]
    public async Task AuditLogDetail_WithPermission_ShouldReturnDetailContract()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read)));

        var response = await client.GetAsync($"/api/audit/logs/{scenario.AuditLogId}");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuditLogDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(scenario.AuditLogId, payload!.Id);
        Assert.Equal(scenario.TenantId, payload.CompanyId);
        Assert.Equal("USER_UPDATED", payload.EventType);
        Assert.NotNull(payload.Before);
        Assert.NotNull(payload.After);
        Assert.NotNull(payload.Diff);
    }

    [Fact]
    public async Task IamUsers_Create_WithPermission_ShouldReturnCreatedUser()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Users, RbacPermissionAction.Create)));

        var response = await client.PostAsJsonAsync("/api/iam/users", new
        {
            firstName = "New",
            lastName = "Operator",
            email = "new.operator@acme-one.test",
            isActive = true,
            roleIds = new[] { scenario.TargetRoleId }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<IamUserDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("new.operator@acme-one.test", payload!.Email);
        var role = Assert.Single(payload.Roles);
        Assert.Equal(scenario.TargetRoleId, role.Id);
    }

    [Fact]
    public async Task IamUsers_SyncRoles_WithPermission_ShouldReturnUpdatedRoles()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Users, RbacPermissionAction.Update)));

        var response = await client.PutAsJsonAsync($"/api/iam/users/{scenario.ActorUserId}/roles", new
        {
            roleIds = new[] { scenario.TargetRoleId }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IamUserDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        var role = Assert.Single(payload!.Roles);
        Assert.Equal(scenario.TargetRoleId, role.Id);
        Assert.Equal("Employee", role.Name);
    }

    [Fact]
    public async Task IamRoles_Create_WithPermission_ShouldReturnCreatedRole()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Create)));

        var response = await client.PostAsJsonAsync("/api/iam/roles", new
        {
            name = "Payroll Reviewer",
            description = "Reviews payroll changes",
            permissionIds = new[] { scenario.ActorPermissionId }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<IamRoleDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Payroll Reviewer", payload!.Name);
        var permission = Assert.Single(payload.Permissions);
        Assert.Equal(scenario.ActorPermissionId, permission.Id);
    }

    [Fact]
    public async Task IamRoles_Update_WithPermission_ShouldReturnUpdatedRole()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Update)));

        var response = await client.PutAsJsonAsync($"/api/iam/roles/{scenario.TargetRoleId}", new
        {
            name = "Employee Updated",
            description = "Updated tenant role"
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IamRoleDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Employee Updated", payload!.Name);
        Assert.Equal("Updated tenant role", payload.Description);
    }

    [Fact]
    public async Task IamRoles_Clone_WithPermission_ShouldReturnClonedRole()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Create)));

        var response = await client.PostAsJsonAsync($"/api/iam/roles/{scenario.TargetRoleId}/clone", new
        {
            name = "Employee Clone",
            description = "Cloned role"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<IamRoleDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Employee Clone", payload!.Name);
        Assert.Equal("Cloned role", payload.Description);
    }

    [Fact]
    public async Task IamRoles_SyncPermissions_WithPermission_ShouldReturnUpdatedPermissions()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            userId: scenario.SecurityAdminUserId,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Update),
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Update)));

        var response = await client.PutAsJsonAsync($"/api/iam/roles/{scenario.TargetRoleId}/permissions", new
        {
            permissionIds = new[] { scenario.ActorPermissionId }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IamRoleDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        var permission = Assert.Single(payload!.Permissions);
        Assert.Equal(scenario.ActorPermissionId, permission.Id);
    }

    [Fact]
    public async Task IamRoles_SyncUsers_WithPermission_ShouldReturnUpdatedUserCount()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Update)));

        var response = await client.PutAsJsonAsync($"/api/iam/roles/{scenario.TargetRoleId}/users", new
        {
            userIds = new[] { scenario.ActorUserId }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IamRoleDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.UserCount);
    }

    [Fact]
    public async Task IamPermissions_Create_WithPermission_ShouldReturnCreatedPermission()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Create)));

        var response = await client.PostAsJsonAsync("/api/iam/permissions", new
        {
            name = "Export Reports",
            description = "Allows exporting reports",
            code = "CUSTOM.REPORTS.EXPORT",
            module = "Reports",
            screen = "Reports",
            kind = IamPermissionKind.ScreenAction,
            action = "Export",
            fieldName = (string?)null,
            fieldAccess = (IamFieldAccessLevel?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<IamPermissionItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("CUSTOM.REPORTS.EXPORT", payload!.Code);
        Assert.Equal("Reports", payload.Module);
        Assert.Equal("Reports", payload.Screen);
        Assert.Equal("ScreenAction", payload.Kind);
    }

    [Fact]
    public async Task RbacRolePermissions_Update_WithPermission_ShouldReturnUpdatedMatrix()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            userId: scenario.SecurityAdminUserId,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Update)));

        var response = await client.PutAsJsonAsync($"/api/rbac/roles/{scenario.TargetRoleId}/permissions", new
        {
            permissions = new[]
            {
                new
                {
                    resourceKey = "RBAC_USERS",
                    hasAccess = true,
                    canRead = true,
                    canCreate = false,
                    canUpdate = true,
                    canDelete = false
                }
            }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RbacRolePermissionsPayload>(JsonOptions);
        Assert.NotNull(payload);
        var usersPermission = Assert.Single(payload!.Permissions, static permission => permission.ResourceKey == "RBAC_USERS");
        Assert.True(usersPermission.HasAccess);
        Assert.True(usersPermission.CanRead);
        Assert.True(usersPermission.CanUpdate);
        Assert.False(usersPermission.CanCreate);
    }

    [Fact]
    public async Task RbacRoleFieldPermissions_Update_WithPermission_ShouldNormalizeOverrides()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            userId: scenario.SecurityAdminUserId,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Update)));

        var grantResourceResponse = await client.PutAsJsonAsync($"/api/rbac/roles/{scenario.TargetRoleId}/permissions", new
        {
            permissions = new[]
            {
                new
                {
                    resourceKey = "RBAC_USERS",
                    hasAccess = true,
                    canRead = true,
                    canCreate = false,
                    canUpdate = true,
                    canDelete = false
                }
            }
        });

        grantResourceResponse.EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync($"/api/rbac/roles/{scenario.TargetRoleId}/field-permissions", new
        {
            resourceKey = "RBAC_USERS",
            fields = new[]
            {
                new
                {
                    fieldKey = "RBAC_USERS.EMAIL",
                    isVisible = false,
                    isEditable = true,
                    isRequired = false,
                    isMasked = true
                }
            }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RoleFieldPermissionsPayload>(JsonOptions);
        Assert.NotNull(payload);
        var email = Assert.Single(payload!.Fields, static field => field.FieldKey == "RBAC_USERS.EMAIL");
        Assert.False(email.IsVisible);
        Assert.False(email.IsEditable);
        Assert.False(email.IsReadOnly);
    }

    private static TestUserContext CreateUserContext(
        IntegrationTestScenario scenario,
        params (RbacPermissionScreen Screen, RbacPermissionAction Action)[] grants) =>
        CreateUserContext(scenario, userId: null, grants);

    private static TestUserContext CreateUserContext(
        IntegrationTestScenario scenario,
        Guid? userId,
        params (RbacPermissionScreen Screen, RbacPermissionAction Action)[] grants) =>
        TestUserContext.Authenticated(
            userId ?? scenario.ActorUserId,
            scenario.TenantId,
            grants
                .SelectMany(static grant => grant.Action == RbacPermissionAction.Access
                    ? new[]
                    {
                        PermissionMatrixCatalog.BuildPermissionCode(grant.Screen, RbacPermissionAction.Access)
                    }
                    : new[]
                    {
                        PermissionMatrixCatalog.BuildPermissionCode(grant.Screen, RbacPermissionAction.Access),
                        PermissionMatrixCatalog.BuildPermissionCode(grant.Screen, grant.Action)
                    })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());

    private static TestUserContext CreateLocationReadContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, LocationPermissionCodes.Read);

    private static TestUserContext CreateLocationAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, LocationPermissionCodes.Admin);

    private static TestUserContext CreateOrgUnitReadContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, OrgUnitPermissionCodes.Read);

    private static TestUserContext CreateOrgUnitAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, OrgUnitPermissionCodes.Admin);

    private static TestUserContext CreateOrgUnitAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            OrgUnitPermissionCodes.Admin,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read));

    private static TestUserContext CreateJobProfileReadContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, JobProfilePermissionCodes.Read);

    private static TestUserContext CreateJobProfileAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, JobProfilePermissionCodes.Admin);

    private static TestUserContext CreateJobProfileAdminWithCatalogContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            JobProfilePermissionCodes.Admin,
            JobProfilePermissionCodes.CatalogAdmin);

    private static TestUserContext CreateJobProfileAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            JobProfilePermissionCodes.Admin,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read));

    private async Task<LocationHierarchyItem> GetLocationHierarchyAsync(HttpClient client, Guid companyId)
    {
        var response = await client.GetAsync($"/api/v1/companies/{companyId}/location-hierarchy");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LocationHierarchyItem>(JsonOptions))!;
    }

    private async Task<LocationGroupItem> GetDefaultLocationGroupAsync(HttpClient client, Guid companyId)
    {
        var response = await client.GetAsync($"/api/v1/companies/{companyId}/location-groups?page=1&pageSize=20");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<LocationGroupItem>>(JsonOptions);
        Assert.NotNull(payload);
        return Assert.Single(payload!.Items, static group => group.IsDefault);
    }

    private async Task<OrgUnitItem> CreateOrgUnitAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string name,
        string unitType,
        Guid? parentId = null,
        int? sortOrder = 1)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/org-units", new
        {
            code,
            name,
            unitType,
            parentId,
            sortOrder,
            description = (string?)null,
            costCenterCode = (string?)null,
            managerEmployeeId = (Guid?)null
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OrgUnitItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<JobProfileItem> CreateJobProfileAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string title)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/job-profiles", new
        {
            code,
            title,
            objective = "Objetivo",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType = "Experience",
                    catalogItemId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description = "2 anios",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Funcion",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensations = Array.Empty<object>(),
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>()
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response, HttpStatusCode expectedStatusCode, string expectedCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expectedStatusCode, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private sealed record PagedCompanyUserResponse(IReadOnlyCollection<CompanyUserItem> Items);

    private sealed record PagedResponseEnvelope<TItem>(
        IReadOnlyCollection<TItem> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);

    private sealed record CompanyUserItem(
        Guid Id,
        string? Email,
        string? FirstName,
        string? LastName,
        Guid? RoleId,
        string? Role,
        string? Status);

    private sealed record AccountCompanyItem(
        Guid CompanyId,
        string Name,
        string Slug,
        string Status,
        string PlanCode,
        bool IsActiveContext,
        bool IsOwnedByCurrentUser,
        DateTime CreatedAtUtc);

    private sealed record AccountCompanyDetailItem(
        Guid CompanyId,
        string Name,
        string Slug,
        string Status,
        string PlanCode,
        bool IsActiveContext,
        bool IsOwnedByCurrentUser,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record ActiveCompanyItem(
        Guid CompanyId,
        string Name,
        string Slug,
        string Status);

    private sealed record SwitchActiveCompanyItem(
        string AccessToken,
        string? RefreshToken,
        int ExpiresIn,
        ActiveCompanyItem ActiveCompany);

    private sealed record LocationHierarchyItem(
        Guid Id,
        bool IsMultiLevel,
        string DefaultGroupCode,
        string DefaultGroupName,
        Guid ConcurrencyToken);

    private sealed record LocationLevelItem(
        Guid Id,
        int LevelOrder,
        string DisplayName,
        bool IsActive,
        bool IsRequired,
        bool AllowsWorkCenters,
        Guid ConcurrencyToken);

    private sealed record LocationGroupItem(
        Guid Id,
        int LevelOrder,
        string Code,
        string Name,
        Guid? ParentId,
        string? Description,
        bool IsActive,
        bool IsDefault,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record WorkCenterTypeItem(
        Guid Id,
        string Code,
        string Name,
        bool RequiresAddress,
        bool RequiresGeo,
        bool AllowsBiometric,
        bool IsActive,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record WorkCenterItem(
        Guid Id,
        string Code,
        string Name,
        Guid WorkCenterTypeId,
        string WorkCenterTypeCode,
        string WorkCenterTypeName,
        Guid LocationGroupId,
        string LocationGroupCode,
        string LocationGroupName,
        int LocationGroupLevelOrder,
        string? Address,
        decimal? GeoLat,
        decimal? GeoLong,
        string? Phone,
        string? Email,
        string? Notes,
        bool IsActive,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record OrgUnitItem(
        Guid Id,
        string Code,
        string Name,
        OrgUnitType UnitType,
        Guid? ParentId,
        int? SortOrder,
        string? Description,
        string? CostCenterCode,
        Guid? ManagerEmployeeId,
        bool IsActive,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record OrgUnitTreeNodeItem(
        Guid Id,
        string Code,
        string Name,
        OrgUnitType UnitType,
        Guid? ParentId,
        int? SortOrder,
        bool IsActive,
        IReadOnlyCollection<OrgUnitTreeNodeItem> Children);

    private sealed record OrgUnitGraphNodeItem(
        Guid Id,
        string Label,
        OrgUnitType Type,
        bool IsActive);

    private sealed record OrgUnitGraphEdgeItem(Guid FromId, Guid ToId);

    private sealed record OrgUnitGraphItem(
        IReadOnlyCollection<OrgUnitGraphNodeItem> Nodes,
        IReadOnlyCollection<OrgUnitGraphEdgeItem> Edges);

    private sealed record JobProfileItem(
        Guid Id,
        string Code,
        string Title,
        JobProfileStatus Status,
        Guid ConcurrencyToken);

    private sealed record JobProfilePrintItem(
        JobProfileItem Profile,
        DateTime GeneratedAtUtc);

    private sealed record JobCatalogItemItem(
        Guid Id,
        JobCatalogCategory Category,
        string Code,
        string Name,
        bool IsActive);

    private sealed record IamUserSummaryItem(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        bool IsActive,
        int RoleCount);

    private sealed record IamRoleReferenceItem(Guid Id, string Name, string? Description);

    private sealed record IamPermissionReferenceItem(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        string Module,
        string Screen,
        string Kind,
        string? Action,
        string? FieldName,
        string? FieldAccess);

    private sealed record IamUserDetailItem(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        bool IsActive,
        IReadOnlyCollection<IamRoleReferenceItem> Roles);

    private sealed record IamRoleSummaryItem(
        Guid Id,
        string Name,
        string? Description,
        bool IsSystemRole,
        int PermissionCount,
        int UserCount);

    private sealed record IamRoleDetailItem(
        Guid Id,
        string Name,
        string? Description,
        bool IsSystemRole,
        int UserCount,
        IReadOnlyCollection<IamPermissionReferenceItem> Permissions);

    private sealed record IamPermissionItem(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        string Module,
        string Screen,
        string Kind,
        string? Action,
        string? FieldName,
        string? FieldAccess);

    private sealed record RbacResourceItem(string ResourceKey, string DisplayName);

    private sealed record RbacResourcesPayload(IReadOnlyCollection<RbacResourceItem> Items);

    private sealed record RbacRolePermissionItem(
        string ResourceKey,
        string DisplayName,
        bool HasAccess,
        bool CanRead,
        bool CanCreate,
        bool CanUpdate,
        bool CanDelete);

    private sealed record RbacRolePermissionsPayload(
        Guid RoleId,
        string RoleName,
        bool IsSystemRole,
        IReadOnlyCollection<RbacRolePermissionItem> Permissions);

    private sealed record RoleFieldPermissionItem(
        string FieldKey,
        string PropertyName,
        string DisplayName,
        string DataType,
        bool IsSensitive,
        bool IsVisible,
        bool IsEditable,
        bool IsRequired,
        bool IsMasked,
        bool IsReadOnly);

    private sealed record RoleFieldPermissionsPayload(
        Guid RoleId,
        string RoleName,
        string ResourceKey,
        IReadOnlyCollection<RoleFieldPermissionItem> Fields);

    private sealed record RbacPermissionAuditStateItem(
        bool HasAccess,
        bool CanRead,
        bool CanCreate,
        bool CanUpdate,
        bool CanDelete);

    private sealed record RbacPermissionAuditItem(
        long Id,
        Guid CompanyId,
        Guid RoleId,
        string ResourceKey,
        Guid ChangedByUserId,
        string ChangeType,
        RbacPermissionAuditStateItem Before,
        RbacPermissionAuditStateItem After,
        DateTime ChangedAtUtc);

    private sealed record AuditLogSummaryItem(
        Guid Id,
        DateTime CreatedAtUtc,
        Guid ActorUserId,
        string? ActorEmail,
        string EventType,
        string EntityType,
        Guid? EntityId,
        string? EntityKey,
        string Action,
        string Summary,
        JsonElement? Diff);

    private sealed record AuditLogDetailItem(
        Guid Id,
        Guid CompanyId,
        DateTime CreatedAtUtc,
        Guid ActorUserId,
        string? ActorEmail,
        string EventType,
        string EntityType,
        Guid? EntityId,
        string? EntityKey,
        string Action,
        string Summary,
        JsonElement? Before,
        JsonElement? After,
        JsonElement? Diff,
        string? IpAddress,
        string? UserAgent);
}
