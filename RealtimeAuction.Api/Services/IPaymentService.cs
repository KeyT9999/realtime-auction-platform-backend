using RealtimeAuction.Api.Dtos.Payment;

namespace RealtimeAuction.Api.Services;

public interface IPaymentService
{
    /// <summary>
    /// Tạo link nạp tiền PayOS
    /// </summary>
    Task<DepositResponse> CreateDepositLinkAsync(string userId, CreateDepositRequest request);
    
    /// <summary>
    /// Xử lý webhook từ PayOS khi thanh toán thành công
    /// </summary>
    Task<bool> HandleWebhookAsync(PayOsWebhookPayload payload);
    
    /// <summary>
    /// Lấy thông tin deposit theo orderCode
    /// </summary>
    Task<DepositResponse?> GetDepositStatusAsync(long orderCode);
    
    /// <summary>
    /// Verify signature từ PayOS
    /// </summary>
    bool VerifySignature(string data, string signature);
}
