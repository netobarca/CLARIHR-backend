using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Infrastructure.Configuration;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class PersonnelFileProfilePhotoService(
    IOptions<BlobStorageOptions> options,
    ILogger<PersonnelFileProfilePhotoService> logger) : IPersonnelFileProfilePhotoService
{
    private const int MaxProfileImageBytes = 2 * 1024 * 1024;
    private const string PhotoUrlField = "photoUrl";
    private const string DataUrlPrefix = "data:";
    private const string Base64Separator = ";base64,";

    private readonly BlobStorageOptions _options = options.Value;
    private readonly ILogger<PersonnelFileProfilePhotoService> _logger = logger;
    private readonly BlobContainerClient? _containerClient =
        !string.IsNullOrWhiteSpace(options.Value.ConnectionString) && !string.IsNullOrWhiteSpace(options.Value.ProfileImagesContainer)
            ? new BlobServiceClient(options.Value.ConnectionString).GetBlobContainerClient(options.Value.ProfileImagesContainer)
            : null;

    public async Task<Result<PersonnelFileProfilePhotoWritePlan>> PrepareWriteAsync(
        Guid companyId,
        Guid personnelFileId,
        string? requestedPhotoUrl,
        string? currentPersistedPhotoUrl,
        CancellationToken cancellationToken)
    {
        var requested = NormalizeOptional(requestedPhotoUrl);
        var current = NormalizeOptional(currentPersistedPhotoUrl);
        var currentManagedCanonical = TryGetManagedCanonicalUrl(current, out _, out var currentCanonical)
            ? currentCanonical
            : null;

        if (string.IsNullOrWhiteSpace(requested))
        {
            return Result<PersonnelFileProfilePhotoWritePlan>.Success(
                new PersonnelFileProfilePhotoWritePlan(
                    PersistedPhotoUrl: null,
                    UploadedManagedPhotoUrl: null,
                    PreviousManagedPhotoUrlToDelete: currentManagedCanonical));
        }

        if (TryParseDataUrl(requested, out var mimeType, out var base64Payload))
        {
            if (!_options.IsConfigured || _containerClient is null)
            {
                return Result<PersonnelFileProfilePhotoWritePlan>.Failure(ProfilePhotoStorageNotConfigured());
            }

            if (!TryResolveMimeType(mimeType, out var extension, out var normalizedMimeType))
            {
                return Result<PersonnelFileProfilePhotoWritePlan>.Failure(
                    PhotoUrlValidation("PhotoUrl only supports png, jpg/jpeg, webp, or svg images."));
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64Payload);
            }
            catch (FormatException)
            {
                return Result<PersonnelFileProfilePhotoWritePlan>.Failure(
                    PhotoUrlValidation("PhotoUrl contains invalid base64 image data."));
            }

            if (bytes.Length == 0)
            {
                return Result<PersonnelFileProfilePhotoWritePlan>.Failure(
                    PhotoUrlValidation("PhotoUrl image content cannot be empty."));
            }

            if (bytes.Length > MaxProfileImageBytes)
            {
                return Result<PersonnelFileProfilePhotoWritePlan>.Failure(
                    PhotoUrlValidation("PhotoUrl exceeds the maximum allowed size of 2 MB."));
            }

            if (string.Equals(normalizedMimeType, "image/svg+xml", StringComparison.Ordinal))
            {
                if (!TryValidateSvgPayload(bytes, out var svgValidationMessage))
                {
                    return Result<PersonnelFileProfilePhotoWritePlan>.Failure(
                        PhotoUrlValidation(svgValidationMessage ?? "PhotoUrl SVG content is invalid."));
                }
            }

            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            var blobName = $"company/{companyId:D}/personnel-files/{personnelFileId:D}/profile-photo/{Guid.NewGuid():D}{extension}";
            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(
                BinaryData.FromBytes(bytes),
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = normalizedMimeType
                    }
                },
                cancellationToken);

            var uploadedManagedCanonical = CanonicalizeAbsoluteUrl(blobClient.Uri.AbsoluteUri);
            var previousManagedPhotoToDelete = currentManagedCanonical is not null &&
                                               !string.Equals(currentManagedCanonical, uploadedManagedCanonical, StringComparison.OrdinalIgnoreCase)
                ? currentManagedCanonical
                : null;

            return Result<PersonnelFileProfilePhotoWritePlan>.Success(
                new PersonnelFileProfilePhotoWritePlan(
                    PersistedPhotoUrl: uploadedManagedCanonical,
                    UploadedManagedPhotoUrl: uploadedManagedCanonical,
                    PreviousManagedPhotoUrlToDelete: previousManagedPhotoToDelete));
        }

        if (Uri.TryCreate(requested, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             absoluteUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            var persistedUrl = requested;
            if (TryGetManagedCanonicalUrl(requested, out _, out var managedCanonical))
            {
                persistedUrl = managedCanonical;
            }

            if (persistedUrl.Length > 1000)
            {
                return Result<PersonnelFileProfilePhotoWritePlan>.Failure(
                    PhotoUrlValidation("PhotoUrl exceeds the maximum persisted length of 1000 characters."));
            }

            var previousManagedPhotoToDelete = currentManagedCanonical is not null &&
                                               !string.Equals(currentManagedCanonical, persistedUrl, StringComparison.OrdinalIgnoreCase)
                ? currentManagedCanonical
                : null;

            return Result<PersonnelFileProfilePhotoWritePlan>.Success(
                new PersonnelFileProfilePhotoWritePlan(
                    PersistedPhotoUrl: persistedUrl,
                    UploadedManagedPhotoUrl: null,
                    PreviousManagedPhotoUrlToDelete: previousManagedPhotoToDelete));
        }

        return Result<PersonnelFileProfilePhotoWritePlan>.Failure(
            PhotoUrlValidation("PhotoUrl must be null, an http/https URL, or a valid base64 data URL."));
    }

    public async Task<string?> ResolveForReadAsync(string? persistedPhotoUrl, CancellationToken cancellationToken)
    {
        var persisted = NormalizeOptional(persistedPhotoUrl);
        if (string.IsNullOrWhiteSpace(persisted))
        {
            return null;
        }

        if (!TryGetManagedCanonicalUrl(persisted, out var blobName, out var canonical))
        {
            return persisted;
        }

        if (!_options.IsConfigured || _containerClient is null)
        {
            return canonical;
        }

        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            if (!blobClient.CanGenerateSasUri)
            {
                return canonical;
            }

            var ttlMinutes = _options.ProfileImageSasTtlMinutes > 0
                ? _options.ProfileImageSasTtlMinutes
                : 15;

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _options.ProfileImagesContainer,
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(ttlMinutes)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            _ = cancellationToken;
            return blobClient.GenerateSasUri(sasBuilder).AbsoluteUri;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to generate profile photo SAS URL for blob {BlobName}. Falling back to canonical URL.",
                blobName);
            return canonical;
        }
    }

    public async Task CleanupAfterPersistenceFailureAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(plan.UploadedManagedPhotoUrl))
        {
            await DeleteManagedBlobBestEffortAsync(
                plan.UploadedManagedPhotoUrl,
                "cleanup after persistence failure",
                cancellationToken);
        }
    }

    public async Task CleanupAfterPersistenceSuccessAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(plan.PreviousManagedPhotoUrlToDelete))
        {
            await DeleteManagedBlobBestEffortAsync(
                plan.PreviousManagedPhotoUrlToDelete,
                "cleanup after persistence success",
                cancellationToken);
        }
    }

    private async Task DeleteManagedBlobBestEffortAsync(
        string managedPhotoUrl,
        string operation,
        CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured || _containerClient is null)
        {
            return;
        }

        if (!TryGetManagedCanonicalUrl(managedPhotoUrl, out var blobName, out _))
        {
            return;
        }

        try
        {
            _ = await _containerClient.GetBlobClient(blobName)
                .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to delete managed profile photo blob {BlobName} during {Operation}.",
                blobName,
                operation);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static bool TryParseDataUrl(string value, out string mimeType, out string base64Payload)
    {
        mimeType = string.Empty;
        base64Payload = string.Empty;

        if (!value.StartsWith(DataUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separatorIndex = value.IndexOf(Base64Separator, StringComparison.OrdinalIgnoreCase);
        if (separatorIndex <= DataUrlPrefix.Length)
        {
            return false;
        }

        mimeType = value[DataUrlPrefix.Length..separatorIndex].Trim().ToLowerInvariant();
        base64Payload = value[(separatorIndex + Base64Separator.Length)..].Trim();
        return !string.IsNullOrWhiteSpace(mimeType) && !string.IsNullOrWhiteSpace(base64Payload);
    }

    private static bool TryResolveMimeType(string mimeType, out string extension, out string normalizedMimeType)
    {
        extension = string.Empty;
        normalizedMimeType = string.Empty;

        switch (mimeType.Trim().ToLowerInvariant())
        {
            case "image/png":
                extension = ".png";
                normalizedMimeType = "image/png";
                return true;
            case "image/jpeg":
            case "image/jpg":
                extension = ".jpg";
                normalizedMimeType = "image/jpeg";
                return true;
            case "image/webp":
                extension = ".webp";
                normalizedMimeType = "image/webp";
                return true;
            case "image/svg+xml":
            case "image/svg":
                extension = ".svg";
                normalizedMimeType = "image/svg+xml";
                return true;
            default:
                return false;
        }
    }

    private static bool TryValidateSvgPayload(byte[] bytes, out string? validationMessage)
    {
        validationMessage = null;

        string svgText;
        try
        {
            svgText = System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            validationMessage = "PhotoUrl SVG content could not be decoded.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(svgText))
        {
            validationMessage = "PhotoUrl SVG content cannot be empty.";
            return false;
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var textReader = new StringReader(svgText);
            using var xmlReader = XmlReader.Create(textReader, settings);
            var document = XDocument.Load(xmlReader, LoadOptions.None);
            var root = document.Root;

            if (root is null || !root.Name.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
            {
                validationMessage = "PhotoUrl SVG content must define an <svg> root element.";
                return false;
            }

            foreach (var element in root.DescendantsAndSelf())
            {
                var localName = element.Name.LocalName;
                if (localName.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                    localName.Equals("foreignObject", StringComparison.OrdinalIgnoreCase))
                {
                    validationMessage = "PhotoUrl SVG content includes unsupported active elements.";
                    return false;
                }

                foreach (var attribute in element.Attributes())
                {
                    var attributeName = attribute.Name.LocalName;
                    var attributeValue = attribute.Value.Trim();

                    if (attributeName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                    {
                        validationMessage = "PhotoUrl SVG content includes unsupported event attributes.";
                        return false;
                    }

                    if ((attributeName.Equals("href", StringComparison.OrdinalIgnoreCase) ||
                         attributeName.Equals("xlink:href", StringComparison.OrdinalIgnoreCase)) &&
                        attributeValue.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                    {
                        validationMessage = "PhotoUrl SVG content includes unsupported javascript references.";
                        return false;
                    }
                }
            }
        }
        catch
        {
            validationMessage = "PhotoUrl SVG content is not well-formed XML.";
            return false;
        }

        return true;
    }

    private bool TryGetManagedCanonicalUrl(string? value, out string blobName, out string canonicalUrl)
    {
        blobName = string.Empty;
        canonicalUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.AccountName) || string.IsNullOrWhiteSpace(_options.ProfileImagesContainer))
        {
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var expectedHost = $"{_options.AccountName}.blob.core.windows.net";
        if (!uri.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath.Trim('/');
        if (!path.StartsWith($"{_options.ProfileImagesContainer}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        blobName = path[(_options.ProfileImagesContainer.Length + 1)..];
        canonicalUrl = CanonicalizeAbsoluteUrl(uri.AbsoluteUri);
        return !string.IsNullOrWhiteSpace(blobName);
    }

    private static string CanonicalizeAbsoluteUrl(string absoluteUrl)
    {
        var uri = new Uri(absoluteUrl, UriKind.Absolute);
        return uri.GetLeftPart(UriPartial.Path);
    }

    private static Error PhotoUrlValidation(string message) =>
        ErrorCatalog.Validation(
            new Dictionary<string, string[]>
            {
                [PhotoUrlField] = [message]
            });

    private static Error ProfilePhotoStorageNotConfigured() =>
        new(
            "PERSONNEL_FILE_PROFILE_PHOTO_STORAGE_NOT_CONFIGURED",
            "Profile photo storage is not configured for base64 uploads.",
            ErrorType.Failure);
}
