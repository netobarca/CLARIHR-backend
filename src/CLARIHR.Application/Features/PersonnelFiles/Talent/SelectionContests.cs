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

public sealed record PersonnelFileSelectionContestResponse(
    Guid SelectionContestPublicId,
    string ContestCode,
    string ContestName,
    DateTime ContestDateUtc,
    string ResultCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => SelectionContestPublicId;
}

public sealed record SelectionContestInput(
    string ContestCode,
    string ContestName,
    DateTime ContestDateUtc,
    string ResultCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFileSelectionContestCommand(
    Guid PersonnelFileId,
    SelectionContestInput Item)
    : ICommand<PersonnelFileSelectionContestResponse>;

public sealed record UpdatePersonnelFileSelectionContestCommand(
    Guid PersonnelFileId,
    Guid SelectionContestPublicId,
    SelectionContestInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSelectionContestResponse>;

public sealed record DeletePersonnelFileSelectionContestCommand(
    Guid PersonnelFileId,
    Guid SelectionContestPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileSelectionContestPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileSelectionContestCommand(
    Guid PersonnelFileId,
    Guid SelectionContestPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileSelectionContestPatchOperation> Operations)
    : ICommand<PersonnelFileSelectionContestResponse>;

public sealed record GetPersonnelFileSelectionContestsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>;

public sealed record GetPersonnelFileSelectionContestByIdQuery(Guid PersonnelFileId, Guid SelectionContestPublicId)
    : IQuery<PersonnelFileSelectionContestResponse>;

internal sealed class SelectionContestInputValidator : AbstractValidator<SelectionContestInput>
{
    public SelectionContestInputValidator()
    {
        RuleFor(input => input.ContestCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.ContestName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.ResultCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class AddPersonnelFileSelectionContestCommandValidator : AbstractValidator<AddPersonnelFileSelectionContestCommand>
{
    public AddPersonnelFileSelectionContestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new SelectionContestInputValidator());
    }
}

internal sealed class UpdatePersonnelFileSelectionContestCommandValidator : AbstractValidator<UpdatePersonnelFileSelectionContestCommand>
{
    public UpdatePersonnelFileSelectionContestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.SelectionContestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new SelectionContestInputValidator());
    }
}

internal sealed class DeletePersonnelFileSelectionContestCommandValidator : AbstractValidator<DeletePersonnelFileSelectionContestCommand>
{
    public DeletePersonnelFileSelectionContestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.SelectionContestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileSelectionContestCommandValidator : AbstractValidator<PatchPersonnelFileSelectionContestCommand>
{
    public PatchPersonnelFileSelectionContestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.SelectionContestPublicId).NotEmpty();
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

internal sealed class GetPersonnelFileSelectionContestByIdQueryValidator : AbstractValidator<GetPersonnelFileSelectionContestByIdQuery>
{
    public GetPersonnelFileSelectionContestByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.SelectionContestPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileSelectionContestPatchState
{
    public string ContestCode { get; set; } = string.Empty;
    public string ContestName { get; set; } = string.Empty;
    public DateTime ContestDateUtc { get; set; }
    public string ResultCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileSelectionContestPatchState From(PersonnelFileSelectionContestResponse response) =>
        new()
        {
            ContestCode = response.ContestCode,
            ContestName = response.ContestName,
            ContestDateUtc = response.ContestDateUtc,
            ResultCode = response.ResultCode,
            Notes = response.Notes,
            SourceSystem = response.SourceSystem,
            SourceReference = response.SourceReference,
            SourceSyncedUtc = response.SourceSyncedUtc
        };

    public SelectionContestInput ToInput() =>
        new(
            ContestCode,
            ContestName,
            ContestDateUtc,
            ResultCode,
            Notes,
            SourceSystem,
            SourceReference,
            SourceSyncedUtc);
}

