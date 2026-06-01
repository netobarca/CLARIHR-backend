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

internal sealed class GetPersonnelFileReferencesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileReferencesQuery, IReadOnlyCollection<PersonnelFileReferenceResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileReferenceResponse>>> Handle(
        GetPersonnelFileReferencesQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileReferenceResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetReferencesAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileReferenceResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileReferenceByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileReferenceByIdQuery, PersonnelFileReferenceResponse>
{
    public async Task<Result<PersonnelFileReferenceResponse>> Handle(
        GetPersonnelFileReferenceByIdQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFileReferenceResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetReferenceAsync(query.PersonnelFileId, query.ReferencePublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileReferenceResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileReferenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<AddPersonnelFileReferenceCommand, PersonnelFileReferenceResponse>
{
    public async Task<Result<PersonnelFileReferenceResponse>> Handle(
        AddPersonnelFileReferenceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileReferenceResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.References,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;


        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Reference.ReferenceTypeCode),
            PersonnelCurriculumCatalogCategories.ReferenceType,
            command.Reference.ReferenceTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileReferenceResponse>.Failure(typeError);

        var before = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);

        PersonnelFileReference reference;
        try
        {
            reference = PersonnelFileReference.Create(
                command.Reference.PersonName,
                command.Reference.Address,
                command.Reference.Phone,
                command.Reference.ReferenceTypeCode,
                command.Reference.Occupation,
                command.Reference.Workplace,
                command.Reference.WorkPhone,
                command.Reference.KnownTimeYears);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddReference(reference);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == reference.PublicId)
                ?? throw new InvalidOperationException("Personnel file reference response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added reference for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileReferenceResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileReferenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileReferenceCommand, PersonnelFileReferenceResponse>
{
    public async Task<Result<PersonnelFileReferenceResponse>> Handle(
        UpdatePersonnelFileReferenceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileReferenceResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.References,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var reference = personnelFile.References.FirstOrDefault(item => item.PublicId == command.ReferencePublicId);
        if (reference is null)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (reference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Reference.ReferenceTypeCode),
            PersonnelCurriculumCatalogCategories.ReferenceType,
            command.Reference.ReferenceTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileReferenceResponse>.Failure(typeError);

        var before = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                personnelFile.UpdateReference(
                    command.ReferencePublicId,
                    command.Reference.PersonName,
                    command.Reference.Address,
                    command.Reference.Phone,
                    command.Reference.ReferenceTypeCode,
                    command.Reference.Occupation,
                    command.Reference.Workplace,
                    command.Reference.WorkPhone,
                    command.Reference.KnownTimeYears);
            }
            catch (InvalidOperationException)
            {
                return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.ReferencePublicId)
                ?? throw new InvalidOperationException("Personnel file reference response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated reference for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileReferenceResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileReferenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileReferenceCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileReferenceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.References,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var reference = personnelFile.References.FirstOrDefault(item => item.PublicId == command.ReferencePublicId);
        if (reference is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (reference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveReference(command.ReferencePublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted reference for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
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

internal sealed class PatchPersonnelFileReferenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileReferenceCommand, PersonnelFileReferenceResponse>
{
    public async Task<Result<PersonnelFileReferenceResponse>> Handle(
        PatchPersonnelFileReferenceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileReferenceResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.References,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var reference = personnelFile.References.FirstOrDefault(item => item.PublicId == command.ReferencePublicId);
        if (reference is null)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (reference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetReferenceAsync(personnelFile.PublicId, command.ReferencePublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileReferencePatchState.From(before);
        var applyResult = PersonnelFileReferencePatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileReferencePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileReferenceResponse>.Success(before);
        }

        var input = state.ToInput();

        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "referenceTypeCode",
            PersonnelCurriculumCatalogCategories.ReferenceType,
            input.ReferenceTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileReferenceResponse>.Failure(typeError);

        var beforeList = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateReference(
                command.ReferencePublicId,
                input.PersonName,
                input.Address,
                input.Phone,
                input.ReferenceTypeCode,
                input.Occupation,
                input.Workplace,
                input.WorkPhone,
                input.KnownTimeYears);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.ReferencePublicId)
                ?? throw new InvalidOperationException("Personnel file reference response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched reference for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileReferenceResponse>.Success(response);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

