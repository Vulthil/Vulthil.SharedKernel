using ServiceDefaults;
using Storefront.Api;
using Storefront.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Service discovery resolves the Aspire resource names to the services' addresses.
builder.Services.AddHttpClient<OrdersApi>(client => client.BaseAddress = new Uri($"https+http://{ServiceNames.OrdersServiceName}"));
builder.Services.AddHttpClient<ShipmentsApi>(client => client.BaseAddress = new Uri($"https+http://{ServiceNames.ShipmentsServiceName}"));
builder.Services.AddHttpClient<CatalogApi>(client => client.BaseAddress = new Uri($"https+http://{ServiceNames.CatalogServiceName}"));

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
