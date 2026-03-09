using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Dispute;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public class DisputeController : ControllerBase
{
    private readonly IDisputeRepository _disputeRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IUserRepository _userRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly IEmailService _emailService;

    public DisputeController(
        IDisputeRepository disputeRepo,
        IOrderRepository orderRepo,
        IUserRepository userRepo,
        INotificationRepository notificationRepo,
        IEmailService emailService)
    {
        _disputeRepo = disputeRepo;
        _orderRepo = orderRepo;
        _userRepo = userRepo;
        _notificationRepo = notificationRepo;
        _emailService = emailService;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private bool IsAdmin() => User.IsInRole("Admin");

    // ═══════════════════════════════════════════
    // POST /api/disputes — Tạo tranh chấp mới
    // ═══════════════════════════════════════════
    [HttpPost]
    public async Task<IActionResult> CreateDispute([FromBody] CreateDisputeRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length < 10)
            return BadRequest(new { message = "Mô tả tranh chấp phải có ít nhất 10 ký tự" });

        // Check existing dispute for this order
        var existingDispute = await _disputeRepo.GetByOrderIdAsync(request.OrderId);
        if (existingDispute != null && existingDispute.Status != DisputeStatus.Closed)
            return BadRequest(new { message = "Đơn hàng này đã có tranh chấp đang mở" });

        // Get order
        var order = await _orderRepo.GetByIdAsync(request.OrderId);
        if (order == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng" });

        // Check user is buyer or seller
        var isBuyer = order.BuyerId == userId;
        var isSeller = order.SellerId == userId;
        if (!isBuyer && !isSeller)
            return Forbid();

        // Get user names
        var buyer = await _userRepo.GetByIdAsync(order.BuyerId);
        var seller = await _userRepo.GetByIdAsync(order.SellerId);

        var dispute = new Dispute
        {
            OrderId = request.OrderId,
            AuctionId = order.AuctionId,
            BuyerId = order.BuyerId,
            SellerId = order.SellerId,
            OpenedBy = isBuyer ? "Buyer" : "Seller",
            Reason = request.Reason,
            Description = request.Description,
            EvidenceImages = request.EvidenceImages ?? new List<string>(),
            Status = DisputeStatus.Open,
            ProductTitle = order.ProductTitle,
            ProductImage = order.ProductImage,
            BuyerName = buyer?.FullName,
            SellerName = seller?.FullName
        };

        await _disputeRepo.CreateAsync(dispute);

        // Update order status to Disputed
        order.Status = OrderStatus.Disputed;
        order.UpdatedAt = DateTime.UtcNow;
        await _orderRepo.UpdateAsync(order);

        // Notify the other party
        var otherUserId = isBuyer ? order.SellerId : order.BuyerId;
        var openerName = isBuyer ? buyer?.FullName : seller?.FullName;

        await _notificationRepo.CreateAsync(new Notification
        {
            UserId = otherUserId,
            Type = "DisputeOpened",
            Title = "Tranh chấp mới",
            Message = $"{openerName} đã mở tranh chấp cho đơn hàng \"{order.ProductTitle}\"",
            RelatedId = dispute.Id
        });

        // Email the other party
        var otherUser = isBuyer ? seller : buyer;
        if (otherUser != null)
        {
            try
            {
                await _emailService.SendDisputeOpenedEmailAsync(
                    otherUser.Email, otherUser.FullName,
                    order.ProductTitle, openerName ?? "Người dùng",
                    GetReasonText(request.Reason));
            }
            catch { /* Don't fail on email error */ }
        }

        return Ok(MapToDto(dispute));
    }

    // ═══════════════════════════════════════════
    // GET /api/disputes — Admin: danh sách tất cả
    // ═══════════════════════════════════════════
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllDisputes(
        [FromQuery] DisputeStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var disputes = await _disputeRepo.GetAllAsync(status, page, pageSize);
        var totalCount = await _disputeRepo.GetCountAsync(status);

        return Ok(new
        {
            items = disputes.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    // ═══════════════════════════════════════════
    // GET /api/disputes/my — Tranh chấp của tôi
    // ═══════════════════════════════════════════
    [HttpGet("my")]
    public async Task<IActionResult> GetMyDisputes()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var disputes = await _disputeRepo.GetByUserIdAsync(userId);
        return Ok(disputes.Select(MapToDto).ToList());
    }

    // ═══════════════════════════════════════════
    // GET /api/disputes/{id} — Chi tiết tranh chấp
    // ═══════════════════════════════════════════
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDispute(string id)
    {
        var dispute = await _disputeRepo.GetByIdAsync(id);
        if (dispute == null)
            return NotFound(new { message = "Không tìm thấy tranh chấp" });

        var userId = GetUserId();
        if (!IsAdmin() && dispute.BuyerId != userId && dispute.SellerId != userId)
            return Forbid();

        return Ok(MapToDto(dispute));
    }

    // ═══════════════════════════════════════════
    // POST /api/disputes/{id}/messages — Gửi tin nhắn
    // ═══════════════════════════════════════════
    [HttpPost("{id}/messages")]
    public async Task<IActionResult> AddMessage(string id, [FromBody] AddDisputeMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "Nội dung tin nhắn không được để trống" });

        var dispute = await _disputeRepo.GetByIdAsync(id);
        if (dispute == null)
            return NotFound(new { message = "Không tìm thấy tranh chấp" });

        var userId = GetUserId();
        var isAdmin = IsAdmin();
        if (!isAdmin && dispute.BuyerId != userId && dispute.SellerId != userId)
            return Forbid();

        if (dispute.Status == DisputeStatus.Closed ||
            dispute.Status == DisputeStatus.ResolvedBuyerWins ||
            dispute.Status == DisputeStatus.ResolvedSellerWins)
            return BadRequest(new { message = "Tranh chấp đã được giải quyết, không thể gửi tin nhắn" });

        var user = await _userRepo.GetByIdAsync(userId);
        string senderRole;
        if (isAdmin) senderRole = "Admin";
        else if (userId == dispute.BuyerId) senderRole = "Buyer";
        else senderRole = "Seller";

        var message = new DisputeMessage
        {
            SenderId = userId,
            SenderName = user?.FullName ?? "Người dùng",
            SenderRole = senderRole,
            Content = request.Content,
            Attachments = request.Attachments ?? new List<string>()
        };

        await _disputeRepo.AddMessageAsync(id, message);

        return Ok(new DisputeMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderName = message.SenderName,
            SenderRole = message.SenderRole,
            Content = message.Content,
            Attachments = message.Attachments,
            CreatedAt = message.CreatedAt
        });
    }

    // ═══════════════════════════════════════════
    // POST /api/disputes/{id}/review — Admin tiếp nhận
    // ═══════════════════════════════════════════
    [HttpPost("{id}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReviewDispute(string id)
    {
        var dispute = await _disputeRepo.GetByIdAsync(id);
        if (dispute == null)
            return NotFound(new { message = "Không tìm thấy tranh chấp" });

        if (dispute.Status != DisputeStatus.Open)
            return BadRequest(new { message = "Chỉ có thể tiếp nhận tranh chấp đang mở" });

        var adminId = GetUserId();
        dispute.Status = DisputeStatus.UnderReview;
        dispute.AdminId = adminId;
        dispute.ReviewedAt = DateTime.UtcNow;

        await _disputeRepo.UpdateAsync(id, dispute);

        // Notify both parties
        var admin = await _userRepo.GetByIdAsync(adminId);
        foreach (var uid in new[] { dispute.BuyerId, dispute.SellerId })
        {
            await _notificationRepo.CreateAsync(new Notification
            {
                UserId = uid,
                Type = "DisputeUnderReview",
                Title = "Tranh chấp đang được xem xét",
                Message = $"Admin {admin?.FullName} đã tiếp nhận tranh chấp cho \"{dispute.ProductTitle}\"",
                RelatedId = dispute.Id
            });
        }

        return Ok(MapToDto(dispute));
    }

    // ═══════════════════════════════════════════
    // POST /api/disputes/{id}/resolve — Admin phán quyết
    // ═══════════════════════════════════════════
    [HttpPost("{id}/resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResolveDispute(string id, [FromBody] ResolveDisputeRequest request)
    {
        var dispute = await _disputeRepo.GetByIdAsync(id);
        if (dispute == null)
            return NotFound(new { message = "Không tìm thấy tranh chấp" });

        if (dispute.Status != DisputeStatus.UnderReview && dispute.Status != DisputeStatus.Open)
            return BadRequest(new { message = "Tranh chấp không ở trạng thái có thể phán quyết" });

        if (request.Resolution != DisputeStatus.ResolvedBuyerWins &&
            request.Resolution != DisputeStatus.ResolvedSellerWins)
            return BadRequest(new { message = "Phán quyết phải là BuyerWins (2) hoặc SellerWins (3)" });

        var adminId = GetUserId();
        dispute.Status = request.Resolution;
        dispute.AdminId = adminId;
        dispute.AdminNote = request.AdminNote;
        dispute.Resolution = request.ResolutionDetail;
        dispute.ResolvedAt = DateTime.UtcNow;

        await _disputeRepo.UpdateAsync(id, dispute);

        // Get names
        var admin = await _userRepo.GetByIdAsync(adminId);
        var buyer = await _userRepo.GetByIdAsync(dispute.BuyerId);
        var seller = await _userRepo.GetByIdAsync(dispute.SellerId);

        var winnerName = request.Resolution == DisputeStatus.ResolvedBuyerWins
            ? buyer?.FullName : seller?.FullName;
        var statusText = request.Resolution == DisputeStatus.ResolvedBuyerWins
            ? "Người mua thắng" : "Người bán thắng";

        // Notify both
        foreach (var uid in new[] { dispute.BuyerId, dispute.SellerId })
        {
            await _notificationRepo.CreateAsync(new Notification
            {
                UserId = uid,
                Type = "DisputeResolved",
                Title = "Tranh chấp đã được giải quyết",
                Message = $"Kết quả: {statusText}. Lý do: {request.AdminNote}",
                RelatedId = dispute.Id
            });
        }

        // Email both
        foreach (var u in new[] { buyer, seller })
        {
            if (u == null) continue;
            try
            {
                await _emailService.SendDisputeResolvedEmailAsync(
                    u.Email, u.FullName, dispute.ProductTitle ?? "",
                    statusText, request.AdminNote);
            }
            catch { /* Don't fail on email error */ }
        }

        return Ok(MapToDto(dispute));
    }

    // ═══════════════════════════════════════════
    // POST /api/disputes/{id}/close — Đóng tranh chấp
    // ═══════════════════════════════════════════
    [HttpPost("{id}/close")]
    public async Task<IActionResult> CloseDispute(string id)
    {
        var dispute = await _disputeRepo.GetByIdAsync(id);
        if (dispute == null)
            return NotFound(new { message = "Không tìm thấy tranh chấp" });

        var userId = GetUserId();
        var isAdmin = IsAdmin();

        // Only admin or the person who opened can close
        var openedByUserId = dispute.OpenedBy == "Buyer" ? dispute.BuyerId : dispute.SellerId;
        if (!isAdmin && userId != openedByUserId)
            return Forbid();

        if (dispute.Status == DisputeStatus.ResolvedBuyerWins ||
            dispute.Status == DisputeStatus.ResolvedSellerWins ||
            dispute.Status == DisputeStatus.Closed)
            return BadRequest(new { message = "Tranh chấp đã được giải quyết hoặc đóng" });

        dispute.Status = DisputeStatus.Closed;
        dispute.ClosedAt = DateTime.UtcNow;
        if (isAdmin) dispute.AdminId = userId;

        await _disputeRepo.UpdateAsync(id, dispute);

        // Notify the other party
        var otherUserId = userId == dispute.BuyerId ? dispute.SellerId : dispute.BuyerId;
        if (otherUserId != userId)
        {
            await _notificationRepo.CreateAsync(new Notification
            {
                UserId = otherUserId,
                Type = "DisputeClosed",
                Title = "Tranh chấp đã được đóng",
                Message = $"Tranh chấp cho \"{dispute.ProductTitle}\" đã được đóng",
                RelatedId = dispute.Id
            });
        }

        return Ok(MapToDto(dispute));
    }

    // ═══════════════════════════════════════════
    // Helper: Map Dispute → DTO
    // ═══════════════════════════════════════════
    private static DisputeResponseDto MapToDto(Dispute d) => new()
    {
        Id = d.Id ?? "",
        OrderId = d.OrderId,
        AuctionId = d.AuctionId,
        BuyerId = d.BuyerId,
        SellerId = d.SellerId,
        OpenedBy = d.OpenedBy,
        Reason = d.Reason,
        Description = d.Description,
        EvidenceImages = d.EvidenceImages,
        Status = d.Status,
        AdminId = d.AdminId,
        AdminNote = d.AdminNote,
        Resolution = d.Resolution,
        ProductTitle = d.ProductTitle,
        ProductImage = d.ProductImage,
        BuyerName = d.BuyerName,
        SellerName = d.SellerName,
        Messages = d.Messages.Select(m => new DisputeMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderName = m.SenderName,
            SenderRole = m.SenderRole,
            Content = m.Content,
            Attachments = m.Attachments,
            CreatedAt = m.CreatedAt
        }).ToList(),
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        ReviewedAt = d.ReviewedAt,
        ResolvedAt = d.ResolvedAt,
        ClosedAt = d.ClosedAt
    };

    private static string GetReasonText(DisputeReason reason) => reason switch
    {
        DisputeReason.ItemNotReceived => "Không nhận được hàng",
        DisputeReason.ItemNotAsDescribed => "Không đúng mô tả",
        DisputeReason.ItemDamaged => "Hàng bị hỏng",
        DisputeReason.WrongItem => "Giao nhầm hàng",
        DisputeReason.SellerNotShipping => "Người bán không giao hàng",
        DisputeReason.BuyerNotPaying => "Người mua không thanh toán",
        DisputeReason.FraudSuspected => "Nghi lừa đảo",
        DisputeReason.Other => "Lý do khác",
        _ => "Không xác định"
    };
}
