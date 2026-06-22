using CLARIHR.Application.Features.Compensation;
using CLARIHR.Domain.Compensation;

namespace CLARIHR.Application.Abstractions.Compensation;

public interface IIncomeTaxBracketRepository
{
    Task<IReadOnlyCollection<IncomeTaxBracketResponse>> GetBracketsAsync(
        Guid tenantId,
        string? payPeriodCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces (deletes + re-adds) the brackets of one pay period for the tenant. The bracket table is
    /// edited as a whole set, not per-row. Registered on the unit of work; the caller commits.
    /// </summary>
    Task ReplaceBracketsForPeriodAsync(
        Guid tenantId,
        string payPeriodCode,
        IReadOnlyCollection<IncomeTaxWithholdingBracket> brackets,
        CancellationToken cancellationToken);
}
