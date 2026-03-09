using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

public interface IPersonnelFileRepository
{
    void Add(PersonnelFile personnelFile);

    void AddCustomFieldDefinition(PersonnelFileCustomFieldDefinition definition);

    Task<PersonnelFile?> GetByIdAsync(Guid personnelFileId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid personnelFileId, CancellationToken cancellationToken);

    Task<bool> IdentificationExistsAsync(
        Guid tenantId,
        string identificationType,
        string normalizedIdentificationNumber,
        long? excludingPersonnelFileId,
        CancellationToken cancellationToken);

    Task<PagedResponse<PersonnelFileListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? maritalStatus,
        string? nationality,
        string? profession,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PersonnelFileResponse?> GetResponseByIdAsync(Guid personnelFileId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCatalogItemsAsync(
        Guid tenantId,
        string category,
        CancellationToken cancellationToken);

    Task<bool> CatalogCodeIsActiveAsync(
        Guid tenantId,
        string category,
        string code,
        CancellationToken cancellationToken);

    Task<PersonnelFileDocumentDownloadResponse?> GetDocumentDownloadByIdAsync(Guid documentId, CancellationToken cancellationToken);

    Task<PersonnelFileDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken);

    Task<bool> DocumentExistsOutsideTenantAsync(Guid documentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileExportRow>> GetExportRowsAsync(
        Guid tenantId,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? maritalStatus,
        string? nationality,
        string? profession,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        CancellationToken cancellationToken);

    Task<PersonnelFileDynamicQueryResponse> DynamicQueryAsync(
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileDynamicFilterInput> filters,
        IReadOnlyCollection<string> groupBy,
        IReadOnlyCollection<PersonnelFileDynamicSortInput> sort,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PersonnelFileAnalyticsSummaryResponse> GetAnalyticsSummaryAsync(
        Guid tenantId,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? search,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>> GetCustomFieldDefinitionsAsync(
        Guid tenantId,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<PersonnelFileCustomFieldDefinition?> GetCustomFieldDefinitionByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CustomFieldKeyExistsAsync(Guid tenantId, string normalizedKey, long? excludingId, CancellationToken cancellationToken);
}
