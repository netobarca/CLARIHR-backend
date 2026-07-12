using CLARIHR.Application.Abstractions.Compensation;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Compensation;
using FluentValidation;

namespace CLARIHR.Application.Features.Compensation;

// ── Contracts ─────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One per-type indebtedness ceiling of the company (REQ-010 D-16).</summary>
public sealed record IndebtednessLimitResponse(
    Guid IndebtednessLimitPublicId,
    string RecurringDeductionTypeCode,
    decimal MaxPercent,
    bool IsActive)
{
    public Guid Id => IndebtednessLimitPublicId;
}

public sealed record IndebtednessLimitInput(
    string RecurringDeductionTypeCode,
    decimal MaxPercent);

public sealed record GetIndebtednessLimitsQuery(Guid CompanyId)
    : IQuery<IReadOnlyCollection<IndebtednessLimitResponse>>;

/// <summary>Replace-all of the company's per-type ceilings (the set is edited as a whole, like the tax brackets).</summary>
public sealed record ReplaceIndebtednessLimitsCommand(
    Guid CompanyId,
    IReadOnlyCollection<IndebtednessLimitInput> Limits)
    : ICommand<IReadOnlyCollection<IndebtednessLimitResponse>>;

// ── Errors ────────────────────────────────────────────────────────────────────────────────────────────

public static class IndebtednessParameterErrors
{
    /// <summary>A ceiling was configured for a deduction type that does not exist (or is inactive) in the catalog.
    /// Without this check a company could silently configure limits over phantom types that never apply.</summary>
    public static readonly Error LimitTypeInvalid = new(
        "INDEBTEDNESS_LIMIT_TYPE_INVALID",
        "The indebtedness limit references a recurring-deduction type that does not exist or is not active.",
        ErrorType.UnprocessableEntity);

    /// <summary>The same deduction type appears twice in the replace-all payload.</summary>
    public static readonly Error LimitTypeDuplicated = new(
        "INDEBTEDNESS_LIMIT_TYPE_DUPLICATED",
        "The same recurring-deduction type cannot carry two indebtedness limits.",
        ErrorType.UnprocessableEntity);
}

// ── Validators ────────────────────────────────────────────────────────────────────────────────────────

internal sealed class GetIndebtednessLimitsQueryValidator : AbstractValidator<GetIndebtednessLimitsQuery>
{
    public GetIndebtednessLimitsQueryValidator() => RuleFor(query => query.CompanyId).NotEmpty();
}

internal sealed class ReplaceIndebtednessLimitsCommandValidator : AbstractValidator<ReplaceIndebtednessLimitsCommand>
{
    public ReplaceIndebtednessLimitsCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Limits).NotNull();
        RuleForEach(command => command.Limits).ChildRules(limit =>
        {
            limit.RuleFor(item => item.RecurringDeductionTypeCode)
                .NotEmpty()
                .MaximumLength(IndebtednessLimit.MaxTypeCodeLength);
            // The domain guard is (0, 100]; mirror it here so the caller gets a 400 with the field, not a 500.
            limit.RuleFor(item => item.MaxPercent).GreaterThan(0m).LessThanOrEqualTo(100m);
        });
    }
}

// ── Handlers ──────────────────────────────────────────────────────────────────────────────────────────

internal sealed class GetIndebtednessLimitsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IIndebtednessRepository repository)
    : IQueryHandler<GetIndebtednessLimitsQuery, IReadOnlyCollection<IndebtednessLimitResponse>>
{
    public async Task<Result<IReadOnlyCollection<IndebtednessLimitResponse>>> Handle(
        GetIndebtednessLimitsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewIndebtednessAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<IndebtednessLimitResponse>>.Failure(authorizationResult.Error);
        }

        var items = await repository.GetLimitsAsync(query.CompanyId, cancellationToken);
        return Result<IReadOnlyCollection<IndebtednessLimitResponse>>.Success(items);
    }
}

internal sealed class ReplaceIndebtednessLimitsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IIndebtednessRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReplaceIndebtednessLimitsCommand, IReadOnlyCollection<IndebtednessLimitResponse>>
{
    public async Task<Result<IReadOnlyCollection<IndebtednessLimitResponse>>> Handle(
        ReplaceIndebtednessLimitsCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageIndebtednessParametersAsync(
            command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<IndebtednessLimitResponse>>.Failure(authorizationResult.Error);
        }

        var normalized = command.Limits
            .Select(limit => limit with { RecurringDeductionTypeCode = limit.RecurringDeductionTypeCode.Trim().ToUpperInvariant() })
            .ToArray();

        // The filtered-unique index would catch this as a 500; catching it here makes it a 422 the caller can act on.
        if (normalized.Select(limit => limit.RecurringDeductionTypeCode).Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            return Result<IReadOnlyCollection<IndebtednessLimitResponse>>.Failure(
                IndebtednessParameterErrors.LimitTypeDuplicated);
        }

        // Every ceiling must point at a live deduction type (the REQ-008 catalog), or it would never apply.
        foreach (var limit in normalized)
        {
            if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                    command.CompanyId,
                    PersonnelCurriculumCatalogCategories.RecurringDeductionType,
                    limit.RecurringDeductionTypeCode,
                    cancellationToken))
            {
                return Result<IReadOnlyCollection<IndebtednessLimitResponse>>.Failure(
                    IndebtednessParameterErrors.LimitTypeInvalid);
            }
        }

        var entities = normalized
            .Select(limit =>
            {
                var entity = IndebtednessLimit.Create(limit.RecurringDeductionTypeCode, limit.MaxPercent, isActive: true);
                entity.SetTenantId(command.CompanyId);
                return entity;
            })
            .ToArray();

        await repository.ReplaceLimitsAsync(command.CompanyId, entities, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var items = await repository.GetLimitsAsync(command.CompanyId, cancellationToken);
        return Result<IReadOnlyCollection<IndebtednessLimitResponse>>.Success(items);
    }
}
