using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the recurring-deduction CHARGE slice (REQ-008 PR-4): the derived schedule (projection
/// + amortization + "total cobrado" / "total no cobrado"), the unitary application (sequence, future-dated credit
/// → 422, finalization on the last charge), the EXTRAORDINARY payment (it shortens the term, a payoff finalizes,
/// above the balance → 422, on a SUSPENDIDO credit → 422), the annulment (it frees the number and REOPENS a
/// finalized credit), the company-wide apply-period BATCH (exclusions postpone; the run is atomic) and the
/// CONCURRENCY RACE on the strict charge sequence (two simultaneous applications: exactly one wins).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private async Task<(Guid DeductionId, Guid Token)> CreateAndAuthorizeDeductionAsync(
        IntegrationTestScenario scenario,
        HttpClient manager,
        Guid fileId,
        object body)
    {
        var (deductionId, token) = await CreateRecurringDeductionAsync(manager, fileId, body);

        using var authorizer = factory.CreateClientFor(RecurringDeductionAuthorizerContext(scenario, Guid.NewGuid()));
        var response = await PatchRecurringDeductionAsync(
            authorizer, fileId, deductionId, "resolution", token, new { targetStatusCode = "VIGENTE", note = (string?)null });
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (deductionId, doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> ApplyChargeAsync(
        HttpClient client, Guid fileId, Guid deductionId, Guid token, object? body = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/personnel-files/{fileId}/recurring-deductions/{deductionId}/installments")
        {
            Content = JsonContent.Create(body ?? new { appliedDate = (DateOnly?)null, payrollPeriodPublicId = (Guid?)null, notes = (string?)null })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ApplyExtraordinaryAsync(
        HttpClient client, Guid fileId, Guid deductionId, Guid token, decimal amount)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/personnel-files/{fileId}/recurring-deductions/{deductionId}/extraordinary-installments")
        {
            Content = JsonContent.Create(new { amount, appliedDate = (DateOnly?)null, payrollPeriodPublicId = (Guid?)null, notes = (string?)null })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> GetDeductionScheduleAsync(HttpClient client, Guid fileId, Guid deductionId)
    {
        var response = await client.GetAsync(
            $"/api/v1/personnel-files/{fileId}/recurring-deductions/{deductionId}/schedule");
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task RecurringDeductionCharges_Schedule_DerivesTheProjectionAndTheTotals()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Sofía", "Schedule", "EMP-RDI-A", "sofia.rdi.a@empresa.test");

        var (deductionId, _) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, SegmentedRecurringDeductionBody());

        var schedule = await GetDeductionScheduleAsync(manager, fileId, deductionId);

        // 6×$50 + 6×$75 = 12 charges, $750; nothing charged yet.
        Assert.Equal(12, schedule.GetProperty("chargeCount").GetInt32());
        Assert.Equal(750m, schedule.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(0m, schedule.GetProperty("totalCharged").GetDecimal());
        Assert.Equal(750m, schedule.GetProperty("totalOutstanding").GetDecimal());
        Assert.Equal(12, schedule.GetProperty("installments").GetArrayLength());
        Assert.Equal(1, schedule.GetProperty("nextInstallmentNumber").GetInt32());
    }

    [Fact]
    public async Task RecurringDeductionCharges_ScheduleWithInterest_ExposesTheAmortizationSplit()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Amara", "Amortiza", "EMP-RDI-B", "amara.rdi.b@empresa.test");

        var (deductionId, _) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, InterestRecurringDeductionBody());

        var schedule = await GetDeductionScheduleAsync(manager, fileId, deductionId);
        var first = schedule.GetProperty("installments")[0];

        // The golden table: quota $88.85 = $10.00 interest + $78.85 capital.
        Assert.Equal(88.85m, first.GetProperty("amount").GetDecimal());
        Assert.Equal(10.00m, first.GetProperty("interestAmount").GetDecimal());
        Assert.Equal(78.85m, first.GetProperty("capitalAmount").GetDecimal());

        // The payoff (outstandingBalance = the CAPITAL owed) is the principal, and it is LESS than what the
        // employee would pay by finishing the plan (which carries the interest).
        Assert.Equal(1000m, schedule.GetProperty("outstandingBalance").GetDecimal());
        Assert.True(schedule.GetProperty("totalAmount").GetDecimal() > 1000m);
    }

    [Fact]
    public async Task RecurringDeductionCharges_ApplyInSequence_AndFinalizeOnTheLastCharge()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Pedro", "Paga", "EMP-RDI-C", "pedro.rdi.c@empresa.test");

        // A short plan: 2 charges of $50.
        var body = SegmentedRecurringDeductionBody(
            segments: [new { fromInstallment = 1, toInstallment = (int?)2, installmentValue = 50m }]);
        var (deductionId, token) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, body);

        // Charge 1 → still VIGENTE.
        var first = await ApplyChargeAsync(manager, fileId, deductionId, token);
        var firstPayload = await first.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == first.StatusCode, firstPayload);
        using (var doc = JsonDocument.Parse(firstPayload))
        {
            Assert.Equal("REGULAR", doc.RootElement.GetProperty("installment").GetProperty("kind").GetString());
            Assert.Equal(1, doc.RootElement.GetProperty("installment").GetProperty("installmentNumber").GetInt32());
            Assert.Equal(50m, doc.RootElement.GetProperty("installment").GetProperty("amount").GetDecimal());
            Assert.Equal("VIGENTE", doc.RootElement.GetProperty("recurringDeductionStatusCode").GetString());
            token = doc.RootElement.GetProperty("recurringDeductionConcurrencyToken").GetGuid();
        }

        // Charge 2 completes the plan → FINALIZADO in the same transaction.
        var second = await ApplyChargeAsync(manager, fileId, deductionId, token);
        second.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await second.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, doc.RootElement.GetProperty("installment").GetProperty("installmentNumber").GetInt32());
            Assert.Equal("FINALIZADO", doc.RootElement.GetProperty("recurringDeductionStatusCode").GetString());
            token = doc.RootElement.GetProperty("recurringDeductionConcurrencyToken").GetGuid();
        }

        // A third application has nothing left to charge.
        var third = await ApplyChargeAsync(manager, fileId, deductionId, token);
        await AssertProblemDetailsAsync(third, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_INSTALLMENT_PLAN_COMPLETE");

        var schedule = await GetDeductionScheduleAsync(manager, fileId, deductionId);
        Assert.Equal(100m, schedule.GetProperty("totalCharged").GetDecimal());
        Assert.Equal(0m, schedule.GetProperty("totalOutstanding").GetDecimal());
        Assert.True(schedule.GetProperty("isPlanComplete").GetBoolean());
    }

    [Fact]
    public async Task RecurringDeductionCharges_FutureDatedCredit_CannotBeChargedYet()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Futura", "Vigencia", "EMP-RDI-D", "futura.rdi.d@empresa.test");

        // The credit is registered and authorized today, but takes effect in two months (D-04).
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(2).ToString("yyyy-MM-dd");
        var body = new
        {
            effectiveDate = future,
            reference = "PREST-FUTURO",
            recurringDeductionTypeCode = "PRESTAMO_BANCARIO",
            conceptTypeCode = "PRESTAMO_BANCARIO",
            financialInstitution = "Banco",
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = future,
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

        var (deductionId, token) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, body);

        var response = await ApplyChargeAsync(manager, fileId, deductionId, token);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_INSTALLMENT_NOT_DUE_YET");
    }

    [Fact]
    public async Task RecurringDeductionCharges_ExtraordinaryPayment_ShortensTheTermAndAPayoffFinalizes()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Abel", "Abono", "EMP-RDI-E", "abel.rdi.e@empresa.test");

        // 4 charges of $50 = $200.
        var body = SegmentedRecurringDeductionBody(
            segments: [new { fromInstallment = 1, toInstallment = (int?)4, installmentValue = 50m }]);
        var (deductionId, token) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, body);

        // An abono of $120 leaves $80 owed.
        var abono = await ApplyExtraordinaryAsync(manager, fileId, deductionId, token, 120m);
        var abonoPayload = await abono.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == abono.StatusCode, abonoPayload);
        using (var doc = JsonDocument.Parse(abonoPayload))
        {
            var installment = doc.RootElement.GetProperty("installment");
            Assert.Equal("EXTRAORDINARIA", installment.GetProperty("kind").GetString());
            Assert.Equal(1, installment.GetProperty("extraordinaryNumber").GetInt32());
            // An abono is 100 % capital: it never pays future interest.
            Assert.Equal(120m, installment.GetProperty("capitalAmount").GetDecimal());
            Assert.Equal(0m, installment.GetProperty("interestAmount").GetDecimal());
            Assert.Equal("VIGENTE", doc.RootElement.GetProperty("recurringDeductionStatusCode").GetString());
            token = doc.RootElement.GetProperty("recurringDeductionConcurrencyToken").GetGuid();
        }

        var schedule = await GetDeductionScheduleAsync(manager, fileId, deductionId);
        Assert.Equal(80m, schedule.GetProperty("outstandingBalance").GetDecimal());

        // Overpaying the remaining balance is rejected.
        var overpay = await ApplyExtraordinaryAsync(manager, fileId, deductionId, token, 80.01m);
        await AssertProblemDetailsAsync(overpay, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_EXTRAORDINARY_EXCEEDS_BALANCE");

        // Paying exactly the balance is a PAYOFF → FINALIZADO, even though 4 charges were never applied.
        var payoff = await ApplyExtraordinaryAsync(manager, fileId, deductionId, token, 80m);
        payoff.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await payoff.Content.ReadAsStringAsync()))
        {
            Assert.Equal("FINALIZADO", doc.RootElement.GetProperty("recurringDeductionStatusCode").GetString());
        }
    }

    [Fact]
    public async Task RecurringDeductionCharges_ExtraordinaryOnASuspendedCredit_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Sergio", "Suspendido", "EMP-RDI-F", "sergio.rdi.f@empresa.test");

        var (deductionId, token) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, SegmentedRecurringDeductionBody());

        var suspend = await PatchRecurringDeductionAsync(
            manager, fileId, deductionId, "suspension", token, new { suspend = true, note = "Pausa" });
        suspend.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await suspend.Content.ReadAsStringAsync()))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        var response = await ApplyExtraordinaryAsync(manager, fileId, deductionId, token, 50m);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_EXTRAORDINARY_NOT_APPLICABLE");
    }

    [Fact]
    public async Task RecurringDeductionCharges_AnnullingACompletingCharge_ReopensTheCredit()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Ana", "Anula", "EMP-RDI-G", "ana.rdi.g@empresa.test");

        // A single-charge plan: applying it finalizes the credit.
        var body = SegmentedRecurringDeductionBody(
            segments: [new { fromInstallment = 1, toInstallment = (int?)1, installmentValue = 50m }]);
        var (deductionId, token) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, body);

        var apply = await ApplyChargeAsync(manager, fileId, deductionId, token);
        apply.EnsureSuccessStatusCode();
        Guid installmentId;
        using (var doc = JsonDocument.Parse(await apply.Content.ReadAsStringAsync()))
        {
            Assert.Equal("FINALIZADO", doc.RootElement.GetProperty("recurringDeductionStatusCode").GetString());
            installmentId = doc.RootElement.GetProperty("installment").GetProperty("installmentPublicId").GetGuid();
            token = doc.RootElement.GetProperty("recurringDeductionConcurrencyToken").GetGuid();
        }

        // Annulling it leaves the plan incomplete → the credit REOPENS to VIGENTE.
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/recurring-deductions/{deductionId}/installments/{installmentId}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Cobro duplicado" })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");

        var annul = await manager.SendAsync(request);
        var annulPayload = await annul.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == annul.StatusCode, annulPayload);
        using (var doc = JsonDocument.Parse(annulPayload))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("installment").GetProperty("statusCode").GetString());
            Assert.Equal("VIGENTE", doc.RootElement.GetProperty("recurringDeductionStatusCode").GetString());
        }

        // The number is free again: the schedule is back to zero charged.
        var schedule = await GetDeductionScheduleAsync(manager, fileId, deductionId);
        Assert.Equal(0m, schedule.GetProperty("totalCharged").GetDecimal());
        Assert.Equal(1, schedule.GetProperty("nextInstallmentNumber").GetInt32());
    }

    [Fact]
    public async Task RecurringDeductionCharges_ApplyPeriodBatch_AppliesDueChargesAndPostponesTheExcluded()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileA = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Lote", "Uno", "EMP-RDI-H", "lote.rdi.h@empresa.test");
        var fileB = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Lote", "Dos", "EMP-RDI-I", "lote.rdi.i@empresa.test");

        var body = SegmentedRecurringDeductionBody(
            segments: [new { fromInstallment = 1, toInstallment = (int?)3, installmentValue = 50m }]);
        var (deductionA, _) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileA, body);
        var (deductionB, _) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileB, body);

        // Cutoff = today: only charge 1 of each credit is due (the plan starts today, monthly).
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var response = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/recurring-deductions/apply-period",
            new
            {
                payrollTypeCode = "MENSUAL",
                payrollPeriodPublicId = (Guid?)null,
                cutoffDate = cutoff,
                excludedDeductionPublicIds = new[] { deductionB }
            });

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        // Credit A charged once; credit B was excluded (postponed), so nothing was applied to it.
        Assert.Equal(1, doc.RootElement.GetProperty("aplicadas").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("finalizados").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("pospuestas").GetInt32());

        var scheduleA = await GetDeductionScheduleAsync(manager, fileA, deductionA);
        Assert.Equal(50m, scheduleA.GetProperty("totalCharged").GetDecimal());

        var scheduleB = await GetDeductionScheduleAsync(manager, fileB, deductionB);
        Assert.Equal(0m, scheduleB.GetProperty("totalCharged").GetDecimal());
    }

    [Fact]
    public async Task RecurringDeductionCharges_ConcurrentApplications_OnlyOneWinsTheSequence()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Carrera", "Concurrente", "EMP-RDI-J", "carrera.rdi.j@empresa.test");

        var (deductionId, token) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, SegmentedRecurringDeductionBody());

        // Two clients race to apply "the next charge" with the SAME If-Match token. The advisory lock serializes
        // them and the optimistic token rejects the loser: exactly one 200, and the credit ends with ONE charge.
        using var clientA = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));
        using var clientB = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var first = ApplyChargeAsync(clientA, fileId, deductionId, token);
        var second = ApplyChargeAsync(clientB, fileId, deductionId, token);
        var responses = await Task.WhenAll(first, second);

        var succeeded = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        var conflicted = responses.Count(response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity);

        Assert.Equal(1, succeeded);
        Assert.Equal(1, conflicted);

        // The ledger holds exactly ONE charge — the sequence never double-applied.
        var schedule = await GetDeductionScheduleAsync(manager, fileId, deductionId);
        Assert.Equal(50m, schedule.GetProperty("totalCharged").GetDecimal());
        Assert.Equal(2, schedule.GetProperty("nextInstallmentNumber").GetInt32());

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}
