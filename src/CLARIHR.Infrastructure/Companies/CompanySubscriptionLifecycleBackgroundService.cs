using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CompanySubscriptionLifecycleBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<CompanySubscriptionLifecycleOptions> options,
    ILogger<CompanySubscriptionLifecycleBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ExecuteCycleAsync(stoppingToken);

        using var timer = new PeriodicTimer(NormalizeInterval(options.Value.ScheduledPromotionInterval));
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ExecuteCycleAsync(stoppingToken);
        }
    }

    private async Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<CompanySubscriptionLifecycleProcessor>();
            _ = await processor.PromoteDueScheduledSubscriptionsAsync(cancellationToken);
            _ = await processor.ExpireDueSubscriptionsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An error occurred while promoting scheduled company subscriptions.");
        }
    }

    private static TimeSpan NormalizeInterval(TimeSpan interval) =>
        interval <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : interval;
}
