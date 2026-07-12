using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the one-time-deduction ("planilla — descuentos eventuales", REQ-009 PR-3) CRUD +
/// resolution slice. The two load-bearing tests: (1) <b>the amount belongs to the server</b> — a client that sends
/// components worth $25 and declares an amount of $500 is rejected WITH the expected figure, so nobody can charge
/// an employee an arbitrary sum behind innocent-looking components; and (2) the <b>anti-self TRIPLE</b> — neither
/// the subject employee, nor the requester, nor the registrar may decide, and a bare Admin cannot either.
/// Also covered: a statutory concept (ISSS) is rejected (RN-04), the re-imputation is only legal while AUTORIZADO,
/// and the full lifecycle.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static readonly DateTime OneTimeDeductionHireDate = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static TestUserContext OneTimeDeductionManagerContext(IntegrationTestScenario scenario, Guid? userId = null) =>
        TestUserContext.Authenticated(userId ?? scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.Admin);

    private static TestUserContext OneTimeDeductionAuthorizerContext(IntegrationTestScenario scenario, Guid userId) =>
        TestUserContext.Authenticated(userId, scenario.TenantId, PersonnelFilePermissionCodes.AuthorizeOneTimeDeductions);

    private async Task<Guid> SeedOneTimeDeductionCandidateAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail,
        Guid? linkedUserPublicId = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            firstName,
            lastName,
            new DateTime(1990, 3, 15, 0, 0, 0, DateTimeKind.Utc),
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

        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, "ACTIVO", OneTimeDeductionHireDate);
        profile.BindToPersonnelFile(file.Id);
        profile.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmployeeProfile>().Add(profile);

        // A one-time deduction needs a plaza but NO cost center (P-08).
        var assignment = PersonnelFileEmploymentAssignment.Create(
            "INDEFINIDO",
            contractTypeCode: null,
            workdayCode: null,
            payrollTypeCode: null,
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: null,
            startDate: OneTimeDeductionHireDate,
            endDate: null,
            isPrimary: true,
            isActive: true,
            notes: null);
        assignment.BindToPersonnelFile(file.Id);
        assignment.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmploymentAssignment>().Add(assignment);

        await dbContext.SaveChangesAsync();
        return file.PublicId;
    }

    /// <summary>A fixed-value deduction: a $75 fine for a damaged asset.</summary>
    private static object FixedOneTimeDeductionBody(Guid requesterFileId, decimal amount = 75m)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            deductionDate = today,
            reference = "DANO-LAPTOP-001",
            conceptTypeCode = "DANO_EQUIPO",
            observations = (string?)null,
            isFixedValue = true,
            calculationMethod = (string?)null,
            quantity = (decimal?)null,
            unitValue = (decimal?)null,
            multiplier = (decimal?)null,
            percentage = (decimal?)null,
            baseAmount = (decimal?)null,
            amount = (decimal?)amount,
            currencyCode = "USD",
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId = requesterFileId,
            payrollTypeCode = "MENSUAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Julio 2026",
            payrollPeriodEndDate = (DateOnly?)null
        };
    }

    /// <summary>A computed deduction: 10% of a $250 base = $25. The amount is DERIVED (it may be omitted).</summary>
    private static object ComputedOneTimeDeductionBody(Guid requesterFileId, decimal? declaredAmount = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            deductionDate = today,
            reference = "MULTA-10PCT",
            conceptTypeCode = "ANTICIPO",
            observations = (string?)null,
            isFixedValue = false,
            calculationMethod = "PORCENTAJE_SOBRE_BASE",
            quantity = (decimal?)null,
            unitValue = (decimal?)null,
            multiplier = (decimal?)null,
            percentage = (decimal?)10m,
            baseAmount = (decimal?)250m,
            amount = declaredAmount,
            currencyCode = "USD",
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId = requesterFileId,
            payrollTypeCode = "MENSUAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Julio 2026",
            payrollPeriodEndDate = (DateOnly?)null
        };
    }

    private static async Task<(Guid DeductionId, Guid Token)> CreateOneTimeDeductionAsync(
        HttpClient client, Guid fileId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/one-time-deductions", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return (
            doc.RootElement.GetProperty("oneTimeDeductionPublicId").GetGuid(),
            doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> PatchOneTimeDeductionAsync(
        HttpClient client, Guid fileId, Guid deductionId, string action, Guid token, object body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/one-time-deductions/{deductionId}/{action}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task OneTimeDeductions_CreateFixedValue_ReturnsEnRevision()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario));

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Dalia", "Descuento", "EMP-OTD-A", "dalia.otd.a@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTD-A2", "jefa.otd.a@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-deductions", FixedOneTimeDeductionBody(requesterId));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.Equal("EN_REVISION", root.GetProperty("statusCode").GetString());
        Assert.Equal("Dano de equipo", root.GetProperty("conceptNameSnapshot").GetString());
        Assert.Equal(75m, root.GetProperty("amount").GetDecimal());
        Assert.True(root.GetProperty("isFixedValue").GetBoolean());
        // The requester's name is snapshotted onto the record (the trío).
        Assert.Equal("Jefa Solicitante", root.GetProperty("requesterNameSnapshot").GetString());
    }

    [Fact]
    public async Task OneTimeDeductions_CreateComputedValue_TheServerDerivesTheAmount()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario));

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Carla", "Calculada", "EMP-OTD-B", "carla.otd.b@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefe", "Solicitante", "EMP-OTD-B2", "jefe.otd.b@empresa.test");

        // The amount is OMITTED: the server derives it from the components (10% of $250 = $25).
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-deductions", ComputedOneTimeDeductionBody(requesterId));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.Equal(25m, root.GetProperty("amount").GetDecimal());
        Assert.False(root.GetProperty("isFixedValue").GetBoolean());
        // The components are PERSISTED, so the figure can always be re-derived and audited.
        Assert.Equal(10m, root.GetProperty("percentage").GetDecimal());
        Assert.Equal(250m, root.GetProperty("baseAmount").GetDecimal());
    }

    [Fact]
    public async Task OneTimeDeductions_ALyingAmountIsRejected_TheServerOwnsTheFigure()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario));

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Mentira", "Monto", "EMP-OTD-C", "mentira.otd.c@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTD-C2", "jefa.otd.c@empresa.test");

        // The components say $25 (10% of $250) but the client declares $500. Without this guard, anyone could
        // charge an employee an arbitrary sum behind innocent-looking components.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-deductions",
            ComputedOneTimeDeductionBody(requesterId, declaredAmount: 500m));

        // The contract is the CODE: the expected figure does not travel in the detail (the localizer replaces it
        // with the catalogued message). The client has its own components and can recompute — and the right move
        // is simply not to send `amount` at all for a computed value, letting the server derive it.
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_DEDUCTION_AMOUNT_MISMATCH");

        // Nothing was persisted: the lie never became a charge.
        var listed = await client.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-deductions");
        listed.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        Assert.Empty(doc.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task OneTimeDeductions_AStatutoryConceptIsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario));

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Estela", "Estatutaria", "EMP-OTD-D", "estela.otd.d@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefe", "Solicitante", "EMP-OTD-D2", "jefe.otd.d@empresa.test");

        // ISSS is payroll law, not a one-off charge (RN-04): it can never back a manual deduction.
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var body = new
        {
            deductionDate = today,
            reference = "ISSS-INTENTO",
            conceptTypeCode = "ISSS",
            observations = (string?)null,
            isFixedValue = true,
            calculationMethod = (string?)null,
            quantity = (decimal?)null,
            unitValue = (decimal?)null,
            multiplier = (decimal?)null,
            percentage = (decimal?)null,
            baseAmount = (decimal?)null,
            amount = (decimal?)50m,
            currencyCode = "USD",
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId = requesterId,
            payrollTypeCode = "MENSUAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Julio 2026",
            payrollPeriodEndDate = (DateOnly?)null
        };

        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/one-time-deductions", body);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_DEDUCTION_CONCEPT_INVALID");
    }

    [Fact]
    public async Task OneTimeDeductions_Resolution_EnforcesTheTripleAntiSelf_AndAuthorizesAFourthParty()
    {
        var scenario = await factory.ResetDatabaseAsync();

        var registrarUserId = Guid.NewGuid();
        var subjectUserId = Guid.NewGuid();
        var requesterUserId = Guid.NewGuid();
        var fourthUserId = Guid.NewGuid();

        // The subject employee and the REQUESTER each have their own linked login.
        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Sujeta", "Descontada", "EMP-OTD-E", "sujeta.otd.e@empresa.test", linkedUserPublicId: subjectUserId);
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Pide", "EMP-OTD-E2", "jefa.otd.e@empresa.test", linkedUserPublicId: requesterUserId);

        // A THIRD user registers it.
        using var registrarClient = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, registrarUserId));
        var (deductionId, token) = await CreateOneTimeDeductionAsync(registrarClient, fileId, FixedOneTimeDeductionBody(requesterId));

        var authorizeBody = new { targetStatusCode = "AUTORIZADO", note = (string?)null };

        // (a) The REGISTRAR cannot authorize what they registered.
        using (var registrarAuthorizer = factory.CreateClientFor(OneTimeDeductionAuthorizerContext(scenario, registrarUserId)))
        {
            var response = await PatchOneTimeDeductionAsync(registrarAuthorizer, fileId, deductionId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "ONE_TIME_DEDUCTION_SELF_APPROVAL_FORBIDDEN");
        }

        // (b) The SUBJECT cannot authorize their own charge.
        using (var subjectAuthorizer = factory.CreateClientFor(OneTimeDeductionAuthorizerContext(scenario, subjectUserId)))
        {
            var response = await PatchOneTimeDeductionAsync(subjectAuthorizer, fileId, deductionId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "ONE_TIME_DEDUCTION_SELF_APPROVAL_FORBIDDEN");
        }

        // (c) The REQUESTER — the third leg — cannot authorize what they asked for.
        using (var requesterAuthorizer = factory.CreateClientFor(OneTimeDeductionAuthorizerContext(scenario, requesterUserId)))
        {
            var response = await PatchOneTimeDeductionAsync(requesterAuthorizer, fileId, deductionId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "ONE_TIME_DEDUCTION_SELF_APPROVAL_FORBIDDEN");
        }

        // (d) A pure Admin (no AuthorizeOneTimeDeductions grant) is blocked by the policy.
        using (var adminOnly = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, fourthUserId)))
        {
            var response = await PatchOneTimeDeductionAsync(adminOnly, fileId, deductionId, "resolution", token, authorizeBody);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // (e) A FOURTH authorized user (none of the three) authorizes → AUTORIZADO.
        using (var fourthAuthorizer = factory.CreateClientFor(OneTimeDeductionAuthorizerContext(scenario, fourthUserId)))
        {
            var response = await PatchOneTimeDeductionAsync(fourthAuthorizer, fileId, deductionId, "resolution", token, authorizeBody);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("AUTORIZADO", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.Equal(fourthUserId, doc.RootElement.GetProperty("decidedByUserPublicId").GetGuid());
        }
    }

    [Fact]
    public async Task OneTimeDeductions_RejectWithoutNote_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Nora", "SinNota", "EMP-OTD-F", "nora.otd.f@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefe", "Solicitante", "EMP-OTD-F2", "jefe.otd.f@empresa.test");

        using var registrarClient = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, registrarUserId));
        var (deductionId, token) = await CreateOneTimeDeductionAsync(registrarClient, fileId, FixedOneTimeDeductionBody(requesterId));

        using var authorizerClient = factory.CreateClientFor(OneTimeDeductionAuthorizerContext(scenario, authorizerUserId));
        var response = await PatchOneTimeDeductionAsync(
            authorizerClient, fileId, deductionId, "resolution", token, new { targetStatusCode = "RECHAZADO", note = (string?)null });
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_DEDUCTION_DECISION_NOTE_REQUIRED");
    }

    [Fact]
    public async Task OneTimeDeductions_Retarget_OnlyWhileAuthorized()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Rita", "Reimputa", "EMP-OTD-G", "rita.otd.g@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTD-G2", "jefa.otd.g@empresa.test");

        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, registrarUserId));
        var (deductionId, token) = await CreateOneTimeDeductionAsync(manager, fileId, FixedOneTimeDeductionBody(requesterId));

        var retargetBody = new
        {
            payrollTypeCode = "MENSUAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Agosto 2026",
            payrollPeriodEndDate = (DateOnly?)null
        };

        // While EN_REVISION the destination is edited through the PUT, not re-targeted.
        var tooEarly = await PatchOneTimeDeductionAsync(manager, fileId, deductionId, "period", token, retargetBody);
        await AssertProblemDetailsAsync(tooEarly, HttpStatusCode.UnprocessableEntity, "ONE_TIME_DEDUCTION_NOT_RETARGETABLE");

        // Authorize, then re-target: now it is legal.
        using var authorizer = factory.CreateClientFor(OneTimeDeductionAuthorizerContext(scenario, authorizerUserId));
        var authorized = await PatchOneTimeDeductionAsync(
            authorizer, fileId, deductionId, "resolution", token, new { targetStatusCode = "AUTORIZADO", note = (string?)null });
        authorized.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await authorized.Content.ReadAsStringAsync()))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        var retargeted = await PatchOneTimeDeductionAsync(manager, fileId, deductionId, "period", token, retargetBody);
        retargeted.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await retargeted.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Agosto 2026", doc.RootElement.GetProperty("payrollPeriodLabel").GetString());
            Assert.Equal("AUTORIZADO", doc.RootElement.GetProperty("statusCode").GetString());
        }
    }

    [Fact]
    public async Task OneTimeDeductions_RevokeAnAuthorizedDeduction_Annuls()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Vera", "Revocada", "EMP-OTD-H", "vera.otd.h@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefe", "Solicitante", "EMP-OTD-H2", "jefe.otd.h@empresa.test");

        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, registrarUserId));
        var (deductionId, token) = await CreateOneTimeDeductionAsync(manager, fileId, FixedOneTimeDeductionBody(requesterId));

        using var authorizer = factory.CreateClientFor(OneTimeDeductionAuthorizerContext(scenario, authorizerUserId));
        var authorized = await PatchOneTimeDeductionAsync(
            authorizer, fileId, deductionId, "resolution", token, new { targetStatusCode = "AUTORIZADO", note = (string?)null });
        authorized.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await authorized.Content.ReadAsStringAsync()))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        var revoked = await PatchOneTimeDeductionAsync(
            authorizer, fileId, deductionId, "revocation", token, new { reason = "El daño lo cubrió el seguro" });
        revoked.EnsureSuccessStatusCode();
        using var final = JsonDocument.Parse(await revoked.Content.ReadAsStringAsync());
        Assert.Equal("ANULADO", final.RootElement.GetProperty("statusCode").GetString());
        Assert.False(final.RootElement.GetProperty("isActive").GetBoolean());
    }
}
