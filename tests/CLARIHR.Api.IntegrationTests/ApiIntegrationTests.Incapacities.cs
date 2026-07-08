using System.Net;
using System.Net.Http.Json;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the incapacities END-TO-END slice (vacaciones e incapacidades PR-5): the RRHH
/// round-trip (create-with-constancia → engine breakdown + INCAPACIDAD journal → extension continuing the
/// chain → annul reverting the employer cap), the self-service round-trip (EN_REVISION → HR confirm →
/// anti-self 403 / missing-constancia 422 / cross-file 403) and the employer-cap exhaustion (3×3 days consume
/// the 9-day cap; the 4th event is reclassified to unpaid with the CAP_EXHAUSTED warning) — with the
/// incapacity-balance endpoint and the profile's disabilityDaysAvailable cuadrando at every step.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static TestUserContext CreateIncapacityManagerContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.Admin);

    private async Task ApplyLeaveTemplateAsync(IntegrationTestScenario scenario)
    {
        using var scope = factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ILeaveTemplateSeeder>();
        _ = await seeder.ApplyTemplateAsync(scenario.TenantId, 2026, CancellationToken.None);
    }

    private async Task<(Guid RiskId, Guid TypeId)> GetIncapacityMasterIdsAsync(Guid tenantId, string riskCode = "ENFERMEDAD_COMUN")
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var riskId = await dbContext.IncapacityRisks
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Code == riskCode)
            .Select(item => item.PublicId)
            .FirstAsync();
        var typeId = await dbContext.IncapacityTypes
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id)
            .Select(item => item.PublicId)
            .FirstAsync();
        return (riskId, typeId);
    }

    private async Task<Guid> SeedIncapacityDocumentFileAsync(IntegrationTestScenario scenario)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = StoredFile.Create(
            "constancia.pdf",
            "application/pdf",
            2048,
            ".pdf",
            StorageProvider.AzureBlob,
            "clarihr-incapacity-documents",
            $"incapacity-documents/{Guid.NewGuid():N}.pdf",
            FilePurpose.IncapacityDocument,
            FileUploadType.DirectUpload,
            scenario.ActorUserId.ToString());
        file.SetTenantId(scenario.TenantId);
        file.MarkActive(2048, "application/pdf");
        dbContext.Set<StoredFile>().Add(file);
        await dbContext.SaveChangesAsync();
        return file.PublicId;
    }

    private async Task<int> CountPersonnelActionsAsync(Guid tenantId, Guid filePublicId, string actionTypeCode)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Set<PersonnelFilePersonnelAction>()
            .IgnoreQueryFilters()
            .Where(action => action.TenantId == tenantId
                && action.PersonnelFile.PublicId == filePublicId
                && action.ActionTypeCode == actionTypeCode)
            .CountAsync();
    }

    private static object BuildIncapacityBody(Guid riskId, Guid typeId, string start, string? end, Guid? documentFilePublicId) =>
        new
        {
            riskPublicId = riskId,
            incapacityTypePublicId = typeId,
            medicalClinicPublicId = (Guid?)null,
            assignedPositionPublicId = (Guid?)null,
            payrollTypeCode = (string?)null,
            payrollPeriodDefinitionPublicId = (Guid?)null,
            startDate = start,
            endDate = end,
            notes = (string?)null,
            documentFilePublicId,
            documentTypeCatalogItemPublicId = (Guid?)null,
            documentObservations = (string?)null
        };

    private async Task<PersonnelFileIncapacityResponse> CreateIncapacityAsync(
        HttpClient client, Guid fileId, Guid riskId, Guid typeId, string start, string end, Guid documentFilePublicId)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/incapacities",
            BuildIncapacityBody(riskId, typeId, start, end, documentFilePublicId));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Create incapacity failed: {(int)response.StatusCode} {payload}");
        var created = await response.Content.ReadFromJsonAsync<PersonnelFileIncapacityResponse>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<PersonnelFileIncapacityBalanceResponse> GetIncapacityBalanceAsync(HttpClient client, Guid fileId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/incapacity-balance");
        response.EnsureSuccessStatusCode();
        var balance = await response.Content.ReadFromJsonAsync<PersonnelFileIncapacityBalanceResponse>(JsonOptions);
        Assert.NotNull(balance);
        return balance!;
    }

    private async Task<decimal?> GetDisabilityDaysAvailableAsync(HttpClient client, Guid fileId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/employment-information");
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<PersonnelFileEmployeeProfileResponse>(JsonOptions);
        return profile?.DisabilityDaysAvailable;
    }

    [Fact]
    public async Task Incapacity_HrRoundTrip_CreatesBreakdownJournalsExtensionAndAnnuls()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await ApplyLeaveTemplateAsync(scenario);
        using var client = factory.CreateClientFor(CreateIncapacityManagerContext(scenario));

        var (fileId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Ingrid", "Incapacitada", "EMP-INC-A", "ingrid.inc.a@empresa.test");
        var (riskId, typeId) = await GetIncapacityMasterIdsAsync(scenario.TenantId);
        var documentFileId = await SeedIncapacityDocumentFileAsync(scenario);

        // [1] Register a 3-day ENFERMEDAD_COMUN (days 1-3 EMPRESA 75%; salary 600 → daily 20, patrono 45).
        var created = await CreateIncapacityAsync(client, fileId, riskId, typeId, "2026-03-04", "2026-03-06", documentFileId);
        Assert.Equal(IncapacityStatuses.Registrada, created.StatusCode);
        Assert.Equal(3, created.CalendarDays);
        Assert.Equal(3, created.ComputableDays);
        Assert.Equal(3, created.EmployerDays);
        Assert.Equal(0, created.SubsidizedDays);
        Assert.Equal(20.00m, created.DailySalary);
        Assert.Equal(45.00m, created.EmployerAmount);
        Assert.Equal(600m, created.MonthlyBaseSalary);
        Assert.Equal(1, await CountPersonnelActionsAsync(scenario.TenantId, fileId, "INCAPACIDAD"));

        // Balance after the first event: 9 − 3 = 6, mirrored on the profile.
        var balanceAfterCreate = await GetIncapacityBalanceAsync(client, fileId);
        Assert.Equal(9, balanceAfterCreate.TotalCapDays);
        Assert.Equal(3, balanceAfterCreate.ConsumedEmployerDays);
        Assert.Equal(6, balanceAfterCreate.RemainingDays);
        Assert.Equal(6m, await GetDisabilityDaysAvailableAsync(client, fileId));

        // [2] Extension: starts on 2026-03-07 (source end + 1); its chain numbering continues at day 4,
        // so all its days fall in the ISSS tranche (subsidy > 0, no employer cap consumption).
        var extensionResponse = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/incapacities/{created.Id}/extensions",
            new
            {
                riskPublicId = riskId,
                incapacityTypePublicId = typeId,
                medicalClinicPublicId = (Guid?)null,
                assignedPositionPublicId = (Guid?)null,
                payrollTypeCode = (string?)null,
                payrollPeriodDefinitionPublicId = (Guid?)null,
                endDate = "2026-03-09",
                notes = (string?)null,
                documentFilePublicId = documentFileId,
                documentTypeCatalogItemPublicId = (Guid?)null,
                documentObservations = (string?)null
            });
        var extensionPayload = await extensionResponse.Content.ReadAsStringAsync();
        Assert.True(extensionResponse.StatusCode == HttpStatusCode.Created, $"Extension failed: {(int)extensionResponse.StatusCode} {extensionPayload}");
        var extension = await extensionResponse.Content.ReadFromJsonAsync<PersonnelFileIncapacityResponse>(JsonOptions);
        Assert.NotNull(extension);
        Assert.Equal(created.Id, extension!.ExtendsIncapacityPublicId);
        Assert.Equal(3, extension.SubsidizedDays);
        Assert.Equal(0, extension.EmployerDays);
        Assert.Equal(45.00m, extension.SubsidyAmount);
        Assert.Equal(1, await CountPersonnelActionsAsync(scenario.TenantId, fileId, "PRORROGA_INCAPACIDAD"));

        // The extension consumed no employer cap, so the balance is unchanged at 6.
        Assert.Equal(6, (await GetIncapacityBalanceAsync(client, fileId)).RemainingDays);

        // [3] Annul tail-first: the source cannot be annulled while a live extension exists.
        var lockedAnnul = await SendSettlementAsync(
            client, HttpMethod.Patch, $"/api/v1/personnel-files/{fileId}/incapacities/{created.Id}/annulment",
            created.ConcurrencyToken, new { reason = "Registro duplicado" });
        await AssertProblemDetailsAsync(lockedAnnul, HttpStatusCode.UnprocessableEntity, "INCAPACITY_CHAIN_LOCKED");

        var annulExtension = await SendSettlementAsync(
            client, HttpMethod.Patch, $"/api/v1/personnel-files/{fileId}/incapacities/{extension.Id}/annulment",
            extension.ConcurrencyToken, new { reason = "Prórroga anulada" });
        Assert.Equal(HttpStatusCode.OK, annulExtension.StatusCode);

        var annulSource = await SendSettlementAsync(
            client, HttpMethod.Patch, $"/api/v1/personnel-files/{fileId}/incapacities/{created.Id}/annulment",
            created.ConcurrencyToken, new { reason = "Registro anulado" });
        Assert.Equal(HttpStatusCode.OK, annulSource.StatusCode);

        // Annulment reverts the employer cap: nothing REGISTRADA remains, so remaining is back to 9.
        var balanceAfterAnnul = await GetIncapacityBalanceAsync(client, fileId);
        Assert.Equal(0, balanceAfterAnnul.ConsumedEmployerDays);
        Assert.Equal(9, balanceAfterAnnul.RemainingDays);
        Assert.Equal(9m, await GetDisabilityDaysAvailableAsync(client, fileId));
    }

    [Fact]
    public async Task Incapacity_SelfService_ConfirmationAntiSelfAndConstanciaGuards()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await ApplyLeaveTemplateAsync(scenario);

        // The subject employee is linked to the acting user (self-service).
        var (fileId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Sara", "Autoservicio", "EMP-INC-S", "sara.inc.s@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);
        var (otherFileId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Otra", "Persona", "EMP-INC-O", "otra.inc.o@empresa.test");
        var (riskId, typeId) = await GetIncapacityMasterIdsAsync(scenario.TenantId);
        var documentFileId = await SeedIncapacityDocumentFileAsync(scenario);

        using var selfClient = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));
        using var hrClient = factory.CreateClientFor(
            TestUserContext.Authenticated(Guid.NewGuid(), scenario.TenantId, PersonnelFilePermissionCodes.Admin));

        // Self-service creation on the own file → EN_REVISION (does not consume the cap yet).
        var selfCreate = await selfClient.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/incapacities",
            BuildIncapacityBody(riskId, typeId, "2026-03-04", "2026-03-06", documentFileId));
        Assert.Equal(HttpStatusCode.Created, selfCreate.StatusCode);
        var selfRegistered = await selfCreate.Content.ReadFromJsonAsync<PersonnelFileIncapacityResponse>(JsonOptions);
        Assert.NotNull(selfRegistered);
        Assert.Equal(IncapacityStatuses.EnRevision, selfRegistered!.StatusCode);
        Assert.Equal("AUTOSERVICIO", selfRegistered.OriginCode);
        Assert.Equal(0, (await GetIncapacityBalanceAsync(hrClient, fileId)).ConsumedEmployerDays);

        // Missing constancia → 422 (the preference requires a document by default).
        var missingDoc = await selfClient.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/incapacities",
            BuildIncapacityBody(riskId, typeId, "2026-04-06", "2026-04-08", documentFilePublicId: null));
        await AssertProblemDetailsAsync(missingDoc, HttpStatusCode.UnprocessableEntity, "INCAPACITY_DOCUMENT_REQUIRED");

        // Self-service on someone else's file → 403 (not the owner, no manage permission).
        var crossFile = await selfClient.PostJsonAsync(
            $"/api/v1/personnel-files/{otherFileId}/incapacities",
            BuildIncapacityBody(riskId, typeId, "2026-03-04", "2026-03-06", documentFileId));
        Assert.Equal(HttpStatusCode.Forbidden, crossFile.StatusCode);

        // HR (a different user) confirms → REGISTRADA and consumes the cap.
        var confirm = await SendSettlementAsync(
            hrClient, HttpMethod.Patch, $"/api/v1/personnel-files/{fileId}/incapacities/{selfRegistered.Id}/confirmation",
            selfRegistered.ConcurrencyToken, body: null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var confirmed = await confirm.Content.ReadFromJsonAsync<PersonnelFileIncapacityResponse>(JsonOptions);
        Assert.NotNull(confirmed);
        Assert.Equal(IncapacityStatuses.Registrada, confirmed!.StatusCode);
        Assert.Equal(3, (await GetIncapacityBalanceAsync(hrClient, fileId)).ConsumedEmployerDays);
        Assert.Equal(1, await CountPersonnelActionsAsync(scenario.TenantId, fileId, "INCAPACIDAD"));

        // Anti-self: a second self-registration, confirmed by the subject employee (who now has manage), → 403.
        var secondSelf = await selfClient.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/incapacities",
            BuildIncapacityBody(riskId, typeId, "2026-05-04", "2026-05-06", documentFileId));
        var secondSelfRegistered = await secondSelf.Content.ReadFromJsonAsync<PersonnelFileIncapacityResponse>(JsonOptions);
        Assert.NotNull(secondSelfRegistered);

        using var selfManagerClient = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.ManageIncapacities));
        var selfConfirm = await SendSettlementAsync(
            selfManagerClient, HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/incapacities/{secondSelfRegistered!.Id}/confirmation",
            secondSelfRegistered.ConcurrencyToken, body: null);
        await AssertProblemDetailsAsync(selfConfirm, HttpStatusCode.Forbidden, "INCAPACITY_CONFIRM_SELF_FORBIDDEN");
    }

    [Fact]
    public async Task Incapacity_EmployerCap_ExhaustsAtNineDaysAndReclassifiesTheFourthEvent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await ApplyLeaveTemplateAsync(scenario);
        using var client = factory.CreateClientFor(CreateIncapacityManagerContext(scenario));

        var (fileId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Tomás", "Tope", "EMP-INC-T", "tomas.inc.t@empresa.test");
        var (riskId, typeId) = await GetIncapacityMasterIdsAsync(scenario.TenantId);
        var documentFileId = await SeedIncapacityDocumentFileAsync(scenario);

        // Three 3-day events (all EMPRESA days 1-3) exhaust the 9-day employer cap.
        var event1 = await CreateIncapacityAsync(client, fileId, riskId, typeId, "2026-03-02", "2026-03-04", documentFileId);
        Assert.Equal(3, event1.EmployerDays);
        Assert.Equal(6, (await GetIncapacityBalanceAsync(client, fileId)).RemainingDays);

        _ = await CreateIncapacityAsync(client, fileId, riskId, typeId, "2026-03-05", "2026-03-07", documentFileId);
        var event3 = await CreateIncapacityAsync(client, fileId, riskId, typeId, "2026-03-09", "2026-03-11", documentFileId);
        Assert.Equal(3, event3.EmployerDays);
        Assert.Equal(0, (await GetIncapacityBalanceAsync(client, fileId)).RemainingDays);

        // The fourth event finds the cap exhausted: its days are reclassified to unpaid discount + warning.
        var event4 = await CreateIncapacityAsync(client, fileId, riskId, typeId, "2026-03-12", "2026-03-14", documentFileId);
        Assert.Equal(0, event4.EmployerDays);
        Assert.Equal(3, event4.DiscountDays);
        Assert.Contains(event4.Warnings, warning => warning.Code == "INCAPACITY_WARNING_CAP_EXHAUSTED");

        var finalBalance = await GetIncapacityBalanceAsync(client, fileId);
        Assert.Equal(9, finalBalance.ConsumedEmployerDays);
        Assert.Equal(0, finalBalance.RemainingDays);
        Assert.Equal(0m, await GetDisabilityDaysAvailableAsync(client, fileId));
    }

    [Fact]
    public async Task Incapacity_OverlappingDateRange_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await ApplyLeaveTemplateAsync(scenario);
        using var client = factory.CreateClientFor(CreateIncapacityManagerContext(scenario));

        var (fileId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Olga", "Solape", "EMP-INC-O", "olga.inc.o@empresa.test");
        var (riskId, typeId) = await GetIncapacityMasterIdsAsync(scenario.TenantId);
        var documentFileId = await SeedIncapacityDocumentFileAsync(scenario);

        _ = await CreateIncapacityAsync(client, fileId, riskId, typeId, "2026-04-06", "2026-04-10", documentFileId);

        // A second incapacity whose range intersects the first (RN-14) is rejected.
        var overlapping = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/incapacities",
            BuildIncapacityBody(riskId, typeId, "2026-04-09", "2026-04-13", documentFileId));
        await AssertProblemDetailsAsync(overlapping, HttpStatusCode.UnprocessableEntity, "INCAPACITY_OVERLAP");

        // A contiguous, non-overlapping range (starts the day after) is accepted.
        var adjacent = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/incapacities",
            BuildIncapacityBody(riskId, typeId, "2026-04-11", "2026-04-13", documentFileId));
        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
    }
}
