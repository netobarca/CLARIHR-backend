using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.IdentityAccess;

public interface IRbacAuthorizationService
{
    Task<Result> AuthorizeAsync(
        string resourceKey,
        RbacPermissionAction action,
        CancellationToken cancellationToken);

    Task<Result> AuthorizeFieldsAsync(
        string resourceKey,
        RbacPermissionAction action,
        IReadOnlyCollection<string> fieldKeys,
        CancellationToken cancellationToken);
}
