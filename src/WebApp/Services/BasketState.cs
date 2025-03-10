using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using eShop.WebAppComponents.Catalog;
using eShop.WebAppComponents.Services;
using System.Diagnostics;
using System.Diagnostics.Metrics;
namespace eShop.WebApp.Services;

public class BasketState(
    BasketService basketService,
    CatalogService catalogService,
    OrderingService orderingService,
    AuthenticationStateProvider authenticationStateProvider) : IBasketState
{
    private static readonly Meter _meter = new("eShop.WebApp.BasketState");

    private static readonly Counter<double> _checkoutValueCounter = _meter.CreateCounter<double>(
        "basket.checkout.value",
        description: "Total value of items checked out",
        unit: "USD"
    );

    private static readonly Counter<int> _checkoutItemsCounter = _meter.CreateCounter<int>(
        "basket.checkout.items",
        description: "Total number of items checked out",
        unit: "items"
    );

    private static readonly Histogram<double> _checkoutValueCounterRecord = _meter.CreateHistogram(
        "basket.checkout.valueRecord",
        description: "Total value of items in basket at checked out",
        unit: "USD",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [ 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 125, 150, 200, 250, 300, 400, 500, 750, 1000, 1500, 2000, 2500, 3000, 4000, 5000, 7500, 10000, 15000, 20000, 25000, 30000, 40000, 50000, 75000, 100000 ]
        }
    );
    private static readonly Histogram<int> _checkoutItemsHistogram = _meter.CreateHistogram(
        "basket.checkout.itemsRecord",
        description: "Total number of items in basket at checked out",
        unit: "items",
        advice: new InstrumentAdvice<int>
        {
            HistogramBucketBoundaries = [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 25, 30, 40, 50, 75, 100, 150, 200, 250, 300, 400, 500, 750, 1000, 1500, 2000, 2500, 3000, 4000, 5000, 7500, 10000 ]
        }
    );

    private Task<IReadOnlyCollection<BasketItem>>? _cachedBasket;
    private HashSet<BasketStateChangedSubscription> _changeSubscriptions = new();

    public Task DeleteBasketAsync()
        => basketService.DeleteBasketAsync();

    public async Task<IReadOnlyCollection<BasketItem>> GetBasketItemsAsync()
        => (await GetUserAsync()).Identity?.IsAuthenticated == true
        ? await FetchBasketItemsAsync()
        : [];

    public IDisposable NotifyOnChange(EventCallback callback)
    {
        var subscription = new BasketStateChangedSubscription(this, callback);
        _changeSubscriptions.Add(subscription);
        return subscription;
    }

    public async Task AddAsync(CatalogItem item)
    {
        var items = (await FetchBasketItemsAsync()).Select(i => new BasketQuantity(i.ProductId, i.Quantity)).ToList();
        bool found = false;
        for (var i = 0; i < items.Count; i++)
        {
            var existing = items[i];
            if (existing.ProductId == item.Id)
            {
                items[i] = existing with { Quantity = existing.Quantity + 1 };
                found = true;
                break;
            }
        }

        if (!found)
        {
            items.Add(new BasketQuantity(item.Id, 1));
        }

        _cachedBasket = null;
        await basketService.UpdateBasketAsync(items);
        await NotifyChangeSubscribersAsync();
    }

    public async Task SetQuantityAsync(int productId, int quantity)
    {
        var existingItems = (await FetchBasketItemsAsync()).ToList();
        if (existingItems.FirstOrDefault(row => row.ProductId == productId) is { } row)
        {
            if (quantity > 0)
            {
                row.Quantity = quantity;
            }
            else
            {
                existingItems.Remove(row);
            }

            _cachedBasket = null;
            await basketService.UpdateBasketAsync(existingItems.Select(i => new BasketQuantity(i.ProductId, i.Quantity)).ToList());
            await NotifyChangeSubscribersAsync();
        }
    }

    public async Task CheckoutAsync(BasketCheckoutInfo checkoutInfo)
    {
        var activity = Activity.Current;

        try
        {
            if (checkoutInfo.RequestId == default)
            {
                checkoutInfo.RequestId = Guid.NewGuid();
            }
            activity?.SetTag("basket.requestId", checkoutInfo.RequestId);

            var buyerId = await authenticationStateProvider.GetBuyerIdAsync() ?? throw new InvalidOperationException("User does not have a buyer ID");
            var userName = await authenticationStateProvider.GetUserNameAsync() ?? throw new InvalidOperationException("User does not have a user name");

            activity?.SetTag("basket.userName", userName);
            activity?.SetTag("basket.buyerId", buyerId);
            

            // Get details for the items in the basket
            var orderItems = await FetchBasketItemsAsync();

            int totalItems = orderItems.Sum(i => i.Quantity);
            double totalValue = orderItems.Sum(i => (double)(i.UnitPrice * i.Quantity));
            
            activity?.SetTag("basket.items.count", orderItems.Count);
            activity?.SetTag("basket.total", totalValue);

            activity?.SetTag("basket.itemsRecord.count", orderItems.Count);
            activity?.SetTag("basket.totalRecord", orderItems.Sum(i => i.UnitPrice * i.Quantity));

            // Call into Ordering.API to create the order using those details
            var request = new CreateOrderRequest(
                UserId: buyerId,
                UserName: userName,
                City: checkoutInfo.City!,
                Street: checkoutInfo.Street!,
                State: checkoutInfo.State!,
                Country: checkoutInfo.Country!,
                ZipCode: checkoutInfo.ZipCode!,
                CardNumber: "1111222233334444",
                CardHolderName: "TESTUSER",
                CardExpiration: DateTime.UtcNow.AddYears(1),
                CardSecurityNumber: "111",
                CardTypeId: checkoutInfo.CardTypeId,
                Buyer: buyerId,
                Items: [.. orderItems]);

            // Update the metrics using the counters
            _checkoutValueCounter.Add(totalValue);
            _checkoutItemsCounter.Add(totalItems);
            
            _checkoutValueCounterRecord.Record((double)orderItems.Sum(i => i.UnitPrice * i.Quantity));
            _checkoutItemsHistogram.Record(orderItems.Sum(i => i.Quantity));

            await orderingService.CreateOrder(request, checkoutInfo.RequestId);
            await DeleteBasketAsync();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private Task NotifyChangeSubscribersAsync()
        => Task.WhenAll(_changeSubscriptions.Select(s => s.NotifyAsync()));

    private async Task<ClaimsPrincipal> GetUserAsync()
        => (await authenticationStateProvider.GetAuthenticationStateAsync()).User;

    private Task<IReadOnlyCollection<BasketItem>> FetchBasketItemsAsync()
    {
        return _cachedBasket ??= FetchCoreAsync();

        async Task<IReadOnlyCollection<BasketItem>> FetchCoreAsync()
        {
            var quantities = await basketService.GetBasketAsync();
            if (quantities.Count == 0)
            {
                return [];
            }

            // Get details for the items in the basket
            var basketItems = new List<BasketItem>();
            var productIds = quantities.Select(row => row.ProductId);
            var catalogItems = (await catalogService.GetCatalogItems(productIds)).ToDictionary(k => k.Id, v => v);
            foreach (var item in quantities)
            {
                var catalogItem = catalogItems[item.ProductId];
                var orderItem = new BasketItem
                {
                    Id = Guid.NewGuid().ToString(), // TODO: this value is meaningless, use ProductId instead.
                    ProductId = catalogItem.Id,
                    ProductName = catalogItem.Name,
                    UnitPrice = catalogItem.Price,
                    Quantity = item.Quantity,
                };
                basketItems.Add(orderItem);
            }

            return basketItems;
        }
    }

    private class BasketStateChangedSubscription(BasketState Owner, EventCallback Callback) : IDisposable
    {
        public Task NotifyAsync() => Callback.InvokeAsync();
        public void Dispose() => Owner._changeSubscriptions.Remove(this);
    }
}

public record CreateOrderRequest(
    string UserId,
    string UserName,
    string City,
    string Street,
    string State,
    string Country,
    string ZipCode,
    string CardNumber,
    string CardHolderName,
    DateTime CardExpiration,
    string CardSecurityNumber,
    int CardTypeId,
    string Buyer,
    List<BasketItem> Items);