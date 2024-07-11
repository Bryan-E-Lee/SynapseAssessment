using Microsoft.Extensions.Logging;
using Orders;
using SynapseAssessment;

namespace Synapse.OrdersExample
{
    class Program
    {
        const int SuccessCode = 0;
        const int CatastrophicErrorCode = 1;

        const string OrdersApiUrl = "https://orders-api.com/orders";
        const string AlertApiUrl = "https://alert-api.com/alerts";
        const string UpdateApiUrl = "https://update-api.com/update";

        /*
         * The below code should be handled through a DI container and there should be
         * configuration for various logging providers to log to as determined by business
         * requirements. This is just a simple console app at the moment though.
         * */
        static ILogger logger = BuildLogger();

        /// <summary>
        /// Creates a new logging service.
        /// </summary>
        /// <returns>A new ILogger.</returns>
        static ILogger BuildLogger()
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder //TODO: Add non-console logging to meet assignment requirements.
                                                                            .AddFilter("System", LogLevel.Warning)
                                                                            .AddFilter("Synapse.OrdersExample.Program", LogLevel.Debug)
                                                                            .AddConsole());
            return factory.CreateLogger("Synapse.OrdersExample.Program");
        }


        static async Task<int> Main(string[] args)
        {
            logger.LogInformation("Start of App");

            try
            {
                var config = new OrderProcessorApiConfig
                {
                    OrdersApiUrl = OrdersApiUrl,
                    AlertApiUrl = AlertApiUrl,
                    UpdateApiUrl = UpdateApiUrl,
                };
                using var factory = new DefaultHttpClientFactory();
                var failedOrders = new Queue<Order>(); //See TODO below.

                var apiService = new OrderApiService(factory, config, logger);

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