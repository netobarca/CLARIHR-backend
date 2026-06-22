using CLARIHR.Application.Abstractions.Compensation;
using CLARIHR.Application.Features.Compensation;
using CLARIHR.Domain.Compensation;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Compensation;

internal sealed class IncomeTaxBracketRepository(ApplicationDbContext dbContext) : IIncomeTaxBracketRepository
{
    public async Task<IReadOnlyCollection<IncomeTaxBracketResponse>> GetBracketsAsync(
        Guid tenantId,
        string? payPeriodCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.IncomeTaxWithholdingBrackets
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(payPeriodCode))
        {
            var normalized = payPeriodCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.PayPeriodCode == normalized);
        }

        return await query
            .OrderBy(item => item.PayPeriodCode)
            .ThenBy(item => item.BracketOrder)
            .Select(item => new IncomeTaxBracketResponse(
                item.PublicId,
                item.PayPeriodCode,
                item.BracketOrder,
                item.LowerBound,
                item.UpperBound,
                item.FixedFee,
                item.RatePercent,
                item.ExcessOver,
                item.EffectiveFromUtc,
                item.EffectiveToUtc,
                item.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task ReplaceBracketsForPeriodAsync(
        Guid tenantId,
        string payPeriodCode,
        IReadOnlyCollection<IncomeTaxWithholdingBracket> brackets,
        CancellationToken cancellationToken)
    {
        var normalized = payPeriodCode.Trim().ToUpperInvariant();
        var existing = await dbContext.IncomeTaxWithholdingBrackets
            .Where(item => item.TenantId == tenantId && item.PayPeriodCode == normalized)
            .ToArrayAsync(cancellationToken);

        dbContext.IncomeTaxWithholdingBrackets.RemoveRange(existing);

        foreach (var bracket in brackets)
        {
            dbContext.IncomeTaxWithholdingBrackets.Add(bracket);
        }
    }
}
