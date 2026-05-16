using CLARIHR.Api.Common.Conventions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using MvcProblemDetailsFactory = Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory;

namespace CLARIHR.Application.UnitTests;

public sealed class ValidateJsonPatchDocumentFilterTests
{
    [Fact]
    public void OnActionExecuting_WhenJsonPatchBodyIsNull_ShouldReturn400ProblemDetails()
    {
        var filter = new ValidateJsonPatchDocumentFilter(new TestProblemDetailsFactory());
        var context = CreateContext(typeof(JsonPatchDocument<TestPatchRequest>), argumentValue: null, includeArgument: false);

        filter.OnActionExecuting(context);

        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("Invalid patch document.", problemDetails.Detail);
    }

    [Fact]
    public void OnActionExecuting_WhenJsonPatchBodyExists_ShouldContinue()
    {
        var filter = new ValidateJsonPatchDocumentFilter(new TestProblemDetailsFactory());
        var context = CreateContext(
            typeof(JsonPatchDocument<TestPatchRequest>),
            new JsonPatchDocument<TestPatchRequest>());

        filter.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnActionExecuting_WhenBodyParameterIsNotJsonPatch_ShouldIgnoreIt()
    {
        var filter = new ValidateJsonPatchDocumentFilter(new TestProblemDetailsFactory());
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

    private sealed class TestProblemDetailsFactory : MvcProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null)
            => new()
            {
                Status = statusCode,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext,
            ModelStateDictionary modelStateDictionary,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null)
            => new(modelStateDictionary)
            {
                Status = statusCode,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };
    }
}
