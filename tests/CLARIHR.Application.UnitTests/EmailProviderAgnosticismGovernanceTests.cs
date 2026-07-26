using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Email;
using CLARIHR.Infrastructure.Email;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Pins the property that makes adding a second provider cheap: **all provider-specific code lives
/// under <c>CLARIHR.Infrastructure.Email.Providers</c>, and the only thing it may implement is
/// <see cref="IEmailSender"/>.**
///
/// <para>The first version of this feature failed exactly here: the mapping from "invitation" to its
/// content lived inside the Brevo classes, so a second provider would have had to copy the whole
/// content pipeline. These tests make that regression fail the build instead of surfacing months
/// later, when someone actually tries to add SendGrid.</para>
/// </summary>
public sealed class EmailProviderAgnosticismGovernanceTests
{
    private const string ProvidersNamespace = "CLARIHR.Infrastructure.Email.Providers";

    private static readonly Assembly InfrastructureAssembly = typeof(EmailProviders).Assembly;

    [Fact]
    public void ProviderCode_MustOnlyImplementTheTransportSeam()
    {
        // A provider that also implements IEmailService or IAuthEmailService is re-implementing the
        // content pipeline — the exact duplication this layering exists to prevent.
        var offenders = ProviderTypes()
            .Where(static type =>
                typeof(IEmailService).IsAssignableFrom(type) ||
                typeof(IAuthEmailService).IsAssignableFrom(type) ||
                typeof(IEmailTemplateRenderer).IsAssignableFrom(type) ||
                typeof(IEmailTemplateSource).IsAssignableFrom(type))
            .Select(static type => type.FullName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Provider code may only implement {nameof(IEmailSender)}. These types reach into the " +
            $"content pipeline: {string.Join(", ", offenders)}. Subjects, templates and placeholder " +
            $"values are ours and shared by every transport.");
    }

    [Fact]
    public void TheContentPipeline_MustNotReferenceAnyProviderType()
    {
        // The reverse direction: nothing outside Providers may name a provider type, otherwise the
        // "swap one class" promise quietly stops being true.
        var offenders = InfrastructureAssembly
            .GetTypes()
            .Where(static type => type.Namespace is not null &&
                                  type.Namespace.StartsWith("CLARIHR.Infrastructure.Email", StringComparison.Ordinal) &&
                                  !IsProviderNamespace(type.Namespace))
            // The composition root is the one allowed exception: selecting the transport is its job.
            .Where(static type => type.Name != "EmailRegistration")
            .Where(static type => type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(static constructor => constructor.GetParameters())
                .Select(static parameter => parameter.ParameterType)
                .Concat(type.GetProperties().Select(static property => property.PropertyType))
                .Any(static dependency => dependency.Namespace is not null && IsProviderNamespace(dependency.Namespace)))
            .Select(static type => type.FullName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"These types depend on provider-specific code: {string.Join(", ", offenders)}. Everything " +
            $"outside {ProvidersNamespace} must talk to {nameof(IEmailSender)} only.");
    }

    [Fact]
    public void EveryDeclaredProvider_ShouldHaveATransportImplementation()
    {
        // Keeps the advertised list honest: a name in EmailProviders.All with no IEmailSender behind
        // it is a config value that fails only at runtime.
        var implementedProviders = ProviderTypes()
            .Where(static type => typeof(IEmailSender).IsAssignableFrom(type))
            .Count();

        Assert.Equal(EmailProviders.All.Count, implementedProviders);
    }

    [Fact]
    public void TheUseCaseServices_ShouldBeSingleImplementations()
    {
        // One IEmailService for every provider — not one per provider. If this count ever exceeds
        // one, the content pipeline has been forked per transport again.
        Assert.Single(Implementations<IEmailService>());
        Assert.Single(Implementations<IAuthEmailService>());
    }

    private static bool IsProviderNamespace(string @namespace) =>
        @namespace == ProvidersNamespace ||
        @namespace.StartsWith(ProvidersNamespace + ".", StringComparison.Ordinal);

    private static IEnumerable<Type> ProviderTypes() =>
        InfrastructureAssembly
            .GetTypes()
            .Where(static type => type is { IsClass: true, IsAbstract: false })
            .Where(static type => type.Namespace is not null && IsProviderNamespace(type.Namespace));

    private static Type[] Implementations<TContract>() =>
        InfrastructureAssembly
            .GetTypes()
            .Where(static type => type is { IsClass: true, IsAbstract: false })
            .Where(static type => typeof(TContract).IsAssignableFrom(type))
            .ToArray();
}
