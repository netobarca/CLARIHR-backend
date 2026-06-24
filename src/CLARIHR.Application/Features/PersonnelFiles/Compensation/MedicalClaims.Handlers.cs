using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Resolved snapshot values for a medical-claim write, produced after validating the insurance,
/// beneficiary, catalogs and resolving the default currency.
/// </summary>
internal sealed record MedicalClaimResolved(
    string? InsuranceName,
    string? PatientName,
    string? KinshipCode,
    string? CurrencyCode);

/// <summary>
/// Cross-aggregate validation + snapshot resolution shared by the medical-claim write handlers
/// (database-backed, so it lives outside the pure <see cref="MedicalClaimRules"/> module):
/// insurance is mandatory and must belong to the employee (D-03); the beneficiary, when the claimant is a
/// beneficiary, must belong to that insurance (D-02); the claim type and status must be active catalog codes
/// (D-04/D-10); the currency defaults from the company preference when omitted (D-05).
/// </summary>
internal static class MedicalClaimWriteSupport
{
    public static async Task<Result<MedicalClaimResolved>> ResolveAndValidateAsync(
        MedicalClaimInput input,
        PersonnelFile personnelFile,
        IPersonnelFileEmployeeRepository employeeRepository,
        IPersonnelFileRepository personnelFileRepository,
        ICompanyPreferenceRepository companyPreferenceRepository,
        CancellationToken cancellationToken)
    {
        // 1) Insurance: mandatory, must exist for THIS employee. GetInsuranceAsync already filters by
        //    personnel file, so a null result means "not found or not owned". Snapshot its name (the code).
        var insurance = await employeeRepository.GetInsuranceAsync(personnelFile.PublicId, input.InsurancePublicId, cancellationToken);
        if (insurance is null)
        {
            return Result<MedicalClaimResolved>.Failure(MedicalClaimErrors.InsuranceNotFound);
        }

        // 2) Patient: when the claimant is a beneficiary, it must belong to that insurance. Snapshot it.
        string? patientName = null;
        string? kinshipCode = null;
        if (string.Equals(input.ClaimantType?.Trim(), MedicalClaimClaimantTypes.Beneficiario, StringComparison.OrdinalIgnoreCase))
        {
            if (input.BeneficiaryPublicId is not { } beneficiaryId || beneficiaryId == Guid.Empty)
            {
                return Result<MedicalClaimResolved>.Failure(MedicalClaimErrors.BeneficiaryNotOwned);
            }

            var beneficiary = await employeeRepository.GetInsuranceBeneficiaryAsync(
                personnelFile.PublicId, input.InsurancePublicId, beneficiaryId, cancellationToken);
            if (beneficiary is null)
            {
                return Result<MedicalClaimResolved>.Failure(MedicalClaimErrors.BeneficiaryNotOwned);
            }

            patientName = beneficiary.FullName;
            kinshipCode = beneficiary.KinshipCode;
        }

        // 3) Catalogs: claim type (mandatory) and status (optional) must be active for the tenant/country.
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.MedicalClaimType, input.ClaimTypeCode, cancellationToken))
        {
            return Result<MedicalClaimResolved>.Failure(MedicalClaimErrors.TypeCodeInvalid);
        }

        if (!string.IsNullOrWhiteSpace(input.ClaimStatusCode)
            && !await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.MedicalClaimStatus, input.ClaimStatusCode!, cancellationToken))
        {
            return Result<MedicalClaimResolved>.Failure(MedicalClaimErrors.StatusCodeInvalid);
        }

        // 4) Currency: default from the company preference (by country) when omitted and an amount is present.
        var currency = input.CurrencyCode;
        if (string.IsNullOrWhiteSpace(currency) && (input.ClaimAmount.HasValue || input.PaidAmount.HasValue))
        {
            var preference = await companyPreferenceRepository.GetByTenantIdAsync(personnelFile.TenantId, cancellationToken);
            currency = preference?.CurrencyCode;
        }

        return Result<MedicalClaimResolved>.Success(new MedicalClaimResolved(insurance.InsuranceCode, patientName, kinshipCode, currency));
    }
}

internal sealed class AddPersonnelFileMedicalClaimCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileMedicalClaimCommand, PersonnelFileMedicalClaimResponse>
{
    public async Task<Result<PersonnelFileMedicalClaimResponse>> Handle(
        AddPersonnelFileMedicalClaimCommand command,
        CancellationToken cancellationToken)
    {
        // Self-service create (D-09): the dedicated manage permission OR the employee on their own file.
        var (failure, personnelFile) = await LoadForCreateOwnOrManageMedicalClaimAsync<PersonnelFileMedicalClaimResponse>(
            command.PersonnelFileId,
            tenantContext,
            authorizationService,
            currentUserService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var resolveResult = await MedicalClaimWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, employeeRepository, personnelFileRepository, companyPreferenceRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;
        var entity = PersonnelFileMedicalClaim.Create(
            command.Item.InsurancePublicId,
            resolved.InsuranceName,
            command.Item.AccountNumber,
            command.Item.ClaimantType,
            command.Item.BeneficiaryPublicId,
            resolved.PatientName,
            resolved.KinshipCode,
            command.Item.ClaimTypeCode,
            command.Item.Diagnosis,
            command.Item.ClaimAmount,
            resolved.CurrencyCode,
            command.Item.PaidAmount,
            command.Item.Notes,
            command.Item.ClaimDateUtc,
            command.Item.ResolutionDateUtc,
            command.Item.ClaimStatusCode,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddMedicalClaimAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Personnel file medical claim response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added medical claim for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileMedicalClaimResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileMedicalClaimCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileMedicalClaimCommand, PersonnelFileMedicalClaimResponse>
{
    public async Task<Result<PersonnelFileMedicalClaimResponse>> Handle(
        UpdatePersonnelFileMedicalClaimCommand command,
        CancellationToken cancellationToken)
    {
        // Edits are manager-only (D-09): dedicated manage permission, no self-service.
        var (failure, personnelFile) = await LoadForManageMedicalClaimsAsync<PersonnelFileMedicalClaimResponse>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetMedicalClaimAsync(personnelFile.PublicId, command.MedicalClaimPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var resolveResult = await MedicalClaimWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, employeeRepository, personnelFileRepository, companyPreferenceRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;

        // PUT replaces business fields only; isActive is preserved (it is mutated exclusively via PATCH).
        var response = await employeeRepository.UpdateMedicalClaimAsync(
            command.MedicalClaimPublicId,
            personnelFile.TenantId,
            command.Item with { CurrencyCode = resolved.CurrencyCode },
            resolved.InsuranceName,
            resolved.PatientName,
            resolved.KinshipCode,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated medical claim for {personnelFile.FullName}.", existing, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileMedicalClaimResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFileMedicalClaimCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileMedicalClaimCommand, PersonnelFileMedicalClaimResponse>
{
    public async Task<Result<PersonnelFileMedicalClaimResponse>> Handle(
        PatchPersonnelFileMedicalClaimCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageMedicalClaimsAsync<PersonnelFileMedicalClaimResponse>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetMedicalClaimAsync(personnelFile.PublicId, command.MedicalClaimPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFileMedicalClaimPatchState.From(existing);
        var applyResult = PersonnelFileMedicalClaimPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileMedicalClaimPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Success(existing);
        }

        var input = state.ToInput();
        var resolveResult = await MedicalClaimWriteSupport.ResolveAndValidateAsync(
            input, personnelFile, employeeRepository, personnelFileRepository, companyPreferenceRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;
        var response = await employeeRepository.PatchMedicalClaimAsync(
            command.MedicalClaimPublicId,
            personnelFile.TenantId,
            input with { CurrencyCode = resolved.CurrencyCode },
            resolved.InsuranceName,
            resolved.PatientName,
            resolved.KinshipCode,
            state.IsActive,
            state.IsActiveMutated,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched medical claim for {personnelFile.FullName}.", existing, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileMedicalClaimResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileMedicalClaimCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileMedicalClaimCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileMedicalClaimCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageMedicalClaimsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetMedicalClaimAsync(personnelFile.PublicId, command.MedicalClaimPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var removed = await employeeRepository.DeleteMedicalClaimAsync(command.MedicalClaimPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted medical claim for {personnelFile.FullName}.", existing, null, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileParentConcurrencyResult>.Success(
            new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
    }
}

internal sealed class GetPersonnelFileMedicalClaimsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileMedicalClaimsQuery, IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>> Handle(
        GetPersonnelFileMedicalClaimsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForMedicalClaimReadAsync<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            currentUserService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetMedicalClaimsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileMedicalClaimByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileMedicalClaimByIdQuery, PersonnelFileMedicalClaimResponse>
{
    public async Task<Result<PersonnelFileMedicalClaimResponse>> Handle(
        GetPersonnelFileMedicalClaimByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForMedicalClaimReadAsync<PersonnelFileMedicalClaimResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            currentUserService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetMedicalClaimAsync(personnelFile!.PublicId, query.MedicalClaimPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileMedicalClaimResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileMedicalClaimResponse>.Success(response);
    }
}

internal static class PersonnelFileMedicalClaimPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFileMedicalClaimPatchOperation> operations, PersonnelFileMedicalClaimPatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!PersonnelFileTalentPatch.SupportedOperations.Contains(op))
            {
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = PersonnelFileTalentPatch.ParsePath(operation.Path);
            if (segments.Length != 1)
            {
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root medical claim properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (PersonnelFilePatchValueException exception)
            {
                return PersonnelFileTalentPatch.ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(PersonnelFileMedicalClaimPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (state.InsurancePublicId == Guid.Empty)
        {
            errors["insurancePublicId"] = ["InsurancePublicId is required."];
        }

        if (string.IsNullOrWhiteSpace(state.ClaimTypeCode))
        {
            errors["claimTypeCode"] = ["ClaimTypeCode is required."];
        }

        if (!MedicalClaimClaimantTypes.IsValid(state.ClaimantType))
        {
            errors["claimantType"] = ["ClaimantType must be TITULAR or BENEFICIARIO."];
        }

        if (string.Equals(state.ClaimantType?.Trim(), MedicalClaimClaimantTypes.Beneficiario, StringComparison.OrdinalIgnoreCase)
            && (state.BeneficiaryPublicId is null || state.BeneficiaryPublicId == Guid.Empty))
        {
            errors["beneficiaryPublicId"] = ["BeneficiaryPublicId is required when the claimant is a beneficiary."];
        }

        if (state.ResolutionDateUtc is { } resolution && resolution < state.ClaimDateUtc)
        {
            errors["resolutionDateUtc"] = ["ResolutionDateUtc must not be before ClaimDateUtc."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileMedicalClaimPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "insurancePublicId"))
        {
            if (isRemove)
            {
                return PersonnelFileTalentPatch.ValidationFailure(path, "InsurancePublicId cannot be removed.");
            }

            var insuranceId = PersonnelFileTalentPatch.ReadNullableGuid(value, path);
            if (insuranceId is null || insuranceId == Guid.Empty)
            {
                return PersonnelFileTalentPatch.ValidationFailure(path, "InsurancePublicId is required.");
            }

            return Mutate(state, () => state.InsurancePublicId = insuranceId.Value);
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "accountNumber"))
        {
            return Mutate(state, () => state.AccountNumber = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "claimantType"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "ClaimantType cannot be removed.")
                : Mutate(state, () => state.ClaimantType = PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "beneficiaryPublicId"))
        {
            return Mutate(state, () => state.BeneficiaryPublicId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "claimTypeCode"))
        {
            return Mutate(state, () => state.ClaimTypeCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "diagnosis"))
        {
            return Mutate(state, () => state.Diagnosis = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "claimAmount"))
        {
            return Mutate(state, () => state.ClaimAmount = isRemove ? null : PersonnelFileTalentPatch.ReadNullableDecimal(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "currencyCode"))
        {
            return Mutate(state, () => state.CurrencyCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "paidAmount"))
        {
            return Mutate(state, () => state.PaidAmount = isRemove ? null : PersonnelFileTalentPatch.ReadNullableDecimal(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "notes"))
        {
            return Mutate(state, () => state.Notes = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "claimDateUtc"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "ClaimDateUtc cannot be removed.")
                : Mutate(state, () => state.ClaimDateUtc = PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "resolutionDateUtc"))
        {
            return Mutate(state, () => state.ResolutionDateUtc = isRemove ? null : PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "claimStatusCode"))
        {
            return Mutate(state, () => state.ClaimStatusCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "sourceSystem"))
        {
            return Mutate(state, () => state.SourceSystem = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "sourceReference"))
        {
            return Mutate(state, () => state.SourceReference = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "sourceSyncedUtc"))
        {
            return Mutate(state, () => state.SourceSyncedUtc = isRemove ? null : PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "isActive"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "IsActive cannot be removed.")
                : Mutate(state, () =>
                {
                    state.IsActive = PersonnelFileTalentPatch.ReadRequiredBoolean(value, path);
                    state.IsActiveMutated = true;
                });
        }

        return PersonnelFileTalentPatch.ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static Result Mutate(PersonnelFileMedicalClaimPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}
