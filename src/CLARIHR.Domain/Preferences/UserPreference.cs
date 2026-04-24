using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Preferences;

public sealed class UserPreference : AuditableEntity
{
    private readonly List<UserSocialLink> _socialLinks = [];

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

    public IReadOnlyCollection<UserSocialLink> SocialLinks => _socialLinks.AsReadOnly();

    public static UserPreference Create(long userId, string language = "en") =>
        new(Guid.NewGuid(), userId, language);

    public void UpdateLanguage(string language)
    {
        Language = PreferenceNormalization.NormalizeLanguage(language);
    }

    public void ReplaceSocialLinks(IEnumerable<UserSocialLinkInput> socialLinks)
    {
        ArgumentNullException.ThrowIfNull(socialLinks);

        var normalizedLinks = socialLinks
            .Select(static socialLink => new UserSocialLink(
                socialLink.ProviderCode,
                socialLink.Url))
            .ToArray();

        if (normalizedLinks.Length > 10)
        {
            throw new ArgumentException("A maximum of 10 social links is allowed.", nameof(socialLinks));
        }

        var uniqueProviderCodes = normalizedLinks
            .Select(static socialLink => socialLink.ProviderCode)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (uniqueProviderCodes != normalizedLinks.Length)
        {
            throw new ArgumentException("Provider codes must be unique.", nameof(socialLinks));
        }

        _socialLinks.Clear();

        for (var index = 0; index < normalizedLinks.Length; index++)
        {
            normalizedLinks[index].AssignSortOrder(index);
            _socialLinks.Add(normalizedLinks[index]);
        }
    }
}

public sealed record UserSocialLinkInput(string ProviderCode, string Url);
