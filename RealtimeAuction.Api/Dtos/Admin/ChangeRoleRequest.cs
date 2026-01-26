using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Admin;

public class ChangeRoleRequest
{
    [Required]
    public string Role { get; set; } = null!;
}
