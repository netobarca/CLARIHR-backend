using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>Field-level validation of the incapacity commands (400 before any handler work).</summary>
public sealed class IncapacityValidatorsTests
{
    private static IncapacityInput ValidInput(DateOnly? endDate = null) =>
        new(
            RiskPublicId: Guid.NewGuid(),
            IncapacityTypePublicId: Guid.NewGuid(),
            MedicalClinicPublicId: null,
            AssignedPositionPublicId: null,
            PayrollTypeCode: null,
            PayrollPeriodDefinitionPublicId: null,
            StartDate: new DateOnly(2026, 3, 2),
            EndDate: endDate ?? new DateOnly(2026, 3, 6),
            Notes: null,
            DocumentFilePublicId: null,
            DocumentTypeCatalogItemPublicId: null,
            DocumentObservations: null);

    [Fact]
    public void Add_WithValidInput_Passes()
    {
        var result = new AddPersonnelFileIncapacityCommandValidator()
            .Validate(new AddPersonnelFileIncapacityCommand(Guid.NewGuid(), ValidInput()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Add_WithEmptyRisk_Fails()
    {
        var input = ValidInput() with { RiskPublicId = Guid.Empty };
        var result = new AddPersonnelFileIncapacityCommandValidator()
            .Validate(new AddPersonnelFileIncapacityCommand(Guid.NewGuid(), input));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Add_WithEndBeforeStart_Fails()
    {
        var input = ValidInput(endDate: new DateOnly(2026, 3, 1));
        var result = new AddPersonnelFileIncapacityCommandValidator()
            .Validate(new AddPersonnelFileIncapacityCommand(Guid.NewGuid(), input));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Add_WithNullEndDate_Passes_OpenEndedIsAllowedAtValidationLayer()
    {
        // The risk-permits-indefinite rule is enforced in the handler (422), not the validator.
        var input = ValidInput() with { EndDate = null };
        var result = new AddPersonnelFileIncapacityCommandValidator()
            .Validate(new AddPersonnelFileIncapacityCommand(Guid.NewGuid(), input));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Annul_WithoutReason_Fails()
    {
        var result = new AnnulPersonnelFileIncapacityCommandValidator()
            .Validate(new AnnulPersonnelFileIncapacityCommand(Guid.NewGuid(), Guid.NewGuid(), string.Empty, Guid.NewGuid()));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Extension_WithoutEndDate_Fails()
    {
        var input = new IncapacityExtensionInput(
            RiskPublicId: Guid.NewGuid(),
            IncapacityTypePublicId: Guid.NewGuid(),
            MedicalClinicPublicId: null,
            AssignedPositionPublicId: null,
            PayrollTypeCode: null,
            PayrollPeriodDefinitionPublicId: null,
            EndDate: default,
            Notes: null,
            DocumentFilePublicId: null,
            DocumentTypeCatalogItemPublicId: null,
            DocumentObservations: null);

        var result = new AddPersonnelFileIncapacityExtensionCommandValidator()
            .Validate(new AddPersonnelFileIncapacityExtensionCommand(Guid.NewGuid(), Guid.NewGuid(), input));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Balance_WithOutOfRangeYear_Fails()
    {
        var result = new GetPersonnelFileIncapacityBalanceQueryValidator()
            .Validate(new GetPersonnelFileIncapacityBalanceQuery(Guid.NewGuid(), 1800));

        Assert.False(result.IsValid);
    }
}
