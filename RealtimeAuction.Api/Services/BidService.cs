using RealtimeAuction.Api.Dtos.Bid;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Services;

public class BidService : IBidService
{
    private readonly IBidRepository _bidRepository;
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<BidService> _logger;

    public BidService(
        IBidRepository bidRepository,
        IAuctionRepository auctionRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        ILogger<BidService> logger)
    {
        _bidRepository = bidRepository;
        _auctionRepository = auctionRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<BidResult> PlaceBidAsync(string userId, CreateBidDto request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return new BidResult { Success = false, ErrorMessage = "Người dùng không tồn tại" };
        }

        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId);
        if (auction == null)
        {
            return new BidResult { Success = false, ErrorMessage = "Phiên đấu giá không tồn tại" };
        }

        if (auction.Status != AuctionStatus.Active)
        {
            return new BidResult { Success = false, ErrorMessage = "Phiên đấu giá không còn hoạt động" };
        }

        if (auction.SellerId == userId)
        {
            return new BidResult { Success = false, ErrorMessage = "Bạn không thể đấu giá sản phẩm của chính mình" };
        }

        // Kiểm tra bid amount
        var minBid = auction.CurrentPrice + auction.BidIncrement;
        if (request.Amount < minBid)
        {
            return new BidResult { Success = false, ErrorMessage = $"Giá đặt tối thiểu phải là {minBid:N0}đ" };
        }

        // Tìm bid trước đó của user trong auction này (nếu có)
        var userBids = await _bidRepository.GetByAuctionIdAsync(request.AuctionId);
        var previousUserBid = userBids
            .Where(b => b.UserId == userId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        // Tính số tiền cần hold
        decimal holdAmount;
        if (previousUserBid != null)
        {
            // Nếu đã có bid trước, chỉ hold thêm phần chênh lệch
            holdAmount = request.Amount - previousUserBid.HeldAmount;
        }
        else
        {
            // Bid đầu tiên trong auction này, hold toàn bộ
            holdAmount = request.Amount;
        }

        // Kiểm tra số dư khả dụng
        if (user.AvailableBalance < holdAmount)
        {
            return new BidResult 
            { 
                Success = false, 
                ErrorMessage = $"Số dư không đủ. Cần {holdAmount:N0}đ, hiện có {user.AvailableBalance:N0}đ" 
            };
        }

        // Tìm previous highest bidder để release hold
        var previousHighestBid = userBids
            .Where(b => b.UserId != userId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        string? outbidUserId = previousHighestBid?.UserId;

        // === BẮT ĐẦU TRANSACTION ===
        
        // 1. Hold tiền của bidder hiện tại
        var balanceBefore = user.AvailableBalance;
        user.AvailableBalance -= holdAmount;
        user.EscrowBalance += holdAmount;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // 2. Tạo transaction hold
        var holdTransaction = new Transaction
        {
            UserId = userId,
            Type = TransactionType.Hold,
            Amount = -holdAmount, // Âm vì trừ từ available
            Description = $"Đặt giá {request.Amount:N0}đ cho phiên #{auction.Id?[..8]}",
            RelatedAuctionId = request.AuctionId,
            BalanceBefore = balanceBefore,
            BalanceAfter = user.AvailableBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(holdTransaction);

        // 3. Tạo bid mới
        var bid = new Bid
        {
            AuctionId = request.AuctionId,
            UserId = userId,
            Amount = request.Amount,
            HeldAmount = previousUserBid != null ? previousUserBid.HeldAmount + holdAmount : holdAmount,
            IsHoldReleased = false,
            AutoBid = request.AutoBid != null ? new Bid.AutoBidSettings
            {
                MaxBid = request.AutoBid.MaxBid,
                IsActive = request.AutoBid.IsActive
            } : null
        };

        var created = await _bidRepository.CreateAsync(bid, auction.CurrentPrice, auction.BidIncrement);

        // 4. Update auction current price
        await _auctionRepository.UpdateCurrentPriceAsync(request.AuctionId, request.Amount);

        // 5. Release hold của previous highest bidder (nếu có)
        if (previousHighestBid != null && outbidUserId != null)
        {
            await ReleaseHoldAsync(outbidUserId, request.AuctionId);
        }

        _logger.LogInformation(
            "Bid placed: User {UserId} bid {Amount} on auction {AuctionId}. Held: {HeldAmount}",
            userId, request.Amount, request.AuctionId, holdAmount);

        return new BidResult
        {
            Success = true,
            Bid = created,
            OutbidUserId = outbidUserId
        };
    }

    public async Task ReleaseHoldAsync(string userId, string auctionId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return;

        // Tìm tất cả bids của user trong auction này chưa release
        var userBids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var unreleased = userBids
            .Where(b => b.UserId == userId && !b.IsHoldReleased)
            .ToList();

        if (!unreleased.Any()) return;

        // Tính tổng held amount
        var totalHeld = unreleased.Max(b => b.HeldAmount);

        // Release hold
        var balanceBefore = user.AvailableBalance;
        user.AvailableBalance += totalHeld;
        user.EscrowBalance -= totalHeld;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Tạo transaction release
        var releaseTransaction = new Transaction
        {
            UserId = userId,
            Type = TransactionType.Release,
            Amount = totalHeld,
            Description = $"Hoàn trả cọc - bị outbid",
            RelatedAuctionId = auctionId,
            BalanceBefore = balanceBefore,
            BalanceAfter = user.AvailableBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(releaseTransaction);

        // Mark bids as released
        foreach (var bid in unreleased)
        {
            bid.IsHoldReleased = true;
            await _bidRepository.UpdateAsync(bid);
        }

        _logger.LogInformation(
            "Released hold for user {UserId} on auction {AuctionId}. Amount: {Amount}",
            userId, auctionId, totalHeld);
    }

    public async Task ReleaseAllHoldsExceptWinnerAsync(string auctionId, string winnerId)
    {
        var allBids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var loserUserIds = allBids
            .Where(b => b.UserId != winnerId && !b.IsHoldReleased)
            .Select(b => b.UserId)
            .Distinct()
            .ToList();

        foreach (var userId in loserUserIds)
        {
            await ReleaseHoldAsync(userId, auctionId);
        }

        _logger.LogInformation(
            "Released all holds for auction {AuctionId} except winner {WinnerId}. Released {Count} users.",
            auctionId, winnerId, loserUserIds.Count);
    }

    public async Task ProcessWinnerPaymentAsync(string auctionId, string winnerId, string sellerId)
    {
        var winner = await _userRepository.GetByIdAsync(winnerId);
        var seller = await _userRepository.GetByIdAsync(sellerId);
        if (winner == null || seller == null) return;

        // Tìm winning bid
        var bids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var winningBid = bids
            .Where(b => b.UserId == winnerId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        if (winningBid == null) return;

        var paymentAmount = winningBid.HeldAmount;

        // 1. Trừ từ escrow của winner
        winner.EscrowBalance -= paymentAmount;
        winner.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(winner);

        // 2. Cộng vào available của seller
        seller.AvailableBalance += paymentAmount;
        seller.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(seller);

        // 3. Tạo transactions
        var buyerTransaction = new Transaction
        {
            UserId = winnerId,
            Type = TransactionType.Payment,
            Amount = -paymentAmount,
            Description = $"Thanh toán thắng đấu giá",
            RelatedAuctionId = auctionId,
            RelatedBidId = winningBid.Id,
            BalanceBefore = winner.EscrowBalance + paymentAmount,
            BalanceAfter = winner.EscrowBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(buyerTransaction);

        var sellerTransaction = new Transaction
        {
            UserId = sellerId,
            Type = TransactionType.Payment,
            Amount = paymentAmount,
            Description = $"Nhận thanh toán từ đấu giá",
            RelatedAuctionId = auctionId,
            RelatedBidId = winningBid.Id,
            BalanceBefore = seller.AvailableBalance - paymentAmount,
            BalanceAfter = seller.AvailableBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(sellerTransaction);

        // 4. Mark bid as released
        winningBid.IsHoldReleased = true;
        winningBid.IsWinningBid = true;
        await _bidRepository.UpdateAsync(winningBid);

        _logger.LogInformation(
            "Processed winner payment: Auction {AuctionId}, Winner {WinnerId}, Seller {SellerId}, Amount {Amount}",
            auctionId, winnerId, sellerId, paymentAmount);
    }

    public async Task ReleaseAllHoldsAsync(string auctionId)
    {
        var allBids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var allUserIds = allBids
            .Where(b => !b.IsHoldReleased)
            .Select(b => b.UserId)
            .Distinct()
            .ToList();

        foreach (var userId in allUserIds)
        {
            await ReleaseHoldAsync(userId, auctionId);
        }

        _logger.LogInformation(
            "Released all holds for auction {AuctionId}. Released {Count} users.",
            auctionId, allUserIds.Count);
    }

    public async Task<bool> EnsureHoldAsync(string userId, string auctionId, decimal amount)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return false;

        var userBids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var activeBid = userBids
            .Where(b => b.UserId == userId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        if (activeBid != null && activeBid.HeldAmount >= amount)
        {
            return true; // Đã có hold đủ
        }

        // Cần hold thêm
        var neededAmount = activeBid != null ? amount - activeBid.HeldAmount : amount;

        if (user.AvailableBalance < neededAmount)
        {
            return false; // Không đủ tiền
        }

        // Hold thêm amount
        var balanceBefore = user.AvailableBalance;
        user.AvailableBalance -= neededAmount;
        user.EscrowBalance += neededAmount;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Update bid held amount
        if (activeBid != null)
        {
            activeBid.HeldAmount = amount;
            await _bidRepository.UpdateAsync(activeBid);
        }

        // Tạo transaction
        var holdTransaction = new Transaction
        {
            UserId = userId,
            Type = TransactionType.Hold,
            Amount = -neededAmount,
            Description = $"Đảm bảo hold {amount:N0}đ cho phiên #{auctionId[..8]}",
            RelatedAuctionId = auctionId,
            BalanceBefore = balanceBefore,
            BalanceAfter = user.AvailableBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(holdTransaction);

        _logger.LogInformation(
            "Ensured hold for user {UserId} on auction {AuctionId}. Amount: {Amount}",
            userId, auctionId, amount);

        return true;
    }

    public async Task<bool> ConfirmTransactionAsync(string auctionId, string userId, bool isBuyer)
    {
        var auction = await _auctionRepository.GetByIdAsync(auctionId);
        if (auction == null || auction.Status != AuctionStatus.Completed)
        {
            return false;
        }

        // Validate user role
        if (isBuyer && auction.WinnerId != userId)
        {
            return false; // Không phải buyer
        }
        if (!isBuyer && auction.SellerId != userId)
        {
            return false; // Không phải seller
        }

        // Tìm transaction pending của auction này (transaction của buyer/winner)
        var transactions = await _transactionRepository.GetByAuctionIdAsync(auctionId);
        var pendingTransaction = transactions
            .Where(t => t.Type == TransactionType.Payment && t.Status == TransactionStatus.Pending && t.UserId == auction.WinnerId)
            .FirstOrDefault();

        if (pendingTransaction == null)
        {
            // Tạo transaction mới nếu chưa có
            var bids = await _bidRepository.GetByAuctionIdAsync(auctionId);
            var winningBid = bids
                .Where(b => b.UserId == auction.WinnerId && !b.IsHoldReleased)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

            if (winningBid == null) return false;

            var winner = await _userRepository.GetByIdAsync(auction.WinnerId!);
            if (winner == null) return false;

            pendingTransaction = new Transaction
            {
                UserId = auction.WinnerId!,
                Type = TransactionType.Payment,
                Amount = -winningBid.HeldAmount,
                Description = $"Thanh toán đấu giá - chờ xác nhận",
                RelatedAuctionId = auctionId,
                RelatedBidId = winningBid.Id,
                Status = TransactionStatus.Pending,
                BuyerConfirmed = false,
                SellerConfirmed = false,
                BalanceBefore = winner.EscrowBalance,
                BalanceAfter = winner.EscrowBalance,
                CreatedAt = DateTime.UtcNow
            };
            await _transactionRepository.CreateAsync(pendingTransaction);
        }

        // Update confirm status
        if (isBuyer)
        {
            pendingTransaction.BuyerConfirmed = true;
        }
        else
        {
            pendingTransaction.SellerConfirmed = true;
        }

        await _transactionRepository.UpdateAsync(pendingTransaction);

        // Nếu cả 2 đã confirm, chuyển tiền
        if (pendingTransaction.BuyerConfirmed && pendingTransaction.SellerConfirmed)
        {
            await ProcessWinnerPaymentAsync(auctionId, auction.WinnerId!, auction.SellerId);
            
            // Update transaction status
            pendingTransaction.Status = TransactionStatus.Completed;
            await _transactionRepository.UpdateAsync(pendingTransaction);

            _logger.LogInformation(
                "Transaction confirmed and completed for auction {AuctionId}",
                auctionId);
        }

        return true;
    }

    public async Task<bool> CancelTransactionAsync(string auctionId, string userId)
    {
        var auction = await _auctionRepository.GetByIdAsync(auctionId);
        if (auction == null || auction.Status != AuctionStatus.Completed)
        {
            return false;
        }

        // Chỉ buyer hoặc seller mới được cancel
        if (auction.WinnerId != userId && auction.SellerId != userId)
        {
            return false;
        }

        // Tìm transaction pending
        var transactions = await _transactionRepository.GetByAuctionIdAsync(auctionId);
        var pendingTransaction = transactions
            .Where(t => t.Type == TransactionType.Payment && t.Status == TransactionStatus.Pending)
            .FirstOrDefault();

        if (pendingTransaction == null)
        {
            return false; // Không có transaction pending
        }

        // Lấy winning bid để biết số tiền cần refund
        var bids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var winningBid = bids
            .Where(b => b.UserId == auction.WinnerId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        if (winningBid == null)
        {
            return false; // Không tìm thấy winning bid
        }

        // Lấy balance trước khi release
        var winner = await _userRepository.GetByIdAsync(auction.WinnerId!);
        if (winner == null) return false;
        var balanceBeforeRefund = winner.AvailableBalance;

        // Release hold của winner (sẽ tự động tạo transaction Release)
        await ReleaseHoldAsync(auction.WinnerId!, auctionId);

        // Lấy balance sau khi release
        var winnerAfter = await _userRepository.GetByIdAsync(auction.WinnerId!);
        if (winnerAfter == null) return false;

        // Update transaction status
        pendingTransaction.Status = TransactionStatus.Cancelled;
        await _transactionRepository.UpdateAsync(pendingTransaction);

        // Tạo refund transaction để tracking
        var refundTransaction = new Transaction
        {
            UserId = auction.WinnerId!,
            Type = TransactionType.Refund,
            Amount = winningBid.HeldAmount,
            Description = $"Hoàn tiền - hủy giao dịch",
            RelatedAuctionId = auctionId,
            RelatedBidId = winningBid.Id,
            Status = TransactionStatus.Completed,
            BalanceBefore = balanceBeforeRefund,
            BalanceAfter = winnerAfter.AvailableBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(refundTransaction);

        _logger.LogInformation(
            "Transaction cancelled and refunded for auction {AuctionId} by user {UserId}",
            auctionId, userId);

        return true;
    }
}
