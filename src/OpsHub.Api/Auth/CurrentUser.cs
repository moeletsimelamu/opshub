using System.Security.Claims;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Auth;

/// <summary>
/// The signed-in user, as proven by the session cookie. Services use this instead
/// of trusting any user ID or role sent by the client.
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }

    bool IsInRole(string role);

    bool IsAdministrator => IsInRole(AppRoles.Administrator);

    /// <summary>Support Agent or Administrator.</summary>
    bool IsStaff => IsAdministrator || IsInRole(AppRoles.SupportAgent);
}

internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("ICurrentUser is only available during an HTTP request.");

    public Guid Id
    {
        get
        {
            var value = Principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("No user is signed in.");
            return Guid.Parse(value);
        }
    }

    public bool IsInRole(string role) => Principal.IsInRole(role);
}
