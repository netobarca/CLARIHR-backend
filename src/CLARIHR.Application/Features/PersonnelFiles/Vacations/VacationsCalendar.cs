using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// One enjoyed vacation window in the calendar (leave module §3.7): an approved / partially-returned / returned
/// request whose date range falls in the requested year.
/// </summary>
public sealed record VacationCalendarEnjoymentEntry(
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    Guid VacationRequestPublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    int RequestedDays,
    string StatusCode);

/// <summary>One planned vacation window in the calendar: a line of a VIGENTE plan of the requested year.</summary>
public sealed record VacationCalendarPlanEntry(
    Guid PersonnelFilePublicId,
    string? EmployeeFullName,
    Guid VacationPlanPublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days);

/// <summary>
/// The company vacation calendar of one year (leave module §3.7): the enjoyed windows (from approved requests)
/// and the planned windows (from VIGENTE plans), each carrying the employee and the dates/days.
/// </summary>
public sealed record VacationCalendarResponse(
    int Year,
    IReadOnlyCollection<VacationCalendarEnjoymentEntry> Enjoyments,
    IReadOnlyCollection<VacationCalendarPlanEntry> PlannedLines);

public sealed record GetVacationCalendarQuery(Guid CompanyId, int Year)
    : IQuery<VacationCalendarResponse>;

internal sealed class GetVacationCalendarQueryValidator : AbstractValidator<GetVacationCalendarQuery>
{
    public GetVacationCalendarQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Year).InclusiveBetween(2000, 2100);
    }
}
