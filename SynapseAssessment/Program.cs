using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders;
using SynapseAssessment;

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
            return services.BuildServiceProvider();
        }


        static async Task<int> Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            var orderApiConfig = new OrderApiConfig();
            config.GetSection("OrderApis").Bind(orderApiConfig);
            var serviceProvider = BuildServices(config);
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Start of App");

            try
            {
                var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                var failedOrders = new Queue<Order>(); //See TODO below.

                var apiService = new OrderApiService(factory, orderApiConfig, logger);

                var processor = new OrderProcessor(apiService, logger, failedOrders);
                await processor.Process();
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