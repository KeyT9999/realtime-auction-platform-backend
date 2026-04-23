namespace RealtimeAuction.Api.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string ServiceName { get; set; } = "RealtimeAuction.Api";
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-Id";
    public string TraceIdHeaderName { get; set; } = "X-Trace-Id";
    public string RequestIdHeaderName { get; set; } = "X-Request-Id";
    public ObservabilitySeqOptions Seq { get; set; } = new();
    public ObservabilityOpenTelemetryOptions OpenTelemetry { get; set; } = new();
    public ObservabilityRequestLoggingOptions RequestLogging { get; set; } = new();
}

public sealed class ObservabilitySeqOptions
{
    public bool Enabled { get; set; }
    public string? ServerUrl { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class ObservabilityOpenTelemetryOptions
{
    public bool Enabled { get; set; }
    public string? OtlpEndpoint { get; set; }
}

public sealed class ObservabilityRequestLoggingOptions
{
    public bool LogHealthChecksAsDebug { get; set; } = true;
}
