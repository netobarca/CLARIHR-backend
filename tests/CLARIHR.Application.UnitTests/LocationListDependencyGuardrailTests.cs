using System.Reflection;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Groups;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §LG2 / §12.7 (ADR-0001) drift-proof guardrail, feature-wide. Every Locations list/search handler —
/// any <see cref="IQueryHandler{TQuery, TResult}"/> whose result is a <see cref="PagedResponse{T}"/> —
/// derives <c>AllowedActions</c> from the caller's permission (<c>canManage</c>) ONLY and must NOT
/// resolve per-item dependency state. Doing so is the forbidden N+1 (one <c>CanInactivate</c> probe per
/// row; 3N queries for a page of N). The dependency policy belongs to the command/PATCH handlers (the
/// server-side inactivation block), never the list. Re-injecting <see cref="ILocationDependencyPolicy"/>
/// into a paged Locations query handler is the signature of that regression, so pin it out — this covers
/// LocationGroups, WorkCenters, WorkCenterTypes and any future Locations list handler by return type.
/// </summary>
public sealed class LocationListDependencyGuardrailTests
{
    private static readonly Assembly ApplicationAssembly = typeof(SearchLocationGroupsQueryHandler).Assembly;

    [Fact]
    public void NoLocationListHandler_InjectsDependencyPolicy()
    {
        var listHandlers = ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.Namespace is not null
                && type.Namespace.StartsWith("CLARIHR.Application.Features.Locations", StringComparison.Ordinal)
                && ReturnsPagedResponse(type))
            .ToArray();

        // Zero-match sentinel: the Locations paged query handlers (groups / work centers / work center
        // types). A drifted namespace/return-type filter that matches none must fail loudly.
        Assert.True(
            listHandlers.Length >= 3,
            $"Expected at least 3 Locations paged query handlers, found {listHandlers.Length} — the filter drifted.");

        var offenders = listHandlers
            .Where(type => type.GetConstructors().Single().GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(ILocationDependencyPolicy)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "§12.7 (ADR-0001): Locations list/search handlers must not inject ILocationDependencyPolicy — " +
            "resolving per-item dependency state in a list is the forbidden N+1; the inactivation block stays " +
            "server-side in the command handlers. Offending:\n  " + string.Join("\n  ", offenders));
    }

    private static bool ReturnsPagedResponse(Type handlerType) =>
        handlerType.GetInterfaces().Any(contract =>
            contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)
            && contract.GetGenericArguments()[1] is { IsGenericType: true } result
            && result.GetGenericTypeDefinition() == typeof(PagedResponse<>));
}
