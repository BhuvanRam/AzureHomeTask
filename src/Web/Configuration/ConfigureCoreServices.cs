using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Data.Queries;
using Microsoft.eShopWeb.Infrastructure.Logging;
using Microsoft.eShopWeb.Infrastructure.Services;

namespace Microsoft.eShopWeb.Web.Configuration;

public static class ConfigureCoreServices
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        services.AddScoped<IBasketService, BasketService>();
        services.AddScoped<IBasketQueryService, BasketQueryService>();
        services.AddSingleton<IUriComposer>(new UriComposer(configuration.Get<CatalogSettings>()));
        services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));
        services.AddTransient<IEmailSender, EmailSender>();

        string serviceBusConnectionString = configuration.GetValue<string>("ServiceBusConnectionString");
        string serviceBusQueueName = configuration.GetValue<string>("ServiceBusQueueName");
        string deliveryOrderProcessorUrl = configuration.GetValue<string>("DeliveryOrderProcessorUrl");
        services.AddScoped<IOrderService>(p => new OrderService(p.GetRequiredService<IRepository<ApplicationCore.Entities.BasketAggregate.Basket>>(),
                                                 p.GetRequiredService<IRepository<ApplicationCore.Entities.CatalogItem>>(),
                                                 p.GetRequiredService<IRepository<ApplicationCore.Entities.OrderAggregate.Order>>(),
                                                 p.GetRequiredService<IUriComposer>(),
                                                 serviceBusConnectionString,
                                                 serviceBusQueueName,
                                                 deliveryOrderProcessorUrl,
                                                 p.GetRequiredService<IAppLogger<OrderService>>()));

        return services;
    }
}
