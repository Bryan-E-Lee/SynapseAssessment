namespace Orders
{
    public interface IOrderApiService
    {
        public Task<List<Order>> FetchMedicalEquipmentOrders();
        public Task SendAlertMessage(Item item, string orderId);
        public Task SendAlertForUpdatedOrder(Order order);
    }
}
