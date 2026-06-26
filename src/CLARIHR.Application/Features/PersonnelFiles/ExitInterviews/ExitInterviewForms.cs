using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ---- Responses (full renderable definition) ----

public sealed record ExitInterviewFormFieldOptionResponse(
    Guid Id,
    string OptionCode,
    string Label,
    decimal? Score,
    int DisplayOrder,
    bool IsActive);

public sealed record ExitInterviewFormFieldResponse(
    Guid Id,
    Guid? GroupId,
    string ControlTypeCode,
    string FieldKey,
    string Title,
    string? Description,
    decimal Weight,
    bool IsRequired,
    int DisplayOrder,
    decimal? MinValue,
    decimal? MaxValue,
    int? MaxLength,
    int? ScaleMax,
    bool IsActive,
    IReadOnlyCollection<ExitInterviewFormFieldOptionResponse> Options);

public sealed record ExitInterviewFormGroupResponse(
    Guid Id,
    string Title,
    string? Description,
    int DisplayOrder);

public sealed record ExitInterviewFormResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsAnonymous,
    string Status,
    int Version,
    string? RetirementReasonCode,
    bool IsActiveForReason,
    bool IsActive,
    Guid ConcurrencyToken,
    IReadOnlyCollection<ExitInterviewFormGroupResponse> Groups,
    IReadOnlyCollection<ExitInterviewFormFieldResponse> Fields);

public sealed record ExitInterviewFormListItemResponse(
    Guid Id,
    string Name,
    string Status,
    int Version,
    string? RetirementReasonCode,
    bool IsActiveForReason,
    bool IsActive,
    int SubmissionCount,
    Guid ConcurrencyToken);

// ---- Inputs (nested definition save) ----

public sealed record ExitInterviewFormGroupInput(
    string GroupKey,
    string Title,
    string? Description,
    int DisplayOrder);

public sealed record ExitInterviewFormFieldOptionInput(
    string OptionCode,
    string Label,
    decimal? Score,
    int DisplayOrder);

public sealed record ExitInterviewFormFieldInput(
    string? GroupKey,
    string ControlTypeCode,
    string FieldKey,
    string Title,
    string? Description,
    decimal Weight,
    bool IsRequired,
    int DisplayOrder,
    decimal? MinValue,
    decimal? MaxValue,
    int? MaxLength,
    int? ScaleMax,
    IReadOnlyCollection<ExitInterviewFormFieldOptionInput> Options);

// ---- Commands / Queries ----

public sealed record CreateExitInterviewFormCommand(
    string Name,
    string? Description,
    bool IsAnonymous)
    : ICommand<ExitInterviewFormResponse>;

public sealed record SaveExitInterviewFormDefinitionCommand(
    Guid FormId,
    Guid ConcurrencyToken,
    string Name,
    string? Description,
    bool IsAnonymous,
    IReadOnlyCollection<ExitInterviewFormGroupInput> Groups,
    IReadOnlyCollection<ExitInterviewFormFieldInput> Fields)
    : ICommand<ExitInterviewFormResponse>;

public sealed record DeleteExitInterviewFormCommand(Guid FormId, Guid ConcurrencyToken)
    : ICommand<ExitInterviewFormResponse>;

public sealed record GetExitInterviewFormByIdQuery(Guid FormId)
    : IQuery<ExitInterviewFormResponse>;

public sealed record ListExitInterviewFormsQuery(
    ExitInterviewFormStatus? Status = null,
    string? ReasonCode = null,
    string? Search = null)
    : IQuery<IReadOnlyCollection<ExitInterviewFormListItemResponse>>;

// ---- Validators ----

internal static class ExitInterviewFormValidation
{
    public const string FieldKeyPattern = "^[A-Za-z0-9_]+$";
    public const string CodePattern = "^[A-Za-z0-9_]+$";
}

internal sealed class ExitInterviewFormFieldOptionInputValidator : AbstractValidator<ExitInterviewFormFieldOptionInput>
{
    public ExitInterviewFormFieldOptionInputValidator()
    {
        RuleFor(option => option.OptionCode).NotEmpty().MaximumLength(80).Matches(ExitInterviewFormValidation.CodePattern);
        RuleFor(option => option.Label).NotEmpty().MaximumLength(300);
        RuleFor(option => option.Score)
            .InclusiveBetween(0m, 100m)
            .When(option => option.Score.HasValue);
        RuleFor(option => option.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class ExitInterviewFormGroupInputValidator : AbstractValidator<ExitInterviewFormGroupInput>
{
    public ExitInterviewFormGroupInputValidator()
    {
        RuleFor(group => group.GroupKey).NotEmpty().MaximumLength(100).Matches(ExitInterviewFormValidation.CodePattern);
        RuleFor(group => group.Title).NotEmpty().MaximumLength(200);
        RuleFor(group => group.Description).MaximumLength(1000);
        RuleFor(group => group.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class ExitInterviewFormFieldInputValidator : AbstractValidator<ExitInterviewFormFieldInput>
{
    public ExitInterviewFormFieldInputValidator()
    {
        RuleFor(field => field.GroupKey).MaximumLength(100).When(field => !string.IsNullOrWhiteSpace(field.GroupKey));
        RuleFor(field => field.ControlTypeCode).NotEmpty().MaximumLength(40);
        RuleFor(field => field.FieldKey).NotEmpty().MaximumLength(100).Matches(ExitInterviewFormValidation.FieldKeyPattern);
        RuleFor(field => field.Title).NotEmpty().MaximumLength(300);
        RuleFor(field => field.Description).MaximumLength(1000);
        RuleFor(field => field.Weight).GreaterThanOrEqualTo(0m);
        RuleFor(field => field.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(field => field.MaxLength).GreaterThan(0).When(field => field.MaxLength.HasValue);
        RuleFor(field => field.ScaleMax).GreaterThan(1).When(field => field.ScaleMax.HasValue);
        RuleForEach(field => field.Options).SetValidator(new ExitInterviewFormFieldOptionInputValidator());
    }
}

internal sealed class CreateExitInterviewFormCommandValidator : AbstractValidator<CreateExitInterviewFormCommand>
{
    public CreateExitInterviewFormCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MinimumLength(3).MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(1000);
    }
}

internal sealed class SaveExitInterviewFormDefinitionCommandValidator : AbstractValidator<SaveExitInterviewFormDefinitionCommand>
{
    public SaveExitInterviewFormDefinitionCommandValidator()
    {
        RuleFor(command => command.FormId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MinimumLength(3).MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.Groups).NotNull();
        RuleFor(command => command.Fields).NotNull();
        RuleForEach(command => command.Groups).SetValidator(new ExitInterviewFormGroupInputValidator());
        RuleForEach(command => command.Fields).SetValidator(new ExitInterviewFormFieldInputValidator());
    }
}

internal sealed class DeleteExitInterviewFormCommandValidator : AbstractValidator<DeleteExitInterviewFormCommand>
{
    public DeleteExitInterviewFormCommandValidator()
    {
        RuleFor(command => command.FormId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetExitInterviewFormByIdQueryValidator : AbstractValidator<GetExitInterviewFormByIdQuery>
{
    public GetExitInterviewFormByIdQueryValidator()
    {
        RuleFor(query => query.FormId).NotEmpty();
    }
}
