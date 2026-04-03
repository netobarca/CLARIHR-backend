using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Features.CommercialModules;

internal sealed class GetCommercialModulesQueryHandler(
    IPlatformAuthorizationService authorizationService)
    : IQueryHandler<GetCommercialModulesQuery, IReadOnlyCollection<CommercialModuleResponse>>
{
    public async Task<Result<IReadOnlyCollection<CommercialModuleResponse>>> Handle(
        GetCommercialModulesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<CommercialModuleResponse>>.Failure(authorizationResult.Error);
        }

        var response = CommercialModuleCatalog.All
            .Select(static definition => new CommercialModuleResponse(
                definition.ModuleKey,
                definition.DisplayName,
                definition.Description))
            .ToArray();

        return Result<IReadOnlyCollection<CommercialModuleResponse>>.Success(response);
    }
}
