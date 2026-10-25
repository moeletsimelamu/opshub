using Microsoft.AspNetCore.Mvc;
using OpsHub.Api.Common.Results;

namespace OpsHub.Api.Common;

/// <summary>
/// Base class for OpsHub controllers. [ApiController] turns on automatic 400 responses
/// for invalid models and binding-source inference; <see cref="Problem(Error)"/>
/// turns service errors into RFC 9457 ProblemDetails responses.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult Problem(Error error)
    {
        if (error.Type == ErrorType.Validation && error.Fields is not null)
        {
            foreach (var (field, messages) in error.Fields)
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(field, message);
                }
            }

            return ValidationProblem(ModelState);
        }

        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, detail: error.Message);
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    protected ActionResult<T> ToActionResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
}
