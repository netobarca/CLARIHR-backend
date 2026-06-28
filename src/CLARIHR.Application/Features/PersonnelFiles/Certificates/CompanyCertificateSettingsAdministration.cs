using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Company-level certificate formatting settings (D-17): the configurable letterhead/firmante/pie.</summary>
public sealed record CompanyCertificateSettingsResponse(
    Guid? LogoFilePublicId,
    string? IssuingCity,
    string? SignatoryName,
    string? SignatoryTitle,
    string? FooterText,
    Guid ConcurrencyToken);

public sealed record GetCompanyCertificateSettingsQuery(Guid CompanyId)
    : IQuery<CompanyCertificateSettingsResponse>;

public sealed record UpdateCompanyCertificateSettingsCommand(
    Guid CompanyId,
    Guid? LogoFilePublicId,
    string? IssuingCity,
    string? SignatoryName,
    string? SignatoryTitle,
    string? FooterText,
    Guid ConcurrencyToken) : ICommand<CompanyCertificateSettingsResponse>;

internal sealed class GetCompanyCertificateSettingsQueryValidator : AbstractValidator<GetCompanyCertificateSettingsQuery>
{
    public GetCompanyCertificateSettingsQueryValidator() => RuleFor(query => query.CompanyId).NotEmpty();
}

internal sealed class UpdateCompanyCertificateSettingsCommandValidator : AbstractValidator<UpdateCompanyCertificateSettingsCommand>
{
    public UpdateCompanyCertificateSettingsCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.IssuingCity).MaximumLength(120);
        RuleFor(command => command.SignatoryName).MaximumLength(200);
        RuleFor(command => command.SignatoryTitle).MaximumLength(200);
        RuleFor(command => command.FooterText).MaximumLength(2000);
    }
}

internal sealed class GetCompanyCertificateSettingsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICompanyCertificateSettingsRepository settingsRepository)
    : IQueryHandler<GetCompanyCertificateSettingsQuery, CompanyCertificateSettingsResponse>
{
    public async Task<Result<CompanyCertificateSettingsResponse>> Handle(
        GetCompanyCertificateSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewCertificateRequestsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<CompanyCertificateSettingsResponse>.Failure(authorization.Error);
        }

        var settings = await settingsRepository.GetByTenantIdAsync(query.CompanyId, cancellationToken);
        return settings is null
            ? Result<CompanyCertificateSettingsResponse>.Success(new CompanyCertificateSettingsResponse(null, null, null, null, null, Guid.Empty))
            : Result<CompanyCertificateSettingsResponse>.Success(Map(settings));
    }

    internal static CompanyCertificateSettingsResponse Map(CompanyCertificateSettings settings) =>
        new(settings.LogoFilePublicId, settings.IssuingCity, settings.SignatoryName, settings.SignatoryTitle, settings.FooterText, settings.ConcurrencyToken);
}

internal sealed class UpdateCompanyCertificateSettingsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICompanyCertificateSettingsRepository settingsRepository,
    IFileRepository fileRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompanyCertificateSettingsCommand, CompanyCertificateSettingsResponse>
{
    public async Task<Result<CompanyCertificateSettingsResponse>> Handle(
        UpdateCompanyCertificateSettingsCommand command,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanManageCertificateRequestsAsync(command.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<CompanyCertificateSettingsResponse>.Failure(authorization.Error);
        }

        // The logo, when supplied, must be an active CompanyLogo file of this tenant.
        if (command.LogoFilePublicId is { } logoId && logoId != Guid.Empty)
        {
            var logoFile = await fileRepository.GetByPublicIdAsync(logoId, cancellationToken);
            if (logoFile is null)
            {
                return Result<CompanyCertificateSettingsResponse>.Failure(FileErrors.FileNotFound);
            }

            if (logoFile.Status != FileStatus.Active)
            {
                return Result<CompanyCertificateSettingsResponse>.Failure(FileErrors.FileNotActive);
            }

            if (logoFile.TenantId != command.CompanyId)
            {
                return Result<CompanyCertificateSettingsResponse>.Failure(FileErrors.FileTenantMismatch);
            }

            if (logoFile.Purpose != FilePurpose.CompanyLogo)
            {
                return Result<CompanyCertificateSettingsResponse>.Failure(FileErrors.InvalidPurpose(logoFile.Purpose.ToString()));
            }
        }

        var settings = await settingsRepository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (settings is null)
        {
            // First write provisions the row (the client sent the empty token from the default GET).
            settings = CompanyCertificateSettings.Create();
            settings.SetTenantId(command.CompanyId);
            settings.Update(command.LogoFilePublicId, command.IssuingCity, command.SignatoryName, command.SignatoryTitle, command.FooterText);
            settingsRepository.Add(settings);
        }
        else
        {
            if (settings.ConcurrencyToken != command.ConcurrencyToken)
            {
                return Result<CompanyCertificateSettingsResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            settings.Update(command.LogoFilePublicId, command.IssuingCity, command.SignatoryName, command.SignatoryTitle, command.FooterText);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<CompanyCertificateSettingsResponse>.Success(GetCompanyCertificateSettingsQueryHandler.Map(settings));
    }
}
