using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;

namespace CLARIHR.Application.UnitTests;

public sealed class IfMatchModelBinderTests
{
    [Fact]
    public async Task BindModelAsync_WhenHeaderIsQuotedGuid_ShouldBindConcurrencyToken()
    {
        var token = Guid.Parse("9b75d8a1-3fb6-47d8-b718-02e41a6a02e7");
        var bindingContext = CreateBindingContext($"\"{token:D}\"");

        await new IfMatchModelBinder().BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(token, Assert.IsType<Guid>(bindingContext.Result.Model));
        Assert.True(bindingContext.ModelState.IsValid);
    }

    [Fact]
    public async Task BindModelAsync_WhenHeaderIsRawGuid_ShouldBindConcurrencyToken()
    {
        var token = Guid.Parse("9b75d8a1-3fb6-47d8-b718-02e41a6a02e7");
        var bindingContext = CreateBindingContext(token.ToString("D"));

        await new IfMatchModelBinder().BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(token, Assert.IsType<Guid>(bindingContext.Result.Model));
        Assert.True(bindingContext.ModelState.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task BindModelAsync_WhenHeaderIsMissingOrInvalid_ShouldFailWithIfMatchModelStateError(string? headerValue)
    {
        var bindingContext = CreateBindingContext(headerValue);

        await new IfMatchModelBinder().BindModelAsync(bindingContext);

        Assert.False(bindingContext.Result.IsModelSet);
        Assert.False(bindingContext.ModelState.IsValid);
        var error = Assert.Single(bindingContext.ModelState[IfMatchHeader.HeaderName]!.Errors);
        Assert.Equal(IfMatchHeader.MissingDetail, error.ErrorMessage);
    }

    [Fact]
    public void FromIfMatchAttribute_ShouldBindFromIfMatchHeaderWithIfMatchModelBinder()
    {
        var attribute = new FromIfMatchAttribute();

        Assert.Equal(BindingSource.Header, attribute.BindingSource);
        Assert.Equal(IfMatchHeader.HeaderName, attribute.Name);
        Assert.Equal(typeof(IfMatchModelBinder), attribute.BinderType);
    }

    private static DefaultModelBindingContext CreateBindingContext(string? headerValue)
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
        {
            httpContext.Request.Headers[IfMatchHeader.HeaderName] = headerValue;
        }

        var modelState = new ModelStateDictionary();
        return new DefaultModelBindingContext
        {
            ActionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor(),
                modelState),
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(Guid)),
            ModelName = "concurrencyToken",
            ModelState = modelState,
            ValueProvider = new CompositeValueProvider()
        };
    }
}
