using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical payment-method JSON Patch surface: the pure
/// <see cref="PersonnelFilePaymentMethodPatchApplier"/> and the
/// <see cref="PersonnelFilePaymentMethodPatchState"/> projection. Like the salary item, the
/// payment method's <c>isActive</c> flag is patchable (replacing the former dedicated
/// <c>/deactivate</c> endpoint), so the applier must accept boolean values and flag the mutation
/// while preserving the business-field validation the Add/Update commands run.
/// </summary>
public sealed class PersonnelFilePaymentMethodPatchTests
{
    private static PersonnelFilePaymentMethodResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "BANK_TRANSFER",
            Guid.NewGuid(),
            true,
            true,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            "Primary salary account.",
            Guid.NewGuid());

    private static PersonnelFilePaymentMethodPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFilePaymentMethodPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var baseline = Baseline();
        var state = PersonnelFilePaymentMethodPatchState.From(baseline);

        Assert.Equal("BANK_TRANSFER", state.PaymentMethodCode);
        Assert.Equal(baseline.BankAccountId, state.BankAccountPublicId);
        Assert.True(state.IsPrimary);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var baseline = Baseline();
        var input = PersonnelFilePaymentMethodPatchState.From(baseline).ToInput();

        Assert.Equal("BANK_TRANSFER", input.PaymentMethodCode);
        Assert.Equal(baseline.BankAccountId, input.BankAccountPublicId);
        Assert.True(input.IsPrimary);
        Assert.True(input.IsActive);
    }

    [Fact]
    public void Apply_ReplacePaymentMethodCode_Mutates()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/paymentMethodCode", "CHECK")], state).IsSuccess);
        Assert.Equal("CHECK", state.PaymentMethodCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceBankAccountPublicId_AcceptsGuid()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());
        var bankAccountId = Guid.NewGuid();

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/bankAccountPublicId", bankAccountId)], state).IsSuccess);
        Assert.Equal(bankAccountId, state.BankAccountPublicId);
    }

    [Fact]
    public void Apply_ReplaceIsPrimary_AcceptsBoolean()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/isPrimary", false)], state).IsSuccess);
        Assert.False(state.IsPrimary);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/paymentMethodCode", "WIRE")], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveEffectiveToUtc_ClearsValue()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Remove("/effectiveToUtc")], state).IsSuccess);
        Assert.Null(state.EffectiveToUtc);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalBankAccountPublicId_ClearsValue()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Remove("/bankAccountPublicId")], state).IsSuccess);
        Assert.Null(state.BankAccountPublicId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalNotes_ClearsValue()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Remove("/notes")], state).IsSuccess);
        Assert.Null(state.Notes);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveEffectiveFromUtc_Fails()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Remove("/effectiveFromUtc")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredPaymentMethodCode_FailsValidation()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Remove("/paymentMethodCode")], state).IsSuccess);
        Assert.True(PersonnelFilePaymentMethodPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonGuidForBankAccountPublicId_Fails()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/bankAccountPublicId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply(
            [new PersonnelFilePaymentMethodPatchOperation("copy", "/paymentMethodCode", "/paymentMethodCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([Replace("/paymentMethodCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFilePaymentMethodPatchApplier.Validate(PersonnelFilePaymentMethodPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankPaymentMethodCode_Fails()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());
        state.PaymentMethodCode = " ";

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_EffectiveFromAfterEffectiveTo_Fails()
    {
        var state = PersonnelFilePaymentMethodPatchState.From(Baseline());
        state.EffectiveFromUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(PersonnelFilePaymentMethodPatchApplier.Validate(state).IsFailure);
    }
}
