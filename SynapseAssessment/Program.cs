using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Config;
using Orders.Entities;
using Orders.Services;

namespace Synapse.OrdersExample
{
    class Program
    {
        const int SuccessCode = 0;
        const int CatastrophicErrorCode = 1;
        
        static ServiceProvider BuildServices(IConfigurationRoot config)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => {
                builder
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("Synapse.OrdersExample.Program", LogLevel.Debug)
                    .AddConsole();

                builder.AddConfiguration(config.GetSection("Logging"));
                builder.AddFile(options => options.RootPath = AppContext.BaseDirectory);
            });
            services.AddHttpClient();
            services.AddSingleton<IOrderRetriever, HttpOrderApiService>();
            services.AddSingleton<IOrderAlertService, HttpOrderApiService>();
            services.AddSingleton<IOrderProcessor, BasicOrderProcessor>();
            var orderApiConfig = new OrderApiConfig();
            config.GetSection("OrderApis").Bind(orderApiConfig);

            services.AddSingleton(orderApiConfig);
            //Normally this isn't appropriate for prod, but I feel as though adding a proper container would be outside the assessment scope.
            services.AddSingleton(new Queue<Order>());

            return services.BuildServiceProvider();
        }


        static async Task<int> Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            var serviceProvider = BuildServices(config);
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Start of App");

            try
            {
                var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                var orderApiConfig = serviceProvider.GetRequiredService<OrderApiConfig>();

                var apiService = serviceProvider.GetRequiredService<IOrderRetriever>();
                var processor = serviceProvider.GetRequiredService<IOrderProcessor>();

                var medicalEquipmentOrders = await apiService.FetchMedicalEquipmentOrders();
                await processor.Process(medicalEquipmentOrders);
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "Unrecoverable error encountered while processing medical equipment orders.");
                return CatastrophicErrorCode;
            }

            /*
             * TODO: Do something with the failed orders, either attempt reprocessing or handle in some other way.
             * I am considering this outside the scope of the assessment.
             * */

            return SuccessCode;
        }
    }
}