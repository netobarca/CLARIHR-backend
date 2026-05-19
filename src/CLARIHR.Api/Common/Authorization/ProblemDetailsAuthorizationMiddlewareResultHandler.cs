using CLARIHR.Api.Common;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Api.Common.Authorization;

/// <summary>
/// Wraps the framework default <see cref="IAuthorizationMiddlewareResultHandler"/> so that
/// policy-layer denials emit the same ProblemDetails contract as handler-layer denials
/// (status, <c>code</c>, <c>traceId</c>, localized title) instead of the ASP.NET default
/// empty body. <c>Challenged</c> (unauthenticated) maps to <c>UNAUTHENTICATED</c> (401);
/// <c>Forbidden</c> (authenticated, policy unmet) maps to the domain-specific forbidden
/// code resolved from the denied policy name, preserving the existing public 403 contract
/// for the JobProfile / PositionDescriptionCatalog modules. Mirrors the established
/// "centralized ProblemDetails on cross-cutting denial" precedent (rate-limiter
/// <c>OnRejected</c>, <see cref="CLARIHR.Api.Common.Binders.IfMatchModelBinder"/>).
/// </summary>
public sealed class ProblemDetailsAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Challenged && !authorizeResult.Forbidden)
        {
            await defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        var error = authorizeResult.Challenged
            ? AuthorizationErrors.Unauthenticated
            : ResolveForbiddenError(context);

        var logger = context.RequestServices?
            .GetService<ILogger<ProblemDetailsAuthorizationMiddlewareResultHandler>>()
            ?? NullLogger<ProblemDetailsAuthorizationMiddlewareResultHandler>.Instance;
        logger.LogWarning(
            "Authorization denied for {Method} {Path}: {Outcome} (code {Code}), traceId {TraceId}.",
            context.Request.Method,
            context.Request.Path.Value,
            authorizeResult.Challenged ? "unauthenticated" : "forbidden",
            error.Code,
            context.TraceIdentifier);

        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(context, error);
        context.Response.StatusCode = problemDetails.Status ?? ProblemDetailsFactory.MapStatusCode(error.Type);
        await context.Response.WriteAsJsonAsync(problemDetails, context.RequestAborted);
    }

    private static Error ResolveForbiddenError(HttpContext context)
    {
        var policyName = context.GetEndpoint()?.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Select(static data => data.Policy)
            .LastOrDefault(static name => !string.IsNullOrWhiteSpace(name));

        return policyName switch
        {
            JobProfilePolicies.Read
                or JobProfilePolicies.Manage
                or JobProfilePolicies.ManageCatalogs => JobProfileErrors.Forbidden,
            PositionDescriptionCatalogPolicies.Read or PositionDescriptionCatalogPolicies.Manage =>
                PositionDescriptionCatalogErrors.Forbidden,
            PositionSlotPolicies.Read or PositionSlotPolicies.Manage =>
                PositionSlotErrors.Forbidden,
            _ => ErrorCatalog.Forbidden
        };
    }
}
