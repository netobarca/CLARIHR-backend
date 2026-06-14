using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using CLARIHR.Api.Authorization;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CLARIHR.Api.Common;

/// <summary>
/// Populates <c>allowedActions</c> on successful PUT/PATCH/GET responses for controllers
/// decorated with <see cref="ResourceActionsAttribute"/>. Single-object responses are always
/// enriched; paged lists are opt-in via <c>?includeAllowedActions=true</c> (to protect
/// performance on large pages). The filter never overwrites a value the handler already set,
/// and it is fail-closed: controllers without the attribute, or unregistered resource keys,
/// emit nothing. Computation reads JWT claims + the already-loaded DTO only (no DB access).
/// </summary>
public sealed class AllowedActionsResultFilter(IAllowedActionsResolver resolver) : IAsyncResultFilter
{
    private const string IncludeAllowedActionsQueryKey = "includeAllowedActions";

    // Per-type cached setter for the init-only AllowedActions property (init setters expose a
    // usable SetMethod via reflection). Null means the type has no settable property.
    private static readonly ConcurrentDictionary<Type, Action<object, AllowedActionsResponse?>?> Setters = new();

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (TryGetResourceKey(context, out var resourceKey) &&
            context.Result is ObjectResult { Value: { } value } objectResult &&
            IsSuccessStatus(objectResult.StatusCode))
        {
            var method = context.HttpContext.Request.Method;
            if (HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsGet(method))
            {
                Enrich(value, resourceKey, context.HttpContext, method);
            }
        }

        await next();
    }

    private void Enrich(object value, string resourceKey, HttpContext httpContext, string method)
    {
        // Single-object response (PUT/PATCH or GET detail): always enriched.
        if (value is ISupportsAllowedActions)
        {
            TrySet(value, resourceKey);
            return;
        }

        // Paged list response: opt-in via ?includeAllowedActions=true on GET only.
        if (HttpMethods.IsGet(method) &&
            IsIncludeAllowedActionsRequested(httpContext) &&
            TryGetPagedItems(value, out var items))
        {
            foreach (var item in items)
            {
                if (item is ISupportsAllowedActions)
                {
                    TrySet(item, resourceKey);
                }
            }
        }
    }

    private void TrySet(object target, string resourceKey)
    {
        if (((ISupportsAllowedActions)target).AllowedActions is not null)
        {
            // The handler already provided an (often more precise) value — keep it.
            return;
        }

        var resolved = resolver.Resolve(resourceKey, target);
        if (resolved is null)
        {
            return;
        }

        var setter = Setters.GetOrAdd(target.GetType(), CreateSetter);
        setter?.Invoke(target, resolved);
    }

    private static Action<object, AllowedActionsResponse?>? CreateSetter(Type type)
    {
        var setMethod = type.GetProperty(nameof(ISupportsAllowedActions.AllowedActions))?.SetMethod;
        return setMethod is null
            ? null
            : (target, value) => setMethod.Invoke(target, [value]);
    }

    private static bool TryGetPagedItems(object value, out IEnumerable items)
    {
        var type = value.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PagedResponse<>))
        {
            items = (IEnumerable?)type.GetProperty(nameof(PagedResponse<object>.Items))?.GetValue(value)
                ?? Array.Empty<object>();
            return true;
        }

        items = Array.Empty<object>();
        return false;
    }

    private static bool TryGetResourceKey(ResultExecutingContext context, out string resourceKey)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            var attribute = descriptor.ControllerTypeInfo.GetCustomAttribute<ResourceActionsAttribute>();
            if (attribute is not null && !string.IsNullOrWhiteSpace(attribute.ResourceKey))
            {
                resourceKey = attribute.ResourceKey;
                return true;
            }
        }

        resourceKey = string.Empty;
        return false;
    }

    private static bool IsIncludeAllowedActionsRequested(HttpContext httpContext) =>
        httpContext.Request.Query.TryGetValue(IncludeAllowedActionsQueryKey, out var raw) &&
        bool.TryParse(raw, out var include) &&
        include;

    private static bool IsSuccessStatus(int? statusCode) =>
        statusCode is null or (>= 200 and < 300);
}
