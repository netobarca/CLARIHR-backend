namespace CLARIHR.Application.Features.CompanyUsers.Common;

/// <summary>
/// Pagination bounds for the Company Users list/search. Single source of truth shared by the
/// handler FluentValidation (<c>GetCompanyUsersQueryValidator</c>) and the controller-boundary
/// <c>[Range(1, MaxPageSize)]</c> on <c>CompanyUsersController.List</c>, so the two enforcement
/// layers cannot drift (mirrors
/// <see cref="CLARIHR.Application.Features.CompetencyFramework.Common.CompetencyFrameworkValidationRules"/>).
/// </summary>
public static class CompanyUserValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
