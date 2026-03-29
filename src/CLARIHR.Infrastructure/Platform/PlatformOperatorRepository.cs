using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Platform;

internal sealed class PlatformOperatorRepository(ApplicationDbContext dbContext) : IPlatformOperatorRepository
{
    public void Add(PlatformOperator platformOperator) => dbContext.PlatformOperators.Add(platformOperator);

    public Task<PlatformOperator?> GetByUserIdAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.PlatformOperators
            .SingleOrDefaultAsync(platformOperator => platformOperator.UserId == userId, cancellationToken);

    public Task<PlatformOperator?> GetActiveByUserPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
        (from platformOperator in dbContext.PlatformOperators
         join user in dbContext.AuthUsers on platformOperator.UserId equals user.Id
         where platformOperator.IsActive && user.PublicId == userPublicId
         select platformOperator)
        .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAnyAsync(CancellationToken cancellationToken) =>
        dbContext.PlatformOperators.AnyAsync(cancellationToken);
}
