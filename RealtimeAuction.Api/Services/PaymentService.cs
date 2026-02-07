using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RealtimeAuction.Api.Dtos.Payment;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Settings;

namespace RealtimeAuction.Api.Services;

public class PaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly PayOsSettings _settings;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMongoCollection<User> _users;
    private readonly ILogger<PaymentService> _logger;
    private const string PAYOS_API_URL = "https://api-merchant.payos.vn/v2/payment-requests";

    public PaymentService(
        IOptions<PayOsSettings> settings,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IMongoDatabase database,
        ILogger<PaymentService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _users = database.GetCollection<User>("Users");
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("PayOS");
    }

    public async Task<DepositResponse> CreateDepositLinkAsync(string userId, CreateDepositRequest request)
    {
        // Use direct MongoDB query like UsersController
        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        
        if (user == null)
        {
            _logger.LogError("User not found for userId: {UserId}", userId);
            throw new Exception("Người dùng không tồn tại");
        }

        // Tạo orderCode unique
        var orderCode = long.Parse(DateTime.UtcNow.ToString("yyMMddHHmmss") + new Random().Next(100, 999).ToString());

        var paymentData = new
        {
            orderCode = orderCode,
            amount = (int)request.Amount,
            description = request.Description ?? $"Nap tien {request.Amount:N0}d",
            items = new[]
            {
                new { name = $"Nap tien vi - {user.Email}", quantity = 1, price = (int)request.Amount }
            },
            cancelUrl = _settings.CancelUrl,
            returnUrl = _settings.ReturnUrl
        };

        // Tạo signature
        var signatureData = $"amount={paymentData.amount}&cancelUrl={paymentData.cancelUrl}&description={paymentData.description}&orderCode={paymentData.orderCode}&returnUrl={paymentData.returnUrl}";
        var signature = ComputeHmacSha256(signatureData, _settings.ChecksumKey);

        var requestBody = new
        {
            orderCode = paymentData.orderCode,
            amount = paymentData.amount,
            description = paymentData.description,
            items = paymentData.items,
            cancelUrl = paymentData.cancelUrl,
            returnUrl = paymentData.returnUrl,
            signature = signature
        };

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-client-id", _settings.ClientId);
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(PAYOS_API_URL, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("PayOS Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"PayOS Error: {responseContent}");
            }

            var payosResponse = JsonSerializer.Deserialize<PayOsCreatePaymentResponse>(responseContent);
            
            if (payosResponse?.code != "00")
            {
                throw new Exception($"PayOS Error: {payosResponse?.desc}");
            }

            // Lưu transaction pending
            var transaction = new Transaction
            {
                UserId = userId,
                Type = TransactionType.Deposit,
                Amount = request.Amount,
                Description = request.Description ?? "Nạp tiền qua PayOS (pending)",
                PayOsOrderCode = orderCode,
                BalanceBefore = user.AvailableBalance,
                BalanceAfter = user.AvailableBalance,
                CreatedAt = DateTime.UtcNow
            };
            await _transactionRepository.CreateAsync(transaction);

            _logger.LogInformation("Created deposit link for user {UserId}, orderCode: {OrderCode}", userId, orderCode);

            return new DepositResponse
            {
                OrderCode = orderCode,
                Amount = request.Amount,
                CheckoutUrl = payosResponse?.data?.checkoutUrl ?? "",
                QrCode = payosResponse?.data?.qrCode ?? "",
                Status = "PENDING"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment link for user {UserId}", userId);
            throw new Exception($"Không thể tạo link thanh toán: {ex.Message}");
        }
    }

    public async Task<bool> HandleWebhookAsync(PayOsWebhookPayload payload)
    {
        if (!payload.Success || payload.Data == null)
        {
            _logger.LogWarning("Webhook received but not successful: {Code} - {Desc}", payload.Code, payload.Desc);
            return false;
        }

        var orderCode = payload.Data.OrderCode;
        var amount = payload.Data.Amount;

        _logger.LogInformation("Processing webhook for orderCode: {OrderCode}, amount: {Amount}", orderCode, amount);

        // Tìm transaction pending bằng orderCode
        var pendingTransactions = await _transactionRepository.GetByPayOsOrderCodeAsync(orderCode);
        var pendingTransaction = pendingTransactions.FirstOrDefault();
        
        if (pendingTransaction == null)
        {
            _logger.LogWarning("No pending transaction found for orderCode: {OrderCode}", orderCode);
            return false;
        }

        // Cập nhật balance của user
        var user = await _userRepository.GetByIdAsync(pendingTransaction.UserId);
        if (user == null)
        {
            _logger.LogError("User not found for transaction: {TransactionId}", pendingTransaction.Id);
            return false;
        }

        // Kiểm tra xem transaction đã được xử lý chưa (tránh duplicate)
        if (pendingTransaction.Status == TransactionStatus.Completed)
        {
            _logger.LogInformation("Transaction {TransactionId} already processed for orderCode: {OrderCode}", 
                pendingTransaction.Id, orderCode);
            return true;
        }

        var balanceBefore = user.AvailableBalance;
        user.AvailableBalance += amount;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Update transaction pending thành completed
        pendingTransaction.Status = TransactionStatus.Completed;
        pendingTransaction.Description = $"Nạp tiền thành công - PayOS #{orderCode}";
        pendingTransaction.BalanceBefore = balanceBefore;
        pendingTransaction.BalanceAfter = user.AvailableBalance;
        pendingTransaction.UpdatedAt = DateTime.UtcNow;
        await _transactionRepository.UpdateAsync(pendingTransaction);

        _logger.LogInformation("Deposit successful for user {UserId}, amount: {Amount}, new balance: {Balance}", 
            user.Id, amount, user.AvailableBalance);

        return true;
    }

    public async Task<DepositResponse?> GetDepositStatusAsync(long orderCode)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-client-id", _settings.ClientId);
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);

            var response = await _httpClient.GetAsync($"{PAYOS_API_URL}/{orderCode}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payosResponse = JsonSerializer.Deserialize<PayOsGetPaymentResponse>(responseContent);
            var payosStatus = payosResponse?.data?.status ?? "UNKNOWN";
            var payosAmount = payosResponse?.data?.amount ?? 0;
            
            // Nếu PayOS đã thanh toán (PAID) nhưng transaction vẫn pending, tự động update
            if (payosStatus == "PAID" && payosAmount > 0)
            {
                var pendingTransactions = await _transactionRepository.GetByPayOsOrderCodeAsync(orderCode);
                var pendingTransaction = pendingTransactions
                    .Where(t => t.Status == TransactionStatus.Pending && t.Type == TransactionType.Deposit)
                    .FirstOrDefault();
                
                if (pendingTransaction != null)
                {
                    _logger.LogInformation("Auto-updating pending transaction {TransactionId} for orderCode: {OrderCode}", 
                        pendingTransaction.Id, orderCode);
                    
                    // Use direct MongoDB collection like CreateDepositLinkAsync to avoid collection name issues
                    var user = await _users.Find(u => u.Id == pendingTransaction.UserId).FirstOrDefaultAsync();
                    
                    if (user != null)
                    {
                        var balanceBefore = user.AvailableBalance;
                        user.AvailableBalance += payosAmount;
                        user.UpdatedAt = DateTime.UtcNow;
                        await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

                        pendingTransaction.Status = TransactionStatus.Completed;
                        pendingTransaction.Description = $"Nạp tiền thành công - PayOS #{orderCode}";
                        pendingTransaction.BalanceBefore = balanceBefore;
                        pendingTransaction.BalanceAfter = user.AvailableBalance;
                        pendingTransaction.UpdatedAt = DateTime.UtcNow;
                        await _transactionRepository.UpdateAsync(pendingTransaction);

                        _logger.LogInformation("Auto-updated deposit for user {UserId}, amount: {Amount}, new balance: {Balance}", 
                            user.Id, payosAmount, user.AvailableBalance);
                    }
                }
            }
            
            return new DepositResponse
            {
                OrderCode = orderCode,
                Amount = payosAmount,
                CheckoutUrl = "",
                QrCode = "",
                Status = payosStatus
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status for orderCode: {OrderCode}", orderCode);
            return null;
        }
    }

    public bool VerifySignature(string data, string signature)
    {
        var computedSignature = ComputeHmacSha256(data, _settings.ChecksumKey);
        return computedSignature.Equals(signature, StringComparison.OrdinalIgnoreCase);
    }

    private string ComputeHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}

// PayOS API Response classes
public class PayOsCreatePaymentResponse
{
    public string code { get; set; } = null!;
    public string desc { get; set; } = null!;
    public PayOsPaymentData? data { get; set; }
}

public class PayOsPaymentData
{
    public string bin { get; set; } = null!;
    public string accountNumber { get; set; } = null!;
    public string accountName { get; set; } = null!;
    public int amount { get; set; }
    public string description { get; set; } = null!;
    public long orderCode { get; set; }
    public string currency { get; set; } = null!;
    public string paymentLinkId { get; set; } = null!;
    public string status { get; set; } = null!;
    public string checkoutUrl { get; set; } = null!;
    public string qrCode { get; set; } = null!;
}

public class PayOsGetPaymentResponse
{
    public string code { get; set; } = null!;
    public string desc { get; set; } = null!;
    public PayOsPaymentStatusData? data { get; set; }
}

public class PayOsPaymentStatusData
{
    public long orderCode { get; set; }
    public int amount { get; set; }
    public int amountPaid { get; set; }
    public int amountRemaining { get; set; }
    public string status { get; set; } = null!;
}
