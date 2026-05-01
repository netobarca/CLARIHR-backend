namespace CLARIHR.Api.Contracts.Files;

public sealed record CreateUploadSessionRequest(
    string FileName,
    string ContentType,
    long SizeBytes,
    string Purpose,
    Guid? EntityId);
