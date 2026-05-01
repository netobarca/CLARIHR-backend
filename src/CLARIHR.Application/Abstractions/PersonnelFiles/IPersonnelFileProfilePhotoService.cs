using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

public sealed record PersonnelFileProfilePhotoWritePlan(
    Guid? PersistedPhotoFilePublicId,
    Guid? PreviousPhotoFilePublicIdToDelete);

public interface IPersonnelFileProfilePhotoService
{
    Task<Result<PersonnelFileProfilePhotoWritePlan>> PrepareWriteAsync(
        Guid companyId,
        Guid personnelFilePublicId,
        Guid? requestedPhotoFilePublicId,
        Guid? currentPersistedPhotoFilePublicId,
        CancellationToken cancellationToken);

    Task<string?> ResolveForReadAsync(Guid? persistedPhotoFilePublicId, CancellationToken cancellationToken);

    Task CleanupAfterPersistenceFailureAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken);

    Task CleanupAfterPersistenceSuccessAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken);
}
