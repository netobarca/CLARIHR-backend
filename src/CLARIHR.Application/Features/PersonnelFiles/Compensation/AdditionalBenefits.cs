using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileAdditionalBenefitResponse(
    Guid AdditionalBenefitPublicId,
    string BenefitTypeCode,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => AdditionalBenefitPublicId;
}

public sealed record AdditionalBenefitInput(
    string BenefitTypeCode,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileAdditionalBenefitCommand(
    Guid PersonnelFileId,
    AdditionalBenefitInput Item)
    : ICommand<PersonnelFileAdditionalBenefitResponse>;

public sealed record UpdatePersonnelFileAdditionalBenefitCommand(
    Guid PersonnelFileId,
    Guid AdditionalBenefitPublicId,
    AdditionalBenefitInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileAdditionalBenefitResponse>;

public sealed record DeletePersonnelFileAdditionalBenefitCommand(
    Guid PersonnelFileId,
    Guid AdditionalBenefitPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileAdditionalBenefitPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileAdditionalBenefitCommand(
    Guid PersonnelFileId,
    Guid AdditionalBenefitPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileAdditionalBenefitPatchOperation> Operations)
    : ICommand<PersonnelFileAdditionalBenefitResponse>;

public sealed record GetPersonnelFileAdditionalBenefitsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>;

public sealed record GetPersonnelFileAdditionalBenefitByIdQuery(Guid PersonnelFileId, Guid AdditionalBenefitPublicId)
    : IQuery<PersonnelFileAdditionalBenefitResponse>;

internal sealed class AdditionalBenefitInputValidator : AbstractValidator<AdditionalBenefitInput>
{
    public AdditionalBenefitInputValidator()
    {
        RuleFor(input => input.BenefitTypeCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class AddPersonnelFileAdditionalBenefitCommandValidator : AbstractValidator<AddPersonnelFileAdditionalBenefitCommand>
{
    public AddPersonnelFileAdditionalBenefitCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AdditionalBenefitInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAdditionalBenefitCommandValidator : AbstractValidator<UpdatePersonnelFileAdditionalBenefitCommand>
{
    public UpdatePersonnelFileAdditionalBenefitCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.AdditionalBenefitPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AdditionalBenefitInputValidator());
    }
}

internal sealed class DeletePersonnelFileAdditionalBenefitCommandValidator : AbstractValidator<DeletePersonnelFileAdditionalBenefitCommand>
{
    public DeletePersonnelFileAdditionalBenefitCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.AdditionalBenefitPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileAdditionalBenefitCommandValidator : AbstractValidator<PatchPersonnelFileAdditionalBenefitCommand>
{
    public PatchPersonnelFileAdditionalBenefitCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.AdditionalBenefitPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Operations).NotEmpty();
        RuleFor(c => c.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(c => c.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class GetPersonnelFileAdditionalBenefitsQueryValidator : AbstractValidator<GetPersonnelFileAdditionalBenefitsQuery>
{
    public GetPersonnelFileAdditionalBenefitsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAdditionalBenefitByIdQueryValidator : AbstractValidator<GetPersonnelFileAdditionalBenefitByIdQuery>
{
    public GetPersonnelFileAdditionalBenefitByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.AdditionalBenefitPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileAdditionalBenefitPatchState
{
    public string BenefitTypeCode { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileAdditionalBenefitPatchState From(PersonnelFileAdditionalBenefitResponse response) =>
        new()
        {
            BenefitTypeCode = response.BenefitTypeCode,
            StartDate = response.StartDate,
            EndDate = response.EndDate,
            Notes = response.Notes,
            IsActive = response.IsActive
        };

    public AdditionalBenefitInput ToInput() =>
        new(
            BenefitTypeCode,
            StartDate,
            EndDate,
            IsActive,
            Notes);
}

