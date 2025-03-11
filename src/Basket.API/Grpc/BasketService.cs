﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using eShop.Basket.API.Repositories;
using eShop.Basket.API.Extensions;
using eShop.Basket.API.Model;

namespace eShop.Basket.API.Grpc;

public class BasketService(
    IBasketRepository repository,
    ILogger<BasketService> logger) : Basket.BasketBase
{
    private static readonly ActivitySource ActivitySource = new("Basket.API");

    [AllowAnonymous]
    public override async Task<CustomerBasketResponse> GetBasket(GetBasketRequest request, ServerCallContext context)
    {
        using var activity = ActivitySource.StartActivity("GetBasket");

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            return new();
        }

        activity?.SetTag("User ID", userId);
        activity?.SetTag("Request", request);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin GetBasketById call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var data = await repository.GetBasketAsync(userId);

        if (data is not null)
        {
            return MapToCustomerBasketResponse(data);
        }

        return new();
    }

    public override async Task<CustomerBasketResponse> UpdateBasket(UpdateBasketRequest request, ServerCallContext context)
    {
        using var activity = ActivitySource.StartActivity("UpdateBasket");

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            ThrowNotAuthenticated();
        }

        activity?.SetTag("User ID", userId);
        activity?.SetTag("Request", request);
        var basketItems = request.Items
            .Select(item => "Product " + item.ProductId + " (" + item.Quantity +")")
            .ToList();

        var basketJson = JsonSerializer.Serialize(basketItems);

        activity?.SetTag("Basket Items", basketJson);
        activity?.SetTag("Total Unique Items", request.Items.Count);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin UpdateBasket call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var customerBasket = MapToCustomerBasket(userId, request);
        var response = await repository.UpdateBasketAsync(customerBasket);
        if (response is null)
        {
            ThrowBasketDoesNotExist(userId);
        }

        return MapToCustomerBasketResponse(response);
    }

    public override async Task<DeleteBasketResponse> DeleteBasket(DeleteBasketRequest request, ServerCallContext context)
    {
        using var activity = ActivitySource.StartActivity("DeleteBasket");

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            ThrowNotAuthenticated();
        }

        activity?.SetTag("User ID", userId);
        activity?.SetTag("Request", request);

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
