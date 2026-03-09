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

        var response = await client.PostAsJsonAsync("/api/auth/register", new
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
        var registerResponse = await anonymousClient.PostAsJsonAsync("/api/auth/register", new
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

        using var accountClient = factory.CreateClientFor(
            TestUserContext.Authenticated(registerPayload.User.Id, scenario.TenantId));

        var createCompanyResponse = await accountClient.PostAsJsonAsync("/api/account/companies", new
        {
            name = "First Access Company",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
        });
        Assert.Equal(HttpStatusCode.Created, createCompanyResponse.StatusCode);

        var companyPayload = await createCompanyResponse.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(companyPayload);

        var switchResponse = await accountClient.PostAsync(
            $"/api/account/companies/{companyPayload!.CompanyId}/switch",
            content: null);
        Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);

        var switchPayload = await switchResponse.Content.ReadFromJsonAsync<SwitchActiveCompanyItem>(JsonOptions);
        Assert.NotNull(switchPayload);
        Assert.Equal(companyPayload.CompanyId, switchPayload!.ActiveCompany.CompanyId);

        var switchToken = new JwtSecurityTokenHandler().ReadJwtToken(switchPayload.AccessToken);
        var tid = switchToken.Claims.Single(claim => claim.Type == "tid").Value;
        Assert.Equal(companyPayload.CompanyId.ToString(), tid);
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
        var scenario = await factory.ResetDatabaseAsync();
        using var anonymousClient = factory.CreateClient();

        var email = $"logout.user.{Guid.NewGuid():N}@clarihr.test";
        const string password = "StrongPass123!";

        var registerResponse = await anonymousClient.PostAsJsonAsync("/api/auth/register", new
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
    public async Task AccountCompanies_Update_WithCompanyType_ShouldReturnCompanyTypeMetadata()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var companyType = await CreateCompanyTypeAsync(client, "PRIVATE", "Private Company");

        var response = await client.PutAsJsonAsync($"/api/account/companies/{scenario.OtherTenantId}", new
        {
            name = "Acme Two Typed",
            companyTypeId = companyType.Id
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AccountCompanyDetailItem>(JsonOptions);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.CompanyType);
        Assert.Equal(companyType.Id, payload.CompanyType!.Id);
        Assert.Equal("PRIVATE", payload.CompanyType.Code);
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
    public async Task PersonnelFiles_CreateAndGet_ShouldReturnCreatedFile()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName = "Maria",
            lastName = "Rodriguez",
            birthDate = new DateTime(1992, 5, 6),
            maritalStatus = "SINGLE",
            profession = "Analyst",
            nationality = "SV",
            personalEmail = "maria.rodriguez@test.com",
            institutionalEmail = (string?)null,
            personalPhone = "+50370000001",
            institutionalPhone = (string?)null,
            birthCountry = "SV",
            birthDepartment = "San Salvador",
            birthMunicipality = "San Salvador",
            photoUrl = (string?)null,
            orgUnitId = (Guid?)null,
            customDataJson = "{ \"shirt_size\": \"M\" }",
            identifications = new[]
            {
                new
                {
                    identificationType = "DUI",
                    identificationNumber = "01234567-8",
                    issuedDate = (DateTime?)null,
                    expiryDate = (DateTime?)null,
                    issuer = (string?)null,
                    isPrimary = true
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<PersonnelFileItem>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Maria Rodriguez", created!.FullName);

        var getResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        getResponse.EnsureSuccessStatusCode();

        var file = await getResponse.Content.ReadFromJsonAsync<PersonnelFileItem>(JsonOptions);
        Assert.NotNull(file);
        Assert.Single(file!.Identifications);
        Assert.Equal("DUI", file.Identifications.First().IdentificationType);
    }

    [Fact]
    public async Task PersonnelFiles_Create_WithDuplicateIdentification_ShouldReturnConflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        async Task<HttpResponseMessage> CreateAsync(string firstName)
        {
            return await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
            {
                recordType = "Candidate",
                firstName,
                lastName = "Lopez",
                birthDate = new DateTime(1991, 1, 1),
                maritalStatus = "SINGLE",
                profession = "Analyst",
                nationality = "SV",
                personalEmail = (string?)null,
                institutionalEmail = (string?)null,
                personalPhone = "+50370000002",
                institutionalPhone = (string?)null,
                birthCountry = "SV",
                birthDepartment = "La Libertad",
                birthMunicipality = "Santa Tecla",
                photoUrl = (string?)null,
                orgUnitId = (Guid?)null,
                customDataJson = (string?)null,
                identifications = new[]
                {
                    new
                    {
                        identificationType = "DUI",
                        identificationNumber = "09999999-9",
                        issuedDate = (DateTime?)null,
                        expiryDate = (DateTime?)null,
                        issuer = (string?)null,
                        isPrimary = true
                    }
                }
            });
        }

        var firstResponse = await CreateAsync("Ana");
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await CreateAsync("Beatriz");
        await AssertProblemDetailsAsync(secondResponse, HttpStatusCode.Conflict, "PERSONNEL_FILE_IDENTIFICATION_CONFLICT");
    }

    [Fact]
    public async Task PersonnelFiles_DocumentUploadAndDownload_ShouldReturnFile()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Carlos", "Gomez", "DUI", "07777777-7");

        using var uploadContent = new MultipartFormDataContent();
        uploadContent.Add(new StringContent("DIPLOMA"), "documentType");
        uploadContent.Add(new StringContent("Adjunto de prueba"), "observations");
        uploadContent.Add(new StringContent(created.ConcurrencyToken.ToString()), "concurrencyToken");

        var fileBytes = Encoding.UTF8.GetBytes("hello personnel file");
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        uploadContent.Add(fileContent, "file", "proof.txt");

        var uploadResponse = await client.PostAsync($"/api/v1/personnel-files/{created.Id}/documents", uploadContent);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var document = await uploadResponse.Content.ReadFromJsonAsync<PersonnelFileDocumentItem>(JsonOptions);
        Assert.NotNull(document);

        var downloadResponse = await client.GetAsync($"/api/v1/personnel-file-documents/{document!.Id}/download");
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", downloadResponse.Content.Headers.ContentType?.MediaType);
        var downloaded = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileBytes, downloaded);
    }

    [Fact]
    public async Task PersonnelFiles_CurriculumSections_ShouldReplaceAndReturnUpdatedSections()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Carla", "Rivas", "DUI", "05555555-5");
        var concurrencyToken = created.ConcurrencyToken;

        var educationsResponse = await client.PutAsJsonAsync($"/api/v1/personnel-files/{created.Id}/educations", new
        {
            educations = new[]
            {
                new
                {
                    statusCode = "GRADUATED",
                    degreeTitle = "Ingenieria en Sistemas",
                    studyTypeCode = "BACHELOR",
                    career = "Ingenieria de Software",
                    institution = "Universidad CLARI",
                    countryCode = "SV",
                    specialty = (string?)null,
                    isCurrentlyStudying = false,
                    startDate = new DateTime(2015, 1, 10),
                    endDate = new DateTime(2020, 11, 30),
                    shiftCode = "MORNING",
                    modalityCode = "ONSITE",
                    totalSubjects = 50,
                    approvedSubjects = 50
                }
            },
            concurrencyToken
        });
        educationsResponse.EnsureSuccessStatusCode();
        var educationPayload = await educationsResponse.Content.ReadFromJsonAsync<PersonnelFileItem>(JsonOptions);
        Assert.NotNull(educationPayload);
        Assert.Single(educationPayload!.Educations);
        concurrencyToken = educationPayload.ConcurrencyToken;

        var languagesResponse = await client.PutAsJsonAsync($"/api/v1/personnel-files/{created.Id}/languages", new
        {
            languages = new[]
            {
                new
                {
                    languageCode = "ENGLISH",
                    levelCode = "ADVANCED",
                    speaks = true,
                    writes = true,
                    reads = true
                }
            },
            concurrencyToken
        });
        languagesResponse.EnsureSuccessStatusCode();
        var languagePayload = await languagesResponse.Content.ReadFromJsonAsync<PersonnelFileItem>(JsonOptions);
        Assert.NotNull(languagePayload);
        Assert.Single(languagePayload!.Languages);
        concurrencyToken = languagePayload.ConcurrencyToken;

        var trainingsResponse = await client.PutAsJsonAsync($"/api/v1/personnel-files/{created.Id}/trainings", new
        {
            trainings = new[]
            {
                new
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
                    costCurrencyCode = "USD"
                }
            },
            concurrencyToken
        });
        trainingsResponse.EnsureSuccessStatusCode();
        var trainingPayload = await trainingsResponse.Content.ReadFromJsonAsync<PersonnelFileItem>(JsonOptions);
        Assert.NotNull(trainingPayload);
        Assert.Single(trainingPayload!.Trainings);
        concurrencyToken = trainingPayload.ConcurrencyToken;

        var employmentsResponse = await client.PutAsJsonAsync($"/api/v1/personnel-files/{created.Id}/previous-employments", new
        {
            previousEmployments = new[]
            {
                new
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
                    currencyCode = "USD"
                }
            },
            concurrencyToken
        });
        employmentsResponse.EnsureSuccessStatusCode();
        var employmentPayload = await employmentsResponse.Content.ReadFromJsonAsync<PersonnelFileItem>(JsonOptions);
        Assert.NotNull(employmentPayload);
        Assert.Single(employmentPayload!.PreviousEmployments);
        concurrencyToken = employmentPayload.ConcurrencyToken;

        var referencesResponse = await client.PutAsJsonAsync($"/api/v1/personnel-files/{created.Id}/references", new
        {
            references = new[]
            {
                new
                {
                    personName = "Ana Martinez",
                    address = "Colonia Centro",
                    phone = "+50370002222",
                    referenceTypeCode = "PERSONAL",
                    occupation = "Manager",
                    workplace = "Empresa Uno",
                    workPhone = "+50370003333",
                    knownTimeYears = 3m
                }
            },
            concurrencyToken
        });
        referencesResponse.EnsureSuccessStatusCode();
        var referencePayload = await referencesResponse.Content.ReadFromJsonAsync<PersonnelFileItem>(JsonOptions);
        Assert.NotNull(referencePayload);
        Assert.Single(referencePayload!.References);

        var getResponse = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var file = await getResponse.Content.ReadFromJsonAsync<PersonnelFileItem>(JsonOptions);
        Assert.NotNull(file);
        Assert.Single(file!.Educations);
        Assert.Single(file.Languages);
        Assert.Single(file.Trainings);
        Assert.Single(file.PreviousEmployments);
        Assert.Single(file.References);
    }

    [Fact]
    public async Task PersonnelFiles_Print_WithSections_ShouldReturnFilteredPayload()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Diana", "Flores", "DUI", "04444444-4");
        var languageResponse = await client.PutAsJsonAsync($"/api/v1/personnel-files/{created.Id}/languages", new
        {
            languages = new[]
            {
                new
                {
                    languageCode = "ENGLISH",
                    levelCode = "ADVANCED",
                    speaks = true,
                    writes = true,
                    reads = true
                }
            },
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

        _ = await CreatePersonnelFileAsync(client, scenario.TenantId, "Ana", "Lopez", "DUI", "02222222-2", profession: "Tester");
        var designer = await CreatePersonnelFileAsync(client, scenario.TenantId, "Brenda", "Garcia", "DUI", "01111111-1", profession: "Designer");

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files?profession=Designer&sortBy=createdAtUtc&sortDirection=Desc&page=1&pageSize=20");
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

        var tester = await CreatePersonnelFileAsync(client, scenario.TenantId, "Raul", "Navas", "DUI", "06666666-6", profession: "Tester");
        var designer = await CreatePersonnelFileAsync(client, scenario.TenantId, "Sonia", "Mendez", "DUI", "07777777-0", profession: "Designer");

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/export?format=csv&profession=Designer&sortBy=fullName&sortDirection=Asc");
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

        _ = await CreatePersonnelFileAsync(client, scenario.TenantId, "Luis", "Alfaro", "DUI", "01010101-1", profession: "Engineer", maritalStatus: "SINGLE", nationality: "SV");
        _ = await CreatePersonnelFileAsync(client, scenario.TenantId, "Marta", "Ayala", "DUI", "02020202-2", profession: "Engineer", maritalStatus: "SINGLE", nationality: "SV");
        _ = await CreatePersonnelFileAsync(client, scenario.TenantId, "Nora", "Zelaya", "DUI", "03030303-3", profession: "Designer", maritalStatus: "MARRIED", nationality: "GT");

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files/dynamic-query", new
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
        Assert.Contains(maritalStatus.Buckets, bucket => bucket.Key == "SINGLE" && bucket.Count == 2);
        Assert.Contains(maritalStatus.Buckets, bucket => bucket.Key == "MARRIED" && bucket.Count == 1);

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
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, scenario.TenantId, "Direccion");

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-001",
            name = "Direccion General",
            orgUnitTypeId = orgUnitType.Id,
            functionalAreaId = (Guid?)null,
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
        Assert.Equal(orgUnitType.Id, payload.OrgUnitType.Id);
        Assert.Equal("Direccion", payload.OrgUnitType.Code);
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
            orgUnitTypeId = unit.OrgUnitType.Id,
            functionalAreaId = unit.FunctionalArea?.Id,
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
    public async Task OrgStructureCatalogs_FunctionalAreas_Inactivate_WhenInUse_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateOrgUnitAdminContext(scenario));

        var orgUnitType = await EnsureOrgUnitTypeAsync(client, scenario.TenantId, "Direccion");
        var functionalArea = await EnsureFunctionalAreaAsync(client, scenario.TenantId, "ADMIN");

        var createOrgUnitResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-FA-USE",
            name = "Direccion Funcional",
            orgUnitTypeId = orgUnitType.Id,
            functionalAreaId = functionalArea.Id,
            parentId = (Guid?)null,
            sortOrder = 1,
            description = (string?)null,
            costCenterCode = (string?)null,
            managerEmployeeId = (Guid?)null
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

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-001",
            name = "Direccion General",
            orgUnitTypeId = orgUnitType.Id,
            functionalAreaId = (Guid?)null,
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
    public async Task CompetencyFramework_FullFlow_ShouldManagePyramidConductMatrixAndExports()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminWithAuditContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-CF-001", "Perfil Competencias");
        var competency = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.Competency, "COMP-CF-001", "Liderazgo Integral");
        var competencyType = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.CompetencyType, "CTYPE-CF-001", "Gerencial");
        var behaviorLevel = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.BehaviorLevel, "BLEVEL-CF-001", "Estrategico");
        var behavior = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.Behavior, "BEHAV-CF-001", "Comunica objetivos y resultados");

        var levelResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/occupational-pyramid-levels", new
        {
            code = "OPL-CF-001",
            name = "Nivel Estrategico CF",
            levelOrder = 1,
            description = "Nivel semilla para pruebas E2E."
        });
        levelResponse.EnsureSuccessStatusCode();
        var level = await levelResponse.Content.ReadFromJsonAsync<OccupationalPyramidLevelItem>(JsonOptions);
        Assert.NotNull(level);

        var conductResponse = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/competency-conducts", new
        {
            competencyId = competency.Id,
            competencyTypeId = competencyType.Id,
            behaviorLevelId = behaviorLevel.Id,
            description = "Alinea decisiones con objetivos institucionales.",
            sortOrder = 1
        });
        conductResponse.EnsureSuccessStatusCode();
        var conduct = await conductResponse.Content.ReadFromJsonAsync<CompetencyConductItem>(JsonOptions);
        Assert.NotNull(conduct);

        var conductBehaviorResponse = await client.PutAsJsonAsync($"/api/v1/competency-conducts/{conduct!.Id}/behaviors", new
        {
            behaviors = new[]
            {
                new
                {
                    behaviorId = behavior.Id,
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

        var matrixBeforeResponse = await client.GetAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix");
        matrixBeforeResponse.EnsureSuccessStatusCode();
        var matrixBefore = await matrixBeforeResponse.Content.ReadFromJsonAsync<JobProfileCompetencyMatrixItem>(JsonOptions);
        Assert.NotNull(matrixBefore);
        Assert.Empty(matrixBefore!.Items);

        var matrixUpdateResponse = await client.PutAsJsonAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix", new
        {
            items = new[]
            {
                new
                {
                    occupationalPyramidLevelId = level!.Id,
                    competencyId = competency.Id,
                    competencyTypeId = competencyType.Id,
                    behaviorLevelId = behaviorLevel.Id,
                    conductIds = new[] { conductWithBehaviors.Id },
                    expectedEvidence = "Resultados comprobables en objetivos institucionales.",
                    sortOrder = 1
                }
            },
            concurrencyToken = matrixBefore.ConcurrencyToken
        });
        matrixUpdateResponse.EnsureSuccessStatusCode();
        var matrixUpdated = await matrixUpdateResponse.Content.ReadFromJsonAsync<JobProfileCompetencyMatrixItem>(JsonOptions);
        Assert.NotNull(matrixUpdated);
        Assert.Single(matrixUpdated!.Items);
        var matrixItem = Assert.Single(matrixUpdated.Items);
        Assert.Equal(level.Id, matrixItem.OccupationalPyramidLevelId);
        Assert.Equal(competency.Id, matrixItem.CompetencyId);
        Assert.Single(matrixItem.Conducts);

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

        var response = await client.PutAsJsonAsync($"/api/v1/job-profiles/{profile.Id}/competency-matrix", new
        {
            items = Array.Empty<object>(),
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
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
            orgUnitTypeCode: "Direccion",
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
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, scenario.TenantId, "Direccion");

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/org-units", new
        {
            code = "DIR-CC-INV",
            name = "Direccion Invalida",
            orgUnitTypeId = orgUnitType.Id,
            functionalAreaId = (Guid?)null,
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

        var createResponse = await requesterClient.PostAsJsonAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator/change-requests", new
        {
            reason = "Ajuste anual",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassId = salaryClass.Id,
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
                    salaryClassId = salaryClass.Id,
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

        var listLinesResponse = await approverClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/salary-tabulator?salaryClassId={salaryClass.Id}&page=1&pageSize=20");
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

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/salary-tabulator/change-requests/{created.Id}", new
        {
            reason = "stale",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassId = salaryClass.Id,
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
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin);

    private static TestUserContext CreateJobProfileAdminWithCatalogContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            JobProfilePermissionCodes.Admin,
            JobProfilePermissionCodes.CatalogAdmin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin);

    private static TestUserContext CreateJobProfileAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.AuditLogs, RbacPermissionAction.Read));

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
            PositionDescriptionCatalogPermissionCodes.Admin);

    private static TestUserContext CreatePositionSlotAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PositionSlotPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
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
            PositionDescriptionCatalogPermissionCodes.Admin);

    private static TestUserContext CreateCostCenterAdminWithAuditContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            CostCenterPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            PositionSlotPermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
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
        return Assert.Single(payload!.Items, static group => group.IsDefault);
    }

    private async Task<OrgUnitItem> CreateOrgUnitAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string name,
        string orgUnitTypeCode,
        Guid? parentId = null,
        int? sortOrder = 1,
        string? costCenterCode = null)
    {
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, companyId, orgUnitTypeCode);

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/org-units", new
        {
            code,
            name,
            orgUnitTypeId = orgUnitType.Id,
            functionalAreaId = (Guid?)null,
            parentId,
            sortOrder,
            description = (string?)null,
            costCenterCode,
            managerEmployeeId = (Guid?)null
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

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/org-structure-catalogs/unit-types", new
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

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/org-structure-catalogs/functional-areas", new
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

    private async Task<OrgStructureCatalogItem> CreateCompanyTypeAsync(HttpClient client, string code, string name)
    {
        var createResponse = await client.PostAsJsonAsync("/api/account/org-structure-catalogs/company-types", new
        {
            code,
            name,
            description = (string?)null,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<OrgStructureCatalogItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
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

    private async Task<PersonnelFileItem> CreatePersonnelFileAsync(
        HttpClient client,
        Guid companyId,
        string firstName,
        string lastName,
        string identificationType,
        string identificationNumber,
        string profession = "Tester",
        string maritalStatus = "SINGLE",
        string nationality = "SV")
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName,
            lastName,
            birthDate = new DateTime(1990, 3, 3),
            maritalStatus,
            profession,
            nationality,
            personalEmail = (string?)null,
            institutionalEmail = (string?)null,
            personalPhone = "+50370001000",
            institutionalPhone = (string?)null,
            birthCountry = "SV",
            birthDepartment = "San Salvador",
            birthMunicipality = "San Salvador",
            photoUrl = (string?)null,
            orgUnitId = (Guid?)null,
            customDataJson = (string?)null,
            identifications = new[]
            {
                new
                {
                    identificationType,
                    identificationNumber,
                    issuedDate = (DateTime?)null,
                    expiryDate = (DateTime?)null,
                    issuer = (string?)null,
                    isPrimary = true
                }
            }
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PersonnelFileItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<JobCatalogItemItem> CreateJobCatalogItemAsync(
        HttpClient client,
        Guid companyId,
        JobCatalogCategory category,
        string code,
        string name)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/job-catalogs/{category}", new
        {
            code,
            name
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JobCatalogItemItem>(JsonOptions);
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
        var listResponse = await client.GetAsync($"/api/v1/companies/{companyId}/{routeSegment}?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<PositionDescriptionCatalogItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/{routeSegment}", new
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

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/position-category-classifications", new
        {
            code,
            name = code,
            description = (string?)null,
            positionFunctionTypeId,
            positionContractTypeId,
            orgUnitTypeId,
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

        var createResponse = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/position-categories", new
        {
            code,
            name = code,
            description = (string?)null,
            classificationId,
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

    private async Task<JobProfileItem> CreateJobProfileAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string title)
    {
        var positionCategory = await EnsureDefaultPositionCategoryAsync(client, companyId);

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/job-profiles", new
        {
            code,
            title,
            objective = "Objetivo",
            orgUnitId = (Guid?)null,
            reportsToJobProfileId = (Guid?)null,
            positionCategoryId = positionCategory.Id,
            strategicObjectiveCatalogItemId = (Guid?)null,
            assignedWorkEquipmentCatalogItemId = (Guid?)null,
            responsibilityCatalogItemId = (Guid?)null,
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
                    requirementTypeCatalogItemId = (Guid?)null,
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
                    frequencyCatalogItemId = (Guid?)null,
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

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/salary-tabulator/change-requests", new
        {
            reason = "Ajuste",
            effectiveFromUtc = DateTime.UtcNow.Date,
            items = new[]
            {
                new
                {
                    salaryClassId = salaryClass.Id,
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
        DateTime CreatedAtUtc,
        CompanyTypeMetadataItem? CompanyType);

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

    private sealed record PersonnelFileIdentificationItem(
        Guid Id,
        string IdentificationType,
        string IdentificationNumber,
        bool IsPrimary);

    private sealed record PersonnelFileDocumentItem(
        Guid Id,
        string DocumentType,
        string FileName,
        string ContentType,
        int SizeBytes,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PersonnelFileEducationItem(
        Guid Id,
        string StatusCode,
        string StudyTypeCode,
        string Career,
        string Institution,
        bool IsCurrentlyStudying);

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
        Guid Id,
        string Institution,
        DateTime EntryDate,
        string CurrencyCode);

    private sealed record PersonnelFileReferenceItem(
        Guid Id,
        string PersonName,
        string ReferenceTypeCode,
        decimal KnownTimeYears);

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
        OrgUnitCatalogReferenceItem OrgUnitType,
        OrgUnitCatalogReferenceItem? FunctionalArea,
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
