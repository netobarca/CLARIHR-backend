using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class IamAdministrationAuthorizationService(
    IRbacAuthorizationService rbacAuthorizationService) : IIamAdministrationAuthorizationService
{
    public Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) =>
        rbacAuthorizationService.AuthorizeAsync(PermissionMatrixCatalog.Get(RbacPermissionScreen.Permissions).ResourceKey, RbacPermissionAction.Update, cancellationToken);

    public Task<Result> EnsureAuthorizedAsync(
        RbacPermissionScreen screen,
        RbacPermissionAction action,
        CancellationToken cancellationToken) =>
        rbacAuthorizationService.AuthorizeAsync(PermissionMatrixCatalog.Get(screen).ResourceKey, action, cancellationToken);
}
