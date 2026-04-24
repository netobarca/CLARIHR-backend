using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Auth;

public sealed class PasswordResetToken : AuditableEntity
{
    private PasswordResetToken()
    {
    }

    private PasswordResetToken(long userId, string tokenHash, DateTime expirationUtc)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be a persisted positive identifier.");
        }

        UserId = userId;
        TokenHash = AuthNormalization.Clean(tokenHash, nameof(tokenHash));
        ExpirationUtc = expirationUtc;
    }

    public long UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpirationUtc { get; private set; }

    public bool IsUsed { get; private set; }

    public DateTime? UsedUtc { get; private set; }

    public DateTime? RevokedUtc { get; private set; }

    public static PasswordResetToken Issue(long userId, string tokenHash, DateTime expirationUtc) =>
        new(userId, tokenHash, expirationUtc);

    public bool IsActive(DateTime utcNow) =>
        !IsUsed &&
        RevokedUtc is null &&
        ExpirationUtc > utcNow;

    public void MarkUsed(DateTime usedUtc)
    {
        if (IsUsed)
        {
            return;
        }

        IsUsed = true;
        UsedUtc = usedUtc;
    }

    public void Revoke(DateTime revokedUtc)
    {
        if (RevokedUtc.HasValue)
        {
            return;
        }

        RevokedUtc = revokedUtc;
    }
}
