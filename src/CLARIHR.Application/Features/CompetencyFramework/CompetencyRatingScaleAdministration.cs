using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.CompetencyFramework;
using FluentValidation;

namespace CLARIHR.Application.Features.CompetencyFramework;

public sealed record CompetencyRatingScaleLevelResponse(
    Guid Id,
    string Code,
    string Label,
    decimal Value,
    int SortOrder);

public sealed record CompetencyRatingScaleResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    CompetencyRatingScaleType ScaleType,
    decimal? MinValue,
    decimal? MaxValue,
    int Decimals,
    bool IsActive,
    Guid ConcurrencyToken,
    IReadOnlyCollection<CompetencyRatingScaleLevelResponse> Levels,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record ActiveCompetencyRatingScaleResponse(
    bool IsConfigured,
    CompetencyRatingScaleResponse? Scale);

public sealed record CompetencyRatingScaleLevelInput(
    string Code,
    string Label,
    decimal Value,
    int SortOrder);

public sealed record GetActiveCompetencyRatingScaleQuery(Guid CompanyId)
    : IQuery<ActiveCompetencyRatingScaleResponse>;

/// <summary>
/// Sets (creates or redefines in place) the company's single active competency rating scale (decision D-04).
/// The scale is the source of truth for expected/achieved competency scores; redefining it in place avoids
/// ever having two active scales.
/// </summary>
public sealed record SetCompetencyRatingScaleCommand(
    Guid CompanyId,
    string Code,
    string Name,
    CompetencyRatingScaleType ScaleType,
    decimal? MinValue,
    decimal? MaxValue,
    int Decimals,
    IReadOnlyCollection<CompetencyRatingScaleLevelInput> Levels)
    : ICommand<CompetencyRatingScaleResponse>;

internal sealed class GetActiveCompetencyRatingScaleQueryValidator : AbstractValidator<GetActiveCompetencyRatingScaleQuery>
{
    public GetActiveCompetencyRatingScaleQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class SetCompetencyRatingScaleCommandValidator : AbstractValidator<SetCompetencyRatingScaleCommand>
{
    public SetCompetencyRatingScaleCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(CompetencyRatingScale.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(CompetencyRatingScale.MaxNameLength);
        RuleFor(command => command.ScaleType).IsInEnum();

        // Numeric scale: a comparable range with no discrete levels.
        When(command => command.ScaleType == CompetencyRatingScaleType.Numeric, () =>
        {
            RuleFor(command => command.MinValue).NotNull();
            RuleFor(command => command.MaxValue).NotNull();
            RuleFor(command => command)
                .Must(command => command.MinValue!.Value < command.MaxValue!.Value)
                .WithMessage("Numeric scale min value must be less than max value.")
                .When(command => command.MinValue.HasValue && command.MaxValue.HasValue);
            RuleFor(command => command.Decimals).GreaterThanOrEqualTo(0);
            RuleFor(command => command.Levels).Empty().WithMessage("Numeric scales must not define discrete levels.");
        });

        // Discrete scale: at least two ordered levels with distinct values.
        When(command => command.ScaleType == CompetencyRatingScaleType.Discrete, () =>
        {
            RuleFor(command => command.Levels)
                .NotNull()
                .Must(levels => levels is not null && levels.Count >= 2)
                .WithMessage("Discrete scales require at least two levels.")
                .Must(levels => levels is null || levels.Select(level => level.Value).Distinct().Count() == levels.Count)
                .WithMessage("Discrete scale levels must have distinct values.");
            RuleForEach(command => command.Levels).ChildRules(level =>
            {
                level.RuleFor(item => item.Code).NotEmpty().MaximumLength(CompetencyRatingScaleLevel.MaxCodeLength);
                level.RuleFor(item => item.Label).NotEmpty().MaximumLength(CompetencyRatingScaleLevel.MaxLabelLength);
                level.RuleFor(item => item.SortOrder).GreaterThanOrEqualTo(0);
            });
        });
    }
}

internal sealed class GetActiveCompetencyRatingScaleQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository)
    : IQueryHandler<GetActiveCompetencyRatingScaleQuery, ActiveCompetencyRatingScaleResponse>
{
    public async Task<Result<ActiveCompetencyRatingScaleResponse>> Handle(
        GetActiveCompetencyRatingScaleQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ActiveCompetencyRatingScaleResponse>.Failure(authorizationResult.Error);
        }

        var scale = await repository.GetActiveRatingScaleAsync(query.CompanyId, cancellationToken);
        return Result<ActiveCompetencyRatingScaleResponse>.Success(
            scale is null
                ? new ActiveCompetencyRatingScaleResponse(false, null)
                : new ActiveCompetencyRatingScaleResponse(true, CompetencyRatingScaleMapper.Map(scale)));
    }
}

internal sealed class SetCompetencyRatingScaleCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetCompetencyRatingScaleCommand, CompetencyRatingScaleResponse>
{
    public async Task<Result<CompetencyRatingScaleResponse>> Handle(
        SetCompetencyRatingScaleCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyRatingScaleResponse>.Failure(authorizationResult.Error);
        }

        var levels = command.ScaleType == CompetencyRatingScaleType.Discrete
            ? command.Levels
                .Select(level =>
                {
                    var entity = CompetencyRatingScaleLevel.Create(level.Code, level.Label, level.Value, level.SortOrder);
                    entity.SetTenantId(command.CompanyId);
                    return entity;
                })
                .ToList()
            : new List<CompetencyRatingScaleLevel>();

        var existing = await repository.GetActiveRatingScaleForUpdateAsync(command.CompanyId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (existing is not null)
            {
                existing.Update(command.Code, command.Name, command.ScaleType, command.MinValue, command.MaxValue, command.Decimals, levels);
            }
            else
            {
                var scale = command.ScaleType == CompetencyRatingScaleType.Numeric
                    ? CompetencyRatingScale.CreateNumeric(command.Code, command.Name, command.MinValue!.Value, command.MaxValue!.Value, command.Decimals)
                    : CompetencyRatingScale.CreateDiscrete(command.Code, command.Name, levels);
                scale.SetTenantId(command.CompanyId);
                foreach (var level in scale.Levels)
                {
                    level.SetTenantId(command.CompanyId);
                }

                repository.AddCompetencyRatingScale(scale);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var refreshed = await repository.GetActiveRatingScaleAsync(command.CompanyId, cancellationToken)
                ?? throw new InvalidOperationException("Competency rating scale could not be resolved after set.");
            var response = CompetencyRatingScaleMapper.Map(refreshed);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyRatingScaleUpdated,
                    AuditEntityTypes.CompetencyRatingScale,
                    response.Id,
                    response.Code,
                    AuditActions.Update,
                    $"Set competency rating scale {response.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyRatingScaleResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CompetencyRatingScaleMapper
{
    public static CompetencyRatingScaleResponse Map(CompetencyRatingScale scale) =>
        new(
            scale.PublicId,
            scale.TenantId,
            scale.Code,
            scale.Name,
            scale.ScaleType,
            scale.MinValue,
            scale.MaxValue,
            scale.Decimals,
            scale.IsActive,
            scale.ConcurrencyToken,
            scale.Levels
                .OrderBy(level => level.SortOrder)
                .Select(level => new CompetencyRatingScaleLevelResponse(level.PublicId, level.Code, level.Label, level.Value, level.SortOrder))
                .ToArray());
}
