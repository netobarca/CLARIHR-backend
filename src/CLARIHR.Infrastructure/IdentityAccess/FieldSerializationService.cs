using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class FieldSerializationService : IFieldSerializationService
{
    public string? SerializeString(FieldAccessRule rule, string? value)
    {
        if (!rule.IsVisible)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value) || !rule.IsMasked)
        {
            return value;
        }

        return value.Contains('@', StringComparison.Ordinal)
            ? MaskEmail(value)
            : MaskValue(value);
    }

    public Guid? SerializeGuid(FieldAccessRule rule, Guid value) =>
        rule.IsVisible ? value : null;

    public TEnum? SerializeEnum<TEnum>(FieldAccessRule rule, TEnum value)
        where TEnum : struct, Enum =>
        rule.IsVisible ? value : null;

    private static string MaskEmail(string value)
    {
        var separatorIndex = value.IndexOf('@');
        if (separatorIndex <= 0)
        {
            return MaskValue(value);
        }

        var localPart = value[..separatorIndex];
        var domain = value[separatorIndex..];
        return $"{MaskValue(localPart)}{domain}";
    }

    private static string MaskValue(string value)
    {
        if (value.Length <= 2)
        {
            return new string('*', value.Length);
        }

        return $"{value[0]}{new string('*', Math.Max(1, value.Length - 2))}{value[^1]}";
    }
}
