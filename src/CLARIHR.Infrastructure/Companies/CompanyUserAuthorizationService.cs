using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CompanyUserAuthorizationService(
    IRbacAuthorizationService rbacAuthorizationService) : ICompanyUserAuthorizationService
{
    public Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) =>
        rbacAuthorizationService.AuthorizeAsync(CompanyUserFieldKeys.ResourceKey, RbacPermissionAction.Update, cancellationToken);

    public Task<Result> EnsureAuthorizedAsync(RbacPermissionAction action, CancellationToken cancellationToken) =>
        rbacAuthorizationService.AuthorizeAsync(CompanyUserFieldKeys.ResourceKey, action, cancellationToken);
}
