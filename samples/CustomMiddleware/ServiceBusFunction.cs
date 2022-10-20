using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus;
using Microsoft.Extensions.Logging;

namespace CustomMiddleware
{
    public class ServiceBusFunction
    {
        private readonly ILogger _logger;

        public ServiceBusFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ServiceBusFunction>();
        }

        [Function("ServiceBusFunction")]
        public void Run(
           
            [ServiceBusTrigger("myqueue", Connection = "MyServiceBusConnStr")] ServiceBusMessageLite triggerData)
        {
            _logger.LogInformation($"C# ServiceBus queue trigger function processed message: {triggerData}");
        }
    }
}
