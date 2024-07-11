using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Synapse.OrdersExample
{
    /// <summary>
    /// Performs the following tasks in order:
    /// 1. Get a list of orders from the API
    /// 2. Check if the order is in a delivered state. 
    ///     2a. If the order is delivered, then send a delivery alert and increment the order's "deliveryNotification" property.
    ///     2b. If the order is not delivered, then ignore and continue processing the remaining items.
    /// </summary>
    class Program
    {
        const int SuccessCode = 0;
        const int CatastrophicErrorCode = 1;
        const int PartialFailureErrorCode = 2;

        const string OrdersApiUrl = "https://orders-api.com/orders";
        const string AlertApiUrl = "https://alert-api.com/alerts";
        const string UpdateApiUrl = "https://update-api.com/update";


        /*
         * The below code should be handled through a DI container and there should be
         * configuration for various logging providers to log to as determined by business
         * requirements. This is just a simple console app at the moment though.
         * */
        static ILogger logger = BuildLogger();

        /// <summary>
        /// Creates a new logging service.
        /// </summary>
        /// <returns>A new ILogger.</returns>
        static ILogger BuildLogger()
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder //TODO: Add non-console logging to meet assignment requirements.
                                                                            .AddFilter("System", LogLevel.Warning)
                                                                            .AddFilter("Synapse.OrdersExample.Program", LogLevel.Debug)
                                                                            .AddConsole());
            return factory.CreateLogger("Synapse.OrdersExample.Program");
        }


        static async Task<int> Main(string[] args)
        {
            logger.LogInformation("Start of App");

            try
            {
                var medicalEquipmentOrders = await FetchMedicalEquipmentOrders();
                var tasks = medicalEquipmentOrders
                    .Select(order =>
                    {
                        var updatedOrder = ProcessOrder(order);
                        return SendAlertAndUpdateOrder(updatedOrder);
                    })
                    .ToList();
                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "Unrecoverable error encountered while processing medical equipment orders.");
                return CatastrophicErrorCode;
            }

            logger.LogInformation("Results sent to relevant APIs.");
            return SuccessCode;
        }

        /// <summary>
        /// Retrieves medical equipment orders from an external API.
        /// </summary>
        /// <returns>A list of medical equipment orders as dynamic JSON objects.</returns>
        static async Task<JObject[]> FetchMedicalEquipmentOrders()
        {
            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.GetAsync(OrdersApiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var ordersData = await response.Content.ReadAsStringAsync();
                    return JArray.Parse(ordersData).ToObject<JObject[]>()
                        ?? [];
                }
                else
                {
                    logger.LogError("Failed to fetch orders from API.");
                    return []; //Instructions do not say whether this sort of graceful error handling is appropriate in this case, so I will leave it in place as there are also no instructions to adjust.
                }
            }
            catch (Exception e) //Presumably a more critical error occurred here.
            {
                logger.LogCritical(e, "Error fetching medical equipment orders.");
                throw;
            }
        }

        /// <summary>
        /// Processes a new order.
        /// </summary>
        /// <param name="order">The order to process.</param>
        /// <returns>The newly processed order.</returns>
        static JObject ProcessOrder(JObject order)
        {
            var items = order["Items"]?.ToObject<JArray>() ?? []; //Consider having an invalid schema generate an exception rather than be handled gracefully.

            var orderId = order["OrderId"];
            if (orderId == null)
            {
                logger.LogError("Received order with no identifier.");
                throw new ArgumentNullException("OrderId"); //TODO: Consider a better way of gracefully handling an error here. Right now there is no outer catch that will not trigger a catastrophic error.
            }

            foreach (var item in items)
            {
                if (IsItemDelivered(item)) //Consider inverting conditional to reduce cyclomatic complexity.
                {
                    SendAlertMessage(item, orderId.ToString());
                    IncrementDeliveryNotification(item);
                }
            }

            return order;
        }

        /// <summary>
        /// Indicates whether the input item has been delievered.
        /// </summary>
        /// <param name="item">The item to be checked.</param>
        /// <returns>True if delivered, else false.</returns>
        static bool IsItemDelivered(JToken item)
        {
            return item["Status"]?.ToString().Equals("Delivered", StringComparison.OrdinalIgnoreCase)
                ?? false;
        }

        /// <summary>
        /// Sends an alert message that an item has been delievered.
        /// </summary>
        /// <param name="orderId">The order id for the alert.</param>
        static void SendAlertMessage(JToken item, string orderId)
        {
            using var httpClient = new HttpClient();
            var alertData = new
            {
                Message = $@"Alert for delivered item: Order {orderId}, Item: {item["Description"]}, 
                            Delivery Notifications: {item["deliveryNotification"]}"
            };
            var content = new StringContent(JObject.FromObject(alertData).ToString(), Encoding.UTF8, "application/json");
            var response = httpClient.PostAsync(AlertApiUrl, content).Result;

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation($"Alert sent for delivered item: {item["Description"]}");
            }
            else
            {
                logger.LogError($"Failed to send alert for delivered item: {item["Description"]}");
            }
        }

        /// <summary>
        /// Increments an item's delivery notification count.
        /// </summary>
        /// <param name="item">The item to update.</param>
        static void IncrementDeliveryNotification(JToken item)
        {
            var deliveryNotificationCount = item["deliveryNotification"]?.Value<int>() ?? 0;
            item["deliveryNotification"] = deliveryNotificationCount + 1;
        }

        /// <summary>
        /// Sends an alert 
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        static async Task SendAlertAndUpdateOrder(JObject order)
        {
            using var httpClient = new HttpClient();
            var content = new StringContent(order.ToString(), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(UpdateApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation($"Updated order sent for processing: OrderId {order["OrderId"]}");
            }
            else
            {
                logger.LogError($"Failed to send updated order for processing: OrderId {order["OrderId"]}");
            }
        }
    }
}