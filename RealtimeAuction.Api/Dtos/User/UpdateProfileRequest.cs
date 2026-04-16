// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho UpdateProfileRequest.
using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.User;

public class UpdateProfileRequest
{
    [Required]
    [MinLength(2)]
    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Address { get; set; }
}
