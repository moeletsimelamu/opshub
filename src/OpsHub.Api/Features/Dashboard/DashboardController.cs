using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsHub.Api.Auth;
using OpsHub.Api.Common;

namespace OpsHub.Api.Features.Dashboard;

/// <summary>Service-desk overview for support staff. Requesters get 403.</summary>
[Route("api/dashboard")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DashboardController(DashboardService dashboard) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await dashboard.GetAsync(cancellationToken));
}
