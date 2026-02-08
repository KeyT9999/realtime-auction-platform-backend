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
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IBidRepository bidRepository,
        IEmailService emailService,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _bidRepository = bidRepository;
        _emailService = emailService;
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
        order.UpdatedAt = DateTime.UtcNow;

        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation("Order {OrderId} marked as shipped by seller {SellerId}", orderId, userId);

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

        // Process payment transfer
        var paymentResult = await ProcessPaymentTransferAsync(order);
        if (!paymentResult)
        {
            return new OrderResult { Success = false, ErrorMessage = "Lỗi khi xử lý thanh toán" };
        }

        order.Status = OrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation("Order {OrderId} completed by buyer {BuyerId}", orderId, userId);

        // Send emails to both parties
        try
        {
            var buyer = await _userRepository.GetByIdAsync(order.BuyerId);
            var seller = await _userRepository.GetByIdAsync(order.SellerId);
            var amountStr = FormatCurrency(order.Amount);
            var dateStr = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm");

            if (buyer != null && !string.IsNullOrEmpty(buyer.Email))
            {
                await _emailService.SendTransactionCompletedEmailAsync(
                    buyer.Email, buyer.FullName, "Người mua",
                    order.ProductTitle, amountStr, dateStr);
            }

            if (seller != null && !string.IsNullOrEmpty(seller.Email))
            {
                await _emailService.SendTransactionCompletedEmailAsync(
                    seller.Email, seller.FullName, "Người bán",
                    order.ProductTitle, amountStr, dateStr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send transaction completed emails for order {OrderId}", orderId);
        }

        var dto = await MapToOrderDtoAsync(order);
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

        // Cannot cancel already cancelled orders
        if (order.Status == OrderStatus.Cancelled)
        {
            return new OrderResult { Success = false, ErrorMessage = "Đơn hàng đã bị hủy" };
        }

        // Refund buyer
        var refundResult = await ProcessRefundAsync(order);
        if (!refundResult)
        {
            return new OrderResult { Success = false, ErrorMessage = "Lỗi khi xử lý hoàn tiền" };
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancelReason = reason;
        order.CancelledBy = userId;
        order.UpdatedAt = DateTime.UtcNow;

        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation("Order {OrderId} cancelled by user {UserId}", orderId, userId);

        var dto = await MapToOrderDtoAsync(order);
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
        var dtos = new List<OrderDto>();
        foreach (var order in orders)
        {
            dtos.Add(await MapToOrderDtoAsync(order));
        }
        return dtos;
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
            CanReview = false // Will be set by controller based on current user
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
            _ => "Không xác định"
        };
    }

    private static string FormatCurrency(decimal amount)
    {
        return string.Format(new System.Globalization.CultureInfo("vi-VN"), "{0:N0} ₫", amount);
    }
}
