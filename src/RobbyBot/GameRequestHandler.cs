using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace RobbyBot
{
    public class GameRequestHandler : IHostedService
    {
        readonly ILogger<GameRequestHandler> _logger;
        readonly IConfiguration _configuration;
        readonly string _botId;
        private readonly MoveMaker _moveMaker;
        ISubscriptionClient _botRequestQueue;
        ITopicClient _botResponseQueue;

        public GameRequestHandler(ILogger<GameRequestHandler> logger, 
                                  IConfiguration configuration,
                                  MoveMaker moveMaker)
        {
            _logger = logger;
            _configuration = configuration;
            _botId = _configuration["BotId"];
            _moveMaker = moveMaker;
        }

        public Task StartAsync(CancellationToken token)
        {
            _botRequestQueue = new SubscriptionClient(_configuration["AzureServiceBusConnectionString"],
                                                      "matchmaking",
                                                      "bots");

            _botResponseQueue = new TopicClient(_configuration["AzureServiceBusConnectionString"],
                                    "matchmaking");

            _botRequestQueue.RegisterMessageHandler(ReceivedGameRequestAsync, ReceivedGameRequestErrorAsync);

            return Task.CompletedTask;
        }

        private async Task ReceivedGameRequestAsync(Message message, CancellationToken _)
        {
            _logger.LogInformation($"Player {message.SessionId} requested game, {_botId} accepted.");

            var msg = new Message
            {
                SessionId = message.ReplyToSessionId,
                Label = "gameready"
            };
            
            var gameId = message.UserProperties["gameId"].ToString();
            msg.UserProperties.Add("gameId", gameId);
            msg.UserProperties.Add("opponentId", _botId);

            await _botResponseQueue.SendAsync(msg);

            await _moveMaker.MakeMove(gameId);
        }

        private Task ReceivedGameRequestErrorAsync(ExceptionReceivedEventArgs arg)
        {
            _logger.LogError(arg.Exception, "Error receiving game request");
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken token)
        {
            await _botRequestQueue?.CloseAsync();
            await _botResponseQueue?.CloseAsync();
        }
    }
}