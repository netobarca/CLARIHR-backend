using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Compliance;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.Compliance;
using FluentValidation;

namespace CLARIHR.Application.Features.Compliance;

public sealed record CompanyLegalProfileResponse(
    Guid Id,
    string LegalName,
    string EmployerNitNumber,
    string IsssEmployerRegistrationNumber,
    string FiscalAddress,
    string? EconomicActivityDescription,
    Guid? LegalRepresentativePublicId,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record GetCompanyLegalProfileQuery(Guid CompanyId) : IQuery<CompanyLegalProfileResponse>;

public sealed record CreateCompanyLegalProfileCommand(
    Guid CompanyId,
    string LegalName,
    string EmployerNitNumber,
    string IsssEmployerRegistrationNumber,
    string FiscalAddress,
    string? EconomicActivityDescription,
    Guid? LegalRepresentativePublicId) : ICommand<CompanyLegalProfileResponse>;

public sealed record UpdateCompanyLegalProfileCommand(
    Guid CompanyId,
    string LegalName,
    string EmployerNitNumber,
    string IsssEmployerRegistrationNumber,
    string FiscalAddress,
    string? EconomicActivityDescription,
    Guid? LegalRepresentativePublicId,
    Guid ConcurrencyToken) : ICommand<CompanyLegalProfileResponse>;

public static class CompanyLegalProfileErrors
{
    public static readonly Error NotFound = new(
        "COMPANY_LEGAL_PROFILE_NOT_FOUND",
        "The company does not have a legal profile configured yet.",
        ErrorType.NotFound);

    public static readonly Error AlreadyExists = new(
        "COMPANY_LEGAL_PROFILE_ALREADY_EXISTS",
        "The company already has a legal profile; use the update endpoint instead.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "COMPANY_LEGAL_PROFILE_CONCURRENCY_CONFLICT",
        "The company legal profile was modified by someone else; reload and try again.",
        ErrorType.Conflict);
}

internal abstract class CompanyLegalProfileCommandValidatorBase<TCommand>
    : AbstractValidator<TCommand>
    where TCommand : notnull
{
    protected void ApplySharedRules(
        Func<TCommand, Guid> companyId,
        Func<TCommand, string> legalName,
        Func<TCommand, string> employerNitNumber,
        Func<TCommand, string> isssEmployerRegistrationNumber,
        Func<TCommand, string> fiscalAddress,
        Func<TCommand, string?> economicActivityDescription)
    {
        RuleFor(command => companyId(command)).NotEmpty();
        RuleFor(command => legalName(command)).NotEmpty().MaximumLength(200);
        RuleFor(command => employerNitNumber(command)).NotEmpty().MaximumLength(20);
        RuleFor(command => isssEmployerRegistrationNumber(command)).NotEmpty().MaximumLength(20);
        RuleFor(command => fiscalAddress(command)).NotEmpty().MaximumLength(500);
        RuleFor(command => economicActivityDescription(command)).MaximumLength(200);
    }
}

internal sealed class GetCompanyLegalProfileQueryValidator : AbstractValidator<GetCompanyLegalProfileQuery>
{
    public GetCompanyLegalProfileQueryValidator() => RuleFor(query => query.CompanyId).NotEmpty();
}

internal sealed class CreateCompanyLegalProfileCommandValidator
    : CompanyLegalProfileCommandValidatorBase<CreateCompanyLegalProfileCommand>
{
    public CreateCompanyLegalProfileCommandValidator() =>
        ApplySharedRules(
            static command => command.CompanyId,
            static command => command.LegalName,
            static command => command.EmployerNitNumber,
            static command => command.IsssEmployerRegistrationNumber,
            static command => command.FiscalAddress,
            static command => command.EconomicActivityDescription);
}

internal sealed class UpdateCompanyLegalProfileCommandValidator
    : CompanyLegalProfileCommandValidatorBase<UpdateCompanyLegalProfileCommand>
{
    public UpdateCompanyLegalProfileCommandValidator()
    {
        ApplySharedRules(
            static command => command.CompanyId,
            static command => command.LegalName,
            static command => command.EmployerNitNumber,
            static command => command.IsssEmployerRegistrationNumber,
            static command => command.FiscalAddress,
            static command => command.EconomicActivityDescription);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetCompanyLegalProfileQueryHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyLegalProfileRepository repository)
    : IQueryHandler<GetCompanyLegalProfileQuery, CompanyLegalProfileResponse>
{
    public async Task<Result<CompanyLegalProfileResponse>> Handle(
        GetCompanyLegalProfileQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyLegalProfileResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByTenantIdAsync(query.CompanyId, cancellationToken);
        return profile is null
            ? Result<CompanyLegalProfileResponse>.Failure(CompanyLegalProfileErrors.NotFound)
            : Result<CompanyLegalProfileResponse>.Success(CompanyLegalProfileAdministrationHelpers.Map(profile));
    }
}

internal sealed class CreateCompanyLegalProfileCommandHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyLegalProfileRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCompanyLegalProfileCommand, CompanyLegalProfileResponse>
{
    public async Task<Result<CompanyLegalProfileResponse>> Handle(
        CreateCompanyLegalProfileCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyLegalProfileResponse>.Failure(authorizationResult.Error);
        }

        var existing = await repository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (existing is not null)
        {
            return Result<CompanyLegalProfileResponse>.Failure(CompanyLegalProfileErrors.AlreadyExists);
        }

        var profile = CompanyLegalProfile.Create(
            command.LegalName,
            command.EmployerNitNumber,
            command.IsssEmployerRegistrationNumber,
            command.FiscalAddress,
            command.EconomicActivityDescription,
            command.LegalRepresentativePublicId);
        profile.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(profile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CompanyLegalProfileAdministrationHelpers.Map(profile);
            await auditService.LogForTenantAsync(
                command.CompanyId,
                new AuditLogEntry(
                    AuditEventTypes.CompanyLegalProfileSaved,
                    AuditEntityTypes.CompanyLegalProfile,
                    profile.PublicId,
                    after.LegalName,
                    AuditActions.Create,
                    $"Created the company legal profile ({after.LegalName}, NIT {after.EmployerNitNumber}).",
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompanyLegalProfileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompanyLegalProfileCommandHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyLegalProfileRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompanyLegalProfileCommand, CompanyLegalProfileResponse>
{
    public async Task<Result<CompanyLegalProfileResponse>> Handle(
        UpdateCompanyLegalProfileCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyLegalProfileResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (profile is null)
        {
            return Result<CompanyLegalProfileResponse>.Failure(CompanyLegalProfileErrors.NotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyLegalProfileResponse>.Failure(CompanyLegalProfileErrors.ConcurrencyConflict);
        }

        var before = CompanyLegalProfileAdministrationHelpers.Map(profile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.Update(
                command.LegalName,
                command.EmployerNitNumber,
                command.IsssEmployerRegistrationNumber,
                command.FiscalAddress,
                command.EconomicActivityDescription,
                command.LegalRepresentativePublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CompanyLegalProfileAdministrationHelpers.Map(profile);
            await auditService.LogForTenantAsync(
                command.CompanyId,
                new AuditLogEntry(
                    AuditEventTypes.CompanyLegalProfileSaved,
                    AuditEntityTypes.CompanyLegalProfile,
                    profile.PublicId,
                    after.LegalName,
                    AuditActions.Update,
                    $"Updated the company legal profile ({after.LegalName}, NIT {after.EmployerNitNumber}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompanyLegalProfileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CompanyLegalProfileAdministrationHelpers
{
    public static CompanyLegalProfileResponse Map(CompanyLegalProfile profile) =>
        new(
            profile.PublicId,
            profile.LegalName,
            profile.EmployerNitNumber,
            profile.IsssEmployerRegistrationNumber,
            profile.FiscalAddress,
            profile.EconomicActivityDescription,
            profile.LegalRepresentativePublicId,
            profile.ConcurrencyToken,
            profile.CreatedUtc,
            profile.ModifiedUtc);
}
