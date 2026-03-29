using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Abstractions.Platform;

public interface IPlatformAuthorizationService
{
    Task<Result> EnsureCanReadAsync(CancellationToken cancellationToken);

    Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken);
}
