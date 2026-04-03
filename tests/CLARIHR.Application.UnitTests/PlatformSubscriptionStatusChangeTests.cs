using System.Reflection;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Application.Features.PlatformSubscriptions;
using CLARIHR.Application.Features.PlatformSubscriptions.Common;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.UnitTests;

public sealed class PlatformSubscriptionStatusChangeTests
{
    private static readonly DateTime UtcNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CurrentUserId = Guid.Parse("90000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Preview_WhenSubscriptionIsNotSuspended_ShouldReturnIneligible()
    {
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var company = CreateCompany();
        companyRepository.Add(company);
        var subscription = CreateActiveSubscription(company.Id);
        subscriptionRepository.Add(subscription);

        var handler = new PreviewPlatformCompanySubscriptionStatusChangeQueryHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            companyRepository,
            subscriptionRepository,
            new FixedDateTimeProvider(UtcNow));

        var result = await handler.Handle(
            new PreviewPlatformCompanySubscriptionStatusChangeQuery(
                company.PublicId,
                subscription.PublicId,
                SubscriptionStatus.Active,
                SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                "Pago confirmado",
                UtcNow.Date),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsEligible);
        Assert.Contains("suspendidas", result.Value.IneligibilityReasons.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeStatus_WhenScheduledRequestAlreadyExists_ShouldReturnConflict()
    {
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var company = CreateCompany();
        companyRepository.Add(company);
        var subscription = CreateSuspendedSubscription(company.Id);
        subscriptionRepository.Add(subscription);
        subscriptionRepository.AddStatusChangeRequest(CompanySubscriptionStatusChangeRequest.Create(
            subscription,
            SubscriptionStatus.Active,
            SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
            UtcNow.AddMinutes(-30),
            UtcNow.Date.AddDays(3),
            CurrentUserId,
            "Pendiente"));

        var handler = new ChangePlatformCompanySubscriptionStatusCommandHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            companyRepository,
            new TestCommercialPlanRepository(),
            subscriptionRepository,
            new TestCurrentUserService(CurrentUserId),
            new TestPlatformAuditService(),
            new FixedDateTimeProvider(UtcNow),
            new TestUnitOfWork());

        var result = await handler.Handle(
            new ChangePlatformCompanySubscriptionStatusCommand(
                company.PublicId,
                subscription.PublicId,
                SubscriptionStatus.Active,
                SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                "Pago confirmado",
                UtcNow.Date.AddDays(5)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PlatformSubscriptionErrors.StatusChangePendingConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task ChangeStatus_WhenSchedulingReactivation_ShouldPersistPendingRequestWithoutMutatingSubscription()
    {
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var company = CreateCompany();
        companyRepository.Add(company);
        var subscription = CreateSuspendedSubscription(company.Id);
        subscriptionRepository.Add(subscription);

        var auditService = new TestPlatformAuditService();
        var handler = new ChangePlatformCompanySubscriptionStatusCommandHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            companyRepository,
            new TestCommercialPlanRepository(),
            subscriptionRepository,
            new TestCurrentUserService(CurrentUserId),
            auditService,
            new FixedDateTimeProvider(UtcNow),
            new TestUnitOfWork());

        var result = await handler.Handle(
            new ChangePlatformCompanySubscriptionStatusCommand(
                company.PublicId,
                subscription.PublicId,
                SubscriptionStatus.Active,
                SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                "Pago confirmado",
                UtcNow.Date.AddDays(2)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.Suspended, subscription.Status);
        Assert.Single(subscriptionRepository.StatusChangeRequests);
        Assert.NotNull(result.Value.PendingStatusChange);
        Assert.Equal(UtcNow.Date.AddDays(2), result.Value.PendingStatusChange!.EffectiveDateUtc);
        Assert.Equal("COMPANY_SUBSCRIPTION_STATUS_CHANGE_REQUESTED", auditService.Entries.Single().EventType);
    }

    [Fact]
    public async Task ChangeStatus_WhenReactivationIsImmediate_ShouldRestoreActiveAndBillable()
    {
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var company = CreateCompany();
        companyRepository.Add(company);
        var subscription = CreateSuspendedSubscription(company.Id);
        subscriptionRepository.Add(subscription);

        var handler = new ChangePlatformCompanySubscriptionStatusCommandHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            companyRepository,
            new TestCommercialPlanRepository(),
            subscriptionRepository,
            new TestCurrentUserService(CurrentUserId),
            new TestPlatformAuditService(),
            new FixedDateTimeProvider(UtcNow),
            new TestUnitOfWork());

        var result = await handler.Handle(
            new ChangePlatformCompanySubscriptionStatusCommand(
                company.PublicId,
                subscription.PublicId,
                SubscriptionStatus.Active,
                SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                "Pago validado",
                UtcNow.Date),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(SubscriptionStatusChangeReasonCode.AuthorizedReactivation, subscription.CurrentStatusReasonCode);
        Assert.True(company.IsBillable);
        Assert.Null(result.Value.PendingStatusChange);
    }

    private static Company CreateCompany()
    {
        var company = Company.Create(
            "Acme",
            "acme",
            CurrentUserId,
            "SV",
            1);
        SetEntityId(company, 10);
        return company;
    }

    private static CompanySubscription CreateActiveSubscription(long companyId) =>
        CreateSubscription(companyId, suspendAtUtc: null);

    private static CompanySubscription CreateSuspendedSubscription(long companyId) =>
        CreateSubscription(companyId, suspendAtUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc));

    private static CompanySubscription CreateSubscription(long companyId, DateTime? suspendAtUtc)
    {
        var plan = CreatePlan();
        var subscription = CompanySubscription.Activate(
            companyId,
            plan,
            CompanySubscriptionPeriodicity.Monthly,
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CurrentUserId,
            new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
            SubscriptionStatusChangeReasonCode.ManualActivation,
            SubscriptionStatusChangeOrigin.PlatformOperator,
            "Activacion manual");

        if (suspendAtUtc.HasValue)
        {
            subscription.Suspend(
                suspendAtUtc.Value,
                SubscriptionStatusChangeReasonCode.ManualSuspension,
                "Mora",
                SubscriptionStatusChangeOrigin.PlatformOperator,
                CurrentUserId);
        }

        SetEntityId(subscription, 100);
        return subscription;
    }

    private static CommercialPlan CreatePlan()
    {
        var plan = CommercialPlan.Create(
            "PRO",
            "Professional",
            "Plan profesional",
            150m,
            4m,
            CommercialPlanStatus.Active,
            isSystemPlan: false,
            [],
            initialVersionEffectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        SetEntityId(plan, 200);
        foreach (var version in plan.Versions)
        {
            SetEntityId(version, 300 + version.VersionNumber);
        }

        return plan;
    }

    private static void SetEntityId(Entity entity, long id) =>
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(entity, id);

    private sealed class TestPlatformAuthorizationService(Result readResult, Result manageResult) : IPlatformAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(CancellationToken cancellationToken) => Task.FromResult(readResult);

        public Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) => Task.FromResult(manageResult);
    }

    private sealed class TestPlatformAuditService : IPlatformAuditService
    {
        public List<AuditLogEntry> Entries { get; } = [];

        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class TestCurrentUserService(Guid userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId.ToString();

        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; } = [];
        public IReadOnlyCollection<string> Permissions { get; } = [];
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IUnitOfWorkTransaction>(new TestUnitOfWorkTransaction());
    }

    private sealed class TestUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestCompanyRepository : ICompanyRepository
    {
        public List<Company> Items { get; } = [];

        public void Add(Company company) => Items.Add(company);

        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<Company?> FindByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(company => company.PublicId == companyPublicId));

        public Task<Application.Features.AccountCompanies.AccountCompanyDetailResponse?> FindOwnedByUserAsync(
            Guid companyPublicId,
            Guid ownerUserPublicId,
            Guid? activeTenantId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResponse<Application.Features.AccountCompanies.AccountCompanySummaryResponse>> GetOwnedByUserAsync(
            Guid ownerUserPublicId,
            CompanyListFilter filter,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountOwnedByUserAsync(
            Guid ownerUserPublicId,
            CompanyOwnershipCountFilter filter,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestCommercialPlanRepository : ICommercialPlanRepository
    {
        public void Add(CommercialPlan plan) => throw new NotSupportedException();

        public Task<CommercialPlan?> GetByInternalIdAsync(long commercialPlanId, CancellationToken cancellationToken) =>
            Task.FromResult<CommercialPlan?>(null);

        public Task<CommercialPlan?> GetByIdAsync(Guid commercialPlanId, CancellationToken cancellationToken) =>
            Task.FromResult<CommercialPlan?>(null);

        public Task<CommercialPlanVersion?> GetEffectiveVersionAsync(Guid commercialPlanId, DateTime effectiveAtUtc, CancellationToken cancellationToken) =>
            Task.FromResult<CommercialPlanVersion?>(null);

        public Task<CommercialPlan?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
            Task.FromResult<CommercialPlan?>(null);

        public Task<bool> IsSystemPlanAsync(long commercialPlanId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> CodeExistsAsync(string normalizedCode, long? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<PagedResponse<Application.Features.CommercialPlans.CommercialPlanSummaryResponse>> SearchAsync(
            CommercialPlanStatus? status,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestCompanySubscriptionRepository(TestCompanyRepository companyRepository) : ICompanySubscriptionRepository
    {
        public List<CompanySubscription> Items { get; } = [];
        public List<CompanySubscriptionStatusChangeRequest> StatusChangeRequests { get; } = [];

        public void Add(CompanySubscription subscription) => Items.Add(subscription);

        public void AddStatusChangeRequest(CompanySubscriptionStatusChangeRequest statusChangeRequest) =>
            StatusChangeRequests.Add(statusChangeRequest);

        public void AddPlanChange(CompanySubscriptionPlanChange planChange) => throw new NotSupportedException();

        public void AddCompanyAddon(CompanyCommercialAddon companyAddon) => throw new NotSupportedException();

        public void AddCompanyAddonChange(CompanyCommercialAddonChange companyAddonChange) => throw new NotSupportedException();

        public Task<CompanySubscription?> GetActiveByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.CompanyId == companyId && item.Status == SubscriptionStatus.Active));

        public Task<CompanySubscription?> GetCurrentByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.CompanyId == companyId));

        public Task<CompanySubscription?> GetScheduledByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.CompanyId == companyId && item.Status == SubscriptionStatus.Scheduled));

        public Task<CompanySubscription?> GetActiveByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.Single(item => item.PublicId == companyPublicId);
            return Task.FromResult(Items.SingleOrDefault(item => item.CompanyId == company.Id && item.Status == SubscriptionStatus.Active));
        }

        public Task<CompanySubscription?> GetByCompanyAndSubscriptionPublicIdAsync(
            Guid companyPublicId,
            Guid subscriptionPublicId,
            CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.Single(item => item.PublicId == companyPublicId);
            return Task.FromResult(Items.SingleOrDefault(item => item.CompanyId == company.Id && item.PublicId == subscriptionPublicId));
        }

        public Task<PlatformCompanySubscriptionOverviewResponse?> GetOverviewByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            if (company is null)
            {
                return Task.FromResult<PlatformCompanySubscriptionOverviewResponse?>(null);
            }

            var currentSubscription = Items
                .Where(item => item.CompanyId == company.Id && item.Status != SubscriptionStatus.Scheduled)
                .OrderByDescending(item => item.StatusChangedAtUtc)
                .Select(BuildResponse)
                .FirstOrDefault();

            var scheduledReplacement = Items
                .Where(item => item.CompanyId == company.Id && item.Status == SubscriptionStatus.Scheduled)
                .OrderByDescending(item => item.StartDateUtc)
                .Select(BuildResponse)
                .FirstOrDefault();

            return Task.FromResult<PlatformCompanySubscriptionOverviewResponse?>(new PlatformCompanySubscriptionOverviewResponse(
                company.PublicId,
                company.Name,
                company.Slug,
                company.Status,
                company.IsBillable,
                company.BillableSinceUtc,
                currentSubscription,
                scheduledReplacement));
        }

        public Task<PagedResponse<PlatformCompanySubscriptionResponse>> SearchByCompanyPublicIdAsync(
            Guid companyPublicId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanySubscriptionResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanySubscriptionListItemResponse>> SearchAsync(
            SubscriptionStatus? status,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanySubscriptionListItemResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>> SearchStatusHistoryAsync(
            Guid companyPublicId,
            Guid subscriptionPublicId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>> SearchPlanChangesByCompanyPublicIdAsync(
            Guid companyPublicId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanyAddonResponse>> SearchCompanyAddonsByCompanyPublicIdAsync(
            Guid companyPublicId,
            CompanyAddonStatus? status,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanyAddonResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanyEligibleAddonResponse>> SearchEligibleAddonsByCompanyPublicIdAsync(
            Guid companyPublicId,
            CommercialAddonType? type,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanyEligibleAddonResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanyAddonChangeResponse>> SearchAddonChangesByCompanyPublicIdAsync(
            Guid companyPublicId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanyAddonChangeResponse>([], pageNumber, pageSize, 0));

        public Task<IReadOnlyCollection<Guid>> GetDueScheduledSubscriptionIdsAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<IReadOnlyCollection<Guid>> GetDueScheduledStatusChangeRequestIdsAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<IReadOnlyCollection<Guid>> GetDueScheduledPlanChangeIdsAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<IReadOnlyCollection<Guid>> GetDueScheduledAddonChangeIdsAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<IReadOnlyCollection<Guid>> GetDueExpiringSubscriptionIdsAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<CompanySubscription?> GetByPublicIdAsync(Guid subscriptionPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.PublicId == subscriptionPublicId));

        public Task<CompanySubscriptionStatusChangeRequest?> GetStatusChangeRequestByPublicIdAsync(
            Guid statusChangeRequestPublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult(StatusChangeRequests.SingleOrDefault(item => item.PublicId == statusChangeRequestPublicId));

        public Task<CompanySubscriptionStatusChangeRequest?> GetScheduledStatusChangeRequestBySubscriptionIdAsync(
            long companySubscriptionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(StatusChangeRequests.SingleOrDefault(item =>
                item.CompanySubscriptionId == companySubscriptionId &&
                item.Status == SubscriptionStatusChangeRequestStatus.Scheduled));

        public Task<CompanySubscriptionPlanChange?> GetPlanChangeByPublicIdAsync(Guid planChangePublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanySubscriptionPlanChange?> GetPlanChangeByCompanyAndPublicIdAsync(Guid companyPublicId, Guid planChangePublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanySubscriptionPlanChange?> GetScheduledPlanChangeByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanyCommercialAddon?> GetCompanyAddonByCompanyAndAddonPublicIdAsync(Guid companyPublicId, Guid commercialAddonPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanyCommercialAddon?> GetCompanyAddonByCompanyIdAndAddonIdAsync(long companyId, long commercialAddonId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanyCommercialAddonChange?> GetAddonChangeByPublicIdAsync(Guid addonChangePublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanyCommercialAddonChange?> GetAddonChangeByCompanyAndPublicIdAsync(Guid companyPublicId, Guid addonChangePublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanyCommercialAddonChange?> GetScheduledAddonChangeByCompanyAndAddonIdAsync(long companyId, long commercialAddonId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PlatformCompanySubscriptionResponse?> GetResponseByPublicIdAsync(
            Guid companyPublicId,
            Guid subscriptionPublicId,
            CancellationToken cancellationToken)
        {
            var subscription = Items.SingleOrDefault(item => item.PublicId == subscriptionPublicId);
            return Task.FromResult(subscription is null ? null : BuildResponse(subscription));
        }

        public Task<PlatformCompanySubscriptionPlanChangeResponse?> GetPlanChangeResponseByPublicIdAsync(Guid companyPublicId, Guid planChangePublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PlatformCompanyAddonChangeResponse?> GetAddonChangeResponseByPublicIdAsync(Guid companyPublicId, Guid addonChangePublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> GetActivePlanCodeAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(Items.SingleOrDefault(item => item.Status == SubscriptionStatus.Active)?.PlanCode);

        private PlatformCompanySubscriptionResponse BuildResponse(CompanySubscription subscription)
        {
            var company = companyRepository.Items.Single(item => item.Id == subscription.CompanyId);
            var pendingStatusChange = StatusChangeRequests.SingleOrDefault(item =>
                item.CompanySubscriptionId == subscription.Id &&
                item.Status == SubscriptionStatusChangeRequestStatus.Scheduled);

            return new PlatformCompanySubscriptionResponse(
                subscription.PublicId,
                company.PublicId,
                Entity.CreateDeterministicPublicId($"plan:{subscription.CommercialPlanId}"),
                Entity.CreateDeterministicPublicId($"plan-version:{subscription.CommercialPlanVersionId}"),
                subscription.PlanCode,
                subscription.PlanName,
                subscription.PlanVersionNumber,
                subscription.BaseMonthlyFee,
                subscription.PricePerActiveEmployee,
                subscription.Periodicity,
                subscription.CurrencyCode,
                subscription.Status,
                subscription.StartDateUtc,
                subscription.ExpiresAtUtc,
                subscription.EndDateUtc,
                subscription.StatusChangedAtUtc,
                subscription.CurrentStatusReasonCode,
                subscription.CurrentStatusObservations,
                subscription.CurrentStatusOrigin,
                SubscriptionStatusPolicy.CanOperate(subscription.Status),
                SubscriptionStatusPolicy.CanGenerateCharges(subscription.Status),
                pendingStatusChange is null
                    ? null
                    : new PlatformCompanySubscriptionPendingStatusChangeResponse(
                        pendingStatusChange.TargetStatus,
                        pendingStatusChange.EffectiveDateUtc,
                        pendingStatusChange.ReasonCode,
                        pendingStatusChange.Observations,
                        pendingStatusChange.RequestedAtUtc,
                        pendingStatusChange.RequestedByUserPublicId),
                subscription.ActivatedByUserPublicId,
                subscription.ActivatedAtUtc,
                subscription.CreatedUtc,
                subscription.ModifiedUtc);
        }
    }
}
