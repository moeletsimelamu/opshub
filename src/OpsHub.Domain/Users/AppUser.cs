using Microsoft.AspNetCore.Identity;

namespace OpsHub.Domain.Users;

/// <summary>
/// An OpsHub account. Inherits email, password hash, lockout and security stamp
/// from ASP.NET Core Identity; adds the fields the service desk needs.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 100;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Users are deactivated instead of deleted so that tickets and comments
    /// keep pointing at a real author.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
