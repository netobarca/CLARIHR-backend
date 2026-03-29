using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Contracts;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

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
                var parameterRenames = action.Parameters
                    .Where(parameter => !string.IsNullOrWhiteSpace(parameter.ParameterName))
                    .Select(parameter => new
                    {
                        OldName = parameter.ParameterName!,
                        NewName = PublicContractNaming.GetExternalIdentifierName(
                            parameter.ParameterName!,
                            parameter.ParameterInfo.ParameterType)
                    })
                    .Where(rename => !string.IsNullOrWhiteSpace(rename.NewName) && rename.NewName != rename.OldName)
                    .ToDictionary(rename => rename.OldName, rename => rename.NewName!, StringComparer.Ordinal);

                if (parameterRenames.Count == 0)
                {
                    continue;
                }

                foreach (var selector in controller.Selectors.Where(selector => selector.AttributeRouteModel is not null))
                {
                    selector.AttributeRouteModel!.Template = RewriteTemplate(
                        selector.AttributeRouteModel.Template,
                        parameterRenames);
                }

                foreach (var selector in action.Selectors.Where(selector => selector.AttributeRouteModel is not null))
                {
                    selector.AttributeRouteModel!.Template = RewriteTemplate(
                        selector.AttributeRouteModel.Template,
                        parameterRenames);
                }
            }
        }
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
