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
                var routeParameterNames = GetRouteParameterNames(controller, action);
                var parameterRenames = action.Parameters
                    .Where(parameter =>
                        !string.IsNullOrWhiteSpace(parameter.ParameterName) &&
                        routeParameterNames.Contains(parameter.ParameterName!))
                    .Select(parameter => new
                    {
                        Parameter = parameter,
                        OldName = parameter.ParameterName!,
                        NewName = PublicContractNaming.GetExternalRouteIdentifierName(
                            parameter.ParameterName!,
                            parameter.ParameterInfo.ParameterType)
                    })
                    .Where(rename => !string.IsNullOrWhiteSpace(rename.NewName) && rename.NewName != rename.OldName)
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
                    rename.Parameter.ParameterName = newName;
                }
            }
        }
    }

    private static HashSet<string> GetRouteParameterNames(ControllerModel controller, ActionModel action)
    {
        var routeParameterNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var template in controller.Selectors
                     .Concat(action.Selectors)
                     .Select(selector => selector.AttributeRouteModel?.Template)
                     .Where(static template => !string.IsNullOrWhiteSpace(template)))
        {
            foreach (Match match in RouteParameterRegex().Matches(template!))
            {
                routeParameterNames.Add(match.Groups["name"].Value);
            }
        }

        return routeParameterNames;
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
