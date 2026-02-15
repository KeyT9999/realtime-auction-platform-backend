using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.BankAccount;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/bank-accounts")]
[Authorize]
public class BankAccountController : ControllerBase
{
    private readonly IBankAccountRepository _bankAccountRepository;
    private readonly ILogger<BankAccountController> _logger;

    public BankAccountController(
        IBankAccountRepository bankAccountRepository,
        ILogger<BankAccountController> logger)
    {
        _bankAccountRepository = bankAccountRepository;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách tài khoản ngân hàng
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBankAccounts()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            var accounts = await _bankAccountRepository.GetByUserIdAsync(userId);
            return Ok(new
            {
                bankAccounts = accounts.Select(a => new
                {
                    id = a.Id,
                    bankName = a.BankName,
                    accountNumber = a.AccountNumber,
                    accountName = a.AccountName,
                    isDefault = a.IsDefault,
                    createdAt = a.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank accounts");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Thêm tài khoản ngân hàng
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateBankAccount([FromBody] CreateBankAccountRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            // If this is set as default, clear other defaults
            if (request.IsDefault)
            {
                await _bankAccountRepository.ClearDefaultByUserIdAsync(userId);
            }

            // If this is the first account, make it default
            var existingAccounts = await _bankAccountRepository.GetByUserIdAsync(userId);
            var isFirst = existingAccounts.Count == 0;

            var bankAccount = new BankAccount
            {
                UserId = userId,
                BankName = request.BankName,
                AccountNumber = request.AccountNumber,
                AccountName = request.AccountName.ToUpper(),
                IsDefault = request.IsDefault || isFirst,
                CreatedAt = DateTime.UtcNow
            };

            await _bankAccountRepository.CreateAsync(bankAccount);

            return Ok(new
            {
                message = "Đã thêm tài khoản ngân hàng",
                bankAccount = new
                {
                    id = bankAccount.Id,
                    bankName = bankAccount.BankName,
                    accountNumber = bankAccount.AccountNumber,
                    accountName = bankAccount.AccountName,
                    isDefault = bankAccount.IsDefault
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bank account");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật tài khoản ngân hàng
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBankAccount(string id, [FromBody] UpdateBankAccountRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            var bankAccount = await _bankAccountRepository.GetByIdAsync(id);
            if (bankAccount == null) return NotFound(new { message = "Tài khoản không tồn tại" });
            if (bankAccount.UserId != userId) return Forbid();

            if (!string.IsNullOrEmpty(request.BankName)) bankAccount.BankName = request.BankName;
            if (!string.IsNullOrEmpty(request.AccountNumber)) bankAccount.AccountNumber = request.AccountNumber;
            if (!string.IsNullOrEmpty(request.AccountName)) bankAccount.AccountName = request.AccountName.ToUpper();
            if (request.IsDefault.HasValue && request.IsDefault.Value)
            {
                await _bankAccountRepository.ClearDefaultByUserIdAsync(userId);
                bankAccount.IsDefault = true;
            }

            await _bankAccountRepository.UpdateAsync(bankAccount);

            return Ok(new { message = "Đã cập nhật tài khoản ngân hàng" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bank account");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa tài khoản ngân hàng
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBankAccount(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            var bankAccount = await _bankAccountRepository.GetByIdAsync(id);
            if (bankAccount == null) return NotFound(new { message = "Tài khoản không tồn tại" });
            if (bankAccount.UserId != userId) return Forbid();

            await _bankAccountRepository.DeleteAsync(id);
            return Ok(new { message = "Đã xóa tài khoản ngân hàng" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bank account");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Đặt tài khoản mặc định
    /// </summary>
    [HttpPut("{id}/default")]
    public async Task<IActionResult> SetDefault(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập" });

            var bankAccount = await _bankAccountRepository.GetByIdAsync(id);
            if (bankAccount == null) return NotFound(new { message = "Tài khoản không tồn tại" });
            if (bankAccount.UserId != userId) return Forbid();

            await _bankAccountRepository.ClearDefaultByUserIdAsync(userId);
            bankAccount.IsDefault = true;
            await _bankAccountRepository.UpdateAsync(bankAccount);

            return Ok(new { message = "Đã đặt làm tài khoản mặc định" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting default bank account");
            return BadRequest(new { message = ex.Message });
        }
    }
}
