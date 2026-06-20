using System.Net.Http.Json;

namespace Vulthil.Extensions.Testing;

/// <summary>
/// Extension methods for <see cref="HttpResponseMessage"/> to simplify testing.
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Reads the JSON content of the HTTP response and deserializes it into the specified type.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="response"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<TResponse> GetResponseAsync<TResponse>(this HttpResponseMessage? response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken) ?? throw new InvalidOperationException("Response content is empty or could not be deserialized.");

        return result;
    }
}
