using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Payment;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
using System.IO;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService, 
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Tạo link nạp tiền PayOS
    /// </summary>
    [HttpPost("deposit")]
    [Authorize]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositRequest request)
    {
        try
        {
            // #region agent log
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var allClaims = User.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList();
            WriteDebugLog("PaymentController.cs:36", "CreateDeposit entry", new { userId, claims = allClaims, amount = request.Amount }, "C", "run1");
            // #endregion
            
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập" });
            }

            var result = await _paymentService.CreateDepositLinkAsync(userId, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            // #region agent log
            WriteDebugLog("PaymentController.cs:51", "Exception in CreateDeposit", new { error = ex.Message, stackTrace = ex.StackTrace }, "C", "run1");
            // #endregion
            _logger.LogError(ex, "Error creating deposit link");
            return BadRequest(new { message = ex.Message });
        }
    }
    
    // #region agent log
    private const string DEBUG_LOG_PATH = @"d:\DauGia\.cursor\debug.log";
    private void WriteDebugLog(string location, string message, object? data = null, string? hypothesisId = null, string? runId = null)
    {
        try
        {
            var logEntry = new
            {
                id = $"log_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                location = location,
                message = message,
                data = data,
                runId = runId ?? "run1",
                hypothesisId = hypothesisId
            };
            var json = System.Text.Json.JsonSerializer.Serialize(logEntry);
            System.IO.File.AppendAllText(DEBUG_LOG_PATH, json + Environment.NewLine);
        }
        catch { }
    }
    // #endregion

    /// <summary>
    /// Webhook từ PayOS khi thanh toán thành công
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook([FromBody] PayOsWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Received PayOS webhook: {Code}", payload.Code);
            
            var result = await _paymentService.HandleWebhookAsync(payload);
            
            if (result)
            {
                return Ok(new { success = true });
            }
            
            return BadRequest(new { success = false, message = "Failed to process webhook" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PayOS webhook");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái nạp tiền
    /// </summary>
    [HttpGet("deposit/{orderCode}")]
    [Authorize]
    public async Task<IActionResult> GetDepositStatus(long orderCode)
    {
        try
        {
            var result = await _paymentService.GetDepositStatusAsync(orderCode);
            
            if (result == null)
            {
                return NotFound(new { message = "Không tìm thấy giao dịch" });
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deposit status");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy thông tin ví của user hiện tại
    /// </summary>
    [HttpGet("wallet")]
    [Authorize]
    public async Task<IActionResult> GetWallet()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập" });
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Người dùng không tồn tại" });
            }

            return Ok(new 
            { 
                availableBalance = user.AvailableBalance,
                escrowBalance = user.EscrowBalance,
                totalBalance = user.AvailableBalance + user.EscrowBalance
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet info");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy lịch sử giao dịch của user
    /// </summary>
    [HttpGet("transactions")]
    [Authorize]
    public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập" });
            }

            var transactions = await _transactionRepository.GetByUserIdAsync(userId);
            
            // Pagination
            var totalCount = transactions.Count;
            var paginatedTransactions = transactions
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(t => new 
                {
                    id = t.Id,
                    type = (int)t.Type,
                    amount = t.Amount,
                    description = t.Description,
                    balanceBefore = t.BalanceBefore,
                    balanceAfter = t.BalanceAfter,
                    relatedAuctionId = t.RelatedAuctionId,
                    payOsOrderCode = t.PayOsOrderCode,
                    status = (int)t.Status,
                    createdAt = t.CreatedAt
                })
                .ToList();

            return Ok(new 
            { 
                transactions = paginatedTransactions,
                totalCount = totalCount,
                page = page,
                limit = limit,
                totalPages = (int)Math.Ceiling(totalCount / (double)limit)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions");
            return BadRequest(new { message = ex.Message });
        }
    }
}
