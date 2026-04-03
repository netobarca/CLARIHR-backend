using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CompanySubscriptionStatusChangeRequest : AuditableEntity
{
    private CompanySubscriptionStatusChangeRequest()
    {
    }

    private CompanySubscriptionStatusChangeRequest(
        long companyId,
        long companySubscriptionId,
        SubscriptionStatus currentStatus,
        SubscriptionStatus targetStatus,
        SubscriptionStatusChangeReasonCode reasonCode,
        DateTime requestedAtUtc,
        DateTime effectiveDateUtc,
        Guid? requestedByUserPublicId,
        string? observations)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "Company id must be greater than zero.");
        }

        if (companySubscriptionId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companySubscriptionId), "Company subscription id must be greater than zero.");
        }

        if (!Enum.IsDefined(currentStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(currentStatus), "Current subscription status is invalid.");
        }

        if (!Enum.IsDefined(targetStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(targetStatus), "Target subscription status is invalid.");
        }

        if (!Enum.IsDefined(reasonCode))
        {
            throw new ArgumentOutOfRangeException(nameof(reasonCode), "Status change reason code is invalid.");
        }

        if (requestedAtUtc == default)
        {
            throw new ArgumentException("RequestedAtUtc is required.", nameof(requestedAtUtc));
        }

        if (effectiveDateUtc == default)
        {
            throw new ArgumentException("EffectiveDateUtc is required.", nameof(effectiveDateUtc));
        }

        CompanyId = companyId;
        CompanySubscriptionId = companySubscriptionId;
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
        ReasonCode = reasonCode;
        RequestedAtUtc = requestedAtUtc;
        EffectiveDateUtc = effectiveDateUtc.Date;
        RequestedByUserPublicId = NormalizeUserId(requestedByUserPublicId);
        Observations = CompanyNormalization.CleanOptional(observations);
        Status = SubscriptionStatusChangeRequestStatus.Scheduled;
    }

    public long CompanyId { get; private set; }

    public long CompanySubscriptionId { get; private set; }

    public SubscriptionStatus CurrentStatus { get; private set; }

    public SubscriptionStatus TargetStatus { get; private set; }

    public SubscriptionStatusChangeReasonCode ReasonCode { get; private set; }

    public DateTime RequestedAtUtc { get; private set; }

    public DateTime EffectiveDateUtc { get; private set; }

    public Guid? RequestedByUserPublicId { get; private set; }

    public string? Observations { get; private set; }

    public SubscriptionStatusChangeRequestStatus Status { get; private set; }

    public DateTime? AppliedAtUtc { get; private set; }

    public DateTime? RejectedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public static CompanySubscriptionStatusChangeRequest Create(
        CompanySubscription subscription,
        SubscriptionStatus targetStatus,
        SubscriptionStatusChangeReasonCode reasonCode,
        DateTime requestedAtUtc,
        DateTime effectiveDateUtc,
        Guid? requestedByUserPublicId,
        string? observations)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new CompanySubscriptionStatusChangeRequest(
            subscription.CompanyId,
            subscription.Id,
            subscription.Status,
            targetStatus,
            reasonCode,
            requestedAtUtc,
            effectiveDateUtc,
            requestedByUserPublicId,
            observations);
    }

    public void MarkApplied(DateTime appliedAtUtc)
    {
        if (Status is SubscriptionStatusChangeRequestStatus.Applied or SubscriptionStatusChangeRequestStatus.Rejected)
        {
            throw new InvalidOperationException("Applied or rejected status change requests cannot be applied again.");
        }

        Status = SubscriptionStatusChangeRequestStatus.Applied;
        AppliedAtUtc = appliedAtUtc;
        RejectedAtUtc = null;
        RejectionReason = null;
    }

    public void Reject(DateTime rejectedAtUtc, string rejectionReason)
    {
        if (Status != SubscriptionStatusChangeRequestStatus.Scheduled)
        {
            throw new InvalidOperationException("Only scheduled status change requests can be rejected.");
        }

        if (rejectedAtUtc == default)
        {
            throw new ArgumentException("RejectedAtUtc is required.", nameof(rejectedAtUtc));
        }

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Rejection reason is required.", nameof(rejectionReason));
        }

        Status = SubscriptionStatusChangeRequestStatus.Rejected;
        RejectedAtUtc = rejectedAtUtc;
        RejectionReason = CompanyNormalization.Clean(rejectionReason, nameof(rejectionReason));
    }

    private static Guid? NormalizeUserId(Guid? userPublicId) =>
        userPublicId.HasValue && userPublicId.Value == Guid.Empty ? null : userPublicId;
}
