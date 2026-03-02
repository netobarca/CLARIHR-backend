using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Abstractions.Locations;

public interface ILocationDependencyPolicy
{
    Task<Result> CanInactivateLocationGroupAsync(Guid locationGroupId, CancellationToken cancellationToken);

    Task<Result> CanInactivateWorkCenterTypeAsync(Guid workCenterTypeId, CancellationToken cancellationToken);

    Task<Result> CanInactivateWorkCenterAsync(Guid workCenterId, CancellationToken cancellationToken);
}
