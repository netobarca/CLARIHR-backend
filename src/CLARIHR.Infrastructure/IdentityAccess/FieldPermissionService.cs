using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class FieldPermissionService(
    IFieldAccessProfileService fieldAccessProfileService) : IFieldPermissionService
{
    public Task<Result<FieldAccessProfile>> GetCurrentUserAccessProfileAsync(
        string resourceKey,
        CancellationToken cancellationToken) =>
        fieldAccessProfileService.GetCurrentUserAccessProfileAsync(resourceKey, cancellationToken);
}
