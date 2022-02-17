using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace OrderProcessor
{
    public static class DeliveryOrderProcessor
    {
        [FunctionName("DeliveryOrderProcessor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log,
            [CosmosDB(databaseName: "eShopByWebOrders", collectionName: "Orders", ConnectionStringSetting = "OrdersConnectionString")] IAsyncCollector<dynamic> documentsOut)
        {

            log.LogInformation($"OrderItemsReserver Started for OrderId: {req.Query["orderId"]}");
            string orderId = req.Query["orderId"];
            string orderDetails = await new StreamReader(req.Body).ReadToEndAsync();
            string responseMessage = $"Order placed successfully. OrderId: {orderId}, OrderDetails: {orderDetails}";

            dynamic data = JsonConvert.DeserializeObject(orderDetails);
            
            if (!string.IsNullOrEmpty(orderId))
            {
                await documentsOut.AddAsync(new
                {
                    id = orderId,
                    data = data
                });
            }

            return new OkObjectResult(responseMessage);
        }
    }
}
