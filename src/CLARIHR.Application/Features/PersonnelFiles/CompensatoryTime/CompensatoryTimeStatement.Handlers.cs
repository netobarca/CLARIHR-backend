using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Estado de cuenta of the compensatory-time fund (REQ-002 §3.9): a paginated, filterable list of credit +
/// absence movements with a running balance plus the fund totals. Read gate is <c>ViewCompensatoryTime</c> OR
/// the owner employee (self-service). The running balance is computed over the WHOLE filtered set so a page's
/// balance already carries the accumulated offset (R-T9); with no filters the balance equals the profile's
/// <c>compensatoryTimeHoursAvailable</c> by construction.
/// </summary>
internal sealed class GetCompensatoryTimeStatementQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCompensatoryTimeStatementQuery, CompensatoryTimeStatementResponse>
{
    public async Task<Result<CompensatoryTimeStatementResponse>> Handle(
        GetCompensatoryTimeStatementQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensatoryTimeReadAsync<CompensatoryTimeStatementResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var page = await compensatoryTimeRepository.GetStatementPageAsync(
            personnelFile!.Id,
            query.FromDate,
            query.ToDate,
            query.CompensatoryTimeTypePublicId,
            query.StatusCode,
            query.IncludeAnnulled,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<CompensatoryTimeStatementResponse>.Success(
            new CompensatoryTimeStatementResponse(
                page.Items,
                query.PageNumber,
                query.PageSize,
                page.TotalCount,
                page.TotalCredited,
                page.TotalDebited,
                page.AvailableBalance));
    }
}
