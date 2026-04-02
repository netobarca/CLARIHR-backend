using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CompanySubscriptionStatusTransition : AuditableEntity
{
    private CompanySubscriptionStatusTransition()
    {
    }

    private CompanySubscriptionStatusTransition(
        SubscriptionStatus? previousStatus,
        SubscriptionStatus newStatus,
        SubscriptionStatusChangeReasonCode reasonCode,
        string? observations,
        DateTime changedAtUtc,
        SubscriptionStatusChangeOrigin origin,
        Guid? actorUserPublicId)
    {
        if (changedAtUtc == default)
        {
            throw new ArgumentException("ChangedAtUtc is required.", nameof(changedAtUtc));
        }

        if (actorUserPublicId.HasValue && actorUserPublicId.Value == Guid.Empty)
        {
            throw new ArgumentException("Actor user public id cannot be empty.", nameof(actorUserPublicId));
        }

        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ReasonCode = reasonCode;
        Observations = CompanyNormalization.CleanOptional(observations);
        ChangedAtUtc = changedAtUtc;
        Origin = origin;
        ActorUserPublicId = actorUserPublicId;
    }

    public long CompanySubscriptionId { get; private set; }

    public CompanySubscription CompanySubscription { get; private set; } = null!;

    public SubscriptionStatus? PreviousStatus { get; private set; }

    public SubscriptionStatus NewStatus { get; private set; }

    public SubscriptionStatusChangeReasonCode ReasonCode { get; private set; }

    public string? Observations { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    public SubscriptionStatusChangeOrigin Origin { get; private set; }

    public Guid? ActorUserPublicId { get; private set; }

    public static CompanySubscriptionStatusTransition Create(
        SubscriptionStatus? previousStatus,
        SubscriptionStatus newStatus,
        SubscriptionStatusChangeReasonCode reasonCode,
        string? observations,
        DateTime changedAtUtc,
        SubscriptionStatusChangeOrigin origin,
        Guid? actorUserPublicId) =>
        new(
            previousStatus,
            newStatus,
            reasonCode,
            observations,
            changedAtUtc,
            origin,
            actorUserPublicId);

    public void BindToSubscription(long companySubscriptionId) => CompanySubscriptionId = companySubscriptionId;
}
