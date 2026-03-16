using Microsoft.AspNetCore.Http;

namespace RealtimeAuction.Api.Services;

public interface IGeminiService
{
    Task<string> AnalyzeProductImagesAsync(
        IReadOnlyCollection<IFormFile> files,
        CancellationToken cancellationToken = default);
}
