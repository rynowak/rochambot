using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rochambot.Models;

namespace Rochambot
{
    public class GameClient : IAsyncDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GameClient> _logger;
        private readonly ITopicClient _matchmakingTopic;
        private readonly ITopicClient _matchmakingSendTopic;
        private readonly ISubscriptionClient _matchmakingClient;
        private readonly ISessionClient _matchmakingSessionClient;
        private IMessageSession _matchmakingSession;
        private ManagementClient _managementClient;
        private bool _alreadyVerified;

        public GameClient(IConfiguration configuration,
                          ILogger<GameClient> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _matchmakingTopic = new TopicClient(_configuration["AzureServiceBusConnectionString"], "matchmaking");
            _matchmakingClient = new SubscriptionClient(_configuration["AzureServiceBusConnectionString"], "matchmaking", "webapp");
            _matchmakingSessionClient = new SessionClient(_configuration["AzureServiceBusConnectionString"], EntityNameHelper.FormatSubscriptionPath("matchmaking", "webapp"));

            // team advised using a separate client for listening and sending
            _matchmakingSendTopic = new TopicClient(_configuration["AzureServiceBusConnectionString"], "matchmaking");
            
            Games = new List<Game>();
        }

        public IList<Game> Games {get;}
        public Game CurrentGame {get;}
        public Opponent Opponent { get; private set; }
        public UserState UserState { get; set; }
        public event EventHandler OnStateChanged;
        
        public async Task SetPlayerId(UserState userState)
        {
            if(UserState == null || (!UserState.DisplayName.Equals(userState.DisplayName)))
            {
                UserState = userState;
                _logger.LogInformation($"User {UserState.DisplayName} logged in");
                _matchmakingClient.RegisterSessionHandler(HandleMatchmakingMessage, HandleMatchmakingError);
                _matchmakingSession = await _matchmakingSessionClient.AcceptMessageSessionAsync(UserState.DisplayName);
                _logger.LogInformation($"Set up subscription session with session id {UserState.DisplayName}");
                OnStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task RequestGameAsync()
        {
            var gameId = Guid.NewGuid().ToString();

            _logger.LogInformation("Sending matchmaker message.");

            var gameStartMessage = new Message
            {
                ReplyToSessionId = UserState.DisplayName
            };

            gameStartMessage.Label = "gamerequest";
            gameStartMessage.UserProperties.Add("gameId", gameId);
            gameStartMessage.UserProperties.Add("gameready", false);
            
            var game = new Game(gameId);
            this.Games.Add(game);

            //await _matchmakingTopic.SendAsync(gameStartMessage);
            await _matchmakingSendTopic.SendAsync(gameStartMessage);
        }

        private async Task HandleMatchmakingMessage(IMessageSession messageSession, Message message, CancellationToken cancellationToken)
        {
            await VerifyPlayReceivedSubscriptionExists();

            var gameId = message.UserProperties["gameId"].ToString();
            var opponentId = message.UserProperties["opponentId"].ToString();

            _logger.LogInformation($"Matchmaking request from {UserState.DisplayName} accepted by {opponentId}");
            
            if(!Games.Any(x => x.Id == gameId))
            {
                // message from expired game, bail out
            }
            else
            {
                Games.Single(x => x.Id == gameId).MatchMade(opponentId);
                _logger.LogInformation("MatchMade");
                OnStateChanged?.Invoke(this, EventArgs.Empty);
                //await _matchmakingClient.CompleteAsync(message.SystemProperties.LockToken);
            }
        }

        private Task HandleMatchmakingError(ExceptionReceivedEventArgs arg)
        {
            //TODO: We probably need to error out whatever game this happened on.
            _logger.LogError("Error in matchmaking", arg.Exception, arg.ExceptionReceivedContext);
            throw new NotImplementedException();
        }

        public async ValueTask DisposeAsync()
        {
            await _matchmakingTopic?.CloseAsync();
            await _matchmakingSendTopic?.CloseAsync();
            await _matchmakingSessionClient?.CloseAsync();
            await _matchmakingClient?.CloseAsync();
        }

        private async Task VerifyPlayReceivedSubscriptionExists()
        {
            if(_alreadyVerified == true) await Task.FromResult(0);

            _managementClient = new ManagementClient(_configuration["AzureServiceBusConnectionString"]);

            if(!await _managementClient.TopicExistsAsync(_configuration["PlayTopic"]))
            {
                await _managementClient.CreateTopicAsync(new TopicDescription(_configuration["PlayTopic"])
                {
                    SupportOrdering = true
                });

            if (!await _managementClient.SubscriptionExistsAsync(_configuration["PlayTopic"], "playreceived"))
            {
                await _managementClient.CreateSubscriptionAsync
                (
                    new SubscriptionDescription(_configuration["PlayTopic"], "playreceived")
                    {
                        DefaultMessageTimeToLive = TimeSpan.FromMinutes(2),
                        MaxDeliveryCount = 3,
                        RequiresSession = true
                    }
                );
            }

            _alreadyVerified = true;
        }
    }
}