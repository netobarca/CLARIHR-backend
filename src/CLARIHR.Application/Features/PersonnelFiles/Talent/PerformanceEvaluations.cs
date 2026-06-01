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

public sealed record PersonnelFilePerformanceEvaluationResponse(
    Guid EvaluationPublicId,
    string EvaluatorName,
    DateTime EvaluationDateUtc,
    decimal? Score,
    string? QualitativeScoreCode,
    string? Comment,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => EvaluationPublicId;
}

public sealed record PerformanceEvaluationInput(
    string EvaluatorName,
    DateTime EvaluationDateUtc,
    decimal? Score,
    string? QualitativeScoreCode,
    string? Comment,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFilePerformanceEvaluationCommand(
    Guid PersonnelFileId,
    PerformanceEvaluationInput Item)
    : ICommand<PersonnelFilePerformanceEvaluationResponse>;

public sealed record UpdatePersonnelFilePerformanceEvaluationCommand(
    Guid PersonnelFileId,
    Guid EvaluationPublicId,
    PerformanceEvaluationInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFilePerformanceEvaluationResponse>;

public sealed record DeletePersonnelFilePerformanceEvaluationCommand(
    Guid PersonnelFileId,
    Guid EvaluationPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFilePerformanceEvaluationPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFilePerformanceEvaluationCommand(
    Guid PersonnelFileId,
    Guid EvaluationPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFilePerformanceEvaluationPatchOperation> Operations)
    : ICommand<PersonnelFilePerformanceEvaluationResponse>;

public sealed record GetPersonnelFilePerformanceEvaluationsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>;

public sealed record GetPersonnelFilePerformanceEvaluationByIdQuery(Guid PersonnelFileId, Guid EvaluationPublicId)
    : IQuery<PersonnelFilePerformanceEvaluationResponse>;

internal sealed class PerformanceEvaluationInputValidator : AbstractValidator<PerformanceEvaluationInput>
{
    public PerformanceEvaluationInputValidator()
    {
        RuleFor(input => input.EvaluatorName).NotEmpty().MaximumLength(200);
    }
}

internal sealed class AddPersonnelFilePerformanceEvaluationCommandValidator : AbstractValidator<AddPersonnelFilePerformanceEvaluationCommand>
{
    public AddPersonnelFilePerformanceEvaluationCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PerformanceEvaluationInputValidator());
    }
}

internal sealed class UpdatePersonnelFilePerformanceEvaluationCommandValidator : AbstractValidator<UpdatePersonnelFilePerformanceEvaluationCommand>
{
    public UpdatePersonnelFilePerformanceEvaluationCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EvaluationPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PerformanceEvaluationInputValidator());
    }
}

internal sealed class DeletePersonnelFilePerformanceEvaluationCommandValidator : AbstractValidator<DeletePersonnelFilePerformanceEvaluationCommand>
{
    public DeletePersonnelFilePerformanceEvaluationCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EvaluationPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFilePerformanceEvaluationCommandValidator : AbstractValidator<PatchPersonnelFilePerformanceEvaluationCommand>
{
    public PatchPersonnelFilePerformanceEvaluationCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EvaluationPublicId).NotEmpty();
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

internal sealed class GetPersonnelFilePerformanceEvaluationByIdQueryValidator : AbstractValidator<GetPersonnelFilePerformanceEvaluationByIdQuery>
{
    public GetPersonnelFilePerformanceEvaluationByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.EvaluationPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFilePerformanceEvaluationPatchState
{
    public string EvaluatorName { get; set; } = string.Empty;
    public DateTime EvaluationDateUtc { get; set; }
    public decimal? Score { get; set; }
    public string? QualitativeScoreCode { get; set; }
    public string? Comment { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFilePerformanceEvaluationPatchState From(PersonnelFilePerformanceEvaluationResponse response) =>
        new()
        {
            EvaluatorName = response.EvaluatorName,
            EvaluationDateUtc = response.EvaluationDateUtc,
            Score = response.Score,
            QualitativeScoreCode = response.QualitativeScoreCode,
            Comment = response.Comment,
            SourceSystem = response.SourceSystem,
            SourceReference = response.SourceReference,
            SourceSyncedUtc = response.SourceSyncedUtc
        };

    public PerformanceEvaluationInput ToInput() =>
        new(
            EvaluatorName,
            EvaluationDateUtc,
            Score,
            QualitativeScoreCode,
            Comment,
            SourceSystem,
            SourceReference,
            SourceSyncedUtc);
}

