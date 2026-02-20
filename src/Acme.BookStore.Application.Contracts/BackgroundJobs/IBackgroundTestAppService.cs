using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Acme.BookStore.BackgroundJobs;

public interface IBackgroundTestAppService : IApplicationService
{
    Task TriggerTestJobAsync(TriggerTestBackgroundJobRequestDto input);
}
