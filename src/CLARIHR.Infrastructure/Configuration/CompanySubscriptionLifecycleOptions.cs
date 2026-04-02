namespace CLARIHR.Infrastructure.Configuration;

public sealed class CompanySubscriptionLifecycleOptions
{
    public const string SectionName = "Billing:Subscriptions";

    public TimeSpan ScheduledPromotionInterval { get; init; } = TimeSpan.FromMinutes(1);

    public int ScheduledPromotionBatchSize { get; init; } = 50;

    public int ExpirationBatchSize { get; init; } = 50;
}
