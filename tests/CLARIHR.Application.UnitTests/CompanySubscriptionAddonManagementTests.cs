using System.Reflection;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;

namespace CLARIHR.Application.UnitTests;

public sealed class CompanySubscriptionAddonManagementTests
{
    [Fact]
    public void CreateImmediateActivationChange_ShouldBeAppliedAndCaptureSnapshot()
    {
        var subscription = CreateSubscription(companyId: 10);
        var addon = CreateMassiveAddon();

        var change = CompanyCommercialAddonChange.Create(
            subscription,
            addon,
            SubscriptionAddonChangeAction.Activate,
            SubscriptionAddonChangeMode.Immediate,
            SubscriptionAddonChangeReasonCode.CustomerRequest,
            CompanyAddonStatus.Inactive,
            CompanyAddonStatus.Active,
            requestedAtUtc: new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc),
            effectiveDateUtc: new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            requestedByUserPublicId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            observations: "Activacion inmediata",
            quantityBasis: 0,
            estimatedNextChargeImpact: 20m);

        change.MarkApplied(new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc), subscription.PublicId);

        Assert.Equal(SubscriptionAddonChangeStatus.Applied, change.Status);
        Assert.Equal(CompanyAddonStatus.Active, change.ResultingStatus);
        Assert.Equal("ADDON-ATTENDANCE", change.AddonCode);
        Assert.Equal(20m, change.EstimatedNextChargeImpact);
        Assert.Equal(subscription.PublicId, change.AppliedSubscriptionPublicId);
    }

    [Fact]
    public void CancelScheduledChange_ShouldSetCancelledAndKeepRequestedStatusSnapshot()
    {
        var subscription = CreateSubscription(companyId: 10);
        var addon = CreateSpecializedAddon();

        var change = CompanyCommercialAddonChange.Create(
            subscription,
            addon,
            SubscriptionAddonChangeAction.Activate,
            SubscriptionAddonChangeMode.SpecificDate,
            SubscriptionAddonChangeReasonCode.CommercialTrial,
            CompanyAddonStatus.Inactive,
            CompanyAddonStatus.PendingActivation,
            requestedAtUtc: new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc),
            effectiveDateUtc: new DateTime(2026, 4, 9, 0, 0, 0, DateTimeKind.Utc),
            requestedByUserPublicId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            observations: "Programar activacion",
            quantityBasis: 2,
            estimatedNextChargeImpact: 25m);

        change.Cancel(
            cancelledAtUtc: new DateTime(2026, 4, 4, 10, 0, 0, DateTimeKind.Utc),
            cancelledByUserPublicId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            observations: "Cancelar programacion");

        Assert.Equal(SubscriptionAddonChangeStatus.Cancelled, change.Status);
        Assert.Equal(CompanyAddonStatus.PendingActivation, change.ResultingStatus);
        Assert.Equal("Cancelar programacion", change.CancellationObservations);
    }

    [Fact]
    public void CompanyAddonState_ShouldScheduleApplyAndRestoreStatuses()
    {
        var subscription = CreateSubscription(companyId: 10);
        var addon = CreateMassiveAddon();

        var state = CompanyCommercialAddon.Create(
            subscription,
            addon,
            CompanyAddonStatus.Inactive,
            new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc));

        state.ScheduleActivation(subscription, addon, new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(CompanyAddonStatus.PendingActivation, state.Status);

        state.RestoreStatus(CompanyAddonStatus.Inactive, new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(CompanyAddonStatus.Inactive, state.Status);

        state.ApplyActivation(subscription, addon, new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(CompanyAddonStatus.Active, state.Status);

        state.ScheduleDeactivation(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(CompanyAddonStatus.PendingDeactivation, state.Status);

        state.ApplyDeactivation(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(CompanyAddonStatus.Inactive, state.Status);
    }

    private static CompanySubscription CreateSubscription(long companyId)
    {
        var plan = SetPersistedIdentifiers(CommercialPlan.Create(
            code: "PRO",
            name: "Professional",
            description: "Plan profesional",
            baseMonthlyFee: 150m,
            pricePerActiveEmployee: 4m,
            status: CommercialPlanStatus.Active,
            isSystemPlan: false,
            limits: [],
            initialVersionEffectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var subscription = CompanySubscription.Activate(
            companyId,
            plan,
            CompanySubscriptionPeriodicity.Monthly,
            startDateUtc: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            expiresAtUtc: null,
            activatedByUserPublicId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            activatedAtUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            reasonCode: SubscriptionStatusChangeReasonCode.ManualActivation,
            origin: SubscriptionStatusChangeOrigin.PlatformOperator,
            observations: null);

        SetEntityId(subscription, 500);
        return subscription;
    }

    private static CommercialAddon CreateMassiveAddon()
    {
        var addon = CommercialAddon.Create(
            "ADDON-ATTENDANCE",
            "Attendance",
            "Attendance addon",
            CommercialAddonType.Massive,
            CommercialAddonBillingModel.PerActiveEmployee,
            CommercialAddon.MassiveMeasurementUnit,
            1.2m,
            null,
            20m,
            CommercialAddonPeriodicity.Monthly,
            CommercialAddonStatus.Active);
        SetEntityId(addon, 1000);
        return addon;
    }

    private static CommercialAddon CreateSpecializedAddon()
    {
        var addon = CommercialAddon.Create(
            "ADDON-RECRUITING",
            "Recruiting",
            "Recruiting addon",
            CommercialAddonType.Specialized,
            CommercialAddonBillingModel.PerSeat,
            "recruiter seat",
            12.5m,
            2,
            null,
            CommercialAddonPeriodicity.Monthly,
            CommercialAddonStatus.Active);
        SetEntityId(addon, 1001);
        return addon;
    }

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
