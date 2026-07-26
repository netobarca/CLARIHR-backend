using System.Reflection;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Common.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Guardrail for the 2026-07-26 defect: three <c>[AllowAnonymous]</c> handlers called
/// <c>IAuditService.LogAsync</c>, which resolves the tenant from the ambient <c>ITenantContext</c>.
/// An anonymous request carries no JWT, so there is no <c>tid</c> claim, so the tenant is always
/// null and the call always threw — a deterministic 500 that rolled back the invitation activation.
///
/// <para>Source A ("ought"): the anonymous surface is read off <c>AuthController</c> by reflection,
/// not enumerated here — <see cref="AuthControllerSurface_IsAnonymousExceptLogout"/> pins that the
/// controller really is anonymous end-to-end, so the namespace rule below stays true as the
/// controller evolves.</para>
///
/// <para>Source B ("is"): no CQRS handler serving that surface may take a dependency on the
/// tenant-scoped <see cref="IAuditService"/>, except the handlers that resolve a tenant themselves
/// and log through <c>LogForTenantAsync</c> — those are allow-listed with their reason.</para>
/// </summary>
public sealed class AnonymousEndpointAuditGovernanceTests
{
    private const string AnonymousHandlerNamespace = "CLARIHR.Application.Features.Auth";

    private static readonly Assembly ApplicationAssembly = typeof(IQuery<>).Assembly;
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    /// <summary>
    /// The single authenticated action on the otherwise anonymous <c>AuthController</c>.
    /// </summary>
    private const string AuthenticatedAction = "Logout";

    /// <summary>
    /// Handlers allowed to inject <see cref="IAuditService"/> despite serving an anonymous endpoint,
    /// because they resolve the tenant themselves and audit via <c>LogForTenantAsync</c> — never via
    /// the ambient context. Adding an entry here is a deliberate decision, not a formality: verify the
    /// handler never reaches <c>LogAsync</c>.
    /// </summary>
    private static readonly HashSet<string> ExplicitTenantAuditHandlers = new(StringComparer.Ordinal)
    {
        // Audits against resolution.CompanyPublicId — the company the invitation was issued for.
        "AcceptCompanyUserInvitationCommandHandler",
    };

    [Fact]
    public void AuthControllerSurface_IsAnonymousExceptLogout()
    {
        var controller = ApiAssembly.GetType("CLARIHR.Api.Controllers.AuthController");
        Assert.NotNull(controller);

        var actions = controller!
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();

        Assert.NotEmpty(actions);

        var authenticated = actions
            .Where(static method => method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is null)
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([AuthenticatedAction], authenticated);
    }

    [Fact]
    public void AnonymousAuthHandlers_MustNotDependOnTenantScopedAuditService()
    {
        var offenders = ApplicationAssembly
            .GetTypes()
            .Where(IsCqrsHandler)
            .Where(static type => type.Namespace is not null &&
                                  (type.Namespace == AnonymousHandlerNamespace ||
                                   type.Namespace.StartsWith(AnonymousHandlerNamespace + ".", StringComparison.Ordinal)))
            .Where(static type => !ExplicitTenantAuditHandlers.Contains(type.Name))
            .Where(static type => type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(static constructor => constructor.GetParameters())
                .Any(static parameter => parameter.ParameterType == typeof(IAuditService)))
            .Select(static type => type.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"These handlers serve [AllowAnonymous] endpoints and inject {nameof(IAuditService)}, whose " +
            $"LogAsync throws without an ambient tenant (there is no JWT on an anonymous request): " +
            $"{string.Join(", ", offenders)}. Use {nameof(IPlatformAuditService)} when there is no natural " +
            $"tenant, or IAuditService.LogForTenantAsync with a tenant resolved by the handler itself — and " +
            $"then allow-list it in {nameof(ExplicitTenantAuditHandlers)} with the reason.");
    }

    /// <summary>
    /// The allow-list must not outlive the handlers it exempts: a renamed or deleted handler has to
    /// force a visit here rather than silently weakening the guardrail.
    /// </summary>
    [Fact]
    public void ExplicitTenantAuditAllowList_HasNoStaleEntries()
    {
        var existingHandlers = ApplicationAssembly
            .GetTypes()
            .Where(IsCqrsHandler)
            .Select(static type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var stale = ExplicitTenantAuditHandlers
            .Where(name => !existingHandlers.Contains(name))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            $"Allow-listed handlers no longer exist: {string.Join(", ", stale)}. Remove them from " +
            $"{nameof(ExplicitTenantAuditHandlers)}.");
    }

    private static bool IsCqrsHandler(Type type) =>
        type is { IsClass: true, IsAbstract: false } &&
        type.GetInterfaces().Any(static contract =>
            contract.IsGenericType &&
            (contract.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
             contract.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));
}
