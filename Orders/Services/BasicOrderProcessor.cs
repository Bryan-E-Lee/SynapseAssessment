using Microsoft.Extensions.Logging;
using Orders.Entities;
using System.Collections.Concurrent;

namespace Orders.Services
{
    /// <summary>
    /// A service for processing orders.
    /// </summary>
    public class BasicOrderProcessor : IOrderProcessor
    {
        private readonly IOrderAlertService apiService;
        private readonly ILogger<BasicOrderProcessor> logger;
        private readonly ConcurrentQueue<Order> retryQueue;

        public BasicOrderProcessor(IOrderAlertService apiService, ILogger<BasicOrderProcessor> logger, ConcurrentQueue<Order> retryQueue)
        {
            this.apiService = apiService;
            this.logger = logger;
            this.retryQueue = retryQueue;
        }

        /// <summary>
        /// Processes the input orders, submitting alerts when they have been delivered and signals when they have been updated.
        /// </summary>
        /// <param name="medicalEquipmentOrders">The collection of orders to process</param>
        public async Task Process(IEnumerable<Order> medicalEquipmentOrders) //Rely on interfaces, return concretions
        {
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
        private bool IsItemDelivered(Item item)
            => item.Status?.Equals("Delivered", StringComparison.OrdinalIgnoreCase)
                ?? false;

        /// <summary>
        /// Increments an item's delivery notification count.
        /// </summary>
        /// <param name="item">The item to update.</param>
        private void IncrementDeliveryNotification(Item item)
        {
            //Consider moving this to a member in the item class. Then again, it is also supposed to be a POJO / POCO so I can understand resistance to that change.
            item.DeliveryNotification++;
        }
    }
}
