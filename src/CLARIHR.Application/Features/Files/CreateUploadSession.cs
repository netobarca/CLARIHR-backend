using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;

namespace CLARIHR.Application.Features.Files;

// --- Command ---

public sealed record CreateUploadSessionCommand(
    string FileName,
    string ContentType,
    long SizeBytes,
    string Purpose,
    Guid? EntityId) : ICommand<CreateUploadSessionResponse>;

// --- Response ---

public sealed record CreateUploadSessionResponse(
    Guid FilePublicId,
    string UploadUrl,
    DateTime ExpiresUtc,
    IReadOnlyDictionary<string, string> RequiredHeaders,
    Guid ConcurrencyToken);

// --- Handler ---

internal sealed class CreateUploadSessionCommandHandler(
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IFilePurposeRuleProvider purposeRuleProvider,
    IFileStorageProviderResolver providerResolver,
    IFileObjectKeyBuilder objectKeyBuilder,
    IFileRepository fileRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateUploadSessionCommand, CreateUploadSessionResponse>
{
    public async Task<Result<CreateUploadSessionResponse>> Handle(
        CreateUploadSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FilePurpose>(command.Purpose, ignoreCase: true, out var purpose))
        {
            return Result<CreateUploadSessionResponse>.Failure(FileErrors.InvalidPurpose(command.Purpose));
        }

        var rule = purposeRuleProvider.GetRule(purpose);
        if (rule is null)
        {
            return Result<CreateUploadSessionResponse>.Failure(FileErrors.PurposeNotConfigured);
        }

        if (command.SizeBytes > rule.MaxSizeBytes)
        {
            return Result<CreateUploadSessionResponse>.Failure(FileErrors.FileTooLarge);
        }

        var normalizedContentType = command.ContentType.Trim().ToLowerInvariant();
        if (!rule.AllowedContentTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Result<CreateUploadSessionResponse>.Failure(FileErrors.ContentTypeNotAllowed(normalizedContentType));
        }

        var extension = Path.GetExtension(command.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(extension) || !rule.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return Result<CreateUploadSessionResponse>.Failure(FileErrors.ExtensionNotAllowed(extension));
        }

        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant context is required for file uploads.");

        var userId = currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user is required for file uploads.");

        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new InvalidOperationException("UserId must be a valid GUID.");
        }

        var filePublicId = Guid.NewGuid();

        var storedFile = StoredFile.Create(
            fileName: command.FileName,
            contentType: normalizedContentType,
            sizeBytes: command.SizeBytes,
            extension: extension,
            provider: rule.DefaultProvider,
            containerName: rule.ContainerOverride ?? "clarihr-files",
            objectKey: objectKeyBuilder.Build(purpose, tenantId, userGuid, filePublicId, extension),
            purpose: purpose,
            visibility: FileVisibility.Private,
            uploadType: FileUploadType.DirectUpload,
            createdByUserId: userId,
            entityId: command.EntityId);

        storedFile.SetTenantId(tenantId);

        await fileRepository.AddAsync(storedFile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var provider = providerResolver.Resolve(rule.DefaultProvider);
        var uploadSession = await provider.CreateUploadSessionAsync(
            new CreateUploadSessionProviderCommand(
                storedFile.ContainerName,
                storedFile.ObjectKey,
                normalizedContentType,
                command.SizeBytes),
            cancellationToken);

        return Result<CreateUploadSessionResponse>.Success(
            new CreateUploadSessionResponse(
                storedFile.PublicId,
                uploadSession.UploadUrl,
                uploadSession.ExpiresUtc,
                uploadSession.RequiredHeaders,
                storedFile.ConcurrencyToken));
    }
}
