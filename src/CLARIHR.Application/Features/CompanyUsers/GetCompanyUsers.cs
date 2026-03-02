using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.CompanyUsers;

internal sealed class GetCompanyUsersQueryHandler(
    IUserCompanyRepository userCompanyRepository,
    ICompanyUserAuthorizationService authorizationService,
    ITenantContext tenantContext,
    IFieldPermissionService fieldPermissionService,
    IFieldSerializationService fieldSerializationService)
    : IQueryHandler<GetCompanyUsersQuery, PagedResponse<CompanyUserSummaryResponse>>
{
    public async Task<Result<PagedResponse<CompanyUserSummaryResponse>>> Handle(
        GetCompanyUsersQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(RbacPermissionAction.Read, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CompanyUserSummaryResponse>>.Failure(authorizationResult.Error);
        }

        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PagedResponse<CompanyUserSummaryResponse>>.Failure(CompanyUserErrors.TenantContextRequired);
        }

        var users = await userCompanyRepository.GetUsersAsync(
            tenantContext.TenantId.Value,
            query.Page,
            query.PageSize,
            query.Status,
            query.RoleId,
            query.Search,
            cancellationToken);

        var fieldAccessResult = await fieldPermissionService.GetCurrentUserAccessProfileAsync(
            CompanyUserFieldKeys.ResourceKey,
            cancellationToken);
        if (fieldAccessResult.IsFailure)
        {
            return Result<PagedResponse<CompanyUserSummaryResponse>>.Failure(fieldAccessResult.Error);
        }

        return Result<PagedResponse<CompanyUserSummaryResponse>>.Success(
            CompanyUserManagementHelpers.Filter(users, fieldAccessResult.Value, fieldSerializationService));
    }
}
