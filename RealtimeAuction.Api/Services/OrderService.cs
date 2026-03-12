using RealtimeAuction.Api.Dtos.Order;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IBidRepository _bidRepository;
    private readonly IEmailService _emailService;
    private readonly IEscrowService _escrowService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IBidRepository bidRepository,
        IEmailService emailService,
        IEscrowService escrowService,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _bidRepository = bidRepository;
        _emailService = emailService;
        _escrowService = escrowService;
        _logger = logger;
    }

    public async Task<Order> CreateOrderFromAuctionAsync(string auctionId, string buyerId, string sellerId, decimal amount, string productTitle, string? productImage)
    {
        // Check if order already exists for this auction
        var existingOrder = await _orderRepository.GetByAuctionIdAsync(auctionId);
        if (existingOrder != null)
        {
            _logger.LogWarning("Order already exists for auction {AuctionId}", auctionId);
            return existingOrder;
        }

        var order = new Order
        {
            AuctionId = auctionId,
            BuyerId = buyerId,
            SellerId = sellerId,
            Amount = amount,
            Status = OrderStatus.PendingShipment,
            ProductTitle = productTitle,
            ProductImage = productImage,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _orderRepository.CreateAsync(order);
        
        _logger.LogInformation("Created order {OrderId} for auction {AuctionId}", order.Id, auctionId);

        // Đóng băng Escrow ngay khi Order được tạo
        var freezeSuccess = await _escrowService.FreezeEscrowAsync(order.Id!);
        if (!freezeSuccess)
        {
            _logger.LogWarning("Failed to freeze escrow for order {OrderId}, will retry on next access", order.Id);
        }
        
        return order;
    }

    public async Task<Order?> GetByIdAsync(string orderId)
    {
        return await _orderRepository.GetByIdAsync(orderId);
    }

    public async Task<IEnumerable<OrderDto>> GetBuyerOrdersAsync(string userId)
    {
        var orders = await _orderRepository.GetByBuyerIdAsync(userId);
        return await MapToOrderDtosAsync(orders);
    }

    public async Task<IEnumerable<OrderDto>> GetSellerOrdersAsync(string userId)
    {
        var orders = await _orderRepository.GetBySellerIdAsync(userId);
        return await MapToOrderDtosAsync(orders);
    }

    public async Task<OrderResult> MarkAsShippedAsync(string orderId, string userId, ShipOrderRequest request)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            return new OrderResult { Success = false, ErrorMessage = "Đơn hàng không tồn tại" };
        }

        // Only seller can mark as shipped
        if (order.SellerId != userId)
        {
            return new OrderResult { Success = false, ErrorMessage = "Bạn không có quyền thực hiện thao tác này" };
        }

        // Only PendingShipment orders can be shipped
        if (order.Status != OrderStatus.PendingShipment)
        {
            return new OrderResult { Success = false, ErrorMessage = "Đơn hàng không ở trạng thái chờ gửi hàng" };
        }

        order.Status = OrderStatus.Shipped;
        order.TrackingNumber = request.TrackingNumber;
        order.ShippingCarrier = request.ShippingCarrier;
        order.ShippingNote = request.ShippingNote;
        order.ShippedAt = DateTime.UtcNow;
        // Đặt hạn auto-release Escrow: 7 ngày sau khi giao hàng
        order.EscrowAutoReleaseAt = DateTime.UtcNow.AddDays(7);
        order.UpdatedAt = DateTime.UtcNow;

        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation("Order {OrderId} marked as shipped by seller {SellerId}, EscrowAutoReleaseAt={AutoRelease}",
            orderId, userId, order.EscrowAutoReleaseAt);

        // Send email to buyer
        try
        {
            var buyer = await _userRepository.GetByIdAsync(order.BuyerId);
            if (buyer != null && !string.IsNullOrEmpty(buyer.Email))
            {
                await _emailService.SendOrderShippedEmailAsync(
                    buyer.Email,
                    buyer.FullName,
                    order.ProductTitle,
                    order.TrackingNumber,
                    order.ShippingCarrier);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order shipped email for order {OrderId}", orderId);
        }

        var dto = await MapToOrderDtoAsync(order);
        return new OrderResult { Success = true, Order = dto };
    }

    public async Task<OrderResult> ConfirmReceivedAsync(string orderId, string userId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            return new OrderResult { Success = false, ErrorMessage = "Đơn hàng không tồn tại" };
        }

        // Only buyer can confirm
        if (order.BuyerId != userId)
        {
            return new OrderResult { Success = false, ErrorMessage = "Bạn không có quyền thực hiện thao tác này" };
        }

        // Only Shipped orders can be confirmed
        if (order.Status != OrderStatus.Shipped)
        {
            return new OrderResult { Success = false, ErrorMessage = "Đơn hàng chưa được gửi đi" };
        }

        // Kiểm tra đã release chưa (tránh double confirm)
        if (order.EscrowReleasedAt.HasValue)
        {
            return new OrderResult { Success = false, ErrorMessage = "Đơn hàng đã được xác nhận trước đó" };
        }

        // Giải phóng Escrow → Seller thông qua EscrowService
        var releaseResult = await _escrowService.ReleaseEscrowToSellerAsync(order.Id!, "BuyerConfirmed");
        if (!releaseResult)
        {
            return new OrderResult { Success = false, ErrorMessage = "Lỗi khi giải phóng Escrow, vui lòng thử lại" };
        }

        // EscrowService đã update Order.Status = Completed, reload để lấy dto mới nhất
        var updatedOrder = await _orderRepository.GetByIdAsync(orderId);
        _logger.LogInformation("Order {OrderId} confirmed by buyer {BuyerId}, Escrow released to seller", orderId, userId);

        var dto = await MapToOrderDtoAsync(updatedOrder ?? order);
        return new OrderResult { Success = true, Order = dto };
    }

    public async Task<OrderResult> CancelOrderAsync(string orderId, string userId, string? reason)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            return new OrderResult { Success = false, ErrorMessage = "Đơn hàng không tồn tại" };
        }

        // Only buyer or seller can cancel
        if (order.BuyerId != userId && order.SellerId != userId)
        {
            return new OrderResult { Success = false, ErrorMessage = "Bạn không có quyền thực hiện thao tác này" };
        }

        // Cannot cancel completed orders
        if (order.Status == OrderStatus.Completed)
        {
            return new OrderResult { Success = false, ErrorMessage = "Không thể hủy đơn hàng đã hoàn tất" };
        }

        // Buyer cannot cancel shipped orders
        if (order.BuyerId == userId && order.Status == OrderStatus.Shipped)
        {
            return new OrderResult { Success = false, ErrorMessage = "Người mua không thể hủy đơn hàng đang được vận chuyển. Vui lòng nhận hàng hoặc mở tranh chấp nếu có vấn đề." };
        }

        // Cannot cancel already cancelled orders
        if (order.Status == OrderStatus.Cancelled)
        {
            return new OrderResult { Success = false, ErrorMessage = "Đơn hàng đã bị hủy" };
        }

        // Hoàn tiền Escrow → Buyer thông qua EscrowService
        var refundResult = await _escrowService.RefundEscrowToBuyerAsync(order.Id!, "OrderCancelled");
        if (!refundResult)
        {
            return new OrderResult { Success = false, ErrorMessage = "Lỗi khi hoàn tiền Escrow, vui lòng thử lại" };
        }

        // EscrowService đã cập nhật Order.Status = Cancelled
        // Cập nhật thêm thông tin cancel
        var cancelledOrder = await _orderRepository.GetByIdAsync(orderId);
        if (cancelledOrder != null)
        {
            cancelledOrder.CancelReason = reason;
            cancelledOrder.CancelledBy = userId;
            cancelledOrder.UpdatedAt = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(cancelledOrder);
        }

        _logger.LogInformation("Order {OrderId} cancelled by user {UserId}, Escrow refunded to buyer", orderId, userId);

        var updatedOrder = await _orderRepository.GetByIdAsync(orderId);
        var dto = await MapToOrderDtoAsync(updatedOrder ?? order);
        return new OrderResult { Success = true, Order = dto };
    }

    private async Task<bool> ProcessPaymentTransferAsync(Order order)
    {
        try
        {
            var buyer = await _userRepository.GetByIdAsync(order.BuyerId);
            var seller = await _userRepository.GetByIdAsync(order.SellerId);

            if (buyer == null || seller == null)
            {
                _logger.LogError("Buyer or seller not found for order {OrderId}", order.Id);
                return false;
            }

            // Find the winning bid to get held amount
            var bids = await _bidRepository.GetByAuctionIdAsync(order.AuctionId);
            var winningBid = bids
                .Where(b => b.UserId == order.BuyerId && !b.IsHoldReleased)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

            if (winningBid == null)
            {
                _logger.LogError("Winning bid not found for order {OrderId}", order.Id);
                return false;
            }

            var paymentAmount = winningBid.HeldAmount;

            // Transfer from buyer's escrow to seller's available
            var buyerBalanceBefore = buyer.EscrowBalance;
            buyer.EscrowBalance -= paymentAmount;
            buyer.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(buyer);

            var sellerBalanceBefore = seller.AvailableBalance;
            seller.AvailableBalance += paymentAmount;
            seller.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(seller);

            // Create buyer transaction
            var buyerTransaction = new Transaction
            {
                UserId = order.BuyerId,
                Type = TransactionType.Payment,
                Amount = -paymentAmount,
                Description = $"Thanh toán đơn hàng #{order.Id?[..8]}",
                RelatedAuctionId = order.AuctionId,
                BalanceBefore = buyerBalanceBefore,
                BalanceAfter = buyer.EscrowBalance,
                Status = TransactionStatus.Completed,
                CreatedAt = DateTime.UtcNow
            };
            await _transactionRepository.CreateAsync(buyerTransaction);

            // Create seller transaction
            var sellerTransaction = new Transaction
            {
                UserId = order.SellerId,
                Type = TransactionType.Payment,
                Amount = paymentAmount,
                Description = $"Nhận thanh toán đơn hàng #{order.Id?[..8]}",
                RelatedAuctionId = order.AuctionId,
                BalanceBefore = sellerBalanceBefore,
                BalanceAfter = seller.AvailableBalance,
                Status = TransactionStatus.Completed,
                CreatedAt = DateTime.UtcNow
            };
            await _transactionRepository.CreateAsync(sellerTransaction);

            // Mark bid as released
            winningBid.IsHoldReleased = true;
            await _bidRepository.UpdateAsync(winningBid);

            _logger.LogInformation("Payment transferred for order {OrderId}: {Amount} from {BuyerId} to {SellerId}",
                order.Id, paymentAmount, order.BuyerId, order.SellerId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment transfer for order {OrderId}", order.Id);
            return false;
        }
    }

    private async Task<bool> ProcessRefundAsync(Order order)
    {
        try
        {
            var buyer = await _userRepository.GetByIdAsync(order.BuyerId);
            if (buyer == null)
            {
                _logger.LogError("Buyer not found for order {OrderId}", order.Id);
                return false;
            }

            // Find held amount
            var bids = await _bidRepository.GetByAuctionIdAsync(order.AuctionId);
            var winningBid = bids
                .Where(b => b.UserId == order.BuyerId && !b.IsHoldReleased)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

            if (winningBid == null)
            {
                _logger.LogWarning("No held bid found for refund, order {OrderId}", order.Id);
                return true; // Consider it already refunded
            }

            var refundAmount = winningBid.HeldAmount;

            // Move from escrow to available
            var balanceBefore = buyer.AvailableBalance;
            buyer.EscrowBalance -= refundAmount;
            buyer.AvailableBalance += refundAmount;
            buyer.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(buyer);

            // Create refund transaction
            var refundTransaction = new Transaction
            {
                UserId = order.BuyerId,
                Type = TransactionType.Refund,
                Amount = refundAmount,
                Description = $"Hoàn tiền đơn hàng #{order.Id?[..8]} - Đã hủy",
                RelatedAuctionId = order.AuctionId,
                BalanceBefore = balanceBefore,
                BalanceAfter = buyer.AvailableBalance,
                Status = TransactionStatus.Completed,
                CreatedAt = DateTime.UtcNow
            };
            await _transactionRepository.CreateAsync(refundTransaction);

            // Mark bid as released
            winningBid.IsHoldReleased = true;
            await _bidRepository.UpdateAsync(winningBid);

            _logger.LogInformation("Refunded {Amount} to buyer {BuyerId} for order {OrderId}",
                refundAmount, order.BuyerId, order.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for order {OrderId}", order.Id);
            return false;
        }
    }

    private async Task<IEnumerable<OrderDto>> MapToOrderDtosAsync(IEnumerable<Order> orders)
    {
        var orderList = orders.ToList();
        if (orderList.Count == 0) return Enumerable.Empty<OrderDto>();

        // Batch fetch all unique users instead of N+1 queries
        var allUserIds = orderList
            .SelectMany(o => new[] { o.BuyerId, o.SellerId })
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var users = await Task.WhenAll(allUserIds.Select(id => _userRepository.GetByIdAsync(id)));
        var userMap = users.Where(u => u != null).ToDictionary(u => u!.Id!, u => u!);

        return orderList.Select(order =>
        {
            userMap.TryGetValue(order.BuyerId, out var buyer);
            userMap.TryGetValue(order.SellerId, out var seller);

            return new OrderDto
            {
                Id = order.Id!,
                AuctionId = order.AuctionId,
                BuyerId = order.BuyerId,
                SellerId = order.SellerId,
                BuyerName = buyer?.FullName,
                SellerName = seller?.FullName,
                Amount = order.Amount,
                Status = order.Status,
                StatusText = GetStatusText(order.Status),
                TrackingNumber = order.TrackingNumber,
                ShippingCarrier = order.ShippingCarrier,
                ShippingNote = order.ShippingNote,
                ProductTitle = order.ProductTitle,
                ProductImage = order.ProductImage,
                ShippedAt = order.ShippedAt,
                CompletedAt = order.CompletedAt,
                CancelledAt = order.CancelledAt,
                CancelReason = order.CancelReason,
                CreatedAt = order.CreatedAt,
                BuyerHasReviewed = order.BuyerHasReviewed,
                SellerHasReviewed = order.SellerHasReviewed,
                CanReview = false, // Will be set by controller based on current user
                // Escrow fields
                EscrowAmount = order.EscrowAmount,
                EscrowFrozenAt = order.EscrowFrozenAt,
                EscrowAutoReleaseAt = order.EscrowAutoReleaseAt,
                EscrowReleasedAt = order.EscrowReleasedAt,
                EscrowReleaseReason = order.EscrowReleaseReason,
                EscrowStatus = GetEscrowStatus(order),
                DaysUntilAutoRelease = GetDaysUntilAutoRelease(order)
            };
        }).ToList();
    }

    private async Task<OrderDto> MapToOrderDtoAsync(Order order)
    {
        var buyer = await _userRepository.GetByIdAsync(order.BuyerId);
        var seller = await _userRepository.GetByIdAsync(order.SellerId);

        return new OrderDto
        {
            Id = order.Id!,
            AuctionId = order.AuctionId,
            BuyerId = order.BuyerId,
            SellerId = order.SellerId,
            BuyerName = buyer?.FullName,
            SellerName = seller?.FullName,
            Amount = order.Amount,
            Status = order.Status,
            StatusText = GetStatusText(order.Status),
            TrackingNumber = order.TrackingNumber,
            ShippingCarrier = order.ShippingCarrier,
            ShippingNote = order.ShippingNote,
            ProductTitle = order.ProductTitle,
            ProductImage = order.ProductImage,
            ShippedAt = order.ShippedAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            CancelReason = order.CancelReason,
            CreatedAt = order.CreatedAt,
            BuyerHasReviewed = order.BuyerHasReviewed,
            SellerHasReviewed = order.SellerHasReviewed,
            CanReview = false, // Will be set by controller based on current user
            // Escrow fields
            EscrowAmount = order.EscrowAmount,
            EscrowFrozenAt = order.EscrowFrozenAt,
            EscrowAutoReleaseAt = order.EscrowAutoReleaseAt,
            EscrowReleasedAt = order.EscrowReleasedAt,
            EscrowReleaseReason = order.EscrowReleaseReason,
            EscrowStatus = GetEscrowStatus(order),
            DaysUntilAutoRelease = GetDaysUntilAutoRelease(order)
        };
    }

    private static string GetStatusText(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.PendingShipment => "Chờ người bán gửi hàng",
            OrderStatus.Shipped => "Đang vận chuyển",
            OrderStatus.Completed => "Đã hoàn tất",
            OrderStatus.Cancelled => "Đã hủy / Hoàn tiền",
            OrderStatus.Disputed => "Đang tranh chấp",
            _ => "Không xác định"
        };
    }

    private static string GetEscrowStatus(Order order)
    {
        if (order.EscrowReleasedAt.HasValue)
        {
            // Refunded = tiền về buyer (hủy đơn hoặc admin phán buyer thắng)
            var reason = order.EscrowReleaseReason ?? "";
            return reason == "OrderCancelled" || reason == "AdminDecision_BuyerWins"
                ? "Refunded"
                : "Released";
        }
        if (order.EscrowFrozenAt.HasValue) return "Frozen";
        return "None";
    }

    private static int? GetDaysUntilAutoRelease(Order order)
    {
        if (!order.EscrowAutoReleaseAt.HasValue || order.EscrowReleasedAt.HasValue) return null;
        var remaining = order.EscrowAutoReleaseAt.Value - DateTime.UtcNow;
        return remaining.TotalDays > 0 ? (int)Math.Ceiling(remaining.TotalDays) : 0;
    }

    private static string FormatCurrency(decimal amount)
    {
        return string.Format(new System.Globalization.CultureInfo("vi-VN"), "{0:N0} ₫", amount);
    }
}
