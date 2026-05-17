using CLARIHR.Api.Common.Conventions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace CLARIHR.Application.UnitTests;

public sealed class ValidateJsonPatchDocumentFilterTests
{
    [Fact]
    public void OnActionExecuting_WhenJsonPatchBodyIsNull_ShouldReturn400WithStandardProblemShape()
    {
        var filter = new ValidateJsonPatchDocumentFilter();
        var context = CreateContext(typeof(JsonPatchDocument<TestPatchRequest>), argumentValue: null, includeArgument: false);

        filter.OnActionExecuting(context);

        // Project-standard shape (CLARIHR.Api.Common.ProblemDetailsFactory), not
        // the framework factory: ObjectResult(400) carrying a ValidationProblemDetails
        // with the typed `code` / `traceId` extensions. (technical-debt JP §8.2)
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);

        var problemDetails = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.Equal("common.validation", Assert.Contains("code", problemDetails.Extensions));
        Assert.Contains("traceId", problemDetails.Extensions);
        Assert.Contains("body", problemDetails.Errors);
        Assert.Contains("The request body is invalid.", problemDetails.Errors["body"]);
    }

    [Fact]
    public void OnActionExecuting_WhenJsonPatchBodyExists_ShouldContinue()
    {
        var filter = new ValidateJsonPatchDocumentFilter();
        var context = CreateContext(
            typeof(JsonPatchDocument<TestPatchRequest>),
            new JsonPatchDocument<TestPatchRequest>());

        filter.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnActionExecuting_WhenBodyParameterIsNotJsonPatch_ShouldIgnoreIt()
    {
        var filter = new ValidateJsonPatchDocumentFilter();
        var context = CreateContext(typeof(TestPatchRequest), argumentValue: null, includeArgument: false);

        filter.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    private static ActionExecutingContext CreateContext(Type parameterType, object? argumentValue, bool includeArgument = true)
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor
            {
                Parameters =
                [
                    new ParameterDescriptor
                    {
                        Name = "patchDoc",
                        ParameterType = parameterType,
                        BindingInfo = new BindingInfo
                        {
                            BindingSource = BindingSource.Body
                        }
                    }
                ]
            });

        var actionArguments = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (includeArgument)
        {
            actionArguments["patchDoc"] = argumentValue;
        }

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            actionArguments,
            new object());
    }

    private sealed class TestPatchRequest;
}
