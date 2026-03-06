using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using FluentValidation;

namespace RealtimeAuction.Api.Middleware;

/// <summary>
/// Global exception handler middleware — catches unhandled exceptions from the pipeline
/// and returns structured JSON error responses with appropriate HTTP status codes.
/// Acts as a safety net; controllers can still use local try/catch for specific flows.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = exception switch
        {
            // FluentValidation validation failures → 400 with structured errors
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "Validation failed",
                validationEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    ) as object
            ),

            // Authentication failures → 401
            AuthenticationException authEx => (
                HttpStatusCode.Unauthorized,
                authEx.Message,
                (object?)null
            ),

            // Authorization failures → 403
            UnauthorizedAccessException _ => (
                HttpStatusCode.Forbidden,
                "Bạn không có quyền truy cập tài nguyên này.",
                (object?)null
            ),

            // Not found → 404
            KeyNotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                notFoundEx.Message,
                (object?)null
            ),

            // Bad request / validation → 400
            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                argEx.Message,
                (object?)null
            ),

            InvalidOperationException invalidOpEx => (
                HttpStatusCode.BadRequest,
                invalidOpEx.Message,
                (object?)null
            ),

            // Catch-all → 500
            _ => (
                HttpStatusCode.InternalServerError,
                _env.IsDevelopment()
                    ? exception.Message
                    : "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
                (object?)null
            )
        };

        // Log based on severity
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning("Handled exception ({StatusCode}): {Message}", (int)statusCode, exception.Message);
        }

        // Build response
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new Dictionary<string, object?>
        {
            ["message"] = message
        };

        if (errors != null)
        {
            response["errors"] = errors;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsJsonAsync(response, jsonOptions);
    }
}
