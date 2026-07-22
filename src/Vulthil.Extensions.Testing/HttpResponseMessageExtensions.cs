using System.Net.Http.Json;

namespace Vulthil.Extensions.Testing;

/// <summary>
/// Extension methods for <see cref="HttpResponseMessage"/> to simplify testing.
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Asserts the response indicates success, then reads its JSON content and deserializes it into
    /// <typeparamref name="TResponse"/>. Throws when the response is <see langword="null"/>, has a
    /// non-success status code, or its body is empty or deserializes to <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type to deserialize the JSON response body into.</typeparam>
    /// <param name="response">The HTTP response to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The deserialized response body.</returns>
    public static async Task<TResponse> GetResponseAsync<TResponse>(this HttpResponseMessage? response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Response content is empty or could not be deserialized.");

        return result;
    }
}
