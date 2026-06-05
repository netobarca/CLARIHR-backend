using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CLARIHR.Application.Features.CompanyUsers.Common;

/// <summary>
/// Computes a deterministic WEAK ETag for the CompanyUser read projection. The resource has no
/// persisted concurrency token (it is assembled from User/auth_users + UserCompanyMembership +
/// IamUser, none of which carry a token), so concurrency is enforced by hashing the projection's
/// observable state. The hash covers every field a mutation can change — profile (email, first/last
/// name), status, and the full role set — so a role change rotates the ETag even though no single
/// aggregate timestamp is guaranteed to bump. The value is computed from the UNFILTERED projection
/// so it is independent of the caller's field-level permissions (two admins with different field
/// visibility must compute the same ETag for the same resource).
/// </summary>
public static class CompanyUserETag
{
    public static string Compute(CompanyUserResponse projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        // Length-prefixed encoding ("len:value;") makes field boundaries unambiguous without relying
        // on a separator that could appear inside a value (emails/names are arbitrary text).
        var builder = new StringBuilder();
        AppendField(builder, projection.Id.ToString("N"));
        AppendField(builder, projection.Email);
        AppendField(builder, projection.FirstName);
        AppendField(builder, projection.LastName);
        AppendField(builder, projection.Status?.ToString());

        // Order-independent: sort the role public-ids so an unordered role set yields a stable hash.
        var roleIds = projection.Roles
            .Select(role => role.Id.ToString("N"))
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        AppendField(builder, roleIds.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var roleId in roleIds)
        {
            AppendField(builder, roleId);
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Compares a client-supplied If-Match value (already stripped of the <c>W/</c> prefix and
    /// surrounding quotes by the API layer) against the current projection's computed ETag. The
    /// RFC 7232 wildcard <c>*</c> matches any existing representation (the caller's load already 404s
    /// when the resource does not exist), mirroring <c>ETagHeader.Matches</c>.
    /// </summary>
    public static bool Matches(string expectedETag, CompanyUserResponse currentProjection)
    {
        if (string.IsNullOrWhiteSpace(expectedETag))
        {
            return false;
        }

        return expectedETag == "*" ||
            string.Equals(expectedETag, Compute(currentProjection), StringComparison.Ordinal);
    }

    private static void AppendField(StringBuilder builder, string? value)
    {
        var text = value ?? string.Empty;
        builder.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text).Append(';');
    }
}
