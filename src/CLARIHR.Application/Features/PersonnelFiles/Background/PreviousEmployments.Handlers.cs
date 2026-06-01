using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;
using FluentValidation.Results;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class GetPersonnelFilePreviousEmploymentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePreviousEmploymentsQuery, IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>> Handle(
        GetPersonnelFilePreviousEmploymentsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetPreviousEmploymentsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFilePreviousEmploymentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePreviousEmploymentByIdQuery, PersonnelFilePreviousEmploymentResponse>
{
    public async Task<Result<PersonnelFilePreviousEmploymentResponse>> Handle(
        GetPersonnelFilePreviousEmploymentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFilePreviousEmploymentResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetPreviousEmploymentAsync(query.PersonnelFileId, query.PreviousEmploymentPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFilePreviousEmploymentResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFilePreviousEmploymentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFilePreviousEmploymentCommand, PersonnelFilePreviousEmploymentResponse>
{
    public async Task<Result<PersonnelFilePreviousEmploymentResponse>> Handle(
        AddPersonnelFilePreviousEmploymentCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.PreviousEmployments,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }


        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.PreviousEmployment.CurrencyCode),
            PersonnelCurriculumCatalogCategories.Currency,
            command.PreviousEmployment.CurrencyCode,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFilePreviousEmploymentResponse>.Failure(currencyError);

        var before = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);

        PersonnelFilePreviousEmployment item;
        try
        {
            item = PersonnelFilePreviousEmployment.Create(
                command.PreviousEmployment.Institution,
                command.PreviousEmployment.Place,
                command.PreviousEmployment.LastPosition,
                command.PreviousEmployment.ManagerName,
                command.PreviousEmployment.EntryDate,
                command.PreviousEmployment.RetirementDate,
                command.PreviousEmployment.CompanyPhone,
                command.PreviousEmployment.ExitReason,
                command.PreviousEmployment.FirstSalaryAmount,
                command.PreviousEmployment.LastSalaryAmount,
                command.PreviousEmployment.AverageCommissionAmount,
                command.PreviousEmployment.CurrencyCode);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddPreviousEmployment(item);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(r => r.Id == item.PublicId)
                ?? throw new InvalidOperationException("Personnel file previous employment response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added previous employment for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFilePreviousEmploymentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFilePreviousEmploymentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFilePreviousEmploymentCommand, PersonnelFilePreviousEmploymentResponse>
{
    public async Task<Result<PersonnelFilePreviousEmploymentResponse>> Handle(
        UpdatePersonnelFilePreviousEmploymentCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.PreviousEmployments,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var previousEmployment = personnelFile.PreviousEmployments.FirstOrDefault(item => item.PublicId == command.PreviousEmploymentPublicId);
        if (previousEmployment is null)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (previousEmployment.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.PreviousEmployment.CurrencyCode),
            PersonnelCurriculumCatalogCategories.Currency,
            command.PreviousEmployment.CurrencyCode,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFilePreviousEmploymentResponse>.Failure(currencyError);

        var before = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                personnelFile.UpdatePreviousEmployment(
                    command.PreviousEmploymentPublicId,
                    command.PreviousEmployment.Institution,
                    command.PreviousEmployment.Place,
                    command.PreviousEmployment.LastPosition,
                    command.PreviousEmployment.ManagerName,
                    command.PreviousEmployment.EntryDate,
                    command.PreviousEmployment.RetirementDate,
                    command.PreviousEmployment.CompanyPhone,
                    command.PreviousEmployment.ExitReason,
                    command.PreviousEmployment.FirstSalaryAmount,
                    command.PreviousEmployment.LastSalaryAmount,
                    command.PreviousEmployment.AverageCommissionAmount,
                    command.PreviousEmployment.CurrencyCode);
            }
            catch (InvalidOperationException)
            {
                return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(r => r.Id == command.PreviousEmploymentPublicId)
                ?? throw new InvalidOperationException("Personnel file previous employment response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated previous employment for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFilePreviousEmploymentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFilePreviousEmploymentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFilePreviousEmploymentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFilePreviousEmploymentCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.PreviousEmployments,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var previousEmployment = personnelFile.PreviousEmployments.FirstOrDefault(item => item.PublicId == command.PreviousEmploymentPublicId);
        if (previousEmployment is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (previousEmployment.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemovePreviousEmployment(command.PreviousEmploymentPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted previous employment for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileParentConcurrencyResult>.Success(
                new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchPersonnelFilePreviousEmploymentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchPersonnelFilePreviousEmploymentCommand, PersonnelFilePreviousEmploymentResponse>
{
    public async Task<Result<PersonnelFilePreviousEmploymentResponse>> Handle(
        PatchPersonnelFilePreviousEmploymentCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.PreviousEmployments,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var previousEmployment = personnelFile.PreviousEmployments.FirstOrDefault(item => item.PublicId == command.PreviousEmploymentPublicId);
        if (previousEmployment is null)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (previousEmployment.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetPreviousEmploymentAsync(personnelFile.PublicId, command.PreviousEmploymentPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFilePreviousEmploymentPatchState.From(before);
        var applyResult = PersonnelFilePreviousEmploymentPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFilePreviousEmploymentPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Success(before);
        }

        var input = state.ToInput();

        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "currencyCode",
            PersonnelCurriculumCatalogCategories.Currency,
            input.CurrencyCode,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFilePreviousEmploymentResponse>.Failure(currencyError);

        var beforeList = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdatePreviousEmployment(
                command.PreviousEmploymentPublicId,
                input.Institution,
                input.Place,
                input.LastPosition,
                input.ManagerName,
                input.EntryDate,
                input.RetirementDate,
                input.CompanyPhone,
                input.ExitReason,
                input.FirstSalaryAmount,
                input.LastSalaryAmount,
                input.AverageCommissionAmount,
                input.CurrencyCode);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.PreviousEmploymentPublicId)
                ?? throw new InvalidOperationException("Personnel file previous employment response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched previous employment for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFilePreviousEmploymentResponse>.Success(response);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

