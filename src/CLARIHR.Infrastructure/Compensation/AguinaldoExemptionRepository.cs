using CLARIHR.Application.Abstractions.Compensation;
using CLARIHR.Application.Features.Compensation;
using CLARIHR.Domain.Compensation;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Compensation;

internal sealed class AguinaldoExemptionRepository(ApplicationDbContext dbContext) : IAguinaldoExemptionRepository
{
    public async Task<IReadOnlyCollection<AguinaldoExemptionResponse>> GetExemptionsAsync(
        Guid tenantId,
        int? year,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AguinaldoExemptions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        if (year.HasValue)
        {
            query = query.Where(item => item.Year == year.Value);
        }

        return await query
            .OrderByDescending(item => item.Year)
            .Select(item => new AguinaldoExemptionResponse(
                item.PublicId,
                item.Year,
                item.ExemptAmount,
                item.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Reconcilia por AÑO en vez de borrar todo y reinsertar. La diferencia no es cosmética: la tabla tiene un
    /// índice único <c>(tenant, year)</c> y EF no garantiza que el DELETE de la fila de 2026 se emita antes del
    /// INSERT de la nueva fila de 2026 — el borrado-y-reinserción reventaría con violación de unicidad justo en
    /// el caso más común, que es corregir el monto de un año ya registrado.
    /// </summary>
    public async Task ReplaceExemptionsAsync(
        Guid tenantId,
        IReadOnlyCollection<AguinaldoExemption> exemptions,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.AguinaldoExemptions
            .Where(item => item.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);

        var incomingYears = exemptions.Select(item => item.Year).ToHashSet();

        // Los años que ya no vienen en el conjunto se eliminan.
        dbContext.AguinaldoExemptions.RemoveRange(existing.Where(item => !incomingYears.Contains(item.Year)));

        var existingByYear = existing.ToDictionary(item => item.Year);
        foreach (var exemption in exemptions)
        {
            if (existingByYear.TryGetValue(exemption.Year, out var current))
            {
                current.UpdateAmount(exemption.ExemptAmount, exemption.IsActive);
            }
            else
            {
                dbContext.AguinaldoExemptions.Add(exemption);
            }
        }
    }
}
