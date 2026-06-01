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

internal sealed class GetPersonnelFileFamilyMembersQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileFamilyMembersQuery, IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>> Handle(
        GetPersonnelFileFamilyMembersQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetFamilyMembersAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileFamilyMemberByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileFamilyMemberByIdQuery, PersonnelFileFamilyMemberResponse>
{
    public async Task<Result<PersonnelFileFamilyMemberResponse>> Handle(
        GetPersonnelFileFamilyMemberByIdQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFileFamilyMemberResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetFamilyMemberAsync(query.PersonnelFileId, query.FamilyMemberPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileFamilyMemberResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileFamilyMemberCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileFamilyMemberCommand, PersonnelFileFamilyMemberResponse>
{
    public async Task<Result<PersonnelFileFamilyMemberResponse>> Handle(
        AddPersonnelFileFamilyMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.FamilyMembers,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }


        var kinshipCodeValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
            repository,
            personnelFile.TenantId,
            "kinshipCode",
            command.FamilyMember.KinshipCode,
            cancellationToken);
        if (kinshipCodeValidation != Error.None)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(kinshipCodeValidation);
        }

        var before = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);

        PersonnelFileFamilyMember familyMember;
        try
        {
            familyMember = PersonnelFileFamilyMember.Create(
                command.FamilyMember.FirstName,
                command.FamilyMember.LastName,
                command.FamilyMember.KinshipCode,
                command.FamilyMember.Nationality,
                command.FamilyMember.BirthDate,
                command.FamilyMember.Sex,
                command.FamilyMember.MaritalStatus,
                command.FamilyMember.Occupation,
                command.FamilyMember.DocumentType,
                command.FamilyMember.DocumentNumber,
                command.FamilyMember.Phone,
                command.FamilyMember.IsStudying,
                command.FamilyMember.StudyPlace,
                command.FamilyMember.AcademicLevel,
                command.FamilyMember.IsBeneficiary,
                command.FamilyMember.IsWorking,
                command.FamilyMember.Workplace,
                command.FamilyMember.JobTitle,
                command.FamilyMember.WorkPhone,
                command.FamilyMember.Salary,
                command.FamilyMember.IsDeceased,
                command.FamilyMember.DeceasedDate);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.FamilyMemberRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddFamilyMember(familyMember);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == familyMember.PublicId)
                ?? throw new InvalidOperationException("Personnel file family member response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added family member for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileFamilyMemberResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileFamilyMemberCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileFamilyMemberCommand, PersonnelFileFamilyMemberResponse>
{
    public async Task<Result<PersonnelFileFamilyMemberResponse>> Handle(
        UpdatePersonnelFileFamilyMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.FamilyMembers,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var familyMember = personnelFile.FamilyMembers.FirstOrDefault(item => item.PublicId == command.FamilyMemberPublicId);
        if (familyMember is null)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (familyMember.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var kinshipCodeValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
            repository,
            personnelFile.TenantId,
            "kinshipCode",
            command.FamilyMember.KinshipCode,
            cancellationToken);
        if (kinshipCodeValidation != Error.None)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(kinshipCodeValidation);
        }

        var before = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateFamilyMember(
                command.FamilyMemberPublicId,
                command.FamilyMember.FirstName,
                command.FamilyMember.LastName,
                command.FamilyMember.KinshipCode,
                command.FamilyMember.Nationality,
                command.FamilyMember.BirthDate,
                command.FamilyMember.Sex,
                command.FamilyMember.MaritalStatus,
                command.FamilyMember.Occupation,
                command.FamilyMember.DocumentType,
                command.FamilyMember.DocumentNumber,
                command.FamilyMember.Phone,
                command.FamilyMember.IsStudying,
                command.FamilyMember.StudyPlace,
                command.FamilyMember.AcademicLevel,
                command.FamilyMember.IsBeneficiary,
                command.FamilyMember.IsWorking,
                command.FamilyMember.Workplace,
                command.FamilyMember.JobTitle,
                command.FamilyMember.WorkPhone,
                command.FamilyMember.Salary,
                command.FamilyMember.IsDeceased,
                command.FamilyMember.DeceasedDate);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.FamilyMemberPublicId)
                ?? throw new InvalidOperationException("Personnel file family member response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated family member for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileFamilyMemberResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.FamilyMemberRuleViolation);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileFamilyMemberCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileFamilyMemberCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileFamilyMemberCommand command,
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
            PersonnelFileTrackedSection.FamilyMembers,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var familyMember = personnelFile.FamilyMembers.FirstOrDefault(item => item.PublicId == command.FamilyMemberPublicId);
        if (familyMember is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (familyMember.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveFamilyMember(command.FamilyMemberPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted family member for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
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

internal sealed class PatchPersonnelFileFamilyMemberCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchPersonnelFileFamilyMemberCommand, PersonnelFileFamilyMemberResponse>
{
    public async Task<Result<PersonnelFileFamilyMemberResponse>> Handle(
        PatchPersonnelFileFamilyMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.FamilyMembers,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var familyMember = personnelFile.FamilyMembers.FirstOrDefault(item => item.PublicId == command.FamilyMemberPublicId);
        if (familyMember is null)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (familyMember.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetFamilyMemberAsync(personnelFile.PublicId, command.FamilyMemberPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileFamilyMemberPatchState.From(before);
        var applyResult = PersonnelFileFamilyMemberPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileFamilyMemberPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Success(before);
        }

        var input = state.ToInput();

        var kinshipCodeValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
            repository,
            personnelFile.TenantId,
            "kinshipCode",
            input.KinshipCode,
            cancellationToken);
        if (kinshipCodeValidation != Error.None)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(kinshipCodeValidation);
        }

        var beforeList = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateFamilyMember(
                command.FamilyMemberPublicId,
                input.FirstName,
                input.LastName,
                input.KinshipCode,
                input.Nationality,
                input.BirthDate,
                input.Sex,
                input.MaritalStatus,
                input.Occupation,
                input.DocumentType,
                input.DocumentNumber,
                input.Phone,
                input.IsStudying,
                input.StudyPlace,
                input.AcademicLevel,
                input.IsBeneficiary,
                input.IsWorking,
                input.Workplace,
                input.JobTitle,
                input.WorkPhone,
                input.Salary,
                input.IsDeceased,
                input.DeceasedDate);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.FamilyMemberPublicId)
                ?? throw new InvalidOperationException("Personnel file family member response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched family member for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileFamilyMemberResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.FamilyMemberRuleViolation);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

