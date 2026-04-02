using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;
using System.Reflection;

namespace CLARIHR.Application.UnitTests;

public sealed class CompanySubscriptionStateManagementTests
{
    [Fact]
    public void Suspend_ThenReactivate_ShouldTrackTransitionsAndRestoreActive()
    {
        var plan = CreatePlan();
        var actorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var subscription = CompanySubscription.Activate(
            companyId: 10,
            commercialPlan: plan,
            periodicity: CompanySubscriptionPeriodicity.Monthly,
            startDateUtc: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            expiresAtUtc: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            activatedByUserPublicId: actorUserId,
            activatedAtUtc: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.ManualActivation,
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            observations: "Activacion manual");

        subscription.Suspend(
            changedAtUtc: new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.ManualSuspension,
            observations: "Pendiente de revision",
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            actorUserPublicId: actorUserId);

        subscription.Reactivate(
            changedAtUtc: new DateTime(2026, 4, 12, 9, 0, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
            observations: "Reactivacion aprobada",
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            actorUserPublicId: actorUserId);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(SubscriptionStatusChangeReasonCode.AuthorizedReactivation, subscription.CurrentStatusReasonCode);
        Assert.Equal(SubscriptionStatusChangeOrigin.PlatformOperator, subscription.CurrentStatusOrigin);
        Assert.Equal("Reactivacion aprobada", subscription.CurrentStatusObservations);
        Assert.Null(subscription.EndDateUtc);
        Assert.Equal(3, subscription.StatusTransitions.Count);
        Assert.Equal(
            new[]
            {
                SubscriptionStatus.Active,
                SubscriptionStatus.Suspended,
                SubscriptionStatus.Active
            },
            subscription.StatusTransitions.Select(transition => transition.NewStatus).ToArray());
    }

    [Fact]
    public void Reactivate_WhenSuspensionIsPastExpiration_ShouldThrow()
    {
        var plan = CreatePlan();
        var subscription = CompanySubscription.Activate(
            companyId: 10,
            commercialPlan: plan,
            periodicity: CompanySubscriptionPeriodicity.Monthly,
            startDateUtc: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            expiresAtUtc: new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            activatedByUserPublicId: Guid.Empty,
            activatedAtUtc: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.ManualActivation,
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            observations: null);

        subscription.Suspend(
            changedAtUtc: new DateTime(2026, 4, 3, 8, 0, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.ManualSuspension,
            observations: null,
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            actorUserPublicId: null);

        var action = () => subscription.Reactivate(
            changedAtUtc: new DateTime(2026, 4, 6, 9, 0, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
            observations: null,
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            actorUserPublicId: null);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Cancel_WhenSubscriptionWasScheduled_ShouldKeepEndDateNull()
    {
        var plan = CreatePlan();
        var subscription = CompanySubscription.Schedule(
            companyId: 10,
            commercialPlan: plan,
            periodicity: CompanySubscriptionPeriodicity.Annual,
            startDateUtc: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            expiresAtUtc: null,
            activatedByUserPublicId: Guid.Empty,
            activatedAtUtc: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.ActivationScheduled,
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            observations: null);

        subscription.Cancel(
            changedAtUtc: new DateTime(2026, 4, 15, 8, 30, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.CommercialCancellation,
            observations: "Cliente desistio",
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            actorUserPublicId: null);

        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.Null(subscription.EndDateUtc);
        Assert.Equal(2, subscription.StatusTransitions.Count);
        Assert.Equal(SubscriptionStatus.Scheduled, subscription.StatusTransitions.First().NewStatus);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.StatusTransitions.Last().NewStatus);
    }

    [Fact]
    public void SubscriptionStatusPolicy_ShouldExposeCapabilitiesAndTransitionRules()
    {
        Assert.True(SubscriptionStatusPolicy.CanOperate(SubscriptionStatus.Active));
        Assert.True(SubscriptionStatusPolicy.CanOperate(SubscriptionStatus.Trial));
        Assert.False(SubscriptionStatusPolicy.CanOperate(SubscriptionStatus.Suspended));

        Assert.True(SubscriptionStatusPolicy.CanGenerateCharges(SubscriptionStatus.Active));
        Assert.False(SubscriptionStatusPolicy.CanGenerateCharges(SubscriptionStatus.Trial));
        Assert.False(SubscriptionStatusPolicy.CanGenerateCharges(SubscriptionStatus.Cancelled));

        Assert.True(SubscriptionStatusPolicy.CanTransition(
            SubscriptionStatus.Active,
            SubscriptionStatus.Suspended,
            SubscriptionStatusChangeOrigin.PlatformOperator));
        Assert.True(SubscriptionStatusPolicy.CanTransition(
            SubscriptionStatus.Scheduled,
            SubscriptionStatus.Active,
            SubscriptionStatusChangeOrigin.SystemProcess));
        Assert.False(SubscriptionStatusPolicy.CanTransition(
            SubscriptionStatus.Cancelled,
            SubscriptionStatus.Active,
            SubscriptionStatusChangeOrigin.PlatformOperator));
        Assert.False(SubscriptionStatusPolicy.IsReasonAllowed(
            SubscriptionStatus.Active,
            SubscriptionStatus.Cancelled,
            SubscriptionStatusChangeOrigin.PlatformOperator,
            SubscriptionStatusChangeReasonCode.ManualSuspension));
    }

    [Fact]
    public void PromoteScheduled_BeforeStartDate_ShouldThrow()
    {
        var plan = CreatePlan();
        var subscription = CompanySubscription.Schedule(
            companyId: 10,
            commercialPlan: plan,
            periodicity: CompanySubscriptionPeriodicity.Monthly,
            startDateUtc: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            expiresAtUtc: null,
            activatedByUserPublicId: Guid.Empty,
            activatedAtUtc: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.ActivationScheduled,
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            observations: null);

        var action = () => subscription.PromoteScheduled(new DateTime(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc));

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    private static CommercialPlan CreatePlan() =>
        SetPersistedIdentifiers(CommercialPlan.Create(
            code: "PRO",
            name: "Professional",
            description: "Plan profesional",
            baseMonthlyFee: 150m,
            pricePerActiveEmployee: 4m,
            status: CommercialPlanStatus.Active,
            isSystemPlan: false,
            limits: [],
            initialVersionEffectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

    private static CommercialPlan SetPersistedIdentifiers(CommercialPlan plan)
    {
        SetEntityId(plan, 100);

        var versionId = 1000L;
        foreach (var version in plan.Versions)
        {
            SetEntityId(version, versionId++);
        }

        return plan;
    }

    private static void SetEntityId(Entity entity, long id) =>
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(entity, id);
}
