using Microsoft.AspNetCore.Builder;
using Vulthil.Results;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Endpoint-convention extensions that declare which <see cref="ErrorType"/>s an endpoint can fail with.
/// </summary>
public static class ProducesErrorsExtensions
{
    /// <summary>
    /// Declares the error classifications the endpoint (or every endpoint in a group) can fail with, so OpenAPI
    /// documents exactly those problem responses and no others. The status code and body schema per error type come
    /// from the same mapping the result extensions use at runtime; a <c>500</c> response is documented on every
    /// operation without a declaration. Repeated error types are declared once.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint or group builder.</param>
    /// <param name="errorTypes">The error classifications the endpoint can produce.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TBuilder ProducesErrors<TBuilder>(this TBuilder builder, params ErrorType[] errorTypes)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(errorTypes);

        foreach (var errorType in errorTypes.Distinct())
        {
            builder.WithMetadata(new ProducesErrorAttribute(errorType));
        }

        return builder;
    }
}
