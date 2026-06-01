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

public sealed record PersonnelFilePositionCompetencyResultResponse(
    Guid PositionCompetencyResultPublicId,
    string CompetencyCode,
    string? DesiredBehaviors,
    decimal? ExpectedScore,
    decimal? AchievedScore,
    decimal? GapScore,
    DateTime? EvaluationDateUtc,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => PositionCompetencyResultPublicId;
}

public sealed record PositionCompetencyResultInput(
    string CompetencyCode,
    string? DesiredBehaviors,
    decimal? ExpectedScore,
    decimal? AchievedScore,
    decimal? GapScore,
    DateTime? EvaluationDateUtc,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFilePositionCompetencyResultCommand(
    Guid PersonnelFileId,
    PositionCompetencyResultInput Item)
    : ICommand<PersonnelFilePositionCompetencyResultResponse>;

public sealed record UpdatePersonnelFilePositionCompetencyResultCommand(
    Guid PersonnelFileId,
    Guid PositionCompetencyResultPublicId,
    PositionCompetencyResultInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFilePositionCompetencyResultResponse>;

public sealed record DeletePersonnelFilePositionCompetencyResultCommand(
    Guid PersonnelFileId,
    Guid PositionCompetencyResultPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFilePositionCompetencyResultPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFilePositionCompetencyResultCommand(
    Guid PersonnelFileId,
    Guid PositionCompetencyResultPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFilePositionCompetencyResultPatchOperation> Operations)
    : ICommand<PersonnelFilePositionCompetencyResultResponse>;

public sealed record GetPersonnelFilePositionCompetencyResultsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>;

public sealed record GetPersonnelFilePositionCompetencyResultByIdQuery(Guid PersonnelFileId, Guid PositionCompetencyResultPublicId)
    : IQuery<PersonnelFilePositionCompetencyResultResponse>;

internal sealed class PositionCompetencyResultInputValidator : AbstractValidator<PositionCompetencyResultInput>
{
    public PositionCompetencyResultInputValidator()
    {
        RuleFor(input => input.CompetencyCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class AddPersonnelFilePositionCompetencyResultCommandValidator : AbstractValidator<AddPersonnelFilePositionCompetencyResultCommand>
{
    public AddPersonnelFilePositionCompetencyResultCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PositionCompetencyResultInputValidator());
    }
}

internal sealed class UpdatePersonnelFilePositionCompetencyResultCommandValidator : AbstractValidator<UpdatePersonnelFilePositionCompetencyResultCommand>
{
    public UpdatePersonnelFilePositionCompetencyResultCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.PositionCompetencyResultPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PositionCompetencyResultInputValidator());
    }
}

internal sealed class DeletePersonnelFilePositionCompetencyResultCommandValidator : AbstractValidator<DeletePersonnelFilePositionCompetencyResultCommand>
{
    public DeletePersonnelFilePositionCompetencyResultCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.PositionCompetencyResultPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFilePositionCompetencyResultCommandValidator : AbstractValidator<PatchPersonnelFilePositionCompetencyResultCommand>
{
    public PatchPersonnelFilePositionCompetencyResultCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.PositionCompetencyResultPublicId).NotEmpty();
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

internal sealed class GetPersonnelFilePositionCompetencyResultByIdQueryValidator : AbstractValidator<GetPersonnelFilePositionCompetencyResultByIdQuery>
{
    public GetPersonnelFilePositionCompetencyResultByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.PositionCompetencyResultPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFilePositionCompetencyResultPatchState
{
    public string CompetencyCode { get; set; } = string.Empty;
    public string? DesiredBehaviors { get; set; }
    public decimal? ExpectedScore { get; set; }
    public decimal? AchievedScore { get; set; }
    public decimal? GapScore { get; set; }
    public DateTime? EvaluationDateUtc { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFilePositionCompetencyResultPatchState From(PersonnelFilePositionCompetencyResultResponse response) =>
        new()
        {
            CompetencyCode = response.CompetencyCode,
            DesiredBehaviors = response.DesiredBehaviors,
            ExpectedScore = response.ExpectedScore,
            AchievedScore = response.AchievedScore,
            GapScore = response.GapScore,
            EvaluationDateUtc = response.EvaluationDateUtc,
            SourceSystem = response.SourceSystem,
            SourceReference = response.SourceReference,
            SourceSyncedUtc = response.SourceSyncedUtc
        };

    public PositionCompetencyResultInput ToInput() =>
        new(
            CompetencyCode,
            DesiredBehaviors,
            ExpectedScore,
            AchievedScore,
            GapScore,
            EvaluationDateUtc,
            SourceSystem,
            SourceReference,
            SourceSyncedUtc);
}

