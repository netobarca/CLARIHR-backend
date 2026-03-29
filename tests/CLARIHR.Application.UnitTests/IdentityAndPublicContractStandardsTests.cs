using CLARIHR.Application.Common.Contracts;
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
        Assert.Equal("permissionPublicIds", PublicContractNaming.GetExternalJsonName("PermissionIds", typeof(Guid[])));
        Assert.Equal("companyPublicId", PublicContractNaming.GetExternalJsonName("companyPublicId", typeof(Guid)));
        Assert.Equal("permissionPublicIds", PublicContractNaming.GetExternalJsonName("permissionPublicIds", typeof(Guid[])));
        Assert.Equal("LEGACY_CODE", PublicContractNaming.NormalizeCodeValue(" legacy_code "));
    }
}
