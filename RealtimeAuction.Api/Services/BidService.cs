using System.Collections.Concurrent;
using MongoDB.Driver;
using RealtimeAuction.Api.Dtos.Bid;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Services;

public class BidService : IBidService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _auctionLocks = new();

    private readonly IBidRepository _bidRepository;
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<BidService> _logger;
    private readonly IMongoClient _mongoClient;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Auction> _auctions;
    private readonly IMongoCollection<Bid> _bids;
    private readonly IMongoCollection<Transaction> _transactions;

    private static SemaphoreSlim GetAuctionLock(string auctionId)
    {
        return _auctionLocks.GetOrAdd(auctionId, _ => new SemaphoreSlim(1, 1));
    }

    public static void RemoveAuctionLock(string auctionId)
    {
        if (_auctionLocks.TryRemove(auctionId, out var semaphore))
        {
            semaphore.Dispose();
        }
    }

    public BidService(
        IBidRepository bidRepository,
        IAuctionRepository auctionRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IMongoClient mongoClient,
        IMongoDatabase database,
        IEmailService emailService,
        ILogger<BidService> logger)
    {
        _bidRepository = bidRepository;
        _auctionRepository = auctionRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _mongoClient = mongoClient;
        _users = database.GetCollection<User>("Users");
        _auctions = database.GetCollection<Auction>("Auctions");
        _bids = database.GetCollection<Bid>("Bids");
        _transactions = database.GetCollection<Transaction>("Transactions");
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<BidResult> PlaceBidAsync(string userId, CreateBidDto request)
    {
        var semaphore = GetAuctionLock(request.AuctionId);
        await semaphore.WaitAsync();

        try
        {
            return await PlaceBidCoreAsync(userId, request);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<BidResult> PlaceBidCoreAsync(string userId, CreateBidDto request)
    {
        var userTask = _userRepository.GetByIdAsync(userId);
        var auctionTask = _auctionRepository.GetByIdAsync(request.AuctionId);
        await Task.WhenAll(userTask, auctionTask);

        var user = userTask.Result;
        var auction = auctionTask.Result;

        if (user == null)
        {
            return new BidResult { Success = false, ErrorMessage = "Nguoi dung khong ton tai" };
        }

        if (auction == null)
        {
            return new BidResult { Success = false, ErrorMessage = "Phien dau gia khong ton tai" };
        }

        if (auction.Status != AuctionStatus.Active)
        {
            return new BidResult { Success = false, ErrorMessage = "Phien dau gia khong con hoat dong" };
        }

        if (auction.SellerId == userId)
        {
            return new BidResult { Success = false, ErrorMessage = "Ban khong the dau gia san pham cua chinh minh" };
        }

        var minBid = auction.CurrentPrice + auction.BidIncrement;
        if (request.Amount < minBid)
        {
            return new BidResult { Success = false, ErrorMessage = $"Gia dat toi thieu phai la {minBid:N0}d" };
        }

        var auctionBids = await _bidRepository.GetByAuctionIdAsync(request.AuctionId);
        var previousUserBid = auctionBids
            .Where(b => b.UserId == userId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        var holdAmount = previousUserBid != null
            ? request.Amount - previousUserBid.HeldAmount
            : request.Amount;

        if (holdAmount < 0)
        {
            return new BidResult { Success = false, ErrorMessage = "So tien hold khong hop le" };
        }

        if (user.AvailableBalance < holdAmount)
        {
            return new BidResult
            {
                Success = false,
                ErrorMessage = $"So du khong du. Can {holdAmount:N0}d, hien co {user.AvailableBalance:N0}d"
            };
        }

        var previousHighestBid = auctionBids
            .Where(b => b.UserId != userId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        var outbidUserId = previousHighestBid?.UserId;
        var outbidHeldAmount = previousHighestBid != null
            ? auctionBids
                .Where(b => b.UserId == previousHighestBid.UserId && !b.IsHoldReleased)
                .Select(b => b.HeldAmount)
                .DefaultIfEmpty(previousHighestBid.HeldAmount)
                .Max()
            : 0m;

        var context = new BidPlacementContext(
            user,
            auction,
            auctionBids,
            previousUserBid,
            holdAmount,
            outbidUserId,
            outbidHeldAmount);

        try
        {
            return await PlaceBidWithTransactionAsync(userId, request, context);
        }
        catch (Exception ex) when (IsTransactionSupportException(ex))
        {
            _logger.LogWarning(ex, "MongoDB transactions are not supported. Falling back to sequential bid processing.");
            return await PlaceBidWithoutTransactionAsync(userId, request, context);
        }
    }

    private async Task<BidResult> PlaceBidWithTransactionAsync(string userId, CreateBidDto request, BidPlacementContext context)
    {
        using var session = await _mongoClient.StartSessionAsync();
        session.StartTransaction();

        try
        {
            var now = DateTime.UtcNow;

            var bidderUpdateResult = await _users.UpdateOneAsync(
                session,
                Builders<User>.Filter.And(
                    Builders<User>.Filter.Eq(u => u.Id, userId),
                    Builders<User>.Filter.Gte(u => u.AvailableBalance, context.HoldAmount)),
                Builders<User>.Update
                    .Inc(u => u.AvailableBalance, -context.HoldAmount)
                    .Inc(u => u.EscrowBalance, context.HoldAmount)
                    .Set(u => u.UpdatedAt, now));

            if (bidderUpdateResult.ModifiedCount == 0)
            {
                throw new InvalidOperationException("So du khong du de dat gia.");
            }

            var holdTransaction = BuildHoldTransaction(userId, request, context, now);
            var createdBid = BuildBid(userId, request, context, now);

            await _transactions.InsertOneAsync(session, holdTransaction);
            await _bids.InsertOneAsync(session, createdBid);

            var auctionUpdateResult = await _auctions.UpdateOneAsync(
                session,
                Builders<Auction>.Filter.And(
                    Builders<Auction>.Filter.Eq(a => a.Id, request.AuctionId),
                    Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active),
                    Builders<Auction>.Filter.Eq(a => a.CurrentPrice, context.Auction.CurrentPrice)),
                Builders<Auction>.Update
                    .Set(a => a.CurrentPrice, request.Amount)
                    .Set(a => a.UpdatedAt, now)
                    .Inc(a => a.BidCount, 1));

            if (auctionUpdateResult.ModifiedCount == 0)
            {
                throw new InvalidOperationException("Gia hien tai da thay doi. Vui long tai lai va thu lai.");
            }

            if (!string.IsNullOrWhiteSpace(context.OutbidUserId) && context.OutbidHeldAmount > 0)
            {
                var outbidUser = await _users
                    .Find(session, Builders<User>.Filter.Eq(u => u.Id, context.OutbidUserId))
                    .FirstOrDefaultAsync();

                if (outbidUser == null)
                {
                    throw new InvalidOperationException("Khong tim thay nguoi dung bi outbid.");
                }

                var outbidUpdateResult = await _users.UpdateOneAsync(
                    session,
                    Builders<User>.Filter.And(
                        Builders<User>.Filter.Eq(u => u.Id, context.OutbidUserId),
                        Builders<User>.Filter.Gte(u => u.EscrowBalance, context.OutbidHeldAmount)),
                    Builders<User>.Update
                        .Inc(u => u.AvailableBalance, context.OutbidHeldAmount)
                        .Inc(u => u.EscrowBalance, -context.OutbidHeldAmount)
                        .Set(u => u.UpdatedAt, now));

                if (outbidUpdateResult.ModifiedCount == 0)
                {
                    throw new InvalidOperationException("Khong the giai phong hold cua nguoi bi outbid.");
                }

                await _transactions.InsertOneAsync(
                    session,
                    BuildReleaseTransaction(
                        context.OutbidUserId,
                        request.AuctionId,
                        context.OutbidHeldAmount,
                        outbidUser.AvailableBalance,
                        outbidUser.AvailableBalance + context.OutbidHeldAmount,
                        now));

                await _bids.UpdateManyAsync(
                    session,
                    Builders<Bid>.Filter.And(
                        Builders<Bid>.Filter.Eq(b => b.AuctionId, request.AuctionId),
                        Builders<Bid>.Filter.Eq(b => b.UserId, context.OutbidUserId),
                        Builders<Bid>.Filter.Eq(b => b.IsHoldReleased, false)),
                    Builders<Bid>.Update.Set(b => b.IsHoldReleased, true));
            }

            await session.CommitTransactionAsync();

            _logger.LogInformation(
                "Bid placed transactionally: User {UserId} bid {Amount} on auction {AuctionId}. Held {HeldAmount}",
                userId, request.Amount, request.AuctionId, context.HoldAmount);

            return new BidResult
            {
                Success = true,
                Bid = createdBid,
                OutbidUserId = context.OutbidUserId
            };
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }
    }

    private async Task<BidResult> PlaceBidWithoutTransactionAsync(string userId, CreateBidDto request, BidPlacementContext context)
    {
        var user = context.User;
        var auction = context.Auction;

        var balanceBefore = user.AvailableBalance;
        user.AvailableBalance -= context.HoldAmount;
        user.EscrowBalance += context.HoldAmount;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        await _transactionRepository.CreateAsync(new Transaction
        {
            UserId = userId,
            Type = TransactionType.Hold,
            Amount = -context.HoldAmount,
            Description = $"Dat gia {request.Amount:N0}d cho phien #{auction.Id?[..8]}",
            RelatedAuctionId = request.AuctionId,
            BalanceBefore = balanceBefore,
            BalanceAfter = user.AvailableBalance,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var createdBid = await _bidRepository.CreateAsync(
            BuildBid(userId, request, context, DateTime.UtcNow),
            auction.CurrentPrice,
            auction.BidIncrement);

        await _auctionRepository.UpdateCurrentPriceAsync(request.AuctionId, request.Amount);

        if (!string.IsNullOrWhiteSpace(context.OutbidUserId))
        {
            await ReleaseHoldAsync(context.OutbidUserId, request.AuctionId);
        }

        _logger.LogInformation(
            "Bid placed sequentially: User {UserId} bid {Amount} on auction {AuctionId}. Held {HeldAmount}",
            userId, request.Amount, request.AuctionId, context.HoldAmount);

        return new BidResult
        {
            Success = true,
            Bid = createdBid,
            OutbidUserId = context.OutbidUserId
        };
    }

    private static Bid BuildBid(string userId, CreateBidDto request, BidPlacementContext context, DateTime timestamp)
    {
        return new Bid
        {
            AuctionId = request.AuctionId,
            UserId = userId,
            Amount = request.Amount,
            HeldAmount = context.PreviousUserBid != null
                ? context.PreviousUserBid.HeldAmount + context.HoldAmount
                : context.HoldAmount,
            IsHoldReleased = false,
            Timestamp = timestamp,
            CreatedAt = timestamp,
            AutoBid = request.AutoBid != null
                ? new Bid.AutoBidSettings
                {
                    MaxBid = request.AutoBid.MaxBid,
                    IsActive = request.AutoBid.IsActive
                }
                : null
        };
    }

    private static Transaction BuildHoldTransaction(string userId, CreateBidDto request, BidPlacementContext context, DateTime createdAt)
    {
        return new Transaction
        {
            UserId = userId,
            Type = TransactionType.Hold,
            Amount = -context.HoldAmount,
            Description = $"Dat gia {request.Amount:N0}d cho phien #{context.Auction.Id?[..8]}",
            RelatedAuctionId = request.AuctionId,
            BalanceBefore = context.User.AvailableBalance,
            BalanceAfter = context.User.AvailableBalance - context.HoldAmount,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    private static Transaction BuildReleaseTransaction(
        string userId,
        string auctionId,
        decimal amount,
        decimal balanceBefore,
        decimal balanceAfter,
        DateTime createdAt)
    {
        return new Transaction
        {
            UserId = userId,
            Type = TransactionType.Release,
            Amount = amount,
            Description = "Hoan tra coc - bi outbid",
            RelatedAuctionId = auctionId,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    private static bool IsTransactionSupportException(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("Transaction numbers are only allowed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("replica set member or mongos", StringComparison.OrdinalIgnoreCase)
            || message.Contains("do not support transactions", StringComparison.OrdinalIgnoreCase)
            || message.Contains("transactions are not supported", StringComparison.OrdinalIgnoreCase);
    }

    public async Task ReleaseHoldAsync(string userId, string auctionId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return;
        }

        var userBids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var unreleased = userBids
            .Where(b => b.UserId == userId && !b.IsHoldReleased)
            .ToList();

        if (!unreleased.Any())
        {
            return;
        }

        var totalHeld = unreleased.Max(b => b.HeldAmount);
        var balanceBefore = user.AvailableBalance;

        user.AvailableBalance += totalHeld;
        user.EscrowBalance -= totalHeld;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        var releaseTransaction = new Transaction
        {
            UserId = userId,
            Type = TransactionType.Release,
            Amount = totalHeld,
            Description = "Hoan tra coc - bi outbid",
            RelatedAuctionId = auctionId,
            BalanceBefore = balanceBefore,
            BalanceAfter = user.AvailableBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(releaseTransaction);

        foreach (var bid in unreleased)
        {
            bid.IsHoldReleased = true;
            await _bidRepository.UpdateAsync(bid);
        }

        _logger.LogInformation(
            "Released hold for user {UserId} on auction {AuctionId}. Amount {Amount}",
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
        if (winner == null || seller == null)
        {
            return;
        }

        var bids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var winningBid = bids
            .Where(b => b.UserId == winnerId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        if (winningBid == null)
        {
            return;
        }

        var paymentAmount = winningBid.HeldAmount;

        winner.EscrowBalance -= paymentAmount;
        winner.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(winner);

        seller.AvailableBalance += paymentAmount;
        seller.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(seller);

        var buyerTransaction = new Transaction
        {
            UserId = winnerId,
            Type = TransactionType.Payment,
            Amount = -paymentAmount,
            Description = "Thanh toan thang dau gia",
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
            Description = "Nhan thanh toan tu dau gia",
            RelatedAuctionId = auctionId,
            RelatedBidId = winningBid.Id,
            BalanceBefore = seller.AvailableBalance - paymentAmount,
            BalanceAfter = seller.AvailableBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(sellerTransaction);

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
        if (user == null)
        {
            return false;
        }

        var userBids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var activeBid = userBids
            .Where(b => b.UserId == userId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        if (activeBid != null && activeBid.HeldAmount >= amount)
        {
            return true;
        }

        var neededAmount = activeBid != null ? amount - activeBid.HeldAmount : amount;
        if (user.AvailableBalance < neededAmount)
        {
            return false;
        }

        var balanceBefore = user.AvailableBalance;
        user.AvailableBalance -= neededAmount;
        user.EscrowBalance += neededAmount;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        if (activeBid != null)
        {
            activeBid.HeldAmount = amount;
            await _bidRepository.UpdateAsync(activeBid);
        }

        var holdTransaction = new Transaction
        {
            UserId = userId,
            Type = TransactionType.Hold,
            Amount = -neededAmount,
            Description = $"Dam bao hold {amount:N0}d cho phien #{auctionId[..8]}",
            RelatedAuctionId = auctionId,
            BalanceBefore = balanceBefore,
            BalanceAfter = user.AvailableBalance,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(holdTransaction);

        _logger.LogInformation(
            "Ensured hold for user {UserId} on auction {AuctionId}. Amount {Amount}",
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

        if (isBuyer && auction.WinnerId != userId)
        {
            return false;
        }

        if (!isBuyer && auction.SellerId != userId)
        {
            return false;
        }

        var transactions = await _transactionRepository.GetByAuctionIdAsync(auctionId);
        var pendingTransaction = transactions
            .Where(t => t.Type == TransactionType.Payment && t.Status == TransactionStatus.Pending && t.UserId == auction.WinnerId)
            .FirstOrDefault();

        if (pendingTransaction == null)
        {
            var bids = await _bidRepository.GetByAuctionIdAsync(auctionId);
            var winningBid = bids
                .Where(b => b.UserId == auction.WinnerId && !b.IsHoldReleased)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

            if (winningBid == null)
            {
                return false;
            }

            var winner = await _userRepository.GetByIdAsync(auction.WinnerId!);
            if (winner == null)
            {
                return false;
            }

            pendingTransaction = new Transaction
            {
                UserId = auction.WinnerId!,
                Type = TransactionType.Payment,
                Amount = -winningBid.HeldAmount,
                Description = "Thanh toan dau gia - cho xac nhan",
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

        if (isBuyer)
        {
            pendingTransaction.BuyerConfirmed = true;
        }
        else
        {
            pendingTransaction.SellerConfirmed = true;
        }

        await _transactionRepository.UpdateAsync(pendingTransaction);

        if (pendingTransaction.BuyerConfirmed && pendingTransaction.SellerConfirmed)
        {
            await ProcessWinnerPaymentAsync(auctionId, auction.WinnerId!, auction.SellerId);

            pendingTransaction.Status = TransactionStatus.Completed;
            await _transactionRepository.UpdateAsync(pendingTransaction);

            var buyer = await _userRepository.GetByIdAsync(auction.WinnerId!);
            var seller = await _userRepository.GetByIdAsync(auction.SellerId);
            var transactionDate = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm");
            var finalAmount = FormatCurrency(Math.Abs(pendingTransaction.Amount));

            if (buyer != null && !string.IsNullOrEmpty(buyer.Email))
            {
                try
                {
                    await _emailService.SendTransactionCompletedEmailAsync(
                        buyer.Email,
                        buyer.FullName,
                        "Nguoi mua",
                        auction.Title,
                        finalAmount,
                        transactionDate);

                    _logger.LogInformation("Sent transaction completed email to buyer {Email}", buyer.Email);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send transaction completed email to buyer {Email}", buyer.Email);
                }
            }

            if (seller != null && !string.IsNullOrEmpty(seller.Email))
            {
                try
                {
                    await _emailService.SendTransactionCompletedEmailAsync(
                        seller.Email,
                        seller.FullName,
                        "Nguoi ban",
                        auction.Title,
                        finalAmount,
                        transactionDate);

                    _logger.LogInformation("Sent transaction completed email to seller {Email}", seller.Email);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send transaction completed email to seller {Email}", seller.Email);
                }
            }

            _logger.LogInformation("Transaction confirmed and completed for auction {AuctionId}", auctionId);
        }

        return true;
    }

    private static string FormatCurrency(decimal amount)
    {
        return string.Format(new System.Globalization.CultureInfo("vi-VN"), "{0:N0} VND", amount);
    }

    public async Task<bool> CancelTransactionAsync(string auctionId, string userId)
    {
        var auction = await _auctionRepository.GetByIdAsync(auctionId);
        if (auction == null || auction.Status != AuctionStatus.Completed)
        {
            return false;
        }

        if (auction.WinnerId != userId && auction.SellerId != userId)
        {
            return false;
        }

        var transactions = await _transactionRepository.GetByAuctionIdAsync(auctionId);
        var pendingTransaction = transactions
            .Where(t => t.Type == TransactionType.Payment && t.Status == TransactionStatus.Pending)
            .FirstOrDefault();

        if (pendingTransaction == null)
        {
            return false;
        }

        var bids = await _bidRepository.GetByAuctionIdAsync(auctionId);
        var winningBid = bids
            .Where(b => b.UserId == auction.WinnerId && !b.IsHoldReleased)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        if (winningBid == null)
        {
            return false;
        }

        var winner = await _userRepository.GetByIdAsync(auction.WinnerId!);
        if (winner == null)
        {
            return false;
        }

        var balanceBeforeRefund = winner.AvailableBalance;
        await ReleaseHoldAsync(auction.WinnerId!, auctionId);

        var winnerAfter = await _userRepository.GetByIdAsync(auction.WinnerId!);
        if (winnerAfter == null)
        {
            return false;
        }

        pendingTransaction.Status = TransactionStatus.Cancelled;
        await _transactionRepository.UpdateAsync(pendingTransaction);

        var refundTransaction = new Transaction
        {
            UserId = auction.WinnerId!,
            Type = TransactionType.Refund,
            Amount = winningBid.HeldAmount,
            Description = "Hoan tien - huy giao dich",
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

    private sealed record BidPlacementContext(
        User User,
        Auction Auction,
        List<Bid> AuctionBids,
        Bid? PreviousUserBid,
        decimal HoldAmount,
        string? OutbidUserId,
        decimal OutbidHeldAmount);
}
