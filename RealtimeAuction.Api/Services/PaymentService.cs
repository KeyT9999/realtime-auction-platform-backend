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
    private static readonly TimeSpan DepositClaimTimeout = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly PayOsSettings _settings;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Transaction> _transactions;
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
        _transactions = database.GetCollection<Transaction>("Transactions");
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("PayOS");
    }

    public async Task<DepositResponse> CreateDepositLinkAsync(string userId, CreateDepositRequest request)
    {
        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            _logger.LogError("User not found for userId: {UserId}", userId);
            throw new Exception("NgÆ°á»i dÃ¹ng khÃ´ng tá»“n táº¡i");
        }

        var randomPart = RandomNumberGenerator.GetInt32(100, 999);
        var orderCode = long.Parse(DateTime.UtcNow.ToString("yyMMddHHmmss") + randomPart.ToString());

        var paymentData = new
        {
            orderCode,
            amount = (int)request.Amount,
            description = request.Description ?? $"Nap tien {request.Amount:N0}d",
            items = new[]
            {
                new { name = $"Nap tien vi - {user.Email}", quantity = 1, price = (int)request.Amount }
            },
            cancelUrl = _settings.CancelUrl,
            returnUrl = _settings.ReturnUrl
        };

        var signatureData =
            $"amount={paymentData.amount}&cancelUrl={paymentData.cancelUrl}&description={paymentData.description}&orderCode={paymentData.orderCode}&returnUrl={paymentData.returnUrl}";
        var signature = ComputeHmacSha256(signatureData, _settings.ChecksumKey);

        var requestBody = new
        {
            orderCode = paymentData.orderCode,
            amount = paymentData.amount,
            description = paymentData.description,
            items = paymentData.items,
            cancelUrl = paymentData.cancelUrl,
            returnUrl = paymentData.returnUrl,
            signature
        };

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, PAYOS_API_URL);
            httpRequest.Headers.Add("x-client-id", _settings.ClientId);
            httpRequest.Headers.Add("x-api-key", _settings.ApiKey);
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
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

            var transaction = new Transaction
            {
                UserId = userId,
                Type = TransactionType.Deposit,
                Amount = request.Amount,
                Description = request.Description ?? "Náº¡p tiá»n qua PayOS (pending)",
                PayOsOrderCode = orderCode,
                BalanceBefore = user.AvailableBalance,
                BalanceAfter = user.AvailableBalance,
                Status = TransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _transactionRepository.CreateAsync(transaction);

            _logger.LogInformation("Created deposit link for user {UserId}, orderCode: {OrderCode}", userId, orderCode);

            return new DepositResponse
            {
                OrderCode = orderCode,
                Amount = request.Amount,
                CheckoutUrl = payosResponse?.data?.checkoutUrl ?? string.Empty,
                QrCode = payosResponse?.data?.qrCode ?? string.Empty,
                Status = "PENDING"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment link for user {UserId}", userId);
            throw new Exception($"KhÃ´ng thá»ƒ táº¡o link thanh toÃ¡n: {ex.Message}");
        }
    }

    public bool VerifyWebhookSignature(PayOsWebhookPayload payload)
    {
        if (payload.Data == null || string.IsNullOrEmpty(payload.Signature))
        {
            return false;
        }

        var data = payload.Data;
        var sortedParams = new SortedDictionary<string, string>
        {
            ["accountNumber"] = data.AccountNumber ?? string.Empty,
            ["amount"] = ((int)data.Amount).ToString(),
            ["code"] = data.Code ?? string.Empty,
            ["counterAccountBankId"] = data.CounterAccountBankId ?? string.Empty,
            ["counterAccountBankName"] = data.CounterAccountBankName ?? string.Empty,
            ["counterAccountName"] = data.CounterAccountName ?? string.Empty,
            ["counterAccountNumber"] = data.CounterAccountNumber ?? string.Empty,
            ["desc"] = data.Desc ?? string.Empty,
            ["description"] = data.Description ?? string.Empty,
            ["orderCode"] = data.OrderCode.ToString(),
            ["paymentLinkId"] = data.PaymentLinkId ?? string.Empty,
            ["reference"] = data.Reference ?? string.Empty,
            ["transactionDateTime"] = data.TransactionDateTime ?? string.Empty,
            ["virtualAccountName"] = data.VirtualAccountName ?? string.Empty,
            ["virtualAccountNumber"] = data.VirtualAccountNumber ?? string.Empty
        };

        var signatureData = string.Join("&", sortedParams.Select(kv => $"{kv.Key}={kv.Value}"));
        return VerifySignature(signatureData, payload.Signature);
    }

    public async Task<bool> HandleWebhookAsync(PayOsWebhookPayload payload)
    {
        if (!VerifyWebhookSignature(payload))
        {
            _logger.LogWarning("Invalid webhook signature received");
            return false;
        }

        if (!payload.Success || payload.Data == null)
        {
            _logger.LogWarning("Webhook received but not successful: {Code} - {Desc}", payload.Code, payload.Desc);
            return false;
        }

        var orderCode = payload.Data.OrderCode;
        var amount = payload.Data.Amount;

        _logger.LogInformation("Processing webhook for orderCode: {OrderCode}, amount: {Amount}", orderCode, amount);
        return await ProcessSuccessfulDepositAsync(orderCode, amount, "webhook");
    }

    public async Task<DepositResponse?> GetDepositStatusAsync(string userId, long orderCode)
    {
        try
        {
            var localTransaction = await _transactions
                .Find(t => t.PayOsOrderCode == orderCode && t.UserId == userId && t.Type == TransactionType.Deposit)
                .SortByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (localTransaction == null)
            {
                return null;
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{PAYOS_API_URL}/{orderCode}");
            httpRequest.Headers.Add("x-client-id", _settings.ClientId);
            httpRequest.Headers.Add("x-api-key", _settings.ApiKey);

            var response = await _httpClient.SendAsync(httpRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payosResponse = JsonSerializer.Deserialize<PayOsGetPaymentResponse>(responseContent);
            var payosStatus = payosResponse?.data?.status ?? "UNKNOWN";
            var payosAmount = payosResponse?.data?.amount ?? 0;

            if (payosStatus == "PAID" && payosAmount > 0)
            {
                await ProcessSuccessfulDepositAsync(orderCode, payosAmount, "polling");
            }

            return new DepositResponse
            {
                OrderCode = orderCode,
                Amount = payosAmount,
                CheckoutUrl = string.Empty,
                QrCode = string.Empty,
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

    private async Task<bool> ProcessSuccessfulDepositAsync(long orderCode, decimal amount, string source)
    {
        var transaction = await _transactions
            .Find(t => t.PayOsOrderCode == orderCode && t.Type == TransactionType.Deposit)
            .SortByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (transaction == null)
        {
            _logger.LogWarning("No deposit transaction found for orderCode: {OrderCode}", orderCode);
            return false;
        }

        if (transaction.Status == TransactionStatus.Completed)
        {
            _logger.LogInformation("Deposit transaction {TransactionId} already processed for orderCode {OrderCode}", transaction.Id, orderCode);
            return true;
        }

        var claim = await TryAcquireDepositClaimAsync(transaction.Id!);
        if (claim.AlreadyCompleted)
        {
            return true;
        }

        if (!claim.Acquired || string.IsNullOrWhiteSpace(claim.Token))
        {
            _logger.LogInformation("Deposit transaction {TransactionId} is being processed by another request", transaction.Id);
            return false;
        }

        try
        {
            var creditResult = await ApplyDepositCreditAsync(transaction.UserId, orderCode, amount);
            if (!creditResult.Success)
            {
                _logger.LogError("Could not apply deposit credit for transaction {TransactionId}", transaction.Id);
                await ReleaseDepositClaimAsync(transaction.Id!, claim.Token);
                return false;
            }

            await FinalizeDepositTransactionAsync(
                transaction.Id!,
                claim.Token,
                creditResult.BalanceBefore,
                creditResult.BalanceAfter,
                orderCode);

            _logger.LogInformation(
                "Deposit processed via {Source} for user {UserId}, orderCode {OrderCode}, amount {Amount}",
                source,
                transaction.UserId,
                orderCode,
                amount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing deposit {OrderCode}", orderCode);
            await ReleaseDepositClaimAsync(transaction.Id!, claim.Token);
            return false;
        }
    }

    private async Task<DepositClaimResult> TryAcquireDepositClaimAsync(string transactionId)
    {
        var claimToken = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var expirationCutoff = now.Subtract(DepositClaimTimeout);
        var filterBuilder = Builders<Transaction>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.Eq(t => t.Id, transactionId),
            filterBuilder.Eq(t => t.Status, TransactionStatus.Pending),
            filterBuilder.Or(
                filterBuilder.Eq(t => t.ProcessingToken, null),
                filterBuilder.Lt(t => t.ProcessingStartedAt, expirationCutoff)));

        var update = Builders<Transaction>.Update
            .Set(t => t.ProcessingToken, claimToken)
            .Set(t => t.ProcessingStartedAt, now)
            .Set(t => t.UpdatedAt, now);

        var result = await _transactions.UpdateOneAsync(filter, update);
        if (result.ModifiedCount == 1)
        {
            return new DepositClaimResult(true, false, claimToken);
        }

        var current = await _transactions.Find(t => t.Id == transactionId).FirstOrDefaultAsync();
        return new DepositClaimResult(false, current?.Status == TransactionStatus.Completed, null);
    }

    private async Task<DepositCreditResult> ApplyDepositCreditAsync(string userId, long orderCode, decimal amount)
    {
        var now = DateTime.UtcNow;
        var filterBuilder = Builders<User>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.Eq(u => u.Id, userId),
            filterBuilder.Not(filterBuilder.AnyEq(u => u.AppliedDepositOrderCodes, orderCode)));

        var update = Builders<User>.Update
            .Inc(u => u.AvailableBalance, amount)
            .AddToSet(u => u.AppliedDepositOrderCodes, orderCode)
            .Set(u => u.UpdatedAt, now);

        var updatedUser = await _users.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<User>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (updatedUser != null)
        {
            return new DepositCreditResult(true, updatedUser.AvailableBalance - amount, updatedUser.AvailableBalance);
        }

        var existingUser = await _userRepository.GetByIdAsync(userId);
        if (existingUser == null)
        {
            return new DepositCreditResult(false, 0, 0);
        }

        if (existingUser.AppliedDepositOrderCodes.Contains(orderCode))
        {
            return new DepositCreditResult(true, existingUser.AvailableBalance - amount, existingUser.AvailableBalance);
        }

        return new DepositCreditResult(false, 0, 0);
    }

    private async Task FinalizeDepositTransactionAsync(
        string transactionId,
        string claimToken,
        decimal balanceBefore,
        decimal balanceAfter,
        long orderCode)
    {
        var filterBuilder = Builders<Transaction>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.Eq(t => t.Id, transactionId),
            filterBuilder.Eq(t => t.Status, TransactionStatus.Pending),
            filterBuilder.Eq(t => t.ProcessingToken, claimToken));

        var update = Builders<Transaction>.Update
            .Set(t => t.Status, TransactionStatus.Completed)
            .Set(t => t.Description, $"Náº¡p tiá»n thÃ nh cÃ´ng - PayOS #{orderCode}")
            .Set(t => t.BalanceBefore, balanceBefore)
            .Set(t => t.BalanceAfter, balanceAfter)
            .Set(t => t.ProcessedAt, DateTime.UtcNow)
            .Set(t => t.UpdatedAt, DateTime.UtcNow)
            .Unset(t => t.ProcessingToken)
            .Unset(t => t.ProcessingStartedAt);

        var result = await _transactions.UpdateOneAsync(filter, update);
        if (result.ModifiedCount == 0)
        {
            var current = await _transactions.Find(t => t.Id == transactionId).FirstOrDefaultAsync();
            if (current?.Status != TransactionStatus.Completed)
            {
                throw new InvalidOperationException($"Could not finalize deposit transaction {transactionId}.");
            }
        }
    }

    private async Task ReleaseDepositClaimAsync(string transactionId, string claimToken)
    {
        var filter = Builders<Transaction>.Filter.And(
            Builders<Transaction>.Filter.Eq(t => t.Id, transactionId),
            Builders<Transaction>.Filter.Eq(t => t.Status, TransactionStatus.Pending),
            Builders<Transaction>.Filter.Eq(t => t.ProcessingToken, claimToken));

        var update = Builders<Transaction>.Update
            .Unset(t => t.ProcessingToken)
            .Unset(t => t.ProcessingStartedAt)
            .Set(t => t.UpdatedAt, DateTime.UtcNow);

        await _transactions.UpdateOneAsync(filter, update);
    }

    private string ComputeHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private sealed record DepositClaimResult(bool Acquired, bool AlreadyCompleted, string? Token);

    private sealed record DepositCreditResult(bool Success, decimal BalanceBefore, decimal BalanceAfter);
}

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
