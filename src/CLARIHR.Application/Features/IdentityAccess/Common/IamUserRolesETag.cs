using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CLARIHR.Application.Features.IdentityAccess.Contracts;

namespace CLARIHR.Application.Features.IdentityAccess.Common;

/// <summary>
/// Computes a deterministic WEAK ETag for the IAM user-roles projection. The <c>iam_users</c> aggregate
/// has ~8 writers (CompanyUsers create/update/(de)reactivate, invitation acceptance, provisioning,
/// role-user sync), so a strong persisted token there would be expensive and contended; the user-roles
/// write surface instead enforces concurrency by hashing the projection's observable state. The hash
/// covers every field the write can change — profile (email, first/last name), active status, and the
/// full role set — so a role change rotates the ETag. Mirrors
/// <see cref="CLARIHR.Application.Features.CompanyUsers.Common.CompanyUserETag"/> (the Identity weak-ETag
/// precedent) and is computed from the UNFILTERED projection so it is independent of the caller.
/// </summary>
public static class IamUserRolesETag
{
    public static string Compute(IamUserResponse projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        // Length-prefixed encoding ("len:value;") makes field boundaries unambiguous without relying
        // on a separator that could appear inside a value (emails/names are arbitrary text).
        var builder = new StringBuilder();
        AppendField(builder, projection.Id.ToString("N"));
        AppendField(builder, projection.Email);
        AppendField(builder, projection.FirstName);
        AppendField(builder, projection.LastName);
        AppendField(builder, projection.IsActive ? "1" : "0");

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
    /// when the resource does not exist).
    /// </summary>
    public static bool Matches(string expectedETag, IamUserResponse currentProjection)
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
