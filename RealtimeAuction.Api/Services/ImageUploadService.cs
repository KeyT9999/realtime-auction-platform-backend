using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using RealtimeAuction.Api.Settings;
using Microsoft.Extensions.Options;

namespace RealtimeAuction.Api.Services;

public class ImageUploadService : IImageUploadService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<ImageUploadService> _logger;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private readonly string _cloudName;

    public ImageUploadService(IOptions<CloudinarySettings> settings, ILogger<ImageUploadService> logger)
    {
        _logger = logger;
        _cloudName = settings.Value.CloudName;
        var account = new Account(
            settings.Value.CloudName,
            settings.Value.ApiKey,
            settings.Value.ApiSecret
        );
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(Stream imageStream, string fileName)
    {
        try
        {
            // Validate file extension
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                throw new ArgumentException($"File type {extension} is not allowed. Allowed types: jpg, jpeg, png, webp");
            }

            // Reset stream position to beginning
            if (imageStream.CanSeek)
            {
                imageStream.Position = 0;
            }

            // Validate file size
            if (imageStream.Length > MaxFileSize)
            {
                throw new ArgumentException($"File size exceeds maximum allowed size of {MaxFileSize / 1024 / 1024}MB");
            }

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, imageStream),
                Folder = "auction-products",
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return uploadResult.SecureUrl.ToString();
            }

            throw new Exception($"Upload failed with status: {uploadResult.StatusCode}, Error: {uploadResult.Error?.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image: {FileName}", fileName);
            throw;
        }
    }

    public async Task<List<string>> UploadImagesAsync(List<(Stream stream, string fileName)> images)
    {
        var uploadedUrls = new List<string>();

        foreach (var (stream, fileName) in images)
        {
            try
            {
                // Reset stream position
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }
                
                var url = await UploadImageAsync(stream, fileName);
                uploadedUrls.Add(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image: {FileName}", fileName);
                // Continue with other images even if one fails
            }
        }

        return uploadedUrls;
    }

    public async Task<bool> DeleteImageAsync(string publicId)
    {
        try
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);
            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image: {PublicId}", publicId);
            return false;
        }
    }
}
