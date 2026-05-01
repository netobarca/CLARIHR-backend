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

public sealed record CompleteFileUploadCommand(
    Guid FilePublicId) : ICommand<CompleteFileUploadResponse>;

// --- Response ---

public sealed record CompleteFileUploadResponse(
    Guid FilePublicId,
    string Status);

// --- Handler ---

internal sealed class CompleteFileUploadCommandHandler(
    ICurrentUserService currentUserService,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver,
    IUnitOfWork unitOfWork) : ICommandHandler<CompleteFileUploadCommand, CompleteFileUploadResponse>
{
    public async Task<Result<CompleteFileUploadResponse>> Handle(
        CompleteFileUploadCommand command,
        CancellationToken cancellationToken)
    {
        var file = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<CompleteFileUploadResponse>.Failure(FileErrors.FileNotFound);
        }

        var userId = currentUserService.UserId ?? string.Empty;
        if (!string.Equals(file.CreatedByUserId, userId, StringComparison.Ordinal))
        {
            return Result<CompleteFileUploadResponse>.Failure(FileErrors.FileOwnershipMismatch);
        }

        if (file.Status != FileStatus.PendingUpload)
        {
            return Result<CompleteFileUploadResponse>.Failure(FileErrors.FileNotPendingUpload);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var exists = await provider.ExistsAsync(file.ContainerName, file.ObjectKey, cancellationToken);

        if (!exists)
        {
            file.MarkFailed("Object not found in storage after upload session.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<CompleteFileUploadResponse>.Failure(FileErrors.UploadNotFoundInStorage);
        }

        var objectInfo = await provider.GetObjectInfoAsync(file.ContainerName, file.ObjectKey, cancellationToken);
        if (objectInfo is null)
        {
            file.MarkFailed("Could not retrieve object metadata from storage.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<CompleteFileUploadResponse>.Failure(FileErrors.UploadNotFoundInStorage);
        }

        file.MarkActive(objectInfo.SizeBytes, objectInfo.ContentType);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CompleteFileUploadResponse>.Success(
            new CompleteFileUploadResponse(file.PublicId, file.Status.ToString()));
    }
}
