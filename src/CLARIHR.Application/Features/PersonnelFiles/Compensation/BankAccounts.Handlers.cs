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

internal sealed class GetPersonnelFileBankAccountsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileBankAccountsQuery, IReadOnlyCollection<PersonnelFileBankAccountResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileBankAccountResponse>>> Handle(
        GetPersonnelFileBankAccountsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileBankAccountResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetBankAccountsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileBankAccountResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileBankAccountByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileBankAccountByIdQuery, PersonnelFileBankAccountResponse>
{
    public async Task<Result<PersonnelFileBankAccountResponse>> Handle(
        GetPersonnelFileBankAccountByIdQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFileBankAccountResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetBankAccountAsync(query.PersonnelFileId, query.BankAccountPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileBankAccountResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileBankAccountCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IBankCatalogRepository bankCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<AddPersonnelFileBankAccountCommand, PersonnelFileBankAccountResponse>
{
    public async Task<Result<PersonnelFileBankAccountResponse>> Handle(
        AddPersonnelFileBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileBankAccountResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.BankAccounts,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;


        var companyCountryCode = await repository.GetCompanyCountryCodeAsync(personnelFile.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(companyCountryCode))
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(ErrorCatalog.NotFound);
        }

        var bankLookup = await bankCatalogRepository.GetActiveLookupByCountryAsync(
            companyCountryCode,
            command.BankAccount.BankPublicId,
            cancellationToken);
        if (bankLookup is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["bankPublicId"] =
                        [
                            "BankPublicId must reference an active bank catalog item for the company country."
                        ]
                    }));
        }

        var accountTypeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "accountTypeCode",
            PersonnelCurriculumCatalogCategories.BankAccountType,
            command.BankAccount.AccountTypeCode,
            cancellationToken);
        if (accountTypeError != Error.None)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(accountTypeError);
        }

        var before = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);
        var bankAccount = PersonnelFileBankAccount.Create(
            bankLookup.InternalId,
            bankLookup.Code,
            command.BankAccount.CurrencyCode,
            command.BankAccount.AccountNumber,
            command.BankAccount.AccountTypeCode,
            command.BankAccount.IsPrimary);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddBankAccount(bankAccount);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == bankAccount.PublicId)
                ?? throw new InvalidOperationException("Personnel file bank account response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added bank account for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileBankAccountResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileBankAccountCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IBankCatalogRepository bankCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileBankAccountCommand, PersonnelFileBankAccountResponse>
{
    public async Task<Result<PersonnelFileBankAccountResponse>> Handle(
        UpdatePersonnelFileBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileBankAccountResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.BankAccounts,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var bankAccount = personnelFile.BankAccounts.FirstOrDefault(item => item.PublicId == command.BankAccountPublicId);
        if (bankAccount is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (bankAccount.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var companyCountryCode = await repository.GetCompanyCountryCodeAsync(personnelFile.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(companyCountryCode))
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(ErrorCatalog.NotFound);
        }

        var bankLookup = await bankCatalogRepository.GetActiveLookupByCountryAsync(
            companyCountryCode,
            command.BankAccount.BankPublicId,
            cancellationToken);
        if (bankLookup is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["bankPublicId"] =
                        [
                            "BankPublicId must reference an active bank catalog item for the company country."
                        ]
                    }));
        }

        var accountTypeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "accountTypeCode",
            PersonnelCurriculumCatalogCategories.BankAccountType,
            command.BankAccount.AccountTypeCode,
            cancellationToken);
        if (accountTypeError != Error.None)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(accountTypeError);
        }

        var before = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateBankAccount(
                command.BankAccountPublicId,
                bankLookup.InternalId,
                bankLookup.Code,
                command.BankAccount.CurrencyCode,
                command.BankAccount.AccountNumber,
                command.BankAccount.AccountTypeCode,
                command.BankAccount.IsPrimary);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.BankAccountPublicId)
                ?? throw new InvalidOperationException("Personnel file bank account response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated bank account for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileBankAccountResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileBankAccountCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileBankAccountCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.BankAccounts,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var bankAccount = personnelFile.BankAccounts.FirstOrDefault(item => item.PublicId == command.BankAccountPublicId);
        if (bankAccount is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (bankAccount.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveBankAccount(command.BankAccountPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted bank account from personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
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

internal sealed class PatchPersonnelFileBankAccountCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IBankCatalogRepository bankCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileSectionCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileBankAccountCommand, PersonnelFileBankAccountResponse>
{
    public async Task<Result<PersonnelFileBankAccountResponse>> Handle(
        PatchPersonnelFileBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadForSectionManageAsync<PersonnelFileBankAccountResponse>(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.BankAccounts,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var personnelFile = file!;

        var bankAccount = personnelFile.BankAccounts.FirstOrDefault(item => item.PublicId == command.BankAccountPublicId);
        if (bankAccount is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (bankAccount.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetBankAccountAsync(personnelFile.PublicId, command.BankAccountPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileBankAccountPatchState.From(before);
        var applyResult = PersonnelFileBankAccountPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileBankAccountPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileBankAccountResponse>.Success(before);
        }

        var input = state.ToInput();

        var companyCountryCode = await repository.GetCompanyCountryCodeAsync(personnelFile.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(companyCountryCode))
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(ErrorCatalog.NotFound);
        }

        var bankLookup = await bankCatalogRepository.GetActiveLookupByCountryAsync(
            companyCountryCode,
            input.BankPublicId,
            cancellationToken);
        if (bankLookup is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["bankPublicId"] =
                        [
                            "BankPublicId must reference an active bank catalog item for the company country."
                        ]
                    }));
        }

        var accountTypeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            "accountTypeCode",
            PersonnelCurriculumCatalogCategories.BankAccountType,
            input.AccountTypeCode,
            cancellationToken);
        if (accountTypeError != Error.None)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(accountTypeError);
        }

        var beforeList = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateBankAccount(
                command.BankAccountPublicId,
                bankLookup.InternalId,
                bankLookup.Code,
                input.CurrencyCode,
                input.AccountNumber,
                input.AccountTypeCode,
                input.IsPrimary);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.BankAccountPublicId)
                ?? throw new InvalidOperationException("Personnel file bank account response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched bank account for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileBankAccountResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

