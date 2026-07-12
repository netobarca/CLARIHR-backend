using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// One recorded stretch of time the employee did not work (REQ-011): an absence, a suspension with discount, a late
/// arrival. There is <b>no decision step</b> (P-16) — the absence already happened, so the record is born
/// <c>REGISTRADO</c> and the only thing that can happen to it is being annulled.
///
/// <para>Everything the calculation depended on is SNAPSHOT here: the type's code, its flags, its percent, and the
/// computed figures. The master is editable, and a type that changes tomorrow must not silently rewrite what was
/// discounted from a payroll that already ran.</para>
/// </summary>
public sealed class PersonnelFileNotWorkedTime : TenantEntity
{
    public const int MaxStatusCodeLength = 20;
    public const int MaxTypeCodeLength = 50;
    public const int MaxTypeNameLength = 150;
    public const int MaxConceptCodeLength = 80;
    public const int MaxOriginCodeLength = 20;
    public const int MaxReasonLength = 500;
    public const int MaxAnnulmentReasonLength = 500;

    private PersonnelFileNotWorkedTime()
    {
    }

    private PersonnelFileNotWorkedTime(
        Guid assignedPositionPublicId,
        string typeCodeSnapshot,
        string typeNameSnapshot,
        bool usesWorkSchedule,
        bool countsHoliday,
        bool countsSaturday,
        bool countsRestDay,
        bool countsSeventhDayPenalty,
        decimal discountPercentSnapshot,
        string? deductionConceptTypeCodeSnapshot,
        string? incomeConceptTypeCodeSnapshot,
        DateOnly startDate,
        DateOnly endDate,
        decimal? hours,
        string? reason,
        string originCode,
        int calendarDays,
        int computableDays,
        int seventhDayPenaltyDays,
        decimal discountedDays,
        decimal dailySalarySnapshot,
        decimal discountAmount,
        string currencyCode,
        string? detailJson,
        Guid registeredByUserId,
        DateTime registeredUtc)
    {
        PublicId = Guid.NewGuid();
        AssignedPositionPublicId = assignedPositionPublicId;
        TypeCodeSnapshot = typeCodeSnapshot;
        TypeNameSnapshot = typeNameSnapshot;
        UsesWorkSchedule = usesWorkSchedule;
        CountsHoliday = countsHoliday;
        CountsSaturday = countsSaturday;
        CountsRestDay = countsRestDay;
        CountsSeventhDayPenalty = countsSeventhDayPenalty;
        DiscountPercentSnapshot = discountPercentSnapshot;
        DeductionConceptTypeCodeSnapshot = deductionConceptTypeCodeSnapshot;
        IncomeConceptTypeCodeSnapshot = incomeConceptTypeCodeSnapshot;
        StartDate = startDate;
        EndDate = endDate;
        Hours = hours;
        Reason = reason;
        OriginCode = originCode;
        CalendarDays = calendarDays;
        ComputableDays = computableDays;
        SeventhDayPenaltyDays = seventhDayPenaltyDays;
        DiscountedDays = discountedDays;
        DailySalarySnapshot = dailySalarySnapshot;
        DiscountAmount = discountAmount;
        CurrencyCode = currencyCode;
        DetailJson = detailJson;
        RegisteredByUserId = registeredByUserId;
        RegisteredUtc = registeredUtc;
        StatusCode = "REGISTRADO";
        ConcurrencyToken = Guid.NewGuid();
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile? PersonnelFile { get; private set; }

    public Guid AssignedPositionPublicId { get; private set; }

    // ── Snapshot of the type at the moment of the record ──────────────────────────────────────────────
    public string TypeCodeSnapshot { get; private set; } = string.Empty;

    public string TypeNameSnapshot { get; private set; } = string.Empty;

    public bool UsesWorkSchedule { get; private set; }

    public bool CountsHoliday { get; private set; }

    public bool CountsSaturday { get; private set; }

    public bool CountsRestDay { get; private set; }

    public bool CountsSeventhDayPenalty { get; private set; }

    public decimal DiscountPercentSnapshot { get; private set; }

    public string? DeductionConceptTypeCodeSnapshot { get; private set; }

    public string? IncomeConceptTypeCodeSnapshot { get; private set; }

    // ── What was captured ─────────────────────────────────────────────────────────────────────────────
    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    /// <summary>Only for the types captured in hours (a late arrival).</summary>
    public decimal? Hours { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>MANUAL today. The biometric module (P-21) will write MARCACION — the seam exists, nothing reads it.</summary>
    public string OriginCode { get; private set; } = string.Empty;

    // ── What the engine computed (the amount is NEVER typed by the user) ──────────────────────────────
    public int CalendarDays { get; private set; }

    public int ComputableDays { get; private set; }

    public int SeventhDayPenaltyDays { get; private set; }

    public decimal DiscountedDays { get; private set; }

    public decimal DailySalarySnapshot { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    /// <summary>The day-by-day trail: WHY six days were discounted for a five-day absence.</summary>
    public string? DetailJson { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────────────────────────────
    public string StatusCode { get; private set; } = "REGISTRADO";

    public Guid RegisteredByUserId { get; private set; }

    public DateTime RegisteredUtc { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public bool IsAnnulled => StatusCode == "ANULADO";

    public static PersonnelFileNotWorkedTime Create(
        Guid assignedPositionPublicId,
        string typeCodeSnapshot,
        string typeNameSnapshot,
        bool usesWorkSchedule,
        bool countsHoliday,
        bool countsSaturday,
        bool countsRestDay,
        bool countsSeventhDayPenalty,
        decimal discountPercentSnapshot,
        string? deductionConceptTypeCodeSnapshot,
        string? incomeConceptTypeCodeSnapshot,
        DateOnly startDate,
        DateOnly endDate,
        decimal? hours,
        string? reason,
        string originCode,
        int calendarDays,
        int computableDays,
        int seventhDayPenaltyDays,
        decimal discountedDays,
        decimal dailySalarySnapshot,
        decimal discountAmount,
        string currencyCode,
        string? detailJson,
        Guid registeredByUserId,
        DateTime registeredUtc)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot be earlier than the start date.", nameof(endDate));
        }

        return new PersonnelFileNotWorkedTime(
            assignedPositionPublicId,
            typeCodeSnapshot,
            typeNameSnapshot,
            usesWorkSchedule,
            countsHoliday,
            countsSaturday,
            countsRestDay,
            countsSeventhDayPenalty,
            discountPercentSnapshot,
            deductionConceptTypeCodeSnapshot,
            incomeConceptTypeCodeSnapshot,
            startDate,
            endDate,
            hours,
            reason,
            originCode,
            calendarDays,
            computableDays,
            seventhDayPenaltyDays,
            discountedDays,
            dailySalarySnapshot,
            discountAmount,
            currencyCode,
            detailJson,
            registeredByUserId,
            registeredUtc);
    }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>The only transition there is. Annulling twice is a no-op guarded by the caller (422).</summary>
    public void Annul(string? reason, Guid byUserId, DateTime atUtc)
    {
        if (IsAnnulled)
        {
            throw new InvalidOperationException("The not-worked-time record is already annulled.");
        }

        StatusCode = "ANULADO";
        AnnulmentReason = reason;
        AnnulledByUserId = byUserId;
        AnnulledUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }
}
