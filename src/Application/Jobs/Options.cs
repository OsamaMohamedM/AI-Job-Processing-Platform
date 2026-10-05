using System.ComponentModel.DataAnnotations;

namespace Application.Jobs;

public sealed class JobsOptions
{
    [Range(100, 200_000)]
    public int MaxInputCharacters { get; set; } = 20_000;
}

public sealed class RetryOptions
{
    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 3;

    [Range(1, 600)]
    public int BaseDelaySeconds { get; set; } = 2;

    [Range(1.0, 10.0)]
    public double Multiplier { get; set; } = 2.0;

    [Range(1, 3600)]
    public int MaxDelaySeconds { get; set; } = 60;

    [Range(0.0, 1.0)]
    public double JitterRatio { get; set; } = 0.2;
}