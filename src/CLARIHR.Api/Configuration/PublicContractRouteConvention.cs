using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Contracts;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CLARIHR.Api.Configuration;

public sealed partial class PublicContractRouteConvention : IApplicationModelConvention
{
    [GeneratedRegex(@"\{(?<name>[^}:]+)(?<suffix>[^}]*)\}", RegexOptions.Compiled)]
    private static partial Regex RouteParameterRegex();

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            foreach (var action in controller.Actions)
            {
                var routeTemplates = GetCombinedRouteTemplates(controller, action);
                var parameterRenames = action.Parameters
                    .Where(parameter =>
                        !string.IsNullOrWhiteSpace(parameter.ParameterName))
                    .Select(parameter => new
                    {
                        Parameter = parameter,
                        OldName = parameter.ParameterName!,
                        RouteTemplate = FindRouteTemplateForParameter(parameter.ParameterName!, routeTemplates),
                        NewName = PublicContractNaming.GetExternalRouteIdentifierName(
                            parameter.ParameterName!,
                            parameter.ParameterInfo.ParameterType,
                            FindRouteTemplateForParameter(parameter.ParameterName!, routeTemplates))
                    })
                    .Where(rename =>
                        !string.IsNullOrWhiteSpace(rename.RouteTemplate) &&
                        !string.IsNullOrWhiteSpace(rename.NewName) &&
                        rename.NewName != rename.OldName)
                    .ToArray();

                if (parameterRenames.Length == 0)
                {
                    continue;
                }

                var parameterRenameMap = parameterRenames.ToDictionary(
                    rename => rename.OldName,
                    rename => rename.NewName!,
                    StringComparer.Ordinal);

                foreach (var selector in controller.Selectors.Where(selector => selector.AttributeRouteModel is not null))
                {
                    selector.AttributeRouteModel!.Template = RewriteTemplate(
                        selector.AttributeRouteModel.Template,
                        parameterRenameMap);
                }

                foreach (var selector in action.Selectors.Where(selector => selector.AttributeRouteModel is not null))
                {
                    selector.AttributeRouteModel!.Template = RewriteTemplate(
                        selector.AttributeRouteModel.Template,
                        parameterRenameMap);
                }

                foreach (var rename in parameterRenames)
                {
                    var newName = rename.NewName!;
                    rename.Parameter.BindingInfo ??= new BindingInfo();
                    rename.Parameter.BindingInfo.BinderModelName = newName;
                    rename.Parameter.BindingInfo.BindingSource ??= BindingSource.Path;
                }
            }
        }
    }

    private static string? FindRouteTemplateForParameter(string parameterName, IReadOnlyCollection<string> routeTemplates)
    {
        foreach (var template in routeTemplates)
        {
            foreach (Match match in RouteParameterRegex().Matches(template!))
            {
                if (match.Groups["name"].Value.Equals(parameterName, StringComparison.Ordinal))
                {
                    return template;
                }
            }
        }

        return null;
    }

    private static IReadOnlyCollection<string> GetCombinedRouteTemplates(ControllerModel controller, ActionModel action)
    {
        var controllerRoutes = controller.Selectors
            .Select(static selector => selector.AttributeRouteModel)
            .Where(static route => route is not null)
            .DefaultIfEmpty(null);
        var actionRoutes = action.Selectors
            .Select(static selector => selector.AttributeRouteModel)
            .Where(static route => route is not null)
            .DefaultIfEmpty(null);
        var templates = new HashSet<string>(StringComparer.Ordinal);

        foreach (var controllerRoute in controllerRoutes)
        {
            foreach (var actionRoute in actionRoutes)
            {
                var combinedRoute = controllerRoute is null
                    ? actionRoute
                    : actionRoute is null
                        ? controllerRoute
                        : AttributeRouteModel.CombineAttributeRouteModel(controllerRoute, actionRoute);

                if (!string.IsNullOrWhiteSpace(combinedRoute?.Template))
                {
                    templates.Add(combinedRoute.Template!);
                }
            }
        }

        return templates;
    }

    private static string? RewriteTemplate(string? template, IReadOnlyDictionary<string, string> parameterRenames)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return template;
        }

        return RouteParameterRegex().Replace(
            template,
            match =>
            {
                var currentName = match.Groups["name"].Value;
                if (!parameterRenames.TryGetValue(currentName, out var renamed))
                {
                    return match.Value;
                }

                return $"{{{renamed}{match.Groups["suffix"].Value}}}";
            });
    }
}
