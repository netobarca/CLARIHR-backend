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

public sealed record PersonnelFileCurricularCompetencyResponse(
    Guid CurricularCompetencyPublicId,
    string RequirementTypeCode,
    string RequirementName,
    string CompetencyDomain,
    decimal? ExperienceTimeValue,
    string? MetricCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => CurricularCompetencyPublicId;
}

public sealed record CurricularCompetencyInput(
    string RequirementTypeCode,
    string RequirementName,
    string CompetencyDomain,
    decimal? ExperienceTimeValue,
    string? MetricCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFileCurricularCompetencyCommand(
    Guid PersonnelFileId,
    CurricularCompetencyInput Item)
    : ICommand<PersonnelFileCurricularCompetencyResponse>;

public sealed record UpdatePersonnelFileCurricularCompetencyCommand(
    Guid PersonnelFileId,
    Guid CurricularCompetencyPublicId,
    CurricularCompetencyInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCurricularCompetencyResponse>;

public sealed record DeletePersonnelFileCurricularCompetencyCommand(
    Guid PersonnelFileId,
    Guid CurricularCompetencyPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileCurricularCompetencyPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileCurricularCompetencyCommand(
    Guid PersonnelFileId,
    Guid CurricularCompetencyPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileCurricularCompetencyPatchOperation> Operations)
    : ICommand<PersonnelFileCurricularCompetencyResponse>;

public sealed record GetPersonnelFileCurricularCompetenciesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>;

public sealed record GetPersonnelFileCurricularCompetencyByIdQuery(Guid PersonnelFileId, Guid CurricularCompetencyPublicId)
    : IQuery<PersonnelFileCurricularCompetencyResponse>;

internal sealed class CurricularCompetencyInputValidator : AbstractValidator<CurricularCompetencyInput>
{
    public CurricularCompetencyInputValidator()
    {
        RuleFor(input => input.RequirementTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.RequirementName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.CompetencyDomain).NotEmpty().MaximumLength(120);
    }
}

internal sealed class AddPersonnelFileCurricularCompetencyCommandValidator : AbstractValidator<AddPersonnelFileCurricularCompetencyCommand>
{
    public AddPersonnelFileCurricularCompetencyCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new CurricularCompetencyInputValidator());
    }
}

internal sealed class UpdatePersonnelFileCurricularCompetencyCommandValidator : AbstractValidator<UpdatePersonnelFileCurricularCompetencyCommand>
{
    public UpdatePersonnelFileCurricularCompetencyCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CurricularCompetencyPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new CurricularCompetencyInputValidator());
    }
}

internal sealed class DeletePersonnelFileCurricularCompetencyCommandValidator : AbstractValidator<DeletePersonnelFileCurricularCompetencyCommand>
{
    public DeletePersonnelFileCurricularCompetencyCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CurricularCompetencyPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileCurricularCompetencyCommandValidator : AbstractValidator<PatchPersonnelFileCurricularCompetencyCommand>
{
    public PatchPersonnelFileCurricularCompetencyCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CurricularCompetencyPublicId).NotEmpty();
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

internal sealed class GetPersonnelFileCurricularCompetencyByIdQueryValidator : AbstractValidator<GetPersonnelFileCurricularCompetencyByIdQuery>
{
    public GetPersonnelFileCurricularCompetencyByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CurricularCompetencyPublicId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileCurricularCompetenciesQueryValidator : AbstractValidator<GetPersonnelFileCurricularCompetenciesQuery>
{
    public GetPersonnelFileCurricularCompetenciesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class PersonnelFileCurricularCompetencyPatchState
{
    public string RequirementTypeCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public string CompetencyDomain { get; set; } = string.Empty;
    public decimal? ExperienceTimeValue { get; set; }
    public string? MetricCode { get; set; }
    public string? Notes { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileCurricularCompetencyPatchState From(PersonnelFileCurricularCompetencyResponse response) =>
        new()
        {
            RequirementTypeCode = response.RequirementTypeCode,
            RequirementName = response.RequirementName,
            CompetencyDomain = response.CompetencyDomain,
            ExperienceTimeValue = response.ExperienceTimeValue,
            MetricCode = response.MetricCode,
            Notes = response.Notes,
            SourceSystem = response.SourceSystem,
            SourceReference = response.SourceReference,
            SourceSyncedUtc = response.SourceSyncedUtc
        };

    public CurricularCompetencyInput ToInput() =>
        new(
            RequirementTypeCode,
            RequirementName,
            CompetencyDomain,
            ExperienceTimeValue,
            MetricCode,
            Notes,
            SourceSystem,
            SourceReference,
            SourceSyncedUtc);
}

