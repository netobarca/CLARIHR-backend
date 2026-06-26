using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>The exit-interview form applicable to a retirement reason, or an explicit "no form" (RF-010).</summary>
public sealed record ExitInterviewApplicableFormResponse(bool HasForm, ExitInterviewFormResponse? Form);

public sealed record PublishExitInterviewFormCommand(Guid FormId, Guid ConcurrencyToken)
    : ICommand<ExitInterviewFormResponse>;

public sealed record ReopenExitInterviewFormCommand(Guid FormId, Guid ConcurrencyToken)
    : ICommand<ExitInterviewFormResponse>;

public sealed record ArchiveExitInterviewFormCommand(Guid FormId, Guid ConcurrencyToken)
    : ICommand<ExitInterviewFormResponse>;

public sealed record AssignExitInterviewFormReasonCommand(Guid FormId, Guid ConcurrencyToken, string ReasonCode)
    : ICommand<ExitInterviewFormResponse>;

public sealed record ResolveApplicableExitInterviewFormQuery(string ReasonCode)
    : IQuery<ExitInterviewApplicableFormResponse>;

internal sealed class PublishExitInterviewFormCommandValidator : AbstractValidator<PublishExitInterviewFormCommand>
{
    public PublishExitInterviewFormCommandValidator()
    {
        RuleFor(command => command.FormId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ReopenExitInterviewFormCommandValidator : AbstractValidator<ReopenExitInterviewFormCommand>
{
    public ReopenExitInterviewFormCommandValidator()
    {
        RuleFor(command => command.FormId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ArchiveExitInterviewFormCommandValidator : AbstractValidator<ArchiveExitInterviewFormCommand>
{
    public ArchiveExitInterviewFormCommandValidator()
    {
        RuleFor(command => command.FormId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AssignExitInterviewFormReasonCommandValidator : AbstractValidator<AssignExitInterviewFormReasonCommand>
{
    public AssignExitInterviewFormReasonCommandValidator()
    {
        RuleFor(command => command.FormId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.ReasonCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class ResolveApplicableExitInterviewFormQueryValidator : AbstractValidator<ResolveApplicableExitInterviewFormQuery>
{
    public ResolveApplicableExitInterviewFormQueryValidator()
    {
        RuleFor(query => query.ReasonCode).NotEmpty().MaximumLength(80);
    }
}
