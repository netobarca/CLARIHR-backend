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

internal sealed class GetPersonnelFileLanguagesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileLanguagesQuery, IReadOnlyCollection<PersonnelFileLanguageResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileLanguageResponse>>> Handle(
        GetPersonnelFileLanguagesQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileLanguageResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetLanguagesAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileLanguageResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileLanguageByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileLanguageByIdQuery, PersonnelFileLanguageResponse>
{
    public async Task<Result<PersonnelFileLanguageResponse>> Handle(
        GetPersonnelFileLanguageByIdQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFileLanguageResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetLanguageAsync(query.PersonnelFileId, query.LanguagePublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileLanguageResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileLanguageCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<AddPersonnelFileLanguageCommand, PersonnelFileLanguageResponse>
{
    public async Task<Result<PersonnelFileLanguageResponse>> Handle(
        AddPersonnelFileLanguageCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileLanguageResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Languages,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;


        var languageError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Language.LanguageCode),
            PersonnelCurriculumCatalogCategories.Language,
            command.Language.LanguageCode,
            cancellationToken);
        if (languageError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(languageError);
        }

        var levelError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Language.LevelCode),
            PersonnelCurriculumCatalogCategories.LanguageLevel,
            command.Language.LevelCode,
            cancellationToken);
        if (levelError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(levelError);
        }

        var before = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);
        PersonnelFileLanguage entity;
        try
        {
            entity = PersonnelFileLanguage.Create(
                command.Language.LanguageCode,
                command.Language.LevelCode,
                command.Language.Speaks,
                command.Language.Writes,
                command.Language.Reads);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddLanguage(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == entity.PublicId)
                ?? throw new InvalidOperationException("Personnel file language response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added language for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileLanguageResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileLanguageCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileLanguageCommand, PersonnelFileLanguageResponse>
{
    public async Task<Result<PersonnelFileLanguageResponse>> Handle(
        UpdatePersonnelFileLanguageCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileLanguageResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Languages,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var language = personnelFile.Languages.FirstOrDefault(item => item.PublicId == command.LanguagePublicId);
        if (language is null)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (language.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var languageError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Language.LanguageCode),
            PersonnelCurriculumCatalogCategories.Language,
            command.Language.LanguageCode,
            cancellationToken);
        if (languageError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(languageError);
        }

        var levelError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Language.LevelCode),
            PersonnelCurriculumCatalogCategories.LanguageLevel,
            command.Language.LevelCode,
            cancellationToken);
        if (levelError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(levelError);
        }

        var before = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateLanguage(
                command.LanguagePublicId,
                command.Language.LanguageCode,
                command.Language.LevelCode,
                command.Language.Speaks,
                command.Language.Writes,
                command.Language.Reads);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.LanguagePublicId)
                ?? throw new InvalidOperationException("Personnel file language response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated language for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileLanguageResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileLanguageCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileLanguageCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileLanguageCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Languages,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var language = personnelFile.Languages.FirstOrDefault(item => item.PublicId == command.LanguagePublicId);
        if (language is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (language.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveLanguage(command.LanguagePublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted language for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
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

internal sealed class PatchPersonnelFileLanguageCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileLanguageCommand, PersonnelFileLanguageResponse>
{
    public async Task<Result<PersonnelFileLanguageResponse>> Handle(
        PatchPersonnelFileLanguageCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileLanguageResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Languages,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var language = personnelFile.Languages.FirstOrDefault(item => item.PublicId == command.LanguagePublicId);
        if (language is null)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (language.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetLanguageAsync(personnelFile.PublicId, command.LanguagePublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileLanguagePatchState.From(before);
        var applyResult = PersonnelFileLanguagePatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileLanguagePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileLanguageResponse>.Success(before);
        }

        var input = state.ToInput();

        var languageError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "languageCode",
            PersonnelCurriculumCatalogCategories.Language,
            input.LanguageCode,
            cancellationToken);
        if (languageError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(languageError);
        }

        var levelError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "levelCode",
            PersonnelCurriculumCatalogCategories.LanguageLevel,
            input.LevelCode,
            cancellationToken);
        if (levelError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(levelError);
        }

        var beforeList = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateLanguage(
                command.LanguagePublicId,
                input.LanguageCode,
                input.LevelCode,
                input.Speaks,
                input.Writes,
                input.Reads);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.LanguagePublicId)
                ?? throw new InvalidOperationException("Personnel file language response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched language for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileLanguageResponse>.Success(response);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

