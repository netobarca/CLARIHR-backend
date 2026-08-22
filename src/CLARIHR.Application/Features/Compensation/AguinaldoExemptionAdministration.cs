using CLARIHR.Application.Abstractions.Compensation;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Compensation;
using FluentValidation;

namespace CLARIHR.Application.Features.Compensation;

public sealed record AguinaldoExemptionResponse(
    Guid Id,
    int Year,
    decimal ExemptAmount,
    bool IsActive);

public sealed record AguinaldoExemptionInput(
    int Year,
    decimal ExemptAmount,
    bool IsActive);

public sealed record GetAguinaldoExemptionsQuery(int? Year)
    : IQuery<IReadOnlyCollection<AguinaldoExemptionResponse>>;

public sealed record ReplaceAguinaldoExemptionsCommand(
    IReadOnlyCollection<AguinaldoExemptionInput> Exemptions)
    : ICommand<IReadOnlyCollection<AguinaldoExemptionResponse>>;

internal sealed class GetAguinaldoExemptionsQueryValidator : AbstractValidator<GetAguinaldoExemptionsQuery>
{
    public GetAguinaldoExemptionsQueryValidator()
    {
        RuleFor(query => query.Year!.Value).InclusiveBetween(2000, 2200).When(query => query.Year.HasValue);
    }
}

internal sealed class ReplaceAguinaldoExemptionsCommandValidator : AbstractValidator<ReplaceAguinaldoExemptionsCommand>
{
    public ReplaceAguinaldoExemptionsCommandValidator()
    {
        RuleForEach(command => command.Exemptions).ChildRules(exemption =>
        {
            exemption.RuleFor(item => item.Year).InclusiveBetween(2000, 2200);
            exemption.RuleFor(item => item.ExemptAmount).GreaterThanOrEqualTo(0);
        });

        // Dos filas para el mismo año serían dos verdades legales y la corrida elegiría una en silencio. El
        // índice único lo impide en la base; acá se detecta antes, para responder 422 con el año culpable en
        // vez de un 500 de Postgres.
        RuleFor(command => command.Exemptions)
            .Must(exemptions => exemptions.Select(item => item.Year).Distinct().Count() == exemptions.Count)
            .WithMessage("Aguinaldo exemptions must have at most one row per year.")
            .WithErrorCode("AGUINALDO_EXEMPTION_DUPLICATE_YEAR");
    }
}

internal sealed class GetAguinaldoExemptionsQueryHandler(
    IAguinaldoExemptionRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetAguinaldoExemptionsQuery, IReadOnlyCollection<AguinaldoExemptionResponse>>
{
    public async Task<Result<IReadOnlyCollection<AguinaldoExemptionResponse>>> Handle(
        GetAguinaldoExemptionsQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result<IReadOnlyCollection<AguinaldoExemptionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var items = await repository.GetExemptionsAsync(tenantId, query.Year, cancellationToken);
        return Result<IReadOnlyCollection<AguinaldoExemptionResponse>>.Success(items);
    }
}

internal sealed class ReplaceAguinaldoExemptionsCommandHandler(
    IAguinaldoExemptionRepository repository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReplaceAguinaldoExemptionsCommand, IReadOnlyCollection<AguinaldoExemptionResponse>>
{
    public async Task<Result<IReadOnlyCollection<AguinaldoExemptionResponse>>> Handle(
        ReplaceAguinaldoExemptionsCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result<IReadOnlyCollection<AguinaldoExemptionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var entities = command.Exemptions
            .Select(exemption =>
            {
                var entity = AguinaldoExemption.Create(exemption.Year, exemption.ExemptAmount, exemption.IsActive);
                entity.SetTenantId(tenantId);
                return entity;
            })
            .ToArray();

        await repository.ReplaceExemptionsAsync(tenantId, entities, cancellationToken);

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

        var items = await repository.GetExemptionsAsync(tenantId, year: null, cancellationToken);
        return Result<IReadOnlyCollection<AguinaldoExemptionResponse>>.Success(items);
    }
}
