using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CLARIHR.Infrastructure.Configuration;

public static class JwtConfigurationDiagnostics
{
    public static SymmetricSecurityKey CreateSigningKey(string signingKey) =>
        new(Encoding.UTF8.GetBytes(signingKey))
        {
            KeyId = ComputeSigningKeyFingerprint(signingKey)
        };

    public static string ComputeSigningKeyFingerprint(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return "missing";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(signingKey));
        return Convert.ToHexString(hash[..6]);
    }

    public static JwtTokenSummary? TryReadSummary(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return new JwtTokenSummary(
                jwt.Header.Alg,
                jwt.Header.Kid,
                jwt.Issuer,
                jwt.Audiences.FirstOrDefault(),
                jwt.Claims.FirstOrDefault(static claim => claim.Type == "client_type")?.Value,
                jwt.ValidFrom,
                jwt.ValidTo);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record JwtTokenSummary(
    string? Algorithm,
    string? KeyId,
    string? Issuer,
    string? Audience,
    string? ClientType,
    DateTime ValidFromUtc,
    DateTime ExpiresAtUtc);
