using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the employee economic-aid feature: the pure rules (seniority eligibility D-08, approved-amount
/// D-05, resolution-target whitelist), the domain transition guards on
/// <see cref="PersonnelFileEconomicAidRequest"/> (Resolve/Disburse/Cancel — D-03/D-09/D-11) and the input/command
/// validators.
/// </summary>
public sealed class EconomicAidRequestTests
{
    private static readonly DateTime AsOf = new(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc);

    private static PersonnelFileEconomicAidRequest NewRequest() =>
        PersonnelFileEconomicAidRequest.Create(
            "EMERGENCIA_MEDICA",
            "Emergencia médica",
            "Necesito apoyo por una emergencia médica.",
            500m,
            "USD",
            new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid());

    private static EconomicAidRequestInput ValidInput() =>
        new("EMERGENCIA_MEDICA", "Necesito apoyo.", 500m, "USD", new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc));

    private static bool IsValid(EconomicAidRequestInput input) =>
        new EconomicAidRequestInputValidator().Validate(input).IsValid;

    // ── Eligibility by minimum seniority (D-08) ──────────────────────────────────

    [Fact]
    public void MeetsMinimumSeniority_NoThreshold_AlwaysTrue() =>
        Assert.True(EconomicAidRequestRules.MeetsMinimumSeniority(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), AsOf, null));

    [Fact]
    public void MeetsMinimumSeniority_ZeroThreshold_AlwaysTrue() =>
        Assert.True(EconomicAidRequestRules.MeetsMinimumSeniority(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), AsOf, 0));

    [Fact]
    public void MeetsMinimumSeniority_BelowThreshold_False() =>
        // Hired 5 months before AsOf, threshold 6 → not eligible.
        Assert.False(EconomicAidRequestRules.MeetsMinimumSeniority(new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc), AsOf, 6));

    [Fact]
    public void MeetsMinimumSeniority_AtThreshold_True() =>
        // Hired exactly 6 months before AsOf, threshold 6 → eligible (>=).
        Assert.True(EconomicAidRequestRules.MeetsMinimumSeniority(new DateTime(2025, 12, 28, 0, 0, 0, DateTimeKind.Utc), AsOf, 6));

    [Fact]
    public void MeetsMinimumSeniority_AboveThreshold_True() =>
        // Hired 7 months before AsOf, threshold 6 → eligible.
        Assert.True(EconomicAidRequestRules.MeetsMinimumSeniority(new DateTime(2025, 11, 28, 0, 0, 0, DateTimeKind.Utc), AsOf, 6));

    // ── Approved amount (D-05) ───────────────────────────────────────────────────

    [Fact]
    public void IsValidApprovedAmount_ApprovePositive_True() =>
        Assert.True(EconomicAidRequestRules.IsValidApprovedAmount(EconomicAidRequestStatuses.Aprobada, 50m));

    [Fact]
    public void IsValidApprovedAmount_ApproveZero_False() =>
        Assert.False(EconomicAidRequestRules.IsValidApprovedAmount(EconomicAidRequestStatuses.Aprobada, 0m));

    [Fact]
    public void IsValidApprovedAmount_ApproveNull_False() =>
        Assert.False(EconomicAidRequestRules.IsValidApprovedAmount(EconomicAidRequestStatuses.Aprobada, null));

    [Fact]
    public void IsValidApprovedAmount_RejectNull_True() =>
        Assert.True(EconomicAidRequestRules.IsValidApprovedAmount(EconomicAidRequestStatuses.Rechazada, null));

    // ── Resolution-target whitelist ──────────────────────────────────────────────

    [Fact]
    public void IsResolutionTarget_Approved_True() =>
        Assert.True(EconomicAidRequestRules.IsResolutionTarget("aprobada"));

    [Fact]
    public void IsResolutionTarget_Solicitada_False() =>
        Assert.False(EconomicAidRequestRules.IsResolutionTarget(EconomicAidRequestStatuses.Solicitada));

    [Fact]
    public void IsResolutionTarget_Desembolsada_False() =>
        Assert.False(EconomicAidRequestRules.IsResolutionTarget(EconomicAidRequestStatuses.Desembolsada));

    [Fact]
    public void IsResolutionTarget_Null_False() =>
        Assert.False(EconomicAidRequestRules.IsResolutionTarget(null));

    // ── Derived response time ────────────────────────────────────────────────────

    [Fact]
    public void DeriveResponseTimeDays_NoResolution_ReturnsNull() =>
        Assert.Null(PersonnelFileEconomicAidRequest.DeriveResponseTimeDays(AsOf, null));

    [Fact]
    public void DeriveResponseTimeDays_FiveDaysLater_ReturnsFive() =>
        Assert.Equal(5, PersonnelFileEconomicAidRequest.DeriveResponseTimeDays(
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void DeriveResponseTimeDays_ResolutionBeforeRequest_ReturnsNull() =>
        Assert.Null(PersonnelFileEconomicAidRequest.DeriveResponseTimeDays(
            new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));

    // ── Domain transitions (D-03/D-09/D-11) ──────────────────────────────────────

    [Fact]
    public void Create_StartsInSolicitada()
    {
        var request = NewRequest();
        Assert.Equal(EconomicAidRequestStatuses.Solicitada, request.RequestStatusCode);
        Assert.Null(request.ApprovedAmount);
        Assert.Null(request.ResolvedByUserId);
    }

    [Fact]
    public void Resolve_Approve_SetsApprovedFieldsAndStatus()
    {
        var request = NewRequest();
        var hr = Guid.NewGuid();
        request.Resolve(EconomicAidRequestStatuses.Aprobada, 300m, hr, AsOf, "Aprobado parcial");

        Assert.Equal(EconomicAidRequestStatuses.Aprobada, request.RequestStatusCode);
        Assert.Equal(300m, request.ApprovedAmount);
        Assert.Equal(hr, request.ResolvedByUserId);
        Assert.NotNull(request.ResolutionDateUtc);
        Assert.NotNull(request.ResponseTimeDays);
    }

    [Fact]
    public void Resolve_Reject_LeavesApprovedAmountNull()
    {
        var request = NewRequest();
        request.Resolve(EconomicAidRequestStatuses.Rechazada, null, Guid.NewGuid(), AsOf, "No aplica");
        Assert.Equal(EconomicAidRequestStatuses.Rechazada, request.RequestStatusCode);
        Assert.Null(request.ApprovedAmount);
        Assert.NotNull(request.ResolvedByUserId);
    }

    [Fact]
    public void Resolve_Intermediate_DoesNotSetResolver()
    {
        var request = NewRequest();
        request.Resolve(EconomicAidRequestStatuses.PendienteDocumentacion, null, Guid.NewGuid(), AsOf, "Falta acta");
        Assert.Equal(EconomicAidRequestStatuses.PendienteDocumentacion, request.RequestStatusCode);
        Assert.Null(request.ResolvedByUserId);
        Assert.Null(request.ResolutionDateUtc);
    }

    [Fact]
    public void Resolve_ApproveWithZeroAmount_Throws()
    {
        var request = NewRequest();
        Assert.Throws<InvalidOperationException>(() =>
            request.Resolve(EconomicAidRequestStatuses.Aprobada, 0m, Guid.NewGuid(), AsOf, null));
    }

    [Fact]
    public void Resolve_AlreadyResolved_Throws()
    {
        var request = NewRequest();
        request.Resolve(EconomicAidRequestStatuses.Aprobada, 100m, Guid.NewGuid(), AsOf, null);
        Assert.Throws<InvalidOperationException>(() =>
            request.Resolve(EconomicAidRequestStatuses.Rechazada, null, Guid.NewGuid(), AsOf, null));
    }

    [Fact]
    public void Disburse_FromApproved_SetsDisbursedStatus()
    {
        var request = NewRequest();
        request.Resolve(EconomicAidRequestStatuses.Aprobada, 300m, Guid.NewGuid(), AsOf, null);
        request.Disburse(300m, AsOf, "TRANSFERENCIA");

        Assert.Equal(EconomicAidRequestStatuses.Desembolsada, request.RequestStatusCode);
        Assert.Equal(300m, request.DisbursedAmount);
        Assert.NotNull(request.DisbursementDateUtc);
    }

    [Fact]
    public void Disburse_NotApproved_Throws()
    {
        var request = NewRequest();
        Assert.Throws<InvalidOperationException>(() => request.Disburse(100m, AsOf, null));
    }

    [Fact]
    public void Cancel_FromPending_SetsAnulada()
    {
        var request = NewRequest();
        request.Cancel();
        Assert.Equal(EconomicAidRequestStatuses.Anulada, request.RequestStatusCode);
    }

    [Fact]
    public void Cancel_AfterResolved_Throws()
    {
        var request = NewRequest();
        request.Resolve(EconomicAidRequestStatuses.Aprobada, 100m, Guid.NewGuid(), AsOf, null);
        Assert.Throws<InvalidOperationException>(request.Cancel);
    }

    // ── Input validator ──────────────────────────────────────────────────────────

    [Fact]
    public void Validator_ValidInput_Passes() => Assert.True(IsValid(ValidInput()));

    [Fact]
    public void Validator_EmptyType_Fails() =>
        Assert.False(IsValid(ValidInput() with { TypeCode = "" }));

    [Fact]
    public void Validator_EmptyDescription_Fails() =>
        Assert.False(IsValid(ValidInput() with { Description = "" }));

    [Fact]
    public void Validator_ZeroRequestedAmount_Fails() =>
        Assert.False(IsValid(ValidInput() with { RequestedAmount = 0m }));

    [Fact]
    public void Validator_CurrencyWrongLength_Fails() =>
        Assert.False(IsValid(ValidInput() with { CurrencyCode = "DOLLAR" }));

    [Fact]
    public void Validator_OmittedCurrency_Passes() =>
        // Currency is optional in the input; the handler resolves the company default.
        Assert.True(IsValid(ValidInput() with { CurrencyCode = null }));

    [Fact]
    public void Validator_FutureRequestDate_Fails() =>
        Assert.False(IsValid(ValidInput() with { RequestDateUtc = DateTime.UtcNow.Date.AddDays(10) }));

    // ── Resolve command validator ────────────────────────────────────────────────

    [Fact]
    public void ResolveValidator_ApprovedZero_Fails()
    {
        var command = new ResolveEconomicAidRequestCommand(Guid.NewGuid(), Guid.NewGuid(), EconomicAidRequestStatuses.Aprobada, 0m, null, Guid.NewGuid());
        Assert.False(new ResolveEconomicAidRequestCommandValidator().Validate(command).IsValid);
    }

    [Fact]
    public void ResolveValidator_EmptyTarget_Fails()
    {
        var command = new ResolveEconomicAidRequestCommand(Guid.NewGuid(), Guid.NewGuid(), "", 100m, null, Guid.NewGuid());
        Assert.False(new ResolveEconomicAidRequestCommandValidator().Validate(command).IsValid);
    }

    [Fact]
    public void ResolveValidator_ValidApproval_Passes()
    {
        var command = new ResolveEconomicAidRequestCommand(Guid.NewGuid(), Guid.NewGuid(), EconomicAidRequestStatuses.Aprobada, 100m, "ok", Guid.NewGuid());
        Assert.True(new ResolveEconomicAidRequestCommandValidator().Validate(command).IsValid);
    }
}
