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

    // §S3: a list/paged result must NOT pay the per-item aggregate SHA-256 on a
    // plain (non-conditional, non-HEAD) GET — the validator has no caching value
    // when the client did not ask for it, so no ETag is emitted at all.
    [Fact]
    public async Task OnResultExecutionAsync_WhenResponseIsPaged_AndNoIfNoneMatch_ShouldNotEmitAggregateETag()
    {
        var context = CreateContext(CreatePagedResponse());
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

    // §S3 + §S4: when the client DOES make a conditional request, the aggregate
    // ETag is computed and emitted as a WEAK validator (order-dependent hash).
    [Fact]
    public async Task OnResultExecutionAsync_WhenResponseIsPaged_AndConditionalRequest_ShouldEmitWeakAggregateETag()
    {
        var context = CreateContext(CreatePagedResponse());
        context.HttpContext.Request.Headers.IfNoneMatch = "\"some-other-etag\"";

        await new ConditionalRequestResultFilter().OnResultExecutionAsync(
            context,
            () => Task.FromResult(CreateExecutedContext(context)));

        var eTag = context.HttpContext.Response.Headers[HeaderNames.ETag].ToString();
        Assert.StartsWith("W/\"", eTag, StringComparison.Ordinal);
        Assert.EndsWith("\"", eTag, StringComparison.Ordinal);
        Assert.Equal(68, eTag.Length);
        Assert.Equal("Fri, 15 May 2026 13:00:00 GMT", context.HttpContext.Response.Headers[HeaderNames.LastModified].ToString());
    }

    // §S4: the weak aggregate ETag still satisfies a matching conditional GET
    // (Matches normalizes the `W/` prefix on both sides) → 304.
    [Fact]
    public async Task OnResultExecutionAsync_WhenWeakAggregateETagMatches_ShouldReturn304()
    {
        // First request (conditional) to learn the current weak aggregate ETag.
        var probeContext = CreateContext(CreatePagedResponse());
        probeContext.HttpContext.Request.Headers.IfNoneMatch = "\"unknown\"";
        await new ConditionalRequestResultFilter().OnResultExecutionAsync(
            probeContext,
            () => Task.FromResult(CreateExecutedContext(probeContext)));
        var currentETag = probeContext.HttpContext.Response.Headers[HeaderNames.ETag].ToString();

        var context = CreateContext(CreatePagedResponse());
        context.HttpContext.Request.Headers.IfNoneMatch = currentETag;
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
    }

    private static PagedResponse<TestItem> CreatePagedResponse() =>
        new(
            [
                new TestItem(Guid.Parse("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc)),
                new TestItem(Guid.Parse("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 5, 15, 13, 0, 0, DateTimeKind.Utc))
            ],
            PageNumber: 2,
            PageSize: 1,
            TotalCount: 2);

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
