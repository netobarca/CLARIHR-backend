using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>Resolved capabilities of a form control type, used to validate field coherence (RF-007).</summary>
public sealed record ControlTypeCapability(
    string Code,
    bool SupportsOptions,
    bool SupportsRange,
    bool SupportsMultiple);

/// <summary>A persisted field reduced to what publish-time validation needs (RF-007/008).</summary>
public sealed record PublishCandidateField(
    string ControlTypeCode,
    decimal? MinValue,
    decimal? MaxValue,
    int ActiveOptionCount);

/// <summary>De-identified analytics dimensions captured on a submission from the employee's baja (RQ-02).</summary>
public sealed record ExitInterviewSubmissionSnapshot(
    string? RetirementReasonCode,
    string? RetirementCategoryCode,
    string? SeparationType,
    Guid? PositionSlotPublicId,
    DateTime? RetirementDate);

/// <summary>
/// Dedicated repository for the exit-interview module (D-01). Kept separate from the large personnel-file
/// repositories: exit-interview forms are tenant-scoped definitions, not personnel-file children.
/// </summary>
public interface IExitInterviewRepository
{
    Task<bool> FormNameExistsAsync(Guid tenantId, string normalizedName, Guid? excludingFormPublicId, CancellationToken cancellationToken);

    void AddForm(ExitInterviewForm form);

    void AddGroup(ExitInterviewFormGroup group);

    void AddField(ExitInterviewFormField field);

    void AddOption(ExitInterviewFormFieldOption option);

    /// <summary>Loads the tracked form aggregate root (for state/concurrency checks and header edits).</summary>
    Task<ExitInterviewForm?> GetFormEntityAsync(Guid tenantId, Guid formPublicId, CancellationToken cancellationToken);

    /// <summary>Hard-deletes the form's groups/fields/options (a Draft definition is replaced wholesale on save).</summary>
    Task RemoveDefinitionChildrenAsync(Guid tenantId, long formId, CancellationToken cancellationToken);

    /// <summary>Reads the full renderable form definition (header + groups + fields + options).</summary>
    Task<ExitInterviewFormResponse?> GetFormResponseAsync(Guid tenantId, Guid formPublicId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExitInterviewFormListItemResponse>> ListFormsAsync(
        Guid tenantId,
        ExitInterviewFormStatus? status,
        string? reasonCode,
        string? search,
        CancellationToken cancellationToken);

    /// <summary>Active form-control-type capabilities for the tenant's country, keyed by normalized code.</summary>
    Task<IReadOnlyDictionary<string, ControlTypeCapability>> GetControlTypeCapabilitiesAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>The form's fields reduced to publish-validation data (control type, range, active option count).</summary>
    Task<IReadOnlyCollection<PublishCandidateField>> GetPublishCandidateFieldsAsync(
        Guid tenantId,
        long formId,
        CancellationToken cancellationToken);

    // ---- Association to a retirement reason (single-active) — RF-009 (PR-4) ----

    /// <summary>The currently-active published form for a reason, if any (excluding the given form).</summary>
    Task<ExitInterviewForm?> GetActiveFormForReasonAsync(
        Guid tenantId,
        string retirementReasonCode,
        Guid? excludingFormPublicId,
        CancellationToken cancellationToken);

    // ---- Submissions (RF-011/RF-012, PR-5) ----

    void AddSubmission(ExitInterviewSubmission submission);

    void AddAnswer(ExitInterviewAnswer answer);

    Task RemoveAnswersAsync(Guid tenantId, long submissionId, CancellationToken cancellationToken);

    /// <summary>The employee's current non-archived submission (one active per file+baja, RQ-06); tracked.</summary>
    Task<ExitInterviewSubmission?> GetActiveSubmissionForFileAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken);

    Task<ExitInterviewSubmission?> GetSubmissionEntityAsync(Guid tenantId, Guid submissionPublicId, CancellationToken cancellationToken);

    Task<ExitInterviewSubmissionResponse?> GetSubmissionResponseAsync(Guid tenantId, Guid submissionPublicId, CancellationToken cancellationToken);

    Task<ExitInterviewSubmissionResponse?> GetSubmissionResponseForFileAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>> ListSubmissionsAsync(
        Guid tenantId,
        string? reasonCode,
        string? period,
        CancellationToken cancellationToken);

    /// <summary>The de-identified snapshot dimensions for a file's baja (reason/category/type/plaza/date).</summary>
    Task<ExitInterviewSubmissionSnapshot> GetSubmissionSnapshotAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken);

    /// <summary>Archives the file's non-archived submissions (rehire — D-12). Returns the count archived.</summary>
    Task<int> ArchiveSubmissionsForFileAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken);
}
