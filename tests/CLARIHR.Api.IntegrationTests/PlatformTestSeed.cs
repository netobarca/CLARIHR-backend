using System.Reflection;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure.Persistence;

namespace CLARIHR.Api.IntegrationTests;

internal static class PlatformTestSeed
{
    public static async Task<User> SeedLocalUserAsync(
        ApplicationDbContext dbContext,
        Guid userPublicId,
        string email,
        string passwordHash,
        string country = "SV")
    {
        var user = User.RegisterLocal("Platform", "Operator", email, passwordHash, country, "integration-tests");
        SetPublicId(user, userPublicId);
        dbContext.AuthUsers.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    public static async Task SeedPlatformOperatorAsync(
        ApplicationDbContext dbContext,
        Guid userPublicId,
        string email,
        string passwordHash,
        PlatformOperatorRole role = PlatformOperatorRole.Admin,
        string country = "SV")
    {
        var user = await SeedLocalUserAsync(dbContext, userPublicId, email, passwordHash, country);

        var platformOperator = PlatformOperator.Create(user.Id, role);
        dbContext.PlatformOperators.Add(platformOperator);
        await dbContext.SaveChangesAsync();
    }

    private static void SetPublicId(Entity entity, Guid publicId)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.PublicId), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [publicId]);
    }
}
