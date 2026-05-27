using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical payroll-transaction JSON Patch surface: the pure
/// <see cref="PersonnelFilePayrollTransactionPatchApplier"/> and the
/// <see cref="PersonnelFilePayrollTransactionPatchState"/> projection. A payroll transaction is an
/// immutable audit record, so the only patchable field is the <c>isActive</c> flag (replacing the
/// former dedicated <c>/deactivate</c> endpoint); every business-field path must be rejected.
/// </summary>
public sealed class PersonnelFilePayrollTransactionPatchTests
{
    private static PersonnelFilePayrollTransactionResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "SALARY",
            new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            "2026-01",
            "January payroll",
            2500.00m,
            "USD",
            IsDebit: false,
            "PAYROLL-SYS",
            "REF-001",
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedAtUtc: null,
            IsActive: true,
            Guid.NewGuid());

    private static PersonnelFilePayrollTransactionPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFilePayrollTransactionPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsIsActive()
    {
        var state = PersonnelFilePayrollTransactionPatchState.From(Baseline());

        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFilePayrollTransactionPatchState.From(Baseline());

        Assert.True(PersonnelFilePayrollTransactionPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFilePayrollTransactionPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFilePayrollTransactionPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFilePayrollTransactionPatchState.From(Baseline());

        Assert.True(PersonnelFilePayrollTransactionPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFilePayrollTransactionPatchState.From(Baseline());

        Assert.True(PersonnelFilePayrollTransactionPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedBusinessPath_Fails()
    {
        var state = PersonnelFilePayrollTransactionPatchState.From(Baseline());

        Assert.True(PersonnelFilePayrollTransactionPatchApplier.Apply([Replace("/amount", 999m)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFilePayrollTransactionPatchState.From(Baseline());

        Assert.True(PersonnelFilePayrollTransactionPatchApplier.Apply(
            [new PersonnelFilePayrollTransactionPatchOperation("copy", "/isActive", "/isActive", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFilePayrollTransactionPatchState.From(Baseline());

        Assert.True(PersonnelFilePayrollTransactionPatchApplier.Apply([Replace("/isActive/0", true)], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFilePayrollTransactionPatchState.From(Baseline());

        Assert.True(PersonnelFilePayrollTransactionPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_AlwaysSucceeds()
    {
        Assert.True(PersonnelFilePayrollTransactionPatchApplier.Validate(PersonnelFilePayrollTransactionPatchState.From(Baseline())).IsSuccess);
    }
}
