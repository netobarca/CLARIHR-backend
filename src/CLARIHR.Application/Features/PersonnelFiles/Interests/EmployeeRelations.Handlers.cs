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

internal sealed class GetPersonnelFileEmployeeRelationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmployeeRelationsQuery, IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>> Handle(
        GetPersonnelFileEmployeeRelationsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetEmployeeRelationsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileEmployeeRelationByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmployeeRelationByIdQuery, PersonnelFileEmployeeRelationResponse>
{
    public async Task<Result<PersonnelFileEmployeeRelationResponse>> Handle(
        GetPersonnelFileEmployeeRelationByIdQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFileEmployeeRelationResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetEmployeeRelationAsync(query.PersonnelFileId, query.EmployeeRelationPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileEmployeeRelationResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileEmployeeRelationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<AddPersonnelFileEmployeeRelationCommand, PersonnelFileEmployeeRelationResponse>
{
    public async Task<Result<PersonnelFileEmployeeRelationResponse>> Handle(
        AddPersonnelFileEmployeeRelationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileEmployeeRelationResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmployeeRelations,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;


        var relation = command.Relation;
        if (relation.RelatedEmployeePublicId == personnelFile.PublicId)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation.relatedEmployeePublicId"] = ["A personnel file cannot be related to itself."]
                    }));
        }

        var relatedPersonnelFile = await repository.GetForAccessCheckAsync(relation.RelatedEmployeePublicId, cancellationToken);
        if (relatedPersonnelFile is null || relatedPersonnelFile.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation.relatedEmployeePublicId"] = ["RelatedEmployeePublicId must reference an existing employee personnel file in the same tenant."]
                    }));
        }

        var existingDuplicate = personnelFile.EmployeeRelations.Any(existing =>
            existing.RelatedPersonnelFileId == relatedPersonnelFile.Id &&
            string.Equals(existing.Relationship, PersonnelFileNormalization.Clean(relation.Relationship, "relationship"), StringComparison.OrdinalIgnoreCase));
        if (existingDuplicate)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation"] = ["An employee relation with the same related employee and relationship already exists."]
                    }));
        }

        var before = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);
        var entity = PersonnelFileEmployeeRelation.Create(relatedPersonnelFile.Id, relation.Relationship);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddEmployeeRelation(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == entity.PublicId)
                ?? throw new InvalidOperationException("Personnel file employee relation response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added employee relation for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEmployeeRelationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileEmployeeRelationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileEmployeeRelationCommand, PersonnelFileEmployeeRelationResponse>
{
    public async Task<Result<PersonnelFileEmployeeRelationResponse>> Handle(
        UpdatePersonnelFileEmployeeRelationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileEmployeeRelationResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmployeeRelations,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var relationEntity = personnelFile.EmployeeRelations.FirstOrDefault(item => item.PublicId == command.RelationPublicId);
        if (relationEntity is null)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (relationEntity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var relation = command.Relation;
        if (relation.RelatedEmployeePublicId == personnelFile.PublicId)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation.relatedEmployeePublicId"] = ["A personnel file cannot be related to itself."]
                    }));
        }

        var relatedPersonnelFile = await repository.GetForAccessCheckAsync(relation.RelatedEmployeePublicId, cancellationToken);
        if (relatedPersonnelFile is null || relatedPersonnelFile.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation.relatedEmployeePublicId"] = ["RelatedEmployeePublicId must reference an existing employee personnel file in the same tenant."]
                    }));
        }

        var existingDuplicate = personnelFile.EmployeeRelations.Any(existing =>
            existing.PublicId != command.RelationPublicId &&
            existing.RelatedPersonnelFileId == relatedPersonnelFile.Id &&
            string.Equals(existing.Relationship, PersonnelFileNormalization.Clean(relation.Relationship, "relationship"), StringComparison.OrdinalIgnoreCase));
        if (existingDuplicate)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation"] = ["An employee relation with the same related employee and relationship already exists."]
                    }));
        }

        var before = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateEmployeeRelation(command.RelationPublicId, relatedPersonnelFile.Id, relation.Relationship);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.RelationPublicId)
                ?? throw new InvalidOperationException("Personnel file employee relation response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated employee relation for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEmployeeRelationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileEmployeeRelationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileEmployeeRelationCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileEmployeeRelationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmployeeRelations,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var relationEntity = personnelFile.EmployeeRelations.FirstOrDefault(item => item.PublicId == command.RelationPublicId);
        if (relationEntity is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (relationEntity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveEmployeeRelation(command.RelationPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted employee relation for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
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

internal sealed class PatchPersonnelFileEmployeeRelationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileEmployeeRelationCommand, PersonnelFileEmployeeRelationResponse>
{
    public async Task<Result<PersonnelFileEmployeeRelationResponse>> Handle(
        PatchPersonnelFileEmployeeRelationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileEmployeeRelationResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmployeeRelations,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var relationEntity = personnelFile.EmployeeRelations.FirstOrDefault(item => item.PublicId == command.EmployeeRelationPublicId);
        if (relationEntity is null)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (relationEntity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetEmployeeRelationAsync(personnelFile.PublicId, command.EmployeeRelationPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileEmployeeRelationPatchState.From(before);
        var applyResult = PersonnelFileEmployeeRelationPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileEmployeeRelationPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Success(before);
        }

        var input = state.ToInput();

        if (input.RelatedEmployeePublicId == personnelFile.PublicId)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relatedEmployeePublicId"] = ["A personnel file cannot be related to itself."]
                    }));
        }

        var relatedPersonnelFile = await repository.GetForAccessCheckAsync(input.RelatedEmployeePublicId, cancellationToken);
        if (relatedPersonnelFile is null || relatedPersonnelFile.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relatedEmployeePublicId"] = ["RelatedEmployeePublicId must reference an existing employee personnel file in the same tenant."]
                    }));
        }

        var existingDuplicate = personnelFile.EmployeeRelations.Any(existing =>
            existing.PublicId != command.EmployeeRelationPublicId &&
            existing.RelatedPersonnelFileId == relatedPersonnelFile.Id &&
            string.Equals(existing.Relationship, PersonnelFileNormalization.Clean(input.Relationship, "relationship"), StringComparison.OrdinalIgnoreCase));
        if (existingDuplicate)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation"] = ["An employee relation with the same related employee and relationship already exists."]
                    }));
        }

        var beforeList = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateEmployeeRelation(command.EmployeeRelationPublicId, relatedPersonnelFile.Id, input.Relationship);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.EmployeeRelationPublicId)
                ?? throw new InvalidOperationException("Personnel file employee relation response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched employee relation for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEmployeeRelationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

