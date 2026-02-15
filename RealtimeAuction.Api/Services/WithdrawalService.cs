using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using RealtimeAuction.Api.Dtos.Withdrawal;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Services;

public class WithdrawalService : IWithdrawalService
{
    private readonly IWithdrawalRepository _withdrawalRepository;
    private readonly IBankAccountRepository _bankAccountRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<WithdrawalService> _logger;

    // Constants
    private const decimal MIN_WITHDRAWAL = 50_000m;
    private const decimal DAILY_LIMIT = 10_000_000m;
    private const int MAX_REQUESTS_PER_DAY = 5;
    private const decimal FEE_PERCENTAGE = 0m; // 0% fee
    private const int OTP_EXPIRY_MINUTES = 10;
    private const int MAX_OTP_ATTEMPTS = 5;

    public WithdrawalService(
        IWithdrawalRepository withdrawalRepository,
        IBankAccountRepository bankAccountRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IEmailService emailService,
        ILogger<WithdrawalService> logger)
    {
        _withdrawalRepository = withdrawalRepository;
        _bankAccountRepository = bankAccountRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<WithdrawalRequest> CreateWithdrawalAsync(string userId, CreateWithdrawalRequest request)
    {
        // 1. Validate user
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new Exception("Người dùng không tồn tại");
        if (user.IsLocked) throw new Exception("Tài khoản đang bị khóa");

        // 2. Validate amount
        if (request.Amount < MIN_WITHDRAWAL)
            throw new Exception($"Số tiền rút tối thiểu là {MIN_WITHDRAWAL:N0} VND");
        if (user.AvailableBalance < request.Amount)
            throw new Exception("Số dư khả dụng không đủ");

        // 3. Check daily limit
        var today = DateTime.UtcNow.Date;
        var todayRequests = await _withdrawalRepository.GetByUserIdAndDateRangeAsync(
            userId, today, today.AddDays(1));
        
        if (todayRequests.Count >= MAX_REQUESTS_PER_DAY)
            throw new Exception($"Đã vượt quá giới hạn {MAX_REQUESTS_PER_DAY} yêu cầu rút tiền trong ngày");

        var todayTotal = todayRequests.Sum(r => r.Amount);
        if (todayTotal + request.Amount > DAILY_LIMIT)
            throw new Exception($"Đã vượt quá giới hạn rút tiền trong ngày ({DAILY_LIMIT:N0} VND)");

        // 4. Validate bank account
        var bankAccount = await _bankAccountRepository.GetByIdAsync(request.BankAccountId);
        if (bankAccount == null || bankAccount.UserId != userId)
            throw new Exception("Tài khoản ngân hàng không tồn tại");

        // 5. Calculate fee
        var processingFee = request.Amount * FEE_PERCENTAGE;
        var finalAmount = request.Amount - processingFee;

        // 6. Generate OTP
        var otpCode = GenerateOtp();
        var otpHash = HashOtp(otpCode);

        // 7. Hold balance: Available → HeldBalance (for withdrawal)
        user.AvailableBalance -= request.Amount;
        user.HeldBalance += request.Amount;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // 8. Create Transaction record (single record, will update status)
        var transaction = new Transaction
        {
            UserId = userId,
            Type = TransactionType.Withdraw,
            Amount = -request.Amount,
            Description = "Yêu cầu rút tiền - chờ xác nhận OTP",
            Status = TransactionStatus.Pending,
            BalanceBefore = user.AvailableBalance + request.Amount,
            BalanceAfter = user.AvailableBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(transaction);

        // 9. Create WithdrawalRequest
        var withdrawal = new WithdrawalRequest
        {
            UserId = userId,
            BankAccountId = request.BankAccountId,
            Amount = request.Amount,
            ProcessingFee = processingFee,
            FinalAmount = finalAmount,
            Status = WithdrawalStatus.Pending,
            OtpHash = otpHash,
            OtpExpiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES),
            OtpAttempts = 0,
            IsOtpVerified = false,
            RelatedTransactionId = transaction.Id,
            BankSnapshot = new BankAccountSnapshot
            {
                BankName = bankAccount.BankName,
                AccountNumberLast4 = bankAccount.AccountNumber.Length >= 4 
                    ? bankAccount.AccountNumber[^4..] 
                    : bankAccount.AccountNumber,
                AccountName = bankAccount.AccountName
            },
            CreatedAt = DateTime.UtcNow
        };
        await _withdrawalRepository.CreateAsync(withdrawal);

        // 10. Send OTP email
        try
        {
            await _emailService.SendWithdrawalOtpEmailAsync(user.Email, user.FullName, otpCode);
            _logger.LogInformation("Sent withdrawal OTP to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send withdrawal OTP email to {Email}", user.Email);
            // Don't fail the request, OTP is still available via logs
        }

        _logger.LogInformation("Created withdrawal request {WithdrawalId} for user {UserId}, amount: {Amount}", 
            withdrawal.Id, userId, request.Amount);

        return withdrawal;
    }

    public async Task<WithdrawalRequest> VerifyOtpAsync(string userId, VerifyWithdrawalOtpRequest request)
    {
        var withdrawal = await _withdrawalRepository.GetByIdAsync(request.WithdrawalId);
        if (withdrawal == null) throw new Exception("Yêu cầu rút tiền không tồn tại");
        if (withdrawal.UserId != userId) throw new Exception("Không có quyền truy cập");
        if (withdrawal.Status != WithdrawalStatus.Pending)
            throw new Exception("Yêu cầu không ở trạng thái chờ xác nhận OTP");

        // Check OTP expiry
        if (withdrawal.OtpExpiresAt.HasValue && withdrawal.OtpExpiresAt.Value < DateTime.UtcNow)
            throw new Exception("Mã OTP đã hết hạn. Vui lòng gửi lại OTP");

        // Check attempts
        if (withdrawal.OtpAttempts >= MAX_OTP_ATTEMPTS)
        {
            // Auto cancel the request
            await CancelWithdrawalInternal(withdrawal, "Nhập sai OTP quá nhiều lần");
            throw new Exception("Nhập sai OTP quá nhiều lần. Yêu cầu đã bị hủy");
        }

        // Verify OTP hash using BCrypt
        if (!VerifyOtp(request.OtpCode, withdrawal.OtpHash))
        {
            withdrawal.OtpAttempts++;
            await _withdrawalRepository.UpdateAsync(withdrawal);
            var remaining = MAX_OTP_ATTEMPTS - withdrawal.OtpAttempts;
            throw new Exception($"Mã OTP không đúng. Còn {remaining} lần thử");
        }

        // OTP verified successfully
        withdrawal.IsOtpVerified = true;
        withdrawal.OtpVerifiedAt = DateTime.UtcNow;
        withdrawal.Status = WithdrawalStatus.OtpVerified;
        withdrawal.OtpHash = null; // Clear for security
        withdrawal.UpdatedAt = DateTime.UtcNow;
        await _withdrawalRepository.UpdateAsync(withdrawal);

        // Update transaction description
        if (!string.IsNullOrEmpty(withdrawal.RelatedTransactionId))
        {
            var transactions = await _transactionRepository.GetByUserIdAsync(userId);
            var tx = transactions.FirstOrDefault(t => t.Id == withdrawal.RelatedTransactionId);
            if (tx != null)
            {
                tx.Description = "Yêu cầu rút tiền - đã xác nhận OTP, chờ admin duyệt";
                tx.UpdatedAt = DateTime.UtcNow;
                await _transactionRepository.UpdateAsync(tx);
            }
        }

        // Notify admins via email
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                _logger.LogInformation(
                    "[ADMIN NOTIFICATION] Yêu cầu rút tiền mới cần duyệt - User: {UserName}, Amount: {Amount:N0} VND",
                    user.FullName, withdrawal.Amount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify admins about withdrawal request");
        }

        _logger.LogInformation("OTP verified for withdrawal {WithdrawalId}", withdrawal.Id);
        return withdrawal;
    }

    public async Task<WithdrawalRequest> ResendOtpAsync(string userId, ResendWithdrawalOtpRequest request)
    {
        var withdrawal = await _withdrawalRepository.GetByIdAsync(request.WithdrawalId);
        if (withdrawal == null) throw new Exception("Yêu cầu rút tiền không tồn tại");
        if (withdrawal.UserId != userId) throw new Exception("Không có quyền truy cập");
        if (withdrawal.Status != WithdrawalStatus.Pending)
            throw new Exception("Yêu cầu không ở trạng thái chờ xác nhận OTP");

        // Generate new OTP
        var otpCode = GenerateOtp();
        withdrawal.OtpHash = HashOtp(otpCode);
        withdrawal.OtpExpiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES);
        withdrawal.OtpAttempts = 0; // Reset attempts
        withdrawal.UpdatedAt = DateTime.UtcNow;
        await _withdrawalRepository.UpdateAsync(withdrawal);

        // Send OTP email
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            try
            {
                await _emailService.SendWithdrawalOtpEmailAsync(user.Email, user.FullName, otpCode);
                _logger.LogInformation("Resent withdrawal OTP to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resend withdrawal OTP email");
            }
        }

        return withdrawal;
    }

    public async Task<WithdrawalRequest> CancelWithdrawalAsync(string userId, string withdrawalId)
    {
        var withdrawal = await _withdrawalRepository.GetByIdAsync(withdrawalId);
        if (withdrawal == null) throw new Exception("Yêu cầu rút tiền không tồn tại");
        if (withdrawal.UserId != userId) throw new Exception("Không có quyền truy cập");
        if (withdrawal.Status != WithdrawalStatus.Pending && withdrawal.Status != WithdrawalStatus.OtpVerified)
            throw new Exception("Không thể hủy yêu cầu ở trạng thái hiện tại");

        await CancelWithdrawalInternal(withdrawal, "User tự hủy yêu cầu");
        return withdrawal;
    }

    public async Task<List<WithdrawalRequest>> GetUserWithdrawalsAsync(string userId)
    {
        return await _withdrawalRepository.GetByUserIdAsync(userId);
    }

    public async Task<WithdrawalRequest?> GetWithdrawalByIdAsync(string withdrawalId)
    {
        return await _withdrawalRepository.GetByIdAsync(withdrawalId);
    }

    // ===== Admin Methods =====

    public async Task<WithdrawalRequest> AdminApproveAsync(string adminId, string withdrawalId)
    {
        var withdrawal = await _withdrawalRepository.GetByIdAsync(withdrawalId);
        if (withdrawal == null) throw new Exception("Yêu cầu rút tiền không tồn tại");
        if (withdrawal.Status != WithdrawalStatus.OtpVerified)
            throw new Exception("Yêu cầu chưa được xác nhận OTP hoặc đã được xử lý");

        // Double check user balance
        var user = await _userRepository.GetByIdAsync(withdrawal.UserId);
        if (user == null) throw new Exception("Người dùng không tồn tại");
        if (user.HeldBalance < withdrawal.Amount)
            throw new Exception("Số dư held không đủ");

        withdrawal.Status = WithdrawalStatus.Processing;
        withdrawal.ApprovedBy = adminId;
        withdrawal.ApprovedAt = DateTime.UtcNow;
        withdrawal.UpdatedAt = DateTime.UtcNow;
        await _withdrawalRepository.UpdateAsync(withdrawal);

        // Update transaction
        await UpdateRelatedTransaction(withdrawal, "Yêu cầu rút tiền - đã được admin duyệt, đang xử lý chuyển khoản");

        // Notify user
        try
        {
            await _emailService.SendWithdrawalApprovedEmailAsync(
                user.Email, user.FullName, withdrawal.Amount,
                $"****{withdrawal.BankSnapshot?.AccountNumberLast4} - {withdrawal.BankSnapshot?.BankName}");
            _logger.LogInformation("Sent withdrawal approved email to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send withdrawal approved email");
        }

        _logger.LogInformation("Admin {AdminId} approved withdrawal {WithdrawalId}", adminId, withdrawalId);
        return withdrawal;
    }

    public async Task<WithdrawalRequest> AdminRejectAsync(string adminId, string withdrawalId, string reason)
    {
        var withdrawal = await _withdrawalRepository.GetByIdAsync(withdrawalId);
        if (withdrawal == null) throw new Exception("Yêu cầu rút tiền không tồn tại");
        if (withdrawal.Status != WithdrawalStatus.OtpVerified)
            throw new Exception("Yêu cầu chưa được xác nhận OTP hoặc đã được xử lý");

        // Refund balance: HeldBalance → Available
        var user = await _userRepository.GetByIdAsync(withdrawal.UserId);
        if (user == null) throw new Exception("Người dùng không tồn tại");

        user.HeldBalance -= withdrawal.Amount;
        user.AvailableBalance += withdrawal.Amount;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Update withdrawal
        withdrawal.Status = WithdrawalStatus.Rejected;
        withdrawal.RejectedBy = adminId;
        withdrawal.RejectedAt = DateTime.UtcNow;
        withdrawal.RejectionReason = reason;
        withdrawal.UpdatedAt = DateTime.UtcNow;
        await _withdrawalRepository.UpdateAsync(withdrawal);

        // Update main transaction to Cancelled
        await UpdateRelatedTransaction(withdrawal, $"Yêu cầu rút tiền bị từ chối: {reason}", TransactionStatus.Cancelled);

        // Notify user
        try
        {
            await _emailService.SendWithdrawalRejectedEmailAsync(
                user.Email, user.FullName, withdrawal.Amount, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send withdrawal rejected email");
        }

        _logger.LogInformation("Admin {AdminId} rejected withdrawal {WithdrawalId}: {Reason}", adminId, withdrawalId, reason);
        return withdrawal;
    }

    public async Task<WithdrawalRequest> AdminCompleteAsync(string adminId, string withdrawalId, string transactionCode, string? proofUrl, decimal? actualAmount = null)
    {
        var withdrawal = await _withdrawalRepository.GetByIdAsync(withdrawalId);
        if (withdrawal == null) throw new Exception("Yêu cầu rút tiền không tồn tại");
        if (withdrawal.Status != WithdrawalStatus.Processing)
            throw new Exception("Yêu cầu chưa được duyệt hoặc đã hoàn tất");

        // Validate số tiền: actualAmount phải đúng với FinalAmount (không cho sai)
        var expectedAmount = withdrawal.FinalAmount;
        var transferAmount = actualAmount ?? expectedAmount;
        
        if (transferAmount != expectedAmount)
        {
            throw new Exception($"Số tiền chuyển ({transferAmount:N0} VND) phải đúng với số tiền yêu cầu ({expectedAmount:N0} VND)");
        }

        // Release HeldBalance (money has been transferred externally)
        var user = await _userRepository.GetByIdAsync(withdrawal.UserId);
        if (user == null) throw new Exception("Người dùng không tồn tại");

        var heldBefore = user.HeldBalance;
        user.HeldBalance -= withdrawal.Amount;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Update withdrawal
        withdrawal.Status = WithdrawalStatus.Completed;
        withdrawal.TransactionCode = transactionCode;
        withdrawal.TransactionProof = proofUrl;
        withdrawal.CompletedAt = DateTime.UtcNow;
        withdrawal.UpdatedAt = DateTime.UtcNow;
        await _withdrawalRepository.UpdateAsync(withdrawal);

        // Update transaction to Completed
        await UpdateRelatedTransaction(withdrawal, 
            $"Rút tiền thành công - Mã GD: {transactionCode}", 
            TransactionStatus.Completed);

        // Notify user
        try
        {
            await _emailService.SendWithdrawalCompletedEmailAsync(
                user.Email, user.FullName,
                withdrawal.Amount, withdrawal.ProcessingFee, withdrawal.FinalAmount,
                transactionCode,
                $"****{withdrawal.BankSnapshot?.AccountNumberLast4} - {withdrawal.BankSnapshot?.BankName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send withdrawal completed email");
        }

        _logger.LogInformation("Admin {AdminId} completed withdrawal {WithdrawalId}, txCode: {TxCode}", 
            adminId, withdrawalId, transactionCode);
        return withdrawal;
    }

    public async Task<WithdrawalRequest> AdminRevertAsync(string adminId, string withdrawalId)
    {
        var withdrawal = await _withdrawalRepository.GetByIdAsync(withdrawalId);
        if (withdrawal == null) throw new Exception("Yêu cầu rút tiền không tồn tại");
        if (withdrawal.Status != WithdrawalStatus.Processing)
            throw new Exception("Chỉ có thể revert yêu cầu đang ở trạng thái Processing");

        withdrawal.Status = WithdrawalStatus.OtpVerified;
        withdrawal.ApprovedBy = null;
        withdrawal.ApprovedAt = null;
        withdrawal.UpdatedAt = DateTime.UtcNow;
        await _withdrawalRepository.UpdateAsync(withdrawal);

        await UpdateRelatedTransaction(withdrawal, "Yêu cầu rút tiền - admin đã revert, chờ duyệt lại");

        _logger.LogInformation("Admin {AdminId} reverted withdrawal {WithdrawalId} to OtpVerified", adminId, withdrawalId);
        return withdrawal;
    }

    public async Task<List<WithdrawalRequest>> GetAllWithdrawalsAsync(int? status = null)
    {
        if (status.HasValue)
        {
            return await _withdrawalRepository.GetByStatusAsync((WithdrawalStatus)status.Value);
        }
        return await _withdrawalRepository.GetAllAsync();
    }

    // ===== Private Helpers =====

    private async Task CancelWithdrawalInternal(WithdrawalRequest withdrawal, string reason)
    {
        // Refund balance: HeldBalance → Available
        var user = await _userRepository.GetByIdAsync(withdrawal.UserId);
        if (user != null)
        {
            user.HeldBalance -= withdrawal.Amount;
            user.AvailableBalance += withdrawal.Amount;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }

        withdrawal.Status = WithdrawalStatus.Cancelled;
        withdrawal.CancelledAt = DateTime.UtcNow;
        withdrawal.CancelReason = reason;
        withdrawal.OtpHash = null;
        withdrawal.UpdatedAt = DateTime.UtcNow;
        await _withdrawalRepository.UpdateAsync(withdrawal);

        // Update transaction to Cancelled
        await UpdateRelatedTransaction(withdrawal, $"Yêu cầu rút tiền đã hủy: {reason}", TransactionStatus.Cancelled);

        _logger.LogInformation("Withdrawal {WithdrawalId} cancelled: {Reason}", withdrawal.Id, reason);
    }

    private async Task UpdateRelatedTransaction(WithdrawalRequest withdrawal, string description, TransactionStatus? newStatus = null)
    {
        if (string.IsNullOrEmpty(withdrawal.RelatedTransactionId)) return;

        var transactions = await _transactionRepository.GetByUserIdAsync(withdrawal.UserId);
        var tx = transactions.FirstOrDefault(t => t.Id == withdrawal.RelatedTransactionId);
        if (tx != null)
        {
            tx.Description = description;
            if (newStatus.HasValue) tx.Status = newStatus.Value;
            tx.UpdatedAt = DateTime.UtcNow;
            await _transactionRepository.UpdateAsync(tx);
        }
    }

    private static string GenerateOtp()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
        return number.ToString("D6");
    }

    private static string HashOtp(string otp)
    {
        return BCrypt.Net.BCrypt.HashPassword(otp);
    }

    private static bool VerifyOtp(string otp, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(otp, hash);
    }
}
