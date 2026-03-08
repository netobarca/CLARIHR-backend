using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Application.Features.SalaryTabulator.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.SalaryTabulator;
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
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload(),
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
    public async Task Register_WithoutInitialLegalRepresentative_ShouldReturnBadRequest()
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

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var email = $"login.user.{Guid.NewGuid():N}@clarihr.test";
        const string password = "StrongPass123!";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Login",
            lastName = "User",
            email,
            password,
            companyName = "Login Company",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload(),
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var payload = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.Equal(email, payload.User.Email);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var email = $"login.user.invalid.{Guid.NewGuid():N}@clarihr.test";
        const string password = "StrongPass123!";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Login",
            lastName = "Invalid",
            email,
            password,
            companyName = "Login Invalid Company",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload(),
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword123!"
        });

        await AssertProblemDetailsAsync(loginResponse, HttpStatusCode.Unauthorized, "auth.login.invalid_credentials");
    }

    [Fact]
    public async Task Logout_ShouldRevokeRefreshTokensForAuthenticatedUser()
    {
        await factory.ResetDatabaseAsync();
        using var anonymousClient = factory.CreateClient();

        var email = $"logout.user.{Guid.NewGuid():N}@clarihr.test";
        const string password = "StrongPass123!";

        var registerResponse = await anonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Logout",
            lastName = "User",
            email,
            password,
            companyName = "Logout Company",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload(),
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(registerPayload);
        Assert.False(string.IsNullOrWhiteSpace(registerPayload!.RefreshToken));

        var tenantId = ExtractTenantIdFromJwt(registerPayload.AccessToken);
        using var authenticatedClient = factory.CreateClientFor(
            TestUserContext.Authenticated(registerPayload.User.Id, tenantId));

        var logoutResponse = await authenticatedClient.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await anonymousClient.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = registerPayload.RefreshToken
        });

        await AssertProblemDetailsAsync(refreshResponse, HttpStatusCode.Unauthorized, "auth.refresh.invalid_token");
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
    public async Task AccountCompanies_GetById_ShouldReturnActiveLegalRepresentatives()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/account/companies/{scenario.TenantId}");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        var representative = Assert.Single(payload!.ActiveLegalRepresentatives);
        Assert.True(representative.IsPrimary);
        Assert.Equal("PrimaryLegalRepresentative", representative.RepresentationType);
        Assert.False(string.IsNullOrWhiteSpace(representative.FullName));
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
            name = "Acme Three",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
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
    public async Task AccountCompanies_Create_WithoutInitialLegalRepresentative_ShouldReturnBadRequest()
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

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            name = "Acme Three",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
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
            name = "Acme Three",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
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
    public async Task LegalRepresentatives_CreateAndList_ShouldReturnCreatedRepresentative()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                "LegalRepresentatives.Admin"));

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/legal-representatives", new
        {
            firstName = "Carla",
            lastName = "Lopez",
            documentType = "Passport",
            documentNumber = "P-12345678",
            positionTitle = "Apoderada Legal",
            representationType = "AttorneyInFact",
            authorityDescription = "Representacion especial",
            appointmentInstrument = "Poder especial",
            appointmentDateUtc = DateTime.UtcNow.Date,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            email = "carla.lopez@test.com",
            phone = "+50371111111",
            isPrimary = false
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<LegalRepresentativeItem>(JsonOptions);
        Assert.NotNull(created);

        var listResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/legal-representatives?page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<LegalRepresentativeListItem>>(JsonOptions);
        Assert.NotNull(listPayload);
        Assert.Contains(listPayload!.Items, item => item.Id == created!.Id);
    }

    [Fact]
    public async Task LegalRepresentatives_InactivateLastActive_ShouldReturnConflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                "LegalRepresentatives.Admin"));

        var listResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/legal-representatives?isActive=true&page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<LegalRepresentativeListItem>>(JsonOptions);
        Assert.NotNull(listPayload);
        var target = Assert.Single(listPayload!.Items);

        var inactivateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/legal-representatives/{target.Id}/inactivate",
            new { concurrencyToken = target.ConcurrencyToken });

        await AssertProblemDetailsAsync(
            inactivateResponse,
            HttpStatusCode.Conflict,
            "LEGAL_REPRESENTATIVE_ACTIVE_MIN_REQUIRED");
    }

    [Fact]
    public async Task LegalRepresentatives_GetById_WhenTenantMismatch_ShouldReturnForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var sourceClient = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                "LegalRepresentatives.Admin"));

        var listResponse = await sourceClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/legal-representatives?page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<LegalRepresentativeListItem>>(JsonOptions);
        Assert.NotNull(listPayload);
        var representative = Assert.Single(listPayload!.Items);

        using var otherTenantClient = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.OtherTenantId,
                "LegalRepresentatives.Admin"));

        var response = await otherTenantClient.GetAsync($"/api/v1/legal-representatives/{representative.Id}");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
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
            costCenterCode = (string?)null,
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
    public async Task OrgUnits_TreeGraphAndExports_ShouldReturnCreatedHierarchyAndFiles()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminWithAuditContext(scenario));

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

        var csvResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units/export?format=csv");
        csvResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);

        var xlsxResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units/export?format=xlsx");
        xlsxResponse.EnsureSuccessStatusCode();
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsxResponse.Content.Headers.ContentType?.MediaType);

        var graphMlResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units/diagram-export?format=graphml");
        graphMlResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/graphml+xml", graphMlResponse.Content.Headers.ContentType?.MediaType);

        var jsonResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units/diagram-export?format=json");
        jsonResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/json", jsonResponse.Content.Headers.ContentType?.MediaType);

        var dotResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units/diagram-export?format=dot");
        dotResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/vnd.graphviz", dotResponse.Content.Headers.ContentType?.MediaType);

        var invalidFormatResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units/diagram-export?format=svg");
        await AssertProblemDetailsAsync(invalidFormatResponse, HttpStatusCode.BadRequest, "REPORT_FORMAT_NOT_SUPPORTED");

        var auditResponse = await client.GetAsync("/api/audit/logs?page=1&pageSize=100");
        auditResponse.EnsureSuccessStatusCode();
        var auditPayload = await auditResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(auditPayload);
        Assert.Contains(auditPayload!.Items, item => item.EventType == "REPORT_EXPORTED" && item.EntityType == "OrgUnit");
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
    public async Task Policy_IncludeAllowedActions_ShouldReturnActionsInCoreLists()
    {
        var scenario = await factory.ResetDatabaseAsync();

        using var orgUnitClient = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));
        var orgUnit = await CreateOrgUnitAsync(orgUnitClient, scenario.TenantId, "DIR-AA", "Direccion AllowedActions", "Direccion");

        using var jobProfileClient = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        var jobProfile = await CreateJobProfileAsync(jobProfileClient, scenario.TenantId, "JP-AA", "Perfil AllowedActions");

        using var positionSlotClient = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));
        _ = await CreatePositionSlotAsync(
            positionSlotClient,
            scenario.TenantId,
            code: "PS-AA",
            title: "Plaza AllowedActions",
            jobProfileId: jobProfile.Id,
            orgUnitId: orgUnit.Id,
            maxEmployees: 1);

        using var costCenterClient = factory.CreateClientFor(CreateCostCenterAdminContext(scenario));
        _ = await CreateCostCenterAsync(costCenterClient, scenario.TenantId, "CC-AA", "Centro AllowedActions", "Mixed");

        using var legalRepresentativeClient = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                "LegalRepresentatives.Read"));

        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        var createdRequest = await CreateSalaryTabulatorRequestAsync(requesterClient, scenario.TenantId, "CLS-AA", "S1", 1300m);
        var submitResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/submit", new
        {
            concurrencyToken = createdRequest.ConcurrencyToken
        });
        submitResponse.EnsureSuccessStatusCode();
        var submittedRequest = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submittedRequest);

        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));
        var approveResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/approve", new
        {
            decisionComment = "ok",
            concurrencyToken = submittedRequest!.ConcurrencyToken
        });
        approveResponse.EnsureSuccessStatusCode();

        var orgUnitsList = await orgUnitClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/org-units?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(orgUnitsList);

        var profilesList = await jobProfileClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/job-profiles?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(profilesList);

        var slotsList = await positionSlotClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-slots?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(slotsList);

        var costCentersList = await costCenterClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/cost-centers?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(costCentersList);

        var legalRepresentativesList = await legalRepresentativeClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/legal-representatives?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(legalRepresentativesList);

        var salaryLinesList = await approverClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/salary-tabulator?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(salaryLinesList);

        var salaryRequestsList = await approverClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/salary-tabulator/change-requests?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(salaryRequestsList);
    }

    [Fact]
    public async Task Reports_Capabilities_ShouldReturnCapabilitiesByResource()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitReadContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/reports/capabilities?resource={OrgUnitPermissionCodes.ResourceKey}");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ReportCapabilitiesItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(OrgUnitPermissionCodes.ResourceKey, payload!.ResourceKey);
        Assert.True(payload.SupportsExport);
        Assert.False(payload.SupportsPrint);
        Assert.Contains("csv", payload.SupportedTableFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("xlsx", payload.SupportedTableFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("graphml", payload.SupportedGraphFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("json", payload.SupportedGraphFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("dot", payload.SupportedGraphFormats, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reports_Capabilities_WithUnsupportedResource_ShouldReturn404()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitReadContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/reports/capabilities?resource=NOT_SUPPORTED");

        await AssertProblemDetailsAsync(response, HttpStatusCode.NotFound, "REPORT_NOT_AVAILABLE");
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
            costCenterCode = (string?)null,
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
    public async Task JobProfiles_PrintAndExport_ShouldWriteReportAuditEvents()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminWithAuditContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-REP", "Perfil Reporte");

        var publishResponse = await client.PatchAsJsonAsync($"/api/v1/job-profiles/{profile.Id}/publish", new
        {
            concurrencyToken = profile.ConcurrencyToken
        });
        publishResponse.EnsureSuccessStatusCode();
        var published = await publishResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(published);

        var printResponse = await client.GetAsync($"/api/v1/job-profiles/{published!.Id}/print");
        printResponse.EnsureSuccessStatusCode();

        var exportResponse = await client.GetAsync($"/api/v1/job-profiles/{published.Id}/export?format=csv");
        exportResponse.EnsureSuccessStatusCode();

        var auditResponse = await client.GetAsync("/api/audit/logs?page=1&pageSize=100");
        auditResponse.EnsureSuccessStatusCode();

        var payload = await auditResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, static item => item.EventType == "REPORT_PRINTED" && item.EntityType == "JobProfile");
        Assert.Contains(payload.Items, static item => item.EventType == "REPORT_EXPORTED" && item.EntityType == "JobProfile");
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
    public async Task PositionSlots_FullFlow_ShouldCreateUpdateDependenciesOccupancyStatusGraphAndExports()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-PS", "Direccion Plazas", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-PS", "Analista de Plazas");

        var primary = await CreatePositionSlotAsync(
            client,
            scenario.TenantId,
            code: "PS-001",
            title: "Plaza Principal",
            jobProfileId: profile.Id,
            orgUnitId: orgUnit.Id,
            maxEmployees: 2);

        var dependency = await CreatePositionSlotAsync(
            client,
            scenario.TenantId,
            code: "PS-002",
            title: "Plaza Dependencia",
            jobProfileId: profile.Id,
            orgUnitId: orgUnit.Id,
            maxEmployees: 1);

        var listResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/position-slots?page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<PositionSlotListItem>>(JsonOptions);
        Assert.NotNull(listPayload);
        Assert.Contains(listPayload!.Items, item => item.Id == primary.Id);
        Assert.Contains(listPayload.Items, item => item.Id == dependency.Id);

        var getResponse = await client.GetAsync($"/api/v1/position-slots/{primary.Id}");
        getResponse.EnsureSuccessStatusCode();
        var getPayload = await getResponse.Content.ReadFromJsonAsync<PositionSlotItem>(JsonOptions);
        Assert.NotNull(getPayload);
        Assert.Equal(primary.Id, getPayload!.Id);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/position-slots/{primary.Id}", new
        {
            code = "PS-001",
            title = "Plaza Principal Actualizada",
            jobProfileId = profile.Id,
            orgUnitId = orgUnit.Id,
            workCenterId = (Guid?)null,
            costCenterCode = (string?)null,
            maxEmployees = 3,
            isFixedTerm = false,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            notes = "Actualizada",
            concurrencyToken = primary.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<PositionSlotItem>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Plaza Principal Actualizada", updated!.Title);

        var dependenciesResponse = await client.PatchAsJsonAsync($"/api/v1/position-slots/{primary.Id}/dependencies", new
        {
            directDependencyPositionSlotId = dependency.Id,
            functionalDependencyPositionSlotId = (Guid?)null,
            concurrencyToken = updated.ConcurrencyToken
        });
        dependenciesResponse.EnsureSuccessStatusCode();
        var withDependencies = await dependenciesResponse.Content.ReadFromJsonAsync<PositionSlotItem>(JsonOptions);
        Assert.NotNull(withDependencies);
        Assert.Equal(dependency.Id, withDependencies!.DirectDependencyPositionSlotId);

        var occupancyResponse = await client.PatchAsJsonAsync($"/api/v1/position-slots/{primary.Id}/occupancy", new
        {
            occupiedEmployees = 1,
            concurrencyToken = withDependencies.ConcurrencyToken
        });
        occupancyResponse.EnsureSuccessStatusCode();
        var withOccupancy = await occupancyResponse.Content.ReadFromJsonAsync<PositionSlotItem>(JsonOptions);
        Assert.NotNull(withOccupancy);
        Assert.Equal(PositionSlotStatus.Occupied, withOccupancy!.Status);
        Assert.Equal(1, withOccupancy.OccupiedEmployees);

        var statusResponse = await client.PatchAsJsonAsync($"/api/v1/position-slots/{primary.Id}/status", new
        {
            status = "Suspended",
            concurrencyToken = withOccupancy.ConcurrencyToken
        });
        statusResponse.EnsureSuccessStatusCode();
        var suspended = await statusResponse.Content.ReadFromJsonAsync<PositionSlotItem>(JsonOptions);
        Assert.NotNull(suspended);
        Assert.Equal(PositionSlotStatus.Suspended, suspended!.Status);
        Assert.False(suspended.IsActive);

        var graphResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/position-slots/graph?depth=5&includeFunctional=true");
        graphResponse.EnsureSuccessStatusCode();
        var graphPayload = await graphResponse.Content.ReadFromJsonAsync<PositionSlotGraphItem>(JsonOptions);
        Assert.NotNull(graphPayload);
        Assert.Contains(graphPayload!.Nodes, node => node.Id == primary.Id);
        Assert.Contains(graphPayload.Nodes, node => node.Id == dependency.Id);
        Assert.Contains(
            graphPayload.Edges,
            edge => edge.FromId == dependency.Id &&
                    edge.ToId == primary.Id &&
                    edge.RelationType == PositionSlotDependencyRelationType.Direct);

        var graphMlResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-slots/diagram-export?format=graphml&depth=5");
        graphMlResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/graphml+xml", graphMlResponse.Content.Headers.ContentType?.MediaType);

        var dotResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-slots/diagram-export?format=dot&depth=5");
        dotResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/vnd.graphviz", dotResponse.Content.Headers.ContentType?.MediaType);

        var csvResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-slots/export?format=csv&status=Suspended");
        csvResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);

        var xlsxResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-slots/export?format=xlsx&status=Suspended");
        xlsxResponse.EnsureSuccessStatusCode();
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsxResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PositionSlots_Update_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-STALE", "Direccion", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-STALE", "Perfil");
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-STALE", "Plaza", profile.Id, orgUnit.Id, 1);

        var response = await client.PutAsJsonAsync($"/api/v1/position-slots/{slot.Id}", new
        {
            code = slot.Code,
            title = "Plaza actualizada",
            jobProfileId = profile.Id,
            orgUnitId = orgUnit.Id,
            workCenterId = (Guid?)null,
            costCenterCode = (string?)null,
            maxEmployees = 1,
            isFixedTerm = false,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            notes = "Notas",
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task PositionSlots_Create_WithDuplicateCode_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-DUP", "Direccion", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-DUP", "Perfil");

        _ = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-DUP", "Plaza 1", profile.Id, orgUnit.Id, 1);

        var duplicateResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-slots", new
        {
            code = "PS-DUP",
            title = "Plaza 2",
            jobProfileId = profile.Id,
            orgUnitId = orgUnit.Id,
            workCenterId = (Guid?)null,
            costCenterCode = (string?)null,
            directDependencyPositionSlotId = (Guid?)null,
            functionalDependencyPositionSlotId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            occupiedEmployees = 0,
            isFixedTerm = false,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            notes = (string?)null
        });

        await AssertProblemDetailsAsync(duplicateResponse, HttpStatusCode.Conflict, "POSITION_SLOT_CODE_CONFLICT");
    }

    [Fact]
    public async Task PositionSlots_Dependencies_WhenCycleDetected_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-CYCLE", "Direccion", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-CYCLE", "Perfil");

        var parent = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-A", "A", profile.Id, orgUnit.Id, 1);
        var child = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-B", "B", profile.Id, orgUnit.Id, 1);

        var parentUpdateResponse = await client.PatchAsJsonAsync($"/api/v1/position-slots/{parent.Id}/dependencies", new
        {
            directDependencyPositionSlotId = child.Id,
            functionalDependencyPositionSlotId = (Guid?)null,
            concurrencyToken = parent.ConcurrencyToken
        });
        parentUpdateResponse.EnsureSuccessStatusCode();

        var cycleResponse = await client.PatchAsJsonAsync($"/api/v1/position-slots/{child.Id}/dependencies", new
        {
            directDependencyPositionSlotId = parent.Id,
            functionalDependencyPositionSlotId = (Guid?)null,
            concurrencyToken = child.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(cycleResponse, HttpStatusCode.Conflict, "POSITION_SLOT_DEPENDENCY_CYCLE");
    }

    [Fact]
    public async Task PositionSlots_UpdateOccupancy_WhenOverCapacity_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-CAP", "Direccion", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-CAP", "Perfil");
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-CAP", "Capacidad", profile.Id, orgUnit.Id, 1);

        var response = await client.PatchAsJsonAsync($"/api/v1/position-slots/{slot.Id}/occupancy", new
        {
            occupiedEmployees = 2,
            concurrencyToken = slot.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "POSITION_SLOT_CAPACITY_RULE_VIOLATION");
    }

    [Fact]
    public async Task PositionSlots_List_WithTenantMismatch_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.OtherTenantId}/position-slots?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task PositionSlots_List_WithoutPermission_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/position-slots?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "POSITION_SLOTS_FORBIDDEN");
    }

    [Fact]
    public async Task PositionSlots_Create_WithAuditPermission_ShouldWriteAuditEvent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminWithAuditContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-AUD", "Direccion", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-AUD", "Perfil");

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-slots", new
        {
            code = "PS-AUD",
            title = "Plaza Audit",
            jobProfileId = profile.Id,
            orgUnitId = orgUnit.Id,
            workCenterId = (Guid?)null,
            costCenterCode = (string?)null,
            directDependencyPositionSlotId = (Guid?)null,
            functionalDependencyPositionSlotId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            occupiedEmployees = 0,
            isFixedTerm = false,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            notes = "audit"
        });
        response.EnsureSuccessStatusCode();

        var auditResponse = await client.GetAsync("/api/audit/logs?page=1&pageSize=50");
        auditResponse.EnsureSuccessStatusCode();

        var payload = await auditResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, static item => item.EventType == "POSITION_SLOT_CREATED" && item.EntityType == "PositionSlot");
    }

    [Fact]
    public async Task CostCenters_FullFlow_ShouldCreateListGetUpdateUsageActivateInactivateAndExport()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCostCenterAdminContext(scenario));

        var created = await CreateCostCenterAsync(client, scenario.TenantId, "CC-001", "Centro Principal", "Mixed");

        var listResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/cost-centers?page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<CostCenterListItem>>(JsonOptions);
        Assert.NotNull(listPayload);
        Assert.Contains(listPayload!.Items, item => item.Id == created.Id);

        var getResponse = await client.GetAsync($"/api/v1/cost-centers/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var getPayload = await getResponse.Content.ReadFromJsonAsync<CostCenterItem>(JsonOptions);
        Assert.NotNull(getPayload);
        Assert.Equal("CC-001", getPayload!.Code);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/cost-centers/{created.Id}", new
        {
            code = "CC-001",
            name = "Centro Principal Actualizado",
            type = "SalaryExpense",
            payrollExpenseAccountCode = "5101-001",
            employerContributionAccountCode = "5102-001",
            provisionAccountCode = "5103-001",
            description = "Actualizado",
            concurrencyToken = created.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<CostCenterItem>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Centro Principal Actualizado", updated!.Name);
        Assert.Equal(CostCenterType.SalaryExpense, updated.Type);

        var usageResponse = await client.GetAsync($"/api/v1/cost-centers/{created.Id}/usage");
        usageResponse.EnsureSuccessStatusCode();
        var usage = await usageResponse.Content.ReadFromJsonAsync<CostCenterUsageItem>(JsonOptions);
        Assert.NotNull(usage);
        Assert.False(usage!.HasActiveReferences);
        Assert.Equal(0, usage.OrgUnitActiveReferences);
        Assert.Equal(0, usage.PositionSlotActiveReferences);

        var inactivateResponse = await client.PatchAsJsonAsync($"/api/v1/cost-centers/{created.Id}/inactivate", new
        {
            concurrencyToken = updated.ConcurrencyToken
        });
        inactivateResponse.EnsureSuccessStatusCode();
        var inactive = await inactivateResponse.Content.ReadFromJsonAsync<CostCenterItem>(JsonOptions);
        Assert.NotNull(inactive);
        Assert.False(inactive!.IsActive);

        var activateResponse = await client.PatchAsJsonAsync($"/api/v1/cost-centers/{created.Id}/activate", new
        {
            concurrencyToken = inactive.ConcurrencyToken
        });
        activateResponse.EnsureSuccessStatusCode();
        var active = await activateResponse.Content.ReadFromJsonAsync<CostCenterItem>(JsonOptions);
        Assert.NotNull(active);
        Assert.True(active!.IsActive);

        var csvResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/cost-centers/export?format=csv");
        csvResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);

        var xlsxResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/cost-centers/export?format=xlsx");
        xlsxResponse.EnsureSuccessStatusCode();
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsxResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CostCenters_Create_WithDuplicateCode_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCostCenterAdminContext(scenario));

        _ = await CreateCostCenterAsync(client, scenario.TenantId, "CC-DUP", "Centro 1", "Mixed");

        var duplicateResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/cost-centers", new
        {
            code = "CC-DUP",
            name = "Centro 2",
            type = "Mixed",
            payrollExpenseAccountCode = (string?)null,
            employerContributionAccountCode = (string?)null,
            provisionAccountCode = (string?)null,
            description = (string?)null
        });

        await AssertProblemDetailsAsync(duplicateResponse, HttpStatusCode.Conflict, "COST_CENTER_CODE_CONFLICT");
    }

    [Fact]
    public async Task CostCenters_Update_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCostCenterAdminContext(scenario));

        var costCenter = await CreateCostCenterAsync(client, scenario.TenantId, "CC-STALE", "Centro", "Mixed");

        var response = await client.PutAsJsonAsync($"/api/v1/cost-centers/{costCenter.Id}", new
        {
            code = "CC-STALE",
            name = "Centro Actualizado",
            type = "Mixed",
            payrollExpenseAccountCode = (string?)null,
            employerContributionAccountCode = (string?)null,
            provisionAccountCode = (string?)null,
            description = (string?)null,
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task CostCenters_Inactivate_WhenInUse_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCostCenterAdminContext(scenario));

        var costCenter = await CreateCostCenterAsync(client, scenario.TenantId, "CC-USE", "Centro Uso", "Mixed");

        _ = await CreateOrgUnitAsync(
            client,
            scenario.TenantId,
            code: "DIR-CC-USE",
            name: "Direccion Centro Costo",
            unitType: "Direccion",
            costCenterCode: costCenter.Code);

        var response = await client.PatchAsJsonAsync($"/api/v1/cost-centers/{costCenter.Id}/inactivate", new
        {
            concurrencyToken = costCenter.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "COST_CENTER_IN_USE");
    }

    [Fact]
    public async Task CostCenters_List_WithTenantMismatch_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCostCenterReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.OtherTenantId}/cost-centers?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task CostCenters_List_WithoutPermission_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/cost-centers?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "COST_CENTERS_FORBIDDEN");
    }

    [Fact]
    public async Task CostCenters_Create_WithAuditPermission_ShouldWriteAuditEvent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCostCenterAdminWithAuditContext(scenario));

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/cost-centers", new
        {
            code = "CC-AUD",
            name = "Centro Audit",
            type = "Mixed",
            payrollExpenseAccountCode = "5101-100",
            employerContributionAccountCode = "5102-100",
            provisionAccountCode = "5103-100",
            description = "audit"
        });
        response.EnsureSuccessStatusCode();

        var auditResponse = await client.GetAsync("/api/audit/logs?page=1&pageSize=50");
        auditResponse.EnsureSuccessStatusCode();

        var payload = await auditResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, static item => item.EventType == "COST_CENTER_CREATED" && item.EntityType == "CostCenter");
    }

    [Fact]
    public async Task OrgUnits_Create_WithUnknownCostCenter_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-CC-INV",
            name = "Direccion Invalida",
            unitType = "Direccion",
            parentId = (Guid?)null,
            sortOrder = 1,
            description = (string?)null,
            costCenterCode = "CC-NOT-EXISTS",
            managerEmployeeId = (Guid?)null
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ORG_UNIT_COST_CENTER_INVALID");
    }

    [Fact]
    public async Task PositionSlots_Create_WithInactiveCostCenter_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var costCenterClient = factory.CreateClientFor(CreateCostCenterAdminContext(scenario));

        var costCenter = await CreateCostCenterAsync(costCenterClient, scenario.TenantId, "CC-INACTIVE", "Centro Inactivo", "Mixed");

        var inactivateResponse = await costCenterClient.PatchAsJsonAsync($"/api/v1/cost-centers/{costCenter.Id}/inactivate", new
        {
            concurrencyToken = costCenter.ConcurrencyToken
        });
        inactivateResponse.EnsureSuccessStatusCode();

        using var slotClient = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(slotClient, scenario.TenantId, "DIR-PS-CC", "Direccion", "Direccion");
        var profile = await CreateJobProfileAsync(slotClient, scenario.TenantId, "JP-PS-CC", "Perfil");

        var response = await slotClient.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-slots", new
        {
            code = "PS-CC-INV",
            title = "Plaza con centro inactivo",
            jobProfileId = profile.Id,
            orgUnitId = orgUnit.Id,
            workCenterId = (Guid?)null,
            costCenterCode = "CC-INACTIVE",
            directDependencyPositionSlotId = (Guid?)null,
            functionalDependencyPositionSlotId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            occupiedEmployees = 0,
            isFixedTerm = false,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            notes = (string?)null
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "POSITION_SLOT_COST_CENTER_INVALID");
    }

    [Fact]
    public async Task SalaryTabulator_FullFlow_ShouldCreateSubmitApproveAndApplyLine()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));

        var createResponse = await requesterClient.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/change-requests", new
        {
            reason = "Ajuste anual",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassCode = "CLS-A",
                    salaryScaleCode = "S1",
                    currencyCode = "USD",
                    changeType = "Create",
                    proposedBaseAmount = 1200m,
                    proposedMinAmount = 1000m,
                    proposedMaxAmount = 1500m,
                    notes = "nuevo"
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(SalaryTabulatorChangeRequestStatus.Draft, created!.Status);

        var updateResponse = await requesterClient.PutAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}", new
        {
            reason = "Ajuste anual actualizado",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassCode = "CLS-A",
                    salaryScaleCode = "S1",
                    currencyCode = "USD",
                    changeType = "Create",
                    proposedBaseAmount = 1250m,
                    proposedMinAmount = 1100m,
                    proposedMaxAmount = 1600m,
                    notes = "ajustado"
                }
            },
            concurrencyToken = created.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(updated);

        var submitResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}/submit", new
        {
            concurrencyToken = updated!.ConcurrencyToken
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submitted);
        Assert.Equal(SalaryTabulatorChangeRequestStatus.Submitted, submitted!.Status);

        var impactResponse = await requesterClient.GetAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}/impact");
        impactResponse.EnsureSuccessStatusCode();

        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));
        var approveResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}/approve", new
        {
            decisionComment = "Aprobado",
            concurrencyToken = submitted.ConcurrencyToken
        });
        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(approved);
        Assert.Equal(SalaryTabulatorChangeRequestStatus.Approved, approved!.Status);

        var listLinesResponse = await approverClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator?salaryClass=CLS-A&page=1&pageSize=20");
        listLinesResponse.EnsureSuccessStatusCode();
        var linesPayload = await listLinesResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<SalaryTabulatorLineItem>>(JsonOptions);
        Assert.NotNull(linesPayload);
        Assert.Contains(linesPayload!.Items, line => line.SalaryClassCode == "CLS-A" && line.BaseAmount == 1250m);

        var csvResponse = await approverClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/export?format=csv");
        csvResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);

        var xlsxResponse = await approverClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/export?format=xlsx");
        xlsxResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsxResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SalaryTabulator_Approve_WithSelfApproval_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateSalaryTabulatorRequestAndApproveContext(scenario));

        var created = await CreateSalaryTabulatorRequestAsync(client, scenario.TenantId, "CLS-B", "S1", 1100m);

        var submitResponse = await client.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}/submit", new
        {
            concurrencyToken = created.ConcurrencyToken
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submitted);

        var approveResponse = await client.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}/approve", new
        {
            decisionComment = "auto",
            concurrencyToken = submitted!.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(approveResponse, HttpStatusCode.UnprocessableEntity, "SALARY_TABULATOR_APPROVAL_POLICY_VIOLATION");
    }

    [Fact]
    public async Task SalaryTabulator_Update_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));

        var created = await CreateSalaryTabulatorRequestAsync(client, scenario.TenantId, "CLS-C", "S1", 1000m);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}", new
        {
            reason = "stale",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassCode = "CLS-C",
                    salaryScaleCode = "S1",
                    currencyCode = "USD",
                    changeType = "Create",
                    proposedBaseAmount = 1050m,
                    proposedMinAmount = (decimal?)null,
                    proposedMaxAmount = (decimal?)null,
                    notes = (string?)null
                }
            },
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(updateResponse, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task SalaryTabulator_SearchLines_WithTenantMismatch_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateSalaryTabulatorReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.OtherTenantId}/salary-tabulator?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task SalaryTabulator_SearchLines_WithoutPermission_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "SALARY_TABULATOR_FORBIDDEN");
    }

    [Fact]
    public async Task SalaryTabulator_Approve_WithAuditPermission_ShouldWriteAuditEvent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        var created = await CreateSalaryTabulatorRequestAsync(requesterClient, scenario.TenantId, "CLS-D", "S1", 1400m);

        var submitResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}/submit", new
        {
            concurrencyToken = created.ConcurrencyToken
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submitted);

        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverWithAuditContext(scenario));
        var approveResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}/approve", new
        {
            decisionComment = "approved",
            concurrencyToken = submitted!.ConcurrencyToken
        });
        approveResponse.EnsureSuccessStatusCode();

        var auditResponse = await approverClient.GetAsync("/api/audit/logs?page=1&pageSize=50");
        auditResponse.EnsureSuccessStatusCode();

        var payload = await auditResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, static item => item.EventType == "SALARY_TABULATOR_REQUEST_APPROVED");
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

    private static TestUserContext CreatePositionSlotReadContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, PositionSlotPermissionCodes.Read);

    private static TestUserContext CreatePositionSlotAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PositionSlotPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin);

    private static TestUserContext CreatePositionSlotAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PositionSlotPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read));

    private static TestUserContext CreateCostCenterReadContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, CostCenterPermissionCodes.Read);

    private static TestUserContext CreateCostCenterAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            CostCenterPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            PositionSlotPermissionCodes.Admin);

    private static TestUserContext CreateCostCenterAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            CostCenterPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            PositionSlotPermissionCodes.Admin,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read));

    private static TestUserContext CreateSalaryTabulatorReadContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, SalaryTabulatorPermissionCodes.Read);

    private static TestUserContext CreateSalaryTabulatorRequesterContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            SalaryTabulatorPermissionCodes.Read,
            SalaryTabulatorPermissionCodes.Request);

    private static TestUserContext CreateSalaryTabulatorApproverContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.SecurityAdminUserId,
            scenario.TenantId,
            SalaryTabulatorPermissionCodes.Read,
            SalaryTabulatorPermissionCodes.Approve);

    private static TestUserContext CreateSalaryTabulatorRequestAndApproveContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            SalaryTabulatorPermissionCodes.Read,
            SalaryTabulatorPermissionCodes.Request,
            SalaryTabulatorPermissionCodes.Approve);

    private static TestUserContext CreateSalaryTabulatorApproverWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.SecurityAdminUserId,
            scenario.TenantId,
            SalaryTabulatorPermissionCodes.Read,
            SalaryTabulatorPermissionCodes.Approve,
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
        int? sortOrder = 1,
        string? costCenterCode = null)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/org-units", new
        {
            code,
            name,
            unitType,
            parentId,
            sortOrder,
            description = (string?)null,
            costCenterCode,
            managerEmployeeId = (Guid?)null
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OrgUnitItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<CostCenterItem> CreateCostCenterAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string name,
        string type)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/cost-centers", new
        {
            code,
            name,
            type,
            payrollExpenseAccountCode = "5101-001",
            employerContributionAccountCode = "5102-001",
            provisionAccountCode = "5103-001",
            description = "Centro de costo"
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CostCenterItem>(JsonOptions);
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

    private async Task<PositionSlotItem> CreatePositionSlotAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string title,
        Guid jobProfileId,
        Guid orgUnitId,
        int maxEmployees)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/position-slots", new
        {
            code,
            title,
            jobProfileId,
            orgUnitId,
            workCenterId = (Guid?)null,
            costCenterCode = (string?)null,
            directDependencyPositionSlotId = (Guid?)null,
            functionalDependencyPositionSlotId = (Guid?)null,
            status = "Vacant",
            maxEmployees,
            occupiedEmployees = 0,
            isFixedTerm = false,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            notes = (string?)null
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PositionSlotItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<SalaryTabulatorChangeRequestItem> CreateSalaryTabulatorRequestAsync(
        HttpClient client,
        Guid companyId,
        string salaryClassCode,
        string salaryScaleCode,
        decimal proposedBaseAmount)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/salary-tabulator/change-requests", new
        {
            reason = "Ajuste",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassCode,
                    salaryScaleCode,
                    currencyCode = "USD",
                    changeType = "Create",
                    proposedBaseAmount,
                    proposedMinAmount = (decimal?)null,
                    proposedMaxAmount = (decimal?)null,
                    notes = (string?)null
                }
            }
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private static Guid ExtractTenantIdFromJwt(string accessToken)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var tenantId = token.Claims.FirstOrDefault(static claim => claim.Type == "tid")?.Value;
        Assert.False(string.IsNullOrWhiteSpace(tenantId));
        return Guid.Parse(tenantId!);
    }

    private static object CreateInitialLegalRepresentativePayload() =>
        new
        {
            firstName = "Ana",
            lastName = "Mendoza",
            documentType = "TaxId",
            documentNumber = "0614-290190-102-3",
            positionTitle = "Representante Legal",
            representationType = "PrimaryLegalRepresentative",
            authorityDescription = "Representación general",
            appointmentInstrument = "Acta de nombramiento",
            appointmentDateUtc = DateTime.UtcNow.Date,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            email = "ana.mendoza@test.com",
            phone = "+50370000000",
            isPrimary = true
        };

    private static async Task AssertFirstItemHasAllowedActionsAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);

        var firstItem = items[0];
        Assert.True(firstItem.TryGetProperty("allowedActions", out var allowedActions));
        Assert.Equal(JsonValueKind.Object, allowedActions.ValueKind);
        Assert.True(allowedActions.TryGetProperty("canEdit", out _));
        Assert.True(allowedActions.TryGetProperty("reasons", out var reasons));
        Assert.Equal(JsonValueKind.Array, reasons.ValueKind);
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
        DateTime? ModifiedAtUtc,
        IReadOnlyCollection<ActiveLegalRepresentativeItem> ActiveLegalRepresentatives);

    private sealed record ActiveLegalRepresentativeItem(
        Guid Id,
        string FullName,
        string RepresentationType,
        string PositionTitle,
        bool IsPrimary);

    private sealed record LegalRepresentativeItem(
        Guid Id,
        Guid CompanyId,
        string FirstName,
        string LastName,
        string FullName,
        string DocumentType,
        string DocumentNumber,
        string PositionTitle,
        string RepresentationType,
        string? AuthorityDescription,
        string? AppointmentInstrument,
        DateTime? AppointmentDateUtc,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Email,
        string? Phone,
        bool IsPrimary,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record LegalRepresentativeListItem(
        Guid Id,
        Guid CompanyId,
        string FullName,
        string DocumentType,
        string DocumentNumber,
        string PositionTitle,
        string RepresentationType,
        bool IsPrimary,
        bool IsActive,
        Guid ConcurrencyToken);

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

    private sealed record PositionSlotListItem(
        Guid Id,
        string Code,
        PositionSlotStatus Status);

    private sealed record PositionSlotItem(
        Guid Id,
        string Code,
        string? Title,
        PositionSlotStatus Status,
        Guid? DirectDependencyPositionSlotId,
        int OccupiedEmployees,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PositionSlotGraphNodeItem(
        Guid Id,
        string Code,
        string Label,
        PositionSlotStatus Status);

    private sealed record PositionSlotGraphEdgeItem(
        Guid FromId,
        Guid ToId,
        PositionSlotDependencyRelationType RelationType);

    private sealed record PositionSlotGraphItem(
        IReadOnlyCollection<PositionSlotGraphNodeItem> Nodes,
        IReadOnlyCollection<PositionSlotGraphEdgeItem> Edges);

    private sealed record CostCenterListItem(
        Guid Id,
        string Code,
        string Name,
        CostCenterType Type,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record CostCenterItem(
        Guid Id,
        string Code,
        string Name,
        CostCenterType Type,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record CostCenterUsageItem(
        Guid Id,
        string Code,
        string Name,
        int OrgUnitActiveReferences,
        int OrgUnitInactiveReferences,
        int PositionSlotActiveReferences,
        int PositionSlotInactiveReferences,
        bool HasActiveReferences);

    private sealed record SalaryTabulatorLineItem(
        Guid Id,
        string SalaryClassCode,
        string SalaryScaleCode,
        decimal BaseAmount,
        Guid ConcurrencyToken);

    private sealed record SalaryTabulatorChangeRequestItem(
        Guid Id,
        string RequestNumber,
        SalaryTabulatorChangeRequestStatus Status,
        Guid ConcurrencyToken);

    private sealed record ReportCapabilitiesItem(
        string ResourceKey,
        bool SupportsPrint,
        bool SupportsExport,
        IReadOnlyCollection<string> SupportedTableFormats,
        IReadOnlyCollection<string> SupportedGraphFormats);

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
