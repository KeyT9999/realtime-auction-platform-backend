using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RealtimeAuction.Api.Settings;

namespace RealtimeAuction.Api.Services;

public class GeminiService : IGeminiService
{
    private const string Prompt = """
        Bạn là chuyên gia thẩm định tài sản cho nền tảng đấu giá trực tuyến tại Việt Nam.
        Hãy phân tích kỹ các ảnh sản phẩm và trả về DUY NHẤT một object JSON hợp lệ theo schema sau:
        {
          "thong_tin_chung": {
            "ten_san_pham": { "gia_tri": "", "ghi_chu": "" },
            "mo_ta_tom_tat": { "gia_tri": "", "ghi_chu": "" },
            "danh_muc_san_pham": { "gia_tri": "", "ghi_chu": "" },
            "tinh_trang_san_pham": { "gia_tri": "", "ghi_chu": "" },
            "thuong_hieu": { "gia_tri": "", "ghi_chu": "" },
            "mau_ma_phien_ban": { "gia_tri": "", "ghi_chu": "" },
            "nam_san_xuat": { "gia_tri": null, "ghi_chu": "" }
          },
          "thong_tin_dau_gia": {
            "gia_khoi_diem": { "gia_tri": null, "ghi_chu": "" },
            "buoc_gia_toi_thieu": { "gia_tri": null, "ghi_chu": "" }
          }
        }

        Yêu cầu:
        - Tất cả nội dung bằng tiếng Việt.
        - Ưu tiên nhận diện đúng tên/model/brand/tình trạng từ hình ảnh và chữ trên tem/hộp.
        - Mô tả phải trung thực, không cường điệu.
        - Nếu không chắc, để giá trị rỗng hoặc null thay vì đoán bừa.
        - Không bọc JSON trong markdown fence.
        """;

    private readonly GeminiSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        IOptions<GeminiSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<GeminiService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient("Gemini");
        _logger = logger;
    }

    public async Task<string> AnalyzeProductImagesAsync(
        IReadOnlyCollection<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured on the server.");
        }

        var parts = new List<object> { new { text = Prompt } };
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);

            parts.Add(new
            {
                inlineData = new
                {
                    data = Convert.ToBase64String(memoryStream.ToArray()),
                    mimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType
                }
            });
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = parts.ToArray()
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";
        using var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini request failed. Status: {StatusCode}. Body: {Body}", response.StatusCode, responseContent);
            throw new InvalidOperationException("Gemini request failed.");
        }

        using var document = JsonDocument.Parse(responseContent);
        var text = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini returned an empty response.");
        }

        return NormalizeJsonPayload(text);
    }

    private static string NormalizeJsonPayload(string rawText)
    {
        var trimmed = rawText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var lines = trimmed.Split('\n');
            if (lines.Length >= 3)
            {
                trimmed = string.Join('\n', lines.Skip(1).SkipLast(1)).Trim();
            }
        }

        // Gemini occasionally returns a BOM or extra whitespace around JSON.
        return trimmed.Trim('\uFEFF', '\u200B', '\r', '\n', ' ', '\t');
    }
}
