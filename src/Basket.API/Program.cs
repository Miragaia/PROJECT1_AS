using Prometheus;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Service Name for Tracing
var serviceName = "eShop.Basket.API";

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317"); // OTLP Exporter
        })
        .AddSource("eShop.Basket.API")
        .AddProcessor(new eShop.ServiceDefaults.Telemetry.SensitiveDataProcessor())
    )
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("Microsoft.AspNetCore.Hosting")
        .AddMeter("eShop.Basket.API")
    );

// Add existing services
builder.AddBasicServiceDefaults();
builder.AddApplicationServices();

builder.Services.AddGrpc();

var app = builder.Build();

// Expose Prometheus metrics
app.UseMetricServer();

app.MapDefaultEndpoints();
app.MapGrpcService<BasketService>();

app.Run();
