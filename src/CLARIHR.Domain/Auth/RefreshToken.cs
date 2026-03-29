using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Auth;

public sealed class RefreshToken : AuditableEntity
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid familyId,
        long userId,
        AuthClientType clientType,
        string tokenHash,
        DateTime expiresUtc)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be a persisted positive identifier.");
        }

        FamilyId = familyId == Guid.Empty ? throw new ArgumentException("Family id cannot be empty.", nameof(familyId)) : familyId;
        UserId = userId;
        ClientType = clientType;
        TokenHash = AuthNormalization.Clean(tokenHash, nameof(tokenHash));
        ExpiresUtc = expiresUtc;
    }

    public Guid FamilyId { get; private set; }

    public long UserId { get; private set; }

    public AuthClientType ClientType { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresUtc { get; private set; }

    public DateTime? RevokedUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public string? RevocationReason { get; private set; }

    public static RefreshToken Issue(
        long userId,
        AuthClientType clientType,
        string tokenHash,
        DateTime expiresUtc,
        Guid? familyId = null) =>
        new(
            familyId ?? Guid.NewGuid(),
            userId,
            clientType,
            tokenHash,
            expiresUtc);

    public bool IsActive(DateTime utcNow) =>
        RevokedUtc is null && ExpiresUtc > utcNow;

    public bool HasBeenRotated =>
        RevokedUtc is not null && !string.IsNullOrWhiteSpace(ReplacedByTokenHash);

    public RefreshToken Rotate(
        string newTokenHash,
        DateTime newExpiresUtc,
        DateTime revokedUtc)
    {
        Revoke(revokedUtc, "rotated", newTokenHash);

        return Issue(UserId, ClientType, newTokenHash, newExpiresUtc, FamilyId);
    }

    public void Revoke(DateTime revokedUtc, string reason, string? replacedByTokenHash = null)
    {
        if (RevokedUtc is not null)
        {
            return;
        }

        RevokedUtc = revokedUtc;
        RevocationReason = AuthNormalization.Clean(reason, nameof(reason));
        ReplacedByTokenHash = AuthNormalization.CleanOptional(replacedByTokenHash);
    }
}
