using Orders.Entities;

namespace Orders.Services
{
    public interface IOrderRetriever
    {
        public Task<List<Order>> FetchMedicalEquipmentOrders();
    }
}
