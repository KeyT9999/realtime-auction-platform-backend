// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho ChangeRoleRequest.
using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Admin;

public class ChangeRoleRequest
{
    [Required]
    public string Role { get; set; } = null!;
}
