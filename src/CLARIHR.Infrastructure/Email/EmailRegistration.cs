using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Email;
using CLARIHR.Infrastructure.Companies;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Email.Providers;
using CLARIHR.Infrastructure.Email.Providers.Brevo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Email;

/// <summary>
/// Composition root for e-mail.
///
/// <para>Read the two halves separately, because that separation is the whole design: everything in
/// <see cref="AddProviderAgnosticEmail"/> is written once and never changes when a provider is added;
/// <see cref="AddTransport"/> is the only place a provider name appears. Adding SendGrid means one
/// new <see cref="IEmailSender"/> and one more branch there — no new template, no new service, no
/// change to any handler.</para>
/// </summary>
internal static class EmailRegistration
{
    public static IServiceCollection AddEmailDelivery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<InvitationOptions>(configuration.GetSection(InvitationOptions.SectionName));

        services.AddProviderAgnosticEmail();
        services.AddTransport(configuration);

        return services;
    }

    /// <summary>Templates, rendering, buffering and the two use-case services. Provider-free.</summary>
    private static void AddProviderAgnosticEmail(this IServiceCollection services)
    {
        services.AddSingleton<IEmailTemplateSource, EmbeddedEmailTemplateSource>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();

        services.AddScoped<IInvitationLinkBuilder, InvitationLinkBuilder>();
        services.AddScoped<IPendingEmailDispatcher, PendingEmailDispatcher>();

        services.AddScoped<IEmailService, TemplatedEmailService>();
        services.AddScoped<IAuthEmailService, TemplatedAuthEmailService>();
    }

    /// <summary>The only provider-aware code in the system.</summary>
    private static void AddTransport(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[$"{EmailOptions.SectionName}:Provider"];

        // Default is Logging on purpose: a fresh clone, CI and the Backoffice host (which has no
        // Email section at all) must not be able to mail real people by omission.
        if (string.IsNullOrWhiteSpace(provider) ||
            string.Equals(provider, EmailProviders.Logging, StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
            return;
        }

        if (string.Equals(provider, EmailProviders.Brevo, StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<BrevoOptions>(configuration.GetSection(BrevoOptions.SectionName));
            services.AddHttpClient<IEmailSender, BrevoEmailSender>(static (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<BrevoOptions>>().Value;
                ValidateBrevoOrThrow(options);

                client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
                client.Timeout = options.NormalizedTimeout;
                client.DefaultRequestHeaders.Add("api-key", options.ApiKey);
                client.DefaultRequestHeaders.Add("accept", "application/json");
            });
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported e-mail provider '{provider}' (config '{EmailOptions.SectionName}:Provider'). " +
            $"Supported providers: {string.Join(", ", EmailProviders.All)}. To add one, implement " +
            $"{nameof(IEmailSender)} and register it in {nameof(EmailRegistration)}.{nameof(AddTransport)}.");
    }

    /// <summary>
    /// Runs lazily, on the first send rather than at startup: a missing mail credential must not stop
    /// the whole API from booting. The messages name the exact key so the fix is unambiguous.
    /// </summary>
    private static void ValidateBrevoOrThrow(BrevoOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Brevo API key is not configured. Set the environment variable 'Email__Brevo__ApiKey' " +
                "(Application Setting in Azure). It must never live in appsettings*.json.");
        }

        if (string.IsNullOrWhiteSpace(options.SenderEmail))
        {
            throw new InvalidOperationException(
                $"Brevo sender address is not configured ('{BrevoOptions.SectionName}:SenderEmail'). " +
                "It must be a sender verified in the Brevo account, on a domain with SPF/DKIM configured.");
        }
    }

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";
}
