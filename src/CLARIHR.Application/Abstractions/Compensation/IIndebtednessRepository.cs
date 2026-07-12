using CLARIHR.Application.Features.Compensation;
using CLARIHR.Domain.Compensation;

namespace CLARIHR.Application.Abstractions.Compensation;

/// <summary>
/// The company's indebtedness PARAMETERS (REQ-010 D-16). The per-type ceilings are edited as a whole set (like the
/// income-tax brackets), not row by row.
/// </summary>
public interface IIndebtednessRepository
{
    Task<IReadOnlyCollection<IndebtednessLimitResponse>> GetLimitsAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces (deletes + re-adds) the ACTIVE per-type ceilings of the tenant. Registered on the unit of work;
    /// the caller commits.
    /// </summary>
    Task ReplaceLimitsAsync(
        Guid tenantId,
        IReadOnlyCollection<IndebtednessLimit> limits,
        CancellationToken cancellationToken);
}
