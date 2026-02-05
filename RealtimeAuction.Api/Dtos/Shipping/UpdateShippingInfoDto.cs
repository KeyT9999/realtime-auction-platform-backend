using System.ComponentModel.DataAnnotations;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Shipping;

public class UpdateShippingInfoDto
{
    [MinLength(2)]
    public string? Province { get; set; }

    public ShippingFeeType? FeeType { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Shipping fee must be greater than or equal to 0")]
    public decimal? ShippingFee { get; set; }

    public ShippingMethod? Method { get; set; }
}
