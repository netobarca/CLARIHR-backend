using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.System.GetApiStatus;

internal sealed class GetApiStatusQueryHandler(
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService)
    : IQueryHandler<GetApiStatusQuery, ApiStatusResponse>
{
    public Task<Result<ApiStatusResponse>> Handle(GetApiStatusQuery query, CancellationToken cancellationToken)
    {
        var response = new ApiStatusResponse(
            ApplicationName: "CLARIHR API",
            UtcNow: dateTimeProvider.UtcNow,
            TenantId: tenantContext.TenantId,
            UserId: currentUserService.UserId,
            IsAuthenticated: currentUserService.IsAuthenticated);

        return Task.FromResult(Result<ApiStatusResponse>.Success(response));
    }
}
