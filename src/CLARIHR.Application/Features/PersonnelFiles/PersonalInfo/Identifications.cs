using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;
using FluentValidation.Results;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileIdentificationResponse(
    Guid IdentificationPublicId,
    string IdentificationTypeCode,
    string? IdentificationTypeName,
    string IdentificationNumber,
    DateTime? IssuedDate,
    DateTime? ExpiryDate,
    string? Issuer,
    bool IsPrimary,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => IdentificationPublicId;
}

public sealed record GetPersonnelFileIdentificationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileIdentificationResponse>>;

public sealed record GetPersonnelFileIdentificationByIdQuery(Guid PersonnelFileId, Guid IdentificationPublicId)
    : IQuery<PersonnelFileIdentificationResponse>;

public sealed record AddPersonnelFileIdentificationCommand(
    Guid PersonnelFileId,
    IdentificationInput Identification)
    : ICommand<PersonnelFileIdentificationResponse>;

public sealed record UpdatePersonnelFileIdentificationCommand(
    Guid PersonnelFileId,
    Guid IdentificationPublicId,
    IdentificationInput Identification,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileIdentificationResponse>;

public sealed record DeletePersonnelFileIdentificationCommand(
    Guid PersonnelFileId,
    Guid IdentificationPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileIdentificationPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileIdentificationCommand(
    Guid PersonnelFileId,
    Guid IdentificationPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileIdentificationPatchOperation> Operations)
    : ICommand<PersonnelFileIdentificationResponse>;

public sealed record IdentificationInput(
    string IdentificationTypeCode,
    string IdentificationNumber,
    DateTime? IssuedDate,
    DateTime? ExpiryDate,
    string? Issuer,
    bool IsPrimary = false);

internal sealed class GetPersonnelFileIdentificationsQueryValidator : AbstractValidator<GetPersonnelFileIdentificationsQuery>
{
    public GetPersonnelFileIdentificationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileIdentificationByIdQueryValidator : AbstractValidator<GetPersonnelFileIdentificationByIdQuery>
{
    public GetPersonnelFileIdentificationByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.IdentificationPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileIdentificationCommandValidator : AbstractValidator<AddPersonnelFileIdentificationCommand>
{
    public AddPersonnelFileIdentificationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Identification).SetValidator(new IdentificationInputValidator());
    }
}

internal sealed class UpdatePersonnelFileIdentificationCommandValidator : AbstractValidator<UpdatePersonnelFileIdentificationCommand>
{
    public UpdatePersonnelFileIdentificationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IdentificationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Identification).SetValidator(new IdentificationInputValidator());
    }
}

internal sealed class DeletePersonnelFileIdentificationCommandValidator : AbstractValidator<DeletePersonnelFileIdentificationCommand>
{
    public DeletePersonnelFileIdentificationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IdentificationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileIdentificationCommandValidator : AbstractValidator<PatchPersonnelFileIdentificationCommand>
{
    public PatchPersonnelFileIdentificationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IdentificationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class IdentificationInputValidator : AbstractValidator<IdentificationInput>
{
    public IdentificationInputValidator()
    {
        RuleFor(input => input.IdentificationTypeCode)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .WithMessage("IdentificationTypeCode format is invalid.");
        RuleFor(input => input.IdentificationNumber)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .WithMessage("IdentificationNumber format is invalid.");
        RuleFor(input => input)
            .Must(static input => !input.IssuedDate.HasValue || !input.ExpiryDate.HasValue || input.ExpiryDate.Value.Date >= input.IssuedDate.Value.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
    }
}

internal sealed class PersonnelFileIdentificationPatchState
{
    public string IdentificationTypeCode { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Issuer { get; set; }
    public bool IsPrimary { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileIdentificationPatchState From(PersonnelFileIdentificationResponse response) =>
        new()
        {
            IdentificationTypeCode = response.IdentificationTypeCode,
            IdentificationNumber = response.IdentificationNumber,
            IssuedDate = response.IssuedDate,
            ExpiryDate = response.ExpiryDate,
            Issuer = response.Issuer,
            IsPrimary = response.IsPrimary
        };

    public IdentificationInput ToInput() =>
        new(
            IdentificationTypeCode,
            IdentificationNumber,
            IssuedDate,
            ExpiryDate,
            Issuer,
            IsPrimary);
}

