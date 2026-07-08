using CLARIHR.Domain.Leave;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Domain guards of the compensatory-time sub-resources (REQ-002 PR-2): the credit's declarative Create
/// (positive hours, coherent time range, non-empty detail / authorized-by), the
/// <see cref="PersonnelFileCompensatoryTimeCredit.ApplyCreditedHours"/>-only calculation write path (override
/// note mandatory, positive factor / hours), the REGISTRADA-gated Update / Annul lifecycle, the mirrored
/// document guards, the absence guards, and the PR-1 <see cref="CompensatoryTimeType"/> factor / operation
/// invariants (re-verified here).
/// </summary>
public sealed class CompensatoryTimeDomainTests
{
    private static readonly DateOnly Work = new(2026, 7, 1);
    private static readonly Guid AuthorizerFile = Guid.NewGuid();
    private static readonly Guid Position = Guid.NewGuid();
    private static readonly Guid Overtime = Guid.NewGuid();

    // ── Credit: Create ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreditCreate_Valid_StartsRegistrada()
    {
        var credit = CreateCredit();

        Assert.Equal(CompensatoryTimeStatuses.Registrada, credit.StatusCode);
        Assert.True(credit.IsActive);
        Assert.NotEqual(Guid.Empty, credit.PublicId);
        Assert.NotEqual(Guid.Empty, credit.ConcurrencyToken);
        Assert.Equal(Overtime, credit.OvertimeRecordPublicId);
        Assert.Equal(3m, credit.HoursWorked);
    }

    [Fact]
    public void CreditCreate_NonPositiveHours_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCredit(hoursWorked: 0m));
    }

    [Fact]
    public void CreditCreate_IncoherentTimeRange_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateCredit(
            startTime: new TimeOnly(17, 0),
            endTime: new TimeOnly(15, 0)));
    }

    [Fact]
    public void CreditCreate_CoherentTimeRange_Succeeds()
    {
        var credit = CreateCredit(startTime: new TimeOnly(15, 0), endTime: new TimeOnly(17, 0));

        Assert.Equal(new TimeOnly(15, 0), credit.StartTime);
        Assert.Equal(new TimeOnly(17, 0), credit.EndTime);
    }

    [Fact]
    public void CreditCreate_EmptyWorkDetail_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateCredit(workDetail: "   "));
    }

    [Fact]
    public void CreditCreate_EmptyAuthorizedBy_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateCredit(authorizedByText: ""));
    }

    [Fact]
    public void CreditCreate_EmptyGuidReferences_Throw()
    {
        Assert.Throws<ArgumentException>(() => CreateCredit(authorizerFilePublicId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateCredit(assignedPositionPublicId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateCredit(overtimeRecordPublicId: Guid.Empty));
    }

    // ── Credit: ApplyCreditedHours (the only calculation write path) ────────────────────────────────

    [Fact]
    public void ApplyCreditedHours_WritesFactorAndHours_RotatesToken()
    {
        var credit = CreateCredit();
        var before = credit.ConcurrencyToken;

        credit.ApplyCreditedHours(factorApplied: 2.00m, hoursCredited: 6.00m, isOverridden: false, overrideNote: null);

        Assert.Equal(2.00m, credit.FactorApplied);
        Assert.Equal(6.00m, credit.HoursCredited);
        Assert.False(credit.IsOverridden);
        Assert.Null(credit.OverrideNote);
        Assert.NotEqual(before, credit.ConcurrencyToken);
    }

    [Fact]
    public void ApplyCreditedHours_OverrideWithoutNote_Throws()
    {
        var credit = CreateCredit();

        Assert.Throws<ArgumentException>(() =>
            credit.ApplyCreditedHours(1.00m, 3.00m, isOverridden: true, overrideNote: "  "));
    }

    [Fact]
    public void ApplyCreditedHours_OverrideWithNote_PersistsNote()
    {
        var credit = CreateCredit();

        credit.ApplyCreditedHours(1.00m, 5.00m, isOverridden: true, overrideNote: "Ajuste manual autorizado");

        Assert.True(credit.IsOverridden);
        Assert.Equal("Ajuste manual autorizado", credit.OverrideNote);
    }

    [Fact]
    public void ApplyCreditedHours_NonPositiveFactorOrHours_Throw()
    {
        var credit = CreateCredit();

        Assert.Throws<ArgumentOutOfRangeException>(() => credit.ApplyCreditedHours(0m, 3m, false, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => credit.ApplyCreditedHours(1m, 0m, false, null));
    }

    // ── Credit: lifecycle ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreditUpdate_OnlyWhileRegistrada()
    {
        var credit = CreateCredit();
        credit.Annul("motivo", "user-1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => credit.Update(
            10L, "Tipo", Work, null, null, 4m, "detalle", "jefatura", AuthorizerFile, Position, Overtime, null));
    }

    [Fact]
    public void CreditAnnul_RequiresReason()
    {
        var credit = CreateCredit();

        Assert.Throws<ArgumentException>(() => credit.Annul("  ", "user-1", DateTime.UtcNow));
    }

    [Fact]
    public void CreditAnnul_Valid_TransitionsToAnulada()
    {
        var credit = CreateCredit();

        credit.Annul("Registro erróneo", "user-1", DateTime.UtcNow);

        Assert.Equal(CompensatoryTimeStatuses.Anulada, credit.StatusCode);
        Assert.False(credit.IsActive);
        Assert.Equal("Registro erróneo", credit.AnnulmentReason);
        Assert.Equal("user-1", credit.AnnulledByUserId);
        Assert.NotNull(credit.AnnulledUtc);
    }

    [Fact]
    public void CreditAnnul_Twice_Throws()
    {
        var credit = CreateCredit();
        credit.Annul("motivo", "user-1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => credit.Annul("otro", "user-1", DateTime.UtcNow));
    }

    [Fact]
    public void ApplyCreditedHours_AfterAnnul_Throws()
    {
        var credit = CreateCredit();
        credit.Annul("motivo", "user-1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => credit.ApplyCreditedHours(1m, 3m, false, null));
    }

    // ── Credit document (mirror of the incapacity document) ─────────────────────────────────────────

    [Fact]
    public void CreditDocumentCreate_EmptyFilePublicId_Throws()
    {
        Assert.Throws<ArgumentException>(() => PersonnelFileCompensatoryTimeCreditDocument.Create(
            Guid.NewGuid(), documentTypeCatalogItemId: null, filePublicId: Guid.Empty,
            fileName: "auth.pdf", contentType: "application/pdf", sizeBytes: 100, observations: null));
    }

    [Fact]
    public void CreditDocumentCreate_Valid_BindsToCredit()
    {
        var document = PersonnelFileCompensatoryTimeCreditDocument.Create(
            Guid.NewGuid(), documentTypeCatalogItemId: null, filePublicId: Guid.NewGuid(),
            fileName: "auth.pdf", contentType: "application/pdf", sizeBytes: 100, observations: "Autorización");

        document.BindToCredit(42L);

        Assert.Equal(42L, document.CreditId);
        Assert.True(document.IsActive);
        Assert.Equal("Autorización", document.Observations);
    }

    // ── Absence ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AbsenceCreate_Valid_StartsRegistrada()
    {
        var absence = CreateAbsence();

        Assert.Equal(CompensatoryTimeStatuses.Registrada, absence.StatusCode);
        Assert.True(absence.IsActive);
        Assert.Equal(8m, absence.HoursDebited);
    }

    [Fact]
    public void AbsenceCreate_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateAbsence(
            startDate: new DateOnly(2026, 7, 10),
            endDate: new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void AbsenceCreate_FutureRange_Allowed()
    {
        var absence = CreateAbsence(
            startDate: new DateOnly(2030, 1, 1),
            endDate: new DateOnly(2030, 1, 2));

        Assert.Equal(new DateOnly(2030, 1, 1), absence.StartDate);
    }

    [Fact]
    public void AbsenceCreate_NonPositiveHours_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateAbsence(hoursDebited: 0m));
    }

    [Fact]
    public void AbsenceCreate_EmptyReason_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateAbsence(reason: "  "));
    }

    [Fact]
    public void AbsenceUpdate_OnlyWhileRegistrada()
    {
        var absence = CreateAbsence();
        absence.Annul("motivo", "user-1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => absence.Update(
            10L, "Tipo", Work, Work, 4m, "razón", null, null));
    }

    [Fact]
    public void AbsenceAnnul_Valid_TransitionsToAnulada()
    {
        var absence = CreateAbsence();

        absence.Annul("Goce cancelado", "user-1", DateTime.UtcNow);

        Assert.Equal(CompensatoryTimeStatuses.Anulada, absence.StatusCode);
        Assert.False(absence.IsActive);
        Assert.Equal("Goce cancelado", absence.AnnulmentReason);
    }

    [Fact]
    public void AbsenceAnnul_RequiresReason()
    {
        var absence = CreateAbsence();

        Assert.Throws<ArgumentException>(() => absence.Annul(" ", "user-1", DateTime.UtcNow));
    }

    // ── CompensatoryTimeType (PR-1) — re-verified ───────────────────────────────────────────────────

    [Fact]
    public void CompensatoryTimeType_NonPositiveFactor_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CompensatoryTimeType.Create("TRABAJO", "Trabajo fuera de jornada", CompensatoryTimeOperations.Credits, 0m, 1));
    }

    [Fact]
    public void CompensatoryTimeType_InvalidOperation_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CompensatoryTimeType.Create("TRABAJO", "Trabajo fuera de jornada", "FOO", 1.00m, 1));
    }

    // ── Factories ───────────────────────────────────────────────────────────────────────────────────

    private static PersonnelFileCompensatoryTimeCredit CreateCredit(
        decimal hoursWorked = 3m,
        TimeOnly? startTime = null,
        TimeOnly? endTime = null,
        string workDetail = "Soporte en cierre contable",
        string authorizedByText = "Jefatura de finanzas",
        Guid? authorizerFilePublicId = null,
        Guid? assignedPositionPublicId = null,
        Guid? overtimeRecordPublicId = null) =>
        PersonnelFileCompensatoryTimeCredit.Create(
            compensatoryTimeTypeId: 10L,
            typeNameSnapshot: "Trabajo fuera de jornada",
            workDate: Work,
            startTime: startTime,
            endTime: endTime,
            hoursWorked: hoursWorked,
            workDetail: workDetail,
            authorizedByText: authorizedByText,
            authorizerFilePublicId: authorizerFilePublicId ?? AuthorizerFile,
            assignedPositionPublicId: assignedPositionPublicId ?? Position,
            overtimeRecordPublicId: overtimeRecordPublicId ?? Overtime,
            registeredByUserId: "user-1",
            notes: null);

    private static PersonnelFileCompensatoryTimeAbsence CreateAbsence(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        decimal hoursDebited = 8m,
        string reason = "Goce de tiempo compensatorio") =>
        PersonnelFileCompensatoryTimeAbsence.Create(
            compensatoryTimeTypeId: 10L,
            typeNameSnapshot: "Goce de tiempo compensatorio",
            startDate: startDate ?? Work,
            endDate: endDate ?? Work,
            hoursDebited: hoursDebited,
            reason: reason,
            payrollPeriodPublicId: null,
            registeredByUserId: "user-1",
            notes: null);
}
