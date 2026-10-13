using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PersoNet.ApiClient;

internal static class ApiResponses
{
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false)
            ?? throw new ApiException(response.StatusCode, "Serwer zwrócił pustą odpowiedź.", code: null);
    }

    /// <summary>
    /// Turns problem details from the API into an <see cref="ApiException"/> with the API's message.
    /// </summary>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? detail = null;
        string? code = null;

        try
        {
            using var problem = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (problem.RootElement.ValueKind == JsonValueKind.Object)
            {
                detail = problem.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
                code = problem.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
            }
        }
        catch (JsonException)
        {
            // Not problem details; fall back to a message based on the status code.
        }

        throw new ApiException(response.StatusCode, detail ?? DefaultMessage(response.StatusCode), code);
    }

    private static string DefaultMessage(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "Sesja wygasła. Zaloguj się ponownie.",
        HttpStatusCode.Forbidden => "Nie masz uprawnień do tej operacji.",
        HttpStatusCode.NotFound => "Nie znaleziono danych.",
        _ => $"Serwer zwrócił błąd ({(int)status}).",
    };
}
