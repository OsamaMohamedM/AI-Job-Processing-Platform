using AiJobPlatform.Worker;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddSerilog((sp, cfg) => cfg
//    .ReadFrom.Configuration(builder.Configuration)
//    .Enrich.FromLogContext()
//    .Enrich.WithProperty("Service", "worker")
//    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddApplication(builder.Configuration).AddWorkerUseCases();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRabbitMq(builder.Configuration);

builder.Services.AddHostedService<OutboxPublisherService>();
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();