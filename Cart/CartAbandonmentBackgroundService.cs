using Microsoft.Extensions.Options;

namespace MarketplaceWeb.Bff.Cart;

public sealed class CartAbandonmentBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CartAbandonmentOptions _options;
    private readonly ILogger<CartAbandonmentBackgroundService> _logger;

    public CartAbandonmentBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<CartAbandonmentOptions> options,
        ILogger<CartAbandonmentBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(_options.ScanIntervalSeconds, 30));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var scanner = scope.ServiceProvider.GetRequiredService<CartAbandonmentScanner>();
                await scanner.ScanAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Cart abandonment scan failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
