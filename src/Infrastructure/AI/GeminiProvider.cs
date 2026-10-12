namespace Infrastructure.AI;

using Application.AI;
using Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public class GeminiProvider(
    HttpClient http,
    IOptions<AiOptions> options,
    ILogger<GeminiProvider> logger
    ) : IAiProvider
{
    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var o = options.Value;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));

        var sw = Stopwatch.StartNew();

        var body = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = request.Instructions
                    }
                }
            },

            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = request.UserContent
                        }
                    }
                }
            },

            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        var url = $"models/{o.Model}:generateContent";

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        req.Headers.Add("x-goog-api-key", o.ApiKey);

        try
        {
            using var resp = await http.SendAsync(
                req,
                timeout.Token);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogError("AI provider returned HTTP {(StatusCode)} after {ElapsedMilliseconds} ms.",
                    (int)resp.StatusCode,
                    sw.ElapsedMilliseconds);
                logger.LogInformation("AI provider response headers: {Headers}", resp.Headers.ToString());
                var errorBody = await resp.Content.ReadAsStringAsync(timeout.Token);

                var kind = MapStatus(resp.StatusCode);
                logger.LogError(
      "Gemini API error. StatusCode={StatusCode}, Body={Body}",
      (int)resp.StatusCode,
      errorBody);

                TimeSpan? retryAfter = null;

                if (resp.Headers.RetryAfter is { } ra)
                {
                    var wait = ra.Delta ??
                        (ra.Date is { } at
                            ? at - DateTimeOffset.UtcNow
                            : null);

                    if (wait is { } w && w > TimeSpan.Zero)
                    {
                        retryAfter = TimeSpan.FromSeconds(
                            Math.Min(w.TotalSeconds, 300));
                    }
                }

                throw new AiProviderException(kind,
                    $"AI provider returned HTTP {(int)resp.StatusCode}.",
                    (int)resp.StatusCode,
                    retryAfter);
            }

            await using var stream =
                await resp.Content.ReadAsStreamAsync(timeout.Token);

            using var doc =
                await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: timeout.Token);

            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return new AiResponse(text ?? string.Empty);
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("AI provider call timed out after {TimeoutSeconds} seconds.",
                o.TimeoutSeconds);
            throw new AiProviderException(
                AiErrorKind.Timeout,
                "AI provider call timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(
                AiErrorKind.Network,
                "AI provider could not be reached.",
                inner: ex);
        }
        catch (Exception ex) when (
            ex is JsonException
            or KeyNotFoundException
            or InvalidOperationException
            or IndexOutOfRangeException)
        {
            logger.LogError(ex, "AI provider returned an unreadable response.");
            throw new AiProviderException(
                AiErrorKind.InvalidResponse,
                "AI provider returned an unreadable response.",
                inner: ex);
        }
    }

    private static AiErrorKind MapStatus(HttpStatusCode status) =>
        (int)status switch
        {
            408 => AiErrorKind.Timeout,
            429 => AiErrorKind.RateLimited,
            401 or 403 => AiErrorKind.Unauthorized,
            >= 500 => AiErrorKind.ServerError,
            _ => AiErrorKind.BadRequest
        };
}