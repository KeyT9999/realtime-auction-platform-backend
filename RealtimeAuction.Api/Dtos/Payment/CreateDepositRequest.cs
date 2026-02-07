using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Payment;

public class CreateDepositRequest
{
    [Required]
    [Range(2000, 100000000, ErrorMessage = "Số tiền nạp tối thiểu 2,000đ và tối đa 100,000,000đ")]
    public decimal Amount { get; set; }

    public string? Description { get; set; }
}
