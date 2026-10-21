namespace StockTransfer.Api.Errors;

/// <summary>
/// A request the API refuses. <see cref="ApiExceptionHandler"/> turns it into a ProblemDetails response whose
/// <c>detail</c> is a message the UI can show as-is, with the individual problems listed under <c>messages</c>.
/// </summary>
public class ApiException(int statusCode, string title, string detail, IReadOnlyList<string>? messages = null)
    : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public IReadOnlyList<string> Messages { get; } = messages ?? [detail];

    public static ApiException NotFound(string detail) =>
        new(StatusCodes.Status404NotFound, "Not found", detail);

    public static ApiException Invalid(IReadOnlyList<string> messages) =>
        new(StatusCodes.Status400BadRequest, "The request is invalid", string.Join(" ", messages), messages);

    public static ApiException Conflict(string title, string detail, IReadOnlyList<string>? messages = null) =>
        new(StatusCodes.Status409Conflict, title, detail, messages);
}
