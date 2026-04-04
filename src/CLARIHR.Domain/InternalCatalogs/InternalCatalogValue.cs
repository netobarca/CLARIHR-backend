using System.Globalization;
using System.Text;
using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.InternalCatalogs;

public sealed class InternalCatalogValue : AuditableEntity
{
    private const int MaxCatalogKeyLength = 120;
    private const int MaxValueLength = 200;

    private InternalCatalogValue()
    {
    }

    private InternalCatalogValue(
        Guid publicId,
        string catalogKey,
        string value,
        Guid createdByUserPublicId)
    {
        PublicId = publicId;
        CatalogKey = InternalCatalogNormalization.NormalizeCatalogKey(catalogKey);
        SetValue(value);
        CreatedByUserPublicId = createdByUserPublicId != Guid.Empty
            ? createdByUserPublicId
            : throw new ArgumentException("CreatedByUserPublicId cannot be empty.", nameof(createdByUserPublicId));
        IsActive = true;
        UsageCount = 0;
    }

    public string CatalogKey { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public string NormalizedValue { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public Guid CreatedByUserPublicId { get; private set; }

    public int UsageCount { get; private set; }

    public DateTime? LastUsedAtUtc { get; private set; }

    public static InternalCatalogValue Create(
        string catalogKey,
        string value,
        Guid createdByUserPublicId) =>
        new(Guid.NewGuid(), catalogKey, value, createdByUserPublicId);

    public void RegisterUsage(DateTime usedAtUtc)
    {
        UsageCount++;
        LastUsedAtUtc = usedAtUtc.Kind == DateTimeKind.Utc
            ? usedAtUtc
            : usedAtUtc.ToUniversalTime();
    }

    public void Activate() => IsActive = true;

    public void Inactivate() => IsActive = false;

    private void SetValue(string value)
    {
        Value = InternalCatalogNormalization.CleanValue(value, MaxValueLength);
        NormalizedValue = InternalCatalogNormalization.NormalizeValue(value, MaxValueLength);
    }

    public static class InternalCatalogNormalization
    {
        public static string NormalizeCatalogKey(string catalogKey)
        {
            if (string.IsNullOrWhiteSpace(catalogKey))
            {
                throw new ArgumentException("CatalogKey cannot be empty.", nameof(catalogKey));
            }

            var cleaned = catalogKey.Trim().ToLowerInvariant();
            return cleaned.Length <= MaxCatalogKeyLength
                ? cleaned
                : throw new ArgumentOutOfRangeException(nameof(catalogKey), $"CatalogKey cannot exceed {MaxCatalogKeyLength} characters.");
        }

        public static string CleanValue(string value, int maxLength = MaxValueLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(value));
            }

            var collapsed = CollapseWhitespace(value.Trim());
            return collapsed.Length <= maxLength
                ? collapsed
                : throw new ArgumentOutOfRangeException(nameof(value), $"Value cannot exceed {maxLength} characters.");
        }

        public static string NormalizeValue(string value, int maxLength = MaxValueLength)
        {
            var cleaned = CleanValue(value, maxLength);
            var decomposed = cleaned.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(char.IsLetterOrDigit(character)
                    ? char.ToUpperInvariant(character)
                    : ' ');
            }

            return CollapseWhitespace(builder.ToString());
        }

        private static string CollapseWhitespace(string value)
        {
            var builder = new StringBuilder(value.Length);
            var previousWasWhitespace = false;

            foreach (var character in value)
            {
                if (char.IsWhiteSpace(character))
                {
                    if (previousWasWhitespace)
                    {
                        continue;
                    }

                    builder.Append(' ');
                    previousWasWhitespace = true;
                    continue;
                }

                builder.Append(character);
                previousWasWhitespace = false;
            }

            return builder.ToString().Trim();
        }
    }
}
