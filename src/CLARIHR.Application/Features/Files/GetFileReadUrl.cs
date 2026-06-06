using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;

namespace CLARIHR.Application.Features.Files;

// --- Query ---

public sealed record GetFileReadUrlQuery(
    Guid FilePublicId) : IQuery<GetFileReadUrlResponse>;

// --- Response ---

public sealed record GetFileReadUrlResponse(
    string ReadUrl,
    DateTime ExpiresUtc);

// --- Handler ---

internal sealed class GetFileReadUrlQueryHandler(
    ICurrentUserService currentUserService,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver) : IQueryHandler<GetFileReadUrlQuery, GetFileReadUrlResponse>
{
    public async Task<Result<GetFileReadUrlResponse>> Handle(
        GetFileReadUrlQuery query,
        CancellationToken cancellationToken)
    {
        var file = await fileRepository.GetByPublicIdAsync(query.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<GetFileReadUrlResponse>.Failure(FileErrors.FileNotFound);
        }

        // FILE-1 (security): the generic read-url endpoint is owner-only — it must not hand out a
        // read SAS for a file the caller does not own (intra-tenant IDOR). Domains that need to serve
        // a file to other authorized users (e.g. personnel-file documents, profile photos) mint the
        // SAS through their own authorized path. Mirrors the gate on complete/delete.
        var userId = currentUserService.UserId ?? string.Empty;
        if (!string.Equals(file.CreatedByUserId, userId, StringComparison.Ordinal))
        {
            return Result<GetFileReadUrlResponse>.Failure(FileErrors.FileOwnershipMismatch);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<GetFileReadUrlResponse>.Failure(FileErrors.FileNotActive);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var session = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
            cancellationToken);

        return Result<GetFileReadUrlResponse>.Success(
            new GetFileReadUrlResponse(session.ReadUrl, session.ExpiresUtc));
    }
}
