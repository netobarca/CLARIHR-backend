using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

public sealed record PersonnelFileProfilePhotoWritePlan(
    string? PersistedPhotoUrl,
    string? UploadedManagedPhotoUrl,
    string? PreviousManagedPhotoUrlToDelete);

public interface IPersonnelFileProfilePhotoService
{
    Task<Result<PersonnelFileProfilePhotoWritePlan>> PrepareWriteAsync(
        Guid companyId,
        Guid personnelFileId,
        string? requestedPhotoUrl,
        string? currentPersistedPhotoUrl,
        CancellationToken cancellationToken);

    Task<string?> ResolveForReadAsync(string? persistedPhotoUrl, CancellationToken cancellationToken);

    Task CleanupAfterPersistenceFailureAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken);

    Task CleanupAfterPersistenceSuccessAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken);
}
