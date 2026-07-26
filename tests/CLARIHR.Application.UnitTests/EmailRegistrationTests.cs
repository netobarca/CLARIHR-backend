using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Email;
using CLARIHR.Infrastructure.Companies;
using CLARIHR.Infrastructure.Email;
using CLARIHR.Infrastructure.Email.Providers;
using CLARIHR.Infrastructure.Email.Providers.Brevo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// A transport swap that only fails when the container is built is a runtime outage, not a compile
/// error, so the selector is resolved for real here — including the typed <c>HttpClient</c> graph,
/// which is the part that breaks silently when a registration is missing.
/// </summary>
public sealed class EmailRegistrationTests
{
    [Fact]
    public void AddEmailDelivery_WithoutConfiguration_ShouldDefaultToTheLoggingTransport()
    {
        // The default must never be a real provider: a freshly cloned environment, CI, and the
        // Backoffice host (which has no Email section at all) must not be able to send mail.
        using var provider = BuildProvider(new Dictionary<string, string?>());
        using var scope = provider.CreateScope();

        Assert.IsType<LoggingEmailSender>(scope.ServiceProvider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void AddEmailDelivery_WithBrevoProvider_ShouldOnlySwapTheTransport()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Brevo",
            ["Email:Brevo:ApiKey"] = "secret-key",
            ["Email:Brevo:SenderEmail"] = "no-reply@clarihr.test",
        });
        using var scope = provider.CreateScope();

        Assert.IsType<BrevoEmailSender>(scope.ServiceProvider.GetRequiredService<IEmailSender>());

        // Everything above the transport is unchanged — that is the whole point of the layering.
        Assert.IsType<TemplatedEmailService>(scope.ServiceProvider.GetRequiredService<IEmailService>());
        Assert.IsType<TemplatedAuthEmailService>(scope.ServiceProvider.GetRequiredService<IAuthEmailService>());
        Assert.IsType<EmailTemplateRenderer>(scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>());
    }

    [Fact]
    public void AddEmailDelivery_ShouldResolveTheSameContentPipelineForEveryProvider()
    {
        foreach (var providerName in EmailProviders.All)
        {
            using var provider = BuildProvider(new Dictionary<string, string?>
            {
                ["Email:Provider"] = providerName,
                ["Email:Brevo:ApiKey"] = "secret-key",
                ["Email:Brevo:SenderEmail"] = "no-reply@clarihr.test",
            });
            using var scope = provider.CreateScope();

            Assert.IsType<TemplatedEmailService>(scope.ServiceProvider.GetRequiredService<IEmailService>());
            Assert.IsType<TemplatedAuthEmailService>(scope.ServiceProvider.GetRequiredService<IAuthEmailService>());
            Assert.Equal(providerName, scope.ServiceProvider.GetRequiredService<IEmailSender>().Provider);
        }
    }

    [Fact]
    public void AddEmailDelivery_WithBrevoProviderAndNoApiKey_ShouldFailNamingTheEnvironmentVariable()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Brevo",
            ["Email:Brevo:SenderEmail"] = "no-reply@clarihr.test",
        });
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IEmailSender>());

        Assert.Contains("Email__Brevo__ApiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddEmailDelivery_WithBrevoProviderAndNoSender_ShouldFailNamingTheConfigurationKey()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Brevo",
            ["Email:Brevo:ApiKey"] = "secret-key",
        });
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IEmailSender>());

        Assert.Contains("SenderEmail", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddEmailDelivery_WithAnUnknownProvider_ShouldFailFastExplainingHowToAddOne()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddEmailDelivery(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Email:Provider"] = "SendGrid" })
                .Build()));

        Assert.Contains("SendGrid", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IEmailSender), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddEmailDelivery_ShouldAlwaysProvideTheInvitationLinkBuilderAndDispatcher()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());
        using var scope = provider.CreateScope();

        Assert.IsType<InvitationLinkBuilder>(scope.ServiceProvider.GetRequiredService<IInvitationLinkBuilder>());
        Assert.IsType<PendingEmailDispatcher>(scope.ServiceProvider.GetRequiredService<IPendingEmailDispatcher>());
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEmailDelivery(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services.BuildServiceProvider(validateScopes: true);
    }
}
