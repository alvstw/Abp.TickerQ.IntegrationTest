using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;

namespace Acme.BookStore.BackgroundJobs;

public class BackgroundTestAppService : BookStoreAppService, IBackgroundTestAppService
{
    private readonly IBackgroundJobManager _backgroundJobManager;

    public BackgroundTestAppService(IBackgroundJobManager backgroundJobManager)
    {
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task TriggerTestJobAsync(TriggerTestBackgroundJobRequestDto input)
    {
        await _backgroundJobManager.EnqueueAsync(new TestBackgroundJobArgs
        {
            Message = input.Message,
            SimulatedDurationSeconds = input.SimulatedDurationSeconds
        });
    }
}