using AiJobPlatform.Worker;
using Application;
using Application.Jobs.Processing;
using Infrastructure;
using Microsoft.Extensions.Options;
using Worker.Services;
using Worker.Validator;

using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog((sp, cfg) => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "worker")
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddApplication(builder.Configuration).AddWorkerUseCases();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddAiProvider(builder.Configuration);
builder.Services.AddOptions<ProcessingOptions>().Bind(builder.Configuration.GetSection("Processing"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ProcessingOptions>, ProcessingOptionsValidator>();
builder.Services.AddHostedService<JobConsumerService>();

builder.Services.AddHostedService<OutboxPublisherService>();
var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var env = builder.Environment.EnvironmentName;
WorkerHostLog.WorkerStarted(logger, "worker", env, null);

app.MapGet("/", () => "Hello World!");

WorkerHostLog.WorkerReady(logger, "worker", env, null);

app.Run();

internal static class WorkerHostLog
{
    public static readonly Action<Microsoft.Extensions.Logging.ILogger, string, string, Exception?> WorkerStarted =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(721, "WorkerStarted"), "WorkerStarted {Service} {Environment}");

    public static readonly Action<Microsoft.Extensions.Logging.ILogger, string, string, Exception?> WorkerReady =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(722, "WorkerReady"), "WorkerReady {Service} {Environment}");
}