using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Features.Files;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// FILE-1 (security): the generic <c>GET /files/{id}/read-url</c> endpoint must be owner-only — it
/// must not hand out a read SAS for a file the caller does not own (intra-tenant IDOR). Mirrors the
/// ownership gate already enforced on complete/delete. Files that other authorized users must read
/// (personnel-file documents, profile photos) are served through their own domain-authorized path.
/// </summary>
public sealed class FileAccessControlTests
{
    private const string OwnerUserId = "11111111-1111-1111-1111-111111111111";
    private const string OtherUserId = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task GetFileReadUrl_WhenCallerIsNotOwner_ShouldReturnOwnershipMismatch()
    {
        var file = CreateActiveFile(OwnerUserId);
        var handler = new GetFileReadUrlQueryHandler(
            new TestCurrentUserService(OtherUserId),
            new TestFileRepository(file),
            new TestFileStorageProviderResolver());

        var result = await handler.Handle(new GetFileReadUrlQuery(file.PublicId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(FileErrors.FileOwnershipMismatch.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetFileReadUrl_WhenCallerIsOwner_ShouldReturnReadUrl()
    {
        var file = CreateActiveFile(OwnerUserId);
        var handler = new GetFileReadUrlQueryHandler(
            new TestCurrentUserService(OwnerUserId),
            new TestFileRepository(file),
            new TestFileStorageProviderResolver());

        var result = await handler.Handle(new GetFileReadUrlQuery(file.PublicId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.ReadUrl));
    }

    private static StoredFile CreateActiveFile(string ownerUserId)
    {
        var file = StoredFile.Create(
            fileName: "doc.pdf",
            contentType: "application/pdf",
            sizeBytes: 1024,
            extension: ".pdf",
            provider: StorageProvider.AzureBlob,
            containerName: "clarihr-files",
            objectKey: "tenants/t/users/u/documents/f.pdf",
            purpose: FilePurpose.PersonnelDocument,
            visibility: FileVisibility.Private,
            uploadType: FileUploadType.DirectUpload,
            createdByUserId: ownerUserId);
        file.MarkActive(1024, "application/pdf");
        return file;
    }

    private sealed class TestCurrentUserService(string userId) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public string? UserId { get; } = userId;
        public IReadOnlyCollection<string> Roles { get; } = [];
        public IReadOnlyCollection<string> Permissions { get; } = [];
    }

    private sealed class TestFileRepository(StoredFile file) : IFileRepository
    {
        public Task<StoredFile?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken) =>
            Task.FromResult<StoredFile?>(file.PublicId == publicId ? file : null);

        public Task AddAsync(StoredFile storedFile, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<StoredFile>> GetExpiredPendingUploadsAsync(DateTime olderThan, int batchSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestFileStorageProviderResolver : IFileStorageProviderResolver
    {
        public IFileStorageProvider Resolve(StorageProvider provider) => new TestFileStorageProvider();
    }

    private sealed class TestFileStorageProvider : IFileStorageProvider
    {
        public StorageProvider ProviderType => StorageProvider.AzureBlob;

        public Task<CreateReadSessionResult> CreateReadSessionAsync(CreateReadSessionCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(new CreateReadSessionResult($"https://blob.test/{command.ObjectKey}?sig=test", new DateTime(2026, 6, 6, 12, 0, 0, DateTimeKind.Utc)));

        public Task<CreateUploadSessionResult> CreateUploadSessionAsync(CreateUploadSessionProviderCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string containerName, string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FileObjectInfo?> GetObjectInfoAsync(string containerName, string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string containerName, string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FileObjectInfo> UploadStreamAsync(string containerName, string objectKey, string contentType, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream?> OpenReadStreamAsync(string containerName, string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
