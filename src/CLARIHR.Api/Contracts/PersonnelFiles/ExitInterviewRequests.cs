using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Api.Contracts.PersonnelFiles;

public sealed record CreateExitInterviewFormRequest(
    string Name,
    string? Description,
    bool IsAnonymous);

public sealed record SaveExitInterviewFormDefinitionRequest(
    string Name,
    string? Description,
    bool IsAnonymous,
    IReadOnlyCollection<ExitInterviewFormGroupInput> Groups,
    IReadOnlyCollection<ExitInterviewFormFieldInput> Fields);

public sealed record AssignExitInterviewFormReasonRequest(string ReasonCode);

public sealed record SaveExitInterviewSubmissionRequest(
    IReadOnlyCollection<ExitInterviewAnswerInput> Answers,
    bool Submit,
    Guid? ConcurrencyToken = null);
