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

public sealed record PersonnelFilePreviousEmploymentResponse(
    Guid PreviousEmploymentPublicId,
    string Institution,
    string? Place,
    string? LastPosition,
    string? ManagerName,
    DateTime EntryDate,
    DateTime? RetirementDate,
    string? CompanyPhone,
    string? ExitReason,
    decimal? FirstSalaryAmount,
    decimal? LastSalaryAmount,
    decimal? AverageCommissionAmount,
    string CurrencyCode,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => PreviousEmploymentPublicId;
}

public sealed record GetPersonnelFilePreviousEmploymentsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>;

public sealed record GetPersonnelFilePreviousEmploymentByIdQuery(Guid PersonnelFileId, Guid PreviousEmploymentPublicId)
    : IQuery<PersonnelFilePreviousEmploymentResponse>;

public sealed record AddPersonnelFilePreviousEmploymentCommand(
    Guid PersonnelFileId,
    PreviousEmploymentInput PreviousEmployment)
    : ICommand<PersonnelFilePreviousEmploymentResponse>;

public sealed record UpdatePersonnelFilePreviousEmploymentCommand(
    Guid PersonnelFileId,
    Guid PreviousEmploymentPublicId,
    PreviousEmploymentInput PreviousEmployment,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFilePreviousEmploymentResponse>;

public sealed record DeletePersonnelFilePreviousEmploymentCommand(
    Guid PersonnelFileId,
    Guid PreviousEmploymentPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFilePreviousEmploymentPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFilePreviousEmploymentCommand(
    Guid PersonnelFileId,
    Guid PreviousEmploymentPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFilePreviousEmploymentPatchOperation> Operations)
    : ICommand<PersonnelFilePreviousEmploymentResponse>;

public sealed record PreviousEmploymentInput(
    string Institution,
    string? Place,
    string? LastPosition,
    string? ManagerName,
    DateTime EntryDate,
    DateTime? RetirementDate,
    string? CompanyPhone,
    string? ExitReason,
    decimal? FirstSalaryAmount,
    decimal? LastSalaryAmount,
    decimal? AverageCommissionAmount,
    string CurrencyCode);

internal sealed class GetPersonnelFilePreviousEmploymentsQueryValidator : AbstractValidator<GetPersonnelFilePreviousEmploymentsQuery>
{
    public GetPersonnelFilePreviousEmploymentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFilePreviousEmploymentByIdQueryValidator : AbstractValidator<GetPersonnelFilePreviousEmploymentByIdQuery>
{
    public GetPersonnelFilePreviousEmploymentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.PreviousEmploymentPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFilePreviousEmploymentCommandValidator : AbstractValidator<AddPersonnelFilePreviousEmploymentCommand>
{
    public AddPersonnelFilePreviousEmploymentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.PreviousEmployment).SetValidator(new PreviousEmploymentInputValidator());
    }
}

internal sealed class UpdatePersonnelFilePreviousEmploymentCommandValidator : AbstractValidator<UpdatePersonnelFilePreviousEmploymentCommand>
{
    public UpdatePersonnelFilePreviousEmploymentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.PreviousEmploymentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.PreviousEmployment).SetValidator(new PreviousEmploymentInputValidator());
    }
}

internal sealed class DeletePersonnelFilePreviousEmploymentCommandValidator : AbstractValidator<DeletePersonnelFilePreviousEmploymentCommand>
{
    public DeletePersonnelFilePreviousEmploymentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.PreviousEmploymentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFilePreviousEmploymentCommandValidator : AbstractValidator<PatchPersonnelFilePreviousEmploymentCommand>
{
    public PatchPersonnelFilePreviousEmploymentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.PreviousEmploymentPublicId).NotEmpty();
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

internal sealed class PreviousEmploymentInputValidator : AbstractValidator<PreviousEmploymentInput>
{
    public PreviousEmploymentInputValidator()
    {
        RuleFor(input => input.Institution).NotEmpty().MaximumLength(200);
        RuleFor(input => input.Place).MaximumLength(200);
        RuleFor(input => input.LastPosition).MaximumLength(150);
        RuleFor(input => input.ManagerName).MaximumLength(150);
        RuleFor(input => input.CompanyPhone)
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .When(input => !string.IsNullOrWhiteSpace(input.CompanyPhone))
            .WithMessage("CompanyPhone format is invalid.");
        RuleFor(input => input.ExitReason).MaximumLength(500);
        RuleFor(input => input.FirstSalaryAmount).GreaterThanOrEqualTo(0).When(static input => input.FirstSalaryAmount.HasValue);
        RuleFor(input => input.LastSalaryAmount).GreaterThanOrEqualTo(0).When(static input => input.LastSalaryAmount.HasValue);
        RuleFor(input => input.AverageCommissionAmount).GreaterThanOrEqualTo(0).When(static input => input.AverageCommissionAmount.HasValue);
        RuleFor(input => input.CurrencyCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input)
            .Must(static input => !input.RetirementDate.HasValue || input.RetirementDate.Value.Date >= input.EntryDate.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
    }
}

internal sealed class PersonnelFilePreviousEmploymentPatchState
{
    public string Institution { get; set; } = string.Empty;
    public string? Place { get; set; }
    public string? LastPosition { get; set; }
    public string? ManagerName { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime? RetirementDate { get; set; }
    public string? CompanyPhone { get; set; }
    public string? ExitReason { get; set; }
    public decimal? FirstSalaryAmount { get; set; }
    public decimal? LastSalaryAmount { get; set; }
    public decimal? AverageCommissionAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool HasMutation { get; set; }

    public static PersonnelFilePreviousEmploymentPatchState From(PersonnelFilePreviousEmploymentResponse response) =>
        new()
        {
            Institution = response.Institution,
            Place = response.Place,
            LastPosition = response.LastPosition,
            ManagerName = response.ManagerName,
            EntryDate = response.EntryDate,
            RetirementDate = response.RetirementDate,
            CompanyPhone = response.CompanyPhone,
            ExitReason = response.ExitReason,
            FirstSalaryAmount = response.FirstSalaryAmount,
            LastSalaryAmount = response.LastSalaryAmount,
            AverageCommissionAmount = response.AverageCommissionAmount,
            CurrencyCode = response.CurrencyCode
        };

    public PreviousEmploymentInput ToInput() =>
        new(
            Institution,
            Place,
            LastPosition,
            ManagerName,
            EntryDate,
            RetirementDate,
            CompanyPhone,
            ExitReason,
            FirstSalaryAmount,
            LastSalaryAmount,
            AverageCommissionAmount,
            CurrencyCode);
}

