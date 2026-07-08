using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Mass generation options (leave module §3.6): one vacation fund period per active employee for
/// <see cref="Year"/>. The grants and anniversary flag default to the company preference; an optional
/// <see cref="EmployeeIds"/> filter restricts the run to specific employees. The run is idempotent by
/// (employee, year) — a re-run creates nothing for employees that already have an active period.
/// </summary>
public sealed record VacationPeriodGenerationInput(
    int Year,
    bool? UseAnniversary,
    int? LegalDaysGranted,
    int? BenefitDaysGranted,
    bool? GeneratesEnjoymentDays,
    IReadOnlyCollection<Guid>? EmployeeIds);

public sealed record GenerateVacationPeriodsCommand(Guid CompanyId, VacationPeriodGenerationInput Item)
    : ICommand<VacationPeriodGenerationSummary>;

/// <summary>One per-employee failure of the mass generation (e.g. Art. 177 eligibility not met).</summary>
public sealed record VacationPeriodGenerationError(
    Guid EmployeePublicId,
    string? EmployeeFullName,
    string Code,
    string Message);

/// <summary>
/// Summary of a mass generation run: how many periods were created, how many employees were skipped (already had
/// an active period for the year — idempotency) and the per-row errors (eligibility, etc.).
/// </summary>
public sealed record VacationPeriodGenerationSummary(
    int Year,
    int TotalEmployees,
    int Created,
    int Skipped,
    IReadOnlyCollection<VacationPeriodGenerationError> Errors);

internal sealed class VacationPeriodGenerationInputValidator : AbstractValidator<VacationPeriodGenerationInput>
{
    public VacationPeriodGenerationInputValidator()
    {
        RuleFor(input => input.Year).InclusiveBetween(2000, 2100);
        RuleFor(input => input.LegalDaysGranted).GreaterThan(0).When(input => input.LegalDaysGranted.HasValue);
        RuleFor(input => input.BenefitDaysGranted).GreaterThanOrEqualTo(0).When(input => input.BenefitDaysGranted.HasValue);
    }
}

internal sealed class GenerateVacationPeriodsCommandValidator : AbstractValidator<GenerateVacationPeriodsCommand>
{
    public GenerateVacationPeriodsCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new VacationPeriodGenerationInputValidator());
    }
}
