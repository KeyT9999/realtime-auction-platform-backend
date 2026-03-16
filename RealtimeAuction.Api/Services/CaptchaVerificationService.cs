using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RealtimeAuction.Api.Settings;

namespace RealtimeAuction.Api.Services;

public class CaptchaVerificationService : ICaptchaVerificationService
{
    private readonly HttpClient _httpClient;
    private readonly CaptchaSettings _settings;
    private readonly ILogger<CaptchaVerificationService> _logger;

    public CaptchaVerificationService(
        IOptions<CaptchaSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<CaptchaVerificationService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient("Captcha");
        _logger = logger;
    }

    public async Task<CaptchaVerificationResult> VerifyAsync(
        string? token,
        string action,
        string? remoteIp = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return CaptchaVerificationResult.Passed();
        }

        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            _logger.LogError("CAPTCHA is enabled but SecretKey is not configured.");
            return new CaptchaVerificationResult(false, "CAPTCHA verification is not configured.", 503);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new CaptchaVerificationResult(false, "CAPTCHA token is required.", 400);
        }

        try
        {
            var payload = new Dictionary<string, string>
            {
                ["secret"] = _settings.SecretKey,
                ["response"] = token
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                payload["remoteip"] = remoteIp;
            }

            using var response = await _httpClient.PostAsync(
                _settings.VerifyUrl,
                new FormUrlEncodedContent(payload),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CAPTCHA provider returned status code {StatusCode}", response.StatusCode);
                return new CaptchaVerificationResult(false, "CAPTCHA verification is temporarily unavailable.", 503);
            }

            var result = await response.Content.ReadFromJsonAsync<GoogleCaptchaVerifyResponse>(cancellationToken: cancellationToken);
            if (result?.Success != true)
            {
                _logger.LogWarning("CAPTCHA verification failed with codes: {ErrorCodes}", result?.ErrorCodes is { Length: > 0 } ? string.Join(", ", result.ErrorCodes) : "none");
                return new CaptchaVerificationResult(false, "CAPTCHA verification failed.", 400);
            }

            if (!string.Equals(result.Action, action, StringComparison.Ordinal))
            {
                _logger.LogWarning("CAPTCHA action mismatch. Expected {ExpectedAction}, received {ActualAction}", action, result.Action);
                return new CaptchaVerificationResult(false, "CAPTCHA action mismatch.", 400);
            }

            if (result.Score < _settings.MinimumScore)
            {
                _logger.LogWarning("CAPTCHA score {Score} is below threshold {Threshold}", result.Score, _settings.MinimumScore);
                return new CaptchaVerificationResult(false, "CAPTCHA verification score is too low.", 400);
            }

            return CaptchaVerificationResult.Passed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while verifying CAPTCHA.");
            return new CaptchaVerificationResult(false, "CAPTCHA verification failed.", 503);
        }
    }

    private sealed class GoogleCaptchaVerifyResponse
    {
        public bool Success { get; set; }
        public double Score { get; set; }
        public string? Action { get; set; }
        [JsonPropertyName("error-codes")]
        public string[] ErrorCodes { get; set; } = [];
    }
}
