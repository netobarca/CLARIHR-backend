using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Banks;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class CompanyBankCatalogsIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();

    [Fact]
    public async Task CompanyBankCatalogs_Search_ShouldResolveActiveBanksByCompanyCountry()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/bank-catalogs?q=agr&page=1&pageSize=20");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PagedResponseEnvelope<CompanyBankCatalogItemEnvelope>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, item => item.Code == "BANCO_AGRICOLA");
        Assert.All(payload.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Name)));
    }

    [Fact]
    public async Task PersonnelFileBankAccounts_PutAndGet_ShouldUseBankPublicIdAndReturnEnrichedBankData()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName = "Ana",
            lastName = "Banco",
            birthDate = new DateTime(1992, 5, 6),
            maritalStatusCode = "SOLTERO_A",
            professionCode = "ANALISTA_DE_DATOS",
            nationality = "SV",
            personalEmail = "ana.banco@test.com",
            institutionalEmail = (string?)null,
            personalPhone = "+50370000011",
            institutionalPhone = (string?)null,
            birthCountryCode = "SV",
            birthDepartmentCode = "SAN_SALVADOR",
            birthMunicipalityCode = "SAN_SALVADOR_CENTRO",
            photoUrl = (string?)null,
            orgUnitPublicId = (Guid?)null,
            customDataJson = (string?)null
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<PersonnelFileCreatedEnvelope>(JsonOptions);
        Assert.NotNull(created);
        var personnelFileId = created!.Id;
        var concurrencyToken = created.ConcurrencyToken;

        var banksResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/bank-catalogs?q=agri&page=1&pageSize=20");
        banksResponse.EnsureSuccessStatusCode();

        var banks = await banksResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<CompanyBankCatalogItemEnvelope>>(JsonOptions);
        var selectedBank = Assert.Single(banks!.Items, item => item.Code == "BANCO_AGRICOLA");

        var replaceResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{personnelFileId}/bank-accounts", new
        {
            bankPublicId = selectedBank.PublicId,
            currencyCode = "USD",
            accountNumber = "0001-1111-2222",
            accountTypeCode = "SAVINGS",
            isPrimary = true,
            concurrencyToken
        });
        replaceResponse.EnsureSuccessStatusCode();

        var stored = await replaceResponse.Content.ReadFromJsonAsync<PersonnelFileBankAccountEnvelope>(JsonOptions);
        Assert.NotNull(stored);
        Assert.Equal(selectedBank.PublicId, stored!.BankPublicId);
        Assert.Equal("BANCO_AGRICOLA", stored.BankCode);
        Assert.Equal("Banco Agricola", stored.BankName);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var bank = await dbContext.BankCatalogItems.SingleAsync(item => item.PublicId == selectedBank.PublicId);
            bank.Inactivate();
            await dbContext.SaveChangesAsync();
        }

        var getResponse = await client.GetAsync($"/api/v1/personnel-files/{personnelFileId}/bank-accounts");
        getResponse.EnsureSuccessStatusCode();

        var getPayload = await getResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PersonnelFileBankAccountEnvelope>>(JsonOptions);
        var fetched = Assert.Single(getPayload!);
        Assert.Equal(selectedBank.PublicId, fetched.BankPublicId);
        Assert.Equal("Banco Agricola", fetched.BankName);
        Assert.Equal("Agricola", fetched.BankAlias);
    }

    private static TestUserContext CreatePersonnelFileAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin);

    private sealed record CompanyBankCatalogItemEnvelope(
        Guid PublicId,
        string Code,
        string Name,
        string? Alias,
        string? SwiftCode,
        string? RoutingCode,
        int SortOrder);

    private sealed record PersonnelFileBankAccountEnvelope(
        Guid Id,
        Guid? BankPublicId,
        string BankCode,
        string? BankName,
        string? BankAlias,
        string? SwiftCode,
        string? RoutingCode,
        string CurrencyCode,
        string AccountNumber,
        string AccountTypeCode,
        bool IsPrimary);

    private sealed record PersonnelFileCreatedEnvelope(
        Guid Id,
        Guid CompanyId,
        string RecordType,
        string FirstName,
        string LastName,
        string FullName,
        DateTime BirthDate,
        int Age,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PagedResponseEnvelope<TItem>(
        IReadOnlyCollection<TItem> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);

    private sealed record PersonnelFileSectionEnvelope<TData>(
        string Section,
        TData Data,
        Guid ConcurrencyToken);
}
