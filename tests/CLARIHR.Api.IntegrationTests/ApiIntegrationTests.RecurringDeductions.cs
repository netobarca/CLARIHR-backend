using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the recurring-deduction ("planilla — descuentos cíclicos", REQ-008 PR-3) CRUD +
/// resolution slice: the create round-trip (segment plan + compound-interest plan → EN_REVISION with the derived
/// count/total), the write guards (statutory concept → 422 RN-04, external concept without a financial institution
/// → 422 P-07, a gap in the segments → 422, DESCONTAR_SALDO × indefinite → 422, an application cadence slower than
/// the installment cadence → 422, retired profile → 422), the replace-all segment edit (EN_REVISION only), the
/// authorizer resolution with the DOUBLE anti-self (registrar → 403, subject → 403, Admin without the dedicated
/// grant → 403, a third authorized user → VIGENTE), and the VIGENTE lifecycle (suspend/resume, manual closure of
/// an indefinite credit, authorizer revocation). Employees are seeded directly (profile + primary assignment).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static readonly DateTime RecurringDeductionHireDate = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static TestUserContext RecurringDeductionManagerContext(IntegrationTestScenario scenario, Guid? userId = null) =>
        TestUserContext.Authenticated(userId ?? scenario.ActorUserId, scenario.TenantId, PersonnelFilePermissionCodes.Admin);

    private static TestUserContext RecurringDeductionAuthorizerContext(IntegrationTestScenario scenario, Guid userId) =>
        TestUserContext.Authenticated(userId, scenario.TenantId, PersonnelFilePermissionCodes.AuthorizeRecurringDeductions);

    private async Task<Guid> SeedRecurringDeductionCandidateAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail,
        Guid? linkedUserPublicId = null,
        bool retired = false)
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

        var status = retired ? PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode : "ACTIVO";
        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, status, RecurringDeductionHireDate);
        profile.BindToPersonnelFile(file.Id);
        profile.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmployeeProfile>().Add(profile);

        // A recurring deduction needs a plaza but NO cost center (P-08), unlike the recurring income.
        var assignment = PersonnelFileEmploymentAssignment.Create(
            "INDEFINIDO",
            contractTypeCode: null,
            workdayCode: null,
            payrollTypeCode: null,
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: null,
            startDate: RecurringDeductionHireDate,
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

    /// <summary>A plain (no-interest) credit: 6 × $50 then 6 × $75 → 12 installments, $750 total.</summary>
    private static object SegmentedRecurringDeductionBody(
        string settlementActionCode = "DESCONTAR_SALDO",
        string applicationFrequencyCode = "MENSUAL",
        object[]? segments = null,
        int[]? exceptionMonths = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            effectiveDate = today,
            reference = "PREST-BCO-2026-001",
            recurringDeductionTypeCode = "PRESTAMO_BANCARIO",
            conceptTypeCode = "PRESTAMO_BANCARIO",
            financialInstitution = "Banco Agrícola",
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = today,
            exceptionMonths,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            applicationFrequencyCode,
            isIndefinite = false,
            usesCompoundInterest = false,
            principalAmount = (decimal?)null,
            interestRatePercent = (decimal?)null,
            plannedInstallments = (int?)null,
            segments = segments ??
            [
                new { fromInstallment = 1, toInstallment = (int?)6, installmentValue = 50m },
                new { fromInstallment = 7, toInstallment = (int?)12, installmentValue = 75m },
            ],
            settlementActionCode
        };
    }

    /// <summary>A compound-interest credit: $1,000 at 12% nominal annual over 12 monthly installments (golden A.3).</summary>
    private static object InterestRecurringDeductionBody(string settlementActionCode = "DESCONTAR_SALDO")
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            effectiveDate = today,
            reference = "PREST-BCO-2026-002",
            recurringDeductionTypeCode = "PRESTAMO_BANCARIO",
            conceptTypeCode = "PRESTAMO_BANCARIO",
            financialInstitution = "Banco Cuscatlán",
            observations = "Crédito con interés compuesto",
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = today,
            exceptionMonths = (int[]?)null,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            applicationFrequencyCode = "MENSUAL",
            isIndefinite = false,
            usesCompoundInterest = true,
            principalAmount = (decimal?)1000m,
            interestRatePercent = (decimal?)12m,
            plannedInstallments = (int?)12,
            segments = (object[]?)null,
            settlementActionCode
        };
    }

    /// <summary>An open-ended credit: a single open segment; only CANCELAR is legal at settlement.</summary>
    private static object IndefiniteRecurringDeductionBody(string settlementActionCode = "CANCELAR")
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            effectiveDate = today,
            reference = "COOP-CUOTA",
            recurringDeductionTypeCode = "COOPERATIVA",
            conceptTypeCode = "COOPERATIVA",
            financialInstitution = "Cooperativa ACACYPAC",
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = today,
            exceptionMonths = (int[]?)null,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            applicationFrequencyCode = "MENSUAL",
            isIndefinite = true,
            usesCompoundInterest = false,
            principalAmount = (decimal?)null,
            interestRatePercent = (decimal?)null,
            plannedInstallments = (int?)null,
            segments = new object[] { new { fromInstallment = 1, toInstallment = (int?)null, installmentValue = 25m } },
            settlementActionCode
        };
    }

    private static async Task<(Guid DeductionId, Guid Token)> CreateRecurringDeductionAsync(
        HttpClient client, Guid fileId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/recurring-deductions", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return (
            doc.RootElement.GetProperty("recurringDeductionPublicId").GetGuid(),
            doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> PatchRecurringDeductionAsync(
        HttpClient client, Guid fileId, Guid deductionId, string action, Guid token, object body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/recurring-deductions/{deductionId}/{action}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task RecurringDeductions_CreateSegmented_DerivesCountAndTotal()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringDeductionManagerContext(scenario));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Dora", "Descuento", "EMP-RD-A", "dora.rd.a@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/recurring-deductions", SegmentedRecurringDeductionBody());
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.Equal("EN_REVISION", root.GetProperty("statusCode").GetString());
        Assert.Equal("Prestamo bancario", root.GetProperty("conceptNameSnapshot").GetString());

        // The plan is DERIVED from the segments: 6×$50 + 6×$75 = 12 installments, $750.
        Assert.Equal(12, root.GetProperty("installmentCount").GetInt32());
        Assert.Equal(750m, root.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(2, root.GetProperty("segments").GetArrayLength());
        Assert.False(root.GetProperty("usesCompoundInterest").GetBoolean());
    }

    [Fact]
    public async Task RecurringDeductions_CreateWithCompoundInterest_DerivesThePlanAndCarriesNoSegments()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringDeductionManagerContext(scenario));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Iker", "Interés", "EMP-RD-B", "iker.rd.b@empresa.test");

        var (_, _) = await CreateRecurringDeductionAsync(client, fileId, InterestRecurringDeductionBody());

        var listResponse = await client.GetAsync($"/api/v1/personnel-files/{fileId}/recurring-deductions");
        listResponse.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var item = doc.RootElement.EnumerateArray().Single();

        Assert.True(item.GetProperty("usesCompoundInterest").GetBoolean());
        Assert.Equal(1000m, item.GetProperty("principalAmount").GetDecimal());
        Assert.Equal(12, item.GetProperty("installmentCount").GetInt32());
        Assert.Empty(item.GetProperty("segments").EnumerateArray());

        // The employee pays back MORE than the principal: the derived total carries the interest.
        Assert.True(item.GetProperty("totalAmount").GetDecimal() > 1000m);
    }

    [Fact]
    public async Task RecurringDeductions_CreateWithStatutoryConcept_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringDeductionManagerContext(scenario));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Elsa", "Estatutaria", "EMP-RD-C", "elsa.rd.c@empresa.test");

        // ISSS is payroll law, not a credit (RN-04) — it can never back a recurring deduction.
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var body = new
        {
            effectiveDate = today,
            reference = "ISSS-INTENTO",
            recurringDeductionTypeCode = "PRESTAMO_BANCARIO",
            conceptTypeCode = "ISSS",
            financialInstitution = "Banco",
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = today,
            exceptionMonths = (int[]?)null,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            applicationFrequencyCode = "MENSUAL",
            isIndefinite = false,
            usesCompoundInterest = false,
            principalAmount = (decimal?)null,
            interestRatePercent = (decimal?)null,
            plannedInstallments = (int?)null,
            segments = new object[] { new { fromInstallment = 1, toInstallment = (int?)6, installmentValue = 50m } },
            settlementActionCode = "DESCONTAR_SALDO"
        };

        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/recurring-deductions", body);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_CONCEPT_INVALID");
    }

    [Fact]
    public async Task RecurringDeductions_CreateExternalConceptWithoutInstitution_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringDeductionManagerContext(scenario));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Fabio", "SinBanco", "EMP-RD-D", "fabio.rd.d@empresa.test");

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var body = new
        {
            effectiveDate = today,
            reference = "PREST-SIN-BANCO",
            recurringDeductionTypeCode = "PRESTAMO_BANCARIO",
            conceptTypeCode = "PRESTAMO_BANCARIO",
            financialInstitution = (string?)null,
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = today,
            exceptionMonths = (int[]?)null,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            applicationFrequencyCode = "MENSUAL",
            isIndefinite = false,
            usesCompoundInterest = false,
            principalAmount = (decimal?)null,
            interestRatePercent = (decimal?)null,
            plannedInstallments = (int?)null,
            segments = new object[] { new { fromInstallment = 1, toInstallment = (int?)6, installmentValue = 50m } },
            settlementActionCode = "DESCONTAR_SALDO"
        };

        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/recurring-deductions", body);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_FINANCIAL_INSTITUTION_REQUIRED");
    }

    [Fact]
    public async Task RecurringDeductions_CreateWithSegmentGap_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringDeductionManagerContext(scenario));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Hugo", "Hueco", "EMP-RD-E", "hugo.rd.e@empresa.test");

        // Installment 7 is missing: 1–6 then 8–12 (a gap → the plan is not contiguous).
        var segments = new object[]
        {
            new { fromInstallment = 1, toInstallment = (int?)6, installmentValue = 50m },
            new { fromInstallment = 8, toInstallment = (int?)12, installmentValue = 75m },
        };

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/recurring-deductions", SegmentedRecurringDeductionBody(segments: segments));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_SEGMENTS_NOT_CONTIGUOUS");
    }

    [Fact]
    public async Task RecurringDeductions_CreateDescontarSaldoIndefinite_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringDeductionManagerContext(scenario));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Iris", "Indefinida", "EMP-RD-F", "iris.rd.f@empresa.test");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/recurring-deductions",
            IndefiniteRecurringDeductionBody(settlementActionCode: "DESCONTAR_SALDO"));
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_SETTLEMENT_ACTION_INDEFINITE");
    }

    [Fact]
    public async Task RecurringDeductions_CreateWithSlowerApplicationFrequency_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringDeductionManagerContext(scenario));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Lento", "Aplicación", "EMP-RD-G", "lento.rd.g@empresa.test");

        // A QUINCENAL quota cannot be applied MENSUAL (the application cadence must divide the quota's).
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var body = new
        {
            effectiveDate = today,
            reference = "PREST-FREQ",
            recurringDeductionTypeCode = "PRESTAMO_BANCARIO",
            conceptTypeCode = "PRESTAMO_BANCARIO",
            financialInstitution = "Banco",
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = today,
            exceptionMonths = (int[]?)null,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "QUINCENAL",
            applicationFrequencyCode = "MENSUAL",
            isIndefinite = false,
            usesCompoundInterest = false,
            principalAmount = (decimal?)null,
            interestRatePercent = (decimal?)null,
            plannedInstallments = (int?)null,
            segments = new object[] { new { fromInstallment = 1, toInstallment = (int?)6, installmentValue = 50m } },
            settlementActionCode = "DESCONTAR_SALDO"
        };

        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{fileId}/recurring-deductions", body);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_APPLICATION_FREQUENCY_INVALID");
    }

    [Fact]
    public async Task RecurringDeductions_CreateOnRetiredProfile_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringDeductionManagerContext(scenario));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Raúl", "Retirado", "EMP-RD-H", "raul.rd.h@empresa.test", retired: true);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/recurring-deductions", SegmentedRecurringDeductionBody());
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "EMPLOYEE_PROFILE_RETIRED_LOCKED");
    }

    [Fact]
    public async Task RecurringDeductions_UpdateReplacesTheSegmentsWholesale()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(RecurringDeductionManagerContext(scenario));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Elena", "Edita", "EMP-RD-I", "elena.rd.i@empresa.test");

        var (deductionId, token) = await CreateRecurringDeductionAsync(client, fileId, SegmentedRecurringDeductionBody());

        // Re-plan to a single 10 × $40 segment ($400 over 10 installments).
        var newSegments = new object[] { new { fromInstallment = 1, toInstallment = (int?)10, installmentValue = 40m } };
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/personnel-files/{fileId}/recurring-deductions/{deductionId}")
        {
            Content = JsonContent.Create(SegmentedRecurringDeductionBody(segments: newSegments))
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("segments").GetArrayLength());
        Assert.Equal(10, root.GetProperty("installmentCount").GetInt32());
        Assert.Equal(400m, root.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task RecurringDeductions_Resolution_EnforcesDoubleAntiSelf_AndAuthorizesThirdParty()
    {
        var scenario = await factory.ResetDatabaseAsync();

        var registrarUserId = Guid.NewGuid();
        var subjectUserId = Guid.NewGuid();
        var thirdUserId = Guid.NewGuid();

        // Subject employee linked to subjectUserId; a DIFFERENT user registers the credit.
        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Sonia", "Sujeta", "EMP-RD-J", "sonia.rd.j@empresa.test", linkedUserPublicId: subjectUserId);

        using var registrarClient = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, registrarUserId));
        var (deductionId, token) = await CreateRecurringDeductionAsync(registrarClient, fileId, SegmentedRecurringDeductionBody());

        var authorizeBody = new { targetStatusCode = "VIGENTE", note = (string?)null };

        // (a) The REGISTRAR (with the Authorize grant) cannot authorize their own registration → 403.
        using (var registrarAuthorizer = factory.CreateClientFor(RecurringDeductionAuthorizerContext(scenario, registrarUserId)))
        {
            var response = await PatchRecurringDeductionAsync(registrarAuthorizer, fileId, deductionId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RECURRING_DEDUCTION_SELF_APPROVAL_FORBIDDEN");
        }

        // (b) The SUBJECT (with the Authorize grant) cannot authorize their own credit → 403.
        using (var subjectAuthorizer = factory.CreateClientFor(RecurringDeductionAuthorizerContext(scenario, subjectUserId)))
        {
            var response = await PatchRecurringDeductionAsync(subjectAuthorizer, fileId, deductionId, "resolution", token, authorizeBody);
            await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RECURRING_DEDUCTION_SELF_APPROVAL_FORBIDDEN");
        }

        // (c) A pure Admin (no AuthorizeRecurringDeductions grant) is blocked by the policy → 403.
        using (var adminOnly = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, thirdUserId)))
        {
            var response = await PatchRecurringDeductionAsync(adminOnly, fileId, deductionId, "resolution", token, authorizeBody);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // (d) A third authorized user (neither subject nor registrar) authorizes → VIGENTE.
        using (var thirdAuthorizer = factory.CreateClientFor(RecurringDeductionAuthorizerContext(scenario, thirdUserId)))
        {
            var response = await PatchRecurringDeductionAsync(thirdAuthorizer, fileId, deductionId, "resolution", token, authorizeBody);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("VIGENTE", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.Equal(thirdUserId, doc.RootElement.GetProperty("decidedByUserPublicId").GetGuid());
        }
    }

    [Fact]
    public async Task RecurringDeductions_RejectWithoutNote_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Nadia", "SinNota", "EMP-RD-K", "nadia.rd.k@empresa.test");

        using var registrarClient = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, registrarUserId));
        var (deductionId, token) = await CreateRecurringDeductionAsync(registrarClient, fileId, SegmentedRecurringDeductionBody());

        using var authorizerClient = factory.CreateClientFor(RecurringDeductionAuthorizerContext(scenario, authorizerUserId));
        var response = await PatchRecurringDeductionAsync(
            authorizerClient, fileId, deductionId, "resolution", token, new { targetStatusCode = "RECHAZADO", note = (string?)null });
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_DECISION_NOTE_REQUIRED");
    }

    [Fact]
    public async Task RecurringDeductions_SuspendResumeAndRevoke_FullLifecycle()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Ciclo", "Completo", "EMP-RD-L", "ciclo.rd.l@empresa.test");

        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, registrarUserId));
        var (deductionId, token) = await CreateRecurringDeductionAsync(manager, fileId, SegmentedRecurringDeductionBody());

        // Authorize (third party).
        using var authorizer = factory.CreateClientFor(RecurringDeductionAuthorizerContext(scenario, authorizerUserId));
        var authorizeResponse = await PatchRecurringDeductionAsync(
            authorizer, fileId, deductionId, "resolution", token, new { targetStatusCode = "VIGENTE", note = (string?)null });
        authorizeResponse.EnsureSuccessStatusCode();
        token = JsonDocument.Parse(await authorizeResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("concurrencyToken").GetGuid();

        // Suspend → SUSPENDIDO.
        var suspendResponse = await PatchRecurringDeductionAsync(
            manager, fileId, deductionId, "suspension", token, new { suspend = true, note = "Empleado de permiso" });
        suspendResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await suspendResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("SUSPENDIDO", doc.RootElement.GetProperty("statusCode").GetString());
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        // Resume → VIGENTE.
        var resumeResponse = await PatchRecurringDeductionAsync(
            manager, fileId, deductionId, "suspension", token, new { suspend = false, note = (string?)null });
        resumeResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await resumeResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("VIGENTE", doc.RootElement.GetProperty("statusCode").GetString());
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        // Authorizer revocation → ANULADO.
        var revokeResponse = await PatchRecurringDeductionAsync(
            authorizer, fileId, deductionId, "revocation", token, new { reason = "Crédito cancelado por el banco" });
        revokeResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await revokeResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("ANULADO", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.False(doc.RootElement.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task RecurringDeductions_CloseIndefinite_ReturnsFinalizado()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var registrarUserId = Guid.NewGuid();
        var authorizerUserId = Guid.NewGuid();

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Corina", "Cierre", "EMP-RD-M", "corina.rd.m@empresa.test");

        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, registrarUserId));
        var (deductionId, token) = await CreateRecurringDeductionAsync(manager, fileId, IndefiniteRecurringDeductionBody());

        using var authorizer = factory.CreateClientFor(RecurringDeductionAuthorizerContext(scenario, authorizerUserId));
        var authorizeResponse = await PatchRecurringDeductionAsync(
            authorizer, fileId, deductionId, "resolution", token, new { targetStatusCode = "VIGENTE", note = (string?)null });
        authorizeResponse.EnsureSuccessStatusCode();
        token = JsonDocument.Parse(await authorizeResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("concurrencyToken").GetGuid();

        var closeResponse = await PatchRecurringDeductionAsync(
            manager, fileId, deductionId, "closure", token, new { reason = "El empleado dejó la cooperativa" });
        closeResponse.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await closeResponse.Content.ReadAsStringAsync());
        Assert.Equal("FINALIZADO", doc.RootElement.GetProperty("statusCode").GetString());
    }
}
