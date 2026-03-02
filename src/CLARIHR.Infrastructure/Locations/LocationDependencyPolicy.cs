using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Locations.Common;

namespace CLARIHR.Infrastructure.Locations;

internal sealed class LocationDependencyPolicy(
    ILocationGroupRepository locationGroupRepository,
    IWorkCenterTypeRepository workCenterTypeRepository) : ILocationDependencyPolicy
{
    public async Task<Result> CanInactivateLocationGroupAsync(Guid locationGroupId, CancellationToken cancellationToken)
    {
        var group = await locationGroupRepository.GetByIdAsync(locationGroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(LocationErrors.GroupNotFound);
        }

        if (await locationGroupRepository.HasActiveChildrenAsync(group.Id, cancellationToken))
        {
            return Result.Failure(LocationErrors.GroupHasActiveChildren);
        }

        if (await locationGroupRepository.HasActiveWorkCentersAsync(group.Id, cancellationToken))
        {
            return Result.Failure(LocationErrors.GroupHasActiveWorkCenters);
        }

        return Result.Success();
    }

    public async Task<Result> CanInactivateWorkCenterTypeAsync(Guid workCenterTypeId, CancellationToken cancellationToken)
    {
        var workCenterType = await workCenterTypeRepository.GetByIdAsync(workCenterTypeId, cancellationToken);
        if (workCenterType is null)
        {
            return Result.Failure(LocationErrors.WorkCenterTypeNotFound);
        }

        if (await workCenterTypeRepository.HasActiveWorkCentersAsync(workCenterType.Id, cancellationToken))
        {
            return Result.Failure(LocationErrors.WorkCenterTypeInUse);
        }

        return Result.Success();
    }

    public Task<Result> CanInactivateWorkCenterAsync(Guid workCenterId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
