using System.Net;
using System.Net.Http.Json;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Leave;
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

    // ── Requests (PR-8) ───────────────────────────────────────────────────────────────────────────

    private async Task<Guid> CreateManualVacationPeriodAsync(HttpClient client, Guid fileId, int year, int legal, int benefit = 0)
    {
        var create = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/vacation-periods",
            new { periodYear = year, useAnniversary = false, legalDaysGranted = legal, benefitDaysGranted = benefit, generatesEnjoymentDays = true });
        var payload = await create.Content.ReadAsStringAsync();
        Assert.True(create.StatusCode == HttpStatusCode.Created, $"Create period failed: {(int)create.StatusCode} {payload}");
        var period = await create.Content.ReadFromJsonAsync<PersonnelFileVacationPeriodResponse>(JsonOptions);
        return period!.Id;
    }

    private async Task SeedCompanyHolidayAsync(IntegrationTestScenario scenario, DateOnly date)
    {
        using var scope = factory.Services.CreateScope();
        var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
        using var _ = ambient.Push(scenario.TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var holiday = CompanyHoliday.Create(date, "Asueto de prueba", "NACIONAL");
        holiday.SetTenantId(scenario.TenantId);
        dbContext.CompanyHolidays.Add(holiday);
        await dbContext.SaveChangesAsync();
    }

    private async Task<PersonnelFileVacationRequestResponse> CreateVacationRequestAsync(
        HttpClient client, Guid fileId, string start, string end, int requestedDays)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/vacation-requests",
            new { startDate = start, endDate = end, requestedDays, planLinePublicId = (Guid?)null, notes = (string?)null });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Create request failed: {(int)response.StatusCode} {payload}");
        var created = await response.Content.ReadFromJsonAsync<PersonnelFileVacationRequestResponse>(JsonOptions);
        return created!;
    }

    private async Task<PersonnelFileVacationRequestResponse> ReadVacationRequestAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Unexpected status: {(int)response.StatusCode} {payload}");
        return (await response.Content.ReadFromJsonAsync<PersonnelFileVacationRequestResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task Vacation_Request_FifoApprovalPartialAndFullReturnRestoreTheBalance()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var hr = factory.CreateClientFor(CreateVacationManagerContext(scenario));

        var (e1, _) = await SeedSettlementCandidateAsync(scenario.TenantId, "Vito", "Vacaciones", "EMP-VR-1", "vito.vr1@empresa.test");

        // Two enjoyment periods (2025 + 2026, 15 each) → initial available 30.
        var p2025 = await CreateManualVacationPeriodAsync(hr, e1, 2025, 15);
        var p2026 = await CreateManualVacationPeriodAsync(hr, e1, 2026, 15);
        Assert.Equal(30m, await GetVacationDaysAvailableAsync(hr, e1));

        // [1] A valid request (Tue → Wed, not a holiday, not the Sunday rest day) for 20 days → SOLICITADA.
        var request = await CreateVacationRequestAsync(hr, e1, "2026-03-17", "2026-04-15", 20);
        Assert.Equal(VacationRequestStatuses.Solicitada, request.StatusCode);

        // [2] HR approves with the FIFO suggestion (no allocations) → 15 from 2025 + 5 from 2026, GOCE_VACACIONES journaled.
        var approved = await ReadVacationRequestAsync(await SendSettlementAsync(
            hr, HttpMethod.Patch, $"/api/v1/personnel-files/{e1}/vacation-requests/{request.Id}/decision",
            request.ConcurrencyToken, new { approve = true, allocations = (object?)null, notes = (string?)null }));
        Assert.Equal(VacationRequestStatuses.Aprobada, approved.StatusCode);
        Assert.Equal(20, approved.ConsumedDays);
        Assert.Equal(2, approved.Allocations.Count);
        Assert.Equal(15, approved.Allocations.Single(a => a.PeriodYear == 2025).Days);
        Assert.Equal(5, approved.Allocations.Single(a => a.PeriodYear == 2026).Days);
        Assert.Equal(1, await CountPersonnelActionsAsync(scenario.TenantId, e1, "GOCE_VACACIONES"));
        Assert.Equal(10m, await GetVacationDaysAvailableAsync(hr, e1));

        // [3] Partial return of 4 (LIFO → from 2026, the most recent allocation) → DEVUELTA_PARCIAL.
        var partial = await ReadVacationRequestAsync(await SendSettlementAsync(
            hr, HttpMethod.Post, $"/api/v1/personnel-files/{e1}/vacation-requests/{approved.Id}/returns",
            approved.ConcurrencyToken, new { days = 4, reason = "Reprogramación", distribution = (object?)null }));
        Assert.Equal(VacationRequestStatuses.DevueltaParcial, partial.StatusCode);
        Assert.Equal(4, partial.ReturnedDays);
        Assert.Equal(16, partial.NetConsumedDays);
        var partialReturn = Assert.Single(partial.Returns);
        var partialDistribution = Assert.Single(partialReturn.Distribution);
        Assert.Equal(2026, partialDistribution.PeriodYear);
        Assert.Equal(4, partialDistribution.Days);
        Assert.Equal(1, await CountPersonnelActionsAsync(scenario.TenantId, e1, "DEVOLUCION_VACACIONES"));
        Assert.Equal(14m, await GetVacationDaysAvailableAsync(hr, e1));

        // [4] Return the remaining 16 (LIFO spills 1 from 2026 + 15 from 2025) → DEVUELTA.
        var full = await ReadVacationRequestAsync(await SendSettlementAsync(
            hr, HttpMethod.Post, $"/api/v1/personnel-files/{e1}/vacation-requests/{partial.Id}/returns",
            partial.ConcurrencyToken, new { days = 16, reason = "Cancelación total", distribution = (object?)null }));
        Assert.Equal(VacationRequestStatuses.Devuelta, full.StatusCode);
        Assert.Equal(20, full.ReturnedDays);
        Assert.Equal(0, full.NetConsumedDays);
        Assert.Equal(2, await CountPersonnelActionsAsync(scenario.TenantId, e1, "DEVOLUCION_VACACIONES"));

        // [5] The fund balance is back to the initial 30.
        Assert.Equal(30m, await GetVacationDaysAvailableAsync(hr, e1));
        _ = p2025;
        _ = p2026;
    }

    [Fact]
    public async Task Vacation_Request_Art178SelfServiceAntiSelfAndFundInsufficientAtApproval()
    {
        var scenario = await factory.ResetDatabaseAsync();

        // The subject employee is linked to the acting user (self-service).
        var (self, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Sol", "Solicitante", "EMP-VR-S", "sol.vrs@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        using var hr = factory.CreateClientFor(
            TestUserContext.Authenticated(Guid.NewGuid(), scenario.TenantId, PersonnelFilePermissionCodes.Admin));
        using var selfClient = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        // One 2026 enjoyment period with 30 days (15 legal + 15 benefit).
        _ = await CreateManualVacationPeriodAsync(hr, self, 2026, 15, benefit: 15);
        await SeedCompanyHolidayAsync(scenario, new DateOnly(2026, 3, 16));

        // [Art. 178] self start on the holiday → 422.
        var onHoliday = await selfClient.PostJsonAsync(
            $"/api/v1/personnel-files/{self}/vacation-requests",
            new { startDate = "2026-03-16", endDate = "2026-03-18", requestedDays = 3, planLinePublicId = (Guid?)null, notes = (string?)null });
        await AssertProblemDetailsAsync(onHoliday, HttpStatusCode.UnprocessableEntity, "VACATION_START_ON_HOLIDAY_FORBIDDEN");

        // [Art. 178] self start on the Sunday rest day (2026-03-01) → 422.
        var onSunday = await selfClient.PostJsonAsync(
            $"/api/v1/personnel-files/{self}/vacation-requests",
            new { startDate = "2026-03-01", endDate = "2026-03-03", requestedDays = 3, planLinePublicId = (Guid?)null, notes = (string?)null });
        await AssertProblemDetailsAsync(onSunday, HttpStatusCode.UnprocessableEntity, "VACATION_START_ON_REST_DAY_FORBIDDEN");

        // Self creates a valid request → SOLICITADA, requester is the own file.
        var created = await CreateVacationRequestAsync(selfClient, self, "2026-03-17", "2026-03-20", 4);
        Assert.Equal(VacationRequestStatuses.Solicitada, created.StatusCode);
        Assert.Equal(self, created.RequesterFilePublicId);

        // Anti-self: the subject (now granted ManageVacations) cannot decide their own request → 403.
        using var selfManager = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.ManageVacations));
        var selfDecide = await SendSettlementAsync(
            selfManager, HttpMethod.Patch, $"/api/v1/personnel-files/{self}/vacation-requests/{created.Id}/decision",
            created.ConcurrencyToken, new { approve = true, allocations = (object?)null, notes = (string?)null });
        await AssertProblemDetailsAsync(selfDecide, HttpStatusCode.Forbidden, "VACATION_DECISION_SELF_FORBIDDEN");

        // HR approves it (consumes 4 → available 26).
        var approved = await ReadVacationRequestAsync(await SendSettlementAsync(
            hr, HttpMethod.Patch, $"/api/v1/personnel-files/{self}/vacation-requests/{created.Id}/decision",
            created.ConcurrencyToken, new { approve = true, allocations = (object?)null, notes = (string?)null }));
        Assert.Equal(VacationRequestStatuses.Aprobada, approved.StatusCode);
        Assert.Equal(26m, await GetVacationDaysAvailableAsync(hr, self));

        // [Fund insufficient at approval / race] request A (20 days) and B (10 days) both created while 26 available.
        var requestA = await CreateVacationRequestAsync(hr, self, "2026-05-04", "2026-05-29", 20);
        var requestB = await CreateVacationRequestAsync(hr, self, "2026-06-02", "2026-06-13", 10);

        // Approving B first drops the availability to 16.
        var approvedB = await ReadVacationRequestAsync(await SendSettlementAsync(
            hr, HttpMethod.Patch, $"/api/v1/personnel-files/{self}/vacation-requests/{requestB.Id}/decision",
            requestB.ConcurrencyToken, new { approve = true, allocations = (object?)null, notes = (string?)null }));
        Assert.Equal(VacationRequestStatuses.Aprobada, approvedB.StatusCode);
        Assert.Equal(16m, await GetVacationDaysAvailableAsync(hr, self));

        // Approving A now needs 20 but only 16 remain → 422 (re-verified inside the transaction).
        var approveA = await SendSettlementAsync(
            hr, HttpMethod.Patch, $"/api/v1/personnel-files/{self}/vacation-requests/{requestA.Id}/decision",
            requestA.ConcurrencyToken, new { approve = true, allocations = (object?)null, notes = (string?)null });
        await AssertProblemDetailsAsync(approveA, HttpStatusCode.UnprocessableEntity, "VACATION_FUND_INSUFFICIENT");
    }
}
