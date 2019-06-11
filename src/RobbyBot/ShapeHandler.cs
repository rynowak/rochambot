using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rochambot;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RobbyBot
{
    public class ShapeHandler : IHostedService
    {
        readonly ILogger<ShapeHandler> _logger;
        readonly IConfiguration _configuration;
        readonly string _botId;
        private readonly MoveMaker _moveMaker;
        private readonly ISubscriptionClient _resultsClient;
        private ManagementClient _managementClient;

        public ShapeHandler(ILogger<ShapeHandler> logger, IConfiguration configuration, MoveMaker moveMaker)
        {
            _logger = logger;
            _configuration = configuration;
            _botId = _configuration["BotId"];
            _moveMaker = moveMaker;
            
            _resultsClient = new SubscriptionClient(_configuration["AzureServiceBusConnectionString"], _configuration["ResultsTopic"], "robbybot");
        }

        private Task HandleError(ExceptionReceivedEventArgs arg)
        {
            _logger.LogError("Error processing play message {exception}", arg.Exception);
            return Task.CompletedTask;
        }

        private async Task HandleMessage(Message message, CancellationToken arg2)
        {
            var gameId = message.UserProperties["gameId"].ToString();
            await _moveMaker.MakeMove(gameId);
        }

        public Task StartAsync(CancellationToken token)
        {
            _resultsClient.RegisterMessageHandler(HandleMessage, new MessageHandlerOptions(HandleError) { AutoComplete = true });
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken token)
        {
            await _managementClient?.CloseAsync();
            await _resultsClient?.CloseAsync();
        }
    }
}