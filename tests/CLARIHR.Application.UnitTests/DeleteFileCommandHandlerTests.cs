using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Features.Files;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;

namespace CLARIHR.Application.UnitTests;

public sealed class DeleteFileCommandHandlerTests
{
    private const string OwnerUserId = "90000000-0000-0000-0000-000000000010";

    [Fact]
    public async Task Handle_WithCorrectConcurrencyToken_ShouldMarkFileDeleted()
    {
        var file = CreateStoredFile();
        var repository = new TestFileRepository(file);
        var handler = new DeleteFileCommandHandler(new TestCurrentUserService(OwnerUserId), repository, new TestUnitOfWork());

        var result = await handler.Handle(new DeleteFileCommand(file.PublicId, file.ConcurrencyToken), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FileStatus.Deleted, file.Status);
    }

    [Fact]
    public async Task Handle_WithStaleConcurrencyToken_ShouldReturnConcurrencyConflictAndNotDelete()
    {
        var file = CreateStoredFile();
        var repository = new TestFileRepository(file);
        var handler = new DeleteFileCommandHandler(new TestCurrentUserService(OwnerUserId), repository, new TestUnitOfWork());

        var result = await handler.Handle(new DeleteFileCommand(file.PublicId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(FileErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.NotEqual(FileStatus.Deleted, file.Status);
    }

    [Fact]
    public async Task Handle_WithStaleToken_ShouldTakePrecedenceOverOwnershipMismatch()
    {
        // The concurrency check runs before the ownership check, so a stale token surfaces
        // CONCURRENCY_CONFLICT even for a caller who also fails the ownership guard.
        var file = CreateStoredFile();
        var repository = new TestFileRepository(file);
        var handler = new DeleteFileCommandHandler(new TestCurrentUserService("someone-else"), repository, new TestUnitOfWork());

        var result = await handler.Handle(new DeleteFileCommand(file.PublicId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(FileErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    private static StoredFile CreateStoredFile() =>
        StoredFile.Create(
            "cv.pdf",
            "application/pdf",
            1024,
            "pdf",
            StorageProvider.AzureBlob,
            "documents",
            "documents/cv.pdf",
            FilePurpose.PersonnelDocument,
            FileUploadType.DirectUpload,
            OwnerUserId);

    private sealed class TestCurrentUserService(string userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;

        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; } = [];
        public IReadOnlyCollection<string> Permissions { get; } = [];
    }

    private sealed class TestFileRepository(StoredFile file) : IFileRepository
    {
        public Task<StoredFile?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken) =>
            Task.FromResult(publicId == file.PublicId ? file : null);

        public Task AddAsync(StoredFile file, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<StoredFile>> GetExpiredPendingUploadsAsync(
            DateTime olderThan,
            int batchSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
