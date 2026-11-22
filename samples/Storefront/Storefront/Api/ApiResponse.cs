using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Results;

namespace Storefront.Api;

/// <summary>
/// Turns an HTTP answer of one of the sample services into a <see cref="Result"/>. The services answer every failure
/// with a problem response (RFC 7807), so the status picks the <see cref="ErrorType"/> and the problem's detail
/// becomes the error's description. The pages then handle a failure like any other result, without exceptions.
/// </summary>
public static class ApiResponse
{
    public static async Task<Result<T>> ReadAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await send();
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<T>(await ToErrorAsync(response, cancellationToken));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return value.ToResult(Error.Problem("Api.EmptyResponse", "The service answered without a body."));
        }
        catch (HttpRequestException exception)
        {
            return Result.Failure<T>(Unavailable(exception));
        }
    }

    public static async Task<Result> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            return response.IsSuccessStatusCode
                ? Result.Success()
                : Result.Failure(await ToErrorAsync(response, cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            return Result.Failure(Unavailable(exception));
        }
    }

    private static async Task<Error> ToErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await ReadProblemAsync(response, cancellationToken);
        var code = problem?.Title ?? response.StatusCode.ToString();
        var description = problem?.Detail ?? problem?.Title ?? $"The service answered {(int)response.StatusCode}.";

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => Error.Validation(code, description),
            HttpStatusCode.NotFound => Error.NotFound(code, description),
            HttpStatusCode.Conflict => Error.Conflict(code, description),
            _ => Error.Failure(code, description),
        };
    }

    private static async Task<ProblemDetails?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static Error Unavailable(HttpRequestException exception) =>
        Error.Failure("Api.Unavailable", $"The service could not be reached: {exception.Message}");
}
