using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using eShop.ServiceDefaults.Telemetry;
using System;
using System.Diagnostics;
using OpenTelemetry.Exporter;
using Prometheus;

namespace eShop.ServiceDefaults;

public static partial class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.AddBasicServiceDefaults();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Adds the services except for making outgoing HTTP calls.
    /// </summary>
    /// <remarks>
    /// This allows for things like Polly to be trimmed out of the app if it isn't used.
    /// </remarks>
    public static IHostApplicationBuilder AddBasicServiceDefaults(this IHostApplicationBuilder builder)
    {
        // Default health checks assume the event bus and self health checks
        builder.AddDefaultHealthChecks();

        builder.ConfigureOpenTelemetry();

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Experimental.Microsoft.Extensions.AI")
                    .AddMeter("eShop.WebApp.BasketState");
                    //TODO: adicionar aqui AddMeter depois de definir metricas do outro lado
            })
            .WithTracing(tracing =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    // We want to view all traces in development
                    tracing.SetSampler(new AlwaysOnSampler());
                }

                tracing.AddAspNetCoreInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("Experimental.Microsoft.Extensions.AI")
                    // .AddSource("eShop.Basket.API") // Add your custom activity source
                    .AddSource("eShop.WebApp.Services.OrderStatus.IntegrationEvents.OrderStatusChangedToSubmittedIntegrationEventHandler")
                    .AddSource("eShop.ClientApp.OrderService")
                    .AddSource("eShop.Ordering.API.OrdersApi")
                    .AddSource("eShop.Ordering.API.CreateOrderCommandHandler")
                    .AddSource("eShop.Ordering.API.Application.DomainEventHandlers.UpdateOrderWhenBuyerAndPaymentMethodVerifiedDomainEventHandler")
                    .AddSource("eShop.Ordering.API.Application.DomainEventHandlers.ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler")
                    .AddSource("eShop.Ordering.Domain.Seedwork.Entity")
                    .AddSource("eShop.Basket.API.IntegrationEvents.EventHandling.OrderStartedIntegrationEventHandler")
                    .AddSource("Experimental.Microsoft.Extensions.AI") 
                    .AddProcessor(new SensitiveDataProcessor()); // Add the processor here
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    // Modify the AddOpenTelemetryExporters method to use the correct exporter method
    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        // Make sure this points to your Jaeger container
        builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => 
            logging.AddOtlpExporter(otlpOptions => 
                otlpOptions.Endpoint = new Uri("http://localhost:4317")));
                
        builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => 
            metrics.AddOtlpExporter(otlpOptions => 
                otlpOptions.Endpoint = new Uri("http://localhost:4317")));
                
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => 
            tracing.AddOtlpExporter(otlpOptions => 
                otlpOptions.Endpoint = new Uri("http://localhost:4317")));

        // Use prometheus-net instead of OpenTelemetry.Exporter.Prometheus
        // The metrics will be available at the /metrics endpoint automatically

        // Add console exporters for local debugging
        if (builder.Environment.IsDevelopment())
        {
            // For metrics
            builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => 
                metrics.AddOtlpExporter());
            
            // For traces - replaced AddConsoleExporter() with AddConsoleExporter
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => 
                tracing.AddOtlpExporter());  // Remove the console exporter for now
            
            // For logs
            builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => 
                logging.AddOtlpExporter());
        }

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Use prometheus-net.AspNetCore
        app.UseMetricServer();  // This exposes the /metrics endpoint for Prometheus

        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
