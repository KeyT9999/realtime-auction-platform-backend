using Serilog.Context;
using RealtimeAuction.Api.Observability;
using Microsoft.Extensions.Options;

namespace RealtimeAuction.Api.Middleware;

public sealed class RequestContextMiddleware
{
    public const string CorrelationIdItemKey = "RequestContext.CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ObservabilityOptions _options;

    public RequestContextMiddleware(
        RequestDelegate next,
        IOptions<ObservabilityOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[CorrelationIdItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[_options.CorrelationIdHeaderName] = correlationId;
            context.Response.Headers[_options.RequestIdHeaderName] = context.TraceIdentifier;

            var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString();
            if (!string.IsNullOrWhiteSpace(traceId))
            {
                context.Response.Headers[_options.TraceIdHeaderName] = traceId;
            }

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        {
            await _next(context);
        }
    }

    private string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(_options.CorrelationIdHeaderName, out var values))
        {
            var incomingCorrelationId = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(incomingCorrelationId))
            {
                return incomingCorrelationId;
            }
        }

        return context.TraceIdentifier;
    }
}
