using System.Net;
using System.Net.Http.Json;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the vacation FUND vertical (vacaciones e incapacidades PR-7): the company-wide
/// mass generation (idempotent by employee-year; Art. 177 eligibility reported per row), the fund detail with
/// the Finanzas provision (pending × daily × 1.30), the profile's vacationDaysAvailable populated by the same
/// derivation, the manual period CRUD (duplicate → 422) and the no-consumption edit/delete guard (simulated by
/// seeding an approved request allocation directly, since the requests endpoints land in PR-8).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static TestUserContext CreateVacationManagerContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.Admin);

    private async Task SeedApprovedRequestConsumingPeriodAsync(
        IntegrationTestScenario scenario, Guid fileId, Guid periodPublicId, int days)
    {
        using var scope = factory.Services.CreateScope();
        var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
        using var _ = ambient.Push(scenario.TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fileInternalId = await dbContext.PersonnelFiles.Where(file => file.PublicId == fileId).Select(file => file.Id).FirstAsync();
        var periodInternalId = await dbContext.PersonnelFileVacationPeriods
            .Where(period => period.PublicId == periodPublicId).Select(period => period.Id).FirstAsync();

        var request = PersonnelFileVacationRequest.Create(
            null, "Sim Consumer", "seed-user",
            new DateOnly(2027, 7, 1), new DateOnly(2027, 7, days), days, planLinePublicId: null, notes: null);
        request.Approve([new VacationAllocationInput(periodInternalId, days)], "seed-hr", DateTime.UtcNow);
        request.BindToPersonnelFile(fileInternalId);
        request.SetTenantId(scenario.TenantId);
        dbContext.PersonnelFileVacationRequests.Add(request);
        await dbContext.SaveChangesAsync();
    }

    private async Task<VacationFundResponse> GetVacationFundAsync(HttpClient client, Guid fileId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/vacation-fund");
        response.EnsureSuccessStatusCode();
        var fund = await response.Content.ReadFromJsonAsync<VacationFundResponse>(JsonOptions);
        Assert.NotNull(fund);
        return fund!;
    }

    private async Task<decimal?> GetVacationDaysAvailableAsync(HttpClient client, Guid fileId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/employment-information");
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<PersonnelFileEmployeeProfileResponse>(JsonOptions);
        return profile?.VacationDaysAvailable;
    }

    [Fact]
    public async Task Vacation_Fund_MassGenerationIsIdempotentProvisionAndProfileBalanceAreExposed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var hr = factory.CreateClientFor(CreateVacationManagerContext(scenario));

        // Both employees are hired 2022-01-01 (SeedSettlementCandidate) with a 600 monthly base salary.
        var (e1, _) = await SeedSettlementCandidateAsync(scenario.TenantId, "Vera", "Vacaciones", "EMP-VAC-1", "vera.vac1@empresa.test");
        _ = await SeedSettlementCandidateAsync(scenario.TenantId, "Diego", "Descanso", "EMP-VAC-2", "diego.vac2@empresa.test");

        // [1] Generate the 2026 fund → both eligible (≥ 1 year of service by 2026-01-01) → created 2.
        var generate = await hr.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/vacation-periods/generate", new { year = 2026 });
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        var summary = await generate.Content.ReadFromJsonAsync<VacationPeriodGenerationSummary>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(2, summary!.TotalEmployees);
        Assert.Equal(2, summary.Created);
        Assert.Equal(0, summary.Skipped);
        Assert.Empty(summary.Errors);

        // [2] A second run is idempotent: nothing created, both skipped.
        var rerun = await hr.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/vacation-periods/generate", new { year = 2026 });
        var rerunSummary = await rerun.Content.ReadFromJsonAsync<VacationPeriodGenerationSummary>(JsonOptions);
        Assert.NotNull(rerunSummary);
        Assert.Equal(0, rerunSummary!.Created);
        Assert.Equal(2, rerunSummary.Skipped);

        // [3] Fund detail for E1: daily 600/30 = 20; one 2026 period; granted 15, pending 15,
        // provision 15 × 20 × 1.30 = 390.00.
        var fund = await GetVacationFundAsync(hr, e1);
        Assert.Equal(20.00m, fund.DailySalary);
        var period = Assert.Single(fund.Periods);
        Assert.Equal(2026, period.PeriodYear);
        Assert.Equal(15, period.TotalDaysGranted);
        Assert.Equal(0, period.EnjoyedDays);
        Assert.Equal(15, period.PendingDays);
        Assert.Equal(390.00m, period.ProvisionAmount);
        Assert.Equal(15, fund.TotalPendingDays);
        Assert.Equal(390.00m, fund.TotalProvisionAmount);

        // [4] The profile publishes vacationDaysAvailable via the same derivation (15, no consumption).
        Assert.Equal(15m, await GetVacationDaysAvailableAsync(hr, e1));

        // [5] Finanzas provision export: 200 + spreadsheet content-type + non-empty body.
        var export = await hr.GetAsync($"/api/v1/companies/{scenario.TenantId}/vacation-fund/export?format=xlsx&year=2026");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            export.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await export.Content.ReadAsByteArrayAsync());

        // [6] Generating the 2022 fund → nobody has a full year of service at 2022-01-01 → per-row errors.
        var ineligible = await hr.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/vacation-periods/generate", new { year = 2022 });
        var ineligibleSummary = await ineligible.Content.ReadFromJsonAsync<VacationPeriodGenerationSummary>(JsonOptions);
        Assert.NotNull(ineligibleSummary);
        Assert.Equal(0, ineligibleSummary!.Created);
        Assert.Equal(2, ineligibleSummary.Errors.Count);
        Assert.All(ineligibleSummary.Errors, error => Assert.Equal("VACATION_ELIGIBILITY_NOT_MET", error.Code));
    }

    [Fact]
    public async Task Vacation_Period_ManualCrudDuplicateAndConsumptionGuard()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var hr = factory.CreateClientFor(CreateVacationManagerContext(scenario));

        var (e1, _) = await SeedSettlementCandidateAsync(scenario.TenantId, "Paula", "Periodo", "EMP-VAC-P", "paula.vacp@empresa.test");

        // [1] Create a manual 2027 calendar-year period (15 legal + 5 benefit).
        var create = await hr.PostJsonAsync(
            $"/api/v1/personnel-files/{e1}/vacation-periods",
            new { periodYear = 2027, useAnniversary = false, legalDaysGranted = 15, benefitDaysGranted = 5, generatesEnjoymentDays = true });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var period = await create.Content.ReadFromJsonAsync<PersonnelFileVacationPeriodResponse>(JsonOptions);
        Assert.NotNull(period);
        Assert.Equal(2027, period!.PeriodYear);
        Assert.Equal(20, period.TotalDaysGranted);
        Assert.Equal("MANUAL", period.SourceCode);
        Assert.False(period.UsedAnniversary);

        // [2] A duplicate active period for the same year is rejected.
        var duplicate = await hr.PostJsonAsync(
            $"/api/v1/personnel-files/{e1}/vacation-periods",
            new { periodYear = 2027, useAnniversary = false, legalDaysGranted = 15, benefitDaysGranted = 0, generatesEnjoymentDays = true });
        await AssertProblemDetailsAsync(duplicate, HttpStatusCode.UnprocessableEntity, "VACATION_PERIOD_DUPLICATE");

        // [3] Edit the grants (no consumption yet) with If-Match → 20 legal / 0 benefit.
        var update = await SendSettlementAsync(
            hr, HttpMethod.Put, $"/api/v1/personnel-files/{e1}/vacation-periods/{period.Id}",
            period.ConcurrencyToken, new { legalDaysGranted = 20, benefitDaysGranted = 0 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<PersonnelFileVacationPeriodResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(20, updated!.LegalDaysGranted);
        Assert.Equal(0, updated.BenefitDaysGranted);

        // [4] Simulate consumption: an approved request allocating 3 days from this period.
        await SeedApprovedRequestConsumingPeriodAsync(scenario, e1, period.Id, 3);

        // The fund now reflects the consumption (enjoyed 3, pending 17) and so does the profile.
        var fund = await GetVacationFundAsync(hr, e1);
        var fundPeriod = Assert.Single(fund.Periods);
        Assert.Equal(3, fundPeriod.EnjoyedDays);
        Assert.Equal(17, fundPeriod.PendingDays);
        Assert.Equal(17m, await GetVacationDaysAvailableAsync(hr, e1));

        // [5] Editing or removing a consumed period is blocked (RF-016).
        var blockedUpdate = await SendSettlementAsync(
            hr, HttpMethod.Put, $"/api/v1/personnel-files/{e1}/vacation-periods/{period.Id}",
            updated.ConcurrencyToken, new { legalDaysGranted = 25, benefitDaysGranted = 0 });
        await AssertProblemDetailsAsync(blockedUpdate, HttpStatusCode.UnprocessableEntity, "VACATION_PERIOD_HAS_CONSUMPTION");

        var blockedDelete = await SendSettlementAsync(
            hr, HttpMethod.Delete, $"/api/v1/personnel-files/{e1}/vacation-periods/{period.Id}",
            updated.ConcurrencyToken, body: null);
        await AssertProblemDetailsAsync(blockedDelete, HttpStatusCode.UnprocessableEntity, "VACATION_PERIOD_HAS_CONSUMPTION");
    }
}
