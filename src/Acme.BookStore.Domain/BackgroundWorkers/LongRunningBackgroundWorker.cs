using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Threading;

namespace Acme.BookStore.BackgroundWorkers;

public class LongRunningBackgroundWorker : AsyncPeriodicBackgroundWorkerBase, ISingletonDependency
{
    private readonly ILogger<LongRunningBackgroundWorker> _logger;

    public LongRunningBackgroundWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<LongRunningBackgroundWorker> logger)
        : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        Timer.Period = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("LongRunningBackgroundWorker cycle started.");

        for (var step = 1; step <= 12; step++)
        {
            workerContext.CancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("LongRunningBackgroundWorker step {Step}/12", step);
            await Task.Delay(TimeSpan.FromSeconds(5), workerContext.CancellationToken);
        }

        _logger.LogInformation("LongRunningBackgroundWorker cycle finished.");
    }
}
