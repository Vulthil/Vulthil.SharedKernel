using System.Net.Http.Json;

namespace Vulthil.Extensions.Testing;

/// <summary>
/// Extension methods for <see cref="HttpResponseMessage"/> to simplify testing.
/// </summary>
public static class HttpResponseMessageExtensions
{
    // A large error page, such as the developer exception page, must not flood the test output.
    private const int MaxBodyLengthInMessage = 4096;

    /// <summary>
    /// Asserts the response indicates success, then reads its JSON content and deserializes it into
    /// <typeparamref name="TResponse"/>. Throws when the response is <see langword="null"/>, has a
    /// non-success status code, or its body is empty or deserializes to <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type to deserialize the JSON response body into.</typeparam>
    /// <param name="response">The HTTP response to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The deserialized response body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is <see langword="null"/>.</exception>
    /// <exception cref="HttpRequestException">
    /// The response has a non-success status code. The message holds the status code, the reason phrase and the
    /// response body (its first 4,096 characters), so a failing test shows why the call failed.
    /// </exception>
    /// <exception cref="InvalidOperationException">The body is empty or deserializes to <see langword="null"/>.</exception>
    public static async Task<TResponse> GetResponseAsync<TResponse>(this HttpResponseMessage? response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateFailureAsync(response, cancellationToken).ConfigureAwait(false);
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Response content is empty or could not be deserialized.");

        return result;
    }

    private static async Task<HttpRequestException> CreateFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var message = $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}).";
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (body.Length > MaxBodyLengthInMessage)
        {
            message += $" Response body: {body[..MaxBodyLengthInMessage]}... (truncated from {body.Length} characters)";
        }
        else if (body.Length > 0)
        {
            message += $" Response body: {body}";
        }

        return new HttpRequestException(message, inner: null, response.StatusCode);
    }
}
