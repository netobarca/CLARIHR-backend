using CLARIHR.Api.Common;
using CLARIHR.Application.Common.Pagination;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace CLARIHR.Application.UnitTests;

public sealed class ConditionalRequestResultFilterTests
{
    [Fact]
    public async Task OnResultExecutionAsync_WhenResponseHasConcurrencyToken_ShouldEmitETagAndLastModified()
    {
        var response = new TestResponse(
            Guid.Parse("9b75d8a1-3fb6-47d8-b718-02e41a6a02e7"),
            new DateTime(2026, 5, 15, 12, 34, 56, DateTimeKind.Utc),
            new DateTime(2026, 5, 15, 13, 45, 07, DateTimeKind.Utc));
        var context = CreateContext(response);
        var nextCalled = false;

        await new ConditionalRequestResultFilter().OnResultExecutionAsync(
            context,
            () =>
            {
                nextCalled = true;
                return Task.FromResult(CreateExecutedContext(context));
            });

        Assert.True(nextCalled);
        Assert.Equal("\"9b75d8a1-3fb6-47d8-b718-02e41a6a02e7\"", context.HttpContext.Response.Headers[HeaderNames.ETag].ToString());
        Assert.Equal("Fri, 15 May 2026 13:45:07 GMT", context.HttpContext.Response.Headers[HeaderNames.LastModified].ToString());
    }

    [Fact]
    public async Task OnResultExecutionAsync_WhenIfNoneMatchMatches_ShouldReturn304WithoutExecutingResult()
    {
        var response = new TestResponse(
            Guid.Parse("9b75d8a1-3fb6-47d8-b718-02e41a6a02e7"),
            new DateTime(2026, 5, 15, 12, 34, 56, DateTimeKind.Utc),
            ModifiedAtUtc: null);
        var context = CreateContext(response);
        context.HttpContext.Request.Headers.IfNoneMatch = "\"9b75d8a1-3fb6-47d8-b718-02e41a6a02e7\"";
        var nextCalled = false;

        await new ConditionalRequestResultFilter().OnResultExecutionAsync(
            context,
            () =>
            {
                nextCalled = true;
                return Task.FromResult(CreateExecutedContext(context));
            });

        Assert.False(nextCalled);
        Assert.True(context.Cancel);
        Assert.Equal(StatusCodes.Status304NotModified, context.HttpContext.Response.StatusCode);
        Assert.Equal("\"9b75d8a1-3fb6-47d8-b718-02e41a6a02e7\"", context.HttpContext.Response.Headers[HeaderNames.ETag].ToString());
        Assert.Equal("Fri, 15 May 2026 12:34:56 GMT", context.HttpContext.Response.Headers[HeaderNames.LastModified].ToString());
    }

    [Fact]
    public async Task OnResultExecutionAsync_WhenResponseIsPaged_ShouldEmitStableAggregateETag()
    {
        var response = new PagedResponse<TestItem>(
            [
                new TestItem(Guid.Parse("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc)),
                new TestItem(Guid.Parse("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 5, 15, 13, 0, 0, DateTimeKind.Utc))
            ],
            PageNumber: 2,
            PageSize: 1,
            TotalCount: 2);
        var context = CreateContext(response);

        await new ConditionalRequestResultFilter().OnResultExecutionAsync(
            context,
            () => Task.FromResult(CreateExecutedContext(context)));

        var eTag = context.HttpContext.Response.Headers[HeaderNames.ETag].ToString();
        Assert.StartsWith("\"", eTag, StringComparison.Ordinal);
        Assert.EndsWith("\"", eTag, StringComparison.Ordinal);
        Assert.Equal(66, eTag.Length);
        Assert.Equal("Fri, 15 May 2026 13:00:00 GMT", context.HttpContext.Response.Headers[HeaderNames.LastModified].ToString());
    }

    [Fact]
    public async Task OnResultExecutionAsync_WhenMethodIsPost_ShouldNotEmitConditionalHeaders()
    {
        var context = CreateContext(new TestResponse(Guid.NewGuid(), DateTime.UtcNow, ModifiedAtUtc: null));
        context.HttpContext.Request.Method = HttpMethods.Post;
        var nextCalled = false;

        await new ConditionalRequestResultFilter().OnResultExecutionAsync(
            context,
            () =>
            {
                nextCalled = true;
                return Task.FromResult(CreateExecutedContext(context));
            });

        Assert.True(nextCalled);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey(HeaderNames.ETag));
        Assert.False(context.HttpContext.Response.Headers.ContainsKey(HeaderNames.LastModified));
    }

    private static ResultExecutingContext CreateContext(object value)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ResultExecutingContext(
            actionContext,
            [],
            new OkObjectResult(value),
            controller: new object());
    }

    private static ResultExecutedContext CreateExecutedContext(ResultExecutingContext context) =>
        new(
            new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
            context.Filters,
            context.Result,
            context.Controller);

    private sealed record TestResponse(Guid ConcurrencyToken, DateTime CreatedAtUtc, DateTime? ModifiedAtUtc);

    private sealed record TestItem(Guid ConcurrencyToken, DateTime ModifiedAtUtc);
}
