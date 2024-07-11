using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

namespace Orders
{
    public class OrderApiService : IOrderApiService
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly OrderProcessorApiConfig config;
        private readonly ILogger logger;

        public OrderApiService(IHttpClientFactory httpClientFactory, OrderProcessorApiConfig config, ILogger logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.config = config;
            this.logger = logger;
        }

        /// <summary>
        /// Retrieves medical equipment orders from an external API.
        /// </summary>
        /// <returns>A list of medical equipment orders as dynamic JSON objects.</returns>
        public async Task<List<Order>> FetchMedicalEquipmentOrders()
        {
            using var httpClient = httpClientFactory.CreateClient();
            try
            {
                var response = await httpClient.GetAsync(config.OrdersApiUrl);
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
        /// Sends an alert message that an item has been delivered.
        /// </summary>
        /// <param name="orderId">The order id for the alert.</param>
        public async Task SendAlertMessage(Item item, string orderId)
        {
            using var httpClient = httpClientFactory.CreateClient();
            var alertData = new
            {
                Message = $@"Alert for delivered item: Order {orderId}, Item: {item.Description}, 
                            Delivery Notifications: {item.DeliveryNotification}"
            };
            var content = new StringContent(JObject.FromObject(alertData).ToString(), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(config.AlertApiUrl, content);

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
        /// Sends an alert that an order has been delievered.
        /// </summary>
        /// <param name="order">The order to generate an alert for.</param>
        /// <returns></returns>
        public async Task SendAlertForUpdatedOrder(Order order)
        {
            var serializedOrder = JsonConvert.SerializeObject(order);
            var content = new StringContent(serializedOrder, Encoding.UTF8, "application/json");

            using var httpClient = httpClientFactory.CreateClient();
            var response = await httpClient.PostAsync(config.UpdateApiUrl, content);

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
