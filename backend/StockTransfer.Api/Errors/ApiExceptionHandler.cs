using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace StockTransfer.Api.Errors;

public class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        // Anything else falls through to the default handler: a 500 ProblemDetails with no internals.
        if (exception is not ApiException error)
        {
            return false;
        }

        context.Response.StatusCode = error.StatusCode;
        var problem = new ProblemDetails { Status = error.StatusCode, Title = error.Title, Detail = error.Message };
        problem.Extensions["messages"] = error.Messages;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
