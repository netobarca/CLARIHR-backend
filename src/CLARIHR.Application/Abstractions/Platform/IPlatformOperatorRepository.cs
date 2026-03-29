using CLARIHR.Domain.Platform;

namespace CLARIHR.Application.Abstractions.Platform;

public interface IPlatformOperatorRepository
{
    void Add(PlatformOperator platformOperator);

    Task<PlatformOperator?> GetByUserIdAsync(long userId, CancellationToken cancellationToken);

    Task<PlatformOperator?> GetActiveByUserPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken);

    Task<bool> ExistsAnyAsync(CancellationToken cancellationToken);
}
