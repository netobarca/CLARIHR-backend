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

public sealed record PersonnelFilePayrollTransactionResponse(
    Guid PayrollTransactionPublicId,
    string TransactionTypeCode,
    DateTime TransactionDateUtc,
    string PayrollPeriodCode,
    string? Description,
    decimal Amount,
    string CurrencyCode,
    bool IsDebit,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => PayrollTransactionPublicId;
}

public sealed record PersonnelFilePayrollTransactionExportRow(
    Guid Id,
    string TransactionTypeCode,
    DateTime TransactionDateUtc,
    string PayrollPeriodCode,
    string? Description,
    decimal Amount,
    string CurrencyCode,
    bool IsDebit,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PayrollTransactionInput(
    string TransactionTypeCode,
    DateTime TransactionDateUtc,
    string PayrollPeriodCode,
    string? Description,
    decimal Amount,
    string CurrencyCode,
    bool IsDebit,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFilePayrollTransactionCommand(
    Guid PersonnelFileId,
    PayrollTransactionInput Item)
    : ICommand<PersonnelFilePayrollTransactionResponse>;

public sealed record GetPersonnelFilePayrollTransactionByIdQuery(Guid PersonnelFileId, Guid PayrollTransactionPublicId)
    : IQuery<PersonnelFilePayrollTransactionResponse>;

public sealed record PersonnelFilePayrollTransactionPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFilePayrollTransactionCommand(
    Guid PersonnelFileId,
    Guid PayrollTransactionPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFilePayrollTransactionPatchOperation> Operations)
    : ICommand<PersonnelFilePayrollTransactionResponse>;

public sealed record SearchPersonnelFilePayrollTransactionsQuery(
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
    : IQuery<PagedResponse<PersonnelFilePayrollTransactionResponse>>;

public sealed record ExportPersonnelFilePayrollTransactionsQuery(
    Guid PersonnelFileId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Type,
    string? Status,
    string? Search,
    string? SortBy = null,
    PersonnelFileSortDirection SortDirection = PersonnelFileSortDirection.Desc,
    int? MaxRows = null)
    : IQuery<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>;

internal sealed class PayrollTransactionInputValidator : AbstractValidator<PayrollTransactionInput>
{
    public PayrollTransactionInputValidator()
    {
        RuleFor(input => input.TransactionTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.PayrollPeriodCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.CurrencyCode).NotEmpty().MaximumLength(40);
    }
}

internal sealed class AddPersonnelFilePayrollTransactionCommandValidator : AbstractValidator<AddPersonnelFilePayrollTransactionCommand>
{
    public AddPersonnelFilePayrollTransactionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PayrollTransactionInputValidator());
    }
}

internal sealed class GetPersonnelFilePayrollTransactionByIdQueryValidator : AbstractValidator<GetPersonnelFilePayrollTransactionByIdQuery>
{
    public GetPersonnelFilePayrollTransactionByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.PayrollTransactionPublicId).NotEmpty();
    }
}

internal sealed class SearchPersonnelFilePayrollTransactionsQueryValidator : AbstractValidator<SearchPersonnelFilePayrollTransactionsQuery>
{
    public SearchPersonnelFilePayrollTransactionsQueryValidator()
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

internal sealed class PatchPersonnelFilePayrollTransactionCommandValidator : AbstractValidator<PatchPersonnelFilePayrollTransactionCommand>
{
    public PatchPersonnelFilePayrollTransactionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.PayrollTransactionPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Operations).NotEmpty();
        RuleFor(c => c.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(c => c.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class AddPersonnelFilePayrollTransactionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFilePayrollTransactionCommand, PersonnelFilePayrollTransactionResponse>
{
    public async Task<Result<PersonnelFilePayrollTransactionResponse>> Handle(
        AddPersonnelFilePayrollTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFilePayrollTransactionResponse>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFilePayrollTransactionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFilePayrollTransaction.Create(
            command.Item.TransactionTypeCode,
            command.Item.TransactionDateUtc,
            command.Item.PayrollPeriodCode,
            command.Item.Description,
            command.Item.Amount,
            command.Item.CurrencyCode,
            command.Item.IsDebit,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddPayrollTransactionAsync(entity, cancellationToken);
        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added payroll transaction for {personnelFile.FullName}.", response, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        return Result<PersonnelFilePayrollTransactionResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFilePayrollTransactionByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePayrollTransactionByIdQuery, PersonnelFilePayrollTransactionResponse>
{
    public async Task<Result<PersonnelFilePayrollTransactionResponse>> Handle(
        GetPersonnelFilePayrollTransactionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<PersonnelFilePayrollTransactionResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetPayrollTransactionAsync(personnelFile!.PublicId, query.PayrollTransactionPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFilePayrollTransactionResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFilePayrollTransactionResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFilePayrollTransactionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFilePayrollTransactionCommand, PersonnelFilePayrollTransactionResponse>
{
    public async Task<Result<PersonnelFilePayrollTransactionResponse>> Handle(
        PatchPersonnelFilePayrollTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFilePayrollTransactionResponse>(
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
            return Result<PersonnelFilePayrollTransactionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetPayrollTransactionAsync(personnelFile.PublicId, command.PayrollTransactionPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFilePayrollTransactionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePayrollTransactionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFilePayrollTransactionPatchState.From(existing);
        var applyResult = PersonnelFilePayrollTransactionPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFilePayrollTransactionResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFilePayrollTransactionPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFilePayrollTransactionResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFilePayrollTransactionResponse>.Success(existing);
        }

        var response = await employeeRepository.PatchPayrollTransactionAsync(
            command.PayrollTransactionPublicId,
            personnelFile.TenantId,
            state.IsActive,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFilePayrollTransactionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched payroll transaction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFilePayrollTransactionResponse>.Success(response);
    }
}

internal sealed class PersonnelFilePayrollTransactionPatchState
{
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFilePayrollTransactionPatchState From(PersonnelFilePayrollTransactionResponse response) =>
        new()
        {
            IsActive = response.IsActive
        };
}

internal static class PersonnelFilePayrollTransactionPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFilePayrollTransactionPatchOperation> operations, PersonnelFilePayrollTransactionPatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!PersonnelFileTalentPatch.SupportedOperations.Contains(op))
            {
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = PersonnelFileTalentPatch.ParsePath(operation.Path);
            if (segments.Length != 1)
            {
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only the root isActive property can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (PersonnelFilePatchValueException exception)
            {
                return PersonnelFileTalentPatch.ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(PersonnelFilePayrollTransactionPatchState state) => Result.Success();

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFilePayrollTransactionPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "isActive"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "IsActive cannot be removed.")
                : Mutate(state, () =>
                {
                    state.IsActive = PersonnelFileTalentPatch.ReadRequiredBoolean(value, path);
                    state.IsActiveMutated = true;
                });
        }

        return PersonnelFileTalentPatch.ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static Result Mutate(PersonnelFilePayrollTransactionPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}

internal sealed class SearchPersonnelFilePayrollTransactionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<SearchPersonnelFilePayrollTransactionsQuery, PagedResponse<PersonnelFilePayrollTransactionResponse>>
{
    public async Task<Result<PagedResponse<PersonnelFilePayrollTransactionResponse>>> Handle(
        SearchPersonnelFilePayrollTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<PagedResponse<PersonnelFilePayrollTransactionResponse>>(
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
            return Result<PagedResponse<PersonnelFilePayrollTransactionResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.SearchPayrollTransactionsAsync(
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
        return Result<PagedResponse<PersonnelFilePayrollTransactionResponse>>.Success(response);
    }
}

internal sealed class ExportPersonnelFilePayrollTransactionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<ExportPersonnelFilePayrollTransactionsQuery, IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>> Handle(
        ExportPersonnelFilePayrollTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>(
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
            return Result<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.ExportPayrollTransactionsAsync(
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
        return Result<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>.Success(response);
    }
}

