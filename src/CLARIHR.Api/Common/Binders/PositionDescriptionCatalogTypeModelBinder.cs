using CLARIHR.Api.Controllers;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Api.Common.Binders;

internal sealed class PositionDescriptionCatalogTypeModelBinder : IModelBinder
{
    // §X-LOG: cap the client-controlled slug before it reaches a Warning log line so a
    // single bad request cannot write an arbitrarily long attacker-controlled string to
    // the logs (log-volume/noise hardening). Valid slugs are short (≤ ~30 chars), so 64
    // preserves all diagnostic value while bounding the worst case.
    private const int MaxLoggedSlugLength = 64;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        var value = valueProviderResult.FirstValue;

        if (PositionDescriptionCatalogRouteMap.TryResolve(value, out var resolvedType))
        {
            bindingContext.Result = ModelBindingResult.Success(resolvedType);
        }
        else
        {
            var logger = bindingContext.HttpContext.RequestServices?
                .GetService<ILogger<PositionDescriptionCatalogTypeModelBinder>>()
                ?? NullLogger<PositionDescriptionCatalogTypeModelBinder>.Instance;
            var request = bindingContext.HttpContext.Request;
            logger.LogWarning(
                "Rejected {Method} {Path}: unknown catalog type slug '{Slug}' (client may be using a deprecated or invalid slug).",
                request.Method,
                request.Path.Value,
                Truncate(value));

            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                PositionDescriptionCatalogErrors.InvalidCatalogType.Message);

            // §S5: report an explicit binding FAILURE, not merely "no result"
            // (parity with IfMatchModelBinder). Today [ApiController] still
            // auto-400s via the ModelState error, but signalling Failed() keeps
            // the contract correct even if auto-400 is ever scoped off, and
            // removes the divergence from the sibling binder.
            bindingContext.Result = ModelBindingResult.Failed();
        }

        return Task.CompletedTask;
    }

    // internal for unit-test reach (InternalsVisibleTo CLARIHR.Application.UnitTests);
    // the non-Web unit project can exercise the hardening without MVC binding plumbing.
    internal static string? Truncate(string? value) =>
        value is { Length: > MaxLoggedSlugLength }
            ? string.Concat(value.AsSpan(0, MaxLoggedSlugLength), "…")
            : value;
}
