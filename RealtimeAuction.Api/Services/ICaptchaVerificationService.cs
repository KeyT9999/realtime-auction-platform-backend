namespace RealtimeAuction.Api.Services;

public interface ICaptchaVerificationService
{
    Task<CaptchaVerificationResult> VerifyAsync(
        string? token,
        string action,
        string? remoteIp = null,
        CancellationToken cancellationToken = default);
}

public sealed record CaptchaVerificationResult(bool Success, string? ErrorMessage = null, int StatusCode = 400)
{
    public static CaptchaVerificationResult Passed() => new(true, null, 200);
}
