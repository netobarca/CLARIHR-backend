using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files.Common;

namespace CLARIHR.Application.Features.Files;

// --- Command ---

public sealed record DeleteFileCommand(
    Guid FilePublicId,
    Guid ConcurrencyToken) : ICommand<DeleteFileResponse>;

// --- Response ---

public sealed record DeleteFileResponse(
    Guid FilePublicId,
    string Status);

// --- Handler ---

internal sealed class DeleteFileCommandHandler(
    ICurrentUserService currentUserService,
    IFileRepository fileRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteFileCommand, DeleteFileResponse>
{
    public async Task<Result<DeleteFileResponse>> Handle(
        DeleteFileCommand command,
        CancellationToken cancellationToken)
    {
        var file = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<DeleteFileResponse>.Failure(FileErrors.FileNotFound);
        }

        if (file.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DeleteFileResponse>.Failure(FileErrors.ConcurrencyConflict);
        }

        var userId = currentUserService.UserId ?? string.Empty;
        if (!string.Equals(file.CreatedByUserId, userId, StringComparison.Ordinal))
        {
            return Result<DeleteFileResponse>.Failure(FileErrors.FileOwnershipMismatch);
        }

        file.MarkDeleted();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<DeleteFileResponse>.Success(
            new DeleteFileResponse(file.PublicId, file.Status.ToString()));
    }
}
