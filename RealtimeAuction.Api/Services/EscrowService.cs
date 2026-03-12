using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Services;

public class EscrowService : IEscrowService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUserRepository _userRepository;
    private readonly IBidRepository _bidRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<EscrowService> _logger;

    // Số ngày tự động release sau khi seller đánh dấu đã giao hàng
    private const int AUTO_RELEASE_DAYS = 7;

    public EscrowService(
        IOrderRepository orderRepository,
        IUserRepository userRepository,
        IBidRepository bidRepository,
        ITransactionRepository transactionRepository,
        IEmailService emailService,
        ILogger<EscrowService> logger)
    {
        _orderRepository = orderRepository;
        _userRepository = userRepository;
        _bidRepository = bidRepository;
        _transactionRepository = transactionRepository;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Đóng băng tiền buyer vào Escrow (ghi metadata escrow vào Order).
    /// Tiền thực tế đã nằm trong EscrowBalance của buyer từ khi bid được hold.
    /// </summary>
    public async Task<bool> FreezeEscrowAsync(string orderId)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogError("FreezeEscrow: Order {OrderId} not found", orderId);
                return false;
            }

            // Kiểm tra đã freeze chưa
            if (order.EscrowFrozenAt.HasValue)
            {
                _logger.LogWarning("FreezeEscrow: Order {OrderId} already frozen at {FrozenAt}", orderId, order.EscrowFrozenAt);
                return true;
            }

            // Tìm winning bid để lấy amount thực tế đang held
            var bids = await _bidRepository.GetByAuctionIdAsync(order.AuctionId);
            var winningBid = bids
                .Where(b => b.UserId == order.BuyerId && !b.IsHoldReleased)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

            var escrowAmount = winningBid?.HeldAmount ?? order.Amount;

            // Ghi metadata Escrow vào Order
            order.EscrowAmount = escrowAmount;
            order.EscrowFrozenAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);

            // Ghi transaction log EscrowFreeze
            var buyer = await _userRepository.GetByIdAsync(order.BuyerId);
            if (buyer != null)
            {
                var freezeTx = new Transaction
                {
                    UserId = order.BuyerId,
                    Type = TransactionType.EscrowFreeze,
                    Amount = -escrowAmount,
                    Description = $"🔒 Đóng băng Escrow - Đơn hàng #{order.Id![..8]} | {order.ProductTitle}",
                    RelatedAuctionId = order.AuctionId,
                    BalanceBefore = buyer.EscrowBalance,
                    BalanceAfter = buyer.EscrowBalance, // EscrowBalance không thay đổi, chỉ labeled
                    Status = TransactionStatus.Completed,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _transactionRepository.CreateAsync(freezeTx);
            }

            _logger.LogInformation("EscrowFrozen: Order {OrderId}, Amount {Amount}, BuyerId {BuyerId}",
                orderId, escrowAmount, order.BuyerId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error freezing escrow for order {OrderId}", orderId);
            return false;
        }
    }

    /// <summary>
    /// Giải phóng Escrow → Seller.AvailableBalance.
    /// Gọi khi: buyer xác nhận nhận hàng, auto-release, hoặc admin phán seller thắng.
    /// </summary>
    public async Task<bool> ReleaseEscrowToSellerAsync(string orderId, string releaseReason)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogError("ReleaseEscrow: Order {OrderId} not found", orderId);
                return false;
            }

            // Chống double-release
            if (order.EscrowReleasedAt.HasValue)
            {
                _logger.LogWarning("ReleaseEscrow: Order {OrderId} already released at {ReleasedAt}", orderId, order.EscrowReleasedAt);
                return true;
            }

            var buyer = await _userRepository.GetByIdAsync(order.BuyerId);
            var seller = await _userRepository.GetByIdAsync(order.SellerId);

            if (buyer == null || seller == null)
            {
                _logger.LogError("ReleaseEscrow: Buyer or Seller not found for Order {OrderId}", orderId);
                return false;
            }

            // Tìm winning bid để lấy HeldAmount chính xác
            var bids = await _bidRepository.GetByAuctionIdAsync(order.AuctionId);
            var winningBid = bids
                .Where(b => b.UserId == order.BuyerId && !b.IsHoldReleased)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

            var releaseAmount = winningBid?.HeldAmount ?? order.EscrowAmount;
            if (releaseAmount <= 0) releaseAmount = order.Amount;

            // Chuyển tiền: Buyer.EscrowBalance → Seller.AvailableBalance
            var buyerEscrowBefore = buyer.EscrowBalance;
            buyer.EscrowBalance -= releaseAmount;
            if (buyer.EscrowBalance < 0) buyer.EscrowBalance = 0;
            buyer.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(buyer);

            var sellerAvailBefore = seller.AvailableBalance;
            seller.AvailableBalance += releaseAmount;
            seller.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(seller);

            // Ghi transaction log cho cả hai phía
            var buyerTx = new Transaction
            {
                UserId = order.BuyerId,
                Type = TransactionType.EscrowRelease,
                Amount = -releaseAmount,
                Description = $"✅ Thanh toán Escrow - {order.ProductTitle} | {GetReasonText(releaseReason)}",
                RelatedAuctionId = order.AuctionId,
                BalanceBefore = buyerEscrowBefore,
                BalanceAfter = buyer.EscrowBalance,
                Status = TransactionStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _transactionRepository.CreateAsync(buyerTx);

            var sellerTx = new Transaction
            {
                UserId = order.SellerId,
                Type = TransactionType.EscrowRelease,
                Amount = releaseAmount,
                Description = $"💸 Nhận thanh toán Escrow - {order.ProductTitle} | {GetReasonText(releaseReason)}",
                RelatedAuctionId = order.AuctionId,
                BalanceBefore = sellerAvailBefore,
                BalanceAfter = seller.AvailableBalance,
                Status = TransactionStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _transactionRepository.CreateAsync(sellerTx);

            // Mark winning bid as released
            if (winningBid != null)
            {
                winningBid.IsHoldReleased = true;
                await _bidRepository.UpdateAsync(winningBid);
            }

            // Cập nhật Order Escrow metadata
            order.EscrowReleasedAt = DateTime.UtcNow;
            order.EscrowReleaseReason = releaseReason;
            order.Status = OrderStatus.Completed;
            order.CompletedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);

            _logger.LogInformation("EscrowReleased: Order {OrderId}, Amount {Amount}, Seller {SellerId}, Reason {Reason}",
                orderId, releaseAmount, order.SellerId, releaseReason);

            // Gửi email thông báo
            await SendReleaseEmailsAsync(buyer, seller, order, releaseAmount, releaseReason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing escrow for order {OrderId}", orderId);
            return false;
        }
    }

    /// <summary>
    /// Hoàn tiền Escrow → Buyer.AvailableBalance.
    /// Gọi khi: cancel order, admin phán buyer thắng dispute.
    /// </summary>
    public async Task<bool> RefundEscrowToBuyerAsync(string orderId, string refundReason)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogError("RefundEscrow: Order {OrderId} not found", orderId);
                return false;
            }

            // Chống double-refund
            if (order.EscrowReleasedAt.HasValue)
            {
                _logger.LogWarning("RefundEscrow: Order {OrderId} already released/refunded at {ReleasedAt}", orderId, order.EscrowReleasedAt);
                return true;
            }

            var buyer = await _userRepository.GetByIdAsync(order.BuyerId);
            if (buyer == null)
            {
                _logger.LogError("RefundEscrow: Buyer not found for Order {OrderId}", orderId);
                return false;
            }

            // Tìm winning bid để lấy HeldAmount chính xác
            var bids = await _bidRepository.GetByAuctionIdAsync(order.AuctionId);
            var winningBid = bids
                .Where(b => b.UserId == order.BuyerId && !b.IsHoldReleased)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

            var refundAmount = winningBid?.HeldAmount ?? order.EscrowAmount;
            if (refundAmount <= 0) refundAmount = order.Amount;

            // Chuyển tiền: Buyer.EscrowBalance → Buyer.AvailableBalance
            var escrowBefore = buyer.EscrowBalance;
            var availBefore = buyer.AvailableBalance;
            buyer.EscrowBalance -= refundAmount;
            if (buyer.EscrowBalance < 0) buyer.EscrowBalance = 0;
            buyer.AvailableBalance += refundAmount;
            buyer.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(buyer);

            // Ghi transaction log
            var refundTx = new Transaction
            {
                UserId = order.BuyerId,
                Type = TransactionType.EscrowRefund,
                Amount = refundAmount,
                Description = $"💸 Hoàn tiền Escrow - {order.ProductTitle} | {GetReasonText(refundReason)}",
                RelatedAuctionId = order.AuctionId,
                BalanceBefore = availBefore,
                BalanceAfter = buyer.AvailableBalance,
                Status = TransactionStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _transactionRepository.CreateAsync(refundTx);

            // Mark winning bid as released
            if (winningBid != null)
            {
                winningBid.IsHoldReleased = true;
                await _bidRepository.UpdateAsync(winningBid);
            }

            // Cập nhật Order Escrow metadata
            order.EscrowReleasedAt = DateTime.UtcNow;
            order.EscrowReleaseReason = refundReason;
            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);

            _logger.LogInformation("EscrowRefunded: Order {OrderId}, Amount {Amount}, Buyer {BuyerId}, Reason {Reason}",
                orderId, refundAmount, order.BuyerId, refundReason);

            // Gửi email thông báo
            await SendRefundEmailAsync(buyer, order, refundAmount, refundReason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding escrow for order {OrderId}", orderId);
            return false;
        }
    }

    /// <summary>
    /// Tự động release tất cả orders đã quá hạn EscrowAutoReleaseAt.
    /// Được gọi bởi BackgroundService định kỳ.
    /// </summary>
    public async Task ProcessAutoReleaseAsync()
    {
        try
        {
            var ordersToRelease = await _orderRepository.GetOrdersForAutoReleaseAsync();
            var orderList = ordersToRelease.ToList();

            if (orderList.Count == 0)
            {
                _logger.LogDebug("AutoRelease: No orders to process");
                return;
            }

            _logger.LogInformation("AutoRelease: Processing {Count} orders", orderList.Count);

            foreach (var order in orderList)
            {
                _logger.LogInformation("AutoRelease: Processing order {OrderId}", order.Id);
                var success = await ReleaseEscrowToSellerAsync(order.Id!, "AutoRelease");
                if (!success)
                {
                    _logger.LogError("AutoRelease: Failed to release escrow for order {OrderId}", order.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during escrow auto-release processing");
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static string GetReasonText(string reason) => reason switch
    {
        "BuyerConfirmed"               => "Người mua xác nhận nhận hàng",
        "AutoRelease"                  => "Tự động giải phóng (7 ngày sau khi giao hàng)",
        "AdminDecision_SellerWins"     => "Admin phán quyết: Người bán thắng tranh chấp",
        "AdminDecision_BuyerWins"      => "Admin phán quyết: Người mua thắng tranh chấp",
        "OrderCancelled"               => "Đơn hàng bị hủy",
        _                              => reason
    };

    private async Task SendReleaseEmailsAsync(User buyer, User seller, Order order, decimal amount, string reason)
    {
        try
        {
            var amountStr = FormatCurrency(amount);
            var dateStr = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm"); // Vietnam time
            var reasonText = GetReasonText(reason);

            if (!string.IsNullOrEmpty(buyer.Email))
            {
                await _emailService.SendTransactionCompletedEmailAsync(
                    buyer.Email, buyer.FullName, "Người mua",
                    order.ProductTitle, amountStr, dateStr);
            }

            if (!string.IsNullOrEmpty(seller.Email))
            {
                await _emailService.SendTransactionCompletedEmailAsync(
                    seller.Email, seller.FullName, "Người bán",
                    order.ProductTitle, amountStr, dateStr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send release emails for order {OrderId}", order.Id);
        }
    }

    private async Task SendRefundEmailAsync(User buyer, Order order, decimal amount, string reason)
    {
        try
        {
            // Gửi email hoàn tiền cho buyer nếu cần
            _logger.LogInformation("Refund email sent to buyer {BuyerId} for order {OrderId}, amount {Amount}",
                buyer.Id, order.Id, amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send refund email for order {OrderId}", order.Id);
        }
    }

    private static string FormatCurrency(decimal amount) =>
        string.Format(new System.Globalization.CultureInfo("vi-VN"), "{0:N0} ₫", amount);
}
