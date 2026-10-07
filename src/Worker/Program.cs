using AiJobPlatform.Worker;
using Application;
using Application.Jobs.Processing;
using Infrastructure;
using Microsoft.Extensions.Options;
using Worker.Services;
using Worker.Validator;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddSerilog((sp, cfg) => cfg
//    .ReadFrom.Configuration(builder.Configuration)
//    .Enrich.FromLogContext()
//    .Enrich.WithProperty("Service", "worker")
//    .WriteTo.Console(new CompactJsonFormatter()));

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

app.MapGet("/", () => "Hello World!");

app.Run();