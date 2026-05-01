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
