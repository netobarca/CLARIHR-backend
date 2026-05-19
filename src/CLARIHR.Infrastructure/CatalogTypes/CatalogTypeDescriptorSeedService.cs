using CLARIHR.Application.Features.JobProfileCatalogTypes;
using CLARIHR.Domain.CatalogTypes;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.CatalogTypes;

/// <summary>
/// Idempotently seeds the global Job Profile catalog type registry from
/// <see cref="JobProfileCatalogBindingMap.CanonicalTypes"/>. Mirrors the
/// "load existing, insert missing" pattern of the position description catalog
/// seed service, but the registry is global so there is no tenant loop.
/// </summary>
internal sealed class CatalogTypeDescriptorSeedService(ApplicationDbContext dbContext)
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var existingNormalizedCodes = await dbContext.CatalogTypeDescriptors
            .AsNoTracking()
            .Select(item => item.NormalizedCode)
            .ToListAsync(cancellationToken);

        var existing = new HashSet<string>(existingNormalizedCodes, StringComparer.Ordinal);

        var missing = new List<CatalogTypeDescriptor>();
        for (var index = 0; index < JobProfileCatalogBindingMap.CanonicalTypes.Count; index++)
        {
            var definition = JobProfileCatalogBindingMap.CanonicalTypes[index];
            var normalizedCode = definition.RegistryCode.Trim().ToUpperInvariant();
            if (existing.Contains(normalizedCode))
            {
                continue;
            }

            missing.Add(CatalogTypeDescriptor.Create(
                definition.RegistryCode,
                definition.DisplayName,
                sortOrder: (index + 1) * 10));
        }

        if (missing.Count > 0)
        {
            dbContext.CatalogTypeDescriptors.AddRange(missing);
            _ = await dbContext.SaveChangesAsync(cancellationToken);
        }

        await VerifySeedCompleteOrThrowAsync(cancellationToken);
    }

    // §D6 (doc technical-debt/07): post-seed fail-fast. Without this, an incomplete
    // registry (partial commit, normalization mismatch between the canonical
    // RegistryCode and the persisted NormalizedCode, etc.) degrades silently — the
    // catalog manifest would emit every field with isActive:false and the frontend
    // would hide all catalog fields with no error anywhere. Re-read the persisted
    // state and refuse to boot if any canonical type is missing, so the failure is
    // loud at startup instead of an invisible product outage. Runs unconditionally
    // (also when nothing was missing) to validate actual persisted, queryable rows.
    private async Task VerifySeedCompleteOrThrowAsync(CancellationToken cancellationToken)
    {
        var persisted = new HashSet<string>(
            await dbContext.CatalogTypeDescriptors
                .AsNoTracking()
                .Select(item => item.NormalizedCode)
                .ToListAsync(cancellationToken),
            StringComparer.Ordinal);

        var missingAfterSeed = JobProfileCatalogBindingMap.CanonicalTypes
            .Select(definition => definition.RegistryCode.Trim().ToUpperInvariant())
            .Where(normalizedCode => !persisted.Contains(normalizedCode))
            .ToList();

        if (missingAfterSeed.Count > 0)
        {
            throw new InvalidOperationException(
                "CatalogTypeDescriptor seed is incomplete after EnsureSeededAsync: " +
                $"{missingAfterSeed.Count} canonical type(s) not persisted " +
                $"[{string.Join(", ", missingAfterSeed)}]. Refusing to start — the " +
                "Job Profile catalog manifest would silently degrade (all isActive:false).");
        }
    }
}
