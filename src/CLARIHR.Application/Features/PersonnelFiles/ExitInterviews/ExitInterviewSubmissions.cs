using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ---- Responses ----

public sealed record ExitInterviewAnswerResponse(
    Guid Id,
    string FieldKey,
    string Title,
    string ControlTypeCode,
    string? ValueText,
    decimal? ValueNumber,
    DateTime? ValueDate,
    bool? ValueBool,
    IReadOnlyCollection<string> SelectedOptionCodes,
    decimal? NormalizedScore);

public sealed record ExitInterviewSubmissionResponse(
    Guid Id,
    Guid FormId,
    int FormVersion,
    bool IsAnonymous,
    Guid? PersonnelFileId,
    string Status,
    string RetirementReasonCode,
    string? RetirementCategoryCode,
    string? SeparationType,
    string Period,
    DateTime? SubmittedUtc,
    decimal? TotalScore,
    Guid ConcurrencyToken,
    IReadOnlyCollection<ExitInterviewAnswerResponse> Answers);

public sealed record ExitInterviewSubmissionListItemResponse(
    Guid Id,
    Guid FormId,
    bool IsAnonymous,
    Guid? PersonnelFileId,
    string Status,
    string RetirementReasonCode,
    string? RetirementCategoryCode,
    string? SeparationType,
    string Period,
    DateTime? SubmittedUtc,
    decimal? TotalScore);

/// <summary>What the employee/RRHH gets when opening an exit interview: the applicable form + current submission.</summary>
public sealed record ExitInterviewForFileResponse(
    bool HasForm,
    ExitInterviewFormResponse? Form,
    ExitInterviewSubmissionResponse? CurrentSubmission);

// ---- Inputs ----

public sealed record ExitInterviewAnswerInput(
    string FieldKey,
    string? ValueText,
    decimal? ValueNumber,
    DateTime? ValueDate,
    bool? ValueBool,
    IReadOnlyCollection<string>? SelectedOptionCodes);

// ---- Commands / Queries ----

public sealed record GetExitInterviewForFileQuery(Guid PersonnelFilePublicId)
    : IQuery<ExitInterviewForFileResponse>;

public sealed record SaveExitInterviewSubmissionCommand(
    Guid PersonnelFilePublicId,
    IReadOnlyCollection<ExitInterviewAnswerInput> Answers,
    bool Submit,
    Guid? ConcurrencyToken)
    : ICommand<ExitInterviewSubmissionResponse>;

public sealed record ListExitInterviewSubmissionsQuery(
    string? ReasonCode = null,
    string? Period = null)
    : IQuery<IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>>;

public sealed record GetExitInterviewSubmissionByIdQuery(Guid SubmissionId)
    : IQuery<ExitInterviewSubmissionResponse>;

// ---- Validators ----

internal sealed class ExitInterviewAnswerInputValidator : AbstractValidator<ExitInterviewAnswerInput>
{
    public ExitInterviewAnswerInputValidator()
    {
        RuleFor(answer => answer.FieldKey).NotEmpty().MaximumLength(100);
        RuleFor(answer => answer.ValueText).MaximumLength(4000);
    }
}

internal sealed class GetExitInterviewForFileQueryValidator : AbstractValidator<GetExitInterviewForFileQuery>
{
    public GetExitInterviewForFileQueryValidator()
    {
        RuleFor(query => query.PersonnelFilePublicId).NotEmpty();
    }
}

internal sealed class SaveExitInterviewSubmissionCommandValidator : AbstractValidator<SaveExitInterviewSubmissionCommand>
{
    public SaveExitInterviewSubmissionCommandValidator()
    {
        RuleFor(command => command.PersonnelFilePublicId).NotEmpty();
        RuleFor(command => command.Answers).NotNull();
        RuleForEach(command => command.Answers).SetValidator(new ExitInterviewAnswerInputValidator());
    }
}

internal sealed class GetExitInterviewSubmissionByIdQueryValidator : AbstractValidator<GetExitInterviewSubmissionByIdQuery>
{
    public GetExitInterviewSubmissionByIdQueryValidator()
    {
        RuleFor(query => query.SubmissionId).NotEmpty();
    }
}
