namespace CLARIHR.Domain.Auth;

public enum AuthClientType
{
    Core = 1,
    Platform = 2
}

public static class AuthClientTypeExtensions
{
    public static string ToClaimValue(this AuthClientType clientType) =>
        clientType switch
        {
            AuthClientType.Core => "core",
            AuthClientType.Platform => "platform",
            _ => throw new ArgumentOutOfRangeException(nameof(clientType), clientType, "Unsupported auth client type.")
        };

    public static bool TryParseClaimValue(string? value, out AuthClientType clientType)
    {
        clientType = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        clientType = normalized switch
        {
            "core" => AuthClientType.Core,
            "platform" => AuthClientType.Platform,
            _ => default
        };

        return normalized is "core" or "platform";
    }
}
