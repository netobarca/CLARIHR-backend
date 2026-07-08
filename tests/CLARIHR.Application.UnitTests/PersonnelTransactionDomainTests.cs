using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Domain guards of the "otras transacciones de personal" sub-resources (REQ-003 PR-2): the recognition /
/// disciplinary-action one-decision lifecycle (Create → EN_REVISION; Update/Apply/Reject only while
/// EN_REVISION; Annul from EN_REVISION or APLICADA with a mandatory reason), the suspension-flag snapshot and
/// the calendar-inclusive derived suspension days, the concept snapshot frozen at Apply, and the surgical
/// <see cref="PersonnelFilePersonnelAction.Annul"/> mutator (aclaración №4).
/// </summary>
public sealed class PersonnelTransactionDomainTests
{
    private static readonly DateOnly Event = new(2026, 5, 1);

    // ── Recognition: Create / Update ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RecognitionCreate_Valid_StartsEnRevision()
    {
        var recognition = CreateRecognition();

        Assert.Equal(PersonnelTransactionStatuses.EnRevision, recognition.StatusCode);
        Assert.True(recognition.IsActive);
        Assert.NotEqual(Guid.Empty, recognition.PublicId);
        Assert.NotEqual(Guid.Empty, recognition.ConcurrencyToken);
        Assert.Null(recognition.PersonnelActionPublicId);
    }

    [Fact]
    public void RecognitionCreate_NonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRecognition(amount: 0m));
    }

    [Fact]
    public void RecognitionCreate_EmptyDetail_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateRecognition(detail: "   "));
    }

    [Fact]
    public void RecognitionUpdate_AfterApply_Throws()
    {
        var recognition = CreateRecognition();
        recognition.Apply("user-authorizer", DateTime.UtcNow, Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            recognition.Update(1, "Felicitación", Event, "edited", null, null, null, null));
    }

    // ── Recognition: Apply / Reject / Annul ──────────────────────────────────────────────────────────

    [Fact]
    public void RecognitionApply_FromEnRevision_CapturesEntryPublicId()
    {
        var recognition = CreateRecognition();
        var entry = Guid.NewGuid();
        var previousToken = recognition.ConcurrencyToken;

        recognition.Apply("user-authorizer", DateTime.UtcNow, entry);

        Assert.Equal(PersonnelTransactionStatuses.Aplicada, recognition.StatusCode);
        Assert.Equal(entry, recognition.PersonnelActionPublicId);
        Assert.Equal("user-authorizer", recognition.DecidedByUserId);
        Assert.NotEqual(previousToken, recognition.ConcurrencyToken);
    }

    [Fact]
    public void RecognitionApply_EmptyEntryPublicId_Throws()
    {
        var recognition = CreateRecognition();
        Assert.Throws<ArgumentException>(() => recognition.Apply("user-authorizer", DateTime.UtcNow, Guid.Empty));
    }

    [Fact]
    public void RecognitionReject_RequiresNote()
    {
        var recognition = CreateRecognition();
        Assert.Throws<ArgumentException>(() => recognition.Reject("user-authorizer", DateTime.UtcNow, "  "));
    }

    [Fact]
    public void RecognitionReject_WithNote_MovesToRechazada()
    {
        var recognition = CreateRecognition();
        recognition.Reject("user-authorizer", DateTime.UtcNow, "no procede");

        Assert.Equal(PersonnelTransactionStatuses.Rechazada, recognition.StatusCode);
        Assert.Equal("no procede", recognition.DecisionNote);
        Assert.False(recognition.IsActive);
    }

    [Fact]
    public void RecognitionAnnul_FromApplied_RequiresReason_AndMovesToAnulada()
    {
        var recognition = CreateRecognition();
        recognition.Apply("user-authorizer", DateTime.UtcNow, Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => recognition.Annul("  ", "user-authorizer", DateTime.UtcNow));

        recognition.Annul("revocada por apelación", "user-authorizer", DateTime.UtcNow);
        Assert.Equal(PersonnelTransactionStatuses.Anulada, recognition.StatusCode);
        Assert.Equal("revocada por apelación", recognition.AnnulmentReason);
        Assert.False(recognition.IsActive);
    }

    [Fact]
    public void RecognitionAnnul_FromRejected_Throws()
    {
        var recognition = CreateRecognition();
        recognition.Reject("user-authorizer", DateTime.UtcNow, "no procede");

        Assert.Throws<InvalidOperationException>(() => recognition.Annul("x", "user-authorizer", DateTime.UtcNow));
    }

    // ── Disciplinary action: Create snapshots + suspension days ──────────────────────────────────────

    [Fact]
    public void DisciplinaryCreate_SnapshotsSuspensionFlag_AndComputesDays()
    {
        var action = CreateDisciplinary(
            typeAppliedSuspension: true,
            suspensionStart: new DateOnly(2026, 4, 28),
            suspensionEnd: new DateOnly(2026, 5, 3));

        Assert.Equal(PersonnelTransactionStatuses.EnRevision, action.StatusCode);
        Assert.True(action.TypeAppliedSuspension);
        Assert.Equal(6, action.SuspensionDays); // 28-Apr → 3-May inclusive.
    }

    [Fact]
    public void DisciplinaryCreate_NoSuspension_LeavesDaysNull()
    {
        var action = CreateDisciplinary(typeAppliedSuspension: false, suspensionStart: null, suspensionEnd: null);

        Assert.Null(action.SuspensionDays);
        Assert.Null(action.SuspensionStartDate);
    }

    [Fact]
    public void DisciplinaryCreate_PartialSuspensionDates_Throw()
    {
        Assert.Throws<ArgumentException>(() => CreateDisciplinary(
            typeAppliedSuspension: true,
            suspensionStart: new DateOnly(2026, 4, 28),
            suspensionEnd: null));
    }

    [Fact]
    public void DisciplinaryCreate_SuspensionStartAfterEnd_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateDisciplinary(
            typeAppliedSuspension: true,
            suspensionStart: new DateOnly(2026, 5, 3),
            suspensionEnd: new DateOnly(2026, 4, 28)));
    }

    [Fact]
    public void DisciplinaryCreate_NonPositiveDeduction_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDisciplinary(
            hasDeduction: true,
            deductionAmount: 0m));
    }

    // ── Disciplinary action: Apply freezes concept + captures entry ids ──────────────────────────────

    [Fact]
    public void DisciplinaryApply_FreezesConcept_AndCapturesEntryIds()
    {
        var action = CreateDisciplinary(
            typeAppliedSuspension: true,
            suspensionStart: new DateOnly(2026, 4, 28),
            suspensionEnd: new DateOnly(2026, 5, 3),
            hasDeduction: true,
            deductionAmount: 25m);
        var entry = Guid.NewGuid();
        var suspensionEntry = Guid.NewGuid();

        action.Apply("user-authorizer", DateTime.UtcNow, entry, suspensionEntry, "descuento_interno", "Descuento interno");

        Assert.Equal(PersonnelTransactionStatuses.Aplicada, action.StatusCode);
        Assert.Equal(entry, action.PersonnelActionPublicId);
        Assert.Equal(suspensionEntry, action.SuspensionActionPublicId);
        Assert.Equal("DESCUENTO_INTERNO", action.DeductionConceptTypeCode);
        Assert.Equal("Descuento interno", action.DeductionConceptNameSnapshot);
    }

    [Fact]
    public void DisciplinaryApply_WithoutSuspensionEntry_IsAllowed()
    {
        var action = CreateDisciplinary(typeAppliedSuspension: false, suspensionStart: null, suspensionEnd: null);

        action.Apply("user-authorizer", DateTime.UtcNow, Guid.NewGuid(), null, null, null);

        Assert.Equal(PersonnelTransactionStatuses.Aplicada, action.StatusCode);
        Assert.Null(action.SuspensionActionPublicId);
        Assert.Null(action.DeductionConceptTypeCode);
    }

    [Fact]
    public void DisciplinaryAnnul_FromApplied_MovesToAnulada()
    {
        var action = CreateDisciplinary(typeAppliedSuspension: false, suspensionStart: null, suspensionEnd: null);
        action.Apply("user-authorizer", DateTime.UtcNow, Guid.NewGuid(), null, null, null);

        action.Annul("revocada", "user-authorizer", DateTime.UtcNow);

        Assert.Equal(PersonnelTransactionStatuses.Anulada, action.StatusCode);
        Assert.False(action.IsActive);
    }

    // ── PersonnelFilePersonnelAction.Annul() — surgical mutator (aclaración №4) ───────────────────────

    [Fact]
    public void PersonnelActionAnnul_FromApplied_MovesToAnulada_AndRotatesToken()
    {
        var entry = PersonnelFilePersonnelAction.Create(
            "RECONOCIMIENTO", "APLICADA", DateTime.UtcNow, null, null, "desc", null, null, null, isSystemGenerated: true);
        var previousToken = entry.ConcurrencyToken;

        entry.Annul();

        Assert.Equal("ANULADA", entry.ActionStatusCode);
        Assert.NotEqual(previousToken, entry.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelActionAnnul_AlreadyAnulada_Throws()
    {
        var entry = PersonnelFilePersonnelAction.Create(
            "AMONESTACION", "ANULADA", DateTime.UtcNow, null, null, "desc", null, null, null, isSystemGenerated: true);

        Assert.Throws<InvalidOperationException>(() => entry.Annul());
    }

    // ── Builders ─────────────────────────────────────────────────────────────────────────────────────

    private static PersonnelFileRecognition CreateRecognition(
        string detail = "Excelente desempeño",
        decimal? amount = 100m) =>
        PersonnelFileRecognition.Create(
            recognitionTypeId: 1,
            typeNameSnapshot: "Felicitación escrita",
            eventDate: Event,
            detail: detail,
            amount: amount,
            currencyCode: amount is null ? null : "USD",
            assignedPositionPublicId: null,
            registeredByUserId: "user-hr",
            notes: null);

    private static PersonnelFileDisciplinaryAction CreateDisciplinary(
        bool typeAppliedSuspension = true,
        DateOnly? suspensionStart = null,
        DateOnly? suspensionEnd = null,
        bool hasDeduction = false,
        decimal? deductionAmount = null) =>
        PersonnelFileDisciplinaryAction.Create(
            disciplinaryActionTypeId: 1,
            typeNameSnapshot: "Suspensión sin goce",
            typeAppliedSuspension: typeAppliedSuspension,
            disciplinaryActionCauseId: 1,
            causeNameSnapshot: "Inasistencia injustificada",
            incidentDate: Event,
            factsDetail: "Faltó tres días sin aviso.",
            hasPayrollDeduction: hasDeduction,
            deductionAmount: deductionAmount,
            currencyCode: deductionAmount is null ? null : "USD",
            suspensionStartDate: suspensionStart,
            suspensionEndDate: suspensionEnd,
            assignedPositionPublicId: null,
            registeredByUserId: "user-hr",
            notes: null);
}
