using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Order;
using RealtimeAuction.Api.Services;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderService orderService, ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? throw new UnauthorizedAccessException("User ID not found in claims");
    }

    /// <summary>
    /// Get buyer's orders (won auctions)
    /// </summary>
    [HttpGet("my-orders")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrders()
    {
        try
        {
            var userId = GetUserId();
            var orders = await _orderService.GetBuyerOrdersAsync(userId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting buyer orders");
            return StatusCode(500, new { message = "Lỗi khi tải danh sách đơn hàng" });
        }
    }

    /// <summary>
    /// Get seller's sales
    /// </summary>
    [HttpGet("my-sales")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetMySales()
    {
        try
        {
            var userId = GetUserId();
            var orders = await _orderService.GetSellerOrdersAsync(userId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting seller orders");
            return StatusCode(500, new { message = "Lỗi khi tải danh sách đơn bán" });
        }
    }

    /// <summary>
    /// Get single order by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrder(string id)
    {
        try
        {
            var userId = GetUserId();
            var order = await _orderService.GetByIdAsync(id);
            
            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không tồn tại" });
            }

            // Only buyer or seller can view the order
            if (order.BuyerId != userId && order.SellerId != userId)
            {
                return Forbid();
            }

            var buyer = await _orderService.GetBuyerOrdersAsync(order.BuyerId);
            var orderDto = buyer.FirstOrDefault(o => o.Id == id);
            
            return Ok(orderDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order {OrderId}", id);
            return StatusCode(500, new { message = "Lỗi khi tải thông tin đơn hàng" });
        }
    }

    /// <summary>
    /// Seller marks order as shipped
    /// </summary>
    [HttpPost("{id}/ship")]
    public async Task<ActionResult<OrderResult>> ShipOrder(string id, [FromBody] ShipOrderRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _orderService.MarkAsShippedAsync(id, userId, request);

            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error shipping order {OrderId}", id);
            return StatusCode(500, new { message = "Lỗi khi cập nhật trạng thái gửi hàng" });
        }
    }

    /// <summary>
    /// Buyer confirms receipt
    /// </summary>
    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<OrderResult>> ConfirmOrder(string id)
    {
        try
        {
            var userId = GetUserId();
            var result = await _orderService.ConfirmReceivedAsync(id, userId);

            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming order {OrderId}", id);
            return StatusCode(500, new { message = "Lỗi khi xác nhận nhận hàng" });
        }
    }

    /// <summary>
    /// Cancel order and refund
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<OrderResult>> CancelOrder(string id, [FromBody] CancelOrderRequest? request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _orderService.CancelOrderAsync(id, userId, request?.Reason);

            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", id);
            return StatusCode(500, new { message = "Lỗi khi hủy đơn hàng" });
        }
    }
}
