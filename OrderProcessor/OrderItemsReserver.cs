using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace OrderProcessor
{
    public static class OrderItemsReserver
    {
        [FunctionName("OrderItemsReserver")]
        [FixedDelayRetry(2, "00:00:5")]
        public static async Task Run
            (
            [ServiceBusTrigger("reserveorders", Connection = "ServiceBusConnectionString")]string myQueueItem, 
            ILogger log,
            IBinder binder)
        {
            log.LogInformation($"Start: C# ServiceBus queue trigger function processed message: {myQueueItem}");

            dynamic orderReserverDetails = JsonConvert.DeserializeObject(myQueueItem);
            BlobAttribute attribute = new BlobAttribute($"orders/{orderReserverDetails.OrderId}.json", FileAccess.Write);
            attribute.Connection = "OrdersStorageConnectionString";

            Stream orderStream = binder.Bind<Stream>(attribute);
            var writer = new StreamWriter(orderStream);
            writer.Write(myQueueItem);
            writer.Flush();
            orderStream.Position = 0;
            log.LogInformation($"END: C# ServiceBus queue trigger function processed message: {myQueueItem}");
        }
    }
}
