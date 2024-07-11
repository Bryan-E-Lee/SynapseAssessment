namespace Orders
{
    public sealed class OrderProcessorApiConfig
    {
        public string OrdersApiUrl { get; set; } = string.Empty;
        public string AlertApiUrl { get; set; } = string.Empty;
        public string UpdateApiUrl { get; set; } = string.Empty;
    }
}
