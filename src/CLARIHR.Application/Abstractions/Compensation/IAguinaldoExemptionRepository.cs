using CLARIHR.Application.Features.Compensation;
using CLARIHR.Domain.Compensation;

namespace CLARIHR.Application.Abstractions.Compensation;

/// <summary>
/// Acceso a la exención de Renta del aguinaldo por año (molde <see cref="IIncomeTaxBracketRepository"/>: el
/// parámetro legal se edita como CONJUNTO, no fila por fila).
/// </summary>
public interface IAguinaldoExemptionRepository
{
    Task<IReadOnlyCollection<AguinaldoExemptionResponse>> GetExemptionsAsync(
        Guid tenantId,
        int? year,
        CancellationToken cancellationToken);

    Task ReplaceExemptionsAsync(
        Guid tenantId,
        IReadOnlyCollection<AguinaldoExemption> exemptions,
        CancellationToken cancellationToken);
}
