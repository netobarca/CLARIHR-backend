using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.CompanyUsers;

public sealed record GetCompanyUserQuery(Guid UserId) : IQuery<CompanyUserResponse>;

internal sealed class GetCompanyUserQueryValidator : AbstractValidator<GetCompanyUserQuery>
{
    public GetCompanyUserQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}

internal sealed class GetCompanyUserQueryHandler(
    IUserCompanyRepository userCompanyRepository,
    ICompanyUserAuthorizationService authorizationService,
    ITenantContext tenantContext,
    IFieldPermissionService fieldPermissionService,
    IFieldSerializationService fieldSerializationService)
    : IQueryHandler<GetCompanyUserQuery, CompanyUserResponse>
{
    public async Task<Result<CompanyUserResponse>> Handle(
        GetCompanyUserQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(RbacPermissionAction.Read, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyUserResponse>.Failure(authorizationResult.Error);
        }

        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.TenantContextRequired);
        }

        var response = await userCompanyRepository.GetUserAsync(tenantContext.TenantId.Value, query.UserId, cancellationToken);
        if (response is null)
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.UserNotFound);
        }

        var fieldAccessResult = await fieldPermissionService.GetCurrentUserAccessProfileAsync(
            CompanyUserFieldKeys.ResourceKey,
            cancellationToken);
        if (fieldAccessResult.IsFailure)
        {
            return Result<CompanyUserResponse>.Failure(fieldAccessResult.Error);
        }

        // Compute the weak ETag from the UNFILTERED projection so it is independent of the caller's
        // field-level visibility, then carry it on the filtered response for the `ETag` header.
        var etag = CompanyUserETag.Compute(response);
        return Result<CompanyUserResponse>.Success(
            CompanyUserManagementHelpers.Filter(response, fieldAccessResult.Value, fieldSerializationService) with { WeakETag = etag });
    }
}
