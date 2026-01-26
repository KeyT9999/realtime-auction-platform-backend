using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Admin;

public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = null!;

    [Required]
    [MinLength(2)]
    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string Role { get; set; } = "User";
}
