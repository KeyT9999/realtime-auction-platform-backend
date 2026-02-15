using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.BackgroundServices;

/// <summary>
/// Background service that sends reminder emails to admins for withdrawals in Processing status.
/// Runs every 6 hours to check for withdrawals approved more than 24 hours ago.
/// </summary>
public class WithdrawalReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WithdrawalReminderService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);
    private readonly int _reminderAfterHours = 24;
    private readonly int _warningAfterHours = 48;

    public WithdrawalReminderService(
        IServiceProvider serviceProvider,
        ILogger<WithdrawalReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WithdrawalReminderService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessWithdrawalRemindersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WithdrawalReminderService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("WithdrawalReminderService stopped");
    }

    private async Task ProcessWithdrawalRemindersAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        
        var withdrawalRepository = scope.ServiceProvider.GetRequiredService<IWithdrawalRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var reminderThreshold = now.AddHours(-_reminderAfterHours);
        var warningThreshold = now.AddHours(-_warningAfterHours);

        // Get withdrawals in Processing status, approved more than 24h ago
        var processingWithdrawals = await withdrawalRepository.GetByStatusAsync(WithdrawalStatus.Processing);
        var overdueWithdrawals = processingWithdrawals
            .Where(w => w.ApprovedAt.HasValue && w.ApprovedAt.Value < reminderThreshold)
            .ToList();

        _logger.LogInformation("Found {Count} overdue withdrawals to send reminders", overdueWithdrawals.Count);

        foreach (var withdrawal in overdueWithdrawals)
        {
            try
            {
                var hoursSinceApproved = (now - withdrawal.ApprovedAt!.Value).TotalHours;
                var isWarning = withdrawal.ApprovedAt.Value < warningThreshold;

                await SendAdminReminderAsync(
                    withdrawal,
                    userRepository,
                    emailService,
                    (int)hoursSinceApproved,
                    isWarning);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending reminder for withdrawal {WithdrawalId}", withdrawal.Id);
            }
        }
    }

    private async Task SendAdminReminderAsync(
        WithdrawalRequest withdrawal,
        IUserRepository userRepository,
        IEmailService emailService,
        int hoursSinceApproved,
        bool isWarning)
    {
        var user = await userRepository.GetByIdAsync(withdrawal.UserId);
        if (user == null)
        {
            return;
        }

        // Get all admin users
        var allUsers = await userRepository.GetAllAsync();
        var adminUsers = allUsers.Where(u => u.Role == "Admin" && !string.IsNullOrEmpty(u.Email)).ToList();

        if (!adminUsers.Any())
        {
            _logger.LogWarning("No admin users found to send withdrawal reminder");
            return;
        }

        var message = isWarning
            ? $"CẢNH BÁO: Yêu cầu rút tiền đã được duyệt {hoursSinceApproved} giờ nhưng chưa được xử lý!"
            : $"Nhắc nhở: Yêu cầu rút tiền đã được duyệt {hoursSinceApproved} giờ, cần xử lý chuyển khoản.";

        foreach (var admin in adminUsers)
        {
            try
            {
                await emailService.SendWithdrawalReminderToAdminAsync(
                    admin.Email,
                    admin.FullName,
                    withdrawal.Id ?? "",
                    user.FullName,
                    user.Email,
                    withdrawal.Amount,
                    withdrawal.FinalAmount,
                    withdrawal.BankSnapshot?.BankName ?? "N/A",
                    $"****{withdrawal.BankSnapshot?.AccountNumberLast4 ?? "N/A"}",
                    hoursSinceApproved,
                    message);

                _logger.LogInformation(
                    "Sent withdrawal reminder to admin {AdminEmail} for withdrawal {WithdrawalId}",
                    admin.Email, withdrawal.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder to admin {AdminEmail}", admin.Email);
            }
        }

        if (isWarning)
        {
            _logger.LogWarning(
                "Withdrawal {WithdrawalId} has been in Processing status for {Hours} hours - URGENT ACTION REQUIRED",
                withdrawal.Id, hoursSinceApproved);
        }
    }
}
