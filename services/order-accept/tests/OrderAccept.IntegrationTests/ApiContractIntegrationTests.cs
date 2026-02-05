using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OrderAccept.Domain.Entities;
using OrderAccept.IntegrationTests.Fixtures;
using OrderAccept.Persistence.Impl;

namespace OrderAccept.IntegrationTests;

public sealed class ApiContractIntegrationTests : IClassFixture<OrderAcceptApiFixture>
{
    private readonly OrderAcceptApiFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ApiContractIntegrationTests(OrderAcceptApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetProducts_WithoutToken_Returns401ProblemDetails()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(401);
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetProducts_WithInvalidPaging_Returns400ProblemDetails()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-1"));

        var response = await client.GetAsync("/api/products?offset=-1&size=10");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("title").GetString().Should().Be("Bad Request");
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetProducts_WhenValid_Returns200PagedResult()
    {
        var externalProductId = $"it-prod-{Guid.NewGuid():N}";
        await SeedActiveProductAsync(externalProductId, name: "IT Product", imageUrl: "https://img.example/it.png", price: 25.50m);

        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-1"));

        var response = await client.GetAsync("/api/products?offset=0&size=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResultResponse<ProductDtoResponse>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle(p => p.ExternalProductId == externalProductId);
    }

    [Fact]
    public async Task GetProductById_WhenMissing_Returns404ProblemDetails()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-1"));

        var response = await client.GetAsync("/api/products/missing-product-it");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetProductById_WhenValid_Returns200ProductDto()
    {
        var externalProductId = $"it-prod-{Guid.NewGuid():N}";
        await SeedActiveProductAsync(externalProductId, name: "IT Product", imageUrl: "https://img.example/it.png", price: 25.50m);

        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-1"));

        var response = await client.GetAsync($"/api/products/{externalProductId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<ProductDtoResponse>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.ExternalProductId.Should().Be(externalProductId);
        dto.Name.Should().Be("IT Product");
        dto.ImageUrl.Should().Be("https://img.example/it.png");
        dto.Price.Should().Be(25.50m);
    }

    [Fact]
    public async Task PostOrders_WithValidationErrors_Returns400ValidationProblem_WithCamelCaseErrorKeys()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-2"));

        var request = new CreateOrderRequestDto(
            CustomerId: "customer-it-2",
            Items: new[] { new CreateOrderItemDto(ProductId: "", Quantity: 0) });

        var response = await client.PostAsJsonAsync("/api/orders", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("title").GetString().Should().Be("One or more validation errors occurred.");
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();

        var errors = doc.RootElement.GetProperty("errors");
        var keys = errors.EnumerateObject().Select(p => p.Name).ToArray();

        keys.Should().Contain("items[0].productId");
        keys.Should().Contain("items[0].quantity");
    }

    [Fact]
    public async Task PostOrders_WhenProductMissing_Returns404ProblemDetails()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-3"));

        var request = new CreateOrderRequestDto(
            CustomerId: "customer-it-3",
            Items: new[] { new CreateOrderItemDto(ProductId: "missing-product-it", Quantity: 1) });

        var response = await client.PostAsJsonAsync("/api/orders", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PostOrders_WhenValid_Returns201Created_WithOrderDto_And_LocationHeader()
    {
        var externalProductId = $"it-prod-{Guid.NewGuid():N}";
        await SeedActiveProductAsync(externalProductId, name: "IT Product", imageUrl: "https://img.example/it.png", price: 25.50m);

        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-4"));

        var request = new CreateOrderRequestDto(
            CustomerId: "customer-it-4",
            Items: new[] { new CreateOrderItemDto(ProductId: externalProductId, Quantity: 2) });

        var response = await client.PostAsJsonAsync("/api/orders", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var dto = await response.Content.ReadFromJsonAsync<OrderDtoResponse>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.Id.Should().BeGreaterThan(0);
        dto.CorrelationId.Should().NotBeNullOrWhiteSpace();
        dto.Items.Should().ContainSingle();

        var item = dto.Items.Single();
        item.ProductId.Should().Be(externalProductId);
        item.ProductName.Should().Be("IT Product");
        item.ImageUrl.Should().Be("https://img.example/it.png");
        item.UnitPrice.Should().Be(25.50m);
        item.Quantity.Should().Be(2);

        // Contract requires Location: /api/orders/{id}
        var location = response.Headers.Location!.OriginalString;
        location.Should().EndWith($"/api/orders/{dto.Id}");
    }

    [Fact]
    public async Task DeleteOrders_WhenOrderDoesNotBelongToUser_Returns403ProblemDetails()
    {
        var externalProductId = $"it-prod-{Guid.NewGuid():N}";
        await SeedActiveProductAsync(externalProductId, name: "IT Product 2", imageUrl: "https://img.example/it2.png", price: 9.99m);

        var owner = $"it-owner-{Guid.NewGuid():N}";
        var other = $"it-other-{Guid.NewGuid():N}";

        // Create an order as the owner (creates owner customer row too)
        var clientOwner = _fixture.Factory.CreateClient();
        clientOwner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: owner));

        var createRequest = new CreateOrderRequestDto(
            CustomerId: owner,
            Items: new[] { new CreateOrderItemDto(ProductId: externalProductId, Quantity: 1) });

        var createResponse = await clientOwner.PostAsJsonAsync("/api/orders", createRequest, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<OrderDtoResponse>(JsonOptions);
        created.Should().NotBeNull();

        // Ensure the other customer exists; otherwise handler would return 404.
        await SeedCustomerAsync(other);

        var clientOther = _fixture.Factory.CreateClient();
        clientOther.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: other));

        var deleteResponse = await clientOther.DeleteAsync($"/api/orders/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deleteResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await deleteResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(403);
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetOrders_WithoutToken_Returns401ProblemDetails()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(401);
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetOrders_WithInvalidPaging_Returns400ProblemDetails()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-10"));

        var response = await client.GetAsync("/api/orders?offset=-1&size=10");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("title").GetString().Should().Be("Bad Request");
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetOrders_WhenValid_Returns200PagedResult()
    {
        var externalProductId = $"it-prod-{Guid.NewGuid():N}";
        await SeedActiveProductAsync(externalProductId, name: "IT Product", imageUrl: "https://img.example/it.png", price: 25.50m);

        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-11"));

        var request = new CreateOrderRequestDto(
            CustomerId: "customer-it-11",
            Items: new[] { new CreateOrderItemDto(ProductId: externalProductId, Quantity: 2) });

        var createResponse = await client.PostAsJsonAsync("/api/orders", request, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<OrderDtoResponse>(JsonOptions);
        created.Should().NotBeNull();

        var response = await client.GetAsync("/api/orders?offset=0&size=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResultResponse<OrderDtoResponse>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle(o => o.Id == created!.Id);
    }

    [Fact]
    public async Task GetOrderById_WhenInvalidId_Returns400ProblemDetails()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-12"));

        var response = await client.GetAsync("/api/orders/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("title").GetString().Should().Be("Bad Request");
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetOrderById_WhenMissing_Returns404ProblemDetails()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-13"));

        var response = await client.GetAsync("/api/orders/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetOrderById_WhenValid_Returns200OrderDto()
    {
        var externalProductId = $"it-prod-{Guid.NewGuid():N}";
        await SeedActiveProductAsync(externalProductId, name: "IT Product", imageUrl: "https://img.example/it.png", price: 25.50m);

        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-14"));

        var request = new CreateOrderRequestDto(
            CustomerId: "customer-it-14",
            Items: new[] { new CreateOrderItemDto(ProductId: externalProductId, Quantity: 1) });

        var createResponse = await client.PostAsJsonAsync("/api/orders", request, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<OrderDtoResponse>(JsonOptions);
        created.Should().NotBeNull();

        var response = await client.GetAsync($"/api/orders/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<OrderDtoResponse>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task DeleteOrders_WhenValid_Returns204NoContent()
    {
        var externalProductId = $"it-prod-{Guid.NewGuid():N}";
        await SeedActiveProductAsync(externalProductId, name: "IT Product", imageUrl: "https://img.example/it.png", price: 25.50m);

        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-15"));

        var request = new CreateOrderRequestDto(
            CustomerId: "customer-it-15",
            Items: new[] { new CreateOrderItemDto(ProductId: externalProductId, Quantity: 1) });

        var createResponse = await client.PostAsJsonAsync("/api/orders", request, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<OrderDtoResponse>(JsonOptions);
        created.Should().NotBeNull();

        var deleteResponse = await client.DeleteAsync($"/api/orders/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetOrders_WhenJwtHasNoSubjectClaim_Returns403ProblemDetails()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(_fixture.JwtSigningKey, sub: null));

        var response = await client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(403);
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    // ----- Helpers -----

    private async Task SeedActiveProductAsync(string externalProductId, string name, string imageUrl, decimal price)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContosoDbContext>();

        // Avoid unique index conflicts if the DB is reused between test runs.
        var existing = await db.Products.FirstOrDefaultAsync(p => p.ExternalProductId == externalProductId);
        if (existing is not null) return;

        db.Products.Add(new Product
        {
            ExternalProductId = externalProductId,
            Name = name,
            Category = "IntegrationTests",
            Vendor = "Contoso",
            ImageUrl = imageUrl,
            Discount = 0,
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = price,
            IsActive = true
        });

        await db.SaveChangesAsync();
    }

    private async Task SeedCustomerAsync(string externalCustomerId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContosoDbContext>();

        var existing = await db.Customers.FirstOrDefaultAsync(c => c.ExternalCustomerId == externalCustomerId);
        if (existing is not null) return;

        db.Customers.Add(new Customer { ExternalCustomerId = externalCustomerId });
        await db.SaveChangesAsync();
    }

    private sealed record CreateOrderRequestDto(string CustomerId, IReadOnlyCollection<CreateOrderItemDto> Items);
    private sealed record CreateOrderItemDto(string ProductId, int Quantity);

    private sealed record OrderDtoResponse(long Id, string CorrelationId, DateTime CreatedAt, IReadOnlyCollection<OrderItemDtoResponse> Items);
    private sealed record OrderItemDtoResponse(string ProductId, string ProductName, string ImageUrl, decimal UnitPrice, int Quantity);

    private sealed record ProductDtoResponse(
        string ExternalProductId,
        string Name,
        string Category,
        string Vendor,
        string ImageUrl,
        int Discount,
        string BillingPeriod,
        bool IsSubscription,
        decimal Price);

    private sealed record PagedResultResponse<T>(int Offset, int Size, int Total, IReadOnlyCollection<T> Items);

    private static string CreateJwt(string signingKey, string? sub)
    {
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>();
        if (!string.IsNullOrWhiteSpace(sub))
            claims.Add(new Claim("sub", sub));

        // Keep token valid even without sub (to test 403 from API-level identity extraction).
        claims.Add(new Claim("scope", "it"));

        var token = new JwtSecurityToken(
            issuer: "order-accept-it",
            audience: "order-accept-it",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
