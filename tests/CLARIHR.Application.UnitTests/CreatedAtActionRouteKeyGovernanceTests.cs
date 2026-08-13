using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Contracts;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// H-02 guardrail. <c>CreatedAtAction</c> resolves its <c>Location</c> by matching the supplied route
/// values against the target action's route template. A key that does not exist in that template produces
/// <c>InvalidOperationException("No route matches the supplied values.")</c> — a <b>500 on a request that
/// already committed its write</b>. The resource is created and the caller is told the server broke.
/// <para>
/// The trap is that <see cref="PublicContractNaming"/> has TWO renaming functions and they disagree for the
/// same input: <c>GetExternalIdentifierName("userId")</c> → <c>userPublicId</c> (JSON bodies), while
/// <c>GetExternalRouteIdentifierName("userId")</c> → <c>publicId</c> (routes, applied by
/// <c>PublicContractRouteConvention</c>). Reaching for the JSON one when building a <c>Location</c> compiles,
/// passes every handler unit test, and 500s in production. It has now happened twice.
/// </para>
/// <para>
/// This test walks every <c>ToCreatedAtActionResult*</c> call site in the API and checks the supplied keys
/// against the REWRITTEN target template. It calls
/// <see cref="PublicContractNaming.GetExternalRouteIdentifierName"/> directly rather than restating the rule,
/// so it cannot drift away from the convention it exists to protect.
/// </para>
/// </summary>
public sealed partial class CreatedAtActionRouteKeyGovernanceTests
{
    // ASP.NET fills route values the caller omits from the CURRENT request's ambient values. That is why a
    // nested sub-resource POST only supplies the new child's key: the parent id is already in its own route.
    // Ignoring this is what makes a naive version of this test report ~88 false failures.
    private const string ApiVersionRouteParameter = "version";

    [Fact]
    public void EveryCreatedAtAction_SuppliesEveryRouteKeyTheTargetTemplateRequires()
    {
        var callSites = CollectCallSites();

        // Sanity floor: if the parsing silently stops matching (a refactor of the helper name, a formatting
        // change), the test would "pass" by checking nothing. Fail loudly instead.
        Assert.True(
            callSites.Count >= 80,
            $"Only {callSites.Count} CreatedAtAction call sites were parsed — the parser is probably broken, " +
            "not the code. Verify the ToCreatedAtActionResult naming before trusting a green run.");

        var failures = new List<string>();

        foreach (var site in callSites)
        {
            var required = RouteKeys(site.TargetTemplate, site.TargetMethodSource)
                .Except(RouteKeys(site.SourceTemplate, site.SourceMethodSource), StringComparer.Ordinal)
                .Where(key => !key.Equals(ApiVersionRouteParameter, StringComparison.Ordinal))
                .ToArray();

            // Extra keys are harmless — they land in the query string. Only a MISSING key breaks routing.
            var missing = required.Except(site.SuppliedKeys, StringComparer.Ordinal).ToArray();
            if (missing.Length == 0)
            {
                continue;
            }

            failures.Add(
                $"""
                {site.ControllerFile} · {site.SourceMethod}() → nameof({site.TargetMethod})
                    target route (rewritten) : {Rewrite(site.TargetTemplate, site.TargetMethodSource)}
                    keys supplied            : {Format(site.SuppliedKeys)}
                    keys MISSING             : {Format(missing)}
                """);
        }

        Assert.True(
            failures.Count == 0,
            $"""
            {failures.Count} CreatedAtAction call site(s) omit a route key the target template requires.
            Each one returns 500 "No route matches the supplied values." AFTER committing its write.

            Remember: the route rename is NOT the JSON rename. A guid route parameter named `somethingId` is
            rewritten to `publicId` — not to `somethingPublicId`, which is what the JSON rename produces.

            {string.Join("\n\n", failures)}
            """);
    }

    private static string Format(IEnumerable<string> keys)
    {
        var ordered = keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        return ordered.Length == 0 ? "(none)" : string.Join(", ", ordered);
    }

    /// <summary>
    /// Route parameter names of <paramref name="template"/> after the convention's rename. The C# parameter
    /// type comes from the target method's signature (not from the `:guid` constraint) so a route parameter
    /// that omits the constraint is still evaluated correctly.
    /// </summary>
    private static string[] RouteKeys(string template, string methodSource) =>
        RouteParameterRegex().Matches(template)
            .Select(match => match.Groups["name"].Value)
            .Select(name => PublicContractNaming.GetExternalRouteIdentifierName(
                name,
                ResolveParameterType(name, methodSource, template),
                template) ?? name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string Rewrite(string template, string methodSource) =>
        RouteParameterRegex().Replace(
            template,
            match =>
            {
                var name = match.Groups["name"].Value;
                var renamed = PublicContractNaming.GetExternalRouteIdentifierName(
                    name,
                    ResolveParameterType(name, methodSource, template),
                    template);
                return renamed is null ? match.Value : $"{{{renamed}{match.Groups["suffix"].Value}}}";
            });

    private static Type ResolveParameterType(string name, string methodSource, string template)
    {
        // `Guid name` / `Guid? name` in the action signature — the type the convention actually inspects.
        if (Regex.IsMatch(methodSource, $@"\bGuid\??\s+{Regex.Escape(name)}\b"))
        {
            return typeof(Guid);
        }

        // Fall back to the route constraint when the signature could not be read (e.g. the parameter is
        // bound by a model). A `:guid` constraint is unambiguous.
        var constrained = RouteParameterRegex().Matches(template)
            .Any(match => match.Groups["name"].Value.Equals(name, StringComparison.Ordinal) &&
                          match.Groups["suffix"].Value.Contains(":guid", StringComparison.OrdinalIgnoreCase));

        return constrained ? typeof(Guid) : typeof(string);
    }

    private static List<CreatedCallSite> CollectCallSites()
    {
        var controllersDirectory = Path.Combine(FindRepositoryRoot(), "src", "CLARIHR.Api", "Controllers");
        var sites = new List<CreatedCallSite>();

        foreach (var file in Directory.EnumerateFiles(controllersDirectory, "*.cs").OrderBy(path => path, StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file);
            var controllerRoute = ControllerRouteRegex().Match(source) is { Success: true } match
                ? match.Groups["template"].Value
                : string.Empty;

            var actions = ParseActions(source, controllerRoute);
            if (actions.Count == 0)
            {
                continue;
            }

            var byName = actions
                .GroupBy(action => action.Method, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (Match call in CreatedCallRegex().Matches(source))
            {
                var window = source.AsSpan(call.Index, Math.Min(900, source.Length - call.Index)).ToString();
                var target = TargetActionRegex().Match(window);
                var routeValues = RouteValuesObjectRegex().Match(window);
                if (!target.Success || !routeValues.Success)
                {
                    continue;
                }

                // The enclosing action is the last one declared before the call site.
                var enclosing = actions.LastOrDefault(action => action.DeclarationIndex < call.Index);
                if (enclosing is null || !byName.TryGetValue(target.Groups["name"].Value, out var targetAction))
                {
                    continue;
                }

                sites.Add(new CreatedCallSite(
                    Path.GetFileName(file),
                    enclosing.Method,
                    enclosing.Template,
                    enclosing.Source,
                    targetAction.Method,
                    targetAction.Template,
                    targetAction.Source,
                    ParseAnonymousMemberNames(routeValues.Groups["members"].Value)));
            }
        }

        return sites;
    }

    private static List<ParsedAction> ParseActions(string source, string controllerRoute)
    {
        var methods = MethodDeclarationRegex().Matches(source)
            .Select(match => new
            {
                Name = match.Groups["name"].Value,
                Index = match.Index,
                Source = source.AsSpan(match.Index, Math.Min(2500, source.Length - match.Index)).ToString()
            })
            .ToArray();

        var actions = new List<ParsedAction>();

        foreach (Match verb in HttpVerbAttributeRegex().Matches(source))
        {
            var method = methods.FirstOrDefault(candidate => candidate.Index > verb.Index);
            if (method is null)
            {
                continue;
            }

            var actionTemplate = verb.Groups["template"].Success ? verb.Groups["template"].Value : string.Empty;
            actions.Add(new ParsedAction(
                method.Name,
                Combine(controllerRoute, actionTemplate),
                method.Source,
                method.Index));
        }

        return actions;
    }

    /// <summary>
    /// Member names of an anonymous object initializer, handling both forms C# allows:
    /// <c>new { publicId = value.Id }</c> and the shorthand <c>new { companyPublicId }</c>, where the member
    /// name is the identifier itself. Missing the shorthand would report a supplied key as absent.
    /// </summary>
    private static string[] ParseAnonymousMemberNames(string members) =>
        members
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part =>
            {
                var assignment = part.IndexOf('=', StringComparison.Ordinal);
                if (assignment >= 0)
                {
                    return part[..assignment].Trim();
                }

                // Shorthand: the member name is the trailing identifier of the expression.
                var segments = part.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return segments.Length == 0 ? string.Empty : segments[^1];
            })
            .Where(name => name.Length > 0 && name.All(character => char.IsLetterOrDigit(character) || character == '_'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string Combine(string controllerRoute, string actionTemplate)
    {
        if (string.IsNullOrEmpty(actionTemplate))
        {
            return controllerRoute;
        }

        if (actionTemplate.StartsWith('/') || string.IsNullOrEmpty(controllerRoute))
        {
            return actionTemplate;
        }

        return $"{controllerRoute.TrimEnd('/')}/{actionTemplate.TrimStart('/')}";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "CLARIHR.Application")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test output path.");
    }

    private sealed record ParsedAction(string Method, string Template, string Source, int DeclarationIndex);

    private sealed record CreatedCallSite(
        string ControllerFile,
        string SourceMethod,
        string SourceTemplate,
        string SourceMethodSource,
        string TargetMethod,
        string TargetTemplate,
        string TargetMethodSource,
        string[] SuppliedKeys);

    [GeneratedRegex(@"\{(?<name>[^}:]+)(?<suffix>[^}]*)\}", RegexOptions.Compiled)]
    private static partial Regex RouteParameterRegex();

    [GeneratedRegex(@"\[Route\(""(?<template>[^""]+)""\)\]", RegexOptions.Compiled)]
    private static partial Regex ControllerRouteRegex();

    [GeneratedRegex(@"\[Http(?:Get|Post|Put|Patch|Delete)(?:\(""(?<template>[^""]*)""\))?\]", RegexOptions.Compiled)]
    private static partial Regex HttpVerbAttributeRegex();

    [GeneratedRegex(@"^\s*public\s+(?:static\s+)?(?:async\s+)?[^;{}()]+?\s(?<name>\w+)\s*\(", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex MethodDeclarationRegex();

    [GeneratedRegex(@"ToCreatedAtActionResult\w*\s*\(", RegexOptions.Compiled)]
    private static partial Regex CreatedCallRegex();

    [GeneratedRegex(@"nameof\(\s*(?<name>\w+)\s*\)", RegexOptions.Compiled)]
    private static partial Regex TargetActionRegex();

    [GeneratedRegex(@"=>\s*new\s*\{(?<members>[^{}]*)\}", RegexOptions.Compiled)]
    private static partial Regex RouteValuesObjectRegex();
}
