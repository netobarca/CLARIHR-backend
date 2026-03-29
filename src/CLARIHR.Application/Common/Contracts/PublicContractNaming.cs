using System.Text.Json;

namespace CLARIHR.Application.Common.Contracts;

public static class PublicContractNaming
{
    public static bool ShouldSuppressMember(string memberName) =>
        memberName.EndsWith("InternalId", StringComparison.OrdinalIgnoreCase);

    public static string GetExternalJsonName(string memberName, Type? memberType) =>
        ToCamelCase(GetExternalIdentifierName(memberName, memberType) ?? memberName);

    public static string? GetExternalRouteIdentifierName(string memberName, Type? memberType)
    {
        if (string.IsNullOrWhiteSpace(memberName) || memberType is null || ShouldSuppressMember(memberName))
        {
            return null;
        }

        if (IsAlreadyPublicIdentifierName(memberName) || IsAlreadyPublicIdentifierCollectionName(memberName))
        {
            return null;
        }

        if (!IsGuidLike(memberType))
        {
            return null;
        }

        if (memberName.Equals("CompanyId", StringComparison.Ordinal))
        {
            return "CompanyPublicId";
        }

        if (memberName.Equals("companyId", StringComparison.Ordinal))
        {
            return "companyPublicId";
        }

        return memberName.Length > 0 && char.IsUpper(memberName[0])
            ? "PublicId"
            : "publicId";
    }

    public static string? GetExternalIdentifierName(string memberName, Type? memberType)
    {
        if (string.IsNullOrWhiteSpace(memberName) || memberType is null || ShouldSuppressMember(memberName))
        {
            return null;
        }

        if (IsAlreadyPublicIdentifierName(memberName) || IsAlreadyPublicIdentifierCollectionName(memberName))
        {
            return null;
        }

        if (IsGuidLike(memberType))
        {
            return TransformGuidIdentifier(memberName);
        }

        if (IsGuidCollectionLike(memberType))
        {
            return TransformGuidCollectionIdentifier(memberName);
        }

        return null;
    }

    public static bool IsCodeProperty(string memberName, Type memberType) =>
        memberType == typeof(string) &&
        (memberName.Equals("Code", StringComparison.Ordinal) ||
         memberName.Equals("NormalizedCode", StringComparison.Ordinal));

    public static string NormalizeCodeValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? value ?? string.Empty : value.Trim().ToUpperInvariant();

    public static string ToCamelCase(string name) =>
        JsonNamingPolicy.CamelCase.ConvertName(name);

    public static bool IsGuidLike(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType == typeof(Guid);
    }

    public static bool IsGuidCollectionLike(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        if (type.IsArray)
        {
            return IsGuidLike(type.GetElementType()!);
        }

        var enumerableType = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces()
                .FirstOrDefault(candidate =>
                    candidate.IsGenericType &&
                    candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableType is not null && IsGuidLike(enumerableType.GetGenericArguments()[0]);
    }

    private static bool IsAlreadyPublicIdentifierName(string memberName) =>
        memberName.Equals("PublicId", StringComparison.Ordinal) ||
        memberName.Equals("publicId", StringComparison.Ordinal) ||
        memberName.EndsWith("PublicId", StringComparison.Ordinal);

    private static bool IsAlreadyPublicIdentifierCollectionName(string memberName) =>
        memberName.Equals("PublicIds", StringComparison.Ordinal) ||
        memberName.Equals("publicIds", StringComparison.Ordinal) ||
        memberName.EndsWith("PublicIds", StringComparison.Ordinal);

    private static string? TransformGuidIdentifier(string memberName)
    {
        if (memberName.Equals("Id", StringComparison.Ordinal))
        {
            return "PublicId";
        }

        if (memberName.Equals("id", StringComparison.Ordinal))
        {
            return "publicId";
        }

        if (memberName.EndsWith("Id", StringComparison.Ordinal))
        {
            return string.Concat(memberName.AsSpan(0, memberName.Length - 2), "PublicId");
        }

        if (memberName.EndsWith("id", StringComparison.Ordinal))
        {
            return string.Concat(memberName.AsSpan(0, memberName.Length - 2), "PublicId");
        }

        return null;
    }

    private static string? TransformGuidCollectionIdentifier(string memberName)
    {
        if (memberName.Equals("Ids", StringComparison.Ordinal))
        {
            return "PublicIds";
        }

        if (memberName.Equals("ids", StringComparison.Ordinal))
        {
            return "publicIds";
        }

        if (memberName.EndsWith("Ids", StringComparison.Ordinal))
        {
            return string.Concat(memberName.AsSpan(0, memberName.Length - 3), "PublicIds");
        }

        if (memberName.EndsWith("ids", StringComparison.Ordinal))
        {
            return string.Concat(memberName.AsSpan(0, memberName.Length - 3), "PublicIds");
        }

        return null;
    }
}
