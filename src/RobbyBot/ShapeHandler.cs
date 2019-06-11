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
    public class ShapeHandler : BackgroundService
    {
        readonly ILogger<ShapeHandler> _logger;
        readonly IConfiguration _configuration;
        readonly string _botId;
        private readonly MoveMaker _moveMaker;
        private readonly SubscriptionClient _resultsClient;
        private readonly SessionClient _resultsSubClient;
        private ManagementClient _managementClient;
        private IMessageSession _resultsSession;

        public ShapeHandler(ILogger<ShapeHandler> logger, IConfiguration configuration, MoveMaker moveMaker)
        {
            _logger = logger;
            _configuration = configuration;
            _botId = _configuration["BotId"];
            _moveMaker = moveMaker;
            
            _resultsClient = new SubscriptionClient(_configuration["AzureServiceBusConnectionString"], _configuration["ResultsTopic"], "players");
            _resultsSubClient = new SessionClient(_configuration["AzureServiceBusConnectionString"], EntityNameHelper.FormatSubscriptionPath(_configuration["ResultsTopic"], "players"));
        }

        public override async Task StartAsync(CancellationToken token)
        {
            await VerifySubscriptionExistsForPlayerAsync();

            _resultsSession = await _resultsSubClient.AcceptMessageSessionAsync(_botId);
            await base.StartAsync(token);
        }


        private Task HandleResultError(ExceptionReceivedEventArgs arg)
        {
            throw new NotImplementedException();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var message = await _resultsSession.ReceiveAsync();
                if (message == null) continue;

                var gameId = message.UserProperties["gameId"].ToString();
                await _moveMaker.MakeMove(gameId);
                await _resultsSession.CompleteAsync(message.SystemProperties.LockToken);
            }
        }

        public override async Task StopAsync(CancellationToken token)
        {
            await _managementClient?.CloseAsync();
            await _resultsSession?.CloseAsync();
            await _resultsSubClient?.CloseAsync();
            await _resultsClient?.CloseAsync();
            await base.StopAsync(token);
        }

        public async Task VerifySubscriptionExistsForPlayerAsync()
        {
            _managementClient = new ManagementClient(_configuration["AzureServiceBusConnectionString"]);

            //await _managementClient.CreateSubscriptionAsync(new SubscriptionDescription("matchmaking", "gamemaster")
            //{
            //    RequiresSession = true,
            //    LockDuration = TimeSpan.FromSeconds(30)
            //}, new RuleDescription("readyrule", new CorrelationFilter() { Label = "gameready" })) ;

            if (!await _managementClient.SubscriptionExistsAsync(_configuration["ResultsTopic"], "bots"))
            {
                await _managementClient.CreateSubscriptionAsync
                (
                    new SubscriptionDescription(_configuration["ResultsTopic"], "bots")
                    {
                        RequiresSession=true
                    }
                );
            }
        }
    }
}