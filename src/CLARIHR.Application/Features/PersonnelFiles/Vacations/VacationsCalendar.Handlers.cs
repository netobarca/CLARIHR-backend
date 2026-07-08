using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class GetVacationCalendarQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository)
    : IQueryHandler<GetVacationCalendarQuery, VacationCalendarResponse>
{
    public async Task<Result<VacationCalendarResponse>> Handle(
        GetVacationCalendarQuery query, CancellationToken cancellationToken)
    {
        // View gate enforced per handler (this GET is a read; ViewVacations covers the company calendar).
        var authorizationResult = await authorizationService.EnsureCanViewVacationsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<VacationCalendarResponse>.Failure(authorizationResult.Error);
        }

        var calendar = await vacationRepository.GetCalendarAsync(query.CompanyId, query.Year, cancellationToken);
        return Result<VacationCalendarResponse>.Success(calendar);
    }
}
