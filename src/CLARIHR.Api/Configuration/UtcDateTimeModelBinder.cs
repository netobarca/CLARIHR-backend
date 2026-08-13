using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace CLARIHR.Api.Configuration;

/// <summary>
/// H-26 — the other entry path, the one the finding never saw: the ~34 <c>[FromQuery] DateTime</c> parameters of
/// the reporting endpoints. Query, route and form values are bound by MVC, never by the JSON serializer, so a
/// converter alone left them broken — <c>personnel-actions/export?fromUtc=2026-08-01</c> answered <c>500</c>.
/// Same normalization rule as <see cref="UtcDateTimeJsonConverter"/>, including converting (not relabelling) a
/// value that carries an explicit offset.
/// </summary>
public sealed class UtcDateTimeModelBinder(IModelBinder inner) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        await inner.BindModelAsync(bindingContext);
        if (!bindingContext.Result.IsModelSet || bindingContext.Result.Model is not DateTime value)
        {
            return;
        }

        bindingContext.Result = ModelBindingResult.Success(UtcDateTimeJsonConverter.ToUtc(value));
    }
}

/// <inheritdoc cref="UtcDateTimeModelBinder"/>
public sealed class UtcDateTimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var type = context.Metadata.UnderlyingOrModelType;
        if (type != typeof(DateTime))
        {
            return null;
        }

        // Wrap whatever MVC would have used (the simple-type binder), so parsing/format errors keep answering the
        // usual `400` and only a successfully bound value gets normalized.
        var inner = new SimpleTypeModelBinderProvider().GetBinder(context);
        return inner is null ? null : new UtcDateTimeModelBinder(inner);
    }
}
