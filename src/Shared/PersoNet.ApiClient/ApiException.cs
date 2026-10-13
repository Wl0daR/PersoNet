using System.Net;

namespace PersoNet.ApiClient;

/// <summary>
/// A failed API call. <see cref="Exception.Message"/> is the API's user-facing description (in Polish); <see cref="Code"/> is
/// its stable error code, for example "department.code_taken".
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException()
    {
    }

    public ApiException(string message)
        : base(message)
    {
    }

    public ApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ApiException(HttpStatusCode statusCode, string message, string? code)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public HttpStatusCode StatusCode { get; }

    public string? Code { get; }
}
