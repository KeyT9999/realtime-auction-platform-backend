using RealtimeAuction.Api.Dtos.Withdrawal;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Services;

public interface IWithdrawalService
{
    // User endpoints
    Task<WithdrawalRequest> CreateWithdrawalAsync(string userId, CreateWithdrawalRequest request);
    Task<WithdrawalRequest> VerifyOtpAsync(string userId, VerifyWithdrawalOtpRequest request);
    Task<WithdrawalRequest> ResendOtpAsync(string userId, ResendWithdrawalOtpRequest request);
    Task<WithdrawalRequest> CancelWithdrawalAsync(string userId, string withdrawalId);
    Task<List<WithdrawalRequest>> GetUserWithdrawalsAsync(string userId);
    Task<WithdrawalRequest?> GetWithdrawalByIdAsync(string withdrawalId);

    // Admin endpoints
    Task<WithdrawalRequest> AdminApproveAsync(string adminId, string withdrawalId);
    Task<WithdrawalRequest> AdminRejectAsync(string adminId, string withdrawalId, string reason);
    Task<WithdrawalRequest> AdminCompleteAsync(string adminId, string withdrawalId, string transactionCode, string? proofUrl, decimal? actualAmount = null);
    Task<WithdrawalRequest> AdminRevertAsync(string adminId, string withdrawalId);
    Task<List<WithdrawalRequest>> GetAllWithdrawalsAsync(int? status = null);
}
