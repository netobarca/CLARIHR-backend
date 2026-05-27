using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Bank Account JSON Patch surface (PersonnelFileCompensation
/// remediation): the pure <see cref="PersonnelFileBankAccountPatchApplier"/> apply/validate
/// logic and the <see cref="PersonnelFileBankAccountPatchState"/> projection. Bank accounts have
/// no <c>isActive</c> flag; the patchable members are the business input fields only.
/// </summary>
public sealed class PersonnelFileBankAccountPatchTests
{
    private static readonly Guid SampleBankId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static PersonnelFileBankAccountResponse Baseline() =>
        new(
            Guid.NewGuid(),
            SampleBankId,
            "BAC",
            "Banco de America Central",
            "BAC",
            "BACSV01",
            "0110",
            "USD",
            "001-002-003",
            "CHECKING",
            true,
            Guid.NewGuid());

    private static PersonnelFileBankAccountPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileBankAccountPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.Equal(SampleBankId, state.BankPublicId);
        Assert.Equal("USD", state.CurrencyCode);
        Assert.Equal("001-002-003", state.AccountNumber);
        Assert.Equal("CHECKING", state.AccountTypeCode);
        Assert.True(state.IsPrimary);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileBankAccountPatchState.From(Baseline()).ToInput();

        Assert.Equal(SampleBankId, input.BankPublicId);
        Assert.Equal("USD", input.CurrencyCode);
        Assert.Equal("001-002-003", input.AccountNumber);
        Assert.Equal("CHECKING", input.AccountTypeCode);
        Assert.True(input.IsPrimary);
    }

    [Fact]
    public void Apply_ReplaceScalarField_Mutates()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Replace("/accountNumber", "999-888-777")], state).IsSuccess);
        Assert.Equal("999-888-777", state.AccountNumber);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceBankPublicId_ParsesGuidString()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());
        var newBankId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Replace("/bankPublicId", newBankId)], state).IsSuccess);
        Assert.Equal(newBankId, state.BankPublicId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsPrimary_Mutates()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Replace("/isPrimary", false)], state).IsSuccess);
        Assert.False(state.IsPrimary);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequired_FailsValidation()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        // Remove clears the value to empty, which the subsequent Validate rejects.
        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Remove("/accountNumber")], state).IsSuccess);
        Assert.True(PersonnelFileBankAccountPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveBankPublicId_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Remove("/bankPublicId")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsPrimary_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Remove("/isPrimary")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForCode_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Replace("/currencyCode", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonBooleanForIsPrimary_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Replace("/isPrimary", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonGuidForBankPublicId_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Replace("/bankPublicId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply(
            [new PersonnelFileBankAccountPatchOperation("copy", "/currencyCode", "/currencyCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Replace("/notARealField", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([Replace("/accountNumber/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());

        Assert.True(PersonnelFileBankAccountPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileBankAccountPatchApplier.Validate(PersonnelFileBankAccountPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankCurrencyCode_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());
        state.CurrencyCode = "  ";

        Assert.True(PersonnelFileBankAccountPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_EmptyBankPublicId_Fails()
    {
        var state = PersonnelFileBankAccountPatchState.From(Baseline());
        state.BankPublicId = Guid.Empty;

        Assert.True(PersonnelFileBankAccountPatchApplier.Validate(state).IsFailure);
    }
}
