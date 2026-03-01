using CLARIHR.Api.Authorization;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace CLARIHR.Application.UnitTests;

public sealed class AuthorizeResourceFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WhenUserIsUnauthenticated_ShouldReturn401ProblemDetails()
    {
        var filter = new AuthorizeResourceFilter(
            "RBAC_USERS",
            RbacPermissionAction.Read,
            new TestRbacAuthorizationService(Result.Failure(AuthorizationErrors.Unauthenticated)));

        var context = CreateContext("/api/company/users");
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        Assert.False(nextCalled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenUserLacksPermission_ShouldReturn403ProblemDetails()
    {
        var filter = new AuthorizeResourceFilter(
            "RBAC_USERS",
            RbacPermissionAction.Update,
            new TestRbacAuthorizationService(Result.Failure(AuthorizationErrors.Denied("RBAC_USERS", RbacPermissionAction.Update))));

        var context = CreateContext("/api/company/users/123");

        await filter.OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null!));

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenAuthorizationSucceeds_ShouldInvokeNext()
    {
        var filter = new AuthorizeResourceFilter(
            "RBAC_USERS",
            RbacPermissionAction.Read,
            new TestRbacAuthorizationService(Result.Success()));

        var context = CreateContext("/api/company/users");
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    private static ActionExecutingContext CreateContext(string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    private sealed class TestRbacAuthorizationService(Result result) : IRbacAuthorizationService
    {
        public Task<Result> AuthorizeAsync(
            string resourceKey,
            RbacPermissionAction action,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);

        public Task<Result> AuthorizeFieldsAsync(
            string resourceKey,
            RbacPermissionAction action,
            IReadOnlyCollection<string> fieldKeys,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
