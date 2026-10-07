using System.ComponentModel.DataAnnotations;

namespace Application.Messaging;

public class OutboxOptions
{
    [Range(100, 60_000)]
    public int PollIntervalMilliseconds { get; set; } = 1000;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 50;

    [Range(1, 3600)]
    public int MaxPublishBackoffSeconds { get; set; } = 60;
}