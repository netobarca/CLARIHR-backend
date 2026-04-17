using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Preferences;

public sealed class UserPreference : AuditableEntity
{
    private UserPreference()
    {
    }

    private UserPreference(Guid publicId, long userId, string language)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be greater than zero.");
        }

        PublicId = publicId;
        UserId = userId;
        Language = PreferenceNormalization.NormalizeLanguage(language);
    }

    public long UserId { get; private set; }

    public string Language { get; private set; } = "en";

    public static UserPreference Create(long userId, string language = "en") =>
        new(Guid.NewGuid(), userId, language);

    public void UpdateLanguage(string language)
    {
        Language = PreferenceNormalization.NormalizeLanguage(language);
    }
}
