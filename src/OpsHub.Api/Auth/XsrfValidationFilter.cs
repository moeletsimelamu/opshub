using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace OpsHub.Api.Auth;

/// <summary>
/// Rejects any state-changing request (POST, PUT, PATCH, DELETE) whose X-XSRF-TOKEN
/// header is missing or does not match the antiforgery cookie. Registered globally, so
/// no controller can forget it. Safe methods (GET, HEAD, OPTIONS) must not change data,
/// so they are not checked.
/// </summary>
internal sealed class XsrfValidationFilter(IAntiforgery antiforgery, ProblemDetailsFactory problemDetailsFactory)
    : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
        {
            var problem = problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                title: "Invalid request token",
                detail: "The X-XSRF-TOKEN header is missing or invalid. Reload the page and try again.");
            problem.Extensions["code"] = "xsrf_token_invalid";

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
        }
    }
}
