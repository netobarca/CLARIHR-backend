using CLARIHR.Application;
using CLARIHR.Infrastructure;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Application.UnitTests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void ServiceProvider_BuildsWithoutCircularDependencies()
    {
        var services = CreateServices();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(serviceProvider);
    }

    /// <summary>
    /// Guards the one cycle the test above cannot see.
    ///
    /// <c>ValidateOnBuild</c> only walks call sites it can analyse statically, and the <c>AddDbContext</c>
    /// options factory is an opaque lambda. So a service resolved inside that lambda — an EF interceptor,
    /// typically — may depend on <c>ApplicationDbContext</c> itself and close a cycle the container reports
    /// as perfectly healthy.
    ///
    /// The failure mode is silence, not an exception. On resolution one thread recurses through the cycle
    /// holding the container's scope lock; when its stack runs low the container's own StackGuard hands the
    /// continuation to a second thread, which blocks acquiring that same lock. Both threads wait on each
    /// other forever, at 0% CPU, with nothing written anywhere. It cost a full session of debugging once.
    ///
    /// Resolving on a background thread with a deadline is what turns that silence back into a red test.
    /// </summary>
    [Fact]
    public async Task ResolvingTheDbContext_Completes_InsteadOfDeadlockingOnACycleThroughItsOptionsFactory()
    {
        var services = CreateServices();

        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var scope = serviceProvider.CreateScope();
        var resolution = Task.Run(() => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var finished = await Task.WhenAny(resolution, Task.Delay(TimeSpan.FromSeconds(30)));

        if (!ReferenceEquals(finished, resolution))
        {
            // Deliberately leaked, and this is the whole point: the wedged resolution thread still holds the
            // container's scope lock, so disposing here would block on it and the failure below would never
            // be reported — the run would just hang, which is the very symptom being guarded against.
            Assert.Fail(
                "Resolving ApplicationDbContext never returned. Something reachable from the AddDbContext " +
                "options factory — an interceptor, most likely — depends on ApplicationDbContext itself. " +
                "Break the cycle by giving that dependency a form which never needs a DbContext.");
        }

        Assert.NotNull(await resolution);

        scope.Dispose();
        serviceProvider.Dispose();
    }

    private static ServiceCollection CreateServices()
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

        return services;
    }
}
