using Orders.Entities;

namespace Orders.Services
{

    public interface IOrderAlertService
    {
        public Task SendAlertMessage(Item item, string orderId);
        public Task SendAlertForUpdatedOrder(Order order);
    }
}
