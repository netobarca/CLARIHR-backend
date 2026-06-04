using CLARIHR.Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CLARIHR.Api.Common.Binders;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromIfMatchAttribute : ModelBinderAttribute
{
    public FromIfMatchAttribute()
        : base(typeof(IfMatchModelBinder))
    {
        BindingSource = BindingSource.Header;
        Name = IfMatchHeader.HeaderName;
    }
}
