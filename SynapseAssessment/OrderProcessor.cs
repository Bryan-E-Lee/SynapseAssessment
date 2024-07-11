using Microsoft.Extensions.Logging;
using Orders;

namespace SynapseAssessment
{
    /// <summary>
    /// A service for processing orders.
    /// </summary>
    public class OrderProcessor
    {
        private readonly IOrderApiService apiService;
        private readonly ILogger logger;
        private readonly Queue<Order> retryQueue;

        public OrderProcessor(IOrderApiService apiService, ILogger logger, Queue<Order> retryQueue)
        {
            this.apiService = apiService;
            this.logger = logger;
            this.retryQueue = retryQueue;
        }

        /// <summary>
        /// Performs the following tasks in order:
        /// 1. Get a list of orders from the API
        /// 2. Check if the order is in a delivered state. 
        ///     2a. If the order is delivered, then send a delivery alert and increment the order's "deliveryNotification" property.
        ///     2b. If the order is not delivered, then ignore and continue processing the remaining items.
        /// </summary>
        public async Task Process()
        {
            var medicalEquipmentOrders = await apiService.FetchMedicalEquipmentOrders();

            var tasks = medicalEquipmentOrders
                .Select(order =>
                {
                    var updatedOrder = ProcessOrder(order);
                    if (updatedOrder == null)
                    {
                        retryQueue.Enqueue(order);
                        return Task.CompletedTask;
                    }
                    return apiService.SendAlertForUpdatedOrder(updatedOrder);
                })
                .ToList();
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Processes a new order.
        /// </summary>
        /// <param name="order">The order to process.</param>
        /// <returns>The newly processed order. If null, there was an error when processing the order.</returns>
        private Order? ProcessOrder(Order order)
        {
            if (string.IsNullOrEmpty(order.OrderId))
            {
                logger.LogError("Received order with no identifier.");
                return null;
            }

            foreach (var item in order.Items)
            {
                if (IsItemDelivered(item)) //Consider inverting conditional to reduce cyclomatic complexity.
                {
                    apiService.SendAlertMessage(item, order.OrderId.ToString());
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
        bool IsItemDelivered(Item item)
        {
            return item.Status?.Equals("Delivered", StringComparison.OrdinalIgnoreCase)
                ?? false;
        }

        /// <summary>
        /// Increments an item's delivery notification count.
        /// </summary>
        /// <param name="item">The item to update.</param>
        void IncrementDeliveryNotification(Item item)
        {
            //Consider moving this to a member in the item class. Then again, it is also supposed to be a POJO / POCO so I can understand resistance to that change.
            item.DeliveryNotification++;
        }
    }
}
