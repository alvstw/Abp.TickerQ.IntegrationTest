using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace Acme.BookStore.BackgroundJobs;

public class TestBackgroundJob : AsyncBackgroundJob<TestBackgroundJobArgs>, ITransientDependency
{
    private readonly ILogger<TestBackgroundJob> _logger;

    public TestBackgroundJob(ILogger<TestBackgroundJob> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync(TestBackgroundJobArgs args)
    {
        var durationSeconds = Math.Clamp(args.SimulatedDurationSeconds, 1, 120);

        _logger.LogInformation("TestBackgroundJob started. Message: {Message}", args.Message);

        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

        _logger.LogInformation("TestBackgroundJob completed after {DurationSeconds} seconds.", durationSeconds);
    }
}
