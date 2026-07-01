using System.Net.Security;
using System.Threading.RateLimiting;
using MarketplaceWeb.Bff.Api;
using MarketplaceWeb.Bff.Application;
using MarketplaceWeb.Bff.Clients;
using MarketplaceWeb.Bff.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(
        "PublicProduct",
        policy => policy
            .Expire(TimeSpan.FromSeconds(50))
            .SetVaryByRouteValue("skuId"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("PerUser", context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

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
builder.Services.AddTransient<DownstreamSslDiagnosticHandler>();
AddDownstreamClient<IProductCatalogClient, ProductCatalogClient>(builder.Services, builder.Configuration, builder.Environment, "ProductCatalog", TimeSpan.FromSeconds(50));
AddDownstreamClient<IProductSearchClient, ProductSearchClient>(builder.Services, builder.Configuration, builder.Environment, "ProductSearch", TimeSpan.FromSeconds(50));
AddDownstreamClient<IShippingPromiseClient, ShippingPromiseClient>(builder.Services, builder.Configuration, builder.Environment, "ShippingPromise", TimeSpan.FromSeconds(50));
AddDownstreamClient<ICheckoutClient, CheckoutClient>(builder.Services, builder.Configuration, builder.Environment, "Checkout", TimeSpan.FromSeconds(50));
AddDownstreamClient<IOrderClient, OrderClient>(builder.Services, builder.Configuration, builder.Environment, "Order", TimeSpan.FromSeconds(50));
AddDownstreamClient<IShipmentClient, ShipmentClient>(builder.Services, builder.Configuration, builder.Environment, "Shipment", TimeSpan.FromSeconds(50));
AddDownstreamClient<ITrackingClient, TrackingClient>(builder.Services, builder.Configuration, builder.Environment, "Tracking", TimeSpan.FromSeconds(50));

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
app.UseRateLimiter();
app.UseOutputCache();

app.MapProductEndpoints();
app.MapShippingEndpoints();
app.MapCheckoutEndpoints();
app.MapOrderEndpoints();
app.MapShipmentEndpoints();

app.Run();

static void AddDownstreamClient<TInterface, TImplementation>(
    IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    string serviceName,
    TimeSpan timeout)
    where TInterface : class
    where TImplementation : class, TInterface
{
    var resilienceAttemptTimeout = timeout < TimeSpan.FromSeconds(1)
        ? TimeSpan.FromSeconds(30)
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
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();

            if (environment.IsDevelopment())
            {
                handler.ServerCertificateCustomValidationCallback = static (request, _, _, sslPolicyErrors) =>
                    sslPolicyErrors == SslPolicyErrors.None
                    || request.RequestUri?.IsLoopback == true;
            }

            return handler;
        })
        .AddHttpMessageHandler<DownstreamSslDiagnosticHandler>()
        .AddHttpMessageHandler<CorrelationIdHandler>()
        //.AddStandardResilienceHandler(options =>
        //{
        //    options.TotalRequestTimeout.Timeout =
        //        (resilienceAttemptTimeout * (maxRetryAttempts + 10))
        //        + (retryDelay * maxRetryAttempts)
        //        + TimeSpan.FromSeconds(30);
        //    options.AttemptTimeout.Timeout = resilienceAttemptTimeout;
        //    options.Retry.MaxRetryAttempts = maxRetryAttempts;
        //    options.Retry.Delay = retryDelay;
        //    //options.Retry.DisableForUnsafeHttpMethods();
        //    options.CircuitBreaker.FailureRatio = 0.5;
        //    options.CircuitBreaker.MinimumThroughput = 20;
        //    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        //    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        //})
        ;
}
