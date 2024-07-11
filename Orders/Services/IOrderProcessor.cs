using Orders.Entities;

namespace Orders.Services
{
    public interface IOrderProcessor
    {
        public Task Process(IEnumerable<Order> medicalEquipmentOrders);
    }
}
