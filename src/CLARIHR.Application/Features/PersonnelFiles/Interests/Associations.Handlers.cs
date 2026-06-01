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

internal sealed class GetPersonnelFileAssociationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAssociationsQuery, IReadOnlyCollection<PersonnelFileAssociationResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileAssociationResponse>>> Handle(
        GetPersonnelFileAssociationsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileAssociationResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetAssociationsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileAssociationResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileAssociationByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAssociationByIdQuery, PersonnelFileAssociationResponse>
{
    public async Task<Result<PersonnelFileAssociationResponse>> Handle(
        GetPersonnelFileAssociationByIdQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFileAssociationResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetAssociationAsync(query.PersonnelFileId, query.AssociationPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileAssociationResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileAssociationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<AddPersonnelFileAssociationCommand, PersonnelFileAssociationResponse>
{
    public async Task<Result<PersonnelFileAssociationResponse>> Handle(
        AddPersonnelFileAssociationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileAssociationResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Associations,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;


        PersonnelFileAssociation association;
        try
        {
            association = PersonnelFileAssociation.Create(
                command.Association.AssociationName,
                command.Association.Role,
                command.Association.JoinedDate,
                command.Association.LeftDate,
                command.Association.Payment);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        var before = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddAssociation(association);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == association.PublicId)
                ?? throw new InvalidOperationException("Personnel file association response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added association for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAssociationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileAssociationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileAssociationCommand, PersonnelFileAssociationResponse>
{
    public async Task<Result<PersonnelFileAssociationResponse>> Handle(
        UpdatePersonnelFileAssociationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileAssociationResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Associations,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var association = personnelFile.Associations.FirstOrDefault(item => item.PublicId == command.AssociationPublicId);
        if (association is null)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (association.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateAssociation(
                command.AssociationPublicId,
                command.Association.AssociationName,
                command.Association.Role,
                command.Association.JoinedDate,
                command.Association.LeftDate,
                command.Association.Payment);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.AssociationPublicId)
                ?? throw new InvalidOperationException("Personnel file association response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated association for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAssociationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileAssociationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileAssociationCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileAssociationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Associations,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var association = personnelFile.Associations.FirstOrDefault(item => item.PublicId == command.AssociationPublicId);
        if (association is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (association.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveAssociation(command.AssociationPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted association for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
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

internal sealed class PatchPersonnelFileAssociationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileAssociationCommand, PersonnelFileAssociationResponse>
{
    public async Task<Result<PersonnelFileAssociationResponse>> Handle(
        PatchPersonnelFileAssociationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileAssociationResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Associations,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var association = personnelFile.Associations.FirstOrDefault(item => item.PublicId == command.AssociationPublicId);
        if (association is null)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (association.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAssociationAsync(personnelFile.PublicId, command.AssociationPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileAssociationPatchState.From(before);
        var applyResult = PersonnelFileAssociationPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileAssociationPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileAssociationResponse>.Success(before);
        }

        var input = state.ToInput();

        var beforeList = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateAssociation(
                command.AssociationPublicId,
                input.AssociationName,
                input.Role,
                input.JoinedDate,
                input.LeftDate,
                input.Payment);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.AssociationPublicId)
                ?? throw new InvalidOperationException("Personnel file association response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched association for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAssociationResponse>.Success(response);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

