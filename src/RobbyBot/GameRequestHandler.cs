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

        ISubscriptionClient _botRequestQueue;
        ITopicClient _botResponseQueue;

        public GameRequestHandler(ILogger<GameRequestHandler> logger, IConfiguration configuration)
        {

            _logger = logger;
            _configuration = configuration;
            _botId = _configuration["BotId"];
        }

        public Task StartAsync(CancellationToken token)
        {
            _botRequestQueue = new SubscriptionClient(_configuration["AzureServiceBusConnectionString"],
                                                      "matchmaking",
                                                      "bots");

            _botResponseQueue = new TopicClient(_configuration["AzureServiceBusConnectionString"],
                                    "matchmaking");

            _botRequestQueue.RegisterMessageHandler(ReceivedGameRequestAsync, ReceivedGameRequestErrorAsync);

            //await _botRequestQueue.AddRuleAsync("requestRule", new CorrelationFilter
            //{
            //    Label = "gamerequest"
            //});

            return Task.CompletedTask;
        }

        private async Task ReceivedGameRequestAsync(Message message, CancellationToken _)
        {
            var msg = new Message
            {
                SessionId = message.ReplyToSessionId
            };
            msg.Label = "gameready";
            msg.UserProperties.Add("gameId", message.UserProperties["gameId"]);
            msg.UserProperties.Add("oponentId", _botId);

            await _botResponseQueue.SendAsync(msg);
            //TODO: Looks like the default for topics is to handle them when they are processed
            //This might be OK for now but we should think about it.
            //await _botRequestQueue.CompleteAsync(message.SystemProperties.LockToken);
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