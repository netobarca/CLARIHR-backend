using CLARIHR.Application;
using CLARIHR.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Application.UnitTests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void ServiceProvider_BuildsWithoutCircularDependencies()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=/tmp;Port=5432;Database=clarihr",
                ["Authentication:Google:ClientId"] = "dev-client-id",
                ["Authentication:Jwt:Issuer"] = "clarihr-local",
                ["Authentication:Jwt:Audience"] = "clarihr-local",
                ["Authentication:Jwt:SigningKey"] = "unit-test-only-signing-key-do-not-use-in-any-real-environment"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(serviceProvider);
    }
}
