namespace Orders.Entities
{
    public class Item
    {
        public string Description { get; set; } = string.Empty;
        public int DeliveryNotification { get; set; }
        public string? Status { get; set; }
    }
}
