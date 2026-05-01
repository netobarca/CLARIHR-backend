using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class PersonnelFileProfilePhotoService : IPersonnelFileProfilePhotoService
{
    private const string PhotoUrlField = "photoUrl";

    public Task<Result<PersonnelFileProfilePhotoWritePlan>> PrepareWriteAsync(
        Guid companyId,
        Guid personnelFileId,
        string? requestedPhotoUrl,
        string? currentPersistedPhotoUrl,
        CancellationToken cancellationToken)
    {
        var requested = string.IsNullOrWhiteSpace(requestedPhotoUrl) ? null : requestedPhotoUrl.Trim();

        if (string.IsNullOrWhiteSpace(requested))
        {
            return Task.FromResult(Result<PersonnelFileProfilePhotoWritePlan>.Success(
                new PersonnelFileProfilePhotoWritePlan(
                    PersistedPhotoUrl: null,
                    UploadedManagedPhotoUrl: null,
                    PreviousManagedPhotoUrlToDelete: null)));
        }

        if (Uri.TryCreate(requested, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             absoluteUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            if (requested.Length > 1000)
            {
                return Task.FromResult(Result<PersonnelFileProfilePhotoWritePlan>.Failure(
                    PhotoUrlValidation("PhotoUrl exceeds the maximum persisted length of 1000 characters.")));
            }

            return Task.FromResult(Result<PersonnelFileProfilePhotoWritePlan>.Success(
                new PersonnelFileProfilePhotoWritePlan(
                    PersistedPhotoUrl: requested,
                    UploadedManagedPhotoUrl: null,
                    PreviousManagedPhotoUrlToDelete: null)));
        }

        return Task.FromResult(Result<PersonnelFileProfilePhotoWritePlan>.Failure(
            PhotoUrlValidation("PhotoUrl must be null or a valid http/https URL.")));
    }

    public Task<string?> ResolveForReadAsync(string? persistedPhotoUrl, CancellationToken cancellationToken)
    {
        var persisted = string.IsNullOrWhiteSpace(persistedPhotoUrl) ? null : persistedPhotoUrl.Trim();
        return Task.FromResult(persisted);
    }

    public Task CleanupAfterPersistenceFailureAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CleanupAfterPersistenceSuccessAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static Error PhotoUrlValidation(string message) =>
        ErrorCatalog.Validation(
            new Dictionary<string, string[]>
            {
                [PhotoUrlField] = [message]
            });
}
