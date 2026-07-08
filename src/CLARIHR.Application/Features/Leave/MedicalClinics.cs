using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using FluentValidation;

namespace CLARIHR.Application.Features.Leave;

public sealed record MedicalClinicListItemResponse(
    Guid Id,
    string Description,
    string? Specialty,
    string? SectorCode,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record MedicalClinicResponse(
    Guid Id,
    string Description,
    string? Specialty,
    string? SectorCode,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchMedicalClinicsQuery(
    Guid CompanyId,
    bool? IsActive,
    string? SectorCode,
    string? Search,
    int PageNumber = 1,
    int PageSize = LeaveConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<MedicalClinicListItemResponse>>;

public sealed record GetMedicalClinicByIdQuery(Guid MedicalClinicId) : IQuery<MedicalClinicResponse>;

public sealed record CreateMedicalClinicCommand(
    Guid CompanyId,
    string Description,
    string? Specialty,
    string? SectorCode)
    : ICommand<MedicalClinicResponse>;

public sealed record UpdateMedicalClinicCommand(
    Guid MedicalClinicId,
    string Description,
    string? Specialty,
    string? SectorCode,
    Guid ConcurrencyToken)
    : ICommand<MedicalClinicResponse>;

public sealed record ActivateMedicalClinicCommand(Guid MedicalClinicId, Guid ConcurrencyToken)
    : ICommand<MedicalClinicResponse>;

public sealed record InactivateMedicalClinicCommand(Guid MedicalClinicId, Guid ConcurrencyToken)
    : ICommand<MedicalClinicResponse>;

public static class MedicalClinicErrors
{
    public static readonly Error MedicalClinicNotFound = new(
        "MEDICAL_CLINIC_NOT_FOUND",
        "The medical clinic could not be found.",
        ErrorType.NotFound);

    public static readonly Error DescriptionConflict = new(
        "MEDICAL_CLINIC_DESCRIPTION_CONFLICT",
        "Another medical clinic already uses the requested description.",
        ErrorType.Conflict);

    public static readonly Error SectorInvalid = new(
        "MEDICAL_CLINIC_SECTOR_INVALID",
        "The clinic sector code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LeaveConfigurationPermissionCodes.MedicalClinicsResourceKey, action);
}

internal sealed class SearchMedicalClinicsQueryValidator : AbstractValidator<SearchMedicalClinicsQuery>
{
    public SearchMedicalClinicsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.SectorCode).MaximumLength(MedicalClinic.MaxSectorCodeLength);
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(LeaveConfigurationValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {LeaveConfigurationValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LeaveConfigurationValidationRules.MaxPageSize);
    }
}

internal sealed class GetMedicalClinicByIdQueryValidator : AbstractValidator<GetMedicalClinicByIdQuery>
{
    public GetMedicalClinicByIdQueryValidator()
    {
        RuleFor(query => query.MedicalClinicId).NotEmpty();
    }
}

internal sealed class CreateMedicalClinicCommandValidator : AbstractValidator<CreateMedicalClinicCommand>
{
    public CreateMedicalClinicCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Description).NotEmpty().MaximumLength(MedicalClinic.MaxDescriptionLength);
        RuleFor(command => command.Specialty).MaximumLength(MedicalClinic.MaxSpecialtyLength);
        RuleFor(command => command.SectorCode).MaximumLength(MedicalClinic.MaxSectorCodeLength);
    }
}

internal sealed class UpdateMedicalClinicCommandValidator : AbstractValidator<UpdateMedicalClinicCommand>
{
    public UpdateMedicalClinicCommandValidator()
    {
        RuleFor(command => command.MedicalClinicId).NotEmpty();
        RuleFor(command => command.Description).NotEmpty().MaximumLength(MedicalClinic.MaxDescriptionLength);
        RuleFor(command => command.Specialty).MaximumLength(MedicalClinic.MaxSpecialtyLength);
        RuleFor(command => command.SectorCode).MaximumLength(MedicalClinic.MaxSectorCodeLength);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateMedicalClinicCommandValidator : AbstractValidator<ActivateMedicalClinicCommand>
{
    public ActivateMedicalClinicCommandValidator()
    {
        RuleFor(command => command.MedicalClinicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateMedicalClinicCommandValidator : AbstractValidator<InactivateMedicalClinicCommand>
{
    public InactivateMedicalClinicCommandValidator()
    {
        RuleFor(command => command.MedicalClinicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
