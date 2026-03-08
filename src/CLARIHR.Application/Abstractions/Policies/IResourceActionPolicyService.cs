using CLARIHR.Application.Common.Policies;

namespace CLARIHR.Application.Abstractions.Policies;

public interface IResourceActionPolicyService
{
    AllowedActionsResponse Evaluate(ResourceActionContext context);
}
