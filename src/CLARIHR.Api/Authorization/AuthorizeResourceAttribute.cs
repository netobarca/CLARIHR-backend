using CLARIHR.Application.Features.IdentityAccess.Common;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class AuthorizeResourceAttribute : TypeFilterAttribute
{
    public AuthorizeResourceAttribute(string resourceKey, RbacPermissionAction action)
        : base(typeof(AuthorizeResourceFilter))
    {
        Arguments = [resourceKey, action];
        Order = int.MinValue;
    }
}
