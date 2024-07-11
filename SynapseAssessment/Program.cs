using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orders;
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
                        return SendAlertForUpdatedOrder(updatedOrder);
                    })
                    .ToList();
                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "Unrecoverable error encountered while processing medical equipment orders.");
                return CatastrophicErrorCode;
            }

            return SuccessCode;
        }

        /// <summary>
        /// Retrieves medical equipment orders from an external API.
        /// </summary>
        /// <returns>A list of medical equipment orders as dynamic JSON objects.</returns>
        static async Task<List<Order>> FetchMedicalEquipmentOrders()
        {
            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.GetAsync(OrdersApiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var ordersData = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<Order>>(ordersData)
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
        static Order ProcessOrder(Order order)
        {
            if (string.IsNullOrEmpty(order.OrderId))
            {
                logger.LogError("Received order with no identifier.");
                throw new InvalidOperationException("Attempted to process order with no order id."); //TODO: Consider a better way of gracefully handling an error here. Right now there is no outer catch that will not trigger a catastrophic error.
            }

            foreach (var item in order.Items)
            {
                if (IsItemDelivered(item)) //Consider inverting conditional to reduce cyclomatic complexity.
                {
                    SendAlertMessage(item, order.OrderId.ToString());
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
        static bool IsItemDelivered(Item item)
        {
            return item.Status?.Equals("Delivered", StringComparison.OrdinalIgnoreCase)
                ?? false;
        }

        /// <summary>
        /// Sends an alert message that an item has been delievered.
        /// </summary>
        /// <param name="orderId">The order id for the alert.</param>
        static void SendAlertMessage(Item item, string orderId)
        {
            using var httpClient = new HttpClient();
            var alertData = new
            {
                Message = $@"Alert for delivered item: Order {orderId}, Item: {item.Description}, 
                            Delivery Notifications: {item.DeliveryNotification}"
            };
            var content = new StringContent(JObject.FromObject(alertData).ToString(), Encoding.UTF8, "application/json");
            var response = httpClient.PostAsync(AlertApiUrl, content).Result;

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation($"Alert sent for delivered item: {item.Description}");
            }
            else
            {
                logger.LogError($"Failed to send alert for delivered item: {item.Description}");
            }
        }

        /// <summary>
        /// Increments an item's delivery notification count.
        /// </summary>
        /// <param name="item">The item to update.</param>
        static void IncrementDeliveryNotification(Item item) 
        {
            //Consider moving this to a member in the item class. Then again, it is also supposed to be a POJO / POCO.
            item.DeliveryNotification++;
        }

        /// <summary>
        /// Sends an alert that an order has been delievered.
        /// </summary>
        /// <param name="order">The order to generate an alert for.</param>
        /// <returns></returns>
        static async Task SendAlertForUpdatedOrder(Order order)
        {
            var serializedOrder = JsonConvert.SerializeObject(order);
            var content = new StringContent(serializedOrder, Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(UpdateApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation($"Updated order sent for processing: OrderId {order.OrderId}");
            }
            else
            {
                logger.LogError($"Error sending updated order for processing: OrderId {order.OrderId}");
            }
        }
    }
}