namespace Application.Jobs.Processing;

using Microsoft.Extensions.Options;

public sealed class RetryPolicy(IOptions<RetryOptions> options)
{
    public TimeSpan Delay(int failedAttemptNumber, TimeSpan? retryAfter = null)
    {
        var o = options.Value;
        var seconds = Math.Min(o.MaxDelaySeconds, o.BaseDelaySeconds * Math.Pow(o.Multiplier, failedAttemptNumber - 1));
        var jitter = 1 + (Random.Shared.NextDouble() * 2 - 1) * o.JitterRatio;
        var delay = TimeSpan.FromSeconds(seconds * jitter);
        return retryAfter is { } ra && ra > delay ? ra : delay;
    }
}