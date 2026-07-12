using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Features.PersonnelFiles.Absences;

internal static class NotWorkedTimeTypeMapping
{
    public static NotWorkedTimeTypeResponse ToResponse(NotWorkedTimeType entity) =>
        new(
            entity.PublicId,
            entity.Code,
            entity.Name,
            entity.AppliesToPermission,
            entity.UsesWorkSchedule,
            entity.CountsHoliday,
            entity.CountsSaturday,
            entity.CountsRestDay,
            entity.CountsSeventhDayPenalty,
            entity.DiscountPercent,
            entity.DeductionConceptTypeCode,
            entity.IncomeConceptTypeCode,
            entity.IsActive,
            entity.ConcurrencyToken);
}

internal sealed class GetNotWorkedTimeTypesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    INotWorkedTimeTypeRepository repository)
    : IQueryHandler<GetNotWorkedTimeTypesQuery, IReadOnlyCollection<NotWorkedTimeTypeResponse>>
{
    public async Task<Result<IReadOnlyCollection<NotWorkedTimeTypeResponse>>> Handle(
        GetNotWorkedTimeTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewNotWorkedTimesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<NotWorkedTimeTypeResponse>>.Failure(authorizationResult.Error);
        }

        var items = await repository.GetAsync(query.CompanyId, query.IsActive, cancellationToken);
        return Result<IReadOnlyCollection<NotWorkedTimeTypeResponse>>.Success(
            items.Select(NotWorkedTimeTypeMapping.ToResponse).ToArray());
    }
}

internal sealed class GetNotWorkedTimeTypeByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    INotWorkedTimeTypeRepository repository)
    : IQueryHandler<GetNotWorkedTimeTypeByIdQuery, NotWorkedTimeTypeResponse>
{
    public async Task<Result<NotWorkedTimeTypeResponse>> Handle(
        GetNotWorkedTimeTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewNotWorkedTimesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var entity = await repository.GetEntityAsync(query.CompanyId, query.NotWorkedTimeTypePublicId, cancellationToken);
        return entity is null
            ? Result<NotWorkedTimeTypeResponse>.Failure(NotWorkedTimeTypeErrors.NotFound)
            : Result<NotWorkedTimeTypeResponse>.Success(NotWorkedTimeTypeMapping.ToResponse(entity));
    }
}

internal sealed class AddNotWorkedTimeTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    INotWorkedTimeTypeRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddNotWorkedTimeTypeCommand, NotWorkedTimeTypeResponse>
{
    public async Task<Result<NotWorkedTimeTypeResponse>> Handle(
        AddNotWorkedTimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageNotWorkedTimeTypesAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(authorizationResult.Error);
        }

        // The domain throws when a discounting type carries no deduction concept; catching it here turns a 500 into
        // the 422 the caller can act on.
        if (command.Item.DiscountPercent > 0m && string.IsNullOrWhiteSpace(command.Item.DeductionConceptTypeCode))
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(NotWorkedTimeTypeErrors.DeductionConceptRequired);
        }

        var normalizedCode = command.Item.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(command.CompanyId, normalizedCode, excludingPublicId: null, cancellationToken))
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(NotWorkedTimeTypeErrors.CodeDuplicated);
        }

        var entity = NotWorkedTimeType.Create(
            command.Item.Code,
            command.Item.Name,
            command.Item.AppliesToPermission,
            command.Item.UsesWorkSchedule,
            command.Item.CountsHoliday,
            command.Item.CountsSaturday,
            command.Item.CountsRestDay,
            command.Item.CountsSeventhDayPenalty,
            command.Item.DiscountPercent,
            command.Item.DeductionConceptTypeCode,
            command.Item.IncomeConceptTypeCode);
        entity.SetTenantId(command.CompanyId);

        repository.Add(entity);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<NotWorkedTimeTypeResponse>.Success(NotWorkedTimeTypeMapping.ToResponse(entity));
    }
}

internal sealed class UpdateNotWorkedTimeTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    INotWorkedTimeTypeRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateNotWorkedTimeTypeCommand, NotWorkedTimeTypeResponse>
{
    public async Task<Result<NotWorkedTimeTypeResponse>> Handle(
        UpdateNotWorkedTimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageNotWorkedTimeTypesAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(authorizationResult.Error);
        }

        if (command.Item.DiscountPercent > 0m && string.IsNullOrWhiteSpace(command.Item.DeductionConceptTypeCode))
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(NotWorkedTimeTypeErrors.DeductionConceptRequired);
        }

        var entity = await repository.GetEntityAsync(command.CompanyId, command.NotWorkedTimeTypePublicId, cancellationToken);
        if (entity is null)
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(NotWorkedTimeTypeErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Item.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(command.CompanyId, normalizedCode, entity.PublicId, cancellationToken))
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(NotWorkedTimeTypeErrors.CodeDuplicated);
        }

        entity.Update(
            command.Item.Code,
            command.Item.Name,
            command.Item.AppliesToPermission,
            command.Item.UsesWorkSchedule,
            command.Item.CountsHoliday,
            command.Item.CountsSaturday,
            command.Item.CountsRestDay,
            command.Item.CountsSeventhDayPenalty,
            command.Item.DiscountPercent,
            command.Item.DeductionConceptTypeCode,
            command.Item.IncomeConceptTypeCode);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<NotWorkedTimeTypeResponse>.Success(NotWorkedTimeTypeMapping.ToResponse(entity));
    }
}

internal sealed class SetNotWorkedTimeTypeActivationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    INotWorkedTimeTypeRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetNotWorkedTimeTypeActivationCommand, NotWorkedTimeTypeResponse>
{
    public async Task<Result<NotWorkedTimeTypeResponse>> Handle(
        SetNotWorkedTimeTypeActivationCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageNotWorkedTimeTypesAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var entity = await repository.GetEntityAsync(command.CompanyId, command.NotWorkedTimeTypePublicId, cancellationToken);
        if (entity is null)
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(NotWorkedTimeTypeErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<NotWorkedTimeTypeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Logical removal only (molde CostCenter — there is no DELETE): a type already stamped on a record must stay
        // readable, or the historical registro would show a dangling code.
        if (command.IsActive)
        {
            entity.Activate();
        }
        else
        {
            entity.Inactivate();
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<NotWorkedTimeTypeResponse>.Success(NotWorkedTimeTypeMapping.ToResponse(entity));
    }
}

internal sealed class LoadNotWorkedTimeTemplateCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    INotWorkedTimeTemplateSeeder seeder)
    : ICommandHandler<LoadNotWorkedTimeTemplateCommand, NotWorkedTimeTemplateResultResponse>
{
    public async Task<Result<NotWorkedTimeTemplateResultResponse>> Handle(
        LoadNotWorkedTimeTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageNotWorkedTimeTypesAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<NotWorkedTimeTemplateResultResponse>.Failure(authorizationResult.Error);
        }

        var result = await seeder.ApplyTemplateAsync(command.CompanyId, cancellationToken);
        return Result<NotWorkedTimeTemplateResultResponse>.Success(
            new NotWorkedTimeTemplateResultResponse(result.TypesCreated, result.TypesSkipped));
    }
}
