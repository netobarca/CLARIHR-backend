using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using FluentValidation;

namespace CLARIHR.Application.Features.AccountCompanies;

public sealed record AccountCompanyEffectiveCapabilityResponse(
    string CapabilityCode,
    string ModuleKey,
    string DisplayName,
    string Description,
    string Source,
    bool GrantedByPlan,
    bool GrantedByAddon);

public sealed record AccountCompanyAccessSubscriptionContextResponse(
    Guid CommercialPlanId,
    string Code,
    string Name,
    string? Description,
    IReadOnlyCollection<string> CapabilityCodes);

public sealed record AccountCompanyAccessAddonContextResponse(
    Guid CommercialAddonId,
    string Code,
    string Name,
    string? Description,
    IReadOnlyCollection<string> CapabilityCodes);

public sealed record AccountCompanyCommercialContextResponse(
    AccountCompanyAccessSubscriptionContextResponse Subscription,
    IReadOnlyCollection<AccountCompanyAccessAddonContextResponse> Extensions);

public sealed record AccountCompanyAccessRoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole);

public sealed record AccountCompanyAccessPermissionResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Module,
    string Screen,
    IamPermissionKind Kind,
    string? Action,
    string? FieldName,
    IamFieldAccessLevel? FieldAccess,
    IReadOnlyCollection<string> CapabilityCodes,
    bool IsDormant,
    IReadOnlyCollection<string> SupportedScopeTypes);

public sealed record AccountCompanyPermissionScopeResponse(
    string PermissionCode,
    string ScopeType,
    IReadOnlyCollection<string> Values,
    bool IsImplicit);

public sealed record AccountCompanyCurrentUserAccessResponse(
    IReadOnlyCollection<AccountCompanyAccessRoleResponse> Roles,
    IReadOnlyCollection<AccountCompanyAccessPermissionResponse> Permissions,
    IReadOnlyCollection<AccountCompanyPermissionScopeResponse> Scopes);

public sealed record AccountCompanyAccessScopeTypeResponse(
    string ScopeType,
    string DisplayName,
    string Description);

public sealed record AccountCompanyFieldPolicyCatalogItemResponse(
    string ResourceKey,
    string FieldKey,
    string DisplayName,
    string DataType,
    bool IsSensitive,
    IReadOnlyCollection<string> SupportedAccessStates);

public sealed record AccountCompanyActionPolicyResponse(
    bool CanAccess,
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete);

public sealed record AccountCompanyFieldPolicyStateResponse(
    string FieldKey,
    string PropertyName,
    string DisplayName,
    string Access,
    bool IsRequired,
    bool IsSensitive);

public sealed record AccountCompanyResourcePolicyResponse(
    string ResourceKey,
    AccountCompanyActionPolicyResponse Actions,
    IReadOnlyCollection<AccountCompanyFieldPolicyStateResponse> Fields);

public sealed record AccountCompanyAccessContextResponse(
    ActiveCompanyDto CompanyContext,
    AccountCompanyCommercialContextResponse CommercialContext,
    IReadOnlyCollection<AccountCompanyEffectiveCapabilityResponse> EffectiveCapabilities,
    IReadOnlyCollection<AccountCompanyEffectiveModuleResponse> EffectiveModules,
    AccountCompanyCurrentUserAccessResponse CurrentUserAccess);

public sealed record AccountCompanyRoleBuilderCatalogResponse(
    IReadOnlyCollection<AccountCompanyEffectiveModuleResponse> AvailableModules,
    IReadOnlyCollection<AccountCompanyEffectiveCapabilityResponse> AvailableCapabilities,
    IReadOnlyCollection<AccountCompanyAccessPermissionResponse> AvailablePermissions,
    IReadOnlyCollection<AccountCompanyFieldPolicyCatalogItemResponse> FieldPoliciesCatalog,
    IReadOnlyCollection<AccountCompanyAccessScopeTypeResponse> ScopeTypes);

public sealed record GetOwnedCompanyAccessContextQuery(Guid CompanyId)
    : IQuery<AccountCompanyAccessContextResponse>;

public sealed record GetOwnedCompanyRoleBuilderCatalogQuery(Guid CompanyId)
    : IQuery<AccountCompanyRoleBuilderCatalogResponse>;

public sealed record GetOwnedCompanyResourcePolicyQuery(Guid CompanyId, string ResourceKey)
    : IQuery<AccountCompanyResourcePolicyResponse>;

internal sealed class GetOwnedCompanyAccessContextQueryValidator : AbstractValidator<GetOwnedCompanyAccessContextQuery>
{
    public GetOwnedCompanyAccessContextQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class GetOwnedCompanyRoleBuilderCatalogQueryValidator : AbstractValidator<GetOwnedCompanyRoleBuilderCatalogQuery>
{
    public GetOwnedCompanyRoleBuilderCatalogQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class GetOwnedCompanyResourcePolicyQueryValidator : AbstractValidator<GetOwnedCompanyResourcePolicyQuery>
{
    public GetOwnedCompanyResourcePolicyQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ResourceKey)
            .NotEmpty()
            .MaximumLength(100);
    }
}
