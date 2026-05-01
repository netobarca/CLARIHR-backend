namespace CLARIHR.Application.Features.Files.Common;

public static class FileTelemetryEvents
{
    public const int UploadSessionCreated = 10_001;
    public const int UploadCompleted = 10_002;
    public const int UploadFailed = 10_003;
    public const int ReadUrlGenerated = 10_004;
    public const int FileDeleted = 10_005;
    public const int CleanupCycleCompleted = 10_006;
    public const int CleanupCycleFailed = 10_007;
}
