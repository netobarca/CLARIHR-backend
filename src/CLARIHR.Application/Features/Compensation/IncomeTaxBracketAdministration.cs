using CLARIHR.Application.Abstractions.Compensation;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Compensation;
using FluentValidation;

namespace CLARIHR.Application.Features.Compensation;

public sealed record IncomeTaxBracketResponse(
    Guid Id,
    string PayPeriodCode,
    int BracketOrder,
    decimal LowerBound,
    decimal? UpperBound,
    decimal FixedFee,
    decimal RatePercent,
    decimal ExcessOver,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive);

public sealed record IncomeTaxBracketInput(
    int BracketOrder,
    decimal LowerBound,
    decimal? UpperBound,
    decimal FixedFee,
    decimal RatePercent,
    decimal ExcessOver,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive);

public sealed record GetIncomeTaxBracketsQuery(string? PayPeriodCode)
    : IQuery<IReadOnlyCollection<IncomeTaxBracketResponse>>;

public sealed record ReplaceIncomeTaxBracketsCommand(
    string PayPeriodCode,
    IReadOnlyCollection<IncomeTaxBracketInput> Brackets)
    : ICommand<IReadOnlyCollection<IncomeTaxBracketResponse>>;

internal sealed class GetIncomeTaxBracketsQueryValidator : AbstractValidator<GetIncomeTaxBracketsQuery>
{
    public GetIncomeTaxBracketsQueryValidator()
    {
        RuleFor(query => query.PayPeriodCode).MaximumLength(40).When(query => !string.IsNullOrWhiteSpace(query.PayPeriodCode));
    }
}

internal sealed class ReplaceIncomeTaxBracketsCommandValidator : AbstractValidator<ReplaceIncomeTaxBracketsCommand>
{
    public ReplaceIncomeTaxBracketsCommandValidator()
    {
        RuleFor(command => command.PayPeriodCode).NotEmpty().MaximumLength(40);
        RuleForEach(command => command.Brackets).ChildRules(bracket =>
        {
            bracket.RuleFor(item => item.LowerBound).GreaterThanOrEqualTo(0);
            bracket.RuleFor(item => item.FixedFee).GreaterThanOrEqualTo(0);
            bracket.RuleFor(item => item.RatePercent).InclusiveBetween(0, 100);
            bracket.RuleFor(item => item.ExcessOver).GreaterThanOrEqualTo(0);
        });
    }
}

internal sealed class GetIncomeTaxBracketsQueryHandler(
    IIncomeTaxBracketRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetIncomeTaxBracketsQuery, IReadOnlyCollection<IncomeTaxBracketResponse>>
{
    public async Task<Result<IReadOnlyCollection<IncomeTaxBracketResponse>>> Handle(
        GetIncomeTaxBracketsQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result<IReadOnlyCollection<IncomeTaxBracketResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var items = await repository.GetBracketsAsync(tenantId, query.PayPeriodCode, cancellationToken);
        return Result<IReadOnlyCollection<IncomeTaxBracketResponse>>.Success(items);
    }
}

internal sealed class ReplaceIncomeTaxBracketsCommandHandler(
    IIncomeTaxBracketRepository repository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReplaceIncomeTaxBracketsCommand, IReadOnlyCollection<IncomeTaxBracketResponse>>
{
    public async Task<Result<IReadOnlyCollection<IncomeTaxBracketResponse>>> Handle(
        ReplaceIncomeTaxBracketsCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result<IReadOnlyCollection<IncomeTaxBracketResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var entities = command.Brackets
            .Select(bracket =>
            {
                var entity = IncomeTaxWithholdingBracket.Create(
                    command.PayPeriodCode,
                    bracket.BracketOrder,
                    bracket.LowerBound,
                    bracket.UpperBound,
                    bracket.FixedFee,
                    bracket.RatePercent,
                    bracket.ExcessOver,
                    bracket.EffectiveFromUtc,
                    bracket.EffectiveToUtc,
                    bracket.IsActive);
                entity.SetTenantId(tenantId);
                return entity;
            })
            .ToArray();

        await repository.ReplaceBracketsForPeriodAsync(tenantId, command.PayPeriodCode, entities, cancellationToken);

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

        var items = await repository.GetBracketsAsync(tenantId, command.PayPeriodCode, cancellationToken);
        return Result<IReadOnlyCollection<IncomeTaxBracketResponse>>.Success(items);
    }
}
