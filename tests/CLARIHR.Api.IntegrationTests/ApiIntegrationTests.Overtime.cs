using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Overtime;
using CLARIHR.Domain.Preferences;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the overtime-record ("horas extras del empleado", REQ-007 PR-3) flow: the CRUD
/// round-trip (2 h 30 m → 2.50 decimal hours, factor override with its mandatory note), the write guards (65 min →
/// 422, factor override without note → 422, work date &gt; +366 days → 422, daily cap exceeded → 422), the DUAL
/// portal channel (preference off → 403, preference on → own PORTAL record with the subject as requester, another
/// file → 403, self-edit of the own EN_REVISION portal draft, self-read), the authorizer resolution with the
/// TRIPLE anti-self (registrar / subject / requester → 403, Admin without the grant → 403, a fourth authorized user
/// → AUTORIZADA), the reject-without-note guard, the future-date create + authorize, the re-imputation (only
/// AUTORIZADA) and the If-Match concurrency guard (missing → 400, stale → 409). Employees + masters are seeded
/// directly. Reuses <c>SeedOneTimeIncomeCandidateAsync</c> (completed employee + primary assignment).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static TestUserContext OvertimeManagerContext(IntegrationTestScenario scenario, Guid? userId = null) =>
        TestUserContext.Authenticated(userId ?? scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.Admin);

    private static TestUserContext OvertimeAuthorizerContext(IntegrationTestScenario scenario, Guid userId) =>
        TestUserContext.Authenticated(userId, scenario.TenantId, PersonnelFilePermissionCodes.AuthorizeOvertimeRecords);

    private async Task<(Guid TypeId, Guid JustificationId)> SeedOvertimeMastersAsync(
        Guid tenantId,
        string typeCode = "HED",
        decimal typeFactor = 2.00m,
        string justificationCode = "PROD",
        bool typeActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var type = OvertimeType.Create(typeCode, $"Tipo {typeCode}", typeFactor, null, 1);
        type.SetTenantId(tenantId);
        if (!typeActive)
        {
            type.Inactivate();
        }

        dbContext.Set<OvertimeType>().Add(type);

        var justification = OvertimeJustificationType.Create(justificationCode, $"Justificación {justificationCode}", null, 1);
        justification.SetTenantId(tenantId);
        dbContext.Set<OvertimeJustificationType>().Add(justification);

        await dbContext.SaveChangesAsync();
        return (type.PublicId, justification.PublicId);
    }

    private async Task SetOvertimePreferencesAsync(Guid tenantId, bool? selfServiceEnabled, int? maxDailyMinutes)
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

        preference.SetOvertimePolicies(selfServiceEnabled, maxDailyMinutes);
        await dbContext.SaveChangesAsync();
    }

    private static object OvertimeBody(
        Guid typeId,
        Guid justificationId,
        Guid? requesterFilePublicId,
        int durationHours = 2,
        int durationMinutes = 30,
        decimal? factorApplied = null,
        string? factorOverrideNote = null,
        int workDateOffsetDays = -1,
        string payrollPeriodLabel = "Quincena 13/2026")
    {
        var workDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(workDateOffsetDays).ToString("yyyy-MM-dd");
        return new
        {
            workDate,
            overtimeTypePublicId = typeId,
            factorApplied,
            factorOverrideNote,
            durationHours,
            durationMinutes,
            startTime = (string?)null,
            endTime = (string?)null,
            justificationTypePublicId = justificationId,
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId,
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel,
            payrollPeriodEndDate = (string?)null
        };
    }

    private static async Task<(Guid RecordId, Guid Token)> CreateOvertimeAsync(HttpClient client, Guid fileId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/overtime-records", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return (
            doc.RootElement.GetProperty("overtimeRecordPublicId").GetGuid(),
            doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> PatchOvertimeAsync(
        HttpClient client, Guid fileId, Guid recordId, string action, Guid? token, object body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/overtime-records/{recordId}/{action}")
        {
            Content = JsonContent.Create(body)
        };
        if (token is { } value)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{value}\"");
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Overtime_CreateByHours_ReturnsEnRevisionWithDecimalHours()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Óscar", "Overtime", "EMP-OT-A", "oscar.ot.a@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rodrigo", "Solicitante", "EMP-OT-A2", "rodrigo.ot.a@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records", OvertimeBody(typeId, justId, requesterId));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.Equal("EN_REVISION", root.GetProperty("statusCode").GetString());
        Assert.Equal("RRHH", root.GetProperty("originChannel").GetString());
        Assert.Equal(2.50m, root.GetProperty("durationDecimalHours").GetDecimal());
        Assert.Equal(2.00m, root.GetProperty("factorApplied").GetDecimal());
        Assert.Equal(2.00m, root.GetProperty("typeFactorSnapshot").GetDecimal());
        Assert.Equal(requesterId, root.GetProperty("requesterFilePublicId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("overtimeTypeCodeSnapshot").GetString()));
    }

    [Fact]
    public async Task Overtime_CreateWithFactorOverrideAndNote_Succeeds()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Fabio", "Factor", "EMP-OT-B", "fabio.ot.b@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rita", "Solicitante", "EMP-OT-B2", "rita.ot.b@empresa.test");

        var (recordId, _) = await CreateOvertimeAsync(
            client, fileId, OvertimeBody(typeId, justId, requesterId, factorApplied: 2.50m, factorOverrideNote: "Ajuste autorizado"));

        var detail = await client.GetAsync($"/api/v1/personnel-files/{fileId}/overtime-records/{recordId}");
        detail.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal(2.50m, doc.RootElement.GetProperty("factorApplied").GetDecimal());
        Assert.Equal("Ajuste autorizado", doc.RootElement.GetProperty("factorOverrideNote").GetString());
    }

    [Fact]
    public async Task Overtime_CreateWithFactorOverrideWithoutNote_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Nora", "SinNota", "EMP-OT-C", "nora.ot.c@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rosa", "Solicitante", "EMP-OT-C2", "rosa.ot.c@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            OvertimeBody(typeId, justId, requesterId, factorApplied: 2.50m, factorOverrideNote: null));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_FACTOR_NOTE_REQUIRED");
    }

    [Fact]
    public async Task Overtime_CreateWith65Minutes_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Dora", "Duración", "EMP-OT-D", "dora.ot.d@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Raúl", "Solicitante", "EMP-OT-D2", "raul.ot.d@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            OvertimeBody(typeId, justId, requesterId, durationHours: 1, durationMinutes: 65));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_DURATION_MINUTES_INVALID");
    }

    [Fact]
    public async Task Overtime_CreateWithInactiveType_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId, typeActive: false);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Iván", "Inactivo", "EMP-OT-E", "ivan.ot.e@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rebeca", "Solicitante", "EMP-OT-E2", "rebeca.ot.e@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records", OvertimeBody(typeId, justId, requesterId));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_TYPE_INVALID");
    }

    [Fact]
    public async Task Overtime_CreateWithWorkDateTooFar_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Lejos", "Futuro", "EMP-OT-F", "lejos.ot.f@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ramiro", "Solicitante", "EMP-OT-F2", "ramiro.ot.f@empresa.test");

        // 400 days ahead > sanity cap (366) → 422.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            OvertimeBody(typeId, justId, requesterId, workDateOffsetDays: 400));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_WORK_DATE_TOO_FAR");
    }

    [Fact]
    public async Task Overtime_CreateExceedingDailyCap_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        await SetOvertimePreferencesAsync(scenario.TenantId, selfServiceEnabled: null, maxDailyMinutes: 180);
        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Capa", "Tope", "EMP-OT-G", "capa.ot.g@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Renata", "Solicitante", "EMP-OT-G2", "renata.ot.g@empresa.test");

        // First 2 h (120 min) on the work date → OK.
        await CreateOvertimeAsync(client, fileId, OvertimeBody(typeId, justId, requesterId, durationHours: 2, durationMinutes: 0));

        // Second 2 h same day → 120 + 120 = 240 > cap 180 → 422.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            OvertimeBody(typeId, justId, requesterId, durationHours: 2, durationMinutes: 0));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_DAILY_CAP_EXCEEDED");
    }

    [Fact]
    public async Task Overtime_FutureWorkDate_CreateAndAuthorizeSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Futura", "Jornada", "EMP-OT-H", "futura.ot.h@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Regina", "Solicitante", "EMP-OT-H2", "regina.ot.h@empresa.test");

        using var registrarClient = factory.CreateClientFor(OvertimeManagerContext(scenario, registrarUserId));
        // Organized shift 10 days ahead → create OK.
        var (recordId, token) = await CreateOvertimeAsync(
            registrarClient, fileId, OvertimeBody(typeId, justId, requesterId, workDateOffsetDays: 10));

        using var authorizerClient = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, authorizerUserId));
        var response = await PatchOvertimeAsync(
            authorizerClient, fileId, recordId, "resolution", token, new { targetStatusCode = "AUTORIZADA", note = (string?)null });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("AUTORIZADA", doc.RootElement.GetProperty("statusCode").GetString());
    }

    [Fact]
    public async Task Overtime_Resolution_EnforcesTripleAntiSelf_AndAuthorizesFourthParty()
    {
        var scenario = await factory.ResetDatabaseAsync();

        var registrarUserId = Guid.NewGuid();
        var subjectUserId = Guid.NewGuid();
        var requesterUserId = Guid.NewGuid();
        var fourthUserId = Guid.NewGuid();

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Sujeta", "Empleada", "EMP-OT-I", "sujeta.ot.i@empresa.test", linkedUserPublicId: subjectUserId);
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Solicitante", "Vinculado", "EMP-OT-I2", "solicitante.ot.i@empresa.test", linkedUserPublicId: requesterUserId);

        using var registrarClient = factory.CreateClientFor(OvertimeManagerContext(scenario, registrarUserId));
        var (recordId, token) = await CreateOvertimeAsync(registrarClient, fileId, OvertimeBody(typeId, justId, requesterId));

        var authorizeBody = new { targetStatusCode = "AUTORIZADA", note = (string?)null };

        // (a) The REGISTRAR (with the Authorize grant) cannot authorize their own registration → 403.
        using (var registrarAuthorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, registrarUserId)))
        {
            var response = await PatchOvertimeAsync(registrarAuthorizer, fileId, recordId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "OVERTIME_SELF_APPROVAL_FORBIDDEN");
        }

        // (b) The SUBJECT (with the Authorize grant) cannot authorize their own record → 403.
        using (var subjectAuthorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, subjectUserId)))
        {
            var response = await PatchOvertimeAsync(subjectAuthorizer, fileId, recordId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "OVERTIME_SELF_APPROVAL_FORBIDDEN");
        }

        // (c) The REQUESTER with a linked login (the third leg) cannot authorize → 403.
        using (var requesterAuthorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, requesterUserId)))
        {
            var response = await PatchOvertimeAsync(requesterAuthorizer, fileId, recordId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "OVERTIME_SELF_APPROVAL_FORBIDDEN");
        }

        // (d) A pure Admin (no AuthorizeOvertimeRecords grant) is blocked by the policy → 403.
        using (var adminOnly = factory.CreateClientFor(OvertimeManagerContext(scenario, fourthUserId)))
        {
            var response = await PatchOvertimeAsync(adminOnly, fileId, recordId, "resolution", token, authorizeBody);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // (e) A fourth authorized user (none of subject/registrar/requester) authorizes → AUTORIZADA.
        using (var fourthAuthorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, fourthUserId)))
        {
            var response = await PatchOvertimeAsync(fourthAuthorizer, fileId, recordId, "resolution", token, authorizeBody);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("AUTORIZADA", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.Equal(fourthUserId, doc.RootElement.GetProperty("decidedByUserPublicId").GetGuid());
        }
    }

    [Fact]
    public async Task Overtime_RejectWithoutNote_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rechazo", "SinNota", "EMP-OT-J", "rechazo.ot.j@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Regina", "Solicitante", "EMP-OT-J2", "regina.ot.j@empresa.test");

        using var registrarClient = factory.CreateClientFor(OvertimeManagerContext(scenario, registrarUserId));
        var (recordId, token) = await CreateOvertimeAsync(registrarClient, fileId, OvertimeBody(typeId, justId, requesterId));

        using var authorizerClient = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, authorizerUserId));
        var response = await PatchOvertimeAsync(
            authorizerClient, fileId, recordId, "resolution", token, new { targetStatusCode = "RECHAZADA", note = (string?)null });
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_DECISION_NOTE_REQUIRED");
    }

    [Fact]
    public async Task Overtime_RetargetOnEnRevision_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Temprano", "Reimputa", "EMP-OT-K", "temprano.ot.k@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Renata", "Solicitante", "EMP-OT-K2", "renata.ot.k@empresa.test");

        var (recordId, token) = await CreateOvertimeAsync(client, fileId, OvertimeBody(typeId, justId, requesterId));

        var response = await PatchOvertimeAsync(
            client, fileId, recordId, "period", token,
            new { payrollTypeCode = "QUINCENAL", payrollPeriodPublicId = (Guid?)null, payrollPeriodLabel = "Quincena 14/2026", payrollPeriodEndDate = (string?)null });
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_NOT_RETARGETABLE");
    }

    [Fact]
    public async Task Overtime_AuthorizeRetargetAndRevoke_FullLifecycle()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Vero", "Vigente", "EMP-OT-L", "vero.ot.l@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ronaldo", "Solicitante", "EMP-OT-L2", "ronaldo.ot.l@empresa.test");

        using var managerClient = factory.CreateClientFor(OvertimeManagerContext(scenario, registrarUserId));
        using var authorizerClient = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, authorizerUserId));

        var (recordId, createToken) = await CreateOvertimeAsync(managerClient, fileId, OvertimeBody(typeId, justId, requesterId));

        var authorizeResponse = await PatchOvertimeAsync(
            authorizerClient, fileId, recordId, "resolution", createToken, new { targetStatusCode = "AUTORIZADA", note = (string?)null });
        authorizeResponse.EnsureSuccessStatusCode();
        var authorizedToken = await ReadTokenAsync(authorizeResponse);

        // Re-target (Manage) the AUTORIZADA record → still AUTORIZADA with the new label.
        var retargetResponse = await PatchOvertimeAsync(
            managerClient, fileId, recordId, "period", authorizedToken,
            new { payrollTypeCode = "QUINCENAL", payrollPeriodPublicId = (Guid?)null, payrollPeriodLabel = "Quincena 14/2026", payrollPeriodEndDate = (string?)null });
        retargetResponse.EnsureSuccessStatusCode();
        Guid retargetedToken;
        using (var doc = JsonDocument.Parse(await retargetResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("AUTORIZADA", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.Equal("Quincena 14/2026", doc.RootElement.GetProperty("payrollPeriodLabel").GetString());
            retargetedToken = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        // Revoke (Authorize) from AUTORIZADA → ANULADA.
        var revokeResponse = await PatchOvertimeAsync(
            authorizerClient, fileId, recordId, "revocation", retargetedToken, new { reason = "Ya no aplica" });
        revokeResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await revokeResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.False(doc.RootElement.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task Overtime_ResolutionWithoutIfMatch_Returns400And409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Íngrid", "IfMatch", "EMP-OT-M", "ingrid.ot.m@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Renzo", "Solicitante", "EMP-OT-M2", "renzo.ot.m@empresa.test");

        using var registrarClient = factory.CreateClientFor(OvertimeManagerContext(scenario, registrarUserId));
        var (recordId, token) = await CreateOvertimeAsync(registrarClient, fileId, OvertimeBody(typeId, justId, requesterId));

        using var authorizerClient = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, authorizerUserId));

        var missing = await PatchOvertimeAsync(
            authorizerClient, fileId, recordId, "resolution", token: null, new { targetStatusCode = "AUTORIZADA", note = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var stale = await PatchOvertimeAsync(
            authorizerClient, fileId, recordId, "resolution", Guid.NewGuid(), new { targetStatusCode = "AUTORIZADA", note = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        _ = token;
    }

    // ── Portal (dual channel) ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Overtime_SelfCreateWithPreferenceOff_Returns403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var employeeUserId = Guid.NewGuid();

        await SetOvertimePreferencesAsync(scenario.TenantId, selfServiceEnabled: false, maxDailyMinutes: null);
        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Porta", "Off", "EMP-OT-N", "porta.ot.n@empresa.test", linkedUserPublicId: employeeUserId);

        using var selfClient = factory.CreateClientFor(TestUserContext.Authenticated(employeeUserId, scenario.TenantId));
        var response = await selfClient.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records", OvertimeBody(typeId, justId, requesterFilePublicId: null));
        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "OVERTIME_SELF_SERVICE_DISABLED");
    }

    [Fact]
    public async Task Overtime_SelfCreateWithPreferenceOn_CreatesPortalRecordWithSubjectRequester()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var employeeUserId = Guid.NewGuid();

        await SetOvertimePreferencesAsync(scenario.TenantId, selfServiceEnabled: true, maxDailyMinutes: null);
        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Porta", "On", "EMP-OT-O", "porta.ot.o@empresa.test", linkedUserPublicId: employeeUserId);

        using var selfClient = factory.CreateClientFor(TestUserContext.Authenticated(employeeUserId, scenario.TenantId));
        var response = await selfClient.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records", OvertimeBody(typeId, justId, requesterFilePublicId: null));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.Equal("EN_REVISION", root.GetProperty("statusCode").GetString());
        Assert.Equal("PORTAL", root.GetProperty("originChannel").GetString());
        // The requester is the subject employee (own file) on the portal channel.
        Assert.Equal(fileId, root.GetProperty("requesterFilePublicId").GetGuid());

        // Self-read of their own records passes without a view permission (P-12).
        var list = await selfClient.GetAsync($"/api/v1/personnel-files/{fileId}/overtime-records");
        list.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1, listDoc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Overtime_SelfCreateOnOtherFile_Returns403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var employeeUserId = Guid.NewGuid();

        await SetOvertimePreferencesAsync(scenario.TenantId, selfServiceEnabled: true, maxDailyMinutes: null);
        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        // The caller is linked to their OWN file, but posts to a DIFFERENT employee's file.
        await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Propio", "Empleado", "EMP-OT-P", "propio.ot.p@empresa.test", linkedUserPublicId: employeeUserId);
        var otherFileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ajeno", "Empleado", "EMP-OT-P2", "ajeno.ot.p@empresa.test");

        using var selfClient = factory.CreateClientFor(TestUserContext.Authenticated(employeeUserId, scenario.TenantId));
        var response = await selfClient.PostAsJsonAsync(
            $"/api/v1/personnel-files/{otherFileId}/overtime-records", OvertimeBody(typeId, justId, requesterFilePublicId: null));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Overtime_SelfEditOwnPortalDraft_Succeeds()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var employeeUserId = Guid.NewGuid();

        await SetOvertimePreferencesAsync(scenario.TenantId, selfServiceEnabled: true, maxDailyMinutes: null);
        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Edita", "Propio", "EMP-OT-Q", "edita.ot.q@empresa.test", linkedUserPublicId: employeeUserId);

        using var selfClient = factory.CreateClientFor(TestUserContext.Authenticated(employeeUserId, scenario.TenantId));
        var (recordId, token) = await CreateOvertimeAsync(selfClient, fileId, OvertimeBody(typeId, justId, requesterFilePublicId: null, durationHours: 1, durationMinutes: 0));

        // Self-edit of the own EN_REVISION portal draft → 200 with the new duration.
        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/personnel-files/{fileId}/overtime-records/{recordId}")
        {
            Content = JsonContent.Create(OvertimeBody(typeId, justId, requesterFilePublicId: null, durationHours: 3, durationMinutes: 0))
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        var response = await selfClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3.00m, doc.RootElement.GetProperty("durationDecimalHours").GetDecimal());
        Assert.Equal("PORTAL", doc.RootElement.GetProperty("originChannel").GetString());
    }
}
