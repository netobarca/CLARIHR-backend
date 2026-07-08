using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>One daily-permit schedule inside a lactation period (date range → permits per day × minutes per permit).</summary>
public sealed record LactationScheduleRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int DailyPermitsCount,
    int MinutesPerPermit);

/// <summary>
/// Body for registering or editing a lactation period ("periodo de lactancia"). <c>IncapacityTypePublicId</c>
/// must reference the active LACTANCIA incapacity type. The full <c>Schedules</c> set travels with every request
/// and is replaced atomically (the PUT reemplaza datos + horarios). HR-only (D-18).
/// </summary>
public sealed record LactationPeriodRequest(
    Guid IncapacityTypePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes,
    IReadOnlyList<LactationScheduleRequest> Schedules)
{
    public LactationPeriodInput ToInput() =>
        new(
            IncapacityTypePublicId,
            StartDate,
            EndDate,
            Notes,
            (Schedules ?? [])
                .Select(schedule => new LactationScheduleInputDto(
                    schedule.StartDate, schedule.EndDate, schedule.DailyPermitsCount, schedule.MinutesPerPermit))
                .ToArray());
}

/// <summary>Body for annulling a lactation period; the reason is mandatory.</summary>
public sealed record AnnulLactationPeriodRequest(string Reason);
