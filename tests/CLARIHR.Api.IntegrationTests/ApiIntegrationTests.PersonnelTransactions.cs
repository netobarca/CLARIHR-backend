using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Compensation;
using CLARIHR.Domain.EmployeeRelations;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for REQ-003 PR-3 — recognitions end-to-end: the one-decision round-trip (register
/// EN_REVISION with no journal entry → apply by a third party → automatic RECONOCIMIENTO personnel action →
/// the employee sees it in self-service only once APLICADA, D-13), the double anti-self-approval (the subject
/// and the registrar are both barred, RN-02), the reject-note requirement, the revocation that annuls the
/// linked entry, the document sub-resource with the RecognitionDocument purpose gate, and the RETIRADO lock.
/// Employees and the recognition-type master are seeded directly (the test harness bypasses provisioning).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static readonly DateTime RecognitionHireDate = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static TestUserContext RecognitionContext(IntegrationTestScenario scenario, Guid userId, params string[] permissions) =>
        TestUserContext.Authenticated(userId, scenario.TenantId, permissions);

    private async Task<Guid> SeedRecognitionTypeAsync(Guid tenantId, string code, string name)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var type = RecognitionType.Create(code, name, 10);
        type.SetTenantId(tenantId);
        dbContext.Set<RecognitionType>().Add(type);
        await dbContext.SaveChangesAsync();
        return type.PublicId;
    }

    private async Task<Guid> SeedRecognitionEmployeeAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail,
        string employmentStatusCode = "ACTIVO",
        Guid? linkedUserPublicId = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            firstName,
            lastName,
            new DateTime(1992, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: null,
            profession: null,
            nationality: "SV",
            personalEmail: null,
            institutionalEmail: institutionalEmail,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null);
        file.SetTenantId(tenantId);
        if (linkedUserPublicId is { } linked)
        {
            file.Complete(linked);
        }
        else
        {
            file.CompleteWithoutLinkedUser();
        }

        dbContext.Set<PersonnelFile>().Add(file);
        await dbContext.SaveChangesAsync();

        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, employmentStatusCode, RecognitionHireDate);
        profile.BindToPersonnelFile(file.Id);
        profile.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmployeeProfile>().Add(profile);
        await dbContext.SaveChangesAsync();

        return file.PublicId;
    }

    private async Task<(Guid RecognitionId, Guid Token)> SeedRecognitionRecordAsync(
        Guid tenantId, Guid filePublicId, Guid recognitionTypePublicId, Guid registeredByUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fileInternalId = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .Where(item => item.PublicId == filePublicId).Select(item => item.Id).SingleAsync();
        var type = await dbContext.Set<RecognitionType>()
            .IgnoreQueryFilters()
            .Where(item => item.PublicId == recognitionTypePublicId).Select(item => new { item.Id, item.Name }).SingleAsync();

        var recognition = PersonnelFileRecognition.Create(
            type.Id, type.Name, new DateOnly(2026, 3, 1), "Desempeño sobresaliente.",
            amount: null, currencyCode: null, assignedPositionPublicId: null, registeredByUserId, notes: null);
        recognition.BindToPersonnelFile(fileInternalId);
        recognition.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileRecognition>().Add(recognition);
        await dbContext.SaveChangesAsync();

        return (recognition.PublicId, recognition.ConcurrencyToken);
    }

    private async Task<Guid> SeedRecognitionFileAsync(
        IntegrationTestScenario scenario, FilePurpose purpose = FilePurpose.RecognitionDocument)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = StoredFile.Create(
            "diploma.pdf",
            "application/pdf",
            2048,
            ".pdf",
            StorageProvider.AzureBlob,
            "clarihr-recognition-documents",
            $"recognition-documents/{Guid.NewGuid():N}.pdf",
            purpose,
            FileUploadType.DirectUpload,
            scenario.ActorUserId.ToString());
        file.SetTenantId(scenario.TenantId);
        file.MarkActive(2048, "application/pdf");
        dbContext.Set<StoredFile>().Add(file);
        await dbContext.SaveChangesAsync();
        return file.PublicId;
    }

    private static object RecognitionBody(Guid recognitionTypePublicId, string eventDate = "2026-03-01") =>
        new
        {
            recognitionTypePublicId,
            eventDate,
            detail = "Reconocimiento por desempeño sobresaliente.",
            amount = (decimal?)null,
            currencyCode = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            notes = (string?)null
        };

    private static async Task<(Guid RecognitionId, Guid Token)> CreateRecognitionAsync(
        HttpClient client, Guid employeeId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{employeeId}/recognitions", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return (
            doc.RootElement.GetProperty("recognitionPublicId").GetGuid(),
            doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> PatchRecognitionAsync(
        HttpClient client, Guid employeeId, Guid recognitionId, string action, Guid token, object body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/recognitions/{recognitionId}/{action}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Recognition_CreateThenApplyByThirdParty_JournalsAndVisibleToSelfOnlyWhenApplied()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var subjectUserId = Guid.NewGuid();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var authorizerClient = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeRecognitions));
        using var selfClient = factory.CreateClientFor(RecognitionContext(scenario, subjectUserId));

        var typeId = await SeedRecognitionTypeAsync(scenario.TenantId, "DESEMPENO", "Desempeño sobresaliente");
        var employeeId = await SeedRecognitionEmployeeAsync(
            scenario.TenantId, "Sonia", "Sujeta", "EMP-RC-A", "sonia.rc.a@empresa.test", linkedUserPublicId: subjectUserId);

        var (recognitionId, token) = await CreateRecognitionAsync(managerClient, employeeId, RecognitionBody(typeId));

        // EN_REVISION and NO journal entry yet.
        using (var doc = JsonDocument.Parse(await (await managerClient.GetAsync($"/api/v1/personnel-files/{employeeId}/recognitions/{recognitionId}")).Content.ReadAsStringAsync()))
        {
            Assert.Equal("EN_REVISION", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("personnelActionPublicId").ValueKind);
        }

        Assert.DoesNotContain("RECONOCIMIENTO", await (await managerClient.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions")).Content.ReadAsStringAsync());

        // Self sees nothing while EN_REVISION (D-13).
        using (var doc = JsonDocument.Parse(await (await selfClient.GetAsync($"/api/v1/personnel-files/{employeeId}/recognitions")).Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, doc.RootElement.GetArrayLength());
        }

        // A third party (neither subject nor registrar) applies → APLICADA + RECONOCIMIENTO journaled.
        var applied = await PatchRecognitionAsync(authorizerClient, employeeId, recognitionId, "decision", token,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        using (var doc = JsonDocument.Parse(await applied.Content.ReadAsStringAsync()))
        {
            Assert.Equal("APLICADA", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.NotEqual(JsonValueKind.Null, doc.RootElement.GetProperty("personnelActionPublicId").ValueKind);
        }

        Assert.Contains("RECONOCIMIENTO", await (await managerClient.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions")).Content.ReadAsStringAsync());

        // Now the employee sees exactly their APLICADA recognition.
        using (var doc = JsonDocument.Parse(await (await selfClient.GetAsync($"/api/v1/personnel-files/{employeeId}/recognitions")).Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, doc.RootElement.GetArrayLength());
            Assert.Equal("APLICADA", doc.RootElement[0].GetProperty("statusCode").GetString());
        }
    }

    [Fact]
    public async Task Recognition_RegistrarOrSubjectDecides_IsForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var subjectUserId = Guid.NewGuid();
        var managerUserId = scenario.ActorUserId;

        // The registrar also holds the authorize grant (so the 403 comes from the anti-self rule, not RBAC).
        using var registrarClient = factory.CreateClientFor(RecognitionContext(
            scenario, managerUserId, PersonnelFilePermissionCodes.Admin, PersonnelFilePermissionCodes.AuthorizeRecognitions));
        using var subjectClient = factory.CreateClientFor(RecognitionContext(scenario, subjectUserId, PersonnelFilePermissionCodes.AuthorizeRecognitions));

        var typeId = await SeedRecognitionTypeAsync(scenario.TenantId, "DESEMPENO", "Desempeño sobresaliente");
        var employeeId = await SeedRecognitionEmployeeAsync(
            scenario.TenantId, "Beto", "Barrera", "EMP-RC-B", "beto.rc.b@empresa.test", linkedUserPublicId: subjectUserId);

        var (recognitionId, token) = await CreateRecognitionAsync(registrarClient, employeeId, RecognitionBody(typeId));

        // The registrar cannot decide their own registration.
        var byRegistrar = await PatchRecognitionAsync(registrarClient, employeeId, recognitionId, "decision", token,
            new { decision = "APLICAR", note = (string?)null });
        await AssertProblemDetailsAsync(byRegistrar, HttpStatusCode.Forbidden, "RECOGNITION_SELF_APPROVAL_FORBIDDEN");

        // The subject employee cannot decide their own recognition.
        var bySubject = await PatchRecognitionAsync(subjectClient, employeeId, recognitionId, "decision", token,
            new { decision = "APLICAR", note = (string?)null });
        await AssertProblemDetailsAsync(bySubject, HttpStatusCode.Forbidden, "RECOGNITION_SELF_APPROVAL_FORBIDDEN");
    }

    [Fact]
    public async Task Recognition_RejectWithoutNote_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var authorizerClient = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeRecognitions));

        var typeId = await SeedRecognitionTypeAsync(scenario.TenantId, "DESEMPENO", "Desempeño sobresaliente");
        var employeeId = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Nadia", "Nota", "EMP-RC-C", "nadia.rc.c@empresa.test");

        var (recognitionId, token) = await CreateRecognitionAsync(managerClient, employeeId, RecognitionBody(typeId));

        var response = await PatchRecognitionAsync(authorizerClient, employeeId, recognitionId, "decision", token,
            new { decision = "RECHAZAR", note = "   " });

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "DECISION_NOTE_REQUIRED");
    }

    [Fact]
    public async Task Recognition_RevokeApplied_AnnulsEntryAndDisappearsFromSelf()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var subjectUserId = Guid.NewGuid();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var authorizerClient = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeRecognitions));
        using var selfClient = factory.CreateClientFor(RecognitionContext(scenario, subjectUserId));

        var typeId = await SeedRecognitionTypeAsync(scenario.TenantId, "DESEMPENO", "Desempeño sobresaliente");
        var employeeId = await SeedRecognitionEmployeeAsync(
            scenario.TenantId, "Rita", "Revocada", "EMP-RC-D", "rita.rc.d@empresa.test", linkedUserPublicId: subjectUserId);

        var (recognitionId, token) = await CreateRecognitionAsync(managerClient, employeeId, RecognitionBody(typeId));

        var applied = await PatchRecognitionAsync(authorizerClient, employeeId, recognitionId, "decision", token,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        Guid appliedToken;
        Guid actionPublicId;
        using (var doc = JsonDocument.Parse(await applied.Content.ReadAsStringAsync()))
        {
            appliedToken = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            actionPublicId = doc.RootElement.GetProperty("personnelActionPublicId").GetGuid();
        }

        // Revocation by a third-party authorizer → ANULADA + the linked entry annulled.
        var revoked = await PatchRecognitionAsync(authorizerClient, employeeId, recognitionId, "annulment", appliedToken,
            new { reason = "Registrado por error." });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        using (var doc = JsonDocument.Parse(await revoked.Content.ReadAsStringAsync()))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("statusCode").GetString());
        }

        // The linked RECONOCIMIENTO entry is ANULADA.
        using (var doc = JsonDocument.Parse(await (await managerClient.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions")).Content.ReadAsStringAsync()))
        {
            var entry = doc.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("personnelActionPublicId").GetGuid() == actionPublicId);
            Assert.Equal("ANULADA", entry.GetProperty("actionStatusCode").GetString());
        }

        // The employee no longer sees it (only APLICADA travels — it is now ANULADA).
        using (var doc = JsonDocument.Parse(await (await selfClient.GetAsync($"/api/v1/personnel-files/{employeeId}/recognitions")).Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, doc.RootElement.GetArrayLength());
        }
    }

    [Fact]
    public async Task Recognition_DocumentSubResource_RoundTrips()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));

        var typeId = await SeedRecognitionTypeAsync(scenario.TenantId, "DESEMPENO", "Desempeño sobresaliente");
        var employeeId = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Diego", "Documento", "EMP-RC-E", "diego.rc.e@empresa.test");
        var (recognitionId, _) = await CreateRecognitionAsync(managerClient, employeeId, RecognitionBody(typeId));

        var fileId = await SeedRecognitionFileAsync(scenario);
        var addResponse = await managerClient.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/recognitions/{recognitionId}/documents",
            new { filePublicId = fileId, documentTypeCatalogItemPublicId = (Guid?)null, observations = "Diploma." });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        Guid documentId;
        using (var doc = JsonDocument.Parse(await addResponse.Content.ReadAsStringAsync()))
        {
            documentId = doc.RootElement.GetProperty("documentPublicId").GetGuid();
        }

        using (var doc = JsonDocument.Parse(await (await managerClient.GetAsync($"/api/v1/personnel-files/{employeeId}/recognitions/{recognitionId}/documents")).Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, doc.RootElement.GetArrayLength());
        }

        var readUrl = await managerClient.GetAsync($"/api/v1/personnel-files/{employeeId}/recognitions/{recognitionId}/documents/{documentId}/read-url");
        Assert.Equal(HttpStatusCode.OK, readUrl.StatusCode);

        // A file uploaded with a foreign purpose is rejected (purpose gate).
        var foreignFileId = await SeedRecognitionFileAsync(scenario, FilePurpose.IncapacityDocument);
        var foreign = await managerClient.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/recognitions/{recognitionId}/documents",
            new { filePublicId = foreignFileId, documentTypeCatalogItemPublicId = (Guid?)null, observations = (string?)null });
        // The shared file-storage purpose gate (mirrored from medical-claims) surfaces a 400 files.invalid_purpose.
        await AssertProblemDetailsAsync(foreign, HttpStatusCode.BadRequest, "files.invalid_purpose");
    }

    [Fact]
    public async Task Recognition_RetiredProfile_BlocksCreateAndApplyButAllowsReject()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var authorizerClient = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeRecognitions));

        var typeId = await SeedRecognitionTypeAsync(scenario.TenantId, "DESEMPENO", "Desempeño sobresaliente");
        var employeeId = await SeedRecognitionEmployeeAsync(
            scenario.TenantId, "Ramon", "Retirado", "EMP-RC-F", "ramon.rc.f@empresa.test", employmentStatusCode: "RETIRADO");

        // Creating on a retired profile is blocked.
        var create = await managerClient.PostAsJsonAsync($"/api/v1/personnel-files/{employeeId}/recognitions", RecognitionBody(typeId));
        await AssertProblemDetailsAsync(create, HttpStatusCode.UnprocessableEntity, "EMPLOYEE_PROFILE_RETIRED_LOCKED");

        // A pending recognition seeded directly can be rejected but not applied on a retired profile.
        var (recognitionId, token) = await SeedRecognitionRecordAsync(scenario.TenantId, employeeId, typeId, managerUserId);

        var apply = await PatchRecognitionAsync(authorizerClient, employeeId, recognitionId, "decision", token,
            new { decision = "APLICAR", note = (string?)null });
        await AssertProblemDetailsAsync(apply, HttpStatusCode.UnprocessableEntity, "EMPLOYEE_PROFILE_RETIRED_LOCKED");

        var reject = await PatchRecognitionAsync(authorizerClient, employeeId, recognitionId, "decision", token,
            new { decision = "RECHAZAR", note = "Empleado retirado." });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        using var doc = JsonDocument.Parse(await reject.Content.ReadAsStringAsync());
        Assert.Equal("RECHAZADA", doc.RootElement.GetProperty("statusCode").GetString());
    }

    // ── Disciplinary actions (REQ-003 PR-4) ──────────────────────────────────────────────────────────

    private async Task<Guid> SeedDisciplinaryActionTypeAsync(Guid tenantId, string code, string name, bool appliesSuspension)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var type = DisciplinaryActionType.Create(code, name, appliesSuspension, 10);
        type.SetTenantId(tenantId);
        dbContext.Set<DisciplinaryActionType>().Add(type);
        await dbContext.SaveChangesAsync();
        return type.PublicId;
    }

    private async Task<Guid> SeedDisciplinaryActionCauseAsync(Guid tenantId, string code, string name, string? deductionConceptTypeCode)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cause = DisciplinaryActionCause.Create(code, name, deductionConceptTypeCode, 10);
        cause.SetTenantId(tenantId);
        dbContext.Set<DisciplinaryActionCause>().Add(cause);
        await dbContext.SaveChangesAsync();
        return cause.PublicId;
    }

    private async Task<string> SeedCompensationConceptAsync(Guid tenantId, string code, string name, CompensationNature nature)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var countryId = await dbContext.Companies
            .Where(company => company.PublicId == tenantId)
            .Select(company => company.CountryCatalogItemId)
            .SingleAsync();

        var concept = CompensationConceptTypeCatalogItem.Create(
            countryId,
            "SV",
            code,
            name,
            nature,
            isStatutory: false,
            defaultDeductionClass: nature == CompensationNature.Egreso ? DeductionClass.Interno : null,
            defaultCalculationType: CompensationCalculationType.Fixed,
            defaultCalculationBaseCode: null,
            defaultEmployeeRate: null,
            defaultEmployerRate: null,
            contributionCap: null,
            isBaseSalary: false,
            isActive: true,
            sortOrder: 500);
        dbContext.CompensationConceptTypeCatalogItems.Add(concept);
        await dbContext.SaveChangesAsync();
        return code;
    }

    private static object DisciplinaryActionBody(
        Guid typeId,
        Guid causeId,
        string incidentDate = "2026-04-28",
        bool hasPayrollDeduction = false,
        decimal? deductionAmount = null,
        string? currencyCode = null,
        string? deductionConceptTypeCode = null,
        string? suspensionStartDate = null,
        string? suspensionEndDate = null) =>
        new
        {
            disciplinaryActionTypePublicId = typeId,
            disciplinaryActionCausePublicId = causeId,
            incidentDate,
            factsDetail = "Faltó tres días sin aviso.",
            hasPayrollDeduction,
            deductionAmount,
            currencyCode,
            deductionConceptTypeCode,
            suspensionStartDate,
            suspensionEndDate,
            assignedPositionPublicId = (Guid?)null,
            notes = (string?)null
        };

    private static async Task<(Guid DisciplinaryActionId, Guid Token)> CreateDisciplinaryActionAsync(
        HttpClient client, Guid employeeId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{employeeId}/disciplinary-actions", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return (
            doc.RootElement.GetProperty("disciplinaryActionPublicId").GetGuid(),
            doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> PatchDisciplinaryActionAsync(
        HttpClient client, Guid employeeId, Guid disciplinaryActionId, string action, Guid token, object body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/disciplinary-actions/{disciplinaryActionId}/{action}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task DisciplinaryAction_ApplyWithSuspensionAndDeduction_JournalsBothEntriesAndSnapshotsConcept()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var authorizerClient = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions));

        var conceptCode = await SeedCompensationConceptAsync(scenario.TenantId, "DESC_DISCIPLINARIO", "Descuento disciplinario", CompensationNature.Egreso);
        var typeId = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "SUSPENSION_SIN_GOCE", "Suspensión sin goce", appliesSuspension: true);
        var causeId = await SeedDisciplinaryActionCauseAsync(scenario.TenantId, "INASISTENCIA", "Inasistencia injustificada", conceptCode);
        var employeeId = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Diana", "Disciplina", "EMP-DA-A", "diana.da.a@empresa.test");

        var (disciplinaryActionId, token) = await CreateDisciplinaryActionAsync(managerClient, employeeId, DisciplinaryActionBody(
            typeId, causeId, hasPayrollDeduction: true, deductionAmount: 25m, currencyCode: "USD",
            suspensionStartDate: "2026-05-10", suspensionEndDate: "2026-05-12"));

        // EN_REVISION with the derived suspension days and no journal entries yet.
        using (var doc = JsonDocument.Parse(await (await managerClient.GetAsync($"/api/v1/personnel-files/{employeeId}/disciplinary-actions/{disciplinaryActionId}")).Content.ReadAsStringAsync()))
        {
            Assert.Equal("EN_REVISION", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.Equal(3, doc.RootElement.GetProperty("suspensionDays").GetInt32());
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("personnelActionPublicId").ValueKind);
        }

        var applied = await PatchDisciplinaryActionAsync(authorizerClient, employeeId, disciplinaryActionId, "decision", token,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);

        Guid amonestacionActionId;
        Guid suspensionActionId;
        using (var doc = JsonDocument.Parse(await applied.Content.ReadAsStringAsync()))
        {
            Assert.Equal("APLICADA", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.Equal("DESC_DISCIPLINARIO", doc.RootElement.GetProperty("deductionConceptTypeCode").GetString());
            Assert.Equal("Descuento disciplinario", doc.RootElement.GetProperty("deductionConceptNameSnapshot").GetString());
            amonestacionActionId = doc.RootElement.GetProperty("personnelActionPublicId").GetGuid();
            suspensionActionId = doc.RootElement.GetProperty("suspensionActionPublicId").GetGuid();
        }

        // Both entries are journaled: AMONESTACION (with the deduction amount) + SUSPENSION (with the range).
        using (var doc = JsonDocument.Parse(await (await managerClient.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions")).Content.ReadAsStringAsync()))
        {
            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToArray();
            var amonestacion = items.Single(item => item.GetProperty("personnelActionPublicId").GetGuid() == amonestacionActionId);
            Assert.Equal("AMONESTACION", amonestacion.GetProperty("actionTypeCode").GetString());
            Assert.Equal("APLICADA", amonestacion.GetProperty("actionStatusCode").GetString());
            Assert.Equal(25m, amonestacion.GetProperty("amount").GetDecimal());

            var suspension = items.Single(item => item.GetProperty("personnelActionPublicId").GetGuid() == suspensionActionId);
            Assert.Equal("SUSPENSION", suspension.GetProperty("actionTypeCode").GetString());
            Assert.Equal("APLICADA", suspension.GetProperty("actionStatusCode").GetString());
            Assert.NotEqual(JsonValueKind.Null, suspension.GetProperty("effectiveFromUtc").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, suspension.GetProperty("effectiveToUtc").ValueKind);
        }
    }

    [Fact]
    public async Task DisciplinaryAction_SuspensionDatesOnNonSuspensionType_AreRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));

        var typeId = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "ESCRITA", "Amonestación escrita", appliesSuspension: false);
        var causeId = await SeedDisciplinaryActionCauseAsync(scenario.TenantId, "CONDUCTA", "Conducta indebida", deductionConceptTypeCode: null);
        var employeeId = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Nora", "NoSuspende", "EMP-DA-B", "nora.da.b@empresa.test");

        var response = await managerClient.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/disciplinary-actions",
            DisciplinaryActionBody(typeId, causeId, suspensionStartDate: "2026-05-10", suspensionEndDate: "2026-05-12"));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "SUSPENSION_NOT_ALLOWED_FOR_TYPE");
    }

    [Fact]
    public async Task DisciplinaryAction_OverlappingSuspension_IsRejectedUnderLock()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var authorizerClient = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions));

        var typeId = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "SUSPENSION_SIN_GOCE", "Suspensión sin goce", appliesSuspension: true);
        var causeId = await SeedDisciplinaryActionCauseAsync(scenario.TenantId, "INASISTENCIA", "Inasistencia injustificada", deductionConceptTypeCode: null);
        var employeeId = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Olga", "Overlap", "EMP-DA-C", "olga.da.c@empresa.test");

        var (firstId, firstToken) = await CreateDisciplinaryActionAsync(managerClient, employeeId, DisciplinaryActionBody(
            typeId, causeId, suspensionStartDate: "2026-05-10", suspensionEndDate: "2026-05-15"));
        var firstApply = await PatchDisciplinaryActionAsync(authorizerClient, employeeId, firstId, "decision", firstToken,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, firstApply.StatusCode);

        // A second suspension overlapping the applied one cannot be applied (RN-18, re-checked under the lock).
        var (secondId, secondToken) = await CreateDisciplinaryActionAsync(managerClient, employeeId, DisciplinaryActionBody(
            typeId, causeId, suspensionStartDate: "2026-05-12", suspensionEndDate: "2026-05-18"));
        var secondApply = await PatchDisciplinaryActionAsync(authorizerClient, employeeId, secondId, "decision", secondToken,
            new { decision = "APLICAR", note = (string?)null });
        await AssertProblemDetailsAsync(secondApply, HttpStatusCode.UnprocessableEntity, "SUSPENSION_OVERLAP");
    }

    [Fact]
    public async Task DisciplinaryAction_RevokeApplied_AnnulsBothLinkedEntries()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var authorizerClient = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions));

        var typeId = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "SUSPENSION_SIN_GOCE", "Suspensión sin goce", appliesSuspension: true);
        var causeId = await SeedDisciplinaryActionCauseAsync(scenario.TenantId, "INASISTENCIA", "Inasistencia injustificada", deductionConceptTypeCode: null);
        var employeeId = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Rene", "Revoca", "EMP-DA-D", "rene.da.d@empresa.test");

        var (disciplinaryActionId, token) = await CreateDisciplinaryActionAsync(managerClient, employeeId, DisciplinaryActionBody(
            typeId, causeId, suspensionStartDate: "2026-05-10", suspensionEndDate: "2026-05-12"));
        var applied = await PatchDisciplinaryActionAsync(authorizerClient, employeeId, disciplinaryActionId, "decision", token,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);

        Guid appliedToken;
        Guid amonestacionActionId;
        Guid suspensionActionId;
        using (var doc = JsonDocument.Parse(await applied.Content.ReadAsStringAsync()))
        {
            appliedToken = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            amonestacionActionId = doc.RootElement.GetProperty("personnelActionPublicId").GetGuid();
            suspensionActionId = doc.RootElement.GetProperty("suspensionActionPublicId").GetGuid();
        }

        var revoked = await PatchDisciplinaryActionAsync(authorizerClient, employeeId, disciplinaryActionId, "annulment", appliedToken,
            new { reason = "Registrada por error." });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        using (var doc = JsonDocument.Parse(await revoked.Content.ReadAsStringAsync()))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("statusCode").GetString());
        }

        // BOTH linked entries (AMONESTACION + SUSPENSION) are ANULADA.
        using (var doc = JsonDocument.Parse(await (await managerClient.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions")).Content.ReadAsStringAsync()))
        {
            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal("ANULADA", items.Single(item => item.GetProperty("personnelActionPublicId").GetGuid() == amonestacionActionId).GetProperty("actionStatusCode").GetString());
            Assert.Equal("ANULADA", items.Single(item => item.GetProperty("personnelActionPublicId").GetGuid() == suspensionActionId).GetProperty("actionStatusCode").GetString());
        }
    }

    [Fact]
    public async Task DisciplinaryAction_DeductionWithoutAmountOrIngressConcept_AreRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));

        var ingressCode = await SeedCompensationConceptAsync(scenario.TenantId, "BONO_INGRESO", "Bono de ingreso", CompensationNature.Ingreso);
        var typeId = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "ESCRITA", "Amonestación escrita", appliesSuspension: false);
        var causeId = await SeedDisciplinaryActionCauseAsync(scenario.TenantId, "DANO", "Daño a bienes", deductionConceptTypeCode: null);
        var employeeId = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Delia", "Descuento", "EMP-DA-E", "delia.da.e@empresa.test");

        // A deduction flag without an amount is rejected (RN-06).
        var noAmount = await managerClient.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/disciplinary-actions",
            DisciplinaryActionBody(typeId, causeId, hasPayrollDeduction: true, deductionAmount: null));
        await AssertProblemDetailsAsync(noAmount, HttpStatusCode.UnprocessableEntity, "DEDUCTION_AMOUNT_REQUIRED");

        // An income (ingreso) concept on the input is rejected as a non-egreso concept.
        var ingress = await managerClient.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/disciplinary-actions",
            DisciplinaryActionBody(typeId, causeId, hasPayrollDeduction: true, deductionAmount: 10m, currencyCode: "USD", deductionConceptTypeCode: ingressCode));
        await AssertProblemDetailsAsync(ingress, HttpStatusCode.UnprocessableEntity, "DEDUCTION_CONCEPT_INVALID");
    }

    [Fact]
    public async Task DisciplinaryAction_RegistrarOrSubjectDecides_IsForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var subjectUserId = Guid.NewGuid();
        var managerUserId = scenario.ActorUserId;

        // The registrar also holds the authorize grant (so the 403 comes from the anti-self rule, not RBAC).
        using var registrarClient = factory.CreateClientFor(RecognitionContext(
            scenario, managerUserId, PersonnelFilePermissionCodes.Admin, PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions));
        using var subjectClient = factory.CreateClientFor(RecognitionContext(scenario, subjectUserId, PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions));

        var typeId = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "ESCRITA", "Amonestación escrita", appliesSuspension: false);
        var causeId = await SeedDisciplinaryActionCauseAsync(scenario.TenantId, "TARDANZA", "Llegadas tardías", deductionConceptTypeCode: null);
        var employeeId = await SeedRecognitionEmployeeAsync(
            scenario.TenantId, "Selso", "Sujeto", "EMP-DA-F", "selso.da.f@empresa.test", linkedUserPublicId: subjectUserId);

        var (disciplinaryActionId, token) = await CreateDisciplinaryActionAsync(registrarClient, employeeId, DisciplinaryActionBody(typeId, causeId));

        // The registrar cannot decide their own registration.
        var byRegistrar = await PatchDisciplinaryActionAsync(registrarClient, employeeId, disciplinaryActionId, "decision", token,
            new { decision = "APLICAR", note = (string?)null });
        await AssertProblemDetailsAsync(byRegistrar, HttpStatusCode.Forbidden, "DISCIPLINARY_ACTION_SELF_APPROVAL_FORBIDDEN");

        // The subject employee cannot decide their own disciplinary action.
        var bySubject = await PatchDisciplinaryActionAsync(subjectClient, employeeId, disciplinaryActionId, "decision", token,
            new { decision = "APLICAR", note = (string?)null });
        await AssertProblemDetailsAsync(bySubject, HttpStatusCode.Forbidden, "DISCIPLINARY_ACTION_SELF_APPROVAL_FORBIDDEN");
    }

    // ── Bandejas + exports + payroll input (REQ-003 PR-5) ─────────────────────────────────────────────

    [Fact]
    public async Task PersonnelTransactions_Bandejas_StatusCountsDefaultExcludeAnnulledAndExport()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var recognitionAuthorizer = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeRecognitions));
        using var disciplinaryAuthorizer = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions));

        var recTypeId = await SeedRecognitionTypeAsync(scenario.TenantId, "DESEMPENO", "Desempeño sobresaliente");
        var employeeId = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Bianca", "Bandeja", "EMP-BJ-A", "bianca.bj.a@empresa.test");

        // rec1 → APLICADA (third-party apply).
        var (rec1, rec1Token) = await CreateRecognitionAsync(managerClient, employeeId, RecognitionBody(recTypeId));
        var rec1Apply = await PatchRecognitionAsync(recognitionAuthorizer, employeeId, rec1, "decision", rec1Token,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, rec1Apply.StatusCode);

        // rec2 → EN_REVISION.
        await CreateRecognitionAsync(managerClient, employeeId, RecognitionBody(recTypeId));

        // rec3 → ANULADA (trámite withdrawal from EN_REVISION).
        var (rec3, rec3Token) = await CreateRecognitionAsync(managerClient, employeeId, RecognitionBody(recTypeId));
        var rec3Annul = await PatchRecognitionAsync(managerClient, employeeId, rec3, "annulment", rec3Token,
            new { reason = "Registrado por error." });
        Assert.Equal(HttpStatusCode.OK, rec3Annul.StatusCode);

        // Recognitions bandeja (default): StatusCounts cover every status; items exclude ANULADA.
        var recBandeja = await managerClient.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/recognitions/query", new { });
        Assert.Equal(HttpStatusCode.OK, recBandeja.StatusCode);
        using (var doc = JsonDocument.Parse(await recBandeja.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, doc.RootElement.GetProperty("totalCount").GetInt32());
            var counts = doc.RootElement.GetProperty("statusCounts");
            Assert.Equal(1, counts.GetProperty("APLICADA").GetInt32());
            Assert.Equal(1, counts.GetProperty("EN_REVISION").GetInt32());
            Assert.Equal(1, counts.GetProperty("ANULADA").GetInt32());
            foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                Assert.NotEqual("ANULADA", item.GetProperty("statusCode").GetString());
            }
        }

        // includeAnnulled → all three.
        var recAll = await managerClient.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/recognitions/query", new { includeAnnulled = true });
        using (var doc = JsonDocument.Parse(await recAll.Content.ReadAsStringAsync()))
        {
            Assert.Equal(3, doc.RootElement.GetProperty("totalCount").GetInt32());
        }

        // Recognitions export xlsx → 200 + spreadsheet content-type + non-empty body.
        var recExportXlsx = await managerClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/recognitions/export?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, recExportXlsx.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            recExportXlsx.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await recExportXlsx.Content.ReadAsByteArrayAsync());

        // Recognitions export json (default excludes annulled → 2 rows).
        var recExportJson = await managerClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/recognitions/export?format=json");
        Assert.Equal(HttpStatusCode.OK, recExportJson.StatusCode);
        using (var doc = JsonDocument.Parse(await recExportJson.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, doc.RootElement.GetArrayLength());
        }

        // Disciplinary actions: da1 → APLICADA, da2 → EN_REVISION.
        var daTypeId = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "ESCRITA", "Amonestación escrita", appliesSuspension: false);
        var daCauseId = await SeedDisciplinaryActionCauseAsync(scenario.TenantId, "TARDANZA", "Llegadas tardías", deductionConceptTypeCode: null);

        var (da1, da1Token) = await CreateDisciplinaryActionAsync(managerClient, employeeId, DisciplinaryActionBody(daTypeId, daCauseId));
        var da1Apply = await PatchDisciplinaryActionAsync(disciplinaryAuthorizer, employeeId, da1, "decision", da1Token,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, da1Apply.StatusCode);
        await CreateDisciplinaryActionAsync(managerClient, employeeId, DisciplinaryActionBody(daTypeId, daCauseId));

        var daBandeja = await managerClient.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/disciplinary-actions/query", new { });
        Assert.Equal(HttpStatusCode.OK, daBandeja.StatusCode);
        using (var doc = JsonDocument.Parse(await daBandeja.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, doc.RootElement.GetProperty("totalCount").GetInt32());
            var counts = doc.RootElement.GetProperty("statusCounts");
            Assert.Equal(1, counts.GetProperty("APLICADA").GetInt32());
            Assert.Equal(1, counts.GetProperty("EN_REVISION").GetInt32());
        }

        var daExportXlsx = await managerClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/disciplinary-actions/export?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, daExportXlsx.StatusCode);
        Assert.NotEmpty(await daExportXlsx.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task DisciplinaryAction_PayrollInput_EmitsRowPerEffectExcludesRevokedAndRequiresRange()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var authorizerClient = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions));

        var conceptCode = await SeedCompensationConceptAsync(scenario.TenantId, "DESC_DISCIPLINARIO", "Descuento disciplinario", CompensationNature.Egreso);
        var suspType = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "SUSPENSION_SIN_GOCE", "Suspensión sin goce", appliesSuspension: true);
        var escritaType = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "ESCRITA", "Amonestación escrita", appliesSuspension: false);
        var causeId = await SeedDisciplinaryActionCauseAsync(scenario.TenantId, "INASISTENCIA", "Inasistencia injustificada", conceptCode);

        var employeeA = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Diana", "Descuento", "EMP-PI-A", "diana.pi.a@empresa.test");
        var employeeB = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Ruben", "Revocado", "EMP-PI-B", "ruben.pi.b@empresa.test");

        // Employee A: APLICADA with deduction $25 + suspension 3 days, incident 2026-04-28 (inside the range).
        var (daA, daAToken) = await CreateDisciplinaryActionAsync(managerClient, employeeA, DisciplinaryActionBody(
            suspType, causeId, hasPayrollDeduction: true, deductionAmount: 25m, currencyCode: "USD",
            suspensionStartDate: "2026-05-10", suspensionEndDate: "2026-05-12"));
        var daAApply = await PatchDisciplinaryActionAsync(authorizerClient, employeeA, daA, "decision", daAToken,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, daAApply.StatusCode);

        // Employee B: APLICADA with a deduction, then REVOKED (→ ANULADA, excluded from the input — RN-14/RN-15).
        var (daB, daBToken) = await CreateDisciplinaryActionAsync(managerClient, employeeB, DisciplinaryActionBody(
            escritaType, causeId, hasPayrollDeduction: true, deductionAmount: 40m, currencyCode: "USD"));
        var daBApply = await PatchDisciplinaryActionAsync(authorizerClient, employeeB, daB, "decision", daBToken,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, daBApply.StatusCode);
        Guid daBAppliedToken;
        using (var doc = JsonDocument.Parse(await daBApply.Content.ReadAsStringAsync()))
        {
            daBAppliedToken = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        var daBRevoke = await PatchDisciplinaryActionAsync(authorizerClient, employeeB, daB, "annulment", daBAppliedToken,
            new { reason = "Registrada por error." });
        Assert.Equal(HttpStatusCode.OK, daBRevoke.StatusCode);

        // Payroll input over the range → exactly 2 rows for A (DESCUENTO + SUSPENSION_SIN_GOCE); B excluded.
        var input = await managerClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/disciplinary-actions/payroll-input/export?format=json&startDate=2026-04-01&endDate=2026-05-31");
        Assert.Equal(HttpStatusCode.OK, input.StatusCode);
        using (var doc = JsonDocument.Parse(await input.Content.ReadAsStringAsync()))
        {
            var rows = doc.RootElement.EnumerateArray().ToArray();
            Assert.Equal(2, rows.Length);
            Assert.All(rows, row => Assert.Equal("EMP-PI-A", row.GetProperty("CodigoEmpleado").GetString()));

            var deduction = rows.Single(row => row.GetProperty("Efecto").GetString() == "DESCUENTO");
            Assert.Equal(25m, deduction.GetProperty("Monto").GetDecimal());
            Assert.Equal("USD", deduction.GetProperty("Moneda").GetString());
            Assert.Equal("Descuento disciplinario", deduction.GetProperty("ConceptoDescuento").GetString());

            var suspension = rows.Single(row => row.GetProperty("Efecto").GetString() == "SUSPENSION_SIN_GOCE");
            Assert.Equal(3, suspension.GetProperty("Dias").GetInt32());
            Assert.Equal("2026-05-10", suspension.GetProperty("FechaInicio").GetString());
            Assert.Equal("2026-05-12", suspension.GetProperty("FechaFin").GetString());
        }

        // xlsx variant → 200 + spreadsheet content-type.
        var inputXlsx = await managerClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/disciplinary-actions/payroll-input/export?format=xlsx&startDate=2026-04-01&endDate=2026-05-31");
        Assert.Equal(HttpStatusCode.OK, inputXlsx.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            inputXlsx.Content.Headers.ContentType?.MediaType);

        // Missing range → 422 PERSONNEL_TRANSACTION_RANGE_REQUIRED.
        var missingRange = await managerClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/disciplinary-actions/payroll-input/export?format=json");
        await AssertProblemDetailsAsync(missingRange, HttpStatusCode.UnprocessableEntity, "PERSONNEL_TRANSACTION_RANGE_REQUIRED");
    }

    // ── PR-6: time-availability query + export (§3.11 / RF-013) ────────────────────────────────────

    private async Task SeedEmploymentAssignmentAsync(
        Guid tenantId, Guid filePublicId, string contractTypeCode, DateTime endDate)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fileInternalId = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .Where(item => item.PublicId == filePublicId).Select(item => item.Id).SingleAsync();

        var assignment = PersonnelFileEmploymentAssignment.Create(
            assignmentTypeCode: "NOMBRAMIENTO",
            contractTypeCode: contractTypeCode,
            workdayCode: null,
            payrollTypeCode: null,
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: null,
            startDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: endDate,
            isPrimary: true,
            isActive: true,
            notes: null);
        assignment.BindToPersonnelFile(fileInternalId);
        assignment.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmploymentAssignment>().Add(assignment);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task TimeAvailability_Query_UnifiesSuspensionsAndTemporaryContractEndsWithStableContract()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var managerUserId = scenario.ActorUserId;
        var authorizerUserId = Guid.NewGuid();
        var viewerUserId = Guid.NewGuid();
        var strangerUserId = Guid.NewGuid();

        using var managerClient = factory.CreateClientFor(RecognitionContext(scenario, managerUserId, PersonnelFilePermissionCodes.Admin));
        using var authorizerClient = factory.CreateClientFor(RecognitionContext(scenario, authorizerUserId, PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions));
        using var viewerClient = factory.CreateClientFor(RecognitionContext(scenario, viewerUserId, PersonnelFilePermissionCodes.ViewTimeAvailability));
        using var strangerClient = factory.CreateClientFor(RecognitionContext(scenario, strangerUserId, PersonnelFilePermissionCodes.ViewRecognitions));

        var suspType = await SeedDisciplinaryActionTypeAsync(scenario.TenantId, "SUSPENSION_SIN_GOCE", "Suspensión sin goce", appliesSuspension: true);
        var causeId = await SeedDisciplinaryActionCauseAsync(scenario.TenantId, "INASISTENCIA", "Inasistencia injustificada", deductionConceptTypeCode: null);

        // Employee A: APLICADA suspension 2026-05-28 → 2026-06-03 (partially intersects the 2026-06-01..15 range).
        var employeeA = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Ana", "Suspendida", "EMP-TA-A", "ana.ta.a@empresa.test");
        var (daA, daAToken) = await CreateDisciplinaryActionAsync(managerClient, employeeA, DisciplinaryActionBody(
            suspType, causeId, incidentDate: "2026-05-28", suspensionStartDate: "2026-05-28", suspensionEndDate: "2026-06-03"));
        var daAApply = await PatchDisciplinaryActionAsync(authorizerClient, employeeA, daA, "decision", daAToken,
            new { decision = "APLICAR", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, daAApply.StatusCode);

        // Employee B: active TEMPORARY contract (PLAZO_FIJO, IsTemporary=true) ending 2026-06-10 (inside the range).
        var employeeB = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Bruno", "Temporal", "EMP-TA-B", "bruno.ta.b@empresa.test");
        await SeedEmploymentAssignmentAsync(scenario.TenantId, employeeB, "PLAZO_FIJO", new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        // Employee C: active INDEFINIDO contract ending 2026-06-12 (in range but NOT temporary → excluded).
        var employeeC = await SeedRecognitionEmployeeAsync(scenario.TenantId, "Carla", "Indefinida", "EMP-TA-C", "carla.ta.c@empresa.test");
        await SeedEmploymentAssignmentAsync(scenario.TenantId, employeeC, "INDEFINIDO", new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        // Query the full range with the dedicated ViewTimeAvailability permission.
        var query = await viewerClient.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/time-availability/query",
            new { startDate = "2026-06-01", endDate = "2026-06-15" });
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
        using (var doc = JsonDocument.Parse(await query.Content.ReadAsStringAsync()))
        {
            var root = doc.RootElement;
            Assert.Equal(2, root.GetProperty("totalCount").GetInt32());

            // activeSources always advertises the two F1 families.
            var activeSources = root.GetProperty("activeSources").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Contains("SUSPENSION", activeSources);
            Assert.Contains("FIN_CONTRATO_TEMPORAL", activeSources);

            var counts = root.GetProperty("categoryCounts");
            Assert.Equal(1, counts.GetProperty("SUSPENSION").GetInt32());
            Assert.Equal(1, counts.GetProperty("FIN_CONTRATO_TEMPORAL").GetInt32());

            var rows = root.GetProperty("rows").EnumerateArray().ToArray();
            Assert.Equal(2, rows.Length);

            // Source 1: the suspension appears with its REAL dates (not clamped to the range) and days from the record.
            var suspension = rows.Single(row => row.GetProperty("categoryCode").GetString() == "SUSPENSION");
            Assert.Equal(employeeA, suspension.GetProperty("personnelFilePublicId").GetGuid());
            Assert.Equal("2026-05-28", suspension.GetProperty("startDate").GetString());
            Assert.Equal("2026-06-03", suspension.GetProperty("endDate").GetString());
            Assert.Equal(7, suspension.GetProperty("days").GetInt32());
            Assert.Equal("EMPLOYEE_RELATIONS", suspension.GetProperty("sourceModule").GetString());
            Assert.Equal(daA, suspension.GetProperty("referencePublicId").GetGuid());

            // Source 2: the temporary contract end is a single-day row (days=1, statusCode VIGENTE, EMPLOYMENT).
            var contractEnd = rows.Single(row => row.GetProperty("categoryCode").GetString() == "FIN_CONTRATO_TEMPORAL");
            Assert.Equal(employeeB, contractEnd.GetProperty("personnelFilePublicId").GetGuid());
            Assert.Equal("2026-06-10", contractEnd.GetProperty("startDate").GetString());
            Assert.Equal("2026-06-10", contractEnd.GetProperty("endDate").GetString());
            Assert.Equal(1, contractEnd.GetProperty("days").GetInt32());
            Assert.Equal("VIGENTE", contractEnd.GetProperty("statusCode").GetString());
            Assert.Equal("EMPLOYMENT", contractEnd.GetProperty("sourceModule").GetString());

            // Employee C (INDEFINIDO) never appears.
            Assert.DoesNotContain(rows, row => row.GetProperty("personnelFilePublicId").GetGuid() == employeeC);
        }

        // categoryCodes filter → only SUSPENSION rows; FIN_CONTRATO_TEMPORAL excluded.
        var filtered = await viewerClient.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/time-availability/query",
            new { startDate = "2026-06-01", endDate = "2026-06-15", categoryCodes = new[] { "SUSPENSION" } });
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        using (var doc = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, doc.RootElement.GetProperty("categoryCounts").GetProperty("SUSPENSION").GetInt32());
            Assert.False(doc.RootElement.GetProperty("categoryCounts").TryGetProperty("FIN_CONTRATO_TEMPORAL", out _));
            Assert.All(
                doc.RootElement.GetProperty("rows").EnumerateArray(),
                row => Assert.Equal("SUSPENSION", row.GetProperty("categoryCode").GetString()));
        }

        // Missing range → 422 TIME_AVAILABILITY_RANGE_REQUIRED.
        var missingRange = await viewerClient.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/time-availability/query", new { });
        await AssertProblemDetailsAsync(missingRange, HttpStatusCode.UnprocessableEntity, "TIME_AVAILABILITY_RANGE_REQUIRED");

        // Incoherent range (start > end) → 422 TIME_AVAILABILITY_RANGE_INVALID.
        var invalidRange = await viewerClient.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/time-availability/query",
            new { startDate = "2026-06-15", endDate = "2026-06-01" });
        await AssertProblemDetailsAsync(invalidRange, HttpStatusCode.UnprocessableEntity, "TIME_AVAILABILITY_RANGE_INVALID");

        // A user without ViewTimeAvailability (only ViewRecognitions) → 403.
        var forbidden = await strangerClient.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/time-availability/query",
            new { startDate = "2026-06-01", endDate = "2026-06-15" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Export xlsx → 200 + spreadsheet content-type + non-empty body.
        var exportXlsx = await viewerClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/time-availability/export?format=xlsx&startDate=2026-06-01&endDate=2026-06-15");
        Assert.Equal(HttpStatusCode.OK, exportXlsx.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            exportXlsx.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await exportXlsx.Content.ReadAsByteArrayAsync());

        // Export json → the 2 rows with Spanish headers.
        var exportJson = await viewerClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/time-availability/export?format=json&startDate=2026-06-01&endDate=2026-06-15");
        Assert.Equal(HttpStatusCode.OK, exportJson.StatusCode);
        using (var doc = JsonDocument.Parse(await exportJson.Content.ReadAsStringAsync()))
        {
            var rows = doc.RootElement.EnumerateArray().ToArray();
            Assert.Equal(2, rows.Length);
            Assert.All(rows, row => Assert.True(row.TryGetProperty("Categoria", out _)));
            Assert.Contains(rows, row => row.GetProperty("Fuente").GetString() == "EMPLOYEE_RELATIONS");
            Assert.Contains(rows, row => row.GetProperty("Fuente").GetString() == "EMPLOYMENT");
        }
    }
}
