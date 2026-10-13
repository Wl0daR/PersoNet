using PersoNet.Application.Common;

namespace PersoNet.Api.Common;

/// <summary>
/// Turns use-case results into HTTP responses. Failures become RFC 9457 problem details with a stable <c>code</c>.
/// </summary>
internal static class ResultExtensions
{
    public static IResult ToProblem(this AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code });
    }

    public static IResult ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();

    public static IResult ToNoContent(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
}
