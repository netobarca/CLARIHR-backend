using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>The company's not-worked-time TYPE master (REQ-011 D-18). No delete: the removal is logical.</summary>
public interface INotWorkedTimeTypeRepository
{
    Task<IReadOnlyCollection<NotWorkedTimeType>> GetAsync(
        Guid tenantId,
        bool? isActive,
        CancellationToken cancellationToken);

    /// <summary>TRACKED — the caller mutates it and the unit of work commits.</summary>
    Task<NotWorkedTimeType?> GetEntityAsync(
        Guid tenantId,
        Guid publicId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingPublicId,
        CancellationToken cancellationToken);

    void Add(NotWorkedTimeType entity);
}
