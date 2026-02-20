namespace Acme.BookStore.BackgroundJobs;

public class TriggerTestBackgroundJobRequestDto
{
    public string Message { get; set; } = "Triggered from API";

    public int SimulatedDurationSeconds { get; set; } = 3;
}
