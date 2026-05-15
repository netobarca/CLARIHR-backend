namespace CLARIHR.Application.Common.JsonPatch;

public static class JsonPatchHardening
{
    public const int MaxOperationsPerDocument = 50;
    public const long MaxRequestBodySizeBytes = 64 * 1024;

    public static string MaxOperationsMessage =>
        $"Patch document cannot contain more than {MaxOperationsPerDocument} operations.";
}
