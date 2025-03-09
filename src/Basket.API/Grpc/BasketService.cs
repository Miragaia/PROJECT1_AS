using System.Diagnostics.CodeAnalysis;
using eShop.Basket.API.Repositories;
using eShop.Basket.API.Extensions;
using eShop.Basket.API.Model;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace eShop.Basket.API.Grpc;

public class BasketService(
    IBasketRepository repository,
    ILogger<BasketService> logger) : Basket.BasketBase
{
    private static readonly ActivitySource _activitySource = new("eShop.Basket.API");
    private static readonly TextMapPropagator _propagator = Propagators.DefaultTextMapPropagator;

    [AllowAnonymous]
    public override async Task<CustomerBasketResponse> GetBasket(GetBasketRequest request, ServerCallContext context)
    {
        using var activity = _activitySource.StartActivity("GetBasket", ActivityKind.Server);
        
        logger.LogInformation("GetBasket called for user ID: {UserId}", context.GetUserIdentity());

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.type", "authentication");
            activity?.AddEvent(new("User is not authenticated"));
            return new();
        }

        activity?.SetTag("user.id", userId); // This will be masked by our processor

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin GetBasketById call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var data = await repository.GetBasketAsync(userId);

        if (data is not null)
        {
            activity?.SetTag("basket.items.count", data.Items?.Count ?? 0);
            activity?.SetTag("basket.found", true);
            return MapToCustomerBasketResponse(data);
        }

        activity?.SetTag("basket.found", false);
        return new();
    }

    public override async Task<CustomerBasketResponse> UpdateBasket(UpdateBasketRequest request, ServerCallContext context)
    {
        using var activity = _activitySource.StartActivity("UpdateBasket", ActivityKind.Server);

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.type", "authentication");
            ThrowNotAuthenticated();
        }

        activity?.SetTag("user.id", userId); // This will be masked by our processor

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin UpdateBasket call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var customerBasket = MapToCustomerBasket(userId, request);
        var response = await repository.UpdateBasketAsync(customerBasket);
        if (response is null)
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.type", "not_found");
            ThrowBasketDoesNotExist(userId);
        }

        activity?.SetTag("basket.items.count", response.Items?.Count ?? 0);
        return MapToCustomerBasketResponse(response);
    }

    public override async Task<DeleteBasketResponse> DeleteBasket(DeleteBasketRequest request, ServerCallContext context)
    {
        using var activity = _activitySource.StartActivity("DeleteBasket", ActivityKind.Server);

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.type", "authentication");
            ThrowNotAuthenticated();
        }

        activity?.SetTag("user.id", userId); // This will be masked by our processor

        await repository.DeleteBasketAsync(userId);
        return new();
    }

    [DoesNotReturn]
    private static void ThrowNotAuthenticated() => throw new RpcException(new Status(StatusCode.Unauthenticated, "The caller is not authenticated."));

    [DoesNotReturn]
    private static void ThrowBasketDoesNotExist(string userId) => throw new RpcException(new Status(StatusCode.NotFound, $"Basket with buyer id {userId} does not exist"));

    private static CustomerBasketResponse MapToCustomerBasketResponse(CustomerBasket customerBasket)
    {
        var response = new CustomerBasketResponse();

        foreach (var item in customerBasket.Items)
        {
            response.Items.Add(new BasketItem()
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            });
        }

        return response;
    }

    private static CustomerBasket MapToCustomerBasket(string userId, UpdateBasketRequest customerBasketRequest)
    {
        var response = new CustomerBasket
        {
            BuyerId = userId
        };

        foreach (var item in customerBasketRequest.Items)
        {
            response.Items.Add(new()
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            });
        }

        return response;
    }
}
