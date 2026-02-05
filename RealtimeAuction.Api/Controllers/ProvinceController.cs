using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/provinces")]
public class ProvinceController : ControllerBase
{
    private readonly IProvinceService _provinceService;

    public ProvinceController(IProvinceService provinceService)
    {
        _provinceService = provinceService;
    }

    [HttpGet]
    public IActionResult GetProvinces()
    {
        try
        {
            var provinces = _provinceService.GetProvinces();
            return Ok(provinces);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
