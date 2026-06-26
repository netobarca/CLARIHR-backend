using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>Lifecycle state of an exit-interview submission (RF-011/RF-012).</summary>
public enum ExitInterviewSubmissionStatus
{
    Draft = 0,
    Submitted = 1,
    Archived = 2,
}

/// <summary>
/// A filled-in exit interview (tenant-scoped). Anonymity is at the whole-submission level (D-06): when
/// <see cref="IsAnonymous"/> the submission keeps NO link to the employee (<see cref="PersonnelFileId"/> /
/// <see cref="SubmittedByUserId"/> are null) — only de-identified analytics dimensions are retained
/// (reason/category/separation-type/plaza/period). The score is a derived 0–100 index (D-07).
/// </summary>
public sealed class ExitInterviewSubmission : TenantEntity
{
    private ExitInterviewSubmission()
    {
    }

    private ExitInterviewSubmission(
        long exitInterviewFormId,
        int formVersion,
        bool isAnonymous,
        long? personnelFileId,
        Guid? submittedByUserId,
        string retirementReasonCode,
        string? retirementCategoryCode,
        string? separationType,
        Guid? positionSlotPublicId,
        string? plazaSnapshot,
        string period)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Status = ExitInterviewSubmissionStatus.Draft;
        ExitInterviewFormId = exitInterviewFormId;
        Apply(
            formVersion,
            isAnonymous,
            personnelFileId,
            submittedByUserId,
            retirementReasonCode,
            retirementCategoryCode,
            separationType,
            positionSlotPublicId,
            plazaSnapshot,
            period);
    }

    public long ExitInterviewFormId { get; private set; }

    public ExitInterviewForm ExitInterviewForm { get; private set; } = null!;

    public int FormVersion { get; private set; }

    public bool IsAnonymous { get; private set; }

    // Null when anonymous (D-06).
    public long? PersonnelFileId { get; private set; }

    public PersonnelFile? PersonnelFile { get; private set; }

    public Guid? SubmittedByUserId { get; private set; }

    public string RetirementReasonCode { get; private set; } = string.Empty;

    public string? RetirementCategoryCode { get; private set; }

    public string? SeparationType { get; private set; }

    public Guid? PositionSlotPublicId { get; private set; }

    public string? PlazaSnapshot { get; private set; }

    public string Period { get; private set; } = string.Empty;

    public ExitInterviewSubmissionStatus Status { get; private set; }

    public DateTime? SubmittedUtc { get; private set; }

    public decimal? TotalScore { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static ExitInterviewSubmission Create(
        long exitInterviewFormId,
        int formVersion,
        bool isAnonymous,
        long? personnelFileId,
        Guid? submittedByUserId,
        string retirementReasonCode,
        string? retirementCategoryCode,
        string? separationType,
        Guid? positionSlotPublicId,
        string? plazaSnapshot,
        string period) =>
        new(
            exitInterviewFormId,
            formVersion,
            isAnonymous,
            personnelFileId,
            submittedByUserId,
            retirementReasonCode,
            retirementCategoryCode,
            separationType,
            positionSlotPublicId,
            plazaSnapshot,
            period);

    /// <summary>Refreshes the snapshot dimensions on a draft re-save (the form/profile may have changed).</summary>
    public void UpdateSnapshot(
        int formVersion,
        bool isAnonymous,
        long? personnelFileId,
        Guid? submittedByUserId,
        string retirementReasonCode,
        string? retirementCategoryCode,
        string? separationType,
        Guid? positionSlotPublicId,
        string? plazaSnapshot,
        string period)
    {
        Apply(
            formVersion,
            isAnonymous,
            personnelFileId,
            submittedByUserId,
            retirementReasonCode,
            retirementCategoryCode,
            separationType,
            positionSlotPublicId,
            plazaSnapshot,
            period);
        ConcurrencyToken = Guid.NewGuid();
    }

    public void MarkSubmitted(DateTime submittedUtc, decimal? totalScore)
    {
        Status = ExitInterviewSubmissionStatus.Submitted;
        SubmittedUtc = submittedUtc;
        TotalScore = totalScore;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void SetTotalScore(decimal? totalScore)
    {
        TotalScore = totalScore;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Archive()
    {
        Status = ExitInterviewSubmissionStatus.Archived;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void Apply(
        int formVersion,
        bool isAnonymous,
        long? personnelFileId,
        Guid? submittedByUserId,
        string retirementReasonCode,
        string? retirementCategoryCode,
        string? separationType,
        Guid? positionSlotPublicId,
        string? plazaSnapshot,
        string period)
    {
        FormVersion = formVersion;
        IsAnonymous = isAnonymous;
        // D-06: an anonymous submission keeps no link back to the employee or the submitter.
        PersonnelFileId = isAnonymous ? null : personnelFileId;
        SubmittedByUserId = isAnonymous ? null : submittedByUserId;
        RetirementReasonCode = (retirementReasonCode ?? string.Empty).Trim().ToUpperInvariant();
        RetirementCategoryCode = string.IsNullOrWhiteSpace(retirementCategoryCode) ? null : retirementCategoryCode.Trim().ToUpperInvariant();
        SeparationType = string.IsNullOrWhiteSpace(separationType) ? null : separationType.Trim().ToUpperInvariant();
        PositionSlotPublicId = positionSlotPublicId;
        PlazaSnapshot = string.IsNullOrWhiteSpace(plazaSnapshot) ? null : plazaSnapshot.Trim();
        Period = (period ?? string.Empty).Trim();
    }
}

/// <summary>
/// A single answer of an exit-interview submission. Carries a snapshot of the field definition (key,
/// title, weight) so the answer stays interpretable even if the form changes (RF-012).
/// </summary>
public sealed class ExitInterviewAnswer : TenantEntity
{
    private ExitInterviewAnswer()
    {
    }

    private ExitInterviewAnswer(
        string fieldKeySnapshot,
        string titleSnapshot,
        string controlTypeCode,
        string? valueText,
        decimal? valueNumber,
        DateTime? valueDate,
        bool? valueBool,
        string? selectedOptionCodes,
        decimal? weightSnapshot,
        decimal? normalizedScore)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        FieldKeySnapshot = (fieldKeySnapshot ?? throw new ArgumentNullException(nameof(fieldKeySnapshot))).Trim();
        TitleSnapshot = (titleSnapshot ?? string.Empty).Trim();
        ControlTypeCode = (controlTypeCode ?? string.Empty).Trim().ToUpperInvariant();
        ValueText = valueText;
        ValueNumber = valueNumber;
        ValueDate = valueDate;
        ValueBool = valueBool;
        SelectedOptionCodes = selectedOptionCodes;
        WeightSnapshot = weightSnapshot;
        NormalizedScore = normalizedScore;
    }

    public long ExitInterviewSubmissionId { get; private set; }

    public ExitInterviewSubmission ExitInterviewSubmission { get; private set; } = null!;

    public string FieldKeySnapshot { get; private set; } = string.Empty;

    public string TitleSnapshot { get; private set; } = string.Empty;

    public string ControlTypeCode { get; private set; } = string.Empty;

    public string? ValueText { get; private set; }

    public decimal? ValueNumber { get; private set; }

    public DateTime? ValueDate { get; private set; }

    public bool? ValueBool { get; private set; }

    // Comma-separated option codes for multi-select; a single code for single-select.
    public string? SelectedOptionCodes { get; private set; }

    public decimal? WeightSnapshot { get; private set; }

    public decimal? NormalizedScore { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToSubmission(long exitInterviewSubmissionId) => ExitInterviewSubmissionId = exitInterviewSubmissionId;

    public static ExitInterviewAnswer Create(
        string fieldKeySnapshot,
        string titleSnapshot,
        string controlTypeCode,
        string? valueText,
        decimal? valueNumber,
        DateTime? valueDate,
        bool? valueBool,
        string? selectedOptionCodes,
        decimal? weightSnapshot,
        decimal? normalizedScore) =>
        new(
            fieldKeySnapshot,
            titleSnapshot,
            controlTypeCode,
            valueText,
            valueNumber,
            valueDate,
            valueBool,
            selectedOptionCodes,
            weightSnapshot,
            normalizedScore);
}
