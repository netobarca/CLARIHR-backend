using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class PersonnelFileProfilePhotoService(
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver) : IPersonnelFileProfilePhotoService
{
    public async Task<Result<PersonnelFileProfilePhotoWritePlan>> PrepareWriteAsync(
        Guid companyId,
        Guid personnelFilePublicId,
        Guid? requestedPhotoFilePublicId,
        Guid? currentPersistedPhotoFilePublicId,
        CancellationToken cancellationToken)
    {
        // Clearing the photo
        if (!requestedPhotoFilePublicId.HasValue)
        {
            return Result<PersonnelFileProfilePhotoWritePlan>.Success(
                new PersonnelFileProfilePhotoWritePlan(
                    PersistedPhotoFilePublicId: null,
                    PreviousPhotoFilePublicIdToDelete: currentPersistedPhotoFilePublicId));
        }

        // Same photo as current — no change needed
        if (requestedPhotoFilePublicId == currentPersistedPhotoFilePublicId)
        {
            return Result<PersonnelFileProfilePhotoWritePlan>.Success(
                new PersonnelFileProfilePhotoWritePlan(
                    PersistedPhotoFilePublicId: requestedPhotoFilePublicId,
                    PreviousPhotoFilePublicIdToDelete: null));
        }

        // Validate the new file exists and is Active with correct Purpose
        var file = await fileRepository.GetByPublicIdAsync(requestedPhotoFilePublicId.Value, cancellationToken);
        if (file is null)
        {
            return Result<PersonnelFileProfilePhotoWritePlan>.Failure(FileErrors.FileNotFound);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<PersonnelFileProfilePhotoWritePlan>.Failure(FileErrors.FileNotActive);
        }

        if (file.Purpose != FilePurpose.ProfileImage)
        {
            return Result<PersonnelFileProfilePhotoWritePlan>.Failure(
                new Error(
                    "files.invalid_purpose_for_profile",
                    "The referenced file is not a profile image.",
                    ErrorType.UnprocessableEntity));
        }

        return Result<PersonnelFileProfilePhotoWritePlan>.Success(
            new PersonnelFileProfilePhotoWritePlan(
                PersistedPhotoFilePublicId: requestedPhotoFilePublicId,
                PreviousPhotoFilePublicIdToDelete: currentPersistedPhotoFilePublicId));
    }

    public async Task<string?> ResolveForReadAsync(Guid? persistedPhotoFilePublicId, CancellationToken cancellationToken)
    {
        if (!persistedPhotoFilePublicId.HasValue)
        {
            return null;
        }

        var file = await fileRepository.GetByPublicIdAsync(persistedPhotoFilePublicId.Value, cancellationToken);
        if (file is null || file.Status != FileStatus.Active)
        {
            return null;
        }

        try
        {
            var provider = providerResolver.Resolve(file.Provider);
            var readSession = await provider.CreateReadSessionAsync(
                new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
                cancellationToken);

            return readSession.ReadUrl;
        }
        catch
        {
            return null;
        }
    }

    public Task CleanupAfterPersistenceFailureAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken)
    {
        // No cleanup needed on failure — the file remains in storage, cleanup job handles orphans
        return Task.CompletedTask;
    }

    public async Task CleanupAfterPersistenceSuccessAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken)
    {
        if (!plan.PreviousPhotoFilePublicIdToDelete.HasValue)
        {
            return;
        }

        // Soft-delete the previous photo file
        var previousFile = await fileRepository.GetByPublicIdAsync(plan.PreviousPhotoFilePublicIdToDelete.Value, cancellationToken);
        if (previousFile is not null)
        {
            previousFile.MarkDeleted();
            // Note: the caller should call unitOfWork.SaveChangesAsync after this
        }
    }
}
