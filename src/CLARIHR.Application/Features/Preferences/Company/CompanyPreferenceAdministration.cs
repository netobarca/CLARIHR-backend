using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Preferences.Common;
using CLARIHR.Domain.Preferences;
using FluentValidation;

namespace CLARIHR.Application.Features.Preferences.Company;

public sealed record CompanyPreferenceResponse(
    Guid Id,
    string CurrencyCode,
    string TimeZone,
    string? HrFunctionalAreaCode,
    int? FileUpToDateThresholdMonths,
    int? MinimumSeniorityMonthsForEconomicAid,
    // Vacation & incapacity parametrization (D-20/D-24/D-26/D-27): null = legal default, resolved on consumption.
    int? AnnualVacationDaysDefault,
    int? AdditionalVacationBenefitDaysDefault,
    bool? AllowVacationStartOnHoliday,
    bool? AllowVacationEndOnHoliday,
    bool? AllowVacationStartOnRestDay,
    bool? DefaultUseAnniversary,
    int? CompanyRestDayOfWeek,
    int? EmployerCoveredIncapacityDaysPerYear,
    int? AdditionalIncapacityBenefitDaysPerYear,
    bool? IncapacityRequiresDocument,
    // Compensatory-time parametrization (REQ-002 P-10/P-11/P-15): null = default, resolved on consumption.
    decimal? CompensatoryTimeStandardDailyHours,
    decimal? CompensatoryTimeMaxBalanceHours,
    bool? CompensatoryTimeCreditRequiresDocument,
    decimal? CompensatoryTimeSettlementRateFactor,
    // Overtime parametrization (REQ-007 P-01/P-05): null = self-service off / no daily cap.
    bool? OvertimeSelfServiceEnabled,
    int? OvertimeMaxDailyMinutes,
    // Recurring-deduction parametrization (REQ-008 P-03): null = no default rate pre-loaded on the form.
    decimal? RecurringDeductionDefaultInterestRatePercent,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record GetCompanyPreferencesQuery(Guid CompanyId) : IQuery<CompanyPreferenceResponse>;

public sealed record UpdateCompanyPreferencesCommand(
    Guid CompanyId,
    string CurrencyCode,
    string TimeZone,
    string? HrFunctionalAreaCode,
    int? FileUpToDateThresholdMonths,
    int? MinimumSeniorityMonthsForEconomicAid,
    // Vacation & incapacity parametrization (D-20/D-24/D-26/D-27): null = legal default, resolved on consumption.
    int? AnnualVacationDaysDefault,
    int? AdditionalVacationBenefitDaysDefault,
    bool? AllowVacationStartOnHoliday,
    bool? AllowVacationEndOnHoliday,
    bool? AllowVacationStartOnRestDay,
    bool? DefaultUseAnniversary,
    int? CompanyRestDayOfWeek,
    int? EmployerCoveredIncapacityDaysPerYear,
    int? AdditionalIncapacityBenefitDaysPerYear,
    bool? IncapacityRequiresDocument,
    // Compensatory-time parametrization (REQ-002 P-10/P-11/P-15): null = default, resolved on consumption.
    decimal? CompensatoryTimeStandardDailyHours,
    decimal? CompensatoryTimeMaxBalanceHours,
    bool? CompensatoryTimeCreditRequiresDocument,
    decimal? CompensatoryTimeSettlementRateFactor,
    // Overtime parametrization (REQ-007 P-01/P-05): null = self-service off / no daily cap.
    bool? OvertimeSelfServiceEnabled,
    int? OvertimeMaxDailyMinutes,
    // Recurring-deduction parametrization (REQ-008 P-03): null = no default rate pre-loaded on the form.
    decimal? RecurringDeductionDefaultInterestRatePercent,
    Guid ConcurrencyToken) : ICommand<CompanyPreferenceResponse>;

public sealed record CompanyPreferencePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchCompanyPreferencesCommand(
    Guid CompanyId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<CompanyPreferencePatchOperation> Operations) : ICommand<CompanyPreferenceResponse>;

internal sealed class GetCompanyPreferencesQueryValidator : AbstractValidator<GetCompanyPreferencesQuery>
{
    public GetCompanyPreferencesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class UpdateCompanyPreferencesCommandValidator : AbstractValidator<UpdateCompanyPreferencesCommand>
{
    public UpdateCompanyPreferencesCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        // Trim before the exact-length check to match the domain normalizer (which trims first); a raw
        // 3-char value like " US" trims to length 2 and would otherwise throw a 500 in Update.
        RuleFor(command => command.CurrencyCode)
            .NotEmpty()
            .Must(static currencyCode => currencyCode is not null && currencyCode.Trim().Length == 3)
            .WithMessage("Currency code must be exactly 3 characters.");
        RuleFor(command => command.TimeZone)
            .NotEmpty()
            .MaximumLength(100);
        // HR analytics dashboard parametrization (optional). The marker is a FunctionalArea code; the
        // threshold (months) must be positive when provided (matches the domain SetDashboardSettings guard).
        RuleFor(command => command.HrFunctionalAreaCode)
            .MaximumLength(80);
        RuleFor(command => command.FileUpToDateThresholdMonths)
            .GreaterThan(0)
            .When(static command => command.FileUpToDateThresholdMonths.HasValue);
        // Economic-aid eligibility (D-08): minimum seniority in months must be positive when provided.
        RuleFor(command => command.MinimumSeniorityMonthsForEconomicAid)
            .GreaterThan(0)
            .When(static command => command.MinimumSeniorityMonthsForEconomicAid.HasValue);
        // Vacation & incapacity parametrization (D-20/D-24/D-26/D-27). All optional; when provided the
        // ranges mirror the domain SetLeavePolicies guards (day counts 0-365, rest day 0-6 Sunday-first).
        RuleFor(command => command.AnnualVacationDaysDefault)
            .InclusiveBetween(0, 365)
            .WithMessage("Annual vacation days must be between 0 and 365.")
            .When(static command => command.AnnualVacationDaysDefault.HasValue);
        RuleFor(command => command.AdditionalVacationBenefitDaysDefault)
            .InclusiveBetween(0, 365)
            .WithMessage("Additional vacation benefit days must be between 0 and 365.")
            .When(static command => command.AdditionalVacationBenefitDaysDefault.HasValue);
        RuleFor(command => command.CompanyRestDayOfWeek)
            .InclusiveBetween(0, 6)
            .WithMessage("Company rest day of week must be between 0 (Sunday) and 6 (Saturday).")
            .When(static command => command.CompanyRestDayOfWeek.HasValue);
        RuleFor(command => command.EmployerCoveredIncapacityDaysPerYear)
            .InclusiveBetween(0, 365)
            .WithMessage("Employer-covered incapacity days per year must be between 0 and 365.")
            .When(static command => command.EmployerCoveredIncapacityDaysPerYear.HasValue);
        RuleFor(command => command.AdditionalIncapacityBenefitDaysPerYear)
            .InclusiveBetween(0, 365)
            .WithMessage("Additional incapacity benefit days per year must be between 0 and 365.")
            .When(static command => command.AdditionalIncapacityBenefitDaysPerYear.HasValue);
        // Compensatory-time parametrization (REQ-002 P-10/P-11/P-15). All optional; when provided the
        // numeric factors mirror the domain SetCompensatoryTimePolicies guards (> 0).
        RuleFor(command => command.CompensatoryTimeStandardDailyHours)
            .GreaterThan(0m)
            .When(static command => command.CompensatoryTimeStandardDailyHours.HasValue);
        RuleFor(command => command.CompensatoryTimeMaxBalanceHours)
            .GreaterThan(0m)
            .When(static command => command.CompensatoryTimeMaxBalanceHours.HasValue);
        RuleFor(command => command.CompensatoryTimeSettlementRateFactor)
            .GreaterThan(0m)
            .When(static command => command.CompensatoryTimeSettlementRateFactor.HasValue);
        // Overtime parametrization (REQ-007 P-05): the daily-minutes cap must be positive when provided
        // (mirrors the domain SetOvertimePolicies guard).
        RuleFor(command => command.OvertimeMaxDailyMinutes)
            .GreaterThan(0)
            .When(static command => command.OvertimeMaxDailyMinutes.HasValue);
        // Recurring-deduction parametrization (REQ-008 P-03): a percentage in (0, 100] when provided
        // (mirrors the domain SetRecurringDeductionPolicies guard).
        RuleFor(command => command.RecurringDeductionDefaultInterestRatePercent)
            .GreaterThan(0m)
            .LessThanOrEqualTo(100m)
            .When(static command => command.RecurringDeductionDefaultInterestRatePercent.HasValue);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchCompanyPreferencesCommandValidator : AbstractValidator<PatchCompanyPreferencesCommand>
{
    public PatchCompanyPreferencesCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class GetCompanyPreferencesQueryHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyPreferenceRepository companyPreferenceRepository)
    : IQueryHandler<GetCompanyPreferencesQuery, CompanyPreferenceResponse>
{
    public async Task<Result<CompanyPreferenceResponse>> Handle(
        GetCompanyPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(authorizationResult.Error);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(query.CompanyId, cancellationToken);
        return preference is null
            ? Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.CompanyPreferenceNotFound)
            : Result<CompanyPreferenceResponse>.Success(CompanyPreferenceAdministrationHelpers.Map(preference));
    }
}

internal sealed class UpdateCompanyPreferencesCommandHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompanyPreferencesCommand, CompanyPreferenceResponse>
{
    public async Task<Result<CompanyPreferenceResponse>> Handle(
        UpdateCompanyPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(authorizationResult.Error);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (preference is null)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.CompanyPreferenceNotFound);
        }

        if (preference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        var before = CompanyPreferenceAdministrationHelpers.Map(preference);

        // Dashboard parametrization is set on the same tracked entity; ApplyUpdateAndAuditAsync persists it in
        // the same transaction (the shared helper still drives currency/time-zone + audit + concurrency).
        preference.SetDashboardSettings(command.HrFunctionalAreaCode, command.FileUpToDateThresholdMonths);
        preference.SetEconomicAidEligibility(command.MinimumSeniorityMonthsForEconomicAid);
        preference.SetLeavePolicies(
            command.AnnualVacationDaysDefault,
            command.AdditionalVacationBenefitDaysDefault,
            command.AllowVacationStartOnHoliday,
            command.AllowVacationEndOnHoliday,
            command.AllowVacationStartOnRestDay,
            command.DefaultUseAnniversary,
            command.CompanyRestDayOfWeek,
            command.EmployerCoveredIncapacityDaysPerYear,
            command.AdditionalIncapacityBenefitDaysPerYear,
            command.IncapacityRequiresDocument);
        preference.SetCompensatoryTimePolicies(
            command.CompensatoryTimeStandardDailyHours,
            command.CompensatoryTimeMaxBalanceHours,
            command.CompensatoryTimeCreditRequiresDocument,
            command.CompensatoryTimeSettlementRateFactor);
        preference.SetOvertimePolicies(
            command.OvertimeSelfServiceEnabled,
            command.OvertimeMaxDailyMinutes);
        preference.SetRecurringDeductionPolicies(
            command.RecurringDeductionDefaultInterestRatePercent);

        return await CompanyPreferenceAdministrationHelpers.ApplyUpdateAndAuditAsync(
            preference,
            command.CompanyId,
            command.CurrencyCode,
            command.TimeZone,
            before,
            auditService,
            unitOfWork,
            cancellationToken);
    }
}

internal sealed class PatchCompanyPreferencesCommandHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchCompanyPreferencesCommand, CompanyPreferenceResponse>
{
    public async Task<Result<CompanyPreferenceResponse>> Handle(
        PatchCompanyPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(authorizationResult.Error);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (preference is null)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.CompanyPreferenceNotFound);
        }

        if (preference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        var state = CompanyPreferencePatchState.From(preference);

        var applied = CompanyPreferencePatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(applied.Error);
        }

        var validation = CompanyPreferencePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            // No effective change → nothing to persist or audit; return the current state unchanged.
            return Result<CompanyPreferenceResponse>.Success(CompanyPreferenceAdministrationHelpers.Map(preference));
        }

        var before = CompanyPreferenceAdministrationHelpers.Map(preference);

        // Scalar-only patch: currencyCode/timeZone are the only patchable fields and are
        // re-normalized + re-validated by Update exactly as the PUT path does. The applier already
        // enforced the same length rules (currencyCode == 3, timeZone <= 100) before we get here.
        // The grouped rich-setter fields (dashboard, economic aid, leave policies) are PUT-only.
        return await CompanyPreferenceAdministrationHelpers.ApplyUpdateAndAuditAsync(
            preference,
            command.CompanyId,
            state.CurrencyCode,
            state.TimeZone,
            before,
            auditService,
            unitOfWork,
            cancellationToken);
    }
}

internal sealed class CompanyPreferencePatchState
{
    public string CurrencyCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public bool HasMutation { get; set; }

    public static CompanyPreferencePatchState From(CompanyPreference preference) =>
        new()
        {
            CurrencyCode = preference.CurrencyCode,
            TimeZone = preference.TimeZone
        };
}

internal sealed class CompanyPreferencePatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class CompanyPreferencePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<CompanyPreferencePatchOperation> operations, CompanyPreferencePatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!SupportedOperations.Contains(op))
            {
                return ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = ParsePath(operation.Path);
            if (segments.Length != 1)
            {
                return ValidationFailure(operation.Path, "Only root company preference properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (CompanyPreferencePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(CompanyPreferencePatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Validate the TRIMMED currency code: the domain normalizer (CompanyNormalization) trims before
        // its exact-length check, so a raw 3-char value that trims shorter (e.g. " US") would otherwise
        // pass here and then throw an unmapped ArgumentException in Update → HTTP 500 instead of a 400.
        if (string.IsNullOrWhiteSpace(state.CurrencyCode))
        {
            errors["currencyCode"] = ["Currency code is required."];
        }
        else if (state.CurrencyCode.Trim().Length != 3)
        {
            errors["currencyCode"] = ["Currency code must be exactly 3 characters."];
        }

        if (string.IsNullOrWhiteSpace(state.TimeZone))
        {
            errors["timeZone"] = ["Time zone is required."];
        }
        else if (state.TimeZone.Length > 100)
        {
            errors["timeZone"] = ["Time zone must be 100 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        CompanyPreferencePatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
        }

        if (IsSegment(property, "id") || IsSegment(property, "publicId") ||
            IsSegment(property, "createdAtUtc") || IsSegment(property, "modifiedAtUtc"))
        {
            return ValidationFailure(path, "This property is read-only and cannot be patched.");
        }

        if (IsSegment(property, "currencyCode"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Currency code cannot be removed.");
            }

            state.CurrencyCode = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "timeZone"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Time zone cannot be removed.");
            }

            state.TimeZone = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static string[] ParsePath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapeJsonPointerSegment)
            .ToArray();

    private static string UnescapeJsonPointerSegment(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static bool IsNull(JsonElement? value) =>
        !value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static bool IsSegment(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new CompanyPreferencePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new CompanyPreferencePatchValueException(path, "Value must be a string.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class CompanyPreferenceAdministrationHelpers
{
    public static CompanyPreferenceResponse Map(CompanyPreference preference) =>
        new(
            preference.PublicId,
            preference.CurrencyCode,
            preference.TimeZone,
            preference.HrFunctionalAreaCode,
            preference.FileUpToDateThresholdMonths,
            preference.MinimumSeniorityMonthsForEconomicAid,
            preference.AnnualVacationDaysDefault,
            preference.AdditionalVacationBenefitDaysDefault,
            preference.AllowVacationStartOnHoliday,
            preference.AllowVacationEndOnHoliday,
            preference.AllowVacationStartOnRestDay,
            preference.DefaultUseAnniversary,
            preference.CompanyRestDayOfWeek,
            preference.EmployerCoveredIncapacityDaysPerYear,
            preference.AdditionalIncapacityBenefitDaysPerYear,
            preference.IncapacityRequiresDocument,
            preference.CompensatoryTimeStandardDailyHours,
            preference.CompensatoryTimeMaxBalanceHours,
            preference.CompensatoryTimeCreditRequiresDocument,
            preference.CompensatoryTimeSettlementRateFactor,
            preference.OvertimeSelfServiceEnabled,
            preference.OvertimeMaxDailyMinutes,
            preference.RecurringDeductionDefaultInterestRatePercent,
            preference.ConcurrencyToken,
            preference.CreatedUtc,
            preference.ModifiedUtc);

    // CP-C: shared mutate-and-audit path for PUT and PATCH (both reduce to "set currency + time zone").
    // Mirrors the tenant-scoped admin handlers (CostCenters/WorkCenterTypes/LocationGroups/OrgUnits): the
    // mutation and the audit row are written in one transaction with a SaveChanges after the log entry
    // (§LG1), so EF's optimistic concurrency token still surfaces a stale write as a 409 and the audit row
    // never orphans. Uses LogForTenantAsync(companyId) because the authorization step already proved
    // companyId == the JWT tenant (so no ITenantContext dependency is needed here).
    public static async Task<Result<CompanyPreferenceResponse>> ApplyUpdateAndAuditAsync(
        CompanyPreference preference,
        Guid companyId,
        string currencyCode,
        string timeZone,
        CompanyPreferenceResponse before,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            preference.Update(currencyCode, timeZone);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = Map(preference);
            await auditService.LogForTenantAsync(
                companyId,
                new AuditLogEntry(
                    AuditEventTypes.CompanyPreferencesUpdated,
                    AuditEntityTypes.CompanyPreference,
                    preference.PublicId,
                    after.CurrencyCode,
                    AuditActions.Update,
                    $"Updated company preferences (currency {after.CurrencyCode}, time zone {after.TimeZone}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompanyPreferenceResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
