using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// Company seniority (antigüedad) is computed on read from HireDate (and RetirementDate when retired),
// never stored — see EmployeeSeniority.Between. The vacation/disability balances are read-only here and
// owned by the future vacations/incapacities module (null until that module computes them).
public sealed record PersonnelFileEmployeeProfileResponse(
    Guid Id,
    string EmployeeCode,
    string EmploymentStatusCode,
    string? InstitutionalEmail,
    DateTime HireDate,
    // Applicable minimum monthly wage (settlement module RF-011, ratified: it lives on the employee
    // "ficha" and feeds the legal caps of the liquidación). Null = not configured yet.
    decimal? MinimumMonthlyWage,
    EmployeeSeniority Seniority,
    string? RetirementCategoryCode,
    string? RetirementReasonCode,
    string? RetirementNotes,
    DateTime? RetirementDate,
    decimal? VacationDaysAvailable,
    decimal? DisabilityDaysAvailable,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record EmployeeSeniority(int Years, int Months, int Days, int TotalDays)
{
    public static readonly EmployeeSeniority None = new(0, 0, 0, 0);

    /// <summary>Calendar seniority between <paramref name="startUtc"/> and <paramref name="asOfUtc"/> (years/months/days + total days).</summary>
    public static EmployeeSeniority Between(DateTime startUtc, DateTime asOfUtc)
    {
        var start = startUtc.Date;
        var end = asOfUtc.Date;
        if (end <= start)
        {
            return None;
        }

        var years = end.Year - start.Year;
        var months = end.Month - start.Month;
        var days = end.Day - start.Day;

        if (days < 0)
        {
            var priorMonth = end.AddMonths(-1);
            days += DateTime.DaysInMonth(priorMonth.Year, priorMonth.Month);
            months--;
        }

        if (months < 0)
        {
            months += 12;
            years--;
        }

        return new EmployeeSeniority(years, months, days, (int)(end - start).TotalDays);
    }
}

// D-01 of the retirement module (ratified, no fallbacks): the retirement metadata (category/reason/notes/
// date) left this contract — the ONLY door for the baja is the retirement-request execution. The GET/response
// keeps exposing the fields.
public sealed record UpdatePersonnelFileEmployeeProfileCommand(
    Guid PersonnelFileId,
    string EmployeeCode,
    string EmploymentStatusCode,
    DateTime HireDate,
    Guid ConcurrencyToken,
    // The institutional email is the employee's login account identifier. When supplied and changed, the
    // handler re-syncs the linked account so sign-in stays consistent (full edit). Null leaves it unchanged.
    string? InstitutionalEmail = null,
    // Applicable minimum monthly wage (settlement module RF-011): part of the upsert like every other
    // employment field; null clears/leaves it unset.
    decimal? MinimumMonthlyWage = null)
    : ICommand<PersonnelFileEmployeeProfileResponse>;

public sealed record GetPersonnelFileEmployeeProfileQuery(Guid PersonnelFileId)
    : IQuery<PersonnelFileEmployeeProfileResponse?>;

internal static class EmploymentStatusErrors
{
    public static readonly Error StatusCodeInvalid = new(
        "EMPLOYMENT_STATUS_CODE_INVALID",
        "The employment status code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    // D-01 (retirement module, ratified) + pre-development clarification #1: the RETIRADO status and every
    // profile already retired are reserved to the retirement module (execution/reversal) and the rehire.
    public static readonly Error RetiradoReserved = new(
        "EMPLOYMENT_STATUS_RETIRADO_RESERVED",
        "The employment information of a retired employee is managed by the retirement module (reversal or rehire); the RETIRADO status is reserved to its execution.",
        ErrorType.UnprocessableEntity);
}

internal static class EmployeeProfileResponseEnricher
{
    // Derived fields (institutional email from the parent file, computed seniority) are filled in the
    // application layer — the repository projects only persisted columns.
    public static PersonnelFileEmployeeProfileResponse Enrich(
        PersonnelFileEmployeeProfileResponse response,
        Domain.PersonnelFiles.PersonnelFile personnelFile,
        IDateTimeProvider dateTimeProvider) =>
        response with
        {
            InstitutionalEmail = personnelFile.InstitutionalEmail,
            Seniority = EmployeeSeniority.Between(response.HireDate, response.RetirementDate ?? dateTimeProvider.UtcNow)
        };
}

internal sealed class UpdatePersonnelFileEmployeeProfileCommandValidator : AbstractValidator<UpdatePersonnelFileEmployeeProfileCommand>
{
    public UpdatePersonnelFileEmployeeProfileCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmployeeCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.EmploymentStatusCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.InstitutionalEmail)
            .MaximumLength(256)
            .EmailAddress()
            .When(command => !string.IsNullOrWhiteSpace(command.InstitutionalEmail));
        RuleFor(command => command.MinimumMonthlyWage)
            .GreaterThan(0)
            .When(command => command.MinimumMonthlyWage.HasValue);
    }
}

internal sealed class GetPersonnelFileEmployeeProfileQueryValidator : AbstractValidator<GetPersonnelFileEmployeeProfileQuery>
{
    public GetPersonnelFileEmployeeProfileQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class UpdatePersonnelFileEmployeeProfileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IUserRepository userRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileEmployeeProfileCommand, PersonnelFileEmployeeProfileResponse>
{
    public async Task<Result<PersonnelFileEmployeeProfileResponse>> Handle(
        UpdatePersonnelFileEmployeeProfileCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileEmployeeProfileResponse>(
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
            return Result<PersonnelFileEmployeeProfileResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        // employee-profile is a 1:1 upsert: enforce optimistic concurrency only when a profile already exists.
        var existing = await employeeRepository.GetEmployeeProfileAsync(personnelFile.PublicId, cancellationToken);
        if (existing is not null && existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmployeeProfileResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // D-01 (single door, both directions — pre-development clarification #1): a retired profile is only
        // touched by the retirement module's reversal or by a rehire. Editing it here could silently
        // "un-retire" the employee and corrupt the executed request + its reversal snapshot.
        if (existing?.RetirementDate is not null)
        {
            return Result<PersonnelFileEmployeeProfileResponse>.Failure(EmploymentStatusErrors.RetiradoReserved);
        }

        // EmploymentStatusCode is now a validated catalog code (replaces the former IsEmploymentActive flag).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
            personnelFile.TenantId, PersonnelCurriculumCatalogCategories.EmploymentStatus, command.EmploymentStatusCode, cancellationToken))
        {
            return Result<PersonnelFileEmployeeProfileResponse>.Failure(EmploymentStatusErrors.StatusCodeInvalid);
        }

        // D-01: RETIRADO is reserved to the retirement-module execution (ApplyRetirement).
        if (string.Equals(
                command.EmploymentStatusCode.Trim(),
                PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<PersonnelFileEmployeeProfileResponse>.Failure(EmploymentStatusErrors.RetiradoReserved);
        }

        // Institutional-email edit (record + login): the institutional email is the employee's account
        // identifier, so a change is applied to BOTH the file and the linked sign-in account to keep them
        // consistent. Only act when a new value is supplied that actually differs; omitting it (null) leaves
        // the current email untouched — it cannot be cleared while a login account is linked.
        var requestedInstitutionalEmail = command.InstitutionalEmail?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedInstitutionalEmail) &&
            !string.Equals(requestedInstitutionalEmail, personnelFile.InstitutionalEmail, StringComparison.OrdinalIgnoreCase))
        {
            // Uniqueness: no other account may already own this email (mirrors the finalize/rehire guard;
            // the unique index on the normalized email is the backstop against a concurrent claim).
            var conflictingUser = await userRepository.GetByEmailAsync(requestedInstitutionalEmail, cancellationToken);
            if (conflictingUser is not null && conflictingUser.PublicId != personnelFile.LinkedUserPublicId)
            {
                return Result<PersonnelFileEmployeeProfileResponse>.Failure(PersonnelFileErrors.LinkedUserConflict);
            }

            personnelFile.ChangeInstitutionalEmail(requestedInstitutionalEmail);

            if (personnelFile.LinkedUserPublicId is { } linkedUserPublicId)
            {
                var linkedUser = await userRepository.GetByPublicIdAsync(linkedUserPublicId, cancellationToken);
                linkedUser?.ChangeEmail(requestedInstitutionalEmail);
            }
        }

        var entity = PersonnelFileEmployeeProfile.Create(
            command.EmployeeCode,
            command.EmploymentStatusCode,
            command.HireDate,
            command.MinimumMonthlyWage);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = EmployeeProfileResponseEnricher.Enrich(
            await employeeRepository.UpsertEmployeeProfileAsync(entity, cancellationToken),
            personnelFile,
            dateTimeProvider);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService,
                personnelFile,
                $"Updated employee profile for {personnelFile.FullName}.",
                response,
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEmployeeProfileResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileEmployeeProfileQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmployeeProfileQuery, PersonnelFileEmployeeProfileResponse?>
{
    public async Task<Result<PersonnelFileEmployeeProfileResponse?>> Handle(
        GetPersonnelFileEmployeeProfileQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<PersonnelFileEmployeeProfileResponse?>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetEmployeeProfileAsync(personnelFile!.PublicId, cancellationToken);
        var enriched = response is null
            ? null
            : EmployeeProfileResponseEnricher.Enrich(response, personnelFile, dateTimeProvider);

        // employee-profile is a 1:1 section created lazily on the first PUT upsert (see
        // UpdatePersonnelFileEmployeeProfileCommandHandler). "Not created yet" is a normal state, not an
        // error: return 200 with a null body so the client treats it as "empty section". This mirrors the
        // sibling employee sub-resources (assigned-positions, contract-history, assets-accesses, ...),
        // whose list endpoints return 200 with an empty array when there is no data yet.
        return Result<PersonnelFileEmployeeProfileResponse?>.Success(enriched);
    }
}

