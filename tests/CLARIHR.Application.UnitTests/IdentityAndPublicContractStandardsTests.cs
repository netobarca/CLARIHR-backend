using System.Reflection;
using CLARIHR.Application.Common.Contracts;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.IdentityAccess;

namespace CLARIHR.Application.UnitTests;

public sealed class IdentityAndPublicContractStandardsTests
{
    [Fact]
    public void IamUser_CreateLinked_ShouldGenerateIndependentPublicId_AndTrackLinkedUserPublicId()
    {
        var linkedUserPublicId = Guid.NewGuid();

        var iamUser = IamUser.CreateLinked(
            linkedUserPublicId,
            " Ana ",
            " Mendoza ",
            " ana.mendoza@clarihr.test ",
            isActive: true);

        Assert.NotEqual(Guid.Empty, iamUser.PublicId);
        Assert.NotEqual(linkedUserPublicId, iamUser.PublicId);
        Assert.Equal(linkedUserPublicId, iamUser.LinkedUserPublicId);
        Assert.Equal("Ana", iamUser.FirstName);
        Assert.Equal("Mendoza", iamUser.LastName);
        Assert.Equal("ana.mendoza@clarihr.test", iamUser.Email);
        Assert.Equal("ANA.MENDOZA@CLARIHR.TEST", iamUser.NormalizedEmail);
        Assert.True(iamUser.IsActive);
    }

    [Fact]
    public void PublicContractNaming_ShouldExposeOnlyPublicIdentifiers_AndUppercaseCodes()
    {
        Assert.True(PublicContractNaming.ShouldSuppressMember("InternalId"));
        Assert.Equal("publicId", PublicContractNaming.GetExternalJsonName("Id", typeof(Guid)));
        Assert.Equal("companyPublicId", PublicContractNaming.GetExternalJsonName("CompanyId", typeof(Guid)));
        Assert.Equal("publicId", PublicContractNaming.GetExternalRouteIdentifierName("roleId", typeof(Guid), "api/iam/roles/{roleId:guid}"));
        Assert.Equal("companyPublicId", PublicContractNaming.GetExternalRouteIdentifierName("companyId", typeof(Guid), "api/v1/companies/{companyId:guid}/job-profiles"));
        Assert.Equal("publicId", PublicContractNaming.GetExternalRouteIdentifierName("companyId", typeof(Guid), "api/account/companies/{companyId:guid}"));
        Assert.Equal("publicId", PublicContractNaming.GetExternalRouteIdentifierName("id", typeof(Guid), "api/v1/job-profiles/{id:guid}"));
        Assert.Equal("permissionPublicIds", PublicContractNaming.GetExternalJsonName("PermissionIds", typeof(Guid[])));
        Assert.Equal("companyPublicId", PublicContractNaming.GetExternalJsonName("companyPublicId", typeof(Guid)));
        Assert.Equal("permissionPublicIds", PublicContractNaming.GetExternalJsonName("permissionPublicIds", typeof(Guid[])));
        Assert.Equal("LEGACY_CODE", PublicContractNaming.NormalizeCodeValue(" legacy_code "));
    }

    /// <summary>
    /// Regression guard for foundation §10.3 and technical-debt §2.5: no type used as a
    /// CQRS response payload (<see cref="IQuery{TResponse}"/> / <see cref="ICommand{TResponse}"/>,
    /// i.e. the serialized public contract surface) — nor anything reachable through its
    /// object graph — may declare an integral member named <c>Id</c> or ending in
    /// <c>InternalId</c>. The internal BIGINT primary key must stay in a separate
    /// internal-only DTO (e.g. <c>CatalogReferenceInternal</c>,
    /// <c>EducationCatalogLookupInternal</c>); the public boundary exposes the
    /// <see cref="Guid"/> public id only. This enforces the DTO-split discipline at the
    /// type level instead of relying solely on the serializer suppression
    /// (<c>PublicContractNaming.ShouldSuppressMember</c>), which is a name-fragile net.
    /// </summary>
    [Fact]
    public void CqrsResponseContracts_MustNotExposeIntegralIdOrInternalId_PerFoundation10_3()
    {
        var applicationAssembly = typeof(IQuery<>).Assembly;

        var integralTypes = new HashSet<Type>
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong)
        };

        var responseRoots = applicationAssembly.GetTypes()
            .SelectMany(type => type.GetInterfaces())
            .Where(@interface => @interface.IsGenericType &&
                (@interface.GetGenericTypeDefinition() == typeof(IQuery<>) ||
                 @interface.GetGenericTypeDefinition() == typeof(ICommand<>)))
            .Select(@interface => @interface.GetGenericArguments()[0]);

        static IEnumerable<Type> Expand(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsArray)
            {
                yield return type.GetElementType()!;
                yield break;
            }

            yield return type;
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    yield return argument;
                }
            }
        }

        static bool IsClarihrComposite(Type type) =>
            type.Namespace is not null &&
            type.Namespace.StartsWith("CLARIHR", StringComparison.Ordinal) &&
            !type.IsEnum;

        var visited = new HashSet<Type>();
        var queue = new Queue<Type>(responseRoots.SelectMany(Expand));
        var violations = new List<string>();

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!IsClarihrComposite(type) || !visited.Add(type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                if (integralTypes.Contains(propertyType) &&
                    (property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.EndsWith("InternalId", StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{type.FullName}.{property.Name} : {propertyType.Name}");
                }

                foreach (var reachable in Expand(property.PropertyType))
                {
                    queue.Enqueue(reachable);
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Foundation §10.3 / technical-debt §2.5: CQRS response contracts must not expose an integral " +
            "'Id' or '*InternalId' (enumeration/IDOR risk). Keep the BIGINT in a separate internal-only DTO " +
            "and expose the Guid public id only. Offending members:\n  " +
            string.Join("\n  ", violations.Distinct().OrderBy(static entry => entry, StringComparer.Ordinal)));
    }
}
