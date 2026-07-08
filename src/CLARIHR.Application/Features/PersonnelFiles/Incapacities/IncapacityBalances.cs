using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Employer-cap balance of one employee for one calendar year (D-27). The SAME pure rule
/// (<see cref="IncapacityBalanceRules"/>) feeds this endpoint AND the profile's
/// <c>DisabilityDaysAvailable</c>, so the two figures cuadran by construction (§3.10).
/// </summary>
public sealed record PersonnelFileIncapacityBalanceResponse(
    Guid PersonnelFilePublicId,
    int Year,
    int EmployerCoveredDays,
    int AdditionalBenefitDays,
    int TotalCapDays,
    int ConsumedEmployerDays,
    int RemainingDays)
{
    [JsonIgnore]
    public Guid Id => PersonnelFilePublicId;
}

/// <summary>
/// Pure, unit-testable employer-cap arithmetic (D-27): the yearly cap is the covered days plus the additional
/// benefit days; only REGISTRADA incapacities consume it (the caller supplies the already-aggregated employer
/// days). The remaining days — which is exactly the profile's <c>DisabilityDaysAvailable</c> — is the cap minus
/// the consumption, floored at zero.
/// </summary>
public static class IncapacityBalanceRules
{
    /// <summary>Legal defaults of the employer cap (D-27): 9 covered days + 0 benefit days per year.</summary>
    public const int DefaultEmployerCoveredDaysPerYear = 9;
    public const int DefaultAdditionalBenefitDaysPerYear = 0;

    public readonly record struct IncapacityBalance(
        int EmployerCoveredDays,
        int AdditionalBenefitDays,
        int TotalCapDays,
        int ConsumedEmployerDays,
        int RemainingDays);

    /// <summary>
    /// Resolves the balance for the given (nullable) preference figures and the year's consumed employer days.
    /// A null preference figure falls back to the legal default (9 / 0).
    /// </summary>
    public static IncapacityBalance Compute(
        int? employerCoveredDays,
        int? additionalBenefitDays,
        int consumedEmployerDays)
    {
        var covered = employerCoveredDays ?? DefaultEmployerCoveredDaysPerYear;
        var benefit = additionalBenefitDays ?? DefaultAdditionalBenefitDaysPerYear;
        var total = covered + benefit;
        var consumed = Math.Max(0, consumedEmployerDays);
        var remaining = Math.Max(0, total - consumed);
        return new IncapacityBalance(covered, benefit, total, consumed, remaining);
    }
}

public sealed record GetPersonnelFileIncapacityBalanceQuery(Guid PersonnelFileId, int? Year)
    : IQuery<PersonnelFileIncapacityBalanceResponse>;

internal sealed class GetPersonnelFileIncapacityBalanceQueryValidator : AbstractValidator<GetPersonnelFileIncapacityBalanceQuery>
{
    public GetPersonnelFileIncapacityBalanceQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.Year)
            .InclusiveBetween(2000, 2100)
            .When(query => query.Year.HasValue);
    }
}
