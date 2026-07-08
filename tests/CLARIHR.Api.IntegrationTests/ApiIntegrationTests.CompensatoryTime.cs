using System.Net;
using System.Net.Http.Json;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.Preferences;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the compensatory-time CREDITS end-to-end slice (REQ-002 PR-3): the HR round-trip
/// (create-with-authorization-document → credited hours + ACREDITACION_TIEMPO_COMPENSATORIO journal + attached
/// document; factor 2.00 × 4h → 8.00), the write guards (missing document / foreign purpose / future work date /
/// override without note / DEBITA type all → 422), the preference-off path (no document required → 201) and the
/// anti-uncover invariant under the advisory lock (annulling a credit that debits already stand against → 422
/// COMPENSATORY_TIME_BALANCE_WOULD_GO_NEGATIVE; a credit with headroom annuls cleanly).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static TestUserContext CreateCompensatoryTimeManagerContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.Admin);

    private async Task<(Guid PublicId, long InternalId)> SeedCompensatoryTimeTypeAsync(
        IntegrationTestScenario scenario, string code, string name, string operationCode, decimal factor)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var type = CompensatoryTimeType.Create(code, name, operationCode, factor, 0);
        type.SetTenantId(scenario.TenantId);
        dbContext.Set<CompensatoryTimeType>().Add(type);
        await dbContext.SaveChangesAsync();
        return (type.PublicId, type.Id);
    }

    private async Task<Guid> SeedCompensatoryTimeDocumentFileAsync(
        IntegrationTestScenario scenario, FilePurpose purpose = FilePurpose.CompensatoryTimeDocument)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = StoredFile.Create(
            "autorizacion.pdf",
            "application/pdf",
            2048,
            ".pdf",
            StorageProvider.AzureBlob,
            "clarihr-compensatory-time-documents",
            $"compensatory-time-documents/{Guid.NewGuid():N}.pdf",
            purpose,
            FileUploadType.DirectUpload,
            scenario.ActorUserId.ToString());
        file.SetTenantId(scenario.TenantId);
        file.MarkActive(2048, "application/pdf");
        dbContext.Set<StoredFile>().Add(file);
        await dbContext.SaveChangesAsync();
        return file.PublicId;
    }

    private async Task SetCompensatoryTimeCreditRequiresDocumentAsync(Guid tenantId, bool requires, decimal? maxBalanceHours = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var preference = await dbContext.Set<CompanyPreference>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId);
        if (preference is null)
        {
            preference = CompanyPreference.CreateDefault();
            preference.SetTenantId(tenantId);
            dbContext.Set<CompanyPreference>().Add(preference);
        }

        preference.SetCompensatoryTimePolicies(null, maxBalanceHours, requires, null);
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedCompensatoryDebitAsync(Guid fileId, Guid tenantId, long typeInternalId, decimal hours)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fileInternalId = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .Where(item => item.PublicId == fileId)
            .Select(item => item.Id)
            .FirstAsync();

        var absence = PersonnelFileCompensatoryTimeAbsence.Create(
            typeInternalId,
            "Goce de tiempo compensatorio",
            new DateOnly(2026, 3, 20),
            new DateOnly(2026, 3, 20),
            hours,
            "Débito sembrado para la prueba de anti-descubierto",
            payrollPeriodPublicId: null,
            registeredByUserId: "seed",
            notes: null);
        absence.BindToPersonnelFile(fileInternalId);
        absence.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileCompensatoryTimeAbsence>().Add(absence);
        await dbContext.SaveChangesAsync();
    }

    private static object BuildCreditBody(
        Guid typeId,
        string workDate,
        decimal hoursWorked,
        Guid? authorizationFilePublicId,
        decimal? hoursCreditedOverride = null,
        string? overrideNote = null) =>
        new
        {
            compensatoryTimeTypePublicId = typeId,
            workDate,
            startTime = (string?)null,
            endTime = (string?)null,
            hoursWorked,
            hoursCreditedOverride,
            overrideNote,
            workDetail = "Soporte de sistemas fuera de jornada",
            authorizedByText = "Jefatura de TI",
            assignedPositionPublicId = (Guid?)null,
            overtimeRecordPublicId = (Guid?)null,
            notes = (string?)null,
            authorizationFilePublicId,
            documentTypeCatalogItemPublicId = (Guid?)null,
            documentObservations = (string?)null
        };

    private async Task<PersonnelFileCompensatoryTimeCreditResponse> CreateCompensatoryCreditAsync(
        HttpClient client, Guid fileId, Guid typeId, string workDate, decimal hoursWorked, Guid documentFileId)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/compensatory-time-credits",
            BuildCreditBody(typeId, workDate, hoursWorked, documentFileId));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Create credit failed: {(int)response.StatusCode} {payload}");
        var created = await response.Content.ReadFromJsonAsync<PersonnelFileCompensatoryTimeCreditResponse>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    [Fact]
    public async Task CompensatoryTimeCredit_HrRoundTrip_CreditsHoursJournalsAndAttachesDocument()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompensatoryTimeManagerContext(scenario));

        var (fileId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Carla", "Compensa", "EMP-CT-A", "carla.ct.a@empresa.test");
        var (typeFactor1, _) = await SeedCompensatoryTimeTypeAsync(scenario, "FUERA_JORNADA", "Trabajo fuera de jornada", CompensatoryTimeOperations.Credits, 1.00m);
        var (typeFactor2, _) = await SeedCompensatoryTimeTypeAsync(scenario, "ASUETO", "Trabajo en asueto", CompensatoryTimeOperations.Both, 2.00m);
        var (typeDebit, _) = await SeedCompensatoryTimeTypeAsync(scenario, "GOCE", "Goce de tiempo compensatorio", CompensatoryTimeOperations.Debits, 1.00m);
        var documentFileId = await SeedCompensatoryTimeDocumentFileAsync(scenario);

        // [1] Register a factor-1.00 credit of 8h with its authorization document → 8.00 credited + journal + doc.
        var credit = await CreateCompensatoryCreditAsync(client, fileId, typeFactor1, "2026-03-04", 8m, documentFileId);
        Assert.Equal(CompensatoryTimeStatuses.Registrada, credit.StatusCode);
        Assert.Equal(1.00m, credit.FactorApplied);
        Assert.Equal(8.00m, credit.HoursCredited);
        Assert.False(credit.IsOverridden);
        Assert.Equal("FUERA_JORNADA", credit.CompensatoryTimeTypeCode);
        Assert.Equal(1, await CountPersonnelActionsAsync(scenario.TenantId, fileId, "ACREDITACION_TIEMPO_COMPENSATORIO"));

        var docs = await client.GetAsync($"/api/v1/personnel-files/{fileId}/compensatory-time-credits/{credit.Id}/documents");
        docs.EnsureSuccessStatusCode();
        var docList = await docs.Content.ReadFromJsonAsync<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>>(JsonOptions);
        Assert.NotNull(docList);
        Assert.Single(docList!);

        // [2] factor 2.00 × 4h → 8.00 (D-19 rounding rule).
        var doubled = await SeedCompensatoryTimeDocumentFileAsync(scenario);
        var creditDoubled = await CreateCompensatoryCreditAsync(client, fileId, typeFactor2, "2026-03-05", 4m, doubled);
        Assert.Equal(2.00m, creditDoubled.FactorApplied);
        Assert.Equal(8.00m, creditDoubled.HoursCredited);

        // [3] Missing authorization document → 422 (the preference requires it by default).
        var missingDoc = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/compensatory-time-credits",
            BuildCreditBody(typeFactor1, "2026-03-06", 3m, authorizationFilePublicId: null));
        await AssertProblemDetailsAsync(missingDoc, HttpStatusCode.UnprocessableEntity, "COMPENSATORY_TIME_DOCUMENT_REQUIRED");

        // [4] A file uploaded with a foreign purpose → 422.
        var foreignPurposeFile = await SeedCompensatoryTimeDocumentFileAsync(scenario, FilePurpose.IncapacityDocument);
        var foreignPurpose = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/compensatory-time-credits",
            BuildCreditBody(typeFactor1, "2026-03-06", 3m, foreignPurposeFile));
        await AssertProblemDetailsAsync(foreignPurpose, HttpStatusCode.UnprocessableEntity, "COMPENSATORY_TIME_DOCUMENT_PURPOSE_INVALID");

        // [5] Work date in the future → 422.
        var futureFile = await SeedCompensatoryTimeDocumentFileAsync(scenario);
        var future = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/compensatory-time-credits",
            BuildCreditBody(typeFactor1, "2999-01-01", 3m, futureFile));
        await AssertProblemDetailsAsync(future, HttpStatusCode.UnprocessableEntity, "COMPENSATORY_TIME_WORK_DATE_IN_FUTURE");

        // [6] Manual override of the credited hours without a note → 422.
        var overrideFile = await SeedCompensatoryTimeDocumentFileAsync(scenario);
        var overrideNoNote = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/compensatory-time-credits",
            BuildCreditBody(typeFactor1, "2026-03-06", 3m, overrideFile, hoursCreditedOverride: 5m, overrideNote: null));
        await AssertProblemDetailsAsync(overrideNoNote, HttpStatusCode.UnprocessableEntity, "COMPENSATORY_TIME_OVERRIDE_NOTE_REQUIRED");

        // [7] A DEBITA-only type cannot be used for a credit → 422.
        var debitFile = await SeedCompensatoryTimeDocumentFileAsync(scenario);
        var debitType = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/compensatory-time-credits",
            BuildCreditBody(typeDebit, "2026-03-06", 3m, debitFile));
        await AssertProblemDetailsAsync(debitType, HttpStatusCode.UnprocessableEntity, "COMPENSATORY_TIME_TYPE_OPERATION_MISMATCH");
    }

    [Fact]
    public async Task CompensatoryTimeCredit_PreferenceOff_AllowsCreditWithoutDocument()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await SetCompensatoryTimeCreditRequiresDocumentAsync(scenario.TenantId, requires: false);
        using var client = factory.CreateClientFor(CreateCompensatoryTimeManagerContext(scenario));

        var (fileId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Bruno", "SinDoc", "EMP-CT-B", "bruno.ct.b@empresa.test");
        var (typeId, _) = await SeedCompensatoryTimeTypeAsync(scenario, "FUERA_JORNADA", "Trabajo fuera de jornada", CompensatoryTimeOperations.Credits, 1.00m);

        var response = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/compensatory-time-credits",
            BuildCreditBody(typeId, "2026-03-04", 6m, authorizationFilePublicId: null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PersonnelFileCompensatoryTimeCreditResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(6.00m, created!.HoursCredited);
        Assert.Null(created.AuthorizerFilePublicId);
    }

    [Fact]
    public async Task CompensatoryTimeCredit_AnnulUncoveringDebits_IsRejectedUnderLock()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await SetCompensatoryTimeCreditRequiresDocumentAsync(scenario.TenantId, requires: false);
        using var client = factory.CreateClientFor(CreateCompensatoryTimeManagerContext(scenario));

        var (fileId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Diana", "Descubierto", "EMP-CT-D", "diana.ct.d@empresa.test");
        var (typeId, typeInternalId) = await SeedCompensatoryTimeTypeAsync(scenario, "FUERA_JORNADA", "Trabajo fuera de jornada", CompensatoryTimeOperations.Both, 1.00m);

        // Fund = 12 + 3 credited − 8 debited = 7.
        var bigCredit = await CreateCompensatoryCreditAsync(client, fileId, typeId, "2026-03-04", 12m, await SeedCompensatoryTimeDocumentFileAsync(scenario));
        var smallCredit = await CreateCompensatoryCreditAsync(client, fileId, typeId, "2026-03-05", 3m, await SeedCompensatoryTimeDocumentFileAsync(scenario));
        await SeedCompensatoryDebitAsync(fileId, scenario.TenantId, typeInternalId, 8m);

        // Annulling the 12h credit would leave 7 − 12 = −5 → rejected under the advisory lock.
        var uncover = await SendSettlementAsync(
            client, HttpMethod.Patch, $"/api/v1/personnel-files/{fileId}/compensatory-time-credits/{bigCredit.Id}/annulment",
            bigCredit.ConcurrencyToken, new { reason = "Registro duplicado" });
        await AssertProblemDetailsAsync(uncover, HttpStatusCode.UnprocessableEntity, "COMPENSATORY_TIME_BALANCE_WOULD_GO_NEGATIVE");

        // The 3h credit has headroom (7 − 3 = 4 ≥ 0) → annuls cleanly.
        var withHeadroom = await SendSettlementAsync(
            client, HttpMethod.Patch, $"/api/v1/personnel-files/{fileId}/compensatory-time-credits/{smallCredit.Id}/annulment",
            smallCredit.ConcurrencyToken, new { reason = "Ajuste" });
        Assert.Equal(HttpStatusCode.OK, withHeadroom.StatusCode);
    }
}
