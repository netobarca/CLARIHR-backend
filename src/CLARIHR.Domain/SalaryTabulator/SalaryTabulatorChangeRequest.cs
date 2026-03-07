using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.SalaryTabulator;

public sealed class SalaryTabulatorChangeRequest : TenantEntity
{
    private readonly List<SalaryTabulatorChangeRequestItem> _items = [];

    private SalaryTabulatorChangeRequest()
    {
    }

    private SalaryTabulatorChangeRequest(
        Guid publicId,
        string requestNumber,
        string reason,
        DateTime effectiveFromUtc,
        Guid requestedByUserId,
        IReadOnlyCollection<SalaryTabulatorChangeRequestItem> items)
    {
        if (requestedByUserId == Guid.Empty)
        {
            throw new InvalidOperationException("RequestedByUserId is required.");
        }

        if (effectiveFromUtc == default)
        {
            throw new InvalidOperationException("EffectiveFromUtc is required.");
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException("At least one change request item is required.");
        }

        PublicId = publicId;
        RequestNumber = SalaryTabulatorNormalization.Clean(requestNumber, nameof(requestNumber));
        Reason = SalaryTabulatorNormalization.Clean(reason, nameof(reason));
        EffectiveFromUtc = effectiveFromUtc;
        RequestedByUserId = requestedByUserId;
        Status = SalaryTabulatorChangeRequestStatus.Draft;
        ConcurrencyToken = Guid.NewGuid();

        _items.AddRange(items);
    }

    public Guid PublicId { get; private set; }

    public string RequestNumber { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public SalaryTabulatorChangeRequestStatus Status { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTime? SubmittedAtUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTime? DecidedAtUtc { get; private set; }

    public string? DecisionComment { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<SalaryTabulatorChangeRequestItem> Items => _items;

    public static SalaryTabulatorChangeRequest Create(
        string requestNumber,
        string reason,
        DateTime effectiveFromUtc,
        Guid requestedByUserId,
        IReadOnlyCollection<SalaryTabulatorChangeRequestItem> items) =>
        new(
            Guid.NewGuid(),
            requestNumber,
            reason,
            effectiveFromUtc,
            requestedByUserId,
            items);

    public void UpdateDraft(
        string reason,
        DateTime effectiveFromUtc,
        IReadOnlyCollection<SalaryTabulatorChangeRequestItem> items)
    {
        EnsureDraft();
        if (items.Count == 0)
        {
            throw new InvalidOperationException("At least one change request item is required.");
        }

        Reason = SalaryTabulatorNormalization.Clean(reason, nameof(reason));
        EffectiveFromUtc = effectiveFromUtc == default
            ? throw new InvalidOperationException("EffectiveFromUtc is required.")
            : effectiveFromUtc;

        _items.Clear();
        _items.AddRange(items);
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Submit(DateTime submittedAtUtc)
    {
        EnsureDraft();

        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Cannot submit an empty change request.");
        }

        Status = SalaryTabulatorChangeRequestStatus.Submitted;
        SubmittedAtUtc = submittedAtUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Approve(
        Guid decidedByUserId,
        DateTime decidedAtUtc,
        string decisionComment,
        bool allowSelfApproval)
    {
        EnsureSubmitted();

        if (decidedByUserId == Guid.Empty)
        {
            throw new InvalidOperationException("DecidedByUserId is required.");
        }

        if (!allowSelfApproval && decidedByUserId == RequestedByUserId)
        {
            throw new InvalidOperationException("Requester cannot approve their own salary tabulator request.");
        }

        DecisionComment = SalaryTabulatorNormalization.Clean(decisionComment, nameof(decisionComment));
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = decidedAtUtc;
        Status = SalaryTabulatorChangeRequestStatus.Approved;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Reject(Guid decidedByUserId, DateTime decidedAtUtc, string decisionComment)
    {
        EnsureSubmitted();

        if (decidedByUserId == Guid.Empty)
        {
            throw new InvalidOperationException("DecidedByUserId is required.");
        }

        DecisionComment = SalaryTabulatorNormalization.Clean(decisionComment, nameof(decisionComment));
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = decidedAtUtc;
        Status = SalaryTabulatorChangeRequestStatus.Rejected;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Cancel()
    {
        if (Status is SalaryTabulatorChangeRequestStatus.Approved or SalaryTabulatorChangeRequestStatus.Rejected)
        {
            throw new InvalidOperationException("Finalized change requests cannot be canceled.");
        }

        Status = SalaryTabulatorChangeRequestStatus.Canceled;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void EnsureDraft()
    {
        if (Status != SalaryTabulatorChangeRequestStatus.Draft)
        {
            throw new InvalidOperationException("Only draft change requests can be modified.");
        }
    }

    private void EnsureSubmitted()
    {
        if (Status != SalaryTabulatorChangeRequestStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted change requests can be approved or rejected.");
        }
    }
}
