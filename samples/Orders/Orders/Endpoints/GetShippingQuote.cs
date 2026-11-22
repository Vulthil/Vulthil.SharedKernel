using Orders.ExternalServices;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;

namespace Orders.Endpoints;

public static class GetShippingQuote
{
    /// <summary>
    /// Calls the external rates API through the typed client and turns a transport failure into a failed result, so
    /// the caller gets a problem response instead of an unhandled exception.
    /// </summary>
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("shipping-quotes/{country}", async (string country, IShippingRatesClient ratesClient, CancellationToken cancellationToken) =>
                {
                    var result = await GetQuoteAsync(ratesClient, country, cancellationToken);
                    return result.ToIResult();
                })
                .WithName("GetShippingQuote")
                .ProducesErrors(ErrorType.NotFound, ErrorType.Failure);

        private static async Task<Result<ShippingQuote>> GetQuoteAsync(IShippingRatesClient ratesClient, string country, CancellationToken cancellationToken)
        {
            try
            {
                var quote = await ratesClient.GetQuoteAsync(country, cancellationToken);
                return quote.ToResult(Error.NotFound("ShippingQuote.NotFound", $"No quote for {country}."));
            }
            catch (HttpRequestException)
            {
                return Result.Failure<ShippingQuote>(ExternalServiceErrors.Unavailable("shipping-rates"));
            }
        }
    }
}
