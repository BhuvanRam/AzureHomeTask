using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Newtonsoft.Json;
using Azure.Messaging.ServiceBus;
using System;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;

    private readonly string _serviceBusConnectionString;
    private readonly string _serviceBusQueueName;
    private readonly string _deliveryOrderProcessorUrl;
    private ServiceBusClient serviceBusclient;
    private ServiceBusSender serviceBusSender;
    private readonly IAppLogger<OrderService> _logger;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        string serviceBusConnectionString,
        string serviceBusQueueName,
        string deliveryOrderProcessorUrl,
        IAppLogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;

        _serviceBusConnectionString = serviceBusConnectionString;
        _serviceBusQueueName = serviceBusQueueName;
        _deliveryOrderProcessorUrl = deliveryOrderProcessorUrl;
        _logger = logger;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);
        await _orderRepository.AddAsync(order);

        _ = SendOrderDetailsToServiceBusQueue(order);
        _ = DeliveryOrderProcessor(order);
    }

    private async Task SendOrderDetailsToServiceBusQueue(Order order)
    {
        OrderReserver orderedItems = new OrderReserver()
        {
            OrderId = order.Id,
            OrderedItems = order.OrderItems.Select(p => new OrderedItem()
            {
                ProductId = p.ItemOrdered.CatalogItemId,
                ProductQuantity = p.Units,
                ProductName = p.ItemOrdered.ProductName
            }).ToList()

        };

        string orderReserverDetails = JsonConvert.SerializeObject(orderedItems);
        try
        {
            serviceBusclient = new ServiceBusClient(_serviceBusConnectionString);
            serviceBusSender = serviceBusclient.CreateSender(_serviceBusQueueName);
            using (ServiceBusMessageBatch messageBatch = await serviceBusSender.CreateMessageBatchAsync())
            {
                var serviceBusMessage = new ServiceBusMessage($"{orderReserverDetails}")
                {
                    ContentType = "application/json",
                };

                if (!messageBatch.TryAddMessage(serviceBusMessage))
                {
                    throw new Exception($"Order failed to store in Service Bus queue. OrderDetails {orderReserverDetails}");
                }
                await serviceBusSender.SendMessagesAsync(messageBatch);
            }
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            await serviceBusSender.DisposeAsync();
            await serviceBusclient.DisposeAsync();
        }
    }

    private async Task DeliveryOrderProcessor(Order order)
    {
        var Url = $"{_deliveryOrderProcessorUrl}?orderId={order.Id}";
        dynamic content = new { data = order };


        using (var client = new HttpClient())
        using (var request = new HttpRequestMessage(HttpMethod.Post, Url))
        using (var httpContent = CreateHttpContent(content))
        {
            request.Content = httpContent;
            using (var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false))
            {
                string responseFromFunction = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Response from DeliveryOrderProcessor: {responseFromFunction}");
            }
        }
    }

    private static HttpContent CreateHttpContent(object content)
    {
        HttpContent httpContent = null;

        if (content != null)
        {
            var ms = new MemoryStream();
            SerializeJsonIntoStream(content, ms);
            ms.Seek(0, SeekOrigin.Begin);
            httpContent = new StreamContent(ms);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return httpContent;
    }

    public static void SerializeJsonIntoStream(object value, Stream stream)
    {
        using (var sw = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
        using (var jtw = new JsonTextWriter(sw) { Formatting = Formatting.None })
        {
            var js = new JsonSerializer();
            js.Serialize(jtw, value);
            jtw.Flush();
        }
    }
}
