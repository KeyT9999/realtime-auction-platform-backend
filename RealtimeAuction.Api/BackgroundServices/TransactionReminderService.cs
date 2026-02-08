using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.BackgroundServices;

/// <summary>
/// Background service that sends reminder emails for pending transactions.
/// Runs every 6 hours to check for transactions pending more than 2 days.
/// </summary>
public class TransactionReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TransactionReminderService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);
    private readonly int _reminderAfterDays = 2;
    private readonly int _maxDaysBeforeCancel = 7;

    public TransactionReminderService(
        IServiceProvider serviceProvider,
        ILogger<TransactionReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TransactionReminderService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTransactionRemindersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TransactionReminderService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("TransactionReminderService stopped");
    }

    private async Task ProcessTransactionRemindersAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        
        var transactionRepository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var auctionRepository = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var reminderThreshold = now.AddDays(-_reminderAfterDays);

        // Get pending transactions older than the reminder threshold
        var allTransactions = await transactionRepository.GetAllAsync();
        var pendingTransactions = allTransactions
            .Where(t => t.Status == TransactionStatus.Pending 
                        && t.CreatedAt <= reminderThreshold
                        && t.Type == TransactionType.Payment)
            .ToList();

        _logger.LogInformation("Found {Count} pending transactions to send reminders", pendingTransactions.Count);

        foreach (var transaction in pendingTransactions)
        {
            try
            {
                await SendTransactionReminderAsync(
                    transaction,
                    auctionRepository,
                    userRepository,
                    emailService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending reminder for transaction {TransactionId}", transaction.Id);
            }
        }
    }

    private async Task SendTransactionReminderAsync(
        Transaction transaction,
        IAuctionRepository auctionRepository,
        IUserRepository userRepository,
        IEmailService emailService)
    {
        if (string.IsNullOrEmpty(transaction.RelatedAuctionId))
        {
            return;
        }

        var auction = await auctionRepository.GetByIdAsync(transaction.RelatedAuctionId);
        if (auction == null)
        {
            return;
        }

        var daysSinceCreated = (DateTime.UtcNow - transaction.CreatedAt).Days;
        var daysRemaining = _maxDaysBeforeCancel - daysSinceCreated;

        if (daysRemaining < 0)
        {
            daysRemaining = 0;
        }

        var transactionUrl = $"http://localhost:5173/auctions/{auction.Id}";
        var warningMessage = daysRemaining <= 2 
            ? "Giao dịch có thể bị hủy nếu không được xác nhận kịp thời!" 
            : "Vui lòng xác nhận giao dịch để hoàn tất quy trình.";

        // Send reminder to buyer if they haven't confirmed
        if (!transaction.BuyerConfirmed)
        {
            var buyer = await userRepository.GetByIdAsync(transaction.UserId);
            if (buyer != null && !string.IsNullOrEmpty(buyer.Email))
            {
                await emailService.SendTransactionReminderEmailAsync(
                    buyer.Email,
                    buyer.FullName,
                    auction.Title,
                    "Chờ bạn xác nhận",
                    daysRemaining,
                    transactionUrl,
                    warningMessage);

                _logger.LogInformation("Sent transaction reminder to buyer {Email} for auction {AuctionId}", 
                    buyer.Email, auction.Id);
            }
        }

        // Send reminder to seller if they haven't confirmed
        if (!transaction.SellerConfirmed)
        {
            var seller = await userRepository.GetByIdAsync(auction.SellerId);
            if (seller != null && !string.IsNullOrEmpty(seller.Email))
            {
                await emailService.SendTransactionReminderEmailAsync(
                    seller.Email,
                    seller.FullName,
                    auction.Title,
                    "Chờ bạn xác nhận giao hàng",
                    daysRemaining,
                    transactionUrl,
                    warningMessage);

                _logger.LogInformation("Sent transaction reminder to seller {Email} for auction {AuctionId}", 
                    seller.Email, auction.Id);
            }
        }
    }
}
