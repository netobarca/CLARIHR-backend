using CLARIHR.Application.Common.CQRS;

namespace CLARIHR.Application.Features.CommercialModules;

public sealed record CommercialModuleResponse(
    string Key,
    string DisplayName,
    string Description);

public sealed record GetCommercialModulesQuery : IQuery<IReadOnlyCollection<CommercialModuleResponse>>;
