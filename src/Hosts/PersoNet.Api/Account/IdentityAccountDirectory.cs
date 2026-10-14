using System.Net;
using System.Text.Json;
using Microsoft.Net.Http.Headers;
using PersoNet.Application.Accounts;
using PersoNet.Application.Common;
using PersoNet.Contracts.Account;

namespace PersoNet.Api.Account;

/// <summary>
/// <see cref="IAccountDirectory"/> over the login server's account administration endpoints. Calls carry the HR
/// user's own access token, so the login server checks the role and keeps to the caller's company itself.
/// </summary>
internal sealed class IdentityAccountDirectory(HttpClient http, IHttpContextAccessor context) : IAccountDirectory
{
    private const string Accounts = "api/accounts";

    public async Task<IdentityAccountResponse?> FindAsync(string email, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, $"{Accounts}?email={Uri.EscapeDataString(email)}");
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IdentityAccountResponse>(cancellationToken);
    }

    public async Task<Result<IdentityAccountLinkResponse>> CreateLinkAsync(IdentityAccountLinkRequest request, CancellationToken cancellationToken)
    {
        using var message = Request(HttpMethod.Post, $"{Accounts}/link");
        message.Content = JsonContent.Create(request);
        using var response = await http.SendAsync(message, cancellationToken);
        return await ReadAsync<IdentityAccountLinkResponse>(response, cancellationToken);
    }

    public async Task<Result<IdentityAccountResponse>> SetBlockedAsync(Guid accountId, bool blocked, CancellationToken cancellationToken)
    {
        using var message = Request(HttpMethod.Post, $"{Accounts}/{accountId}/{(blocked ? "block" : "unblock")}");
        using var response = await http.SendAsync(message, cancellationToken);
        return await ReadAsync<IdentityAccountResponse>(response, cancellationToken);
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (context.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString() is { Length: > 0 } authorization)
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, authorization);
        }

        return request;
    }

    // The login server answers expected failures with problem details carrying a "code", like this API does.
    private static async Task<Result<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return AppError.NotFound("account.not_found", "Pracownik nie ma jeszcze konta w portalu.");
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var code = problem.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
            var detail = problem.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
            if (code is not null && detail is not null)
            {
                return response.StatusCode == HttpStatusCode.Conflict ? AppError.Conflict(code, detail) : AppError.Validation(code, detail);
            }
        }

        // Anything else (the login server down, a rejected token) is an outage, not a user error.
        response.EnsureSuccessStatusCode();
        throw new HttpRequestException($"Unexpected answer from the login server: {(int)response.StatusCode}.");
    }
}
