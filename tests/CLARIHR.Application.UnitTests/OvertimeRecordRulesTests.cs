using System.Reflection;
using System.Xml.Linq;
using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The overtime-record duration + lifecycle critical golden suite (PR-2, the REQ-007 gate) — the RATIFIED Anexo
/// A.4 rule/domain cases encoded as blocking assertions (the e2e cases are PR-3…PR-6). The rules module is 100%
/// pure so these fully pin the h:m → decimal-hours derivation, the factor coherence (override note), the daily
/// cap, the state machine, the application rule (elapsed work date), the overdue derivation and the settlement
/// valuation (factored hours × hourly rate = $10.94); the domain guards pin the custodied mutators; a
/// localization-parity assertion pins the bilingual error catalog. Reference country: El Salvador.
/// </summary>
public sealed class OvertimeRecordRulesTests
{
    private static readonly DateOnly Today = new(2026, 7, 9);

    // ── A.4: duration derivation (2 h 30 m = 2.50 / 0 h 45 m = 0.75) ────────────────────────────────

    [Fact]
    public void DeriveDecimalHours_TwoHoursThirty_IsTwoPointFive()
    {
        var result = OvertimeRecordRules.DeriveDecimalHours(2, 30);

        Assert.True(result.IsValid);
        Assert.Equal(2.50m, result.DecimalHours);
    }

    [Fact]
    public void DeriveDecimalHours_FortyFiveMinutes_IsZeroPointSevenFive()
    {
        var result = OvertimeRecordRules.DeriveDecimalHours(0, 45);

        Assert.True(result.IsValid);
        Assert.Equal(0.75m, result.DecimalHours);
    }

    [Fact]
    public void DeriveDecimalHours_MinutesOutOfRange_Fails()
    {
        var result = OvertimeRecordRules.DeriveDecimalHours(1, 65);

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeRecordRules.DurationMinutesInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void DeriveDecimalHours_ZeroTotal_Fails()
    {
        var result = OvertimeRecordRules.DeriveDecimalHours(0, 0);

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeRecordRules.DurationEmptyCode, result.ErrorCode);
    }

    [Fact]
    public void DeriveDecimalHours_NegativeHours_Fails()
    {
        var result = OvertimeRecordRules.DeriveDecimalHours(-1, 30);

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeRecordRules.DurationHoursInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void DeriveDecimalHours_RoundsHalfUpAwayFromZero()
    {
        // 0 h 20 m = 0.3333… → 0.33 (half-up away-from-zero, the single rounding point).
        var result = OvertimeRecordRules.DeriveDecimalHours(0, 20);

        Assert.True(result.IsValid);
        Assert.Equal(0.33m, result.DecimalHours);
    }

    // ── ValidateFactor: positivity + override note (P-06) ───────────────────────────────────────────

    [Fact]
    public void ValidateFactor_SameAsType_Ok_WithoutNote()
    {
        var result = OvertimeRecordRules.ValidateFactor(factorApplied: 2.00m, typeFactorSnapshot: 2.00m, note: null);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateFactor_DiffersWithNote_Ok()
    {
        var result = OvertimeRecordRules.ValidateFactor(factorApplied: 2.50m, typeFactorSnapshot: 2.00m, note: "Ajuste autorizado");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateFactor_DiffersWithoutNote_Fails()
    {
        var result = OvertimeRecordRules.ValidateFactor(factorApplied: 2.50m, typeFactorSnapshot: 2.00m, note: "   ");

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeRecordRules.FactorNoteRequiredCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateFactor_NonPositive_Fails()
    {
        var result = OvertimeRecordRules.ValidateFactor(factorApplied: 0m, typeFactorSnapshot: 2.00m, note: null);

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeRecordRules.FactorInvalidCode, result.ErrorCode);
    }

    // ── ValidateDailyCap (P-05): 240 min cap, +250 → error ──────────────────────────────────────────

    [Fact]
    public void ValidateDailyCap_OverCap_IsExceeded_WithLimitAndTotal()
    {
        var result = OvertimeRecordRules.ValidateDailyCap(existingActiveMinutes: 0, newMinutes: 250, capMinutes: 240);

        Assert.True(result.IsExceeded);
        Assert.Equal(240, result.CapMinutes);
        Assert.Equal(250, result.TotalMinutes);
    }

    [Fact]
    public void ValidateDailyCap_WithinCap_IsAllowed()
    {
        var result = OvertimeRecordRules.ValidateDailyCap(existingActiveMinutes: 120, newMinutes: 100, capMinutes: 240);

        Assert.True(result.IsWithinCap);
        Assert.Equal(220, result.TotalMinutes);
    }

    [Fact]
    public void ValidateDailyCap_NullCap_IsAllowed()
    {
        var result = OvertimeRecordRules.ValidateDailyCap(existingActiveMinutes: 600, newMinutes: 600, capMinutes: null);

        Assert.True(result.IsWithinCap);
    }

    // ── State machine (RN-01/RN-02) ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OvertimeRecordStatuses.EnRevision, OvertimeRecordStatuses.Autorizada, true)]
    [InlineData(OvertimeRecordStatuses.EnRevision, OvertimeRecordStatuses.Rechazada, true)]
    [InlineData(OvertimeRecordStatuses.EnRevision, OvertimeRecordStatuses.Anulada, true)]
    [InlineData(OvertimeRecordStatuses.Autorizada, OvertimeRecordStatuses.Aplicada, true)]
    [InlineData(OvertimeRecordStatuses.Autorizada, OvertimeRecordStatuses.Anulada, true)]
    [InlineData(OvertimeRecordStatuses.Aplicada, OvertimeRecordStatuses.Autorizada, true)]
    [InlineData(OvertimeRecordStatuses.EnRevision, OvertimeRecordStatuses.Aplicada, false)]
    [InlineData(OvertimeRecordStatuses.Autorizada, OvertimeRecordStatuses.Rechazada, false)]
    [InlineData(OvertimeRecordStatuses.Aplicada, OvertimeRecordStatuses.Anulada, false)]
    [InlineData(OvertimeRecordStatuses.Rechazada, OvertimeRecordStatuses.Autorizada, false)]
    [InlineData(OvertimeRecordStatuses.Anulada, OvertimeRecordStatuses.Autorizada, false)]
    [InlineData(OvertimeRecordStatuses.Autorizada, OvertimeRecordStatuses.Autorizada, false)]
    public void CanTransition_EnforcesStateMachine(string from, string to, bool expected)
    {
        Assert.Equal(expected, OvertimeRecordRules.CanTransition(from, to));
    }

    // ── CanApply / CanRetarget / CanRevertApplication (elapsed work date, №13) ──────────────────────

    [Fact]
    public void CanApply_AutorizadaElapsedWithoutActiveApplication_Ok()
    {
        var result = OvertimeRecordRules.CanApply(
            OvertimeRecordStatuses.Autorizada, hasActiveApplication: false, workDate: Today.AddDays(-1), today: Today);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CanApply_FutureWorkDate_IsNotApplicable()
    {
        var result = OvertimeRecordRules.CanApply(
            OvertimeRecordStatuses.Autorizada, hasActiveApplication: false, workDate: Today.AddDays(1), today: Today);

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeRecordRules.WorkDateNotElapsedCode, result.ErrorCode);
    }

    [Fact]
    public void CanApply_WithActiveApplication_Fails()
    {
        var result = OvertimeRecordRules.CanApply(
            OvertimeRecordStatuses.Autorizada, hasActiveApplication: true, workDate: Today, today: Today);

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeRecordRules.AlreadyAppliedCode, result.ErrorCode);
    }

    [Fact]
    public void CanApply_NotAutorizada_Fails()
    {
        var result = OvertimeRecordRules.CanApply(
            OvertimeRecordStatuses.EnRevision, hasActiveApplication: false, workDate: Today, today: Today);

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeRecordRules.NotApplicableCode, result.ErrorCode);
    }

    [Fact]
    public void CanRetarget_OnlyAutorizada()
    {
        Assert.True(OvertimeRecordRules.CanRetarget(OvertimeRecordStatuses.Autorizada).IsValid);

        var blocked = OvertimeRecordRules.CanRetarget(OvertimeRecordStatuses.EnRevision);
        Assert.False(blocked.IsValid);
        Assert.Equal(OvertimeRecordRules.NotRetargetableCode, blocked.ErrorCode);
    }

    [Fact]
    public void CanRevertApplication_OnlyAplicada()
    {
        Assert.True(OvertimeRecordRules.CanRevertApplication(OvertimeRecordStatuses.Aplicada).IsValid);

        var blocked = OvertimeRecordRules.CanRevertApplication(OvertimeRecordStatuses.Autorizada);
        Assert.False(blocked.IsValid);
        Assert.Equal(OvertimeRecordRules.ApplicationNotRevertibleCode, blocked.ErrorCode);
    }

    // ── ValidateWorkDate (future permitted; sanity cap) ─────────────────────────────────────────────

    [Fact]
    public void ValidateWorkDate_FutureWithinCap_Ok()
    {
        Assert.True(OvertimeRecordRules.ValidateWorkDate(Today.AddDays(30), Today, sanityCapDays: 366).IsValid);
        Assert.True(OvertimeRecordRules.ValidateWorkDate(Today.AddDays(-500), Today, sanityCapDays: 366).IsValid);
    }

    [Fact]
    public void ValidateWorkDate_BeyondSanityCap_Fails()
    {
        var result = OvertimeRecordRules.ValidateWorkDate(Today.AddDays(400), Today, sanityCapDays: 366);

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeRecordRules.WorkDateTooFarCode, result.ErrorCode);
    }

    // ── IsOverdue (with and without an end date) ───────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_NullEndDate_IsNeverOverdue()
    {
        Assert.False(OvertimeRecordRules.IsOverdue(null, Today));
    }

    [Fact]
    public void IsOverdue_PastEndDate_IsOverdue()
    {
        Assert.True(OvertimeRecordRules.IsOverdue(Today.AddDays(-1), Today));
    }

    [Fact]
    public void IsOverdue_TodayOrFutureEndDate_IsNotOverdue()
    {
        Assert.False(OvertimeRecordRules.IsOverdue(Today, Today));
        Assert.False(OvertimeRecordRules.IsOverdue(Today.AddDays(1), Today));
    }

    // ── A.4-16: settlement helper = factored hours × hourly rate = $10.94 ──────────────────────────

    [Fact]
    public void SettlementHelpers_TwoPendingRecords_ComputeTenNinetyFour()
    {
        // 2.50 h × 2.00 + 1.50 h × 2.50 = 5.00 + 3.75 = 8.75 factored hours.
        var factored = OvertimeRecordRules.FactoredHours(new[]
        {
            new OvertimeFactoredRecord(2.50m, 2.00m),
            new OvertimeFactoredRecord(1.50m, 2.50m),
        });
        Assert.Equal(8.75m, factored);

        // dailySalary $10.00, 8 h/day → hourly rate $1.25 (reuses CompensatoryTimeRules.HourlyRate).
        var hourlyRate = OvertimeRecordRules.HourlyRate(dailySalary: 10.00m, standardDailyHours: 8m);
        Assert.Equal(1.25m, hourlyRate);
        Assert.Equal(CompensatoryTimeRules.HourlyRate(10.00m, 8m), hourlyRate);

        // 8.75 × 1.25 = 10.9375 → 10.94 (half-up away-from-zero).
        var amount = OvertimeRecordRules.SettlementAmount(factored, hourlyRate);
        Assert.Equal(10.94m, amount);
    }

    [Fact]
    public void FactoredHours_Empty_IsZero()
    {
        Assert.Equal(0m, OvertimeRecordRules.FactoredHours([]));
    }

    // ── Domain guards: custodied mutators ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_DerivesDecimalHours_AndStartsEnRevision()
    {
        var record = Build(durationHours: 2, durationMinutes: 30);

        Assert.Equal(2.50m, record.DurationDecimalHours);
        Assert.Equal(OvertimeRecordStatuses.EnRevision, record.StatusCode);
        Assert.Equal(OvertimeRecordChannels.Rrhh, record.OriginChannel);
    }

    [Fact]
    public void Create_ZeroDuration_Throws()
    {
        Assert.Throws<ArgumentException>(() => Build(durationHours: 0, durationMinutes: 0));
    }

    [Fact]
    public void Create_MinutesOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(durationHours: 1, durationMinutes: 65));
    }

    [Fact]
    public void Create_FactorDiffersWithoutNote_Throws()
    {
        Assert.Throws<ArgumentException>(() => Build(typeFactor: 2.00m, factorApplied: 2.50m, factorOverrideNote: null));
    }

    [Fact]
    public void Create_FactorDiffersWithNote_Succeeds()
    {
        var record = Build(typeFactor: 2.00m, factorApplied: 2.50m, factorOverrideNote: "Ajuste");

        Assert.Equal(2.50m, record.FactorApplied);
        Assert.Equal("Ajuste", record.FactorOverrideNote);
    }

    [Fact]
    public void Update_OnlyAllowedWhileEnRevision()
    {
        var record = Build();
        record.Approve(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => record.Update(
            Today, Guid.NewGuid(), "HED", "Hora extra diurna", 2.00m, 2.00m, null,
            durationHours: 1, durationMinutes: 0, startTime: null, endTime: null,
            justificationTypePublicId: Guid.NewGuid(), justificationCodeSnapshot: "OPERATIVA", justificationNameSnapshot: "Carga operativa",
            observations: null,
            assignedPositionPublicId: Guid.NewGuid(),
            requesterFilePublicId: Guid.NewGuid(), requesterNameSnapshot: "Jefe",
            payrollTypeCode: "MENSUAL",
            payrollPeriodId: null, payrollPeriodPublicId: null, payrollPeriodLabel: "Julio 2026", payrollPeriodEndDate: null));
    }

    [Fact]
    public void Approve_MovesToAutorizada()
    {
        var record = Build();
        var decidedBy = Guid.NewGuid();

        record.Approve(decidedBy, DateTime.UtcNow);

        Assert.Equal(OvertimeRecordStatuses.Autorizada, record.StatusCode);
        Assert.Equal(decidedBy, record.DecidedByUserId);
    }

    [Fact]
    public void Reject_RequiresNote_AndIsTerminal()
    {
        var record = Build();

        Assert.Throws<ArgumentException>(() => record.Reject(Guid.NewGuid(), DateTime.UtcNow, "   "));

        record.Reject(Guid.NewGuid(), DateTime.UtcNow, "Sin respaldo");
        Assert.Equal(OvertimeRecordStatuses.Rechazada, record.StatusCode);
        Assert.False(record.IsActive);
    }

    [Fact]
    public void Annul_FromEnRevisionOrAutorizada_RequiresReason()
    {
        var fromReview = Build();
        Assert.Throws<ArgumentException>(() => fromReview.Annul("  ", Guid.NewGuid(), DateTime.UtcNow));
        fromReview.Annul("Duplicado", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(OvertimeRecordStatuses.Anulada, fromReview.StatusCode);
        Assert.False(fromReview.IsActive);

        var fromAutorizada = Build();
        fromAutorizada.Approve(Guid.NewGuid(), DateTime.UtcNow);
        fromAutorizada.Annul("Revocado", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(OvertimeRecordStatuses.Anulada, fromAutorizada.StatusCode);
    }

    [Fact]
    public void Apply_FutureWorkDate_Throws()
    {
        var record = Build(workDate: Today.AddDays(1));
        record.Approve(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => Apply(record));
    }

    [Fact]
    public void Apply_OnlyOnAutorizada_MovesToAplicada_AndCreatesApplication()
    {
        var record = Build(workDate: Today.AddDays(-1));
        Assert.Throws<InvalidOperationException>(() => Apply(record)); // EN_REVISION

        record.Approve(Guid.NewGuid(), DateTime.UtcNow);
        var application = Apply(record);

        Assert.Equal(OvertimeRecordStatuses.Aplicada, record.StatusCode);
        Assert.True(record.HasActiveApplication);
        Assert.Equal(OvertimeApplicationStatuses.Aplicada, application.StatusCode);
    }

    [Fact]
    public void Apply_WhenAlreadyApplied_Throws()
    {
        var record = Build(workDate: Today.AddDays(-1));
        record.Approve(Guid.NewGuid(), DateTime.UtcNow);
        Apply(record);

        Assert.Throws<InvalidOperationException>(() => Apply(record));
    }

    [Fact]
    public void AnnulApplication_ReopensToAutorizada_AndAllowsReapply()
    {
        var record = Build(workDate: Today.AddDays(-1));
        record.Approve(Guid.NewGuid(), DateTime.UtcNow);
        var application = Apply(record);

        Assert.Throws<ArgumentException>(() => record.AnnulApplication(application.PublicId, "  ", Guid.NewGuid(), DateTime.UtcNow));

        record.AnnulApplication(application.PublicId, "Corrección", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(OvertimeRecordStatuses.Autorizada, record.StatusCode);
        Assert.False(record.HasActiveApplication);

        var reapplied = Apply(record);
        Assert.Equal(OvertimeRecordStatuses.Aplicada, record.StatusCode);
        Assert.NotEqual(application.PublicId, reapplied.PublicId);
    }

    [Fact]
    public void MarkAppliedBySettlement_ThenReopenBySameSettlement_IsSymmetric()
    {
        var settlement = Guid.NewGuid();
        var record = Build();
        record.Approve(Guid.NewGuid(), DateTime.UtcNow);

        record.MarkAppliedBySettlement(settlement, DateTime.UtcNow);
        Assert.Equal(OvertimeRecordStatuses.Aplicada, record.StatusCode);

        // A different settlement's reopen is a no-op.
        record.ReopenFromSettlement(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(OvertimeRecordStatuses.Aplicada, record.StatusCode);

        record.ReopenFromSettlement(settlement, DateTime.UtcNow);
        Assert.Equal(OvertimeRecordStatuses.Autorizada, record.StatusCode);
    }

    [Fact]
    public void Annul_WithSettlement_ThenReopen_ReopensFutureAnnulled()
    {
        var settlement = Guid.NewGuid();
        var record = Build(workDate: Today.AddDays(10));
        record.Approve(Guid.NewGuid(), DateTime.UtcNow);

        record.Annul("Retiro", Guid.NewGuid(), DateTime.UtcNow, settlement);
        Assert.Equal(OvertimeRecordStatuses.Anulada, record.StatusCode);
        Assert.False(record.IsActive);

        record.ReopenFromSettlement(settlement, DateTime.UtcNow);
        Assert.Equal(OvertimeRecordStatuses.Autorizada, record.StatusCode);
        Assert.True(record.IsActive);
        Assert.Null(record.AnnulledBySettlementPublicId);
    }

    [Fact]
    public void MarkCompensated_ThenClear_IsSymmetric()
    {
        var credit = Guid.NewGuid();
        var record = Build();

        record.MarkCompensated(credit);
        Assert.Equal(credit, record.CompensatedByCreditPublicId);

        // A different credit's clear is a no-op.
        record.ClearCompensation(Guid.NewGuid());
        Assert.Equal(credit, record.CompensatedByCreditPublicId);

        record.ClearCompensation(credit);
        Assert.Null(record.CompensatedByCreditPublicId);
    }

    // ── Localization parity: every new rule error code is bilingual (EN + ES) ──────────────────────

    [Fact]
    public void RuleErrorCodes_ArePresentInBothResourceCatalogs()
    {
        var codes = typeof(OvertimeRecordRules)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string)
                && field.IsLiteral
                && field.Name.EndsWith("Code", StringComparison.Ordinal))
            .Select(field => (string)field.GetValue(null)!)
            .ToArray();

        Assert.NotEmpty(codes);

        var repositoryRoot = ResolveRepositoryRoot();
        var englishKeys = LoadResourceKeys(Path.Combine(repositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.resx"));
        var spanishKeys = LoadResourceKeys(Path.Combine(repositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.es.resx"));

        var missingInEnglish = codes.Where(code => !englishKeys.Contains(code)).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        var missingInSpanish = codes.Where(code => !spanishKeys.Contains(code)).OrderBy(code => code, StringComparer.Ordinal).ToArray();

        Assert.True(missingInEnglish.Length == 0, $"Missing in BackendMessages.resx: {string.Join(", ", missingInEnglish)}");
        Assert.True(missingInSpanish.Length == 0, $"Missing in BackendMessages.es.resx: {string.Join(", ", missingInSpanish)}");
    }

    // ── PR-4 application handler error codes are bilingual (EN + ES) ────────────────────────────────

    [Theory]
    [InlineData("OVERTIME_APPLICATION_NOT_FOUND")]
    [InlineData("OVERTIME_APPLICATION_PAYROLL_PERIOD_INVALID")]
    [InlineData("OVERTIME_APPLY_PERIOD_CONFLICT")]
    public void ApplicationHandlerErrorCodes_ArePresentInBothResourceCatalogs(string code)
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var englishKeys = LoadResourceKeys(Path.Combine(repositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.resx"));
        var spanishKeys = LoadResourceKeys(Path.Combine(repositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.es.resx"));

        Assert.True(englishKeys.Contains(code), $"Missing in BackendMessages.resx: {code}");
        Assert.True(spanishKeys.Contains(code), $"Missing in BackendMessages.es.resx: {code}");
    }

    // ── Builders ───────────────────────────────────────────────────────────────────────────────────

    private static PersonnelFileOvertimeRecordApplication Apply(PersonnelFileOvertimeRecord record) =>
        record.Apply(
            appliedDate: Today,
            today: Today,
            payrollTypeCode: record.PayrollTypeCode,
            payrollPeriodId: null,
            payrollPeriodPublicId: null,
            payrollPeriodLabel: record.PayrollPeriodLabel,
            originCode: OvertimeApplicationOrigins.Manual,
            appliedByUserId: Guid.NewGuid(),
            settlementPublicId: null,
            notes: null);

    private static PersonnelFileOvertimeRecord Build(
        DateOnly? workDate = null,
        decimal typeFactor = 2.00m,
        decimal factorApplied = 2.00m,
        string? factorOverrideNote = null,
        int durationHours = 2,
        int durationMinutes = 0) =>
        PersonnelFileOvertimeRecord.Create(
            workDate: workDate ?? Today,
            overtimeTypePublicId: Guid.NewGuid(),
            overtimeTypeCodeSnapshot: "HED",
            overtimeTypeNameSnapshot: "Hora extra diurna",
            typeFactorSnapshot: typeFactor,
            factorApplied: factorApplied,
            factorOverrideNote: factorOverrideNote,
            durationHours: durationHours,
            durationMinutes: durationMinutes,
            startTime: null,
            endTime: null,
            justificationTypePublicId: Guid.NewGuid(),
            justificationCodeSnapshot: "OPERATIVA",
            justificationNameSnapshot: "Carga operativa",
            observations: null,
            originChannel: OvertimeRecordChannels.Rrhh,
            assignedPositionPublicId: Guid.NewGuid(),
            requesterFilePublicId: Guid.NewGuid(),
            requesterNameSnapshot: "Jefe solicitante",
            payrollTypeCode: "MENSUAL",
            payrollPeriodId: null,
            payrollPeriodPublicId: null,
            payrollPeriodLabel: "Julio 2026",
            payrollPeriodEndDate: new DateOnly(2026, 7, 31),
            requestedByUserId: Guid.NewGuid());

    private static HashSet<string> LoadResourceKeys(string path)
    {
        var document = XDocument.Load(path);
        return document.Root?
            .Elements("data")
            .Select(static element => element.Attribute("name")?.Value)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal)
            ?? [];
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root for localization tests.");
    }
}
