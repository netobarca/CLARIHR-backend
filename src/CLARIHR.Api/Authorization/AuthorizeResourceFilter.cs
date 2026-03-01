using CLARIHR.Api.Common;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Features.IdentityAccess.Common;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CLARIHR.Api.Authorization;

internal sealed class AuthorizeResourceFilter(
    string resourceKey,
    RbacPermissionAction action,
    IRbacAuthorizationService authorizationService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(
            resourceKey,
            action,
            context.HttpContext.RequestAborted);
        if (authorizationResult.IsFailure)
        {
            context.Result = ProblemDetailsFactory.Create(context.HttpContext, authorizationResult.Error);
            return;
        }

        await next();
    }
}
