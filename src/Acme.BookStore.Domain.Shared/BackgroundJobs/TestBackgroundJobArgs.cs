using System;

namespace Acme.BookStore.BackgroundJobs;

[Serializable]
public class TestBackgroundJobArgs
{
    public string Message { get; set; } = string.Empty;

    public int SimulatedDurationSeconds { get; set; } = 3;
}
