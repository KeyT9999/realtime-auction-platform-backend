using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromForm] CreateProductDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var product = await _productService.CreateAsync(dto, userId);
            return Ok(product);
        }

        [HttpPut("{id}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(string id)
        {
            var product = await _productService.ApproveAsync(id);
            if (product == null) return NotFound();
            return Ok(product);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] ProductFilterDto filter)
        {
            var products = await _productService.SearchAsync(filter);
            return Ok(products);
        }
    }
}
