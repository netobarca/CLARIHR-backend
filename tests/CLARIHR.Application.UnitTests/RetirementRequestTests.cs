using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the definitive-retirement feature: the pure rules (eligibility RN-001.1, date coherence
/// RN-001.4/RF-016, executability D-05, the ratified 30-day reversal window RN-012.4, closing blockers R-T5),
/// the domain state machine on <see cref="PersonnelFileRetirementRequest"/> (Resolve/Cancel/MarkExecuted/
/// MarkReverted), the profile ApplyRetirement/ClearRetirement pair and the assignment/contract Reopen
/// counterparts (D-11 — exact restoration).
/// </summary>
public sealed class RetirementRequestTests
{
    private static readonly DateTime AsOf = new(2026, 7, 4, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Requester = Guid.NewGuid();

    private static PersonnelFileRetirementRequest NewRequest(
        DateTime? requestDate = null,
        DateTime? retirementDate = null) =>
        PersonnelFileRetirementRequest.Create(
            Requester,
            "Ana Beatriz Pérez",
            requestDate ?? new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            retirementDate ?? new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc),
            "voluntaria",
            "Renuncia voluntaria",
            "renuncia",
            "Renuncia",
            "Observación de prueba",
            Guid.NewGuid());

    private static PersonnelFileRetirementRequest AuthorizedRequest()
    {
        var request = NewRequest();
        request.Resolve(RetirementRequestStatuses.Autorizada, Guid.NewGuid(), AsOf, null);
        return request;
    }

    private static PersonnelFileRetirementRequest ExecutedRequest()
    {
        var request = AuthorizedRequest();
        request.MarkExecuted(Guid.NewGuid(), AsOf, "ACTIVO", priorLoginWasActive: true, priorRehireBlocked: false, priorRehireBlockReason: null);
        return request;
    }

    // ── Pure rules ────────────────────────────────────────────────────────────────

    [Fact]
    public void IsWithinReversalWindow_OnDay30_IsAllowed() =>
        Assert.True(RetirementRequestRules.IsWithinReversalWindow(AsOf, AsOf.AddDays(30)));

    [Fact]
    public void IsWithinReversalWindow_JustPastDay30_IsBlocked() =>
        Assert.False(RetirementRequestRules.IsWithinReversalWindow(AsOf, AsOf.AddDays(30).AddMinutes(1)));

    [Fact]
    public void IsWithinReversalWindow_WellWithin_IsAllowed() =>
        Assert.True(RetirementRequestRules.IsWithinReversalWindow(AsOf, AsOf.AddDays(3)));

    [Fact]
    public void AreDatesCoherent_ValidRange_IsTrue() =>
        Assert.True(RetirementRequestRules.AreDatesCoherent(
            requestDate: AsOf.Date,
            retirementDate: AsOf.Date.AddDays(10),
            hireDate: AsOf.Date.AddYears(-2),
            asOfUtc: AsOf));

    [Fact]
    public void AreDatesCoherent_FutureRequestDate_IsFalse() =>
        Assert.False(RetirementRequestRules.AreDatesCoherent(
            requestDate: AsOf.Date.AddDays(1),
            retirementDate: AsOf.Date.AddDays(10),
            hireDate: AsOf.Date.AddYears(-2),
            asOfUtc: AsOf));

    [Fact]
    public void AreDatesCoherent_RetirementBeforeHire_IsFalse() =>
        Assert.False(RetirementRequestRules.AreDatesCoherent(
            requestDate: AsOf.Date,
            retirementDate: AsOf.Date.AddYears(-3),
            hireDate: AsOf.Date.AddYears(-2),
            asOfUtc: AsOf));

    [Fact]
    public void AreDatesCoherent_RetirementOnHireDate_IsTrue() =>
        Assert.True(RetirementRequestRules.AreDatesCoherent(
            requestDate: AsOf.Date,
            retirementDate: AsOf.Date.AddYears(-2),
            hireDate: AsOf.Date.AddYears(-2),
            asOfUtc: AsOf));

    [Fact]
    public void IsExecutableOn_TodayOrPast_IsTrue()
    {
        Assert.True(RetirementRequestRules.IsExecutableOn(AsOf.Date, AsOf));
        Assert.True(RetirementRequestRules.IsExecutableOn(AsOf.Date.AddDays(-30), AsOf));
    }

    [Fact]
    public void IsExecutableOn_Tomorrow_IsFalse() =>
        Assert.False(RetirementRequestRules.IsExecutableOn(AsOf.Date.AddDays(1), AsOf));

    [Fact]
    public void IsEligibleForRequest_CompletedActiveNotRetired_IsTrue() =>
        Assert.True(RetirementRequestRules.IsEligibleForRequest(
            isCompletedEmployee: true, fileIsActive: true, profileRetirementDate: null));

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IsEligibleForRequest_NotCompletedOrInactive_IsFalse(bool completed, bool active) =>
        Assert.False(RetirementRequestRules.IsEligibleForRequest(completed, active, profileRetirementDate: null));

    [Fact]
    public void IsEligibleForRequest_AlreadyRetired_IsFalse() =>
        Assert.False(RetirementRequestRules.IsEligibleForRequest(
            isCompletedEmployee: true, fileIsActive: true, profileRetirementDate: AsOf.Date));

    [Fact]
    public void HasClosingBlockers_RowStartsAfterRetirementDate_IsTrue() =>
        Assert.True(RetirementRequestRules.HasClosingBlockers(
            [AsOf.Date.AddDays(-30), AsOf.Date.AddDays(5)], AsOf.Date));

    [Fact]
    public void HasClosingBlockers_AllRowsStartBefore_IsFalse() =>
        Assert.False(RetirementRequestRules.HasClosingBlockers(
            [AsOf.Date.AddDays(-30), AsOf.Date], AsOf.Date));

    [Fact]
    public void HasClosingBlockers_NoActiveRows_IsFalse() =>
        Assert.False(RetirementRequestRules.HasClosingBlockers([], AsOf.Date));

    // ── Domain state machine ──────────────────────────────────────────────────────

    [Fact]
    public void Create_StartsInSolicitada_WithNormalizedCodesAndSnapshot()
    {
        var request = NewRequest();

        Assert.Equal(RetirementRequestStatuses.Solicitada, request.RequestStatusCode);
        Assert.Equal("VOLUNTARIA", request.RetirementCategoryCode);
        Assert.Equal("RENUNCIA", request.RetirementReasonCode);
        Assert.Equal(Requester, request.RequesterFilePublicId);
        Assert.Equal("Ana Beatriz Pérez", request.RequesterNameSnapshot);
        Assert.True(request.IsActive);
        Assert.Null(request.ResolvedByUserId);
        Assert.Null(request.ExecutionDateUtc);
    }

    [Fact]
    public void Update_WhileSolicitada_AppliesFields()
    {
        var request = NewRequest();
        request.Update(
            Requester,
            "Ana Beatriz Pérez de López",
            new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            "INVOLUNTARIA",
            "Despido",
            "REESTRUCTURACION",
            "Reestructuración",
            null);

        Assert.Equal("INVOLUNTARIA", request.RetirementCategoryCode);
        Assert.Equal("Ana Beatriz Pérez de López", request.RequesterNameSnapshot);
        Assert.Null(request.Notes);
    }

    [Fact]
    public void Update_AfterResolution_Throws()
    {
        var request = AuthorizedRequest();
        Assert.Throws<InvalidOperationException>(() => request.Update(
            Requester, "Otro Nombre", AsOf.Date, AsOf.Date, "VOLUNTARIA", null, "RENUNCIA", null, null));
    }

    [Fact]
    public void Resolve_Authorize_SetsResolutionFields_NoteOptional()
    {
        var request = NewRequest();
        var authorizer = Guid.NewGuid();
        request.Resolve(RetirementRequestStatuses.Autorizada, authorizer, AsOf, null);

        Assert.Equal(RetirementRequestStatuses.Autorizada, request.RequestStatusCode);
        Assert.Equal(authorizer, request.ResolvedByUserId);
        Assert.NotNull(request.ResolutionDateUtc);
        Assert.Null(request.ResolutionNotes);
    }

    [Fact]
    public void Resolve_RejectWithoutNote_Throws()
    {
        var request = NewRequest();
        Assert.Throws<InvalidOperationException>(() =>
            request.Resolve(RetirementRequestStatuses.Rechazada, Guid.NewGuid(), AsOf, "   "));
    }

    [Fact]
    public void Resolve_RejectWithNote_IsTerminal()
    {
        var request = NewRequest();
        request.Resolve(RetirementRequestStatuses.Rechazada, Guid.NewGuid(), AsOf, "No procede la baja.");

        Assert.Equal(RetirementRequestStatuses.Rechazada, request.RequestStatusCode);
        Assert.Equal("No procede la baja.", request.ResolutionNotes);
    }

    [Fact]
    public void Resolve_InvalidTarget_Throws()
    {
        var request = NewRequest();
        Assert.Throws<InvalidOperationException>(() =>
            request.Resolve(RetirementRequestStatuses.Ejecutada, Guid.NewGuid(), AsOf, null));
    }

    [Fact]
    public void Resolve_AlreadyResolved_Throws()
    {
        var request = AuthorizedRequest();
        Assert.Throws<InvalidOperationException>(() =>
            request.Resolve(RetirementRequestStatuses.Rechazada, Guid.NewGuid(), AsOf, "nota"));
    }

    [Fact]
    public void Cancel_FromSolicitadaAndAutorizada_SetsCancellationFields()
    {
        var solicitada = NewRequest();
        var canceledBy = Guid.NewGuid();
        solicitada.Cancel(canceledBy, AsOf, "Registrada por error");
        Assert.Equal(RetirementRequestStatuses.Anulada, solicitada.RequestStatusCode);
        Assert.Equal(canceledBy, solicitada.CanceledByUserId);
        Assert.Equal("Registrada por error", solicitada.CancellationNotes);

        var autorizada = AuthorizedRequest();
        autorizada.Cancel(Guid.NewGuid(), AsOf, null);
        Assert.Equal(RetirementRequestStatuses.Anulada, autorizada.RequestStatusCode);
    }

    [Fact]
    public void Cancel_Executed_Throws()
    {
        var request = ExecutedRequest();
        Assert.Throws<InvalidOperationException>(() => request.Cancel(Guid.NewGuid(), AsOf, null));
    }

    [Fact]
    public void MarkExecuted_FromAutorizada_CapturesSnapshotAndExactTimestamp()
    {
        var request = AuthorizedRequest();
        var executor = Guid.NewGuid();
        var executedAt = new DateTime(2026, 7, 4, 16, 42, 30, DateTimeKind.Utc);

        request.MarkExecuted(executor, executedAt, "LICENCIA", priorLoginWasActive: true, priorRehireBlocked: true, priorRehireBlockReason: "Bloqueado por política");

        Assert.Equal(RetirementRequestStatuses.Ejecutada, request.RequestStatusCode);
        Assert.Equal(executor, request.ExecutedByUserId);
        Assert.Equal(executedAt, request.ExecutionDateUtc); // exact timestamp — 30-day window anchor
        Assert.Equal("LICENCIA", request.PriorEmploymentStatusCode);
        Assert.True(request.PriorLoginWasActive);
        Assert.True(request.PriorRehireBlocked);
        Assert.Equal("Bloqueado por política", request.PriorRehireBlockReason);
    }

    [Fact]
    public void MarkExecuted_FromSolicitada_Throws()
    {
        var request = NewRequest();
        Assert.Throws<InvalidOperationException>(() =>
            request.MarkExecuted(Guid.NewGuid(), AsOf, "ACTIVO", null, false, null));
    }

    [Fact]
    public void AddClosedRecord_TracksClosedRows()
    {
        var request = ExecutedRequest();
        var assignmentId = Guid.NewGuid();
        request.AddClosedRecord(RetirementRequestClosedRecord.Create(
            RetirementClosedRecordKinds.Assignment, assignmentId, previousEndDate: null));
        request.AddClosedRecord(RetirementRequestClosedRecord.Create(
            RetirementClosedRecordKinds.Contract, Guid.NewGuid(), previousEndDate: AsOf.Date));

        Assert.Equal(2, request.ClosedRecords.Count);
        var assignmentRecord = request.ClosedRecords.First(record => record.EntityKind == RetirementClosedRecordKinds.Assignment);
        Assert.Equal(assignmentId, assignmentRecord.EntityPublicId);
        Assert.Null(assignmentRecord.PreviousEndDate);
    }

    [Fact]
    public void MarkReverted_FromEjecutada_SetsReversalFields()
    {
        var request = ExecutedRequest();
        var reverter = Guid.NewGuid();
        var revertedAt = new DateTime(2026, 7, 10, 9, 15, 0, DateTimeKind.Utc);

        request.MarkReverted(reverter, revertedAt, "Baja registrada por error administrativo");

        Assert.Equal(RetirementRequestStatuses.Revertida, request.RequestStatusCode);
        Assert.Equal(reverter, request.RevertedByUserId);
        Assert.Equal(revertedAt, request.ReversalDateUtc);
        Assert.Equal("Baja registrada por error administrativo", request.ReversalReason);
        // History preserved (RN-010.4): execution data survives the reversal.
        Assert.NotNull(request.ExecutionDateUtc);
        Assert.NotNull(request.ResolvedByUserId);
    }

    [Fact]
    public void MarkReverted_WithoutReason_Throws()
    {
        var request = ExecutedRequest();
        Assert.ThrowsAny<ArgumentException>(() => request.MarkReverted(Guid.NewGuid(), AsOf, "   "));
    }

    [Fact]
    public void MarkReverted_NotExecuted_Throws()
    {
        var request = AuthorizedRequest();
        Assert.Throws<InvalidOperationException>(() => request.MarkReverted(Guid.NewGuid(), AsOf, "motivo"));
    }

    // ── Profile ApplyRetirement / ClearRetirement (RF-006/RF-010) ────────────────

    private static PersonnelFileEmployeeProfile NewProfile() =>
        PersonnelFileEmployeeProfile.Create(
            "EMP-001",
            "ACTIVO",
            new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            retirementCategoryCode: null,
            retirementReasonCode: null,
            retirementNotes: null,
            retirementDate: null);

    [Fact]
    public void ApplyRetirement_StampsFieldsAndRetiradoStatus()
    {
        var profile = NewProfile();
        profile.ApplyRetirement("voluntaria", "renuncia", "Se retira por estudios", new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode, profile.EmploymentStatusCode);
        Assert.Equal("VOLUNTARIA", profile.RetirementCategoryCode);
        Assert.Equal("RENUNCIA", profile.RetirementReasonCode);
        Assert.Equal("Se retira por estudios", profile.RetirementNotes);
        Assert.Equal(new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc), profile.RetirementDate);
    }

    [Fact]
    public void ApplyRetirement_BeforeHireDate_Throws()
    {
        var profile = NewProfile();
        Assert.Throws<InvalidOperationException>(() =>
            profile.ApplyRetirement("VOLUNTARIA", "RENUNCIA", null, new DateTime(2023, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ClearRetirement_RestoresPriorStatusAndClearsMetadata()
    {
        var profile = NewProfile();
        profile.ApplyRetirement("VOLUNTARIA", "RENUNCIA", "nota", new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc));

        profile.ClearRetirement("LICENCIA"); // restores the SNAPSHOT status, never assumes ACTIVO (D-11)

        Assert.Equal("LICENCIA", profile.EmploymentStatusCode);
        Assert.Null(profile.RetirementCategoryCode);
        Assert.Null(profile.RetirementReasonCode);
        Assert.Null(profile.RetirementNotes);
        Assert.Null(profile.RetirementDate);
    }

    // ── Assignment / contract Reopen (D-11 — exact restoration) ─────────────────

    [Fact]
    public void AssignmentReopen_RestoresPreviousEndDateExactly()
    {
        var previousEnd = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var assignment = PersonnelFileEmploymentAssignment.Create(
            "PLAZA", null, null, null, Guid.NewGuid(), null, null, null,
            startDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: previousEnd,
            isPrimary: true,
            isActive: true,
            notes: null);

        assignment.Close(new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc));
        Assert.False(assignment.IsActive);

        assignment.Reopen(previousEnd);
        Assert.True(assignment.IsActive);
        Assert.Equal(previousEnd, assignment.EndDate);

        assignment.Close(new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc));
        assignment.Reopen(previousEndDate: null); // the execution set the end date → reversal clears it
        Assert.True(assignment.IsActive);
        Assert.Null(assignment.EndDate);
    }

    [Fact]
    public void ContractReopen_RestoresPreviousEndDateExactly()
    {
        var contract = PersonnelFileContractHistory.Create(
            "INDEFINIDO",
            contractDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            contractEndDate: null,
            positionSlotPublicId: null,
            isActive: true,
            notes: null);

        contract.Close(new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc));
        Assert.False(contract.IsActive);
        Assert.NotNull(contract.ContractEndDate);

        contract.Reopen(previousContractEndDate: null);
        Assert.True(contract.IsActive);
        Assert.Null(contract.ContractEndDate);
    }
}
