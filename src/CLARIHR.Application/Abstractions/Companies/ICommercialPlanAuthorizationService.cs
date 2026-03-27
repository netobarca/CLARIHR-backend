using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICommercialPlanAuthorizationService
{
    Task<Result> EnsurePlatformAdministrationAsync(CancellationToken cancellationToken);
}
