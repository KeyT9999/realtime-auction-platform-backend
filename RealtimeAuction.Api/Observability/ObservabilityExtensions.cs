using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RealtimeAuction.Api.Middleware;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;

namespace RealtimeAuction.Api.Observability;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddProductionObservability(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ObservabilityOptions>(
            builder.Configuration.GetSection(ObservabilityOptions.SectionName));

        var options = BuildOptions(builder.Configuration, builder.Environment);
        builder.Services.PostConfigure<ObservabilityOptions>(configuredOptions =>
        {
            configuredOptions.ServiceName = options.ServiceName;
            configuredOptions.CorrelationIdHeaderName = options.CorrelationIdHeaderName;
            configuredOptions.TraceIdHeaderName = options.TraceIdHeaderName;
            configuredOptions.RequestIdHeaderName = options.RequestIdHeaderName;
            configuredOptions.Seq.Enabled = options.Seq.Enabled;
            configuredOptions.Seq.ServerUrl = options.Seq.ServerUrl;
            configuredOptions.Seq.ApiKey = options.Seq.ApiKey;
            configuredOptions.OpenTelemetry.Enabled = options.OpenTelemetry.Enabled;
            configuredOptions.OpenTelemetry.OtlpEndpoint = options.OpenTelemetry.OtlpEndpoint;
            configuredOptions.RequestLogging.LogHealthChecksAsDebug = options.RequestLogging.LogHealthChecksAsDebug;
        });

        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            var resolvedOptions = BuildOptions(context.Configuration, context.HostingEnvironment);
            var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";

            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithSpan()
                .Enrich.WithProperty("service.name", resolvedOptions.ServiceName)
                .Enrich.WithProperty("service.version", serviceVersion)
                .WriteTo.Console(
                    outputTemplate:
                        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

            // Seq (run via Docker): push logs to the local Seq container (host->container 5341).
            // Enable with Observability:Seq:Enabled=true or env Observability__Seq__Enabled=true
            if (resolvedOptions.Seq.Enabled)
            {
                loggerConfiguration.WriteTo.Seq("http://localhost:5341");
            }
        });

        if (options.OpenTelemetry.Enabled && TryParseUri(options.OpenTelemetry.OtlpEndpoint) is { } otlpEndpoint)
        {
            var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName: options.ServiceName, serviceVersion: serviceVersion);

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(options.ServiceName, serviceVersion: serviceVersion))
                .WithTracing(tracing =>
                {
                    tracing
                        .SetResourceBuilder(resourceBuilder)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint);
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .SetResourceBuilder(resourceBuilder)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint);
                });
        }

        return builder;
    }

    public static WebApplication UseProductionObservability(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        app.UseMiddleware<RequestContextMiddleware>();
        app.UseSerilogRequestLogging(logging =>
        {
            logging.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            logging.GetLevel = (httpContext, _, exception) =>
            {
                if (exception != null)
                {
                    return LogEventLevel.Error;
                }

                if (options.RequestLogging.LogHealthChecksAsDebug
                    && httpContext.Response.StatusCode < 400
                    && httpContext.Request.Path.StartsWithSegments("/health"))
                {
                    return LogEventLevel.Debug;
                }

                return httpContext.Response.StatusCode switch
                {
                    >= 500 => LogEventLevel.Error,
                    >= 400 => LogEventLevel.Warning,
                    _ => LogEventLevel.Information
                };
            };
            logging.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);

                if (httpContext.Items.TryGetValue(RequestContextMiddleware.CorrelationIdItemKey, out var correlationId))
                {
                    diagnosticContext.Set("CorrelationId", correlationId);
                }

                var traceId = Activity.Current?.TraceId.ToString();
                if (!string.IsNullOrWhiteSpace(traceId))
                {
                    diagnosticContext.Set("TraceId", traceId);
                }

                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    diagnosticContext.Set("UserId", userId);
                }

                var endpointName = httpContext.GetEndpoint()?.DisplayName;
                if (!string.IsNullOrWhiteSpace(endpointName))
                {
                    diagnosticContext.Set("EndpointName", endpointName);
                }

                diagnosticContext.Set("RequestHost", httpContext.Request.Host.ToString());
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            };
        });

        return app;
    }

    private static ObservabilityOptions BuildOptions(IConfiguration configuration, IHostEnvironment environment)
    {
        var options = new ObservabilityOptions();
        configuration.GetSection(ObservabilityOptions.SectionName).Bind(options);

        options.ServiceName = FirstNonEmpty(
                Environment.GetEnvironmentVariable("SERVICE_NAME"),
                options.ServiceName,
                environment.ApplicationName,
                "RealtimeAuction.Api")
            ?? "RealtimeAuction.Api";

        options.CorrelationIdHeaderName = FirstNonEmpty(
                Environment.GetEnvironmentVariable("CORRELATION_ID_HEADER_NAME"),
                options.CorrelationIdHeaderName,
                "X-Correlation-Id")
            ?? "X-Correlation-Id";

        options.TraceIdHeaderName = FirstNonEmpty(
                Environment.GetEnvironmentVariable("TRACE_ID_HEADER_NAME"),
                options.TraceIdHeaderName,
                "X-Trace-Id")
            ?? "X-Trace-Id";

        options.RequestIdHeaderName = FirstNonEmpty(
                Environment.GetEnvironmentVariable("REQUEST_ID_HEADER_NAME"),
                options.RequestIdHeaderName,
                "X-Request-Id")
            ?? "X-Request-Id";

        options.Seq.Enabled = ParseBool(
            Environment.GetEnvironmentVariable("SEQ_ENABLED"),
            options.Seq.Enabled);
        options.Seq.ServerUrl = FirstNonEmpty(
            Environment.GetEnvironmentVariable("SEQ_URL"),
            options.Seq.ServerUrl);
        options.Seq.ApiKey = FirstNonEmpty(
            Environment.GetEnvironmentVariable("SEQ_API_KEY"),
            options.Seq.ApiKey);

        options.OpenTelemetry.Enabled = ParseBool(
            Environment.GetEnvironmentVariable("OTEL_ENABLED"),
            options.OpenTelemetry.Enabled || !string.IsNullOrWhiteSpace(options.OpenTelemetry.OtlpEndpoint));
        options.OpenTelemetry.OtlpEndpoint = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"),
            options.OpenTelemetry.OtlpEndpoint);

        return options;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static Uri? TryParseUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }
}
