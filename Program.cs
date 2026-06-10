using System.Threading.RateLimiting;
using MarketplaceWeb.Bff.Api;
using MarketplaceWeb.Bff.Application;
using MarketplaceWeb.Bff.Clients;
using MarketplaceWeb.Bff.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-marketplace-bff";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.ClientId = builder.Configuration["Authentication:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:ClientSecret"];
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("marketplace-api");
    });

builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-marketplace-csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(
        "PublicProduct",
        policy => policy
            .Expire(TimeSpan.FromSeconds(30))
            .SetVaryByRouteValue("skuId"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("PerUser", context =>
    {
        var key =
            context.User.FindFirst("sub")?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(
            key,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                TokensPerPeriod = 100,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 10,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddTransient<AccessTokenHandler>();

AddDownstreamClient<IProductCatalogClient, ProductCatalogClient>(builder.Services, builder.Configuration, "ProductCatalog", TimeSpan.FromSeconds(1));
AddDownstreamClient<IProductSearchClient, ProductSearchClient>(builder.Services, builder.Configuration, "ProductSearch", TimeSpan.FromSeconds(3));
AddDownstreamClient<IShippingPromiseClient, ShippingPromiseClient>(builder.Services, builder.Configuration, "ShippingPromise", TimeSpan.FromSeconds(1));
AddDownstreamClient<ICheckoutClient, CheckoutClient>(builder.Services, builder.Configuration, "Checkout", TimeSpan.FromSeconds(2));
AddDownstreamClient<IOrderClient, OrderClient>(builder.Services, builder.Configuration, "Order", TimeSpan.FromSeconds(1));
AddDownstreamClient<IShipmentClient, ShipmentClient>(builder.Services, builder.Configuration, "Shipment", TimeSpan.FromSeconds(1));
AddDownstreamClient<ITrackingClient, TrackingClient>(builder.Services, builder.Configuration, "Tracking", TimeSpan.FromSeconds(1));

builder.Services.AddScoped<ProductPageComposer>();
builder.Services.AddScoped<OrderPageComposer>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();
app.UseOutputCache();

app.MapGet("/bff/csrf", (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new { token = tokens.RequestToken });
    })
    .RequireAuthorization();

app.MapProductEndpoints();
app.MapShippingEndpoints();
app.MapCheckoutEndpoints();
app.MapOrderEndpoints();
app.MapShipmentEndpoints();

app.Run();

static void AddDownstreamClient<TInterface, TImplementation>(
    IServiceCollection services,
    IConfiguration configuration,
    string serviceName,
    TimeSpan timeout)
    where TInterface : class
    where TImplementation : class, TInterface
{
    var resilienceAttemptTimeout = timeout < TimeSpan.FromSeconds(1)
        ? TimeSpan.FromSeconds(1)
        : timeout;
    const int maxRetryAttempts = 2;
    var retryDelay = TimeSpan.FromMilliseconds(100);

    services
        .AddHttpClient<TInterface, TImplementation>(client =>
        {
            var url = configuration[$"Services:{serviceName}"]
                ?? throw new InvalidOperationException($"{serviceName} URL is missing");

            client.BaseAddress = new Uri(url);
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddHttpMessageHandler<CorrelationIdHandler>()
        .AddHttpMessageHandler<AccessTokenHandler>()
        .AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout =
                (resilienceAttemptTimeout * (maxRetryAttempts + 1))
                + (retryDelay * maxRetryAttempts)
                + TimeSpan.FromMilliseconds(300);
            options.AttemptTimeout.Timeout = resilienceAttemptTimeout;
            options.Retry.MaxRetryAttempts = maxRetryAttempts;
            options.Retry.Delay = retryDelay;
            //options.Retry.DisableForUnsafeHttpMethods();
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 20;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        });
}
