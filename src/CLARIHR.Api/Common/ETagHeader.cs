namespace CLARIHR.Api.Common;

internal static class ETagHeader
{
    public const string HeaderName = "ETag";

    public static string Format(Guid token) => $"\"{token:D}\"";
}
