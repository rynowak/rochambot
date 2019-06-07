using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.ServiceBus.Management;
using System.Text;
using Rochambot;

namespace GameMaster
{
    public class GameMaster : IHostedService
    {
        private const string MatchMakingTopic = "MatchmakingTopic";
        private const string PlayTopic = "PlayTopic";
        private const string AzureServiceBusConnectionString = "AzureServiceBusConnectionString";
        private static string Name = nameof(GameMaster).ToLower();
        private readonly IConfiguration _configuration;
        private readonly ILogger<GameMaster> _logger;
        private readonly GameData _gameData;
        private ManagementClient _managementClient;
        private ISubscriptionClient _matchmakingSubscriptionClient;
        private ISubscriptionClient _playSubscriptionClient;
        private TopicClient _playTopicClient;

        public GameMaster(ILogger<GameMaster> logger, 
            IConfiguration configuration,
            GameData gameData)
        {
            _configuration = configuration;
            _logger = logger;
            _gameData = gameData;
        }

        public async Task StartAsync(CancellationToken token)
        {
            await VerifyMatchMakingSubscriptionExists();

            _matchmakingSubscriptionClient = new SubscriptionClient(
                _configuration[GameMaster.AzureServiceBusConnectionString],
                _configuration[GameMaster.MatchMakingTopic],
                GameMaster.Name);

            _matchmakingSubscriptionClient.RegisterMessageHandler(OnMatchmakingMessageReceived, 
                new MessageHandlerOptions(OnMessageHandlingException) 
                {
                    AutoComplete = false,
                    MaxConcurrentCalls = 1
                });
        }

        public async Task StopAsync(CancellationToken token)
        {
            await _matchmakingSubscriptionClient.CloseAsync();
            await _managementClient.CloseAsync();
        }

        private async Task OnMatchmakingMessageReceived(Message message, CancellationToken token)
        {
            _logger.LogInformation($"Received message: {message.SystemProperties.SequenceNumber}");

            var gameId = message.UserProperties["gameId"].ToString();
            var opponentId = message.UserProperties["opponentId"].ToString();
            var playerId = message.SessionId;

            if(!string.IsNullOrEmpty(playerId))
            {
                Game game = null;

                if(!(await _gameData.GameExists(playerId, gameId)))
                {
                    game = await _gameData.CreateGame(playerId, gameId, opponentId);
                    _logger.LogInformation($"Game created for {playerId} to play {opponentId}");
                }
                else
                {
                    game = await _gameData.GetGame(playerId, gameId);
                    _logger.LogInformation($"Game retrieved for {playerId} to play {opponentId}");
                    
                }
            }

            //await _matchmakingSubscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
        }

        private Task OnMessageHandlingException(ExceptionReceivedEventArgs args)
        {
            _logger.LogError(args.Exception, $"Message handler error: {Environment.NewLine}Endpoint: {args.ExceptionReceivedContext.Endpoint}{Environment.NewLine}Client ID: {args.ExceptionReceivedContext.ClientId}{Environment.NewLine}Entity Path: {args.ExceptionReceivedContext.EntityPath}");
            return Task.CompletedTask;
        }

        private async Task VerifyMatchMakingSubscriptionExists()
        {
            _managementClient = new ManagementClient(_configuration[GameMaster.AzureServiceBusConnectionString]);

            if (!await _managementClient.SubscriptionExistsAsync(_configuration[GameMaster.MatchMakingTopic], GameMaster.Name))
            {
                await _managementClient.CreateSubscriptionAsync
                (
                    new SubscriptionDescription(_configuration[GameMaster.MatchMakingTopic], GameMaster.Name)
                    {
                        DefaultMessageTimeToLive = TimeSpan.FromMinutes(2),
                        MaxDeliveryCount = 3
                    },
                    new RuleDescription($"gamereadyrule", new CorrelationFilter
                    {
                        Label = "gameready"
                    })
                );
            }
        }
    }
}