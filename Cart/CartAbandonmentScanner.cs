using Microsoft.Extensions.Options;

namespace MarketplaceWeb.Bff.Cart;

/// <summary>
/// The actual abandonment-detection logic, factored out of <see cref="CartAbandonmentBackgroundService"/>
/// so it can be unit tested without spinning up a DI scope/hosted service.
/// </summary>
public sealed class CartAbandonmentScanner
{
    private readonly ICartRepository _repository;
    private readonly ICartEventPublisher _publisher;
    private readonly CartAbandonmentOptions _options;
    private readonly ILogger<CartAbandonmentScanner> _logger;

    public CartAbandonmentScanner(
        ICartRepository repository,
        ICartEventPublisher publisher,
        IOptions<CartAbandonmentOptions> options,
        ILogger<CartAbandonmentScanner> logger)
    {
        _repository = repository;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var threshold = DateTimeOffset.UtcNow.AddMinutes(-Math.Max(_options.AbandonmentThresholdMinutes, 1));
        var ownerIds = await _repository.ListOwnerIdsAsync(cancellationToken);

        foreach (var ownerId in ownerIds)
        {
            var cart = await _repository.GetAsync(ownerId, cancellationToken);

            if (cart is null || cart.Lines.Count == 0 || cart.AbandonedFlagSet)
            {
                continue;
            }

            if (cart.LastModifiedAt > threshold)
            {
                continue;
            }

            await _publisher.PublishAbandonedAsync(ownerId, cart, cancellationToken);

            cart.AbandonedFlagSet = true;
            await _repository.SaveAsync(ownerId, cart, cancellationToken);

            _logger.LogInformation("Flagged cart {CartOwnerId} as abandoned (inactive since {LastModifiedAt})", ownerId, cart.LastModifiedAt);
        }
    }
}
