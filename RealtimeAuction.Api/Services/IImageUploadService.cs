namespace RealtimeAuction.Api.Services;

public interface IImageUploadService
{
    Task<string> UploadImageAsync(Stream imageStream, string fileName);
    Task<List<string>> UploadImagesAsync(List<(Stream stream, string fileName)> images);
    Task<bool> DeleteImageAsync(string publicId);
}
