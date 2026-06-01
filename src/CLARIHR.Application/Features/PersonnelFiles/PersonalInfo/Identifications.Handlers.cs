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

internal sealed class GetPersonnelFileIdentificationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileIdentificationsQuery, IReadOnlyCollection<PersonnelFileIdentificationResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileIdentificationResponse>>> Handle(
        GetPersonnelFileIdentificationsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileIdentificationResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetIdentificationsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileIdentificationResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileIdentificationByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileIdentificationByIdQuery, PersonnelFileIdentificationResponse>
{
    public async Task<Result<PersonnelFileIdentificationResponse>> Handle(
        GetPersonnelFileIdentificationByIdQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFileIdentificationResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetIdentificationAsync(query.PersonnelFileId, query.IdentificationPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileIdentificationResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileIdentificationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<AddPersonnelFileIdentificationCommand, PersonnelFileIdentificationResponse>
{
    public async Task<Result<PersonnelFileIdentificationResponse>> Handle(
        AddPersonnelFileIdentificationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileIdentificationResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Identifications,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;


        var normalizedIdentificationTypeCode = command.Identification.IdentificationTypeCode.Trim().ToUpperInvariant();
        var identificationTypeValidation = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
            repository,
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            cancellationToken);
        if (identificationTypeValidation != Error.None)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(identificationTypeValidation);
        }

        var normalizedIdentificationNumber = command.Identification.IdentificationNumber.Trim().ToUpperInvariant();
        var exists = await repository.IdentificationExistsAsync(
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            normalizedIdentificationNumber,
            excludingPersonnelFileId: null,
            cancellationToken);
        if (exists)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.IdentificationConflict);
        }

        var before = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);

        var identification = PersonnelFileIdentification.Create(
            normalizedIdentificationTypeCode,
            command.Identification.IdentificationNumber,
            command.Identification.IssuedDate,
            command.Identification.ExpiryDate,
            command.Identification.Issuer,
            command.Identification.IsPrimary);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddIdentification(identification);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == identification.PublicId)
                ?? throw new InvalidOperationException("Personnel file identification response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added identification for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileIdentificationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileIdentificationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileIdentificationCommand, PersonnelFileIdentificationResponse>
{
    public async Task<Result<PersonnelFileIdentificationResponse>> Handle(
        UpdatePersonnelFileIdentificationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileIdentificationResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Identifications,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var identification = personnelFile.Identifications.FirstOrDefault(item => item.PublicId == command.IdentificationPublicId);
        if (identification is null)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (identification.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var normalizedIdentificationTypeCode = command.Identification.IdentificationTypeCode.Trim().ToUpperInvariant();
        var identificationTypeValidation = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
            repository,
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            cancellationToken);
        if (identificationTypeValidation != Error.None)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(identificationTypeValidation);
        }

        var normalizedIdentificationNumber = command.Identification.IdentificationNumber.Trim().ToUpperInvariant();
        var exists = await repository.IdentificationExistsAsync(
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            normalizedIdentificationNumber,
            excludingPersonnelFileId: personnelFile.Id,
            cancellationToken);
        if (exists)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.IdentificationConflict);
        }

        var before = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateIdentification(
                command.IdentificationPublicId,
                normalizedIdentificationTypeCode,
                command.Identification.IdentificationNumber,
                command.Identification.IssuedDate,
                command.Identification.ExpiryDate,
                command.Identification.Issuer,
                command.Identification.IsPrimary);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.IdentificationPublicId)
                ?? throw new InvalidOperationException("Personnel file identification response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated identification for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileIdentificationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileIdentificationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileIdentificationCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileIdentificationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Identifications,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var identification = personnelFile.Identifications.FirstOrDefault(item => item.PublicId == command.IdentificationPublicId);
        if (identification is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (identification.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveIdentification(command.IdentificationPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted identification for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
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

internal sealed class PatchPersonnelFileIdentificationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileIdentificationCommand, PersonnelFileIdentificationResponse>
{
    public async Task<Result<PersonnelFileIdentificationResponse>> Handle(
        PatchPersonnelFileIdentificationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileIdentificationResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Identifications,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var identification = personnelFile.Identifications.FirstOrDefault(item => item.PublicId == command.IdentificationPublicId);
        if (identification is null)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (identification.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetIdentificationAsync(personnelFile.PublicId, command.IdentificationPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileIdentificationPatchState.From(before);
        var applyResult = PersonnelFileIdentificationPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileIdentificationPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileIdentificationResponse>.Success(before);
        }

        var input = state.ToInput();

        var normalizedIdentificationTypeCode = input.IdentificationTypeCode.Trim().ToUpperInvariant();
        var identificationTypeValidation = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
            repository,
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            cancellationToken);
        if (identificationTypeValidation != Error.None)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(identificationTypeValidation);
        }

        var normalizedIdentificationNumber = input.IdentificationNumber.Trim().ToUpperInvariant();
        var exists = await repository.IdentificationExistsAsync(
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            normalizedIdentificationNumber,
            excludingPersonnelFileId: personnelFile.Id,
            cancellationToken);
        if (exists)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.IdentificationConflict);
        }

        var beforeList = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateIdentification(
                command.IdentificationPublicId,
                normalizedIdentificationTypeCode,
                input.IdentificationNumber,
                input.IssuedDate,
                input.ExpiryDate,
                input.Issuer,
                input.IsPrimary);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.IdentificationPublicId)
                ?? throw new InvalidOperationException("Personnel file identification response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched identification for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileIdentificationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

