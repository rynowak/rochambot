using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rochambot.Models;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Rochambot
{
    public class GameClient : IAsyncDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GameClient> _logger;
        private readonly ITopicClient _matchmakingTopic;
        private readonly ITopicClient _matchmakingSendTopic;
        private readonly ITopicClient _playTopic;
        private readonly SubscriptionClient _resultsSubscription;
        private readonly SessionClient _resultsSessionClient;
        private readonly ISubscriptionClient _matchmakingClient;
        private readonly ISessionClient _matchmakingSessionClient;
        private IMessageSession _matchmakingSession;
        private ManagementClient _managementClient;
        private bool _alreadyVerified;
        private IMessageSession _resultsSession;

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

            _playTopic = new TopicClient(_configuration["AzureServiceBusConnectionString"], _configuration["PlayTopic"]);
            
            _resultsSubscription = new SubscriptionClient(_configuration["AzureServiceBusConnectionString"], "results", "players");
            _resultsSessionClient = new SessionClient(_configuration["AzureServiceBusConnectionString"], EntityNameHelper.FormatSubscriptionPath("results", "players"));

            Games = new List<Game>();
        }

        public IList<Game> Games { get; }
        public Game CurrentGame { get; set; }
        public Opponent Opponent { get; private set; }
        public UserState UserState { get; set; }
        public event EventHandler OnStateChanged;
        private string userHash;
        public async Task SetPlayerId(UserState userState)
        {
            if (UserState == null || (!UserState.DisplayName.Equals(userState.DisplayName)))
            {
                //userHash = Encoding.UTF8.GetString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(userState.DisplayName)));
                UserState = userState;
                _logger.LogInformation($"User {UserState.DisplayName} logged in");
                _resultsSubscription.RegisterSessionHandler(OnResultMessage, new SessionHandlerOptions(OnResultError)
                {
                    AutoComplete = false
                });
                //_resultsSession = await _resultsSessionClient.AcceptMessageSessionAsync(userState.DisplayName);
                _matchmakingClient.RegisterSessionHandler(HandleMatchmakingMessage, new SessionHandlerOptions(HandleMatchmakingError)
                {
                    AutoComplete = false
                });
                //_matchmakingSession = await _matchmakingSessionClient.AcceptMessageSessionAsync(userState.DisplayName);
                _logger.LogInformation($"Set up subscription session with session id {userState.DisplayName}");
                OnStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private Task OnResultError(ExceptionReceivedEventArgs arg)
        {
            _logger.LogError("Error handing results {ex}", arg.Exception);
            return Task.CompletedTask;
        }

        private async Task OnResultMessage(IMessageSession session, Message message, CancellationToken arg2)
        {
            try
            {
                if (session.SessionId != UserState.DisplayName) { return;  }

                _logger.LogInformation("Results message arrived.");
                //await session.CompleteAsync(message.SystemProperties.LockToken);
                var gameId = message.UserProperties["gameId"].ToString();
                var game = Games.Single(x => x.Id == gameId);
                var round = JsonConvert.DeserializeObject<Round>(Encoding.UTF8.GetString(message.Body));
                game.Rounds.Add(round);
                game.PlayMade = false;

                if (message.Label == "GameComplete")
                {
                    game.GameOver = true;
                }

                OnStateChanged?.Invoke(this, EventArgs.Empty);
                await session.CompleteAsync(message.SystemProperties.LockToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling result {ex}", ex);
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
            if (messageSession.SessionId != UserState.DisplayName) { return; }

            //await _matchmakingSession.CompleteAsync(message.SystemProperties.LockToken);

            //await VerifyPlayReceivedSubscriptionExists();

            var gameId = message.UserProperties["gameId"].ToString();
            var opponentId = message.UserProperties["opponentId"].ToString();

            _logger.LogInformation($"Matchmaking request from {UserState.DisplayName} accepted by {opponentId}");

            var game = Games.FirstOrDefault(x=>x.Id == gameId);

            if (game != null)
            {
                game.MatchMade(opponentId);
                _logger.LogInformation("MatchMade");
                OnStateChanged?.Invoke(this, EventArgs.Empty);
                //await messageSession.CompleteAsync(message.SystemProperties.LockToken);
            }
            else
            {
                _logger.LogInformation("Skipping message for game {gameId} as we have no game associated with that id.", gameId);
                //Ignore old messages for games that don't exist anymore.
            }
            await messageSession.CompleteAsync(message.SystemProperties.LockToken);
        }

        private Task HandleMatchmakingError(ExceptionReceivedEventArgs arg)
        {
            //TODO: We probably need to error out whatever game this happened on.
            _logger.LogError("Error in matchmaking {0}", arg.Exception, arg.ExceptionReceivedContext);
            return Task.CompletedTask;
        }

        public async Task PlayShapeAsync(Shape shape)
        {
            if(CurrentGame == null) return;

            var playShapeMessage = new Message
            {
                ReplyToSessionId = UserState.DisplayName
            };

            playShapeMessage.Label = "playshape";
            playShapeMessage.UserProperties.Add("gameId", CurrentGame.Id);
            playShapeMessage.UserProperties.Add("gameready", false);
            playShapeMessage.Body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(shape));

            await _playTopic.SendAsync(playShapeMessage);
            CurrentGame.PlayMade = true;
        }

        public async ValueTask DisposeAsync()
        {
            await _playTopic?.CloseAsync();
            await _resultsSession?.CloseAsync();
            await _resultsSessionClient?.CloseAsync();
            await _resultsSubscription?.CloseAsync();
            await _matchmakingTopic?.CloseAsync();
            await _matchmakingSendTopic?.CloseAsync();
            await _matchmakingSessionClient?.CloseAsync();
            await _matchmakingClient?.CloseAsync();
        }

        private async Task VerifyPlayReceivedSubscriptionExists()
        {
            if (_alreadyVerified == true) await Task.FromResult(0);

            _managementClient = new ManagementClient(_configuration["AzureServiceBusConnectionString"]);

            if (!await _managementClient.TopicExistsAsync(_configuration["PlayTopic"]))
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
}