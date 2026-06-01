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

public sealed record PersonnelFilePersonnelActionResponse(
    Guid PersonnelActionPublicId,
    string ActionTypeCode,
    string ActionStatusCode,
    DateTime ActionDateUtc,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Description,
    string? Reference,
    decimal? Amount,
    string? CurrencyCode,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => PersonnelActionPublicId;
}

public sealed record PersonnelFilePersonnelActionExportRow(
    Guid Id,
    string ActionTypeCode,
    string ActionStatusCode,
    DateTime ActionDateUtc,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Description,
    string? Reference,
    decimal? Amount,
    string? CurrencyCode,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record AddPersonnelFilePersonnelActionCommand(
    Guid PersonnelFileId,
    string ActionTypeCode,
    string ActionStatusCode,
    DateTime ActionDateUtc,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Description,
    string? Reference,
    decimal? Amount,
    string? CurrencyCode)
    : ICommand<PersonnelFilePersonnelActionResponse>;

public sealed record GetPersonnelFilePersonnelActionByIdQuery(Guid PersonnelFileId, Guid PersonnelActionPublicId)
    : IQuery<PersonnelFilePersonnelActionResponse>;

public sealed record SearchPersonnelFilePersonnelActionsQuery(
    Guid PersonnelFileId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Type,
    string? Status,
    string? Search,
    string? SortBy = null,
    PersonnelFileSortDirection SortDirection = PersonnelFileSortDirection.Desc,
    int PageNumber = 1,
    int PageSize = PersonnelFileValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PersonnelFilePersonnelActionResponse>>;

public sealed record ExportPersonnelFilePersonnelActionsQuery(
    Guid PersonnelFileId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Type,
    string? Status,
    string? Search,
    string? SortBy = null,
    PersonnelFileSortDirection SortDirection = PersonnelFileSortDirection.Desc,
    int? MaxRows = null)
    : IQuery<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>;

internal sealed class PersonnelActionInputValidator : AbstractValidator<AddPersonnelFilePersonnelActionCommand>
{
    public PersonnelActionInputValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ActionTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ActionStatusCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ActionDateUtc).LessThanOrEqualTo(command => command.EffectiveToUtc!.Value).When(command => command.EffectiveToUtc.HasValue);
    }
}

internal sealed class GetPersonnelFilePersonnelActionByIdQueryValidator : AbstractValidator<GetPersonnelFilePersonnelActionByIdQuery>
{
    public GetPersonnelFilePersonnelActionByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.PersonnelActionPublicId).NotEmpty();
    }
}

internal sealed class SearchPersonnelFilePersonnelActionsQueryValidator : AbstractValidator<SearchPersonnelFilePersonnelActionsQuery>
{
    public SearchPersonnelFilePersonnelActionsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.Search)
            .MaximumLength(PersonnelFileValidationRules.MaxSearchLength)
            .Must(PersonnelFileValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {PersonnelFileValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.SortBy).MaximumLength(80);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PersonnelFileValidationRules.MaxPageSize);
    }
}

internal sealed class AddPersonnelFilePersonnelActionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFilePersonnelActionCommand, PersonnelFilePersonnelActionResponse>
{
    public async Task<Result<PersonnelFilePersonnelActionResponse>> Handle(
        AddPersonnelFilePersonnelActionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFilePersonnelActionResponse>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFilePersonnelActionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFilePersonnelAction.Create(
            command.ActionTypeCode,
            command.ActionStatusCode,
            command.ActionDateUtc,
            command.EffectiveFromUtc,
            command.EffectiveToUtc,
            command.Description,
            command.Reference,
            command.Amount,
            command.CurrencyCode,
            isSystemGenerated: false);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddPersonnelActionAsync(entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added personnel action for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFilePersonnelActionResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFilePersonnelActionByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePersonnelActionByIdQuery, PersonnelFilePersonnelActionResponse>
{
    public async Task<Result<PersonnelFilePersonnelActionResponse>> Handle(
        GetPersonnelFilePersonnelActionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<PersonnelFilePersonnelActionResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetPersonnelActionAsync(personnelFile!.PublicId, query.PersonnelActionPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFilePersonnelActionResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFilePersonnelActionResponse>.Success(response);
    }
}

internal sealed class SearchPersonnelFilePersonnelActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<SearchPersonnelFilePersonnelActionsQuery, PagedResponse<PersonnelFilePersonnelActionResponse>>
{
    public async Task<Result<PagedResponse<PersonnelFilePersonnelActionResponse>>> Handle(
        SearchPersonnelFilePersonnelActionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<PagedResponse<PersonnelFilePersonnelActionResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PagedResponse<PersonnelFilePersonnelActionResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.SearchPersonnelActionsAsync(
            personnelFile!.PublicId,
            query.FromUtc,
            query.ToUtc,
            query.Type,
            query.Status,
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        return Result<PagedResponse<PersonnelFilePersonnelActionResponse>>.Success(response);
    }
}

internal sealed class ExportPersonnelFilePersonnelActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<ExportPersonnelFilePersonnelActionsQuery, IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>> Handle(
        ExportPersonnelFilePersonnelActionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.ExportPersonnelActionsAsync(
            personnelFile!.PublicId,
            query.FromUtc,
            query.ToUtc,
            query.Type,
            query.Status,
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.MaxRows,
            cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>.Success(response);
    }
}

