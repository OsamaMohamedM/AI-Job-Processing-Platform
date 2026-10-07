using AiJobPlatform.Api.Endpoints;
using AiJobPlatform.Api.Infrastructure;
using API.Endpoints;
using API.Infrastructure;
using API.Middleware;
using Application;
using Application.Common;
using Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "api")
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplication(builder.Configuration).AddApiUseCases();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapGet("/api/ping", () => Results.Ok(new { message = "pong" }));

app.MapAuthEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.MapJobsEndpoints();

app.Run();