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

internal sealed class GetPersonnelFileAddressesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAddressesQuery, IReadOnlyCollection<PersonnelFileAddressResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileAddressResponse>>> Handle(
        GetPersonnelFileAddressesQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileAddressResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetAddressesAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileAddressResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileAddressByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAddressByIdQuery, PersonnelFileAddressResponse>
{
    public async Task<Result<PersonnelFileAddressResponse>> Handle(
        GetPersonnelFileAddressByIdQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFileAddressResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetAddressAsync(query.PersonnelFileId, query.AddressPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileAddressResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileAddressCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<AddPersonnelFileAddressCommand, PersonnelFileAddressResponse>
{
    public async Task<Result<PersonnelFileAddressResponse>> Handle(
        AddPersonnelFileAddressCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileAddressResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Addresses,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        // Optional address type validated against the AddressType reference catalog (RF-002, D-03).
        var addressTypeValidation = await PersonnelReferenceCatalogValidation.ValidateAddressTypeCodeAsync(
            repository,
            personnelFile.TenantId,
            "addressTypeCode",
            command.Address.AddressTypeCode,
            cancellationToken);
        if (addressTypeValidation != Error.None)
        {
            return Result<PersonnelFileAddressResponse>.Failure(addressTypeValidation);
        }

        var before = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);
        var address = PersonnelFileAddress.Create(
            command.Address.AddressLine,
            command.Address.AddressTypeCode,
            command.Address.Country,
            command.Address.Department,
            command.Address.Municipality,
            command.Address.PostalCode,
            command.Address.IsCurrent);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddAddress(address);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == address.PublicId)
                ?? throw new InvalidOperationException("Personnel file address response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added address for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAddressResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileAddressCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileAddressCommand, PersonnelFileAddressResponse>
{
    public async Task<Result<PersonnelFileAddressResponse>> Handle(
        UpdatePersonnelFileAddressCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileAddressResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Addresses,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var address = personnelFile.Addresses.FirstOrDefault(item => item.PublicId == command.AddressPublicId);
        if (address is null)
        {
            return Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (address.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Optional address type validated against the AddressType reference catalog (RF-002, D-03).
        var addressTypeValidation = await PersonnelReferenceCatalogValidation.ValidateAddressTypeCodeAsync(
            repository,
            personnelFile.TenantId,
            "addressTypeCode",
            command.Address.AddressTypeCode,
            cancellationToken);
        if (addressTypeValidation != Error.None)
        {
            return Result<PersonnelFileAddressResponse>.Failure(addressTypeValidation);
        }

        var before = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateAddress(
                command.AddressPublicId,
                command.Address.AddressLine,
                command.Address.AddressTypeCode,
                command.Address.Country,
                command.Address.Department,
                command.Address.Municipality,
                command.Address.PostalCode,
                command.Address.IsCurrent);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.AddressPublicId)
                ?? throw new InvalidOperationException("Personnel file address response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated address for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAddressResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileAddressCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileAddressCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileAddressCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Addresses,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var address = personnelFile.Addresses.FirstOrDefault(item => item.PublicId == command.AddressPublicId);
        if (address is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (address.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveAddress(command.AddressPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted address for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
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

internal sealed class PatchPersonnelFileAddressCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileAddressCommand, PersonnelFileAddressResponse>
{
    public async Task<Result<PersonnelFileAddressResponse>> Handle(
        PatchPersonnelFileAddressCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileAddressResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Addresses,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var address = personnelFile.Addresses.FirstOrDefault(item => item.PublicId == command.AddressPublicId);
        if (address is null)
        {
            return Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (address.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAddressAsync(personnelFile.PublicId, command.AddressPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileAddressPatchState.From(before);
        var applyResult = PersonnelFileAddressPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileAddressResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileAddressPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileAddressResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileAddressResponse>.Success(before);
        }

        var input = state.ToInput();

        // Optional address type validated against the AddressType reference catalog (RF-002, D-03).
        var addressTypeValidation = await PersonnelReferenceCatalogValidation.ValidateAddressTypeCodeAsync(
            repository,
            personnelFile.TenantId,
            "addressTypeCode",
            input.AddressTypeCode,
            cancellationToken);
        if (addressTypeValidation != Error.None)
        {
            return Result<PersonnelFileAddressResponse>.Failure(addressTypeValidation);
        }

        var beforeList = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateAddress(
                command.AddressPublicId,
                input.AddressLine,
                input.AddressTypeCode,
                input.Country,
                input.Department,
                input.Municipality,
                input.PostalCode,
                input.IsCurrent);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.AddressPublicId)
                ?? throw new InvalidOperationException("Personnel file address response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched address for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAddressResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

