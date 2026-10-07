using AiJobPlatform.Application.Jobs;
using Application.Jobs.Processing;
using Application.Messaging;
using Domain.Jobs.Enums;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace Worker.Parser;

public static class EnvelopeParser
{
    public static bool TryParse(BasicDeliverEventArgs ea, out ConsumedMessage? message, out string reason)
    {
        message = null; reason = "";
        JobQueuedMessage? p;
        try { p = JsonSerializer.Deserialize<JobQueuedMessage>(ea.Body.Span, MessageJson.Options); }
        catch (JsonException) { reason = "invalid_json"; return false; }

        if (p is null) { reason = "empty_body"; return false; }
        if (p.SchemaVersion != JobQueuedMessage.CurrentSchemaVersion) { reason = "unsupported_schema"; return false; }
        if (p.JobId == Guid.Empty || p.MessageId == Guid.Empty) { reason = "missing_ids"; return false; }
        if (!Guid.TryParse(ea.BasicProperties.MessageId, out var propId) || propId != p.MessageId)
        { reason = "message_id_mismatch"; return false; }
        if (!EnumParser.TryParse<JobType>(p.JobType, out _)) { reason = "unknown_job_type"; return false; }

        message = new ConsumedMessage(p.MessageId, p.JobId, p.JobType, p.CorrelationId ?? "", p.Reason);
        return true;
    }
}