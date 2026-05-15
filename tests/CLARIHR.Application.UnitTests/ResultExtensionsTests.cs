using CLARIHR.Api.Common;
using CLARIHR.Application.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Application.UnitTests;

public sealed class ResultExtensionsTests
{
    [Fact]
    public void ToCreatedResult_WhenResultSucceeds_ShouldReturnCreatedResult()
    {
        var controller = new TestController();
        var response = new TestResponse(Guid.Parse("9b75d8a1-3fb6-47d8-b718-02e41a6a02e7"));

        var actionResult = controller.ToCreatedResult(
            Result<TestResponse>.Success(response),
            value => $"/api/test/{value.Id:D}");

        var created = Assert.IsType<CreatedResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal("/api/test/9b75d8a1-3fb6-47d8-b718-02e41a6a02e7", created.Location);
        Assert.Same(response, created.Value);
    }

    [Fact]
    public void ToActionResultWithETag_WhenResultSucceeds_ShouldReturnOkAndSetQuotedETag()
    {
        var controller = new TestController();
        var response = new TestResponse(Guid.Parse("9b75d8a1-3fb6-47d8-b718-02e41a6a02e7"));

        var actionResult = controller.ToActionResultWithETag(
            Result<TestResponse>.Success(response),
            value => value.Id);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Same(response, ok.Value);
        Assert.Equal("\"9b75d8a1-3fb6-47d8-b718-02e41a6a02e7\"", controller.Response.Headers[ETagHeader.HeaderName].ToString());
    }

    [Fact]
    public void SetETag_WhenResultSucceeds_ShouldSetQuotedETag()
    {
        var controller = new TestController();
        var response = new TestResponse(Guid.Parse("9b75d8a1-3fb6-47d8-b718-02e41a6a02e7"));

        controller.SetETag(Result<TestResponse>.Success(response), value => value.Id);

        Assert.Equal("\"9b75d8a1-3fb6-47d8-b718-02e41a6a02e7\"", controller.Response.Headers[ETagHeader.HeaderName].ToString());
    }

    [Fact]
    public void ToCreatedResult_WhenResultFails_ShouldReturnProblemDetails()
    {
        var controller = new TestController();

        var actionResult = controller.ToCreatedResult(
            Result<TestResponse>.Failure(ErrorCatalog.NotFound),
            value => $"/api/test/{value.Id:D}");

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        Assert.Equal(ErrorCatalog.NotFound.Code, problemDetails.Extensions["code"]);
    }

    private sealed record TestResponse(Guid Id);

    private sealed class TestController : ControllerBase
    {
        public TestController()
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }
    }
}
