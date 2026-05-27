using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Application.Features.SalaryTabulator.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.SalaryTabulator;
using CLARIHR.Infrastructure;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Api.IntegrationTests;

public sealed class ApiIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private const string LegacyIamRbacApiDeprecatedSkipReason =
        "Legacy /api/iam and /api/rbac routes were removed; coverage lives in account-company authorization and company-users tests.";

    [Fact]
    public async Task Register_ShouldReturnCreatedAndTokens()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Admin",
            lastName = "Local",
            email = "admin.local@test.com",
            password = "StrongPass123!",
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
    public async Task Register_WithoutCompanyPayload_ShouldReturnCreated()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Admin",
            lastName = "Local",
            email = "admin.local@test.com",
            password = "StrongPass123!",
            country = "SV",
            source = "integration-tests"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_ThenCreateFirstCompany_ThenSwitch_ShouldReturnTenantToken()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var anonymousClient = factory.CreateClient();

        var email = $"onboarding.{Guid.NewGuid():N}@clarihr.test";
        var registerResponse = await anonymousClient.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Onboarding",
            lastName = "User",
            email,
            password = "StrongPass123!",
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(registerPayload);
        Assert.False(string.IsNullOrWhiteSpace(registerPayload!.AccessToken));

        var registerToken = new JwtSecurityTokenHandler().ReadJwtToken(registerPayload.AccessToken);
        Assert.DoesNotContain(registerToken.Claims, static claim => claim.Type == "tid");
        Assert.DoesNotContain(registerToken.Claims, static claim => claim.Type == "role");

        using var accountClient = factory.CreateClientFor(
            TestUserContext.Authenticated(registerPayload.User.Id, scenario.TenantId));

        var createCompanyResponse = await accountClient.PostJsonAsync("/api/account/companies", new
        {
            name = "First Access Company",
            countryCode = "SV",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
        });
        Assert.Equal(HttpStatusCode.Created, createCompanyResponse.StatusCode);

        var companyPayload = await createCompanyResponse.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(companyPayload);

        var switchResponse = await accountClient.PostAsync(
            $"/api/account/companies/{companyPayload!.PublicId}/switch",
            content: null);
        Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);

        var switchPayload = await switchResponse.Content.ReadFromJsonAsync<SwitchActiveCompanyItem>(JsonOptions);
        Assert.NotNull(switchPayload);
        Assert.Equal(companyPayload.PublicId, switchPayload!.ActiveCompany.PublicId);

        var switchToken = new JwtSecurityTokenHandler().ReadJwtToken(switchPayload.AccessToken);
        var tid = switchToken.Claims.Single(claim => claim.Type == "tid").Value;
        Assert.Equal(companyPayload.PublicId.ToString(), tid);
        Assert.Equal("ADMIN DE EMPRESA", switchToken.Claims.Single(claim => claim.Type == "role").Value);
    }

    [Fact]
    public async Task Onboarding_CreateCompanyForSV_ShouldSeedTerritorialTreeWith14DepartmentsAnd44Municipalities()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var anonymousClient = factory.CreateClient();

        var email = $"onboarding.locations.{Guid.NewGuid():N}@clarihr.test";
        var registerResponse = await anonymousClient.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Onboarding",
            lastName = "Locations",
            email,
            password = "StrongPass123!",
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(registerPayload);

        using var accountClient = factory.CreateClientFor(
            TestUserContext.Authenticated(registerPayload!.User.Id, scenario.TenantId));

        var createCompanyResponse = await accountClient.PostJsonAsync("/api/account/companies", new
        {
            name = "Onboarding Locations Company",
            countryCode = "SV",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
        });
        Assert.Equal(HttpStatusCode.Created, createCompanyResponse.StatusCode);

        var companyPayload = await createCompanyResponse.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(companyPayload);

        using var locationsClient = factory.CreateClientFor(
            TestUserContext.Authenticated(
                registerPayload.User.Id,
                companyPayload!.PublicId,
                LocationPermissionCodes.Read));

        var locationTree = await GetLocationGroupTreeAsync(locationsClient, companyPayload.PublicId);
        var countryNode = Assert.Single(locationTree);
        Assert.Equal("SV", countryNode.Code);

        var departments = countryNode.Children.ToArray();
        Assert.Equal(14, departments.Length);
        var municipalities = departments.SelectMany(static department => department.Children).ToArray();
        Assert.Equal(44, municipalities.Length);
        Assert.Contains(departments, department => department.Code == "SAN_SALVADOR");
        Assert.Contains(municipalities, municipality => municipality.Code == "SAN_SALVADOR_CENTRO");
    }

    [Fact]
    public async Task Login_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var email = $"login.user.{Guid.NewGuid():N}@clarihr.test";
        const string password = "StrongPass123!";

        var registerResponse = await client.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Login",
            lastName = "User",
            email,
            password,
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostJsonAsync("/api/auth/login", new
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

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        Assert.DoesNotContain(jwt.Claims, static claim => claim.Type is System.Security.Claims.ClaimTypes.Role or "role" or "permission" or "permissions");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var email = $"login.user.invalid.{Guid.NewGuid():N}@clarihr.test";
        const string password = "StrongPass123!";

        var registerResponse = await client.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Login",
            lastName = "Invalid",
            email,
            password,
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword123!"
        });

        await AssertProblemDetailsAsync(loginResponse, HttpStatusCode.Unauthorized, "auth.login.invalid_credentials");
    }

    [Fact]
    public async Task Logout_ShouldRevokeRefreshTokensForAuthenticatedUser()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var anonymousClient = factory.CreateClient();

        var email = $"logout.user.{Guid.NewGuid():N}@clarihr.test";
        const string password = "StrongPass123!";

        var registerResponse = await anonymousClient.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Logout",
            lastName = "User",
            email,
            password,
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(registerPayload);
        Assert.False(string.IsNullOrWhiteSpace(registerPayload!.RefreshToken));

        using var authenticatedClient = factory.CreateClientFor(
            TestUserContext.Authenticated(registerPayload.User.Id, scenario.TenantId));

        var logoutResponse = await authenticatedClient.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await anonymousClient.PostJsonAsync("/api/auth/refresh", new
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
        Assert.True(representative.IsPrimary == true);
        Assert.Equal("PrimaryLegalRepresentative", representative.RepresentationType);
        Assert.False(string.IsNullOrWhiteSpace(representative.FullName));
    }

    [Fact]
    public async Task AccountCompanies_GetCountries_ShouldRequireAuthentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/account/companies/countries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccountCompanies_GetCountries_ShouldReturnActiveCountryCatalog()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync("/api/account/companies/countries");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<CountryCatalogItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload);
        Assert.Contains(payload!, item => item.Code == "SV" && item.Name == "El Salvador");
        Assert.Contains(payload, item => item.Code == "GT" && item.Name == "Guatemala");
    }

    [Fact]
    public async Task AccountCompanies_GetCompanyTypes_ShouldRequireAuthentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/account/companies/company-types?countryCode=SV");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccountCompanies_GetCompanyTypes_ShouldReturnCountryScopedCatalog()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var svTypes = await GetAccountCompanyTypesAsync(client, "SV");
        var usTypes = await GetAccountCompanyTypesAsync(client, "US");

        Assert.NotEmpty(svTypes);
        Assert.Contains(svTypes, item => item.Code == "SA_DE_CV" && item.Name == "Sociedad Anonima de Capital Variable");
        Assert.DoesNotContain(svTypes, item => item.Code == "LLC");

        Assert.NotEmpty(usTypes);
        Assert.Contains(usTypes, item => item.Code == "LLC" && item.Name == "Limited Liability Company");
        Assert.DoesNotContain(usTypes, item => item.Code == "SA_DE_CV");
    }

    [Fact]
    public async Task GeneralCatalogs_GetIdentificationTypes_ShouldReturnSeededCatalog()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/reference-catalogs/identification-types");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<PersonnelReferenceCatalogLookupItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload);
        Assert.All(payload!, item => Assert.NotEqual(Guid.Empty, item.Id));
        Assert.Contains(payload!, item => item.Code == "DUI");
        Assert.Contains(payload, item => item.Code == "NIT");
        Assert.Contains(payload, item => item.Code == "PASSPORT");
    }

    [Fact]
    public async Task AccountCompanies_GetLegalRepresentativePositionTitles_ShouldReturnSeededCatalog()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync("/api/account/companies/legal-representative-position-titles");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<LegalRepresentativePositionTitleCatalogItem>>(JsonOptions);
        Assert.NotNull(payload);

        Assert.All(payload!, item => Assert.NotEqual(Guid.Empty, item.Id));
        Assert.Equal(payload!.Count, payload.Select(item => item.Id).Distinct().Count());
        Assert.Equal(
            [
                "OWNER",
                "CEO",
                "EXECUTIVE_MANAGEMENT",
                "HUMAN_RESOURCES",
                "FINANCE",
                "ACCOUNTING",
                "OPERATIONS",
                "PROCUREMENT",
                "SALES",
                "MARKETING",
                "CUSTOMER_SERVICE",
                "INFORMATION_TECHNOLOGY",
                "SOFTWARE_DEVELOPMENT",
                "INFRASTRUCTURE_DEVOPS",
                "DATA_ANALYTICS",
                "LEGAL",
                "ADMINISTRATION",
                "LOGISTICS",
                "MAINTENANCE",
                "SECURITY"
            ],
            payload.Select(item => item.Code));
        Assert.Equal(
            [
                "OWNER",
                "CEO",
                "Executive Management",
                "Human Resources",
                "Finance",
                "Accounting",
                "Operations",
                "Procurement",
                "Sales",
                "Marketing",
                "Customer Service",
                "Information Technology",
                "Software Development",
                "Infrastructure / DevOps",
                "Data & Analytics",
                "Legal",
                "Administration",
                "Logistics",
                "Maintenance",
                "Security"
            ],
            payload.Select(item => item.Name));
        Assert.Equal(Enumerable.Range(1, 20), payload.Select(item => item.SortOrder));
    }

    [Fact]
    public async Task AccountCompanies_GetLegalRepresentativeRepresentationTypes_ShouldReturnSeededCatalog()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync("/api/account/companies/legal-representative-representation-types");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<LegalRepresentativeRepresentationTypeCatalogItem>>(JsonOptions);
        Assert.NotNull(payload);

        Assert.Collection(
            payload!,
            item =>
            {
                Assert.NotEqual(Guid.Empty, item.Id);
                Assert.Equal("PRIMARYLEGALREPRESENTATIVE", item.Code);
                Assert.Equal("Primary Legal Representative", item.Name);
                Assert.Equal(1, item.SortOrder);
            },
            item =>
            {
                Assert.NotEqual(Guid.Empty, item.Id);
                Assert.Equal("ALTERNATELEGALREPRESENTATIVE", item.Code);
                Assert.Equal("Alternate Legal Representative", item.Name);
                Assert.Equal(2, item.SortOrder);
            },
            item =>
            {
                Assert.NotEqual(Guid.Empty, item.Id);
                Assert.Equal("ATTORNEYINFACT", item.Code);
                Assert.Equal("Attorney in Fact", item.Name);
                Assert.Equal(3, item.SortOrder);
            });
    }

    [Fact]
    public async Task AccountCompanies_Create_WithSeededPositionTitleContainingSlash_ShouldReturnCreatedCompany()
    {
        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            var companyToArchive = dbContext.Companies.Single(company => company.Slug == "acme-two");
            companyToArchive.Archive();
            await dbContext.SaveChangesAsync();
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PostJsonAsync("/api/account/companies", new
        {
            name = "Acme DevOps",
            countryCode = "SV",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload("Infrastructure / DevOps")
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var representative = await dbContext.LegalRepresentatives
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.TenantId == payload!.PublicId);

        Assert.Equal("Infrastructure / DevOps", representative.PositionTitle);
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

        var response = await client.PostJsonAsync("/api/account/companies", new
        {
            name = "Acme Three",
            countryCode = "SV",
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
    public async Task AccountCompanies_Create_WhenInitialLegalRepresentativeOmitsPrimaryFlag_ShouldPersistNullPrimaryFlag()
    {
        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            var companyToArchive = dbContext.Companies.Single(company => company.Slug == "acme-two");
            companyToArchive.Archive();
            await dbContext.SaveChangesAsync();
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PostJsonAsync("/api/account/companies", new
        {
            name = "Acme Nullable Primary",
            countryCode = "SV",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload(includeIsPrimary: false)
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(payload);

        using var legalRepresentativesClient = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                payload!.PublicId,
                "LegalRepresentatives.Read"));

        var listResponse = await legalRepresentativesClient.GetAsync(
            $"/api/v1/companies/{payload.PublicId}/legal-representatives?page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<LegalRepresentativeListItem>>(JsonOptions);
        Assert.NotNull(listPayload);
        var representative = Assert.Single(listPayload!.Items);
        Assert.Null(representative.IsPrimary);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedRepresentative = await dbContext.LegalRepresentatives
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.TenantId == payload.PublicId);

        Assert.Null(persistedRepresentative.IsPrimary);
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

        var response = await client.PostJsonAsync("/api/account/companies", new
        {
            name = "Acme Three",
            countryCode = "SV"
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

        var createResponse = await accountClient.PostJsonAsync("/api/account/companies", new
        {
            name = "Acme Three",
            countryCode = "SV",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var company = await createResponse.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(company);

        using var locationClient = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, company!.PublicId, LocationPermissionCodes.Read));

        var hierarchyResponse = await locationClient.GetAsync($"/api/v1/companies/{company.PublicId}/location-hierarchy");

        hierarchyResponse.EnsureSuccessStatusCode();

        var hierarchy = await hierarchyResponse.Content.ReadFromJsonAsync<LocationHierarchyItem>(JsonOptions);
        Assert.NotNull(hierarchy);
        Assert.True(hierarchy!.IsMultiLevel);
        Assert.Equal("GENERAL", hierarchy.DefaultGroupCode);
        Assert.Equal("General", hierarchy.DefaultGroupName);
    }

    [Fact]
    public async Task AccountCompanies_Create_WithDifferentCountry_ShouldSeedGenericDefaultLocations()
    {
        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            var companyToArchive = dbContext.Companies.Single(company => company.Slug == "acme-two");
            companyToArchive.Archive();
            await dbContext.SaveChangesAsync();
        });

        using var accountClient = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var createResponse = await accountClient.PostJsonAsync("/api/account/companies", new
        {
            name = "Acme Guatemala",
            countryCode = "GT",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var company = await createResponse.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(company);
        Assert.Equal("GT", company!.CountryCode);

        using var locationClient = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, company.PublicId, LocationPermissionCodes.Read));

        var hierarchyResponse = await locationClient.GetAsync($"/api/v1/companies/{company.PublicId}/location-hierarchy");
        hierarchyResponse.EnsureSuccessStatusCode();

        var hierarchy = await hierarchyResponse.Content.ReadFromJsonAsync<LocationHierarchyItem>(JsonOptions);
        Assert.NotNull(hierarchy);
        Assert.False(hierarchy!.IsMultiLevel);

        var levelsResponse = await locationClient.GetAsync($"/api/v1/companies/{company.PublicId}/location-levels");
        levelsResponse.EnsureSuccessStatusCode();

        var levels = await levelsResponse.Content.ReadFromJsonAsync<List<LocationLevelItem>>(JsonOptions);
        var level = Assert.Single(levels!);
        Assert.Equal(1, level.LevelOrder);
        Assert.True(level.AllowsWorkCenters);
        Assert.Equal("Pais", level.DisplayName);
    }

    [Fact]
    public async Task AccountCompanies_Create_WhenLimitReached_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PostJsonAsync("/api/account/companies", new
        {
            name = "Acme Three",
            countryCode = "SV",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "COMPANY_LIMIT_REACHED");
    }

    [Fact]
    public async Task AccountCompanies_Update_ShouldRenameOwnedCompany()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PutJsonAsync($"/api/account/companies/{scenario.OtherTenantId}", new
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
    public async Task AccountCompanies_Update_WithCompanyType_ShouldReturnCompanyTypeMetadata()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var companyType = (await GetAccountCompanyTypesAsync(client, "SV"))
            .Single(item => item.Code == "SA_DE_CV");

        var response = await client.PutJsonAsync($"/api/account/companies/{scenario.OtherTenantId}", new
        {
            name = "Acme Two Typed",
            companyTypePublicId = companyType.Id
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.CompanyType);
        Assert.Equal(companyType.Id, payload.CompanyType!.Id);
        Assert.Equal("SA_DE_CV", payload.CompanyType.Code);
    }

    [Fact]
    public async Task AccountCompanies_Update_WithCompanyTypeFromDifferentCountry_ShouldReturn404()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var usCompanyType = (await GetAccountCompanyTypesAsync(client, "US"))
            .Single(item => item.Code == "LLC");

        var response = await client.PutJsonAsync($"/api/account/companies/{scenario.OtherTenantId}", new
        {
            name = "Acme Two Typed",
            companyTypePublicId = usCompanyType.Id
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.NotFound, "COMPANY_TYPE_NOT_FOUND");
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
            var countryCatalogItemId = await dbContext.CountryCatalogItems
                .Where(item => item.NormalizedCode == "SV")
                .Select(item => item.Id)
                .SingleAsync();

            var foreignCompany = Company.Create(
                "Foreign Company",
                "foreign-company",
                Guid.Parse("99999999-9999-9999-9999-999999999999"),
                "SV",
                countryCatalogItemId);
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
            var companyB = dbContext.Companies.Single(company => company.Slug == "acme-two");
            var roleB = dbContext.IamRoles.IgnoreQueryFilters().Single(role => role.Name == "Auditor B");

            var tenantBMembershipExists = await dbContext.UserCompanyMemberships
                .AnyAsync(membership => membership.UserId == actorUser.Id && membership.CompanyId == companyB.Id);

            if (!tenantBMembershipExists)
            {
                dbContext.UserCompanyMemberships.Add(UserCompanyMembership.Create(actorUser.Id, companyB.Id, roleB.Id, isPrimary: false));
            }

            await dbContext.SaveChangesAsync();
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PostAsync($"/api/account/companies/{scenario.OtherTenantId}/switch", content: null);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Switch company failed with {(int)response.StatusCode} {response.StatusCode}. Body: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<SwitchActiveCompanyItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(scenario.OtherTenantId, payload!.ActiveCompany.PublicId);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        var tid = token.Claims.Single(claim => claim.Type == "tid").Value;
        Assert.Equal(scenario.OtherTenantId.ToString(), tid);
        Assert.Equal("AUDITOR B", token.Claims.Single(claim => claim.Type == "role").Value);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actorUser = dbContext.AuthUsers.Single(user => user.PublicId == scenario.ActorUserId);
        var primaryMemberships = dbContext.UserCompanyMemberships
            .Where(membership => membership.UserId == actorUser.Id && membership.IsPrimary)
            .ToList();
        var primaryMembership = Assert.Single(primaryMemberships);
        var primaryCompanyPublicId = dbContext.Companies
            .Where(company => company.Id == primaryMembership.CompanyId)
            .Select(company => company.PublicId)
            .Single();
        Assert.Equal(scenario.OtherTenantId, primaryCompanyPublicId);
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

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/legal-representatives", new
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
    public async Task PersonnelFiles_CreateAndGet_ShouldReturnCreatedFile()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName = "Maria",
            lastName = "Rodriguez",
            birthDate = new DateTime(1992, 5, 6),
            maritalStatusCode = "SOLTERO_A",
            professionCode = "ANALISTA_DE_DATOS",
            nationality = "SV",
            personalEmail = "maria.rodriguez@test.com",
            institutionalEmail = (string?)null,
            personalPhone = "+50370000001",
            institutionalPhone = (string?)null,
            birthCountryCode = "SV",
            birthDepartmentCode = "SAN_SALVADOR",
            birthMunicipalityCode = "SAN_SALVADOR_CENTRO",
            photoFilePublicId = (Guid?)null,
            orgUnitPublicId = (Guid?)null,
            customDataJson = "{ \"shirt_size\": \"M\" }"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Maria Rodriguez", created!.FullName);

        var getResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        getResponse.EnsureSuccessStatusCode();

        var file = await getResponse.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        Assert.NotNull(file);
        Assert.Equal("Maria Rodriguez", file!.FullName);

        var identificationsResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/identifications");
        identificationsResponse.EnsureSuccessStatusCode();
        var identifications = await identificationsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileIdentificationItem>>(JsonOptions);
        Assert.NotNull(identifications);
        Assert.Empty(identifications!);
    }

    [Fact]
    public async Task PersonnelFiles_Search_ShouldNotExposeBirthDateOrConcurrencyToken()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        _ = await CreatePersonnelFileAsync(client, scenario.TenantId, "Ana", "Lopez", "DUI", "02222222-2");

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files?page=1&pageSize=20");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = document.RootElement.GetProperty("items")[0];
        Assert.False(item.TryGetProperty("birthDate", out _));
        Assert.False(item.TryGetProperty("concurrencyToken", out _));
        Assert.True(item.TryGetProperty("age", out _));
    }

    [Fact]
    public async Task PersonnelFiles_Create_WithLegacyItemsPayload_ShouldReturnValidation()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName = "Ana",
            lastName = "Lopez",
            birthDate = new DateTime(1991, 1, 1),
            maritalStatusCode = "SOLTERO_A",
            professionCode = "ANALISTA_DE_DATOS",
            nationality = "SV",
            personalEmail = (string?)null,
            institutionalEmail = (string?)null,
            personalPhone = "+50370000002",
            institutionalPhone = (string?)null,
            birthCountryCode = "SV",
            birthDepartmentCode = "LA_LIBERTAD",
            birthMunicipalityCode = "LA_LIBERTAD_SUR",
            photoFilePublicId = (Guid?)null,
            orgUnitPublicId = (Guid?)null,
            customDataJson = (string?)null,
            items = new[]
            {
                new
                {
                    identificationTypeCode = "DUI",
                    identificationNumber = "09999999-9",
                    issuedDate = (DateTime?)null,
                    expiryDate = (DateTime?)null,
                    issuer = (string?)null,
                    isPrimary = true
                }
            }
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "common.validation");
    }

    [Fact]
    public async Task PersonnelFileIdentifications_Add_ShouldPersistCreatedIdentification()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreateBarePersonnelFileAsync(client, scenario.TenantId, "Ana", "Lopez");

        var addResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/identifications", new
        {
            identificationTypeCode = "DUI",
            identificationNumber = "09999999-9",
            issuedDate = (DateTime?)null,
            expiryDate = (DateTime?)null,
            issuer = (string?)null,
            isPrimary = true,
            concurrencyToken = created.ConcurrencyToken
        });

        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var added = await addResponse.Content.ReadFromJsonAsync<PersonnelFileIdentificationItem>(JsonOptions);
        Assert.NotNull(added);
        Assert.Equal("DUI", added!.IdentificationTypeCode);
        Assert.Equal("09999999-9", added.IdentificationNumber);

        var identificationsResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/identifications");
        identificationsResponse.EnsureSuccessStatusCode();
        var identifications = await identificationsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileIdentificationItem>>(JsonOptions);
        var stored = Assert.Single(identifications!);
        Assert.Equal(added.Id, stored.Id);
    }

    [Fact]
    public async Task PersonnelFileIdentifications_Add_WithDuplicateIdentification_ShouldReturnConflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var firstFile = await CreateBarePersonnelFileAsync(client, scenario.TenantId, "Ana", "Lopez");
        var firstAddResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{firstFile.Id}/identifications", new
        {
            identificationTypeCode = "DUI",
            identificationNumber = "09999999-9",
            issuedDate = (DateTime?)null,
            expiryDate = (DateTime?)null,
            issuer = (string?)null,
            isPrimary = true,
            concurrencyToken = firstFile.ConcurrencyToken
        });
        firstAddResponse.EnsureSuccessStatusCode();

        var secondFile = await CreateBarePersonnelFileAsync(client, scenario.TenantId, "Beatriz", "Lopez");
        var duplicateResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{secondFile.Id}/identifications", new
        {
            identificationTypeCode = "DUI",
            identificationNumber = "09999999-9",
            issuedDate = (DateTime?)null,
            expiryDate = (DateTime?)null,
            issuer = (string?)null,
            isPrimary = true,
            concurrencyToken = secondFile.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(duplicateResponse, HttpStatusCode.Conflict, "PERSONNEL_FILE_IDENTIFICATION_CONFLICT");
    }

    [Fact]
    public async Task ReferenceCatalogs_Get_ShouldReturnSeededElSalvadorCatalogs()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var professionsResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/reference-catalogs/professions");
        professionsResponse.EnsureSuccessStatusCode();
        var professions = await professionsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelReferenceCatalogLookupItem>>(JsonOptions);
        Assert.NotNull(professions);
        Assert.Equal(35, professions!.Count);
        Assert.Contains(professions, item => item.Code == "ANALISTA_DE_DATOS" && item.Name == "Analista de datos");

        var maritalStatusesResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/reference-catalogs/marital-statuses");
        maritalStatusesResponse.EnsureSuccessStatusCode();
        var maritalStatuses = await maritalStatusesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelReferenceCatalogLookupItem>>(JsonOptions);
        Assert.NotNull(maritalStatuses);
        Assert.Equal(6, maritalStatuses!.Count);
        Assert.Contains(maritalStatuses, item => item.Code == "SOLTERO_A" && item.Name == "Soltero/a");

        var identificationTypesResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/reference-catalogs/identification-types");
        identificationTypesResponse.EnsureSuccessStatusCode();
        var identificationTypes = await identificationTypesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelReferenceCatalogLookupItem>>(JsonOptions);
        Assert.NotNull(identificationTypes);
        Assert.Equal(4, identificationTypes!.Count);
        Assert.Contains(identificationTypes, item => item.Code == "DUI" && item.Name == "DUI");
        Assert.Contains(identificationTypes, item => item.Code == "PASSPORT" && item.Name == "Pasaporte");

        var kinshipsResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/reference-catalogs/kinships");
        kinshipsResponse.EnsureSuccessStatusCode();
        var kinships = await kinshipsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelReferenceCatalogLookupItem>>(JsonOptions);
        Assert.NotNull(kinships);
        Assert.Equal(10, kinships!.Count);
        Assert.Contains(kinships, item => item.Code == "HERMANO_A" && item.Name == "Hermano/a");

        var departmentsResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/reference-catalogs/departments");
        departmentsResponse.EnsureSuccessStatusCode();
        var departments = await departmentsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelReferenceCatalogLookupItem>>(JsonOptions);
        Assert.NotNull(departments);
        Assert.Equal(14, departments!.Count);
        Assert.Contains(departments, item => item.Code == "SAN_SALVADOR" && item.Name == "San Salvador");

        var municipalitiesResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/reference-catalogs/municipalities?parentCode=SAN_SALVADOR");
        municipalitiesResponse.EnsureSuccessStatusCode();
        var municipalities = await municipalitiesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelReferenceCatalogLookupItem>>(JsonOptions);
        Assert.NotNull(municipalities);
        Assert.Equal(5, municipalities!.Count);
        Assert.Contains(municipalities, item => item.Code == "SAN_SALVADOR_CENTRO" && item.Name == "San Salvador Centro");
    }

    [Fact]
    public async Task PersonnelFiles_Create_WithInvalidCatalogCodes_ShouldReturnValidation()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var invalidProfessionResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName = "Lidia",
            lastName = "Lopez",
            birthDate = new DateTime(1994, 4, 20),
            maritalStatusCode = "SOLTERO_A",
            professionCode = "INVALID_PROFESSION",
            nationality = "SV",
            personalEmail = (string?)null,
            institutionalEmail = (string?)null,
            personalPhone = "+50370000010",
            institutionalPhone = (string?)null,
            birthCountryCode = "SV",
            birthDepartmentCode = "SAN_SALVADOR",
            birthMunicipalityCode = "SAN_SALVADOR_CENTRO",
            photoFilePublicId = (Guid?)null,
            orgUnitPublicId = (Guid?)null,
            customDataJson = (string?)null
        });

        await AssertProblemDetailsAsync(invalidProfessionResponse, HttpStatusCode.BadRequest, "common.validation");

        var created = await CreateBarePersonnelFileAsync(client, scenario.TenantId, "Marta", "Castillo");
        var invalidIdentificationTypeResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/identifications", new
        {
            identificationTypeCode = "UNKNOWN_DOCUMENT",
            identificationNumber = "09822222-2",
            issuedDate = (DateTime?)null,
            expiryDate = (DateTime?)null,
            issuer = (string?)null,
            isPrimary = true,
            concurrencyToken = created.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(invalidIdentificationTypeResponse, HttpStatusCode.BadRequest, "common.validation");
    }

    [Fact]
    public async Task PersonnelFileAddresses_ItemCrud_ShouldPersist()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(
            client,
            scenario.TenantId,
            "Carlos",
            "Ramirez",
            "DUI",
            "09877777-1");

        var addResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/addresses", new
        {
            addressLine = "Colonia Escalon",
            country = "SV",
            department = "SAN_SALVADOR",
            municipality = "SAN_SALVADOR_CENTRO",
            postalCode = "1101",
            isCurrent = true,
            concurrencyToken = created.ConcurrencyToken
        });

        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var added = await addResponse.Content.ReadFromJsonAsync<PersonnelFileAddressItem>(JsonOptions);
        Assert.NotNull(added);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/personnel-files/{created.Id}/addresses/{added!.Id}")
        {
            Content = JsonContent.Create(new
            {
                addressLine = "Residencial San Benito",
                country = "SV",
                department = "SAN_SALVADOR",
                municipality = "SAN_SALVADOR_CENTRO",
                postalCode = "1101",
                isCurrent = false
            })
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{added.ConcurrencyToken}\"");
        var updateResponse = await client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<PersonnelFileAddressItem>(JsonOptions);
        Assert.NotNull(updated);

        var getResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/addresses");
        getResponse.EnsureSuccessStatusCode();
        var addresses = await getResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileAddressItem>>(JsonOptions);
        var address = Assert.Single(addresses!);
        Assert.Equal("Residencial San Benito", address.AddressLine);
        Assert.False(address.IsCurrent);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/personnel-files/{created.Id}/addresses/{added.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{updated!.ConcurrencyToken}\"");
        var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var getAfterDeleteResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/addresses");
        getAfterDeleteResponse.EnsureSuccessStatusCode();
        var addressesAfterDelete = await getAfterDeleteResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileAddressItem>>(JsonOptions);
        Assert.NotNull(addressesAfterDelete);
        Assert.Empty(addressesAfterDelete!);
    }

    [Fact]
    public async Task PersonnelFileEmergencyContacts_ItemCrud_ShouldPersist()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(
            client,
            scenario.TenantId,
            "Elena",
            "Molina",
            "DUI",
            "09877777-2");

        var addResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/emergency-contacts", new
        {
            name = "Rosa Molina",
            relationship = "Madre",
            phone = "+50370000020",
            address = "San Salvador",
            workplace = "Casa",
            concurrencyToken = created.ConcurrencyToken
        });

        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var added = await addResponse.Content.ReadFromJsonAsync<PersonnelFileEmergencyContactItem>(JsonOptions);
        Assert.NotNull(added);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/personnel-files/{created.Id}/emergency-contacts/{added!.Id}")
        {
            Content = JsonContent.Create(new
            {
                name = "Juan Molina",
                relationship = "Padre",
                phone = "+50370000021",
                address = "Santa Tecla",
                workplace = "Oficina"
            })
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{added.ConcurrencyToken}\"");
        var updateResponse = await client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<PersonnelFileEmergencyContactItem>(JsonOptions);
        Assert.NotNull(updated);

        var getResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/emergency-contacts");
        getResponse.EnsureSuccessStatusCode();
        var contacts = await getResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileEmergencyContactItem>>(JsonOptions);
        var contact = Assert.Single(contacts!);
        Assert.Equal("Juan Molina", contact.Name);
        Assert.Equal("Padre", contact.Relationship);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/personnel-files/{created.Id}/emergency-contacts/{added.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{updated!.ConcurrencyToken}\"");
        var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var getAfterDeleteResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/emergency-contacts");
        getAfterDeleteResponse.EnsureSuccessStatusCode();
        var contactsAfterDelete = await getAfterDeleteResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileEmergencyContactItem>>(JsonOptions);
        Assert.NotNull(contactsAfterDelete);
        Assert.Empty(contactsAfterDelete!);
    }

    [Fact]
    public async Task PersonnelFileFamilyMembers_Add_WithValidKinshipCode_ShouldPersist()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(
            client,
            scenario.TenantId,
            "Dario",
            "Mena",
            "DUI",
            "09877777-3");

        var addResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/family-members", new
        {
            firstName = "Luis",
            lastName = "Mena",
            kinshipCode = "HERMANO_A",
            nationality = "SV",
            birthDate = new DateTime(2000, 7, 15),
            sex = "Male",
            maritalStatus = (string?)null,
            occupation = (string?)null,
            documentType = (string?)null,
            documentNumber = (string?)null,
            phone = (string?)null,
            isStudying = false,
            studyPlace = (string?)null,
            academicLevel = (string?)null,
            isBeneficiary = false,
            isWorking = false,
            workplace = (string?)null,
            jobTitle = (string?)null,
            workPhone = (string?)null,
            salary = (decimal?)null,
            isDeceased = false,
            deceasedDate = (DateTime?)null,
            concurrencyToken = created.ConcurrencyToken
        });

        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/family-members");
        getResponse.EnsureSuccessStatusCode();

        var familyMembers = await getResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileFamilyMemberItem>>(JsonOptions);
        Assert.NotNull(familyMembers);
        var member = Assert.Single(familyMembers!);
        Assert.Equal("HERMANO_A", member.KinshipCode);
    }

    [Fact]
    public async Task PersonnelFileFamilyMembers_Update_WithInvalidKinshipCode_ShouldReturnValidation()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(
            client,
            scenario.TenantId,
            "Elena",
            "Molina",
            "DUI",
            "09877777-4");

        var addResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/family-members", new
        {
            firstName = "Marcos",
            lastName = "Molina",
            kinshipCode = "HERMANO_A",
            nationality = "SV",
            birthDate = new DateTime(2001, 1, 5),
            sex = "Male",
            maritalStatus = (string?)null,
            occupation = (string?)null,
            documentType = (string?)null,
            documentNumber = (string?)null,
            phone = (string?)null,
            isStudying = false,
            studyPlace = (string?)null,
            academicLevel = (string?)null,
            isBeneficiary = false,
            isWorking = false,
            workplace = (string?)null,
            jobTitle = (string?)null,
            workPhone = (string?)null,
            salary = (decimal?)null,
            isDeceased = false,
            deceasedDate = (DateTime?)null,
            concurrencyToken = created.ConcurrencyToken
        });
        addResponse.EnsureSuccessStatusCode();

        var added = await addResponse.Content.ReadFromJsonAsync<PersonnelFileFamilyMemberItem>(JsonOptions);
        Assert.NotNull(added);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/personnel-files/{created.Id}/family-members/{added!.Id}")
        {
            Content = JsonContent.Create(new
            {
                firstName = "Marcos",
                lastName = "Molina",
                kinshipCode = "UNKNOWN_KINSHIP",
                nationality = "SV",
                birthDate = new DateTime(2001, 1, 5),
                sex = "Male",
                maritalStatus = (string?)null,
                occupation = (string?)null,
                documentType = (string?)null,
                documentNumber = (string?)null,
                phone = (string?)null,
                isStudying = false,
                studyPlace = (string?)null,
                academicLevel = (string?)null,
                isBeneficiary = false,
                isWorking = false,
                workplace = (string?)null,
                jobTitle = (string?)null,
                workPhone = (string?)null,
                salary = (decimal?)null,
                isDeceased = false,
                deceasedDate = (DateTime?)null
            })
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{added.ConcurrencyToken}\"");
        var updateResponse = await client.SendAsync(updateRequest);

        await AssertProblemDetailsAsync(updateResponse, HttpStatusCode.BadRequest, "common.validation");
    }

    [Fact]
    public async Task PersonnelFileFamilyMembers_Delete_ShouldRemoveItem()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(
            client,
            scenario.TenantId,
            "Julia",
            "Molina",
            "DUI",
            "09877777-5");

        var addResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/family-members", new
        {
            firstName = "Marcos",
            lastName = "Molina",
            kinshipCode = "HERMANO_A",
            nationality = "SV",
            birthDate = new DateTime(2001, 1, 5),
            sex = "Male",
            maritalStatus = (string?)null,
            occupation = (string?)null,
            documentType = (string?)null,
            documentNumber = (string?)null,
            phone = (string?)null,
            isStudying = false,
            studyPlace = (string?)null,
            academicLevel = (string?)null,
            isBeneficiary = false,
            isWorking = false,
            workplace = (string?)null,
            jobTitle = (string?)null,
            workPhone = (string?)null,
            salary = (decimal?)null,
            isDeceased = false,
            deceasedDate = (DateTime?)null,
            concurrencyToken = created.ConcurrencyToken
        });
        addResponse.EnsureSuccessStatusCode();

        var added = await addResponse.Content.ReadFromJsonAsync<PersonnelFileFamilyMemberItem>(JsonOptions);
        Assert.NotNull(added);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/personnel-files/{created.Id}/family-members/{added!.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{added.ConcurrencyToken}\"");
        var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/family-members");
        getResponse.EnsureSuccessStatusCode();
        var familyMembers = await getResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileFamilyMemberItem>>(JsonOptions);
        Assert.NotNull(familyMembers);
        Assert.Empty(familyMembers!);
    }

    [Fact]
    public async Task PersonnelFiles_Create_WithInvalidBirthHierarchy_ShouldReturnValidation()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var missingCountryResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName = "Silvia",
            lastName = "Rivas",
            birthDate = new DateTime(1990, 1, 30),
            maritalStatusCode = "SOLTERO_A",
            professionCode = "ANALISTA_DE_DATOS",
            nationality = "SV",
            personalEmail = (string?)null,
            institutionalEmail = (string?)null,
            personalPhone = "+50370000012",
            institutionalPhone = (string?)null,
            birthCountryCode = (string?)null,
            birthDepartmentCode = "SAN_SALVADOR",
            birthMunicipalityCode = (string?)null,
            photoFilePublicId = (Guid?)null,
            orgUnitPublicId = (Guid?)null,
            customDataJson = (string?)null
        });
        await AssertProblemDetailsAsync(missingCountryResponse, HttpStatusCode.BadRequest, "common.validation");

        var mismatchedMunicipalityResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName = "Teresa",
            lastName = "Molina",
            birthDate = new DateTime(1993, 8, 15),
            maritalStatusCode = "SOLTERO_A",
            professionCode = "ANALISTA_DE_DATOS",
            nationality = "SV",
            personalEmail = (string?)null,
            institutionalEmail = (string?)null,
            personalPhone = "+50370000013",
            institutionalPhone = (string?)null,
            birthCountryCode = "SV",
            birthDepartmentCode = "LA_LIBERTAD",
            birthMunicipalityCode = "SAN_SALVADOR_CENTRO",
            photoFilePublicId = (Guid?)null,
            orgUnitPublicId = (Guid?)null,
            customDataJson = (string?)null
        });
        await AssertProblemDetailsAsync(mismatchedMunicipalityResponse, HttpStatusCode.BadRequest, "common.validation");
    }

    [Fact]
    public async Task PersonnelFiles_Get_ShouldResolveReferenceCatalogNamesInDetailAndList()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(
            client,
            scenario.TenantId,
            "Rosa",
            "Alvarado",
            "DUI",
            "09855555-5",
            profession: "ANALISTA_DE_DATOS",
            maritalStatus: "SOLTERO_A");

        var detailResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/personal-info");
        detailResponse.EnsureSuccessStatusCode();
        using var detailDocument = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        var detail = detailDocument.RootElement;

        Assert.Equal("SOLTERO_A", detail.GetProperty("maritalStatusCode").GetString());
        Assert.Equal("Soltero/a", detail.GetProperty("maritalStatusName").GetString());
        Assert.Equal("ANALISTA_DE_DATOS", detail.GetProperty("professionCode").GetString());
        Assert.Equal("Analista de datos", detail.GetProperty("professionName").GetString());
        Assert.Equal("SV", detail.GetProperty("birthCountryCode").GetString());
        Assert.Equal("El Salvador", detail.GetProperty("birthCountryName").GetString());
        Assert.Equal("SAN_SALVADOR", detail.GetProperty("birthDepartmentCode").GetString());
        Assert.Equal("San Salvador", detail.GetProperty("birthDepartmentName").GetString());
        Assert.Equal("SAN_SALVADOR_CENTRO", detail.GetProperty("birthMunicipalityCode").GetString());
        Assert.Equal("San Salvador Centro", detail.GetProperty("birthMunicipalityName").GetString());

        var identificationsResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/identifications");
        identificationsResponse.EnsureSuccessStatusCode();
        using var identificationsDocument = JsonDocument.Parse(await identificationsResponse.Content.ReadAsStringAsync());
        var identifications = identificationsDocument.RootElement;
        Assert.True(identifications.GetArrayLength() > 0);
        var firstIdentification = identifications[0];
        Assert.Equal("DUI", firstIdentification.GetProperty("identificationTypeCode").GetString());
        Assert.Equal("DUI", firstIdentification.GetProperty("identificationTypeName").GetString());

        var listResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files?q=09855555-5&page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();
        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var listItems = listDocument.RootElement.GetProperty("items");
        var listedItem = listItems.EnumerateArray().Single(item =>
            (item.TryGetProperty("id", out var idElement) && idElement.GetGuid() == created.Id) ||
            (item.TryGetProperty("publicId", out var publicIdElement) && publicIdElement.GetGuid() == created.Id) ||
            (item.TryGetProperty("fullName", out var fullNameElement) &&
             string.Equals(fullNameElement.GetString(), created.FullName, StringComparison.Ordinal)));

        Assert.Equal("SOLTERO_A", listedItem.GetProperty("maritalStatusCode").GetString());
        Assert.Equal("Soltero/a", listedItem.GetProperty("maritalStatusName").GetString());
        Assert.Equal("ANALISTA_DE_DATOS", listedItem.GetProperty("professionCode").GetString());
        Assert.Equal("Analista de datos", listedItem.GetProperty("professionName").GetString());
    }

    [Fact(Skip = "Pending rewrite: documents now use File Management (FilePublicId) instead of multipart upload")]
    public async Task PersonnelFiles_DocumentUpload_ShouldReturnMetadataWithResolvedFileUrl()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Carlos", "Gomez", "DUI", "07777777-7");

        using var uploadContent = new MultipartFormDataContent();
        uploadContent.Add(new StringContent("DIPLOMA"), "documentType");
        uploadContent.Add(new StringContent("Adjunto de prueba"), "observations");
        uploadContent.Add(new StringContent(created.ConcurrencyToken.ToString()), "concurrencyToken");

        var fileBytes = "%PDF-hello personnel file"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        uploadContent.Add(fileContent, "file", "proof.pdf");

        var uploadResponse = await client.PostAsync($"/api/v1/personnel-files/{created.Id}/documents", uploadContent);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var document = await uploadResponse.Content.ReadFromJsonAsync<PersonnelFileDocumentItem>(JsonOptions);
        Assert.NotNull(document);
        Assert.Equal("proof.pdf", document!.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(fileBytes.Length, document.SizeBytes);
        Assert.NotNull(document.FileUrl);
        Assert.Contains("sig=fake", document.FileUrl, StringComparison.Ordinal);
    }

    [Fact(Skip = "Pending rewrite: documents now use File Management (FilePublicId) instead of multipart upload")]
    public async Task PersonnelFiles_GetDocuments_ShouldReturnLightweightMetadataWithoutDownloadingFiles()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Claudia", "Mendez", "DUI", "06666666-6");

        using var olderUploadContent = new MultipartFormDataContent();
        olderUploadContent.Add(new StringContent("CONSTANCIA"), "documentType");
        olderUploadContent.Add(new StringContent("Documento mas antiguo"), "observations");
        olderUploadContent.Add(new StringContent(created.ConcurrencyToken.ToString()), "concurrencyToken");

        byte[] olderFileBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03];
        var olderFileContent = new ByteArrayContent(olderFileBytes);
        olderFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        olderUploadContent.Add(olderFileContent, "file", "older.png");

        var olderUploadResponse = await client.PostAsync($"/api/v1/personnel-files/{created.Id}/documents", olderUploadContent);
        Assert.Equal(HttpStatusCode.Created, olderUploadResponse.StatusCode);
        var olderDocument = await olderUploadResponse.Content.ReadFromJsonAsync<PersonnelFileDocumentItem>(JsonOptions);
        Assert.NotNull(olderDocument);

        var personnelFileResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        personnelFileResponse.EnsureSuccessStatusCode();
        var personnelFile = await personnelFileResponse.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        Assert.NotNull(personnelFile);

        using var newerUploadContent = new MultipartFormDataContent();
        newerUploadContent.Add(new StringContent("DIPLOMA"), "documentType");
        newerUploadContent.Add(new StringContent("Documento mas reciente"), "observations");
        newerUploadContent.Add(new StringContent(personnelFile!.ConcurrencyToken.ToString()), "concurrencyToken");

        var newerFileBytes = "%PDF-newer preview payload"u8.ToArray();
        var newerFileContent = new ByteArrayContent(newerFileBytes);
        newerFileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        newerUploadContent.Add(newerFileContent, "file", "newer.pdf");

        var newerUploadResponse = await client.PostAsync($"/api/v1/personnel-files/{created.Id}/documents", newerUploadContent);
        Assert.Equal(HttpStatusCode.Created, newerUploadResponse.StatusCode);
        var newerDocument = await newerUploadResponse.Content.ReadFromJsonAsync<PersonnelFileDocumentItem>(JsonOptions);
        Assert.NotNull(newerDocument);

        var documentsResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/documents");
        documentsResponse.EnsureSuccessStatusCode();

        var documents = await documentsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileDocumentDetailItem>>(JsonOptions);
        Assert.NotNull(documents);

        var documentItems = documents!.ToArray();
        Assert.Equal(2, documentItems.Length);
        Assert.Equal(newerDocument!.Id, documentItems[0].Id);
        Assert.Equal("DIPLOMA", documentItems[0].DocumentType);
        Assert.Equal("newer.pdf", documentItems[0].FileName);
        Assert.Equal("application/pdf", documentItems[0].ContentType);
        Assert.NotNull(documentItems[0].FileUrl);
        Assert.Contains("sig=fake", documentItems[0].FileUrl!, StringComparison.Ordinal);
        Assert.Equal(newerFileBytes.Length, documentItems[0].SizeBytes);
        Assert.True(documentItems[0].IsActive);
        Assert.Equal(olderDocument!.Id, documentItems[1].Id);
        Assert.Equal("older.png", documentItems[1].FileName);
    }

    [Fact(Skip = "Pending rewrite: documents now use File Management (FilePublicId) instead of multipart upload")]
    public async Task PersonnelFiles_ReplaceDocuments_ShouldSyncCollection_WithoutUploadingUnchangedFiles()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Marcos", "Soto", "DUI", "01234567-8");

        using var firstUploadContent = new MultipartFormDataContent();
        firstUploadContent.Add(new StringContent("CONSTANCIA"), "documentType");
        firstUploadContent.Add(new StringContent("Documento base"), "observations");
        firstUploadContent.Add(new StringContent(created.ConcurrencyToken.ToString()), "concurrencyToken");

        var firstBytes = "%PDF-original payload"u8.ToArray();
        var firstFileContent = new ByteArrayContent(firstBytes);
        firstFileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        firstUploadContent.Add(firstFileContent, "file", "original.pdf");

        var firstUploadResponse = await client.PostAsync($"/api/v1/personnel-files/{created.Id}/documents", firstUploadContent);
        Assert.Equal(HttpStatusCode.Created, firstUploadResponse.StatusCode);
        var firstDocument = await firstUploadResponse.Content.ReadFromJsonAsync<PersonnelFileDocumentItem>(JsonOptions);
        Assert.NotNull(firstDocument);

        var shellResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        shellResponse.EnsureSuccessStatusCode();
        var shell = await shellResponse.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        Assert.NotNull(shell);

        using var secondUploadContent = new MultipartFormDataContent();
        secondUploadContent.Add(new StringContent("EXPEDIENTE"), "documentType");
        secondUploadContent.Add(new StringContent("Documento a reemplazar"), "observations");
        secondUploadContent.Add(new StringContent(shell!.ConcurrencyToken.ToString()), "concurrencyToken");

        byte[] secondBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x10, 0x20, 0x30];
        var secondFileContent = new ByteArrayContent(secondBytes);
        secondFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        secondUploadContent.Add(secondFileContent, "file", "replace-me.png");

        var secondUploadResponse = await client.PostAsync($"/api/v1/personnel-files/{created.Id}/documents", secondUploadContent);
        Assert.Equal(HttpStatusCode.Created, secondUploadResponse.StatusCode);
        var secondDocument = await secondUploadResponse.Content.ReadFromJsonAsync<PersonnelFileDocumentItem>(JsonOptions);
        Assert.NotNull(secondDocument);

        var latestShellResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        latestShellResponse.EnsureSuccessStatusCode();
        var latestShell = await latestShellResponse.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        Assert.NotNull(latestShell);

        using var replaceContent = new MultipartFormDataContent();
        replaceContent.Add(new StringContent(latestShell!.ConcurrencyToken.ToString()), "concurrencyToken");
        replaceContent.Add(new StringContent(JsonSerializer.Serialize(new
        {
            items = new object[]
            {
                new
                {
                    documentPublicId = firstDocument!.Id,
                    documentType = "CONSTANCIA-LABORAL",
                    observations = "Solo metadatos",
                    deliveryDate = "2026-04-25T00:00:00Z",
                    loanDate = (string?)null,
                    returnDate = (string?)null
                },
                new
                {
                    documentPublicId = secondDocument!.Id,
                    documentType = "EXPEDIENTE-ACTUALIZADO",
                    observations = "Reemplazo con archivo nuevo",
                    deliveryDate = "2026-04-26T00:00:00Z",
                    loanDate = "2026-04-27T00:00:00Z",
                    returnDate = "2026-04-30T00:00:00Z",
                    fileKey = "replacementFile"
                },
                new
                {
                    documentType = "DIPLOMA",
                    observations = "Documento nuevo",
                    deliveryDate = "2026-05-01T00:00:00Z",
                    loanDate = (string?)null,
                    returnDate = (string?)null,
                    fileKey = "newFile"
                }
            }
        })), "manifestJson");

        byte[] replacementBytes = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00, 0x08, 0x00];
        var replacementFile = new ByteArrayContent(replacementBytes);
        replacementFile.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        replaceContent.Add(replacementFile, "replacementFile", "replacement.docx");

        var newBytes = "%PDF-new document payload"u8.ToArray();
        var newFile = new ByteArrayContent(newBytes);
        newFile.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        replaceContent.Add(newFile, "newFile", "new-put.pdf");

        var replaceResponse = await client.PutAsync($"/api/v1/personnel-files/{created.Id}/documents", replaceContent);
        replaceResponse.EnsureSuccessStatusCode();

        var payload = await replaceResponse.Content.ReadFromJsonAsync<PersonnelFileSectionResultItem<IReadOnlyCollection<PersonnelFileDocumentDetailItem>>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.NotEqual(latestShell.ConcurrencyToken, payload!.PersonnelFileConcurrencyToken);
        Assert.Equal(3, payload.Data.Count);

        var synchronizedDocuments = payload.Data.OrderBy(item => item.CreatedAtUtc).ToArray();
        var metadataOnly = synchronizedDocuments.Single(item => item.Id == firstDocument.Id);
        var replaced = synchronizedDocuments.Single(item => item.Id == secondDocument.Id);
        var createdByPut = synchronizedDocuments.Single(item => item.Id != firstDocument.Id && item.Id != secondDocument.Id);

        Assert.Equal("CONSTANCIA-LABORAL", metadataOnly.DocumentType);
        Assert.Equal("Solo metadatos", metadataOnly.Observations);
        Assert.Equal("original.pdf", metadataOnly.FileName);
        Assert.Equal("application/pdf", metadataOnly.ContentType);
        Assert.True(metadataOnly.IsActive);
        Assert.NotEqual(firstDocument.ConcurrencyToken, metadataOnly.ConcurrencyToken);
        Assert.NotNull(metadataOnly.FileUrl);
        Assert.Contains("sig=fake", metadataOnly.FileUrl!, StringComparison.Ordinal);

        Assert.Equal("replacement.docx", replaced.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", replaced.ContentType);
        Assert.Equal(replacementBytes.Length, replaced.SizeBytes);
        Assert.Equal("EXPEDIENTE-ACTUALIZADO", replaced.DocumentType);
        Assert.True(replaced.IsActive);
        Assert.NotEqual(secondDocument.ConcurrencyToken, replaced.ConcurrencyToken);
        Assert.NotNull(replaced.FileUrl);

        Assert.Equal("DIPLOMA", createdByPut.DocumentType);
        Assert.Equal("new-put.pdf", createdByPut.FileName);
        Assert.Equal("application/pdf", createdByPut.ContentType);
        Assert.Equal(newBytes.Length, createdByPut.SizeBytes);
        Assert.True(createdByPut.IsActive);
        Assert.NotEqual(Guid.Empty, createdByPut.ConcurrencyToken);
    }

    [Fact]
    public async Task PersonnelFiles_LegacyDocumentRoutes_ShouldNotExist()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var legacyReplaceResponse = await client.PatchAsync(
            $"/api/v1/personnel-files/documents/{Guid.NewGuid()}/file",
            CreateLegacyReplaceContent());
        Assert.Equal(HttpStatusCode.NotFound, legacyReplaceResponse.StatusCode);

        var legacyInactivateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/personnel-files/documents/{Guid.NewGuid()}/inactivate",
            new
            {
                concurrencyToken = Guid.NewGuid()
            });
        Assert.Equal(HttpStatusCode.NotFound, legacyInactivateResponse.StatusCode);

        var directReplaceResponse = await client.PatchAsync(
            $"/api/v1/personnel-file-documents/{Guid.NewGuid()}/file",
            CreateLegacyReplaceContent());
        Assert.Equal(HttpStatusCode.NotFound, directReplaceResponse.StatusCode);

        var directInactivateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/personnel-file-documents/{Guid.NewGuid()}/inactivate",
            new
            {
                concurrencyToken = Guid.NewGuid()
            });
        Assert.Equal(HttpStatusCode.NotFound, directInactivateResponse.StatusCode);

        static MultipartFormDataContent CreateLegacyReplaceContent()
        {
            var replaceContent = new MultipartFormDataContent();
            replaceContent.Add(new StringContent(Guid.NewGuid().ToString()), "concurrencyToken");

            byte[] replacementBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x10];
            var replacementFileContent = new ByteArrayContent(replacementBytes);
            replacementFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            replaceContent.Add(replacementFileContent, "file", "legacy.png");
            return replaceContent;
        }
    }

    [Fact]
    public async Task PersonnelFiles_PatchIsActive_ShouldTogglePersonnelFileState()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreateBarePersonnelFileAsync(client, scenario.TenantId, "Lucia", "Reyes");

        using var inactivateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/personnel-files/{created.Id}")
        {
            Content = new StringContent(
                "[{\"op\":\"replace\",\"path\":\"/isActive\",\"value\":false}]",
                Encoding.UTF8,
                "application/json-patch+json")
        };
        inactivateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");
        var inactivateResponse = await client.SendAsync(inactivateRequest);
        inactivateResponse.EnsureSuccessStatusCode();

        using var inactivateDocument = JsonDocument.Parse(await inactivateResponse.Content.ReadAsStringAsync());
        var inactivated = inactivateDocument.RootElement;
        Assert.False(inactivated.GetProperty("isActive").GetBoolean());
        Assert.True(inactivated.TryGetProperty("lifecycleStatus", out _));
        Assert.True(inactivated.TryGetProperty("concurrencyToken", out var inactivatedToken));
        // The unified PATCH returns the personal-info projection (carries the edited fields),
        // not the lightweight shell, so the caller can re-render without an extra fetch.
        Assert.True(inactivated.TryGetProperty("firstName", out _));
        Assert.False(inactivated.TryGetProperty("documents", out _));

        using var activateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/personnel-files/{created.Id}")
        {
            Content = new StringContent(
                "[{\"op\":\"replace\",\"path\":\"/isActive\",\"value\":true}]",
                Encoding.UTF8,
                "application/json-patch+json")
        };
        activateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{inactivatedToken.GetGuid()}\"");
        var activateResponse = await client.SendAsync(activateRequest);
        activateResponse.EnsureSuccessStatusCode();

        using var activateDocument = JsonDocument.Parse(await activateResponse.Content.ReadAsStringAsync());
        var activated = activateDocument.RootElement;
        Assert.True(activated.GetProperty("isActive").GetBoolean());
        Assert.True(activated.TryGetProperty("firstName", out _));
        Assert.False(activated.TryGetProperty("documents", out _));
    }

    [Fact]
    public async Task PersonnelFiles_Create_ShouldRateLimit()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        HttpResponseMessage? lastResponse = null;
        for (var index = 0; index < 21; index++)
        {
            lastResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
            {
                recordType = "Candidate",
                firstName = $"Rate{index}",
                lastName = "Create",
                birthDate = new DateTime(1992, 5, 6),
                maritalStatusCode = "SOLTERO_A",
                professionCode = "ANALISTA_DE_DATOS",
                nationality = "SV",
                personalEmail = (string?)null,
                institutionalEmail = (string?)null,
                personalPhone = $"+5037001{index:0000}",
                institutionalPhone = (string?)null,
                birthCountryCode = "SV",
                birthDepartmentCode = "SAN_SALVADOR",
                birthMunicipalityCode = "SAN_SALVADOR_CENTRO",
                photoFilePublicId = (Guid?)null,
                orgUnitPublicId = (Guid?)null,
                customDataJson = (string?)null
            });

            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        await AssertProblemDetailsAsync(lastResponse, HttpStatusCode.TooManyRequests, "common.too_many_requests");
    }

    [Fact]
    public async Task PersonnelFiles_Search_ShouldRateLimit()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        HttpResponseMessage? lastResponse = null;
        for (var index = 0; index < 121; index++)
        {
            lastResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files?page=1&pageSize=20&q={index}");
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        await AssertProblemDetailsAsync(lastResponse, HttpStatusCode.TooManyRequests, "common.too_many_requests");
    }

    [Fact]
    public async Task PositionSlots_Export_ShouldRateLimit()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        HttpResponseMessage? lastResponse = null;
        for (var index = 0; index < 11; index++)
        {
            lastResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/position-slots/export?format=csv");
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        await AssertProblemDetailsAsync(lastResponse, HttpStatusCode.TooManyRequests, "common.too_many_requests");
    }

    [Fact]
    public async Task PersonnelFiles_Lifecycle_ShouldRateLimit()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreateBarePersonnelFileAsync(client, scenario.TenantId, "Mario", "Lifecycle");
        HttpResponseMessage? lastResponse = null;

        for (var index = 0; index < 31; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/personnel-files/{created.Id}")
            {
                Content = new StringContent(
                    "[{\"op\":\"replace\",\"path\":\"/isActive\",\"value\":false}]",
                    Encoding.UTF8,
                    "application/json-patch+json")
            };
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");
            lastResponse = await client.SendAsync(request);

            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        await AssertProblemDetailsAsync(lastResponse, HttpStatusCode.TooManyRequests, "common.too_many_requests");
    }

    [Fact]
    public async Task PersonnelFiles_GetObservations_ShouldReturnNewestFirst()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Lucia", "Herrera", "DUI", "06543210-9");

        var firstCreateResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/observations", new
        {
            note = "Primera observacion",
            concurrencyToken = created.ConcurrencyToken
        });
        Assert.Equal(HttpStatusCode.Created, firstCreateResponse.StatusCode);
        var firstObservation = await firstCreateResponse.Content.ReadFromJsonAsync<PersonnelFileObservationItem>(JsonOptions);
        Assert.NotNull(firstObservation);

        var shellResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        shellResponse.EnsureSuccessStatusCode();
        var shell = await shellResponse.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        Assert.NotNull(shell);

        var secondCreateResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/observations", new
        {
            note = "Segunda observacion",
            concurrencyToken = shell!.ConcurrencyToken
        });
        Assert.Equal(HttpStatusCode.Created, secondCreateResponse.StatusCode);
        var secondObservation = await secondCreateResponse.Content.ReadFromJsonAsync<PersonnelFileObservationItem>(JsonOptions);
        Assert.NotNull(secondObservation);

        var getResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/observations");
        getResponse.EnsureSuccessStatusCode();

        var observations = await getResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileObservationItem>>(JsonOptions);
        Assert.NotNull(observations);

        var observationItems = observations!.ToArray();
        Assert.Equal(2, observationItems.Length);
        Assert.Equal(secondObservation!.Id, observationItems[0].Id);
        Assert.Equal("Segunda observacion", observationItems[0].Note);
        Assert.Equal(firstObservation!.Id, observationItems[1].Id);
        Assert.Equal("Primera observacion", observationItems[1].Note);
    }

    [Fact]
    public async Task PersonnelFiles_CurriculumSections_ShouldReplaceAndReturnUpdatedSections()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Carla", "Rivas", "DUI", "05555555-5");
        var concurrencyToken = created.ConcurrencyToken;
        var statusId = await GetEducationCatalogIdByCodeAsync(client, scenario.TenantId, "education-statuses", "GRADUATED");
        var studyTypeId = await GetEducationCatalogIdByCodeAsync(client, scenario.TenantId, "education-study-types", "BACHELOR");
        var careerId = await GetEducationCatalogIdByCodeAsync(client, scenario.TenantId, "education-careers", "SOFTWARE_ENGINEERING");
        var shiftId = await GetEducationCatalogIdByCodeAsync(client, scenario.TenantId, "education-shifts", "MORNING");
        var modalityId = await GetEducationCatalogIdByCodeAsync(client, scenario.TenantId, "education-modalities", "ONSITE");

        var educationsResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/educations", new
        {
            statusPublicId = statusId,
            degreeTitle = "Ingenieria en Sistemas",
            studyTypePublicId = studyTypeId,
            careerPublicId = careerId,
            institution = "Universidad CLARI",
            countryCode = "SV",
            specialty = (string?)null,
            isCurrentlyStudying = false,
            startDate = new DateTime(2015, 1, 10),
            endDate = new DateTime(2020, 11, 30),
            shiftPublicId = shiftId,
            modalityPublicId = modalityId,
            totalSubjects = 50,
            approvedSubjects = 50,
            concurrencyToken
        });
        educationsResponse.EnsureSuccessStatusCode();
        var educationPayload = await educationsResponse.Content.ReadFromJsonAsync<PersonnelFileEducationItem>(JsonOptions);
        Assert.NotNull(educationPayload);
        
        var getEducationsResponseFirst = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        var updatedShell = await getEducationsResponseFirst.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        concurrencyToken = updatedShell!.ConcurrencyToken;

        var languagesResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/languages", new
        {
            languageCode = "ENGLISH",
            levelCode = "ADVANCED",
            speaks = true,
            writes = true,
            reads = true,
            concurrencyToken
        });
        languagesResponse.EnsureSuccessStatusCode();
        var languagePayload = await languagesResponse.Content.ReadFromJsonAsync<PersonnelFileLanguageItem>(JsonOptions);
        Assert.NotNull(languagePayload);
        
        var getLanguagesResponseFirst = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        updatedShell = await getLanguagesResponseFirst.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        concurrencyToken = updatedShell!.ConcurrencyToken;

        var trainingsResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/trainings", new
        {
            trainingName = "Scrum Fundamentals",
            trainingTypeCode = "COURSE",
            description = "Curso de agilidad",
            topic = "Scrum",
            institution = "CLARI Academy",
            instructors = "Trainer One",
            score = 95m,
            startDate = new DateTime(2024, 2, 1),
            endDate = new DateTime(2024, 2, 15),
            isInternal = false,
            isLocal = true,
            countryCode = "SV",
            durationValue = 40m,
            durationUnitCode = "HOUR",
            costAmount = 50m,
            costCurrencyCode = "USD",
            concurrencyToken
        });
        trainingsResponse.EnsureSuccessStatusCode();
        var trainingPayload = await trainingsResponse.Content.ReadFromJsonAsync<PersonnelFileTrainingItem>(JsonOptions);
        Assert.NotNull(trainingPayload);

        var getTrainingsResponseFirst = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        updatedShell = await getTrainingsResponseFirst.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        concurrencyToken = updatedShell!.ConcurrencyToken;

        var employmentsResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/previous-employments", new
        {
            institution = "Empresa Uno",
            place = "San Salvador",
            lastPosition = "Developer",
            managerName = "Jefe Uno",
            entryDate = new DateTime(2021, 1, 1),
            retirementDate = new DateTime(2023, 1, 1),
            companyPhone = "+50370001111",
            exitReason = "Career growth",
            firstSalaryAmount = 700m,
            lastSalaryAmount = 950m,
            averageCommissionAmount = 0m,
            currencyCode = "USD",
            concurrencyToken
        });
        Assert.Equal(HttpStatusCode.Created, employmentsResponse.StatusCode);
        var employmentPayload = await employmentsResponse.Content.ReadFromJsonAsync<PersonnelFilePreviousEmploymentItem>(JsonOptions);
        Assert.NotNull(employmentPayload);
        Assert.Equal("Empresa Uno", employmentPayload!.Institution);

        var getShellForToken = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        var updatedShellForToken = await getShellForToken.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        concurrencyToken = updatedShellForToken!.ConcurrencyToken;

        var referencesResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/references", new
        {
            personName = "Ana Martinez",
            address = "Colonia Centro",
            phone = "+50370002222",
            referenceTypeCode = "PERSONAL",
            occupation = "Manager",
            workplace = "Empresa Uno",
            workPhone = "+50370003333",
            knownTimeYears = 3m,
            concurrencyToken
        });
        Assert.Equal(HttpStatusCode.Created, referencesResponse.StatusCode);
        var referencePayload = await referencesResponse.Content.ReadFromJsonAsync<PersonnelFileReferenceItem>(JsonOptions);
        Assert.NotNull(referencePayload);
        Assert.Equal("Ana Martinez", referencePayload!.PersonName);


        var getEducationsResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/educations");
        getEducationsResponse.EnsureSuccessStatusCode();
        var getEducations = await getEducationsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileEducationItem>>(JsonOptions);
        Assert.NotNull(getEducations);
        Assert.Single(getEducations!);

        var getLanguagesResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/languages");
        getLanguagesResponse.EnsureSuccessStatusCode();
        var getLanguages = await getLanguagesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileLanguageItem>>(JsonOptions);
        Assert.NotNull(getLanguages);
        Assert.Single(getLanguages!);

        var getTrainingsResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/trainings");
        getTrainingsResponse.EnsureSuccessStatusCode();
        var getTrainings = await getTrainingsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileTrainingItem>>(JsonOptions);
        Assert.NotNull(getTrainings);
        Assert.Single(getTrainings!);

        var getPreviousEmploymentsResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/previous-employments");
        getPreviousEmploymentsResponse.EnsureSuccessStatusCode();
        var getPreviousEmployments = await getPreviousEmploymentsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFilePreviousEmploymentItem>>(JsonOptions);
        Assert.NotNull(getPreviousEmployments);
        Assert.Single(getPreviousEmployments!);

        var getReferencesResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/references");
        getReferencesResponse.EnsureSuccessStatusCode();
        var getReferences = await getReferencesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileReferenceItem>>(JsonOptions);
        Assert.NotNull(getReferences);
        Assert.Single(getReferences!);
    }

    [Fact]
    public async Task PersonnelFiles_Print_WithSections_ShouldReturnFilteredPayload()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Diana", "Flores", "DUI", "04444444-4");
        var languageResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/languages", new
        {
            languageCode = "ENGLISH",
            levelCode = "ADVANCED",
            speaks = true,
            writes = true,
            reads = true,
            concurrencyToken = created.ConcurrencyToken
        });
        languageResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/print?sections=languages,references");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PersonnelFilePrintItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains("languages", payload!.IncludedSections, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("references", payload.IncludedSections, StringComparer.OrdinalIgnoreCase);
        Assert.Single(payload.PersonnelFile.Languages);
        Assert.Empty(payload.PersonnelFile.Identifications);
    }

    [Fact]
    public async Task PersonnelFiles_Print_WhenTenantMismatch_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var sourceClient = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));
        var created = await CreatePersonnelFileAsync(sourceClient, scenario.TenantId, "Tomas", "Perez", "DUI", "03333333-3");

        using var otherTenantClient = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.OtherTenantId,
                PersonnelFilePermissionCodes.Admin));

        var response = await otherTenantClient.GetAsync($"/api/v1/personnel-files/{created.Id}/print");
        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task PersonnelFiles_Search_WithColumnFiltersAndSort_ShouldReturnFilteredRows()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        _ = await CreatePersonnelFileAsync(client, scenario.TenantId, "Ana", "Lopez", "DUI", "02222222-2", profession: "TECNICO_A_DE_SOPORTE");
        var designer = await CreatePersonnelFileAsync(client, scenario.TenantId, "Brenda", "Garcia", "DUI", "01111111-1", profession: "DISENADOR_A_GRAFICO_A");

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files?profession=DISENADOR_A_GRAFICO_A&sortBy=createdAtUtc&sortDirection=Desc&page=1&pageSize=20");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<PersonnelFileListProjectionItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);
        var item = Assert.Single(payload.Items);
        Assert.Equal(designer.Id, item.Id);
    }

    [Fact]
    public async Task PersonnelFiles_Export_WithColumnFilters_ShouldApplySameFilters()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var tester = await CreatePersonnelFileAsync(client, scenario.TenantId, "Raul", "Navas", "DUI", "06666666-6", profession: "TECNICO_A_DE_SOPORTE");
        var designer = await CreatePersonnelFileAsync(client, scenario.TenantId, "Sonia", "Mendez", "DUI", "07777777-0", profession: "DISENADOR_A_GRAFICO_A");

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/export?format=csv&profession=DISENADOR_A_GRAFICO_A&sortBy=fullName&sortDirection=Asc");
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains(designer.FullName, csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(tester.FullName, csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersonnelFiles_DynamicQuery_ShouldReturnGroupsAndPagedItems()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        _ = await CreatePersonnelFileAsync(client, scenario.TenantId, "Luis", "Alfaro", "DUI", "01010101-1", profession: "INGENIERO_A_EN_SISTEMAS", maritalStatus: "SOLTERO_A", nationality: "SV");
        _ = await CreatePersonnelFileAsync(client, scenario.TenantId, "Marta", "Ayala", "DUI", "02020202-2", profession: "INGENIERO_A_CIVIL", maritalStatus: "SOLTERO_A", nationality: "SV");
        _ = await CreatePersonnelFileAsync(client, scenario.TenantId, "Nora", "Zelaya", "DUI", "03030303-3", profession: "DISENADOR_A_GRAFICO_A", maritalStatus: "CASADO_A", nationality: "GT");

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files/dynamic-query", new
        {
            filters = Array.Empty<object>(),
            groupBy = new[] { "maritalStatus", "nationality" },
            sort = new[]
            {
                new
                {
                    field = "fullName",
                    direction = "Asc"
                }
            },
            q = (string?)null,
            page = 1,
            pageSize = 50
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PersonnelFileDynamicQueryItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(3, payload!.TotalCount);
        Assert.Equal(3, payload.Items.Count);

        var maritalStatus = payload.Groups.Single(group => string.Equals(group.Field, "maritalstatus", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(maritalStatus.Buckets, bucket => bucket.Key == "SOLTERO_A" && bucket.Count == 2);
        Assert.Contains(maritalStatus.Buckets, bucket => bucket.Key == "CASADO_A" && bucket.Count == 1);

        var nationality = payload.Groups.Single(group => string.Equals(group.Field, "nationality", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nationality.Buckets, bucket => bucket.Key == "SV" && bucket.Count == 2);
        Assert.Contains(nationality.Buckets, bucket => bucket.Key == "GT" && bucket.Count == 1);
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
        Assert.True(payload!.IsMultiLevel);
        Assert.Equal("GENERAL", payload.DefaultGroupCode);
        Assert.Equal("General", payload.DefaultGroupName);
    }

    [Fact]
    public async Task LocationHierarchy_Update_WithValidToken_ShouldReturnUpdatedConfig()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var current = await GetLocationHierarchyAsync(client, scenario.TenantId);

        var response = await client.PutJsonAsync($"/api/v1/companies/{scenario.TenantId}/location-hierarchy", new
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

        var response = await client.PutJsonAsync($"/api/v1/companies/{scenario.TenantId}/location-hierarchy", new
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
    public async Task LocationLevels_List_ShouldReturnSeededCountryTemplate()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/location-levels");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<LocationLevelItem>>(JsonOptions);
        Assert.NotNull(payload);
        var levels = payload!.OrderBy(level => level.LevelOrder).ToArray();
        Assert.Equal(3, levels.Length);
        Assert.Collection(
            levels,
            level =>
            {
                Assert.Equal(1, level.LevelOrder);
                Assert.Equal("Pais", level.DisplayName);
                Assert.True(level.IsActive);
                Assert.True(level.IsRequired);
                Assert.False(level.AllowsWorkCenters);
            },
            level =>
            {
                Assert.Equal(2, level.LevelOrder);
                Assert.Equal("Departamento", level.DisplayName);
                Assert.True(level.IsActive);
                Assert.False(level.IsRequired);
                Assert.False(level.AllowsWorkCenters);
            },
            level =>
            {
                Assert.Equal(3, level.LevelOrder);
                Assert.Equal("Municipio", level.DisplayName);
                Assert.True(level.IsActive);
                Assert.False(level.IsRequired);
                Assert.True(level.AllowsWorkCenters);
            });
    }

    [Fact]
    public async Task LocationGroups_Create_ShouldReturnCreatedGroup()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/location-groups", new
        {
            levelOrder = 1,
            code = "WEST",
            name = "West",
            parentPublicId = (Guid?)null,
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

        var typeResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        Assert.Equal(HttpStatusCode.Created, typeResponse.StatusCode);
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro General",
            workCenterTypePublicId = workCenterType!.Id,
            locationGroupPublicId = defaultGroup.Id,
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

        var typeResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro General",
            workCenterTypePublicId = workCenterType!.Id,
            locationGroupPublicId = defaultGroup.Id,
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
        var group = await GetDefaultLocationGroupAsync(client, scenario.TenantId);

        var typeResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var createCenterResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro Apopa",
            workCenterTypePublicId = workCenterType!.Id,
            locationGroupPublicId = group.Id,
            address = "San Salvador",
            geoLat = (decimal?)null,
            geoLong = (decimal?)null,
            phone = "2222-2222",
            email = "apopa@acme-one.test",
            notes = "Centro Apopa"
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

        var typeResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var createCenterResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro General",
            workCenterTypePublicId = workCenterType!.Id,
            locationGroupPublicId = defaultGroup.Id,
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

        var typeResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "AGENCY",
            name = "Agency",
            requiresAddress = true,
            requiresGeo = false,
            allowsBiometric = true
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var createCenterResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "CEN-001",
            name = "Centro General",
            workCenterTypePublicId = workCenterType!.Id,
            locationGroupPublicId = defaultGroup.Id,
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
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, scenario.TenantId, "Direccion");

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-001",
            name = "Direccion General",
            orgUnitTypePublicId = orgUnitType.Id,
            functionalAreaPublicId = (Guid?)null,
            parentPublicId = (Guid?)null,
            sortOrder = 1,
            description = "Direccion principal",
            costCenterCode = (string?)null,
            managerEmployeePublicId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<OrgUnitItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("DIR-001", payload!.Code);
        Assert.Equal(orgUnitType.Id, payload.OrgUnitType.Id);
        Assert.Equal(orgUnitType.Code, payload.OrgUnitType.Code);
        Assert.Null(payload.Parent);
        Assert.True(payload.IsActive);
    }

    [Fact]
    public async Task OrgUnits_ListAndGetById_ShouldIncludeParentReference()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var root = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-001", "Direccion General", "Direccion");
        var child = await CreateOrgUnitAsync(client, scenario.TenantId, "GER-001", "Gerencia Finanzas", "Gerencia", root.Id);

        Assert.NotNull(child.Parent);
        Assert.Equal(root.Id, child.Parent!.Id);
        Assert.Equal(root.Code, child.Parent.Code);
        Assert.Equal(root.Name, child.Parent.Name);

        var listResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-units?page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<OrgUnitItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var listedChild = Assert.Single(listPayload!.Items, item => item.Id == child.Id);
        Assert.NotNull(listedChild.Parent);
        Assert.Equal(root.Id, listedChild.Parent!.Id);
        Assert.Equal(root.Code, listedChild.Parent.Code);
        Assert.Equal(root.Name, listedChild.Parent.Name);

        var detailResponse = await client.GetAsync($"/api/v1/org-units/{child.Id}");
        detailResponse.EnsureSuccessStatusCode();

        var detailJson = await detailResponse.Content.ReadAsStringAsync();
        using var detailDocument = JsonDocument.Parse(detailJson);
        Assert.False(detailDocument.RootElement.TryGetProperty("parentPublicId", out _));
        Assert.True(detailDocument.RootElement.TryGetProperty("parent", out var parentElement));
        Assert.True(parentElement.TryGetProperty("publicId", out var parentPublicIdElement));
        Assert.False(parentElement.TryGetProperty("id", out _));
        Assert.Equal(root.Id, parentPublicIdElement.GetGuid());

        var detailPayload = JsonSerializer.Deserialize<OrgUnitItem>(detailJson, JsonOptions);
        Assert.NotNull(detailPayload);
        Assert.NotNull(detailPayload.Parent);
        Assert.Equal(root.Id, detailPayload.Parent!.Id);
        Assert.Equal(root.Code, detailPayload.Parent.Code);
        Assert.Equal(root.Name, detailPayload.Parent.Name);
    }

    [Fact]
    public async Task OrgUnits_Update_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var unit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-001", "Direccion General", "Direccion");

        var response = await client.PutJsonAsync($"/api/v1/org-units/{unit.Id}", new
        {
            code = "DIR-001",
            name = "Direccion Actualizada",
            orgUnitTypePublicId = unit.OrgUnitType.Id,
            functionalAreaPublicId = unit.FunctionalArea?.Id,
            sortOrder = 1,
            description = "Actualizada",
            costCenterCode = "CC-01",
            managerEmployeePublicId = (Guid?)null,
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
            newParentPublicId = child.Id,
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
    public async Task OrgStructureCatalogs_UnitTypes_List_WithOrgUnitsReadFallback_ShouldReturnItems()
    {
        var scenario = await factory.ResetDatabaseAsync();

        using var adminClient = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));
        _ = await EnsureOrgUnitTypeAsync(adminClient, scenario.TenantId, "Direccion");

        using var readClient = factory.CreateClientFor(CreateOrgUnitReadContext(scenario));
        var response = await readClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/org-structure-catalogs/unit-types?page=1&pageSize=20");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<OrgStructureCatalogItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, item => item.Code.Equals("Direccion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AccountCompanies_GetCompanyTypes_ShouldReturnStableSeededCatalogWithoutDuplicates()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var firstPayload = await GetAccountCompanyTypesAsync(client, "MX");
        var secondPayload = await GetAccountCompanyTypesAsync(client, "MX");

        Assert.Equal(5, firstPayload.Count);
        Assert.Contains(firstPayload, item => item.Code == "SA_DE_CV" && item.Name == "Sociedad Anonima de Capital Variable");
        Assert.Contains(firstPayload, item => item.Code == "S_DE_RL_DE_CV" && item.Name == "Sociedad de Responsabilidad Limitada de Capital Variable");
        Assert.Contains(firstPayload, item => item.Code == "SAS" && item.Name == "Sociedad por Acciones Simplificada");
        Assert.Contains(firstPayload, item => item.Code == "BRANCH_OFFICE" && item.Name == "Sucursal");
        Assert.Contains(firstPayload, item => item.Code == "AC" && item.Name == "Asociacion Civil");
        Assert.Equal(firstPayload.Count, secondPayload.Count);
    }

    [Fact]
    public async Task OrgStructureCatalogs_FunctionalAreas_Inactivate_WhenInUse_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var orgUnitType = await EnsureOrgUnitTypeAsync(client, scenario.TenantId, "Direccion");
        var functionalArea = await EnsureFunctionalAreaAsync(client, scenario.TenantId, "ADMIN");

        var createOrgUnitResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-FA-USE",
            name = "Direccion Funcional",
            orgUnitTypePublicId = orgUnitType.Id,
            functionalAreaPublicId = functionalArea.Id,
            parentPublicId = (Guid?)null,
            sortOrder = 1,
            description = (string?)null,
            costCenterCode = (string?)null,
            managerEmployeePublicId = (Guid?)null
        });
        createOrgUnitResponse.EnsureSuccessStatusCode();

        var response = await client.PatchAsJsonAsync($"/api/v1/org-structure-catalogs/functional-areas/{functionalArea.Id}/inactivate", new
        {
            concurrencyToken = functionalArea.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "ORG_STRUCTURE_CATALOG_IN_USE");
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task Policy_IncludeAllowedActions_ShouldReturnActionsInCoreLists()
    {
        var scenario = await factory.ResetDatabaseAsync();

        using var orgUnitClient = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));
        var orgUnit = await CreateOrgUnitAsync(orgUnitClient, scenario.TenantId, "DIR-AA", "Direccion AllowedActions", "Direccion");

        using var jobProfileClient = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        var jobProfile = await CreateJobProfileAsync(jobProfileClient, scenario.TenantId, "JP-AA", "Perfil AllowedActions", orgUnit.Id);

        using var positionSlotClient = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));
        _ = await CreatePositionSlotAsync(
            positionSlotClient,
            scenario.TenantId,
            code: "PS-AA",
            title: "Plaza AllowedActions",
            jobProfileId: jobProfile.Id,
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
            $"/api/v1/companies/{scenario.TenantId}/salary-tabulator/lines?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(salaryLinesList);

        var salaryRequestsList = await approverClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/salary-tabulator/change-requests?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(salaryRequestsList);

        using var companyUsersClient = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Users, RbacPermissionAction.Read),
            (RbacPermissionScreen.Users, RbacPermissionAction.Update)));
        var companyUsersList = await companyUsersClient.GetAsync("/api/company/users?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(companyUsersList);

        using var locationClient = factory.CreateClientFor(CreateLocationAdminContext(scenario));
        var defaultLocationGroup = await GetDefaultLocationGroupAsync(locationClient, scenario.TenantId);
        var createWorkCenterTypeResponse = await locationClient.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types", new
        {
            code = "WCT-AA",
            name = "Tipo AllowedActions",
            requiresAddress = false,
            requiresGeo = false,
            allowsBiometric = false
        });
        createWorkCenterTypeResponse.EnsureSuccessStatusCode();
        var workCenterType = await createWorkCenterTypeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);
        Assert.NotNull(workCenterType);

        var createWorkCenterResponse = await locationClient.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/work-centers", new
        {
            code = "WC-AA",
            name = "Centro AllowedActions",
            workCenterTypePublicId = workCenterType!.Id,
            locationGroupPublicId = defaultLocationGroup.Id,
            address = (string?)null,
            geoLat = (decimal?)null,
            geoLong = (decimal?)null,
            phone = (string?)null,
            email = (string?)null,
            notes = (string?)null
        });
        createWorkCenterResponse.EnsureSuccessStatusCode();

        var locationGroupsList = await locationClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/location-groups?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(locationGroupsList);

        var workCenterTypesList = await locationClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/work-center-types?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(workCenterTypesList);

        var workCentersList = await locationClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/work-centers?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(workCentersList);

        using var catalogClient = factory.CreateClientFor(CreateJobProfileAdminWithCatalogContext(scenario));
        var orgUnitType = await EnsureOrgUnitTypeAsync(catalogClient, scenario.TenantId, "ORG-AA");
        _ = await EnsureFunctionalAreaAsync(catalogClient, scenario.TenantId, "FUNC-AREA-AA");
        var functionType = await EnsurePositionDescriptionCatalogItemAsync(catalogClient, scenario.TenantId, "position-function-types", "FUNC-AA");
        var contractType = await EnsurePositionDescriptionCatalogItemAsync(catalogClient, scenario.TenantId, "position-contract-types", "CON-AA");
        var classification = await EnsurePositionCategoryClassificationAsync(
            catalogClient,
            scenario.TenantId,
            "CLASS-AA",
            functionType.Id,
            contractType.Id,
            orgUnitType.Id);
        _ = await EnsurePositionCategoryAsync(catalogClient, scenario.TenantId, "CAT-AA", classification.Id);
        _ = await CreateJobCatalogItemAsync(catalogClient, scenario.TenantId, JobCatalogCategory.EducationLevel, "EDU-AA", "Educacion AllowedActions");

        var orgUnitTypesList = await catalogClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/org-structure-catalogs/unit-types?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(orgUnitTypesList);

        var functionalAreasList = await catalogClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/org-structure-catalogs/functional-areas?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(functionalAreasList);

        var positionFunctionTypesList = await catalogClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-description-catalogs/position-function-types/items?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(positionFunctionTypesList);

        var classificationsList = await catalogClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-category-classifications?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(classificationsList);

        var categoriesList = await catalogClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-categories?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(categoriesList);

        var jobCatalogsList = await catalogClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/job-catalogs/EducationLevel?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(jobCatalogsList);

        using var iamUsersClient = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Users, RbacPermissionAction.Read),
            (RbacPermissionScreen.Users, RbacPermissionAction.Update)));
        var iamUsersList = await iamUsersClient.GetAsync("/api/iam/users?pageNumber=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(iamUsersList);

        using var iamRolesClient = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Read),
            (RbacPermissionScreen.Roles, RbacPermissionAction.Update),
            (RbacPermissionScreen.Roles, RbacPermissionAction.Delete)));
        var iamRolesList = await iamRolesClient.GetAsync("/api/iam/roles?pageNumber=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(iamRolesList);

        using var accountCompaniesClient = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));
        var accountCompaniesList = await accountCompaniesClient.GetAsync("/api/account/companies?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstItemHasAllowedActionsAsync(accountCompaniesList);
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
    public async Task Reports_Capabilities_ShouldReturnPersonnelFilesCapabilities()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/reports/capabilities?resource={PersonnelFilePermissionCodes.ResourceKey}");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ReportCapabilitiesItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(PersonnelFilePermissionCodes.ResourceKey, payload!.ResourceKey);
        Assert.True(payload.SupportsPrint);
        Assert.True(payload.SupportsExport);
        Assert.Contains("csv", payload.SupportedTableFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("xlsx", payload.SupportedTableFormats, StringComparer.OrdinalIgnoreCase);
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
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, scenario.TenantId, "Direccion");

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-001",
            name = "Direccion General",
            orgUnitTypePublicId = orgUnitType.Id,
            functionalAreaPublicId = (Guid?)null,
            parentPublicId = (Guid?)null,
            sortOrder = 1,
            description = "Direccion principal",
            costCenterCode = (string?)null,
            managerEmployeePublicId = (Guid?)null
        });
        createResponse.EnsureSuccessStatusCode();

        var auditResponse = await client.GetAsync("/api/audit/logs?page=1&pageSize=50");
        auditResponse.EnsureSuccessStatusCode();

        var payload = await auditResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, static item => item.EventType == "ORG_UNIT_CREATED" && item.EntityType == "OrgUnit");
    }

    [Fact]
    public async Task JobProfiles_FullFlow_ShouldCreateUpdateAndPublish()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-JP-001", "Direccion Nomina", "Direccion");

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-001",
            title = "Analista de Nomina",
            objective = "Garantizar el proceso de nomina.",
            orgUnitPublicId = orgUnit.Id,
            reportsToJobProfilePublicId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo RRHH",
            responsibilities = "Ejecutar nomina mensual.",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado local",
            valuationNotes = "Valuado 2026",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(JobProfileStatus.Draft, created!.Status);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/job-profiles/{created.Id}")
        {
            Content = JsonContent.Create(new
            {
                code = "JP-001",
                title = "Analista de Nomina Senior",
                objective = "Garantizar el proceso de nomina.",
                orgUnitPublicId = orgUnit.Id,
                reportsToJobProfilePublicId = (Guid?)null,
                decisionScope = "Operacion",
                assignedResources = "Equipo RRHH",
                responsibilities = "Ejecutar nomina mensual.",
                benefitsSummary = "Ley",
                workingConditionSummary = "Presencial",
                marketSalaryReference = "Mercado local",
                valuationNotes = "Valuado 2026",
                effectiveFromUtc = (DateTime?)null,
                effectiveToUtc = (DateTime?)null,
                allowInlineCatalogCreate = false
            })
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");
        var updateResponse = await client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Analista de Nomina Senior", updated!.Title);

        var functionResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{updated.Id}/functions", new
        {
            functionType = "General",
            description = "Gestionar proceso completo de planilla",
            sortOrder = 1
        });
        functionResponse.EnsureSuccessStatusCode();
        var addedFunction = await functionResponse.Content.ReadFromJsonAsync<JobProfileFunctionResponse>(JsonOptions);
        
        var reqResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{updated.Id}/requirements", new
        {
            requirementType = "Experience",
            description = "3 anios de experiencia",
            sortOrder = 1
        });
        reqResponse.EnsureSuccessStatusCode();

        var refetchResponse = await client.GetAsync($"/api/v1/job-profiles/{updated.Id}");
        refetchResponse.EnsureSuccessStatusCode();
        var refetched = await refetchResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);

        using var publishRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/job-profiles/{updated.Id}")
        {
            Content = new StringContent(
                "[{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"Published\"}]",
                Encoding.UTF8,
                "application/json-patch+json")
        };
        publishRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{refetched!.ConcurrencyToken}\"");
        var publishResponse = await client.SendAsync(publishRequest);
        publishResponse.EnsureSuccessStatusCode();

        var published = await publishResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(published);
        Assert.Equal(JobProfileStatus.Published, published!.Status);
    }

    [Fact]
    public async Task JobProfiles_Create_ShouldReturnLocationHeaderPointingToGetById()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var response = await PostJobProfileAsync(
            client,
            scenario.TenantId,
            code: "JP-LOC",
            title: "Perfil con Location header");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.NotNull(response.Headers.Location);

        var expectedPath = $"/api/v1/job-profiles/{payload!.Id}";
        var actualPath = response.Headers.Location!.IsAbsoluteUri
            ? response.Headers.Location.AbsolutePath
            : response.Headers.Location.OriginalString;
        Assert.Equal(expectedPath, actualPath, ignoreCase: true);

        var followUp = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, followUp.StatusCode);
    }

    [Fact]
    public async Task JobProfiles_Create_WithCompensationWithoutActiveTabulatorLine_ShouldReturn404()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        var missingLineId = Guid.NewGuid();

        var profile = await CreateJobProfileAsync(
            client,
            scenario.TenantId,
            code: "JP-COMP-NOLINE",
            title: "Perfil sin tabulador activo");

        var response = await client.PostJsonAsync(
            $"/api/v1/job-profiles/{profile.Id}/compensations",
            new { salaryTabulatorLinePublicId = missingLineId });

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "JOB_PROFILE_COMPENSATION_SALARY_TABULATOR_LINE_NOT_FOUND");
    }

    [Fact]
    public async Task JobProfiles_Create_WithCompensation_ShouldResolveCanonicalTabulatorLine()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));
        using var jobProfileClient = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var salaryClass = await EnsureSalaryClassAsync(requesterClient, scenario.TenantId, "CLS-JP-CANON");
        var createdRequest = await CreateSalaryTabulatorRequestAsync(requesterClient, scenario.TenantId, "CLS-JP-CANON", "S1", 1850m);

        var submitResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/submit", new
        {
            concurrencyToken = createdRequest.ConcurrencyToken
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submitted);

        var approveResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/approve", new
        {
            decisionComment = "Aprobado para compensaciones de perfil",
            concurrencyToken = submitted!.ConcurrencyToken
        });
        approveResponse.EnsureSuccessStatusCode();

        var salaryTabulatorLineId = await GetSalaryTabulatorLineIdAsync(scenario.TenantId, salaryClass.Id, "S1");

        var profile = await CreateJobProfileAsync(
            jobProfileClient,
            scenario.TenantId,
            code: "JP-COMP-OK",
            title: "Perfil con compensacion canonical");

        var compensationResponse = await jobProfileClient.PostJsonAsync(
            $"/api/v1/job-profiles/{profile.Id}/compensations",
            new { salaryTabulatorLinePublicId = salaryTabulatorLineId });
        if (!compensationResponse.IsSuccessStatusCode) { Console.WriteLine("ERROR: " + await compensationResponse.Content.ReadAsStringAsync()); compensationResponse.EnsureSuccessStatusCode(); }

        var detailResponse = await jobProfileClient.GetAsync($"/api/v1/job-profiles/{profile.Id}");
        detailResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profileEntity = await dbContext.JobProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.PublicId == profile.Id);
            var compensationEntity = await dbContext.JobProfileCompensations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.JobProfileId == profileEntity.Id);

            var activeLine = await dbContext.SalaryTabulatorLines
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(line =>
                    line.TenantId == scenario.TenantId &&
                    line.NormalizedSalaryClassCode == salaryClass.Code &&
                    line.NormalizedSalaryScaleCode == "S1" &&
                    line.IsActive);
            Assert.NotNull(activeLine);
            Assert.Equal(activeLine!.Id, compensationEntity.SalaryTabulatorLineId);
            Assert.Equal(salaryClass.Code, activeLine.SalaryClassCode);
            Assert.Equal(salaryClass.Code, activeLine.NormalizedSalaryClassCode);
            Assert.Equal("S1", activeLine.SalaryScaleCode);
            Assert.Equal("S1", activeLine.NormalizedSalaryScaleCode);
            Assert.Equal(1850m, activeLine!.BaseAmount);
        }
    }

    [Fact]
    public async Task JobProfiles_Compensation_WithCanonicalTabulatorLine_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));
        using var jobProfileClient = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(jobProfileClient, scenario.TenantId, "DIR-LEGACY", "Unidad Legacy", "Direccion");
        var positionCategory = await EnsureDefaultPositionCategoryAsync(jobProfileClient, scenario.TenantId);
        var salaryClass = await EnsureSalaryClassAsync(requesterClient, scenario.TenantId, "CLS-JP-LEGACY");

        var requestResponse = await requesterClient.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/change-requests", new
        {
            reason = "Compatibilidad request legado",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassPublicId = salaryClass.Id,
                    salaryScaleCode = "S1",
                    currencyCode = "USD",
                    changeType = "Create",
                    proposedBaseAmount = 4000m,
                    proposedMinAmount = 3000m,
                    proposedMaxAmount = 5000m,
                    notes = "Linea para compatibilidad legacy"
                }
            }
        });
        requestResponse.EnsureSuccessStatusCode();
        var createdRequest = await requestResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(createdRequest);

        var submitResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest!.Id}/submit", new
        {
            concurrencyToken = createdRequest.ConcurrencyToken
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submitted);

        var approveResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/approve", new
        {
            decisionComment = "Aprobado para request legado",
            concurrencyToken = submitted!.ConcurrencyToken
        });
        approveResponse.EnsureSuccessStatusCode();

        var listLinesResponse = await approverClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/lines?salaryClassPublicId={salaryClass.Id}&page=1&pageSize=20");
        listLinesResponse.EnsureSuccessStatusCode();
        var linesPayload = await listLinesResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<SalaryTabulatorLineItem>>(JsonOptions);
        Assert.NotNull(linesPayload);
        var approvedLine = Assert.Single(linesPayload!.Items, line => line.BaseAmount == 4000m);

        var createdProfile = await CreateJobProfileAsync(
            jobProfileClient,
            scenario.TenantId,
            code: "JP-LEGACY",
            title: "Perfil legado",
            orgUnitPublicId: orgUnit.Id);

        var updateResponse = await jobProfileClient.PutJsonAsync($"/api/v1/job-profiles/{createdProfile.Id}", new
        {
            code = "JP-LEGACY",
            title = "Perfil legado actualizado",
            objective = "Objetivo",
            orgUnitPublicId = orgUnit.Id,
            reportsToJobProfilePublicId = (Guid?)null,
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
            concurrencyToken = createdProfile.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();

        var compResponse = await jobProfileClient.PostJsonAsync($"/api/v1/job-profiles/{createdProfile.Id}/compensations", new
        {
            salaryTabulatorLinePublicId = approvedLine.Id,
            notes = "Notas"
        });
        compResponse.EnsureSuccessStatusCode();

        var compListResponse = await jobProfileClient.GetAsync($"/api/v1/job-profiles/{createdProfile.Id}/compensations");
        compListResponse.EnsureSuccessStatusCode();

        var compList = await compListResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<JobProfileCompensationItemResponse>>(JsonOptions);
        Assert.NotNull(compList);
        var compensation = Assert.Single(compList!);
        Assert.Equal(approvedLine.Id, compensation.SalaryTabulatorLinePublicId);
        Assert.Equal("USD", compensation.CurrencyCode);
        Assert.Equal(3000m, compensation.MinAmount);
        Assert.Equal(5000m, compensation.MaxAmount);

        var detailResponse = await jobProfileClient.GetAsync($"/api/v1/job-profiles/{createdProfile.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<JobProfileEntityItem>(JsonOptions);
        var detailJson = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        Assert.NotNull(detail);
        Assert.Equal(createdProfile.Id, detail!.Id);
        Assert.DoesNotContain(detailJson.RootElement.EnumerateObject(), static property => string.Equals(property.Name, "compensation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task JobProfiles_Compensation_WithTabulatorRangeOverlappingProfileRange_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));
        using var jobProfileClient = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(jobProfileClient, scenario.TenantId, "DIR-RANGE", "Unidad Rango", "Direccion");
        var positionCategory = await EnsureDefaultPositionCategoryAsync(jobProfileClient, scenario.TenantId);
        var salaryClass = await EnsureSalaryClassAsync(requesterClient, scenario.TenantId, "CLS-RANGE");

        var requestResponse = await requesterClient.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/change-requests", new
        {
            reason = "Linea que se cruza con vigencia del perfil",
            effectiveFromUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            items = new[]
            {
                new
                {
                    salaryClassPublicId = salaryClass.Id,
                    salaryScaleCode = "S1",
                    currencyCode = "USD",
                    changeType = "Create",
                    proposedBaseAmount = 4000m,
                    proposedMinAmount = 3000m,
                    proposedMaxAmount = 5000m,
                    notes = "Linea para rango de perfil"
                }
            }
        });
        requestResponse.EnsureSuccessStatusCode();
        var createdRequest = await requestResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(createdRequest);

        var submitResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest!.Id}/submit", new
        {
            concurrencyToken = createdRequest.ConcurrencyToken
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submitted);

        var approveResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/approve", new
        {
            decisionComment = "Aprobado para rango",
            concurrencyToken = submitted!.ConcurrencyToken
        });
        approveResponse.EnsureSuccessStatusCode();

        var listLinesResponse = await approverClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/lines?salaryClassPublicId={salaryClass.Id}&page=1&pageSize=20");
        listLinesResponse.EnsureSuccessStatusCode();
        var linesPayload = await listLinesResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<SalaryTabulatorLineItem>>(JsonOptions);
        Assert.NotNull(linesPayload);
        var approvedLine = Assert.Single(linesPayload!.Items, line => line.BaseAmount == 4000m);

        var createdProfile = await CreateJobProfileAsync(
            jobProfileClient,
            scenario.TenantId,
            code: "JP-RANGE",
            title: "Perfil con rango",
            orgUnitPublicId: orgUnit.Id);

        var updateResponse = await jobProfileClient.PutJsonAsync($"/api/v1/job-profiles/{createdProfile.Id}", new
        {
            code = "JP-RANGE",
            title = "Perfil con rango",
            objective = "General - objetivo",
            orgUnitPublicId = orgUnit.Id,
            reportsToJobProfilePublicId = (Guid?)null,
            positionCategoryPublicId = positionCategory.Id,
            strategicObjectiveCatalogItemPublicId = (Guid?)null,
            assignedWorkEquipmentCatalogItemPublicId = (Guid?)null,
            responsibilityCatalogItemPublicId = (Guid?)null,
            effectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveToUtc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            decisionScope = "General - alcance",
            assignedResources = "General - recursos",
            responsibilities = "General - responsabilidades",
            benefitsSummary = "Beneficios",
            workingConditionSummary = (string?)null,
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            allowInlineCatalogCreate = false,
            concurrencyToken = createdProfile.ConcurrencyToken
        });

        updateResponse.EnsureSuccessStatusCode();

        var compResponse = await jobProfileClient.PostJsonAsync($"/api/v1/job-profiles/{createdProfile.Id}/compensations", new
        {
            salaryTabulatorLinePublicId = approvedLine.Id,
            notes = "Notas"
        });
        compResponse.EnsureSuccessStatusCode();

        var compListResponse = await jobProfileClient.GetAsync($"/api/v1/job-profiles/{createdProfile.Id}/compensations");
        compListResponse.EnsureSuccessStatusCode();

        var compList = await compListResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<JobProfileCompensationItemResponse>>(JsonOptions);
        Assert.NotNull(compList);
        var compensation = Assert.Single(compList!);
        Assert.Equal(approvedLine.Id, compensation.SalaryTabulatorLinePublicId);
        Assert.Equal("S1", compensation.SalaryScaleCode);
    }

    [Fact]
    public async Task SalaryTabulator_Approve_WhenInactivationLeavesJobProfileCompensationUncovered_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));
        using var jobProfileClient = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var salaryClass = await EnsureSalaryClassAsync(requesterClient, scenario.TenantId, "CLS-JP-COVERAGE");
        var createdRequest = await CreateSalaryTabulatorRequestAsync(requesterClient, scenario.TenantId, "CLS-JP-COVERAGE", "S1", 1600m);

        var submitCreateResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/submit", new
        {
            concurrencyToken = createdRequest.ConcurrencyToken
        });
        submitCreateResponse.EnsureSuccessStatusCode();
        var submittedCreate = await submitCreateResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submittedCreate);

        var approveCreateResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/approve", new
        {
            decisionComment = "Linea base aprobada",
            concurrencyToken = submittedCreate!.ConcurrencyToken
        });
        approveCreateResponse.EnsureSuccessStatusCode();

        var salaryTabulatorLineId = await GetSalaryTabulatorLineIdAsync(scenario.TenantId, salaryClass.Id, "S1");

        var createdProfile = await CreateJobProfileAsync(
            jobProfileClient,
            scenario.TenantId,
            code: "JP-COMP-COVERAGE",
            title: "Perfil protegido por cobertura");

        var compensationResponse = await jobProfileClient.PostJsonAsync(
            $"/api/v1/job-profiles/{createdProfile.Id}/compensations",
            new { salaryTabulatorLinePublicId = salaryTabulatorLineId });
        if (!compensationResponse.IsSuccessStatusCode) { Console.WriteLine("ERROR: " + await compensationResponse.Content.ReadAsStringAsync()); compensationResponse.EnsureSuccessStatusCode(); }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profileEntity = await dbContext.JobProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.PublicId == createdProfile.Id);
            var compensationEntity = await dbContext.JobProfileCompensations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.JobProfileId == profileEntity.Id);
            var linkedLine = await dbContext.SalaryTabulatorLines
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.Id == compensationEntity.SalaryTabulatorLineId);
            Assert.False(string.IsNullOrWhiteSpace(linkedLine.SalaryClassCode));
            Assert.Equal("S1", linkedLine.NormalizedSalaryScaleCode);
        }

        var inactivateRequestResponse = await requesterClient.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/change-requests", new
        {
            reason = "Retiro de linea",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassPublicId = salaryClass.Id,
                    salaryScaleCode = "S1",
                    currencyCode = "USD",
                    changeType = "Inactivate",
                    proposedBaseAmount = (decimal?)null,
                    proposedMinAmount = (decimal?)null,
                    proposedMaxAmount = (decimal?)null,
                    notes = "intentando cerrar cobertura"
                }
            }
        });
        inactivateRequestResponse.EnsureSuccessStatusCode();
        var inactivateRequest = await inactivateRequestResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(inactivateRequest);

        var submitInactivateResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{inactivateRequest!.Id}/submit", new
        {
            concurrencyToken = inactivateRequest.ConcurrencyToken
        });
        submitInactivateResponse.EnsureSuccessStatusCode();
        var submittedInactivate = await submitInactivateResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submittedInactivate);

        var approveInactivateResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{inactivateRequest.Id}/approve", new
        {
            decisionComment = "Intento de inactivacion",
            concurrencyToken = submittedInactivate!.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(
            approveInactivateResponse,
            HttpStatusCode.Conflict,
            "SALARY_TABULATOR_JOB_PROFILE_COVERAGE_CONFLICT");
    }


    [Fact]
    public async Task JobProfiles_GetById_ShouldReturnEntityOnly_WhenDependentPositionReferencesProfileOutsideTenantScope()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var adminClient = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        var profile = await CreateJobProfileAsync(adminClient, scenario.TenantId, "JP-DET", "Perfil Detalle");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profileEntity = await dbContext.JobProfiles
                .IgnoreQueryFilters()
                .SingleAsync(item => item.PublicId == profile.Id);

            var externalDependentProfile = JobProfile.Create("JP-EXT", "Perfil Externo");
            externalDependentProfile.SetTenantId(scenario.OtherTenantId);
            externalDependentProfile.UpdateCore(
                "JP-EXT",
                "Perfil Externo",
                objective: null,
                orgUnitId: profileEntity.OrgUnitId,
                reportsToJobProfileId: null,
                positionCategoryId: null,
                strategicObjectiveCatalogItemId: null,
                assignedWorkEquipmentCatalogItemId: null,
                responsibilityCatalogItemId: null,
                decisionScope: null,
                assignedResources: null,
                responsibilities: null,
                benefitsSummary: null,
                workingConditionSummary: null,
                marketSalaryReference: null,
                valuationNotes: null,
                effectiveFromUtc: null,
                effectiveToUtc: null,
                bumpVersion: false);
            dbContext.JobProfiles.Add(externalDependentProfile);
            await dbContext.SaveChangesAsync();

            var dependentPosition = JobProfileDependentPosition.Create(externalDependentProfile.Id, 1, "Legacy external dependency");
            dependentPosition.SetTenantId(profileEntity.TenantId);

            profileEntity.ReplaceDependentPositions([dependentPosition]);
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClientFor(CreateJobProfileReadContext(scenario));
        var response = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JobProfileEntityItem>(JsonOptions);
        var payloadJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(payload);
        Assert.Equal(profile.Id, payload!.Id);
        Assert.DoesNotContain(payloadJson.RootElement.EnumerateObject(), static property => string.Equals(property.Name, "dependentPositions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task JobProfiles_Search_WithoutPermissionClaim_ShouldReturn403FromPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "JOB_PROFILES_FORBIDDEN");
    }

    [Fact]
    public async Task JobProfiles_Create_WithReadOnlyPermissionClaim_ShouldReturn403FromPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileReadContext(scenario));

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-POLICY",
            title = "Should not be created",
            objective = "n/a",
            orgUnitPublicId = Guid.NewGuid(),
            decisionScope = "n/a",
            assignedResources = "n/a",
            responsibilities = "n/a",
            benefitsSummary = "n/a",
            workingConditionSummary = "n/a",
            marketSalaryReference = "n/a",
            valuationNotes = "n/a",
            allowInlineCatalogCreate = false
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "JOB_PROFILES_FORBIDDEN");
    }

    [Fact]
    public async Task JobProfileFunctions_Add_WithoutPermissionClaim_ShouldReturn403FromPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.PostJsonAsync($"/api/v1/job-profiles/{Guid.NewGuid()}/functions", new
        {
            functionType = "General",
            description = "Should not be added",
            sortOrder = 1,
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "JOB_PROFILES_FORBIDDEN");
    }

    [Fact]
    public async Task JobProfiles_GetById_Unauthenticated_ShouldReturn401WithProblemDetails()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/job-profiles/{Guid.NewGuid()}");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
    }

    [Fact]
    public async Task PositionCategories_List_Unauthenticated_ShouldReturn401WithProblemDetails()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/position-categories?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
    }

    [Fact]
    public async Task PositionCategories_List_WithoutPermissionClaim_ShouldReturn403FromPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/position-categories?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "POSITION_DESCRIPTION_CATALOG_FORBIDDEN");
    }

    [Fact]
    public async Task PositionCategories_List_WithReadPermission_ShouldPassPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionCatalogReadContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/position-categories?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PositionCategories_Add_WithReadOnlyPermissionClaim_ShouldReturn403FromPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionCatalogReadContext(scenario));

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-categories", new
        {
            code = "PC-POLICY",
            name = "Should not be created",
            description = (string?)null,
            classificationPublicId = Guid.NewGuid(),
            sortOrder = 1
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "POSITION_DESCRIPTION_CATALOG_FORBIDDEN");
    }

    [Fact]
    public async Task PositionCategoryClassifications_List_WithoutPermissionClaim_ShouldReturn403FromPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/position-category-classifications?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "POSITION_DESCRIPTION_CATALOG_FORBIDDEN");
    }

    [Fact]
    public async Task PositionDescriptionCatalogItems_List_WithoutPermissionClaim_ShouldReturn403FromPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-description-catalogs/position-function-types/items?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "POSITION_DESCRIPTION_CATALOG_FORBIDDEN");
    }

    [Fact]
    public async Task PositionDescriptionCatalogItems_List_WithReadPermission_ShouldPassPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionCatalogReadContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-description-catalogs/position-function-types/items?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task JobProfiles_GetById_WithReadPermission_ShouldPassPolicy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var adminClient = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        var profile = await CreateJobProfileAsync(adminClient, scenario.TenantId, "JP-POL-OK", "Perfil Policy OK");

        using var readerClient = factory.CreateClientFor(CreateJobProfileReadContext(scenario));
        var response = await readerClient.GetAsync($"/api/v1/job-profiles/{profile.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task JobProfiles_GetById_WithIfNoneMatch_ShouldReturn304WhenETagMatches()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-ETAG", "Perfil ETag");

        var getResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.True(getResponse.Headers.ETag is not null);
        Assert.True(getResponse.Content.Headers.LastModified is not null);

        using var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/job-profiles/{profile.Id}");
        conditionalRequest.Headers.IfNoneMatch.Add(getResponse.Headers.ETag);

        var conditionalResponse = await client.SendAsync(conditionalRequest);

        Assert.Equal(HttpStatusCode.NotModified, conditionalResponse.StatusCode);
        Assert.Equal(getResponse.Headers.ETag, conditionalResponse.Headers.ETag);
    }

    [Fact]
    public async Task JobProfiles_Update_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-001", "Analista");

        var response = await client.PutJsonAsync($"/api/v1/job-profiles/{profile.Id}", new
        {
            code = "JP-001",
            title = "Analista Actualizado",
            objective = "Objetivo",
            orgUnitPublicId = profile.OrgUnitId,
            reportsToJobProfilePublicId = (Guid?)null,
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
                    catalogItemPublicId = (Guid?)null,
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
            compensation = (object?)null,
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>(),
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task JobProfiles_Update_WhenDraftCanRemainIncomplete_ShouldReturn200()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-DRAFT", "Perfil Draft");

        var response = await client.PutJsonAsync($"/api/v1/job-profiles/{profile.Id}", new
        {
            code = profile.Code,
            title = profile.Title,
            objective = (string?)null,
            orgUnitPublicId = profile.OrgUnitId,
            reportsToJobProfilePublicId = (Guid?)null,
            positionCategoryPublicId = (Guid?)null,
            strategicObjectiveCatalogItemPublicId = (Guid?)null,
            assignedWorkEquipmentCatalogItemPublicId = (Guid?)null,
            responsibilityCatalogItemPublicId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = (string?)null,
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = Array.Empty<object>(),
            functions = Array.Empty<object>(),
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensation = (object?)null,
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>(),
            concurrencyToken = profile.ConcurrencyToken
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(JobProfileStatus.Draft, payload!.Status);
    }

    [Fact]
    public async Task JobProfiles_Update_WithStatusInBody_ShouldKeepExistingStatus()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-STATUS-PUT", "Perfil Status PUT");

        var response = await client.PutJsonAsync($"/api/v1/job-profiles/{profile.Id}", new
        {
            code = profile.Code,
            title = "Perfil Status PUT Actualizado",
            status = JobProfileStatus.Published,
            objective = "Objetivo",
            orgUnitPublicId = profile.OrgUnitId,
            reportsToJobProfilePublicId = (Guid?)null,
            positionCategoryPublicId = (Guid?)null,
            strategicObjectiveCatalogItemPublicId = (Guid?)null,
            assignedWorkEquipmentCatalogItemPublicId = (Guid?)null,
            responsibilityCatalogItemPublicId = (Guid?)null,
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
                    catalogItemPublicId = (Guid?)null,
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
            compensation = (object?)null,
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>(),
            concurrencyToken = profile.ConcurrencyToken
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Perfil Status PUT Actualizado", payload!.Title);
        Assert.Equal(JobProfileStatus.Draft, payload.Status);

        var detailResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<JobProfileEntityItem>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(JobProfileStatus.Draft, detail!.Status);
    }

    [Fact]
    public async Task JobProfiles_Publish_WhenMinimumRequirementsMissing_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-PUB-UPD", "Perfil Publicado");

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/job-profiles/{profile.Id}")
        {
            Content = new StringContent(
                "[{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"Published\"}]",
                Encoding.UTF8,
                "application/json-patch+json")
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{profile.ConcurrencyToken}\"");

        var response = await client.SendAsync(request);

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING");
    }

    [Fact]
    public async Task JobProfiles_Update_WithReportsToAlsoAsDependentPosition_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var parent = await CreateJobProfileAsync(client, scenario.TenantId, "JP-PARENT", "Gerente General");
        var child = await CreateJobProfileAsync(client, scenario.TenantId, "JP-CHILD", "Analista");

        var updateResponse = await client.PutJsonAsync($"/api/v1/job-profiles/{child.Id}", new
        {
            code = child.Code,
            title = child.Title,
            objective = "General - objetivo",
            orgUnitPublicId = child.OrgUnitId,
            reportsToJobProfilePublicId = parent.Id,
            decisionScope = "General - alcance de decision",
            assignedResources = "General - recursos asignados",
            responsibilities = "General - responsabilidades",
            benefitsSummary = "Resumen del beneficio",
            marketSalaryReference = "General - referencia salarial de mercado",
            valuationNotes = "General - Notas de evaluacion",
            allowInlineCatalogCreate = false,
            concurrencyToken = child.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();

        // Try to add parent as dependent position
        var response = await client.PostJsonAsync($"/api/v1/job-profiles/{child.Id}/dependent-positions", new
        {
            dependentJobProfilePublicId = parent.Id,
            quantity = 1,
            notes = "Posiciones dependientes - notas"
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "JOB_PROFILE_DEPENDENCY_CYCLE");
    }

    [Fact]
    public async Task JobProfiles_UpdateDraft_WithDateOnlyEffectiveRange_ShouldPersistUtcDates()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-DATE", "Perfil Fechas");
        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-DATE", "Departamento Fechas", "Direccion");
        var positionCategory = await EnsureDefaultPositionCategoryAsync(client, scenario.TenantId);

        var response = await client.PutJsonAsync($"/api/v1/job-profiles/{profile.Id}", new
        {
            code = "JP-DATE",
            title = "Perfil Fechas",
            objective = "General - objetivo",
            orgUnitId = orgUnit.Id,
            reportsToJobProfileId = (Guid?)null,
            positionCategoryId = positionCategory.Id,
            strategicObjectiveCatalogItemId = (Guid?)null,
            assignedWorkEquipmentCatalogItemId = (Guid?)null,
            responsibilityCatalogItemId = (Guid?)null,
            effectiveFromUtc = "2026-02-04",
            effectiveToUtc = "2026-08-31",
            decisionScope = "General - alcance",
            assignedResources = "General - recursos",
            responsibilities = "General - responsabilidades",
            benefitsSummary = "Beneficios",
            workingConditionSummary = (string?)null,
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            requirements = new[]
            {
                new
                {
                    requirementType = "Education",
                    requirementTypeCatalogItemId = (Guid?)null,
                    catalogItemId = (Guid?)null,
                    description = "Requisitos - descripcion",
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    frequencyCatalogItemId = (Guid?)null,
                    description = "Funciones - Descripcion",
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

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await dbContext.JobProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.PublicId == profile.Id);

        Assert.Equal(DateTimeKind.Utc, persisted.EffectiveFromUtc!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, persisted.EffectiveToUtc!.Value.Kind);
        Assert.Equal(new DateTime(2026, 2, 4, 0, 0, 0, DateTimeKind.Utc), persisted.EffectiveFromUtc);
        Assert.Equal(new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), persisted.EffectiveToUtc);
    }

    [Fact]
    public async Task JobProfiles_Create_WithoutOrgUnit_ShouldReturn400Validation()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-NO-ORG",
            title = "Perfil sin unidad",
            objective = "Objetivo",
            orgUnitPublicId = Guid.Empty,
            reportsToJobProfilePublicId = (Guid?)null,
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
            requirements = Array.Empty<object>(),
            functions = Array.Empty<object>(),
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensation = (object?)null,
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "common.validation");
    }

    [Fact]
    public async Task JobProfiles_Create_WithDuplicateCode_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-DUP-JP", "Direccion Duplicados", "Direccion");

        _ = await CreateJobProfileAsync(client, scenario.TenantId, "JP-001", "Analista");

        var duplicateResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-001",
            title = "Analista 2",
            objective = "Objetivo",
            orgUnitPublicId = orgUnit.Id,
            reportsToJobProfilePublicId = (Guid?)null,
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
                    catalogItemPublicId = (Guid?)null,
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
            compensation = (object?)null,
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>()
        });

        await AssertProblemDetailsAsync(duplicateResponse, HttpStatusCode.Conflict, "JOB_PROFILE_CODE_CONFLICT");
    }

    [Fact]
    public async Task JobProfiles_Create_WithInvalidDependentPositionIdentifier_ShouldReturnStandardValidationProblem()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        
        var profile = await CreateJobProfileAsync(
            client,
            scenario.TenantId,
            code: "JP-VAL-001",
            title: "Perfil invalido");

        var response = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/dependent-positions", new
        {
            dependentJobProfilePublicId = "not-a-guid",
            quantity = 1,
            notes = "dep"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://httpstatuses.com/400", document.RootElement.GetProperty("type").GetString());
        var title = document.RootElement.GetProperty("title").GetString();
        var detail = document.RootElement.GetProperty("detail").GetString();
        var expectedValidationMessages = new[]
        {
            "One or more validation errors occurred.",
            "Se encontraron uno o más errores de validación."
        };
        Assert.Contains(title, expectedValidationMessages);
        Assert.Contains(detail, expectedValidationMessages);
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("common.validation", document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));

        var errors = document.RootElement.GetProperty("errors");
        Assert.False(errors.TryGetProperty("request", out _));
        Assert.False(errors.TryGetProperty("body", out _));
        Assert.False(errors.TryGetProperty("$.dependentJobProfilePublicId", out _));

        var fieldErrors = errors.GetProperty("dependentJobProfilePublicId");
        Assert.Contains(
            fieldErrors.EnumerateArray().Select(static item => item.GetString()),
            static message => message is "The value must be a valid UUID." or "El valor debe ser un UUID válido.");
    }

    [Fact]
    public async Task JobProfiles_Update_WhenDependencyCycleDetected_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profileA = await CreateJobProfileAsync(client, scenario.TenantId, "JP-A", "Perfil A");
        var profileB = await CreateJobProfileAsync(client, scenario.TenantId, "JP-B", "Perfil B");

        var updateAResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profileA.Id}/dependent-positions", new
        {
            dependentJobProfilePublicId = profileB.Id,
            quantity = 1,
            notes = "dep"
        });
        updateAResponse.EnsureSuccessStatusCode();

        var updateBResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profileB.Id}/dependent-positions", new
        {
            dependentJobProfilePublicId = profileA.Id,
            quantity = 1,
            notes = "dep"
        });

        await AssertProblemDetailsAsync(updateBResponse, HttpStatusCode.Conflict, "JOB_PROFILE_DEPENDENCY_CYCLE");
        using var cycleDocument = JsonDocument.Parse(await updateBResponse.Content.ReadAsStringAsync());
        const string expectedCycleMessage = "This change cannot be saved because it would make the selected job profiles depend on each other in a circular way. Review the reporting profile and dependent positions, then try again.";
        Assert.Equal(expectedCycleMessage, cycleDocument.RootElement.GetProperty("title").GetString());
        Assert.Equal(expectedCycleMessage, cycleDocument.RootElement.GetProperty("detail").GetString());
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
    public async Task JobProfiles_List_WithPageSizeAboveMax_ShouldReturn400()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileReadContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/job-profiles?page=1&pageSize={JobProfileValidationRules.MaxPageSize + 1}");

        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "common.validation");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");
        var fieldErrors = errors.GetProperty("pageSize");
        Assert.NotEmpty(fieldErrors.EnumerateArray());
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
    public async Task JobProfiles_Create_WithAuditPermission_ShouldWriteAuditEvent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminWithAuditContext(scenario));
        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-AUD-JP", "Direccion Audit", "Direccion");

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-001",
            title = "Analista",
            objective = "Objetivo",
            orgUnitPublicId = orgUnit.Id,
            reportsToJobProfilePublicId = (Guid?)null,
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
                    catalogItemPublicId = (Guid?)null,
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
            compensation = (object?)null,
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

    [Theory]
    [MemberData(nameof(JobProfilePatchEndpointTemplates))]
    public async Task JobProfilePatchEndpoints_WithJsonContentType_ShouldReturn415(string endpointTemplate)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var endpoint = endpointTemplate
            .Replace("{profileId}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal)
            .Replace("{resourceId}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal);

        using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new[]
                {
                    new { op = "replace", path = "/concurrencyToken", value = Guid.NewGuid().ToString("D") }
                }),
                Encoding.UTF8,
                "application/json")
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task JobProfileFunctions_CrudFlow_ShouldReturnEntityContracts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-FN-001", "Perfil Funciones");

        var addResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/functions", new
        {
            functionType = "General",
            frequencyCatalogItemPublicId = (Guid?)null,
            description = "Planificar entregables",
            sortOrder = 1
        });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var addBody = await addResponse.Content.ReadAsStringAsync();
        var addJson = JsonDocument.Parse(addBody);
        Assert.False(addJson.RootElement.TryGetProperty("item", out _));
        Assert.False(addJson.RootElement.TryGetProperty("code", out _));
        var created = JsonSerializer.Deserialize<JobProfileFunctionResponse>(addBody, JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Planificar entregables", created!.Description);
        Assert.Equal(JobFunctionType.General, created.FunctionType);
        Assert.NotEqual(Guid.Empty, created.ConcurrencyToken);

        var functionId = created.FunctionPublicId;

        var getResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}/functions?page=1&pageSize=1");
        getResponse.EnsureSuccessStatusCode();
        var listed = await getResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<JobProfileFunctionResponse>>(JsonOptions);
        Assert.NotNull(listed);
        Assert.Equal(1, listed!.PageNumber);
        Assert.Equal(1, listed.PageSize);
        Assert.Equal(1, listed.TotalCount);
        var fromList = Assert.Single(listed.Items);
        Assert.Equal(functionId, fromList.FunctionPublicId);

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/job-profiles/{profile.Id}/functions/{functionId}")
        {
            Content = JsonContent.Create(new
            {
                functionType = "Specific",
                frequencyCatalogItemPublicId = (Guid?)null,
                description = "Planificar entregables y roadmap",
                sortOrder = 2
            })
        };
        putRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");
        var putResponse = await client.SendAsync(putRequest);
        putResponse.EnsureSuccessStatusCode();
        var updated = await putResponse.Content.ReadFromJsonAsync<JobProfileFunctionResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Planificar entregables y roadmap", updated!.Description);
        Assert.Equal(JobFunctionType.Specific, updated.FunctionType);
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);

        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/job-profiles/{profile.Id}/functions/{functionId}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new[]
                {
                    new { op = "replace", path = "/sortOrder", value = (object)3 }
                }),
                Encoding.UTF8,
                "application/json-patch+json")
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{updated.ConcurrencyToken}\"");
        var patchResponse = await client.SendAsync(patchRequest);
        patchResponse.EnsureSuccessStatusCode();
        var patched = await patchResponse.Content.ReadFromJsonAsync<JobProfileFunctionResponse>(JsonOptions);
        Assert.NotNull(patched);
        Assert.Equal(3, patched!.SortOrder);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/job-profiles/{profile.Id}/functions/{functionId}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{patched.ConcurrencyToken}\"");
        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<JobProfileParentConcurrencyResult>(JsonOptions);
        Assert.NotNull(deleteResult);
        Assert.NotEqual(Guid.Empty, deleteResult!.ParentConcurrencyToken);
    }

    [Fact]
    public async Task JobProfileFunctions_Delete_WithoutIfMatchHeader_ShouldReturn400()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-FN-IM", "Perfil If-Match");

        var addResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/functions", new
        {
            functionType = "General",
            frequencyCatalogItemPublicId = (Guid?)null,
            description = "Tarea efímera",
            sortOrder = 1
        });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var created = await addResponse.Content.ReadFromJsonAsync<JobProfileFunctionResponse>(JsonOptions);
        Assert.NotNull(created);

        var deleteResponse = await client.DeleteAsync($"/api/v1/job-profiles/{profile.Id}/functions/{created!.FunctionPublicId}");

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task JobProfileFunctions_Delete_WithQuotedIfMatchHeader_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-FN-Q", "Perfil If-Match Quoted");

        var addResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/functions", new
        {
            functionType = "General",
            frequencyCatalogItemPublicId = (Guid?)null,
            description = "Tarea con ETag con comillas",
            sortOrder = 1
        });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var created = await addResponse.Content.ReadFromJsonAsync<JobProfileFunctionResponse>(JsonOptions);
        Assert.NotNull(created);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/job-profiles/{profile.Id}/functions/{created!.FunctionPublicId}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");
        var deleteResponse = await client.SendAsync(deleteRequest);

        deleteResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task JobProfileRequirements_Get_ShouldReturnPagedResponse()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-REQ-001", "Perfil Requisitos");

        var firstResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/requirements", new
        {
            requirementType = "Experience",
            requirementTypeCatalogItemPublicId = (Guid?)null,
            catalogItemPublicId = (Guid?)null,
            catalogCode = (string?)null,
            catalogName = (string?)null,
            description = "3 years",
            sortOrder = 1
        });
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/requirements", new
        {
            requirementType = "Education",
            requirementTypeCatalogItemPublicId = (Guid?)null,
            catalogItemPublicId = (Guid?)null,
            catalogCode = (string?)null,
            catalogName = (string?)null,
            description = "Degree",
            sortOrder = 2
        });
        secondResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}/requirements?page=2&pageSize=1");
        listResponse.EnsureSuccessStatusCode();

        var payload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<JobProfileRequirementResponse>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.PageNumber);
        Assert.Equal(1, payload.PageSize);
        Assert.Equal(2, payload.TotalCount);
        var item = Assert.Single(payload.Items);
        Assert.Equal("Degree", item.Description);
    }

    [Fact]
    public async Task JobProfileCompetencies_Get_ShouldReturnPagedResponse()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-COMP-001", "Perfil Competencias");

        var firstResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/competencies", new
        {
            catalogItemPublicId = (Guid?)null,
            name = "Communication",
            expectedLevel = "Intermediate",
            notes = (string?)null,
            sortOrder = 1
        });
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/competencies", new
        {
            catalogItemPublicId = (Guid?)null,
            name = "Leadership",
            expectedLevel = "Advanced",
            notes = (string?)null,
            sortOrder = 2
        });
        secondResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}/competencies?page=2&pageSize=1");
        listResponse.EnsureSuccessStatusCode();

        var payload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<JobProfileLegacyCompetencyResponse>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.PageNumber);
        Assert.Equal(1, payload.PageSize);
        Assert.Equal(2, payload.TotalCount);
        var item = Assert.Single(payload.Items);
        Assert.Equal("Leadership", item.Name);
    }

    [Fact]
    public async Task CompetencyFramework_FullFlow_ShouldManagePyramidConductMatrixAndExports()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminWithAuditContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-CF-001", "Perfil Competencias");
        var competency = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.Competency, "COMP-CF-001", "Liderazgo Integral");
        var competencyType = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.CompetencyType, "CTYPE-CF-001", "Gerencial");
        var behaviorLevel = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.BehaviorLevel, "BLEVEL-CF-001", "Estrategico");
        var behavior = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.Behavior, "BEHAV-CF-001", "Comunica objetivos y resultados");

        var levelResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/occupational-pyramid-levels", new
        {
            code = "OPL-CF-001",
            name = "Nivel Estrategico CF",
            levelOrder = 1,
            description = "Nivel semilla para pruebas E2E."
        });
        levelResponse.EnsureSuccessStatusCode();
        var level = await levelResponse.Content.ReadFromJsonAsync<OccupationalPyramidLevelItem>(JsonOptions);
        Assert.NotNull(level);

        var conductResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/competency-conducts", new
        {
            competencyPublicId = competency.Id,
            competencyTypePublicId = competencyType.Id,
            behaviorLevelPublicId = behaviorLevel.Id,
            description = "Alinea decisiones con objetivos institucionales.",
            sortOrder = 1
        });
        conductResponse.EnsureSuccessStatusCode();
        var conduct = await conductResponse.Content.ReadFromJsonAsync<CompetencyConductItem>(JsonOptions);
        Assert.NotNull(conduct);

        var conductBehaviorResponse = await client.PutJsonAsync($"/api/v1/competency-conducts/{conduct!.Id}/behaviors", new
        {
            behaviors = new[]
            {
                new
                {
                    behaviorPublicId = behavior.Id,
                    notes = "Comportamiento base para pruebas.",
                    sortOrder = 0
                }
            },
            concurrencyToken = conduct.ConcurrencyToken
        });
        conductBehaviorResponse.EnsureSuccessStatusCode();
        var conductWithBehaviors = await conductBehaviorResponse.Content.ReadFromJsonAsync<CompetencyConductItem>(JsonOptions);
        Assert.NotNull(conductWithBehaviors);
        Assert.Single(conductWithBehaviors!.Behaviors);

        var profileBeforeResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}");
        profileBeforeResponse.EnsureSuccessStatusCode();
        var profileBefore = await profileBeforeResponse.Content.ReadFromJsonAsync<JobProfileEntityItem>(JsonOptions);
        Assert.NotNull(profileBefore);

        var matrixUpdateResponse = await client.PutJsonAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix", new
        {
            items = new[]
            {
                new
                {
                    occupationalPyramidLevelPublicId = level!.Id,
                    competencyPublicId = competency.Id,
                    competencyTypePublicId = competencyType.Id,
                    behaviorLevelPublicId = behaviorLevel.Id,
                    conductPublicIds = new[] { conductWithBehaviors.Id },
                    expectedEvidence = "Resultados comprobables en objetivos institucionales.",
                    sortOrder = 1
                }
            },
            concurrencyToken = profileBefore.ConcurrencyToken
        });
        matrixUpdateResponse.EnsureSuccessStatusCode();
        var matrixUpdated = await matrixUpdateResponse.Content.ReadFromJsonAsync<JobProfileCompetencyMatrixItem>(JsonOptions);
        Assert.NotNull(matrixUpdated);
        Assert.Single(matrixUpdated!.Items);
        var matrixItem = Assert.Single(matrixUpdated.Items);
        Assert.Equal(level.Id, matrixItem.OccupationalPyramidLevelId);
        Assert.Equal(competency.Id, matrixItem.CompetencyId);
        Assert.Single(matrixItem.Conducts);

        var profileAfterResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}");
        profileAfterResponse.EnsureSuccessStatusCode();
        var profileAfter = await profileAfterResponse.Content.ReadFromJsonAsync<JobProfileEntityItem>(JsonOptions);
        Assert.NotNull(profileAfter);
        Assert.True(profileAfter!.Version > profileBefore!.Version);
        Assert.NotEqual(profileBefore.ConcurrencyToken, profileAfter.ConcurrencyToken);

        var csvResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix/export?format=csv");
        csvResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);

        var jsonResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix/export?format=json");
        jsonResponse.EnsureSuccessStatusCode();
        var jsonPayload = await jsonResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<JobProfileCompetencyMatrixExportRowItem>>(JsonOptions);
        Assert.NotNull(jsonPayload);
        Assert.NotEmpty(jsonPayload!);

        var xlsxResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix/export?format=xlsx");
        xlsxResponse.EnsureSuccessStatusCode();
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsxResponse.Content.Headers.ContentType?.MediaType);

        var invalidExportResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix/export?format=xml");
        await AssertProblemDetailsAsync(invalidExportResponse, HttpStatusCode.BadRequest, "COMPETENCY_FRAMEWORK_EXPORT_FORMAT_INVALID");

        var auditResponse = await client.GetAsync("/api/audit/logs?page=1&pageSize=100");
        auditResponse.EnsureSuccessStatusCode();
        var auditPayload = await auditResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(auditPayload);
        Assert.Contains(
            auditPayload!.Items,
            static item => item.EventType == "REPORT_EXPORTED" &&
                           item.EntityType == "JobProfileCompetencyMatrix");
    }

    [Fact]
    public async Task CompetencyFramework_MatrixUpdate_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-CF-STALE", "Perfil CF Stale");

        var response = await client.PutJsonAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix", new
        {
            items = Array.Empty<object>(),
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task CompetencyFramework_MatrixUpdate_ShouldNotRequireLegacyCompetencyName()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminWithAuditContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-CF-PUT", "Perfil Competencias PUT");
        var detailResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<JobProfileEntityItem>(JsonOptions);
        Assert.NotNull(detail);

        var competency = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.Competency, "COMP-PUT-001", "Comunicacion efectiva");
        var competencyType = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.CompetencyType, "CTYPE-PUT-001", "Gerencial");
        var behaviorLevel = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.BehaviorLevel, "BLEVEL-PUT-001", "Basico");

        var levelResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/occupational-pyramid-levels", new
        {
            code = "OPL-PUT-001",
            name = "Analista",
            levelOrder = 30,
            description = (string?)null
        });
        levelResponse.EnsureSuccessStatusCode();
        var level = await levelResponse.Content.ReadFromJsonAsync<OccupationalPyramidLevelItem>(JsonOptions);
        Assert.NotNull(level);

        var conduct = await CreateCompetencyConductAsync(
            client,
            scenario.TenantId,
            competency.Id,
            competencyType.Id,
            behaviorLevel.Id,
            "Comunica informacion clara.",
            1);

        var matrixUpdateResponse = await client.PutJsonAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix", new
        {
            items = new[]
            {
                new
                {
                    occupationalPyramidLevelPublicId = level!.Id,
                    competencyPublicId = competency.Id,
                    competencyTypePublicId = competencyType.Id,
                    behaviorLevelPublicId = behaviorLevel.Id,
                    conductPublicIds = new[] { conduct.Id },
                    expectedEvidence = "Esperado",
                    sortOrder = 1
                }
            },
            concurrencyToken = detail!.ConcurrencyToken
        });
        matrixUpdateResponse.EnsureSuccessStatusCode();

        var matrix = await matrixUpdateResponse.Content.ReadFromJsonAsync<JobProfileCompetencyMatrixItem>(JsonOptions);
        Assert.NotNull(matrix);
        var competencyItem = Assert.Single(matrix!.Items);
        Assert.Equal(competency.Id, competencyItem.CompetencyId);
        Assert.Equal(behaviorLevel.Id, competencyItem.BehaviorLevelId);
        Assert.Single(competencyItem.Conducts);
    }

    [Fact]
    public async Task CompetencyConducts_Search_WithCombinationFilters_ShouldReturnOnlyExactBehaviorLevel()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminWithAuditContext(scenario));

        var competency = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.Competency, "COMP-COM-EF", "Comunicacion efectiva");
        var competencyType = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.CompetencyType, "CTYPE-GER", "Gerencial");
        var basicLevel = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.BehaviorLevel, "BLEVEL-BAS", "Basico");
        var intermediateLevel = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.BehaviorLevel, "BLEVEL-INT", "Intermedio");

        await CreateCompetencyConductAsync(client, scenario.TenantId, competency.Id, competencyType.Id, basicLevel.Id, "La comunicacion efectiva para gerencial basico", 1);
        await CreateCompetencyConductAsync(client, scenario.TenantId, competency.Id, competencyType.Id, basicLevel.Id, "Se comunica bien con sus subalternos", 2);
        await CreateCompetencyConductAsync(client, scenario.TenantId, competency.Id, competencyType.Id, basicLevel.Id, "Se comunica bien con sus superiores", 3);
        await CreateCompetencyConductAsync(client, scenario.TenantId, competency.Id, competencyType.Id, intermediateLevel.Id, "Seguimiento A", 4);
        await CreateCompetencyConductAsync(client, scenario.TenantId, competency.Id, competencyType.Id, intermediateLevel.Id, "Seguimiento B", 5);

        var basicResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/competency-conducts?competencyId={competency.Id}&competencyTypeId={competencyType.Id}&behaviorLevelId={basicLevel.Id}&isActive=true&page=1&pageSize=100");
        basicResponse.EnsureSuccessStatusCode();
        var basicPayload = await basicResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<CompetencyConductListItem>>(JsonOptions);

        Assert.NotNull(basicPayload);
        Assert.Equal(3, basicPayload!.TotalCount);
        Assert.All(basicPayload.Items, item => Assert.Equal(basicLevel.Id, item.BehaviorLevelId));
        Assert.DoesNotContain(basicPayload.Items, static item => item.Description is "Seguimiento A" or "Seguimiento B");

        var intermediateResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/competency-conducts?competencyId={competency.Id}&competencyTypeId={competencyType.Id}&behaviorLevelId={intermediateLevel.Id}&isActive=true&page=1&pageSize=100");
        intermediateResponse.EnsureSuccessStatusCode();
        var intermediatePayload = await intermediateResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<CompetencyConductListItem>>(JsonOptions);

        Assert.NotNull(intermediatePayload);
        Assert.Equal(2, intermediatePayload!.TotalCount);
        Assert.All(intermediatePayload.Items, item => Assert.Equal(intermediateLevel.Id, item.BehaviorLevelId));

        var partialFilterResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/competency-conducts?competencyId={competency.Id}&competencyTypeId={competencyType.Id}&isActive=true&page=1&pageSize=100");
        await AssertProblemDetailsAsync(partialFilterResponse, HttpStatusCode.BadRequest, "common.validation");
    }

    [Fact]
    public async Task CompetencyFramework_List_WithTenantMismatch_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkReadContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.OtherTenantId}/occupational-pyramid-levels?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task CompetencyFramework_List_WithoutPermission_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/occupational-pyramid-levels?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "COMPETENCY_FRAMEWORK_FORBIDDEN");
    }

    [Fact]
    public async Task Reports_Capabilities_ShouldReturnCompetencyFrameworkCapabilities()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkReadContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/reports/capabilities?resource={CompetencyFrameworkPermissionCodes.ResourceKey}");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ReportCapabilitiesItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(CompetencyFrameworkPermissionCodes.ResourceKey, payload!.ResourceKey);
        Assert.True(payload.SupportsExport);
        Assert.False(payload.SupportsPrint);
        Assert.Contains("json", payload.SupportedTableFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("csv", payload.SupportedTableFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("xlsx", payload.SupportedTableFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(payload.SupportedGraphFormats);
    }

    [Fact]
    public async Task PositionSlots_FullFlow_ShouldCreateUpdateDependenciesOccupancyStatusGraphAndExports()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-PS", "Direccion Plazas", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-PS", "Analista de Plazas", orgUnit.Id);

        var primary = await CreatePositionSlotAsync(
            client,
            scenario.TenantId,
            code: "PS-001",
            title: "Plaza Principal",
            jobProfileId: profile.Id,
            maxEmployees: 2);

        var dependency = await CreatePositionSlotAsync(
            client,
            scenario.TenantId,
            code: "PS-002",
            title: "Plaza Dependencia",
            jobProfileId: profile.Id,
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
        Assert.Equal(orgUnit.Id, getPayload.OrgUnitId);
        Assert.Null(getPayload.CostCenterCode);

        var updateResponse = await client.PutJsonAsync($"/api/v1/position-slots/{primary.Id}", new
        {
            code = "PS-001",
            title = "Plaza Principal Actualizada",
            jobProfilePublicId = profile.Id,
            workCenterPublicId = (Guid?)null,
            maxEmployees = 3,
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
            directDependencyPositionSlotPublicId = dependency.Id,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
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
    public async Task PositionSlots_ShouldReflectUpdatedJobProfileOrgUnitAndCostCenter()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));
        using var costCenterClient = factory.CreateClientFor(CreateCostCenterAdminContext(scenario));

        _ = await CreateCostCenterAsync(costCenterClient, scenario.TenantId, "CC-DER-OLD", "Centro Derivado Inicial", "Mixed");
        _ = await CreateCostCenterAsync(costCenterClient, scenario.TenantId, "CC-DER-NEW", "Centro Derivado Nuevo", "Mixed");

        var initialOrgUnit = await CreateOrgUnitAsync(
            client,
            scenario.TenantId,
            "DIR-DER-OLD",
            "Direccion Inicial",
            "Direccion",
            costCenterCode: "CC-DER-OLD");
        var updatedOrgUnit = await CreateOrgUnitAsync(
            client,
            scenario.TenantId,
            "DIR-DER-NEW",
            "Direccion Nueva",
            "Direccion",
            costCenterCode: "CC-DER-NEW");

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-DER", "Perfil Derivado", initialOrgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-DER", "Plaza Derivada", profile.Id, 1);
        var positionCategory = await EnsureDefaultPositionCategoryAsync(client, scenario.TenantId);

        var updateProfileResponse = await client.PutJsonAsync($"/api/v1/job-profiles/{profile.Id}", new
        {
            code = profile.Code,
            title = profile.Title,
            objective = "Objetivo",
            orgUnitPublicId = updatedOrgUnit.Id,
            reportsToJobProfilePublicId = (Guid?)null,
            positionCategoryPublicId = positionCategory.Id,
            strategicObjectiveCatalogItemPublicId = (Guid?)null,
            assignedWorkEquipmentCatalogItemPublicId = (Guid?)null,
            responsibilityCatalogItemPublicId = (Guid?)null,
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
                    catalogItemPublicId = (Guid?)null,
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
            compensation = (object?)null,
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>(),
            concurrencyToken = profile.ConcurrencyToken
        });
        updateProfileResponse.EnsureSuccessStatusCode();

        var detailResponse = await client.GetAsync($"/api/v1/position-slots/{slot.Id}");
        detailResponse.EnsureSuccessStatusCode();

        var payload = await detailResponse.Content.ReadFromJsonAsync<PositionSlotItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(updatedOrgUnit.Id, payload!.OrgUnitId);
        Assert.Equal("CC-DER-NEW", payload.CostCenterCode);

        var filteredResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-slots?page=1&pageSize=20&orgUnitId={updatedOrgUnit.Id}");
        filteredResponse.EnsureSuccessStatusCode();

        var filteredPayload = await filteredResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<PositionSlotListItem>>(JsonOptions);
        Assert.NotNull(filteredPayload);
        Assert.Contains(filteredPayload!.Items, item => item.Id == slot.Id);
    }

    [Fact]
    public async Task PositionSlots_Update_WithStaleToken_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-STALE", "Direccion", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-STALE", "Perfil", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-STALE", "Plaza", profile.Id, 1);

        var response = await client.PutJsonAsync($"/api/v1/position-slots/{slot.Id}", new
        {
            code = slot.Code,
            title = "Plaza actualizada",
            jobProfilePublicId = profile.Id,
            workCenterPublicId = (Guid?)null,
            maxEmployees = 1,
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
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-DUP", "Perfil", orgUnit.Id);

        _ = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-DUP", "Plaza 1", profile.Id, 1);

        var duplicateResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-slots", new
        {
            code = "PS-DUP",
            title = "Plaza 2",
            jobProfilePublicId = profile.Id,
            workCenterPublicId = (Guid?)null,
            directDependencyPositionSlotPublicId = (Guid?)null,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            occupiedEmployees = 0,
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
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-CYCLE", "Perfil", orgUnit.Id);

        var parent = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-A", "A", profile.Id, 1);
        var child = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-B", "B", profile.Id, 1);

        var parentUpdateResponse = await client.PatchAsJsonAsync($"/api/v1/position-slots/{parent.Id}/dependencies", new
        {
            directDependencyPositionSlotPublicId = child.Id,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
            concurrencyToken = parent.ConcurrencyToken
        });
        parentUpdateResponse.EnsureSuccessStatusCode();

        var cycleResponse = await client.PatchAsJsonAsync($"/api/v1/position-slots/{child.Id}/dependencies", new
        {
            directDependencyPositionSlotPublicId = parent.Id,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
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
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-CAP", "Perfil", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-CAP", "Capacidad", profile.Id, 1);

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
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-AUD", "Perfil", orgUnit.Id);

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-slots", new
        {
            code = "PS-AUD",
            title = "Plaza Audit",
            jobProfilePublicId = profile.Id,
            workCenterPublicId = (Guid?)null,
            directDependencyPositionSlotPublicId = (Guid?)null,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            occupiedEmployees = 0,
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

        var updateResponse = await client.PutJsonAsync($"/api/v1/cost-centers/{created.Id}", new
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

        var duplicateResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/cost-centers", new
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

        var response = await client.PutJsonAsync($"/api/v1/cost-centers/{costCenter.Id}", new
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
            orgUnitTypeCode: "Direccion",
            costCenterCode: costCenter.Code);

        var response = await client.PatchAsJsonAsync($"/api/v1/cost-centers/{costCenter.Id}/inactivate", new
        {
            concurrencyToken = costCenter.ConcurrencyToken
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "COST_CENTER_IN_USE");
    }

    [Fact]
    public async Task CostCenters_Usage_ShouldCountPositionSlotsDerivedFromJobProfileOrgUnit()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var costCenterClient = factory.CreateClientFor(CreateCostCenterAdminContext(scenario));
        using var orgUnitClient = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));
        using var jobProfileClient = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        using var positionSlotClient = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var costCenter = await CreateCostCenterAsync(costCenterClient, scenario.TenantId, "CC-SLOT-USAGE", "Centro Plaza", "Mixed");
        var orgUnit = await CreateOrgUnitAsync(
            orgUnitClient,
            scenario.TenantId,
            "DIR-SLOT-USAGE",
            "Direccion Plaza",
            "Direccion",
            costCenterCode: costCenter.Code);
        var profile = await CreateJobProfileAsync(jobProfileClient, scenario.TenantId, "JP-SLOT-USAGE", "Perfil Plaza", orgUnit.Id);
        _ = await CreatePositionSlotAsync(positionSlotClient, scenario.TenantId, "PS-SLOT-USAGE", "Plaza Uso", profile.Id, 1);

        var usageResponse = await costCenterClient.GetAsync($"/api/v1/cost-centers/{costCenter.Id}/usage");
        usageResponse.EnsureSuccessStatusCode();

        var usage = await usageResponse.Content.ReadFromJsonAsync<CostCenterUsageItem>(JsonOptions);
        Assert.NotNull(usage);
        Assert.Equal(1, usage!.OrgUnitActiveReferences);
        Assert.Equal(1, usage.PositionSlotActiveReferences);
        Assert.True(usage.HasActiveReferences);
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

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/cost-centers", new
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
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, scenario.TenantId, "Direccion");

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-CC-INV",
            name = "Direccion Invalida",
            orgUnitTypePublicId = orgUnitType.Id,
            functionalAreaPublicId = (Guid?)null,
            parentPublicId = (Guid?)null,
            sortOrder = 1,
            description = (string?)null,
            costCenterCode = "CC-NOT-EXISTS",
            managerEmployeePublicId = (Guid?)null
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ORG_UNIT_COST_CENTER_INVALID");
    }

    [Fact]
    public async Task PositionSlots_Create_WithInactiveCostCenterInferredFromOrgUnit_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var costCenterClient = factory.CreateClientFor(CreateCostCenterAdminContext(scenario));

        var costCenter = await CreateCostCenterAsync(costCenterClient, scenario.TenantId, "CC-INACTIVE", "Centro Inactivo", "Mixed");

        using var slotClient = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(
            slotClient,
            scenario.TenantId,
            "DIR-PS-CC",
            "Direccion",
            "Direccion",
            costCenterCode: "CC-INACTIVE");
        var profile = await CreateJobProfileAsync(slotClient, scenario.TenantId, "JP-PS-CC", "Perfil", orgUnit.Id);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var costCenterEntity = await dbContext.CostCenters
                .IgnoreQueryFilters()
                .SingleAsync(item => item.PublicId == costCenter.Id);
            costCenterEntity.Inactivate();
            await dbContext.SaveChangesAsync();
        }

        var response = await slotClient.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-slots", new
        {
            code = "PS-CC-INV",
            title = "Plaza con centro inactivo",
            jobProfilePublicId = profile.Id,
            workCenterPublicId = (Guid?)null,
            directDependencyPositionSlotPublicId = (Guid?)null,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            occupiedEmployees = 0,
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
        var salaryClass = await EnsureSalaryClassAsync(requesterClient, scenario.TenantId, "CLS-A");

        var createResponse = await requesterClient.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/change-requests", new
        {
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            items = new[]
            {
                new
                {
                    salaryClassPublicId = salaryClass.Id,
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

        var updateResponse = await requesterClient.PutJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}", new
        {
            reason = "Ajuste anual actualizado",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassPublicId = salaryClass.Id,
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

        var listLinesResponse = await approverClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/lines?salaryClassPublicId={salaryClass.Id}&page=1&pageSize=20");
        listLinesResponse.EnsureSuccessStatusCode();
        var linesPayload = await listLinesResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<SalaryTabulatorLineItem>>(JsonOptions);
        Assert.NotNull(linesPayload);
        Assert.Contains(linesPayload!.Items, line => line.SalaryClassId == salaryClass.Id && line.BaseAmount == 1250m);

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
        var salaryClass = await EnsureSalaryClassAsync(client, scenario.TenantId, "CLS-C");

        var created = await CreateSalaryTabulatorRequestAsync(client, scenario.TenantId, "CLS-C", "S1", 1000m);

        var updateResponse = await client.PutJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}", new
        {
            reason = "stale",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassPublicId = salaryClass.Id,
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

        var response = await client.GetAsync($"/api/v1/companies/{scenario.OtherTenantId}/salary-tabulator/lines?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "TENANT_MISMATCH");
    }

    [Fact]
    public async Task SalaryTabulator_SearchLines_WithoutPermission_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/lines?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "SALARY_TABULATOR_FORBIDDEN");
    }

    [Fact]
    public async Task SalaryTabulator_SearchLines_WithIncludeAllowedActions_ShouldReflectRequestPermission()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));
        using var readClient = factory.CreateClientFor(CreateSalaryTabulatorReadContext(scenario));

        var createdRequest = await CreateSalaryTabulatorRequestAsync(requesterClient, scenario.TenantId, "CLS-ACTIONS", "S1", 1350m);

        var submitResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/submit", new
        {
            concurrencyToken = createdRequest.ConcurrencyToken
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submitted);

        var approveResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{createdRequest.Id}/approve", new
        {
            decisionComment = "approved",
            concurrencyToken = submitted!.ConcurrencyToken
        });
        approveResponse.EnsureSuccessStatusCode();

        var requesterLinesResponse = await requesterClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/salary-tabulator/lines?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstActiveSalaryLineAllowedActionsAsync(requesterLinesResponse, expectedCanEdit: true, expectedCanInactivate: true);

        var readLinesResponse = await readClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/salary-tabulator/lines?page=1&pageSize=20&includeAllowedActions=true");
        await AssertFirstActiveSalaryLineAllowedActionsAsync(readLinesResponse, expectedCanEdit: false, expectedCanInactivate: false);
    }

    [Fact]
    public async Task SalaryTabulator_ChangeRequests_WithIncludeAllowedActions_ShouldReflectWorkflowPermissions()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var requesterClient = factory.CreateClientFor(CreateSalaryTabulatorRequesterContext(scenario));
        using var approverClient = factory.CreateClientFor(CreateSalaryTabulatorApproverContext(scenario));
        using var requestAndApproveClient = factory.CreateClientFor(CreateSalaryTabulatorRequestAndApproveContext(scenario));

        var draft = await CreateSalaryTabulatorRequestAsync(requesterClient, scenario.TenantId, "CLS-WFA-DRAFT", "S1", 1100m);
        await AssertSalaryTabulatorChangeRequestAllowedActionsAsync(
            requesterClient,
            scenario.TenantId,
            draft.Id,
            allowedActions =>
            {
                AssertWorkflowAllowedActions(
                    allowedActions,
                    canEdit: true,
                    canSubmit: true,
                    canApprove: false,
                    canReject: false,
                    canCancel: true);
                AssertActionPermission(allowedActions, "submit", SalaryTabulatorPermissionCodes.Request, allowed: true);
                AssertActionPermission(allowedActions, "cancel", SalaryTabulatorPermissionCodes.Request, allowed: true);
                AssertActionPermission(allowedActions, "approve", SalaryTabulatorPermissionCodes.Approve, allowed: false);
            });

        var submitResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{draft.Id}/submit", new
        {
            concurrencyToken = draft.ConcurrencyToken
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(submitted);

        await AssertSalaryTabulatorChangeRequestAllowedActionsAsync(
            approverClient,
            scenario.TenantId,
            draft.Id,
            allowedActions =>
            {
                AssertWorkflowAllowedActions(
                    allowedActions,
                    canEdit: false,
                    canSubmit: false,
                    canApprove: true,
                    canReject: true,
                    canCancel: false);
                AssertActionPermission(allowedActions, "approve", SalaryTabulatorPermissionCodes.Approve, allowed: true);
                AssertActionPermission(allowedActions, "reject", SalaryTabulatorPermissionCodes.Approve, allowed: true);
            });

        var selfApprovalRequest = await CreateSalaryTabulatorRequestAsync(requestAndApproveClient, scenario.TenantId, "CLS-WFA-SELF", "S1", 1200m);
        var selfSubmitResponse = await requestAndApproveClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{selfApprovalRequest.Id}/submit", new
        {
            concurrencyToken = selfApprovalRequest.ConcurrencyToken
        });
        selfSubmitResponse.EnsureSuccessStatusCode();

        await AssertSalaryTabulatorChangeRequestAllowedActionsAsync(
            requestAndApproveClient,
            scenario.TenantId,
            selfApprovalRequest.Id,
            allowedActions =>
            {
                AssertWorkflowAllowedActions(
                    allowedActions,
                    canEdit: false,
                    canSubmit: false,
                    canApprove: false,
                    canReject: true,
                    canCancel: false);
                AssertActionPermission(allowedActions, "approve", SalaryTabulatorPermissionCodes.Approve, allowed: false);
                AssertActionPermission(allowedActions, "reject", SalaryTabulatorPermissionCodes.Approve, allowed: true);
            });

        var approveResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{draft.Id}/approve", new
        {
            decisionComment = "approved",
            concurrencyToken = submitted!.ConcurrencyToken
        });
        approveResponse.EnsureSuccessStatusCode();
        await AssertSalaryTabulatorChangeRequestAllowedActionsAsync(
            approverClient,
            scenario.TenantId,
            draft.Id,
            AssertNoWorkflowAllowedActions);

        var rejectedRequest = await CreateSalaryTabulatorRequestAsync(requesterClient, scenario.TenantId, "CLS-WFA-REJECT", "S1", 1300m);
        var rejectedSubmitResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{rejectedRequest.Id}/submit", new
        {
            concurrencyToken = rejectedRequest.ConcurrencyToken
        });
        rejectedSubmitResponse.EnsureSuccessStatusCode();
        var rejectedSubmitted = await rejectedSubmitResponse.Content.ReadFromJsonAsync<SalaryTabulatorChangeRequestItem>(JsonOptions);
        Assert.NotNull(rejectedSubmitted);

        var rejectResponse = await approverClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{rejectedRequest.Id}/reject", new
        {
            decisionComment = "rejected",
            concurrencyToken = rejectedSubmitted!.ConcurrencyToken
        });
        rejectResponse.EnsureSuccessStatusCode();
        await AssertSalaryTabulatorChangeRequestAllowedActionsAsync(
            approverClient,
            scenario.TenantId,
            rejectedRequest.Id,
            AssertNoWorkflowAllowedActions);

        var canceledRequest = await CreateSalaryTabulatorRequestAsync(requesterClient, scenario.TenantId, "CLS-WFA-CANCEL", "S1", 1400m);
        var cancelResponse = await requesterClient.PatchAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{canceledRequest.Id}/cancel", new
        {
            concurrencyToken = canceledRequest.ConcurrencyToken
        });
        cancelResponse.EnsureSuccessStatusCode();
        await AssertSalaryTabulatorChangeRequestAllowedActionsAsync(
            requesterClient,
            scenario.TenantId,
            canceledRequest.Id,
            AssertNoWorkflowAllowedActions);
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

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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
        var user = Assert.Single(payload!.Items, static item => item.FirstName == "Target" && item.LastName == "User");
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

        var response = await client.PutJsonAsync($"/api/company/users/{scenario.TargetUserId}", new
        {
            firstName = "Blocked",
            lastName = "User",
            rolePublicIds = new[] { scenario.TargetRoleId }
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

        var response = await client.PutJsonAsync($"/api/company/users/{scenario.TargetUserId}", new
        {
            firstName = "Target",
            lastName = "Updated",
            rolePublicIds = new[] { scenario.TargetRoleId }
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task IamUsers_GetById_WithLinkedUserPublicId_ShouldReturnUserContract()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Users, RbacPermissionAction.Access),
            (RbacPermissionScreen.Users, RbacPermissionAction.Read)));

        var response = await client.GetAsync($"/api/iam/users/{scenario.ActorUserId}");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IamUserDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("security.operator@acme-one.test", payload!.Email);
        var role = Assert.Single(payload.Roles);
        Assert.Equal("Security Operator", role.Name);
    }

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task InfrastructureInitialization_ShouldSeedRbacCatalogResourcesAndMatrixPermissions()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var matrixCodes = PermissionMatrixCatalog.AllMatrixCodes
            .Select(static code => code.ToUpperInvariant())
            .ToArray();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seededMatrixPermissions = await dbContext.IamPermissions
                .Where(permission => permission.TenantId == scenario.TenantId && matrixCodes.Contains(permission.NormalizedCode))
                .ToListAsync();

            dbContext.IamPermissions.RemoveRange(seededMatrixPermissions);
            await dbContext.SaveChangesAsync();
        }

        await factory.Services.InitializeInfrastructureAsync(NullLogger.Instance);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenantMatrixPermissions = await verificationDbContext.IamPermissions
            .AsNoTracking()
            .Where(permission => permission.TenantId == scenario.TenantId && matrixCodes.Contains(permission.NormalizedCode))
            .Select(permission => permission.NormalizedCode)
            .ToListAsync();
        Assert.Equal(matrixCodes.Length, tenantMatrixPermissions.Count);
        Assert.All(matrixCodes, code => Assert.Contains(code, tenantMatrixPermissions));
    }

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
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
    public async Task AuditLogs_WithEntityPublicIdFilter_ShouldReturnOnlyMatchingEntityAndAlignedTotalCount()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read)));

        var response = await client.GetAsync(
            $"/api/audit/logs?EntityPublicId={scenario.TargetUserId}&EntityType=User&page=1&pageSize=20");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);

        var item = Assert.Single(payload.Items);
        Assert.Equal("USER_UPDATED", item.EventType);
        Assert.Equal("User", item.EntityType);
        Assert.Equal(scenario.TargetUserId, item.EntityId);
    }

    [Fact]
    public async Task AuditLogs_WithLegacyEntityIdAlias_ShouldReturnOnlyMatchingEntityAndAlignedTotalCount()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read)));

        var response = await client.GetAsync(
            $"/api/audit/logs?EntityId={scenario.TargetUserId}&EntityType=User&page=1&pageSize=20");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<AuditLogSummaryItem>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);

        var item = Assert.Single(payload.Items);
        Assert.Equal("USER_UPDATED", item.EventType);
        Assert.Equal("User", item.EntityType);
        Assert.Equal(scenario.TargetUserId, item.EntityId);
    }

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task IamUsers_Create_WithPermission_ShouldReturnCreatedUser()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Users, RbacPermissionAction.Create)));

        var response = await client.PostJsonAsync("/api/iam/users", new
        {
            firstName = "New",
            lastName = "Operator",
            email = "new.operator@acme-one.test",
            isActive = true,
            rolePublicIds = new[] { scenario.TargetRoleId }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<IamUserDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("new.operator@acme-one.test", payload!.Email);
        var role = Assert.Single(payload.Roles);
        Assert.Equal(scenario.TargetRoleId, role.Id);
    }

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task IamUsers_SyncRoles_WithPermission_ShouldReturnUpdatedRoles()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Users, RbacPermissionAction.Update)));

        var response = await client.PutJsonAsync($"/api/iam/users/{scenario.ActorUserId}/roles", new
        {
            rolePublicIds = new[] { scenario.TargetRoleId }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IamUserDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        var role = Assert.Single(payload!.Roles);
        Assert.Equal(scenario.TargetRoleId, role.Id);
        Assert.Equal("Employee", role.Name);
    }

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task IamRoles_Create_WithPermission_ShouldReturnCreatedRole()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Create)));

        var response = await client.PostJsonAsync("/api/iam/roles", new
        {
            name = "Payroll Reviewer",
            description = "Reviews payroll changes",
            permissionPublicIds = new[] { scenario.ActorPermissionId }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<IamRoleDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Payroll Reviewer", payload!.Name);
        var permission = Assert.Single(payload.Permissions);
        Assert.Equal(scenario.ActorPermissionId, permission.Id);
    }

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task IamRoles_Update_WithPermission_ShouldReturnUpdatedRole()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Update)));

        var response = await client.PutJsonAsync($"/api/iam/roles/{scenario.TargetRoleId}", new
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task IamRoles_Clone_WithPermission_ShouldReturnClonedRole()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Create)));

        var response = await client.PostJsonAsync($"/api/iam/roles/{scenario.TargetRoleId}/clone", new
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task IamRoles_SyncPermissions_WithPermission_ShouldReturnUpdatedPermissions()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            userId: scenario.SecurityAdminUserId,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Update),
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Update)));

        var response = await client.PutJsonAsync($"/api/iam/roles/{scenario.TargetRoleId}/permissions", new
        {
            permissionPublicIds = new[] { scenario.ActorPermissionId }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IamRoleDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        var permission = Assert.Single(payload!.Permissions);
        Assert.Equal(scenario.ActorPermissionId, permission.Id);
    }

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task IamRoles_SyncUsers_WithPermission_ShouldReturnUpdatedUserCount()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Roles, RbacPermissionAction.Update)));

        var response = await client.PutJsonAsync($"/api/iam/roles/{scenario.TargetRoleId}/users", new
        {
            userPublicIds = new[] { scenario.ActorUserId }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IamRoleDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.UserCount);
    }

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task IamPermissions_Create_WithPermission_ShouldReturnCreatedPermission()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Create)));

        var response = await client.PostJsonAsync("/api/iam/permissions", new
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task RbacRolePermissions_Update_WithPermission_ShouldReturnUpdatedMatrix()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            userId: scenario.SecurityAdminUserId,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Update)));

        var response = await client.PutJsonAsync($"/api/rbac/roles/{scenario.TargetRoleId}/permissions", new
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

    [Fact(Skip = LegacyIamRbacApiDeprecatedSkipReason)]
    public async Task RbacRoleFieldPermissions_Update_WithPermission_ShouldNormalizeOverrides()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUserContext(
            scenario,
            userId: scenario.SecurityAdminUserId,
            (RbacPermissionScreen.Permissions, RbacPermissionAction.Update)));

        var grantResourceResponse = await client.PutJsonAsync($"/api/rbac/roles/{scenario.TargetRoleId}/permissions", new
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

        var response = await client.PutJsonAsync($"/api/rbac/roles/{scenario.TargetRoleId}/field-permissions", new
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

    private static TestUserContext CreatePositionCatalogReadContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, PositionDescriptionCatalogPermissionCodes.Read);

    private static TestUserContext CreateJobProfileAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin);

    private static TestUserContext CreateJobProfileAdminWithCatalogContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            JobProfilePermissionCodes.Admin,
            JobProfilePermissionCodes.CatalogAdmin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin);

    private static TestUserContext CreateJobProfileAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read));

    public static IEnumerable<object[]> JobProfilePatchEndpointTemplates()
    {
        yield return ["/api/v1/job-profiles/{profileId}"];
        yield return ["/api/v1/job-profiles/{profileId}/requirements/{resourceId}"];
        yield return ["/api/v1/job-profiles/{profileId}/functions/{resourceId}"];
        yield return ["/api/v1/job-profiles/{profileId}/relations/{resourceId}"];
        yield return ["/api/v1/job-profiles/{profileId}/competencies/{resourceId}"];
        yield return ["/api/v1/job-profiles/{profileId}/trainings/{resourceId}"];
        yield return ["/api/v1/job-profiles/{profileId}/compensations/{resourceId}"];
        yield return ["/api/v1/job-profiles/{profileId}/benefits/{resourceId}"];
        yield return ["/api/v1/job-profiles/{profileId}/working-conditions/{resourceId}"];
        yield return ["/api/v1/job-profiles/{profileId}/dependent-positions/{resourceId}"];
    }

    private static TestUserContext CreateCompetencyFrameworkReadContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, CompetencyFrameworkPermissionCodes.Read);

    private static TestUserContext CreateCompetencyFrameworkAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            CompetencyFrameworkPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            JobProfilePermissionCodes.CatalogAdmin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin);

    private static TestUserContext CreateCompetencyFrameworkAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            CompetencyFrameworkPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            JobProfilePermissionCodes.CatalogAdmin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
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
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin);

    private static TestUserContext CreatePositionSlotAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PositionSlotPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin,
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
            PositionSlotPermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin);

    private static TestUserContext CreateCostCenterAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            CostCenterPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            PositionSlotPermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read));

    private static TestUserContext CreatePersonnelFileAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin);

    private static TestUserContext CreateSalaryTabulatorReadContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, SalaryTabulatorPermissionCodes.Read);

    private static TestUserContext CreateSalaryTabulatorRequesterContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            SalaryTabulatorPermissionCodes.Read,
            SalaryTabulatorPermissionCodes.Request,
            PositionDescriptionCatalogPermissionCodes.Admin);

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
            SalaryTabulatorPermissionCodes.Approve,
            PositionDescriptionCatalogPermissionCodes.Admin);

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
        return Assert.Single(payload!.Items, static group => group.LevelOrder == 3 && group.Code == "APOPA");
    }

    private async Task<IReadOnlyCollection<LocationGroupTreeItem>> GetLocationGroupTreeAsync(HttpClient client, Guid companyId)
    {
        var response = await client.GetAsync($"/api/v1/companies/{companyId}/location-groups/tree");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IReadOnlyCollection<LocationGroupTreeItem>>(JsonOptions))!;
    }

    private async Task<OrgUnitItem> CreateOrgUnitAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string name,
        string orgUnitTypeCode,
        Guid? parentPublicId = null,
        int? sortOrder = 1,
        string? costCenterCode = null)
    {
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, companyId, orgUnitTypeCode);

        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/org-units", new
        {
            code,
            name,
            orgUnitTypePublicId = orgUnitType.Id,
            functionalAreaPublicId = (Guid?)null,
            parentPublicId,
            sortOrder,
            description = (string?)null,
            costCenterCode,
            managerEmployeePublicId = (Guid?)null
        });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"CreateOrgUnitAsync failed: {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<OrgUnitItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<OrgStructureCatalogItem> EnsureOrgUnitTypeAsync(HttpClient client, Guid companyId, string code)
    {
        var listResponse = await client.GetAsync($"/api/v1/companies/{companyId}/org-structure-catalogs/unit-types?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<OrgStructureCatalogItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/org-structure-catalogs/unit-types", new
        {
            code,
            name = code,
            description = (string?)null,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<OrgStructureCatalogItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<OrgStructureCatalogItem> EnsureFunctionalAreaAsync(HttpClient client, Guid companyId, string code)
    {
        var listResponse = await client.GetAsync($"/api/v1/companies/{companyId}/org-structure-catalogs/functional-areas?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<OrgStructureCatalogItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/org-structure-catalogs/functional-areas", new
        {
            code,
            name = code,
            description = (string?)null,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<OrgStructureCatalogItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<List<OrgStructureCatalogItem>> GetAccountCompanyTypesAsync(HttpClient client, string countryCode)
    {
        var response = await client.GetAsync($"/api/account/companies/company-types?countryCode={countryCode}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<OrgStructureCatalogItem>>(JsonOptions);
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
        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/cost-centers", new
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

    private async Task<PersonnelFileShellItem> CreateBarePersonnelFileAsync(
        HttpClient client,
        Guid companyId,
        string firstName,
        string lastName,
        string profession = "ANALISTA_DE_DATOS",
        string maritalStatus = "SOLTERO_A",
        string nationality = "SV")
    {
        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName,
            lastName,
            birthDate = new DateTime(1990, 3, 3),
            maritalStatusCode = maritalStatus,
            professionCode = profession,
            nationality,
            personalEmail = (string?)null,
            institutionalEmail = (string?)null,
            personalPhone = "+50370001000",
            institutionalPhone = (string?)null,
            birthCountryCode = "SV",
            birthDepartmentCode = "SAN_SALVADOR",
            birthMunicipalityCode = "SAN_SALVADOR_CENTRO",
            photoFilePublicId = (Guid?)null,
            orgUnitPublicId = (Guid?)null,
            customDataJson = (string?)null
        });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Personnel file create failed with {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<PersonnelFileShellItem> CreatePersonnelFileAsync(
        HttpClient client,
        Guid companyId,
        string firstName,
        string lastName,
        string identificationType,
        string identificationNumber,
        string profession = "ANALISTA_DE_DATOS",
        string maritalStatus = "SOLTERO_A",
        string nationality = "SV")
    {
        var created = await CreateBarePersonnelFileAsync(
            client,
            companyId,
            firstName,
            lastName,
            profession,
            maritalStatus,
            nationality);

        var addIdentificationResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/identifications", new
        {
            identificationTypeCode = identificationType,
            identificationNumber,
            issuedDate = (DateTime?)null,
            expiryDate = (DateTime?)null,
            issuer = (string?)null,
            isPrimary = true,
            concurrencyToken = created.ConcurrencyToken
        });
        if (!addIdentificationResponse.IsSuccessStatusCode)
        {
            var body = await addIdentificationResponse.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Personnel file identification add failed with {(int)addIdentificationResponse.StatusCode} {addIdentificationResponse.StatusCode}. Body: {body}");
        }

        var shellResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        shellResponse.EnsureSuccessStatusCode();

        var refreshed = await shellResponse.Content.ReadFromJsonAsync<PersonnelFileShellItem>(JsonOptions);
        Assert.NotNull(refreshed);

        var identificationsResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/identifications");
        identificationsResponse.EnsureSuccessStatusCode();
        Assert.NotNull(await identificationsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileIdentificationItem>>(JsonOptions));

        return refreshed!;
    }

    private async Task<JobCatalogItemItem> CreateJobCatalogItemAsync(
        HttpClient client,
        Guid companyId,
        JobCatalogCategory category,
        string code,
        string name)
    {
        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/job-catalogs/{category}", new
        {
            code,
            name
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JobCatalogItemItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<CompetencyConductItem> CreateCompetencyConductAsync(
        HttpClient client,
        Guid companyId,
        Guid competencyId,
        Guid competencyTypeId,
        Guid behaviorLevelId,
        string description,
        int sortOrder)
    {
        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/competency-conducts", new
        {
            competencyPublicId = competencyId,
            competencyTypePublicId = competencyTypeId,
            behaviorLevelPublicId = behaviorLevelId,
            description,
            sortOrder
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CompetencyConductItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<PositionDescriptionCatalogItem> EnsurePositionDescriptionCatalogItemAsync(
        HttpClient client,
        Guid companyId,
        string routeSegment,
        string code,
        string? name = null)
    {
        var listResponse = await client.GetAsync($"/api/v1/companies/{companyId}/position-description-catalogs/{routeSegment}/items?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<PositionDescriptionCatalogItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/position-description-catalogs/{routeSegment}/items", new
        {
            code,
            name = name ?? code,
            description = (string?)null,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<PositionDescriptionCatalogItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<PositionCategoryClassificationItem> EnsurePositionCategoryClassificationAsync(
        HttpClient client,
        Guid companyId,
        string code,
        Guid positionFunctionTypeId,
        Guid positionContractTypeId,
        Guid orgUnitTypeId)
    {
        var listResponse = await client.GetAsync(
            $"/api/v1/companies/{companyId}/position-category-classifications?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<PositionCategoryClassificationItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/position-category-classifications", new
        {
            code,
            name = code,
            description = (string?)null,
            positionFunctionTypePublicId = positionFunctionTypeId,
            positionContractTypePublicId = positionContractTypeId,
            orgUnitTypePublicId = orgUnitTypeId,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<PositionCategoryClassificationItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<PositionCategoryItem> EnsurePositionCategoryAsync(
        HttpClient client,
        Guid companyId,
        string code,
        Guid classificationId)
    {
        var listResponse = await client.GetAsync($"/api/v1/companies/{companyId}/position-categories?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<PositionCategoryItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/position-categories", new
        {
            code,
            name = code,
            description = (string?)null,
            classificationPublicId = classificationId,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<PositionCategoryItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<PositionCategoryItem> EnsureDefaultPositionCategoryAsync(HttpClient client, Guid companyId)
    {
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, companyId, "Direccion");
        var functionType = await EnsurePositionDescriptionCatalogItemAsync(client, companyId, "position-function-types", "FUNC-BASE");
        var contractType = await EnsurePositionDescriptionCatalogItemAsync(client, companyId, "position-contract-types", "CON-BASE");
        var classification = await EnsurePositionCategoryClassificationAsync(
            client,
            companyId,
            code: "CLASS-BASE",
            positionFunctionTypeId: functionType.Id,
            positionContractTypeId: contractType.Id,
            orgUnitTypeId: orgUnitType.Id);

        return await EnsurePositionCategoryAsync(client, companyId, "CAT-BASE", classification.Id);
    }

    private Task<PositionDescriptionCatalogItem> EnsureSalaryClassAsync(HttpClient client, Guid companyId, string code) =>
        EnsurePositionDescriptionCatalogItemAsync(client, companyId, "salary-classes", code);

    private async Task<HttpResponseMessage> PostJobProfileAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string title,
        Guid? orgUnitPublicId = null)
    {
        var orgUnit = orgUnitPublicId.HasValue
            ? (OrgUnitItem?)null
            : await CreateOrgUnitAsync(client, companyId, $"DIR-{code}", $"Unidad {title}", "Direccion");
        var positionCategory = await EnsureDefaultPositionCategoryAsync(client, companyId);

        return await client.PostJsonAsync($"/api/v1/companies/{companyId}/job-profiles", new
        {
            code,
            title,
            objective = "Objetivo",
            orgUnitPublicId = orgUnitPublicId ?? orgUnit!.Id,
            reportsToJobProfilePublicId = (Guid?)null,
            positionCategoryPublicId = positionCategory.Id,
            strategicObjectiveCatalogItemPublicId = (Guid?)null,
            assignedWorkEquipmentCatalogItemPublicId = (Guid?)null,
            responsibilityCatalogItemPublicId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false
        });
    }

    private async Task<JobProfileItem> CreateJobProfileAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string title,
        Guid? orgUnitPublicId = null)
    {
        var response = await PostJobProfileAsync(
            client,
            companyId,
            code,
            title,
            orgUnitPublicId);
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
        int maxEmployees)
    {
        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/position-slots", new
        {
            code,
            title,
            jobProfilePublicId = jobProfileId,
            workCenterPublicId = (Guid?)null,
            directDependencyPositionSlotPublicId = (Guid?)null,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
            status = "Vacant",
            maxEmployees,
            occupiedEmployees = 0,
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
        var salaryClass = await EnsureSalaryClassAsync(client, companyId, salaryClassCode);

        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/salary-tabulator/change-requests", new
        {
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            items = new[]
            {
                new
                {
                    salaryClassPublicId = salaryClass.Id,
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

    private async Task<Guid> GetSalaryTabulatorLineIdAsync(Guid companyId, Guid salaryClassId, string salaryScaleCode)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var salaryClassCode = await dbContext.PositionDescriptionCatalogItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.TenantId == companyId && item.PublicId == salaryClassId)
            .Select(item => item.Code)
            .SingleAsync();

        var normalizedSalaryScaleCode = salaryScaleCode.Trim().ToUpperInvariant();
        var lineId = await dbContext.SalaryTabulatorLines
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(line =>
                line.TenantId == companyId &&
                line.NormalizedSalaryClassCode == salaryClassCode &&
                line.NormalizedSalaryScaleCode == normalizedSalaryScaleCode &&
                line.IsActive)
            .OrderByDescending(line => line.EffectiveFromUtc)
            .Select(line => (Guid?)line.PublicId)
            .FirstOrDefaultAsync();

        Assert.NotNull(lineId);
        return lineId!.Value;
    }

    private async Task<Guid> GetEducationCatalogIdByCodeAsync(
        HttpClient client,
        Guid companyId,
        string routeSegment,
        string code)
    {
        var response = await client.GetAsync(
            $"/api/v1/companies/{companyId}/general-catalogs/{routeSegment}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelEducationCatalogLookupItem>>(JsonOptions);
        Assert.NotNull(payload);

        var item = payload!.SingleOrDefault(i => i.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(item);
        return item!.Id;
    }

    private static object CreateInitialLegalRepresentativePayload(
        string positionTitle = "Representante Legal",
        bool includeIsPrimary = true,
        bool? isPrimary = true) =>
        includeIsPrimary
            ? new
            {
                firstName = "Ana",
                lastName = "Mendoza",
                documentType = "TaxId",
                documentNumber = "0614-290190-102-3",
                positionTitle,
                representationType = "PrimaryLegalRepresentative",
                authorityDescription = "Representación general",
                appointmentInstrument = "Acta de nombramiento",
                appointmentDateUtc = DateTime.UtcNow.Date,
                effectiveFromUtc = DateTime.UtcNow.Date,
                effectiveToUtc = (DateTime?)null,
                email = "ana.mendoza@test.com",
                phone = "+50370000000",
                isPrimary
            }
            : new
            {
                firstName = "Ana",
                lastName = "Mendoza",
                documentType = "TaxId",
                documentNumber = "0614-290190-102-3",
                positionTitle,
                representationType = "PrimaryLegalRepresentative",
                authorityDescription = "Representación general",
                appointmentInstrument = "Acta de nombramiento",
                appointmentDateUtc = DateTime.UtcNow.Date,
                effectiveFromUtc = DateTime.UtcNow.Date,
                effectiveToUtc = (DateTime?)null,
                email = "ana.mendoza@test.com",
                phone = "+50370000000"
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

    private static async Task AssertSalaryTabulatorChangeRequestAllowedActionsAsync(
        HttpClient client,
        Guid companyId,
        Guid requestId,
        Action<JsonElement> assertAllowedActions)
    {
        var response = await client.GetAsync(
            $"/api/v1/companies/{companyId}/salary-tabulator/change-requests?page=1&pageSize=50&includeAllowedActions=true");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var matchingRequest = document.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Single(item => GetPublicIdentifier(item) == requestId);

        Assert.True(matchingRequest.TryGetProperty("allowedActions", out var allowedActions));
        assertAllowedActions(allowedActions);
    }

    private static Guid GetPublicIdentifier(JsonElement item) =>
        item.TryGetProperty("publicId", out var publicId)
            ? publicId.GetGuid()
            : item.GetProperty("id").GetGuid();

    private static void AssertWorkflowAllowedActions(
        JsonElement allowedActions,
        bool canEdit,
        bool canSubmit,
        bool canApprove,
        bool canReject,
        bool canCancel)
    {
        Assert.Equal(canEdit, allowedActions.GetProperty("canEdit").GetBoolean());
        Assert.Equal(canSubmit, allowedActions.GetProperty("canSubmit").GetBoolean());
        Assert.Equal(canApprove, allowedActions.GetProperty("canApprove").GetBoolean());
        Assert.Equal(canReject, allowedActions.GetProperty("canReject").GetBoolean());
        Assert.Equal(canCancel, allowedActions.GetProperty("canCancel").GetBoolean());
        Assert.False(allowedActions.GetProperty("canPublish").GetBoolean());
        Assert.False(allowedActions.GetProperty("canFinalize").GetBoolean());
    }

    private static void AssertNoWorkflowAllowedActions(JsonElement allowedActions) =>
        AssertWorkflowAllowedActions(
            allowedActions,
            canEdit: false,
            canSubmit: false,
            canApprove: false,
            canReject: false,
            canCancel: false);

    private static void AssertActionPermission(
        JsonElement allowedActions,
        string action,
        string permissionCode,
        bool allowed)
    {
        var actionPermission = allowedActions
            .GetProperty("actionPermissions")
            .EnumerateArray()
            .Single(item => item.GetProperty("action").GetString() == action);

        Assert.Equal(permissionCode, actionPermission.GetProperty("permissionCode").GetString());
        Assert.Equal(allowed, actionPermission.GetProperty("allowed").GetBoolean());
        Assert.Equal(JsonValueKind.Array, actionPermission.GetProperty("reasons").ValueKind);
    }

    private static async Task AssertFirstActiveSalaryLineAllowedActionsAsync(
        HttpResponseMessage response,
        bool expectedCanEdit,
        bool expectedCanInactivate)
    {
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);

        var foundActive = false;
        JsonElement activeLine = default;
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("isActive", out var isActiveElement) && isActiveElement.GetBoolean())
            {
                activeLine = item;
                foundActive = true;
                break;
            }
        }

        Assert.True(foundActive);
        var allowedActions = activeLine.GetProperty("allowedActions");
        Assert.Equal(expectedCanEdit, allowedActions.GetProperty("canEdit").GetBoolean());
        Assert.Equal(expectedCanInactivate, allowedActions.GetProperty("canInactivate").GetBoolean());
        AssertActionPermission(allowedActions, "edit", SalaryTabulatorPermissionCodes.Request, expectedCanEdit);
        AssertActionPermission(allowedActions, "inactivate", SalaryTabulatorPermissionCodes.Request, expectedCanInactivate);
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
        Guid PublicId,
        string Name,
        string Slug,
        string CountryCode,
        string Status,
        string PlanCode,
        bool IsActiveContext,
        bool IsOwnedByCurrentUser,
        DateTime CreatedAtUtc,
        CompanyTypeMetadataItem? CompanyType);

    private sealed record AccountCompanyDetailItem(
        Guid PublicId,
        string Name,
        string Slug,
        string CountryCode,
        string Status,
        string PlanCode,
        bool IsActiveContext,
        bool IsOwnedByCurrentUser,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc,
        IReadOnlyCollection<ActiveLegalRepresentativeItem> ActiveLegalRepresentatives,
        CompanyTypeMetadataItem? CompanyType);

    private sealed record CompanyTypeMetadataItem(
        Guid Id,
        string Code,
        string Name,
        bool IsActive);

    private sealed record ActiveLegalRepresentativeItem(
        Guid Id,
        string FullName,
        string RepresentationType,
        string PositionTitle,
        bool? IsPrimary);

    private sealed record LegalRepresentativePositionTitleCatalogItem(
        Guid Id,
        string Code,
        string Name,
        int SortOrder);

    private sealed record LegalRepresentativeRepresentationTypeCatalogItem(
        Guid Id,
        string Code,
        string Name,
        int SortOrder);

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
        bool? IsPrimary,
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
        bool? IsPrimary,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PersonnelFileIdentificationItem(
        [property: JsonPropertyName("identificationPublicId")] Guid Id,
        string IdentificationTypeCode,
        string? IdentificationTypeName,
        string IdentificationNumber,
        bool IsPrimary,
        Guid ConcurrencyToken);

    private sealed record PersonnelReferenceCatalogLookupItem(
        Guid Id,
        string Code,
        string Name,
        int SortOrder);

    private sealed record PersonnelEducationCatalogLookupItem(
        Guid Id,
        string Code,
        string Name);

    private sealed record PersonnelFileDocumentItem(
        Guid Id,
        string DocumentType,
        string FileName,
        string ContentType,
        string? FileUrl,
        int SizeBytes,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PersonnelFileDocumentDetailItem(
        Guid Id,
        string DocumentType,
        string? Observations,
        DateTime? DeliveryDate,
        DateTime? LoanDate,
        DateTime? ReturnDate,
        string FileName,
        string ContentType,
        string? FileUrl,
        int SizeBytes,
        bool IsActive,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record PersonnelFileObservationItem(
        Guid Id,
        Guid AuthorUserId,
        string Note,
        DateTime CreatedAtUtc);

    private sealed record PersonnelEducationCatalogReferenceItem(
        Guid Id,
        string Code,
        string Name,
        bool IsActive);

    private sealed record PersonnelFileEducationItem(
        Guid Id,
        PersonnelEducationCatalogReferenceItem Status,
        PersonnelEducationCatalogReferenceItem StudyType,
        PersonnelEducationCatalogReferenceItem Career,
        string Institution,
        bool IsCurrentlyStudying,
        PersonnelEducationCatalogReferenceItem? Shift,
        PersonnelEducationCatalogReferenceItem? Modality);

    private sealed record PersonnelFileLanguageItem(
        Guid Id,
        string LanguageCode,
        string LevelCode,
        bool Speaks,
        bool Writes,
        bool Reads);

    private sealed record PersonnelFileTrainingItem(
        Guid Id,
        string TrainingName,
        string TrainingTypeCode,
        decimal DurationValue,
        string DurationUnitCode,
        string? CostCurrencyCode);

    private sealed record PersonnelFilePreviousEmploymentItem(
        Guid PublicId,
        string Institution,
        DateTime EntryDate,
        string CurrencyCode);

    private sealed record PersonnelFileReferenceItem(
        Guid PublicId,
        string PersonName,
        string ReferenceTypeCode,
        decimal KnownTimeYears);

    private sealed record PersonnelFileFamilyMemberItem(
        [property: JsonPropertyName("familyMemberPublicId")] Guid Id,
        string FirstName,
        string LastName,
        string FullName,
        string KinshipCode,
        Guid ConcurrencyToken);

    private sealed record PersonnelFileAddressItem(
        [property: JsonPropertyName("addressPublicId")] Guid Id,
        string AddressLine,
        string? Country,
        string? Department,
        string? Municipality,
        string? PostalCode,
        bool IsCurrent,
        Guid ConcurrencyToken);

    private sealed record PersonnelFileEmergencyContactItem(
        [property: JsonPropertyName("emergencyContactPublicId")] Guid Id,
        string Name,
        string Relationship,
        string Phone,
        string? Address,
        string? Workplace,
        Guid ConcurrencyToken);

    private sealed record PersonnelFileListProjectionItem(
        Guid Id,
        string FullName,
        bool IsActive,
        DateTime CreatedAtUtc);

    private sealed record PersonnelFileItem(
        Guid Id,
        Guid CompanyId,
        string RecordType,
        string FirstName,
        string LastName,
        string FullName,
        DateTime BirthDate,
        int Age,
        bool IsActive,
        Guid ConcurrencyToken,
        IReadOnlyCollection<PersonnelFileIdentificationItem> Identifications,
        IReadOnlyCollection<PersonnelFileEducationItem> Educations,
        IReadOnlyCollection<PersonnelFileLanguageItem> Languages,
        IReadOnlyCollection<PersonnelFileTrainingItem> Trainings,
        IReadOnlyCollection<PersonnelFilePreviousEmploymentItem> PreviousEmployments,
        IReadOnlyCollection<PersonnelFileReferenceItem> References,
        IReadOnlyCollection<PersonnelFileDocumentItem> Documents);

    private sealed record PersonnelFileShellItem(
        Guid Id,
        Guid CompanyId,
        string RecordType,
        string LifecycleStatus,
        string FullName,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PersonnelFileSectionResultItem<TData>(
        TData Data,
        Guid PersonnelFileConcurrencyToken,
        DateTime? ModifiedAtUtc);

    private sealed record PersonnelFilePrintItem(
        DateTime GeneratedAtUtc,
        IReadOnlyCollection<string> IncludedSections,
        PersonnelFileItem PersonnelFile);

    private sealed record PersonnelFileDynamicGroupBucketItem(
        string Key,
        string Label,
        int Count);

    private sealed record PersonnelFileDynamicGroupItem(
        string Field,
        IReadOnlyCollection<PersonnelFileDynamicGroupBucketItem> Buckets);

    private sealed record PersonnelFileDynamicQueryItem(
        IReadOnlyCollection<PersonnelFileListProjectionItem> Items,
        IReadOnlyCollection<PersonnelFileDynamicGroupItem> Groups,
        int TotalCount,
        int PageNumber,
        int PageSize);

    private sealed record ActiveCompanyItem(
        Guid PublicId,
        string Name,
        string Slug,
        string CountryCode,
        string Status);

    private sealed record CountryCatalogItem(
        Guid Id,
        string Code,
        string Name,
        int SortOrder);

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

    private sealed record LocationGroupTreeItem(
        Guid Id,
        int LevelOrder,
        string Code,
        string Name,
        Guid? ParentId,
        string? Description,
        bool IsActive,
        bool IsDefault,
        Guid ConcurrencyToken,
        IReadOnlyCollection<LocationGroupTreeItem> Children);

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
        OrgUnitCatalogReferenceItem OrgUnitType,
        OrgUnitCatalogReferenceItem? FunctionalArea,
        OrgUnitCatalogReferenceItem? Parent,
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
        OrgUnitCatalogReferenceItem OrgUnitType,
        OrgUnitCatalogReferenceItem? FunctionalArea,
        Guid? ParentId,
        int? SortOrder,
        bool IsActive,
        IReadOnlyCollection<OrgUnitTreeNodeItem> Children);

    private sealed record OrgUnitCatalogReferenceItem(Guid Id, string Code, string Name);

    private sealed record OrgUnitGraphNodeItem(
        Guid Id,
        string Label,
        Guid OrgUnitTypeId,
        string OrgUnitTypeCode,
        string OrgUnitTypeName,
        bool IsActive);

    private sealed record OrgStructureCatalogItem(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        int SortOrder,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record OrgUnitGraphEdgeItem(Guid FromId, Guid ToId);

    private sealed record OrgUnitGraphItem(
        IReadOnlyCollection<OrgUnitGraphNodeItem> Nodes,
        IReadOnlyCollection<OrgUnitGraphEdgeItem> Edges);

    private sealed record JobProfileItem(
        Guid Id,
        string Code,
        string Title,
        JobProfileStatus Status,
        Guid? OrgUnitId,
        string? OrgUnitName,
        Guid ConcurrencyToken);

    private sealed record JobProfileEntityItem(
        Guid Id,
        string Code,
        string Title,
        JobProfileStatus Status,
        int Version,
        string? Objective,
        Guid OrgUnitId,
        Guid? ReportsToJobProfileId,
        Guid? PositionCategoryId,
        Guid? StrategicObjectiveCatalogItemId,
        Guid? AssignedWorkEquipmentCatalogItemId,
        Guid? ResponsibilityCatalogItemId,
        string? DecisionScope,
        string? AssignedResources,
        string? Responsibilities,
        string? BenefitsSummary,
        string? WorkingConditionSummary,
        string? MarketSalaryReference,
        string? ValuationNotes,
        DateTime? EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        bool IsActive,
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

    private sealed record PositionDescriptionCatalogItem(
        Guid Id,
        string Code,
        string Name,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PositionCategoryClassificationItem(
        Guid Id,
        string Code,
        string Name,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PositionCategoryItem(
        Guid Id,
        string Code,
        string Name,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record OccupationalPyramidLevelItem(
        Guid Id,
        Guid CompanyId,
        string Code,
        string Name,
        int LevelOrder,
        string? Description,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record CompetencyConductBehaviorItem(
        Guid BehaviorId,
        string BehaviorCode,
        string BehaviorName,
        string? Notes,
        int SortOrder);

    private sealed record CompetencyConductItem(
        Guid Id,
        Guid CompanyId,
        Guid CompetencyId,
        Guid CompetencyTypeId,
        Guid BehaviorLevelId,
        string Description,
        int SortOrder,
        bool IsActive,
        IReadOnlyCollection<CompetencyConductBehaviorItem> Behaviors,
        Guid ConcurrencyToken);

    private sealed record CompetencyConductListItem(
        Guid Id,
        Guid CompanyId,
        Guid CompetencyId,
        Guid CompetencyTypeId,
        Guid BehaviorLevelId,
        string Description,
        int SortOrder,
        bool IsActive);

    private sealed record JobProfileCompetencyMatrixConductItem(
        Guid ConductId,
        string Description,
        int SortOrder);

    private sealed record JobProfileCompetencyMatrixLineItem(
        Guid OccupationalPyramidLevelId,
        string OccupationalPyramidLevelCode,
        string OccupationalPyramidLevelName,
        int OccupationalPyramidLevelOrder,
        Guid CompetencyId,
        string CompetencyCode,
        string CompetencyName,
        Guid CompetencyTypeId,
        string CompetencyTypeCode,
        string CompetencyTypeName,
        Guid BehaviorLevelId,
        string BehaviorLevelCode,
        string BehaviorLevelName,
        string? ExpectedEvidence,
        int SortOrder,
        IReadOnlyCollection<JobProfileCompetencyMatrixConductItem> Conducts);

    private sealed record JobProfileCompetencyMatrixItem(
        Guid JobProfileId,
        string JobProfileCode,
        string JobProfileTitle,
        JobProfileStatus JobProfileStatus,
        int JobProfileVersion,
        Guid ConcurrencyToken,
        IReadOnlyCollection<JobProfileCompetencyMatrixLineItem> Items);

    private sealed record JobProfileCompetencyMatrixExportRowItem(
        Guid JobProfileId,
        string JobProfileCode,
        Guid OccupationalPyramidLevelId,
        Guid CompetencyId,
        Guid CompetencyTypeId,
        Guid BehaviorLevelId,
        Guid? ConductId,
        int ItemSortOrder);

    private sealed record PositionSlotListItem(
        Guid Id,
        string Code,
        PositionSlotStatus Status);

    private sealed record PositionSlotItem(
        Guid Id,
        string Code,
        string? Title,
        PositionSlotStatus Status,
        Guid JobProfileId,
        Guid OrgUnitId,
        string OrgUnitName,
        string? CostCenterCode,
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
        Guid? SalaryClassId,
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
