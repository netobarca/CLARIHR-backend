using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.IdentityAccess;

public interface IFieldAccessProfileService
{
    Task<Result<FieldAccessProfile>> GetCurrentUserAccessProfileAsync(
        string resourceKey,
        CancellationToken cancellationToken);
}
