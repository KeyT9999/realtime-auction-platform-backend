using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Withdrawal;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WithdrawalController : ControllerBase
{
    private readonly IWithdrawalService _withdrawalService;
    private readonly ILogger<WithdrawalController> _logger;

    public WithdrawalController(
        IWithdrawalService withdrawalService,
        ILogger<WithdrawalController> logger)
    {
        _withdrawalService = withdrawalService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo yêu cầu rút tiền
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateWithdrawal([FromBody] CreateWithdrawalRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            var result = await _withdrawalService.CreateWithdrawalAsync(userId, request);
            return Ok(new
            {
                message = "Yêu cầu rút tiền đã được tạo. Vui lòng nhập OTP đã gửi đến email",
                withdrawalId = result.Id,
                amount = result.Amount,
                processingFee = result.ProcessingFee,
                finalAmount = result.FinalAmount,
                bankInfo = $"****{result.BankSnapshot?.AccountNumberLast4} - {result.BankSnapshot?.BankName}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating withdrawal");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Xác nhận OTP rút tiền
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyWithdrawalOtpRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            var result = await _withdrawalService.VerifyOtpAsync(userId, request);
            return Ok(new
            {
                message = "Xác nhận OTP thành công. Yêu cầu đang chờ admin duyệt",
                withdrawalId = result.Id,
                status = (int)result.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying withdrawal OTP");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gửi lại OTP
    /// </summary>
    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendWithdrawalOtpRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            await _withdrawalService.ResendOtpAsync(userId, request);
            return Ok(new { message = "Đã gửi lại mã OTP đến email của bạn" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending withdrawal OTP");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Hủy yêu cầu rút tiền
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelWithdrawal(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            await _withdrawalService.CancelWithdrawalAsync(userId, id);
            return Ok(new { message = "Đã hủy yêu cầu rút tiền. Số tiền đã được hoàn vào ví" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling withdrawal");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy danh sách yêu cầu rút tiền của user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyWithdrawals()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            var result = await _withdrawalService.GetUserWithdrawalsAsync(userId);
            return Ok(new
            {
                withdrawals = result.Select(w => new
                {
                    id = w.Id,
                    amount = w.Amount,
                    processingFee = w.ProcessingFee,
                    finalAmount = w.FinalAmount,
                    status = (int)w.Status,
                    statusText = GetStatusText(w.Status),
                    bankInfo = $"****{w.BankSnapshot?.AccountNumberLast4} - {w.BankSnapshot?.BankName}",
                    isOtpVerified = w.IsOtpVerified,
                    rejectionReason = w.RejectionReason,
                    transactionCode = w.TransactionCode,
                    createdAt = w.CreatedAt,
                    completedAt = w.CompletedAt,
                    cancelledAt = w.CancelledAt,
                    cancelReason = w.CancelReason
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting withdrawals");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy chi tiết yêu cầu rút tiền
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetWithdrawalDetail(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            var w = await _withdrawalService.GetWithdrawalByIdAsync(id);
            if (w == null) return NotFound(new { message = "Yêu cầu không tồn tại" });
            if (w.UserId != userId) return Forbid();

            return Ok(new
            {
                id = w.Id,
                amount = w.Amount,
                processingFee = w.ProcessingFee,
                finalAmount = w.FinalAmount,
                status = (int)w.Status,
                statusText = GetStatusText(w.Status),
                bankInfo = $"****{w.BankSnapshot?.AccountNumberLast4} - {w.BankSnapshot?.BankName}",
                bankSnapshot = w.BankSnapshot,
                isOtpVerified = w.IsOtpVerified,
                otpVerifiedAt = w.OtpVerifiedAt,
                approvedAt = w.ApprovedAt,
                rejectedAt = w.RejectedAt,
                rejectionReason = w.RejectionReason,
                transactionCode = w.TransactionCode,
                transactionProof = w.TransactionProof,
                completedAt = w.CompletedAt,
                cancelledAt = w.CancelledAt,
                cancelReason = w.CancelReason,
                createdAt = w.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting withdrawal detail");
            return BadRequest(new { message = ex.Message });
        }
    }

    private static string GetStatusText(Models.Enums.WithdrawalStatus status)
    {
        return status switch
        {
            Models.Enums.WithdrawalStatus.Pending => "Chờ xác nhận OTP",
            Models.Enums.WithdrawalStatus.OtpVerified => "Chờ admin duyệt",
            Models.Enums.WithdrawalStatus.Processing => "Đang xử lý",
            Models.Enums.WithdrawalStatus.Completed => "Hoàn tất",
            Models.Enums.WithdrawalStatus.Rejected => "Bị từ chối",
            Models.Enums.WithdrawalStatus.Cancelled => "Đã hủy",
            _ => "Không xác định"
        };
    }
}
