using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalog.Endpoints;
using Catalog.Search.Contracts;
using Catalog.Search.Searching;
using Catalog.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Catalog.Tests;

public sealed class CatalogTests(CatalogWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<CatalogWebApplicationFactory>
{
    private ITestHarness Harness => ScopedServices.GetRequiredService<ITestHarness>();

    [Fact]
    public async Task AddProduct_makes_the_product_searchable()
    {
        // Act
        using var response = await Client.PostAsJsonAsync("products", new AddProduct.Request("CUP-0001", "Espresso cup", 12.5m), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.GetResponseAsync<AddProduct.Response>(CancellationToken);

        var search = await Polling.WaitAsync(TimeSpan.FromSeconds(30), async cancellationToken =>
        {
            var results = await (await Client.GetAsync("search/products?term=espresso", cancellationToken))
                .GetResponseAsync<List<ProductSearchResult>>(cancellationToken);

            return results.Exists(result => result.ProductId == created.Id)
                ? Result.Success(results)
                : Result.Failure<List<ProductSearchResult>>(Error.NotFound("Search.NotIndexed", "The product is not indexed yet."));
        }, CancellationToken);

        search.IsSuccess.ShouldBeTrue();
        Harness.Published<ProductPublished>().ShouldContain(published =>
            published.Message.ProductId == created.Id && published.Envelope.MessageId == ProductPublished.MessageIdFor(created.Id));
    }

    [Fact]
    public async Task AddProduct_with_a_taken_sku_returns_a_conflict_problem()
    {
        // Arrange
        using var first = await Client.PostAsJsonAsync("products", new AddProduct.Request("MUG-0001", "Mug", 8m), CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        using var second = await Client.PostAsJsonAsync("products", new AddProduct.Request("MUG-0001", "Other mug", 9m), CancellationToken);

        // Assert
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = JsonNode.Parse(await second.Content.ReadAsStringAsync(CancellationToken))!;
        ((string?)problem["detail"]).ShouldBe("A product with SKU MUG-0001 already exists.");
    }

    [Fact]
    public async Task AddProduct_with_an_invalid_sku_returns_the_domain_error_code()
    {
        // Act
        using var response = await Client.PostAsJsonAsync("products", new AddProduct.Request("cup-1", "Cup", 5m), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!;
        problem["errorCodes"]!["Sku"]!.AsArray().Select(code => (string?)code).ShouldBe(["Product.InvalidSku"]);
    }

    [Fact]
    public async Task ProductPublished_delivered_twice_with_the_same_message_id_is_indexed_once()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var publisher = ScopedServices.GetRequiredService<IPublisher>();

        // Act
        await publisher.PublishAsync(new ProductPublished(productId, "TEA-0001", "Teapot", 30m), WithMessageId(productId), CancellationToken);
        await publisher.PublishAsync(new ProductPublished(productId, "TEA-0001", "Teapot renamed", 30m), WithMessageId(productId), CancellationToken);

        // Assert
        var results = await (await Client.GetAsync("search/products?term=teapot", CancellationToken))
            .GetResponseAsync<List<ProductSearchResult>>(CancellationToken);
        results.ShouldHaveSingleItem().Name.ShouldBe("Teapot");
    }

    [Fact]
    public async Task Search_with_a_one_letter_term_returns_a_validation_problem()
    {
        // Act
        using var response = await Client.GetAsync("search/products?term=a", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_unknown_product_returns_a_not_found_problem()
    {
        // Act
        using var response = await Client.GetAsync($"products/{Guid.NewGuid()}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static Func<IPublishContext, ValueTask> WithMessageId(Guid productId) => context =>
    {
        context.SetMessageId(ProductPublished.MessageIdFor(productId));
        return ValueTask.CompletedTask;
    };
}
