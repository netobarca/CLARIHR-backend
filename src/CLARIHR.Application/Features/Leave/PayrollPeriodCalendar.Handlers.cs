using System.Globalization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.Features.Leave;

/// <summary>
/// Annual calendar mass-generation of a Nómina's payroll periods (REQ-012 §3.2, molde
/// <c>vacation-periods/generate</c>). Derives the ranges for the year by the Nómina's pay frequency —
/// quincenas 1-15/16-fin · ISO weeks (Mon-Sun) · calendar months —, caps at the natural calendar
/// capacity (a <c>TotalPeriods</c> beyond it, e.g. a 13th monthly run, is reported as not derivable and
/// created by hand), materializes the entry windows from the Nómina rule (P-18: window =
/// [start, end + offset], editable afterwards) and is idempotent by (definition, year, number).
/// </summary>
internal sealed class GeneratePayrollPeriodCalendarCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IPayrollDefinitionRepository payrollDefinitionRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<GeneratePayrollPeriodCalendarCommand, PayrollPeriodCalendarGenerationSummary>
{
    public async Task<Result<PayrollPeriodCalendarGenerationSummary>> Handle(
        GeneratePayrollPeriodCalendarCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodCalendarGenerationSummary>.Failure(authorizationResult.Error);
        }

        var definition = await payrollDefinitionRepository.GetByIdAsync(command.PayrollDefinitionPublicId, cancellationToken);
        if (definition is null || definition.TenantId != command.CompanyId || !definition.IsActive)
        {
            return Result<PayrollPeriodCalendarGenerationSummary>.Failure(PayrollPeriodErrors.DefinitionRequired);
        }

        var ranges = DeriveRanges(definition.PayPeriodCode, command.Year);
        if (ranges is null)
        {
            // UNICA (or any frequency without a fixed cadence) has no derivable annual calendar.
            return Result<PayrollPeriodCalendarGenerationSummary>.Failure(PayrollPeriodErrors.ScheduleInvalid);
        }

        // «{NOMINA}-{YYYY}-{NN}» must fit the 80-char code column.
        if (definition.Code.Length + 8 > PayrollPeriodDefinition.MaxCodeLength)
        {
            return Result<PayrollPeriodCalendarGenerationSummary>.Failure(PayrollPeriodErrors.ScheduleInvalid);
        }

        var toDerive = Math.Min(definition.TotalPeriods, ranges.Count);
        var notDerivable = Math.Max(0, definition.TotalPeriods - ranges.Count);
        var derived = ranges.Take(toDerive).ToArray();

        // The Nómina window rule must produce a coherent window on every derived range (a very negative
        // offset that closes the window before the period starts is a configuration error, not a row skip).
        var overtimeOffset = definition.OvertimeWindowEnabled ? definition.OvertimeWindowOffsetDays ?? 0 : 0;
        var attendanceOffset = definition.AttendanceWindowEnabled ? definition.AttendanceWindowOffsetDays ?? 0 : 0;
        foreach (var range in derived)
        {
            if (definition.OvertimeWindowEnabled && range.End.AddDays(overtimeOffset) < range.Start)
            {
                return Result<PayrollPeriodCalendarGenerationSummary>.Failure(PayrollPeriodErrors.ScheduleInvalid);
            }

            if (definition.AttendanceWindowEnabled && range.End.AddDays(attendanceOffset) < range.Start)
            {
                return Result<PayrollPeriodCalendarGenerationSummary>.Failure(PayrollPeriodErrors.ScheduleInvalid);
            }
        }

        var existingNumbers = (await repository.GetExistingNumbersForDefinitionAsync(
            command.CompanyId, definition.Id, command.Year, cancellationToken)).ToHashSet();

        var created = 0;
        var skipped = 0;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var range in derived)
            {
                if (existingNumbers.Contains(range.Number))
                {
                    skipped++;
                    continue;
                }

                var code = string.Create(
                    CultureInfo.InvariantCulture, $"{definition.Code}-{command.Year}-{range.Number:00}");
                var period = PayrollPeriodDefinition.Create(
                    definition.PayPeriodCode,
                    command.Year,
                    range.Number,
                    code,
                    range.Start,
                    range.End);
                period.SetTenantId(command.CompanyId);
                period.AssignDefinition(definition.Id);
                period.SetCode(code);
                // Cutoff/payment default to the period end (editable — P-04 soft month = end's month).
                period.SetSchedule(range.End, range.End, range.End.Month);
                period.SetWindows(
                    definition.OvertimeWindowEnabled,
                    definition.OvertimeWindowEnabled ? range.Start : null,
                    definition.OvertimeWindowEnabled ? range.End.AddDays(overtimeOffset) : null,
                    definition.AttendanceWindowEnabled,
                    definition.AttendanceWindowEnabled ? range.Start : null,
                    definition.AttendanceWindowEnabled ? range.End.AddDays(attendanceOffset) : null);
                repository.Add(period);
                created++;
            }

            if (created > 0)
            {
                _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var summary = new PayrollPeriodCalendarGenerationSummary(
                command.Year,
                definition.TotalPeriods,
                created,
                skipped,
                notDerivable);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollPeriodCalendarGenerated,
                    AuditEntityTypes.PayrollPeriodDefinition,
                    definition.PublicId,
                    definition.Code,
                    AuditActions.Create,
                    $"Generated payroll period calendar {command.Year} for {definition.Code}: {created} created, {skipped} skipped.",
                    After: summary),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollPeriodCalendarGenerationSummary>.Success(summary);
        }
        catch (UniqueConstraintViolationException ex) when (string.Equals(
            ex.ConstraintName, LeaveMasterConstraintNames.PayrollPeriodDefinitionScopedUnique, StringComparison.Ordinal))
        {
            // Two concurrent generates of the same calendar: the second one trips the per-Nómina unique
            // index — a clean 409 (a re-run then reports everything as skipped).
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollPeriodCalendarGenerationSummary>.Failure(PayrollPeriodErrors.PeriodConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed record PeriodRange(int Number, DateOnly Start, DateOnly End);

    private static IReadOnlyList<PeriodRange>? DeriveRanges(string payPeriodCode, int year)
    {
        switch (payPeriodCode)
        {
            case PayrollFrequencies.Mensual:
            {
                var ranges = new List<PeriodRange>(12);
                for (var month = 1; month <= 12; month++)
                {
                    ranges.Add(new PeriodRange(
                        month,
                        new DateOnly(year, month, 1),
                        new DateOnly(year, month, DateTime.DaysInMonth(year, month))));
                }

                return ranges;
            }

            case PayrollFrequencies.Quincenal:
            {
                var ranges = new List<PeriodRange>(24);
                for (var number = 1; number <= 24; number++)
                {
                    var month = (number + 1) / 2;
                    var isFirstHalf = number % 2 == 1;
                    ranges.Add(new PeriodRange(
                        number,
                        new DateOnly(year, month, isFirstHalf ? 1 : 16),
                        isFirstHalf
                            ? new DateOnly(year, month, 15)
                            : new DateOnly(year, month, DateTime.DaysInMonth(year, month))));
                }

                return ranges;
            }

            case PayrollFrequencies.Semanal:
            {
                var weeks = ISOWeek.GetWeeksInYear(year);
                var ranges = new List<PeriodRange>(weeks);
                for (var week = 1; week <= weeks; week++)
                {
                    var monday = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
                    ranges.Add(new PeriodRange(week, monday, monday.AddDays(6)));
                }

                return ranges;
            }

            default:
                return null;
        }
    }
}
