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

internal sealed class AddPersonnelFileTrainingCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileTrainingCommand, PersonnelFileTrainingResponse>
{
    public async Task<Result<PersonnelFileTrainingResponse>> Handle(
        AddPersonnelFileTrainingCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Trainings,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }


        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.TrainingTypeCode),
            PersonnelCurriculumCatalogCategories.TrainingType,
            command.Training.TrainingTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(typeError);

        var countryError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.CountryCode),
            PersonnelCurriculumCatalogCategories.Country,
            command.Training.CountryCode,
            cancellationToken);
        if (countryError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(countryError);

        var durationUnitError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.DurationUnitCode),
            PersonnelCurriculumCatalogCategories.DurationUnit,
            command.Training.DurationUnitCode,
            cancellationToken);
        if (durationUnitError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(durationUnitError);

        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.CostCurrencyCode),
            PersonnelCurriculumCatalogCategories.Currency,
            command.Training.CostCurrencyCode ?? string.Empty,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(currencyError);

        var before = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);
        
        PersonnelFileTraining training;
        try
        {
            training = PersonnelFileTraining.Create(
                command.Training.TrainingName,
                command.Training.TrainingTypeCode,
                command.Training.Description,
                command.Training.Topic,
                command.Training.Institution,
                command.Training.Instructors,
                command.Training.Score,
                command.Training.StartDate,
                command.Training.EndDate,
                command.Training.IsInternal,
                command.Training.IsLocal,
                command.Training.CountryCode,
                command.Training.DurationValue,
                command.Training.DurationUnitCode,
                command.Training.CostAmount,
                command.Training.CostCurrencyCode);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddTraining(training);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == training.PublicId)
                ?? throw new InvalidOperationException("Personnel file training response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added training for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileTrainingResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileTrainingCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileTrainingCommand, PersonnelFileTrainingResponse>
{
    public async Task<Result<PersonnelFileTrainingResponse>> Handle(
        UpdatePersonnelFileTrainingCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Trainings,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var training = personnelFile.Trainings.FirstOrDefault(item => item.PublicId == command.TrainingPublicId);
        if (training is null)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (training.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.TrainingTypeCode),
            PersonnelCurriculumCatalogCategories.TrainingType,
            command.Training.TrainingTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(typeError);

        var countryError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.CountryCode),
            PersonnelCurriculumCatalogCategories.Country,
            command.Training.CountryCode,
            cancellationToken);
        if (countryError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(countryError);

        var durationUnitError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.DurationUnitCode),
            PersonnelCurriculumCatalogCategories.DurationUnit,
            command.Training.DurationUnitCode,
            cancellationToken);
        if (durationUnitError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(durationUnitError);

        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.CostCurrencyCode),
            PersonnelCurriculumCatalogCategories.Currency,
            command.Training.CostCurrencyCode ?? string.Empty,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(currencyError);

        var before = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                personnelFile.UpdateTraining(
                    command.TrainingPublicId,
                    command.Training.TrainingName,
                    command.Training.TrainingTypeCode,
                    command.Training.Description,
                    command.Training.Topic,
                    command.Training.Institution,
                    command.Training.Instructors,
                    command.Training.Score,
                    command.Training.StartDate,
                    command.Training.EndDate,
                    command.Training.IsInternal,
                    command.Training.IsLocal,
                    command.Training.CountryCode,
                    command.Training.DurationValue,
                    command.Training.DurationUnitCode,
                    command.Training.CostAmount,
                    command.Training.CostCurrencyCode);
            }
            catch (InvalidOperationException)
            {
                return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.TrainingPublicId)
                ?? throw new InvalidOperationException("Personnel file training response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated training for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileTrainingResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileTrainingCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileTrainingCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileTrainingCommand command,
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
            PersonnelFileTrackedSection.Trainings,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var training = personnelFile.Trainings.FirstOrDefault(item => item.PublicId == command.TrainingPublicId);
        if (training is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (training.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveTraining(command.TrainingPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted training for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
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

internal sealed class PatchPersonnelFileTrainingCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchPersonnelFileTrainingCommand, PersonnelFileTrainingResponse>
{
    public async Task<Result<PersonnelFileTrainingResponse>> Handle(
        PatchPersonnelFileTrainingCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Trainings,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var training = personnelFile.Trainings.FirstOrDefault(item => item.PublicId == command.TrainingPublicId);
        if (training is null)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (training.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetTrainingAsync(personnelFile.PublicId, command.TrainingPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileTrainingPatchState.From(before);
        var applyResult = PersonnelFileTrainingPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileTrainingPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileTrainingResponse>.Success(before);
        }

        var input = state.ToInput();

        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "trainingTypeCode",
            PersonnelCurriculumCatalogCategories.TrainingType,
            input.TrainingTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(typeError);

        var countryError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "countryCode",
            PersonnelCurriculumCatalogCategories.Country,
            input.CountryCode,
            cancellationToken);
        if (countryError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(countryError);

        var durationUnitError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "durationUnitCode",
            PersonnelCurriculumCatalogCategories.DurationUnit,
            input.DurationUnitCode,
            cancellationToken);
        if (durationUnitError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(durationUnitError);

        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "costCurrencyCode",
            PersonnelCurriculumCatalogCategories.Currency,
            input.CostCurrencyCode ?? string.Empty,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(currencyError);

        var beforeList = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateTraining(
                command.TrainingPublicId,
                input.TrainingName,
                input.TrainingTypeCode,
                input.Description,
                input.Topic,
                input.Institution,
                input.Instructors,
                input.Score,
                input.StartDate,
                input.EndDate,
                input.IsInternal,
                input.IsLocal,
                input.CountryCode,
                input.DurationValue,
                input.DurationUnitCode,
                input.CostAmount,
                input.CostCurrencyCode);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.TrainingPublicId)
                ?? throw new InvalidOperationException("Personnel file training response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched training for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileTrainingResponse>.Success(response);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

