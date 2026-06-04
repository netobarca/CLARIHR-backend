using System.Text.Json;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Domain.LegalRepresentatives;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the descriptive-only JSON Patch allow-list of
/// <see cref="LegalRepresentativePatchApplier"/> (the novel logic added when LegalRepresentatives
/// was aligned to the canonical RFC-6902 PATCH). Patchable = descriptive/contact fields with
/// independent validation. The legal identity (document), the effective-date range, the primary
/// flag and activation are NOT patchable here (uniqueness/range/state invariants → PUT /
/// /set-primary / activate-inactivate). Locks that boundary in.
/// </summary>
public sealed class LegalRepresentativePatchApplierTests
{
    private static LegalRepresentativePatchState NewState() =>
        LegalRepresentativePatchState.From(new LegalRepresentativeResponse(
            Id: Guid.NewGuid(),
            CompanyId: Guid.NewGuid(),
            FirstName: "Ana",
            LastName: "Lopez",
            FullName: "Ana Lopez",
            DocumentType: "DUI",
            DocumentNumber: "01234567-8",
            PositionTitle: "Gerente",
            RepresentationType: LegalRepresentativeRepresentationType.AlternateLegalRepresentative,
            AuthorityDescription: "Poder amplio",
            AppointmentInstrument: "Escritura 123",
            AppointmentDateUtc: DateTime.UtcNow,
            EffectiveFromUtc: DateTime.UtcNow,
            EffectiveToUtc: null,
            Email: "ana@acme.test",
            Phone: "2222-2222",
            IsPrimary: false,
            IsActive: true,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null));

    private static LegalRepresentativePatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceFirstName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/firstName", "Carla") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Carla", state.FirstName);
        Assert.True(LegalRepresentativePatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceRepresentationType_ParsesEnumCaseInsensitively()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/representationType", "attorneyinfact") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(LegalRepresentativeRepresentationType.AttorneyInFact, state.RepresentationType);
    }

    [Fact]
    public void Apply_InvalidRepresentationType_IsRejected()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/representationType", "NotAType") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ReplaceAppointmentDate_ParsesIso()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/appointmentDateUtc", "2024-01-15T00:00:00Z") }, state);

        Assert.True(result.IsSuccess);
        Assert.NotNull(state.AppointmentDateUtc);
    }

    [Fact]
    public void Apply_RemoveAppointmentDate_SetsNull()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("remove", "/appointmentDateUtc", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.Null(state.AppointmentDateUtc);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_IdentityAndRangeAndPrimaryAndActive_AreRejected()
    {
        var state = NewState();

        Assert.True(LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/documentNumber", "9") }, state).IsFailure);
        Assert.True(LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/documentType", "PAS") }, state).IsFailure);
        Assert.True(LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/effectiveFromUtc", "2024-01-01T00:00:00Z") }, state).IsFailure);
        Assert.True(LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/effectiveToUtc", "2024-12-31T00:00:00Z") }, state).IsFailure);
        Assert.True(LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/isPrimary", true) }, state).IsFailure);
        Assert.True(LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("move", "/firstName", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/firstName/sub", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/unknownField", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredFirstName_IsRejected()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(new[] { Op("remove", "/firstName", null) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_InvalidEmail_Fails()
    {
        var state = NewState();

        var apply = LegalRepresentativePatchApplier.Apply(new[] { Op("replace", "/email", "not-an-email") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(LegalRepresentativePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = LegalRepresentativePatchApplier.Apply(Array.Empty<LegalRepresentativePatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
