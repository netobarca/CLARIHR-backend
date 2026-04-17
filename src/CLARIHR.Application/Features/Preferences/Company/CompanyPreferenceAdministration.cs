using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Preferences.Common;
using CLARIHR.Domain.Preferences;
using FluentValidation;

namespace CLARIHR.Application.Features.Preferences.Company;

public sealed record CompanyPreferenceResponse(
    Guid Id,
    string CurrencyCode,
    string TimeZone,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record GetCompanyPreferencesQuery(Guid CompanyId) : IQuery<CompanyPreferenceResponse>;

public sealed record UpdateCompanyPreferencesCommand(
    Guid CompanyId,
    string CurrencyCode,
    string TimeZone,
    Guid ConcurrencyToken) : ICommand<CompanyPreferenceResponse>;

internal sealed class GetCompanyPreferencesQueryValidator : AbstractValidator<GetCompanyPreferencesQuery>
{
    public GetCompanyPreferencesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class UpdateCompanyPreferencesCommandValidator : AbstractValidator<UpdateCompanyPreferencesCommand>
{
    public UpdateCompanyPreferencesCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.CurrencyCode)
            .NotEmpty()
            .Length(3);
        RuleFor(command => command.TimeZone)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetCompanyPreferencesQueryHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyPreferenceRepository companyPreferenceRepository)
    : IQueryHandler<GetCompanyPreferencesQuery, CompanyPreferenceResponse>
{
    public async Task<Result<CompanyPreferenceResponse>> Handle(
        GetCompanyPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(authorizationResult.Error);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(query.CompanyId, cancellationToken);
        return preference is null
            ? Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.CompanyPreferenceNotFound)
            : Result<CompanyPreferenceResponse>.Success(CompanyPreferenceAdministrationHelpers.Map(preference));
    }
}

internal sealed class UpdateCompanyPreferencesCommandHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompanyPreferencesCommand, CompanyPreferenceResponse>
{
    public async Task<Result<CompanyPreferenceResponse>> Handle(
        UpdateCompanyPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(authorizationResult.Error);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (preference is null)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.CompanyPreferenceNotFound);
        }

        if (preference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        preference.Update(command.CurrencyCode, command.TimeZone);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CompanyPreferenceResponse>.Success(CompanyPreferenceAdministrationHelpers.Map(preference));
    }
}

internal static class CompanyPreferenceAdministrationHelpers
{
    public static CompanyPreferenceResponse Map(CompanyPreference preference) =>
        new(
            preference.PublicId,
            preference.CurrencyCode,
            preference.TimeZone,
            preference.ConcurrencyToken,
            preference.CreatedUtc,
            preference.ModifiedUtc);
}
