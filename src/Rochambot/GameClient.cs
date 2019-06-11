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
using Microsoft.FeatureManagement;
using System.Collections.Concurrent;
using System.Collections;
using System.Security.Cryptography.Xml;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Rest.Serialization;
using System.Runtime.InteropServices;
using Grpc.Core;
using GameMaster;

namespace Rochambot
{
    public class GameClient : IAsyncDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GameClient> _logger;
        private readonly ITopicClient _matchmakingTopic;
        private readonly ITopicClient _matchmakingSendTopic;
        private readonly ITopicClient _playTopic;
        private readonly SessionClient _resultsSessionClient;
        private readonly ISubscriptionClient _matchmakingClient;
        private readonly ISessionClient _matchmakingSessionClient;
        private IMessageSession _matchmakingSession;
        private ManagementClient _managementClient;
        private bool _alreadyVerified;
        private IMessageSession _resultsSession;
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private Task _resultsTask;
        private Task _matchmakingTask;

        public Channel Channel { get; private set; }
        public GameLister.GameListerClient GameListerClient { get; private set; }

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

            _resultsSessionClient = new SessionClient(_configuration["AzureServiceBusConnectionString"], EntityNameHelper.FormatSubscriptionPath("results", "players"));
            Games = new List<Game>();
        }

        public IList<Game> Games { get; }
        public Game CurrentGame { get; set; }
        public Opponent Opponent { get; private set; }
        public UserState UserState { get; set; }
        public event EventHandler OnStateChanged;

        public async Task SetPlayerId(UserState userState)
        {
            if (UserState == null || (!UserState.DisplayName.Equals(userState.DisplayName)))
            {
                UserState = userState;
                _logger.LogInformation($"User {UserState.DisplayName} logged in");

                _resultsSession = await _resultsSessionClient.AcceptMessageSessionAsync(userState.DisplayName);
                _matchmakingSession = await _matchmakingSessionClient.AcceptMessageSessionAsync(userState.DisplayName);

                _logger.LogInformation($"Set up subscription session with session id {userState.DisplayName}");
                OnStateChanged?.Invoke(this, EventArgs.Empty);
                _resultsTask = HandleResults();
                _matchmakingTask = HandleMatchmaking();

                if(Channel != null)
                {
                    await Channel.ShutdownAsync();
                }
                Channel = new Channel(_configuration["GameMasterUri"], ChannelCredentials.Insecure);
                GameListerClient = new GameMaster.GameLister.GameListerClient(Channel);
                var games = await GameListerClient.GetGameListAsync(new GameListRequest
                {
                    Player = UserState.DisplayName
                });

                foreach (var game in games.Games)
                {
                    var newGame = new Game(game.Id)
                    {
                        CurrentStatus = "Loaded.",
                        GameOver = game.GameOver,
                        Id = game.Id,
                        OpponentId = game.OpponentId,
                        ReadyToPlay = true,
                    };
                    foreach (var round in game.Rounds)
                    {
                        newGame.Rounds.Add(new Round()
                        {
                            Completed = round.Completed,
                            OpponentShape = Enum.Parse<Shape>(round.OpponentShape),
                            PlayerShape = Enum.Parse<Shape>(round.PlayerShape),
                            PlayerWins = round.PlayerWins,
                            Summary = round.Summary
                        });
                    }
                    Games.Add(newGame);
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private async Task HandleMatchmaking()
        {
            var token = _stoppingCts.Token;
            while (!token.IsCancellationRequested)
            {
                var message = await _matchmakingSession.ReceiveAsync(TimeSpan.FromSeconds(5));
                if (message is null) continue;
                await HandleMatchmakingMessage(message);
                await _matchmakingSession.CompleteAsync(message.SystemProperties.LockToken);
            }
        }

        private async Task HandleResults()
        {
            var token = _stoppingCts.Token;
            while (!token.IsCancellationRequested)
            {
                var message = await _resultsSession.ReceiveAsync(TimeSpan.FromSeconds(5));
                if (message is null) continue;
                await HandleResultsMessage(message);
                await _resultsSession.CompleteAsync(message.SystemProperties.LockToken);
            }
        }

        private Task HandleMatchmakingMessage(Message message)
        {
            var gameId = message.UserProperties["gameId"].ToString();
            var opponentId = message.UserProperties["opponentId"].ToString();

            var game = Games.FirstOrDefault(x => x.Id == gameId);

            if (game != null)
            {
                _logger.LogInformation($"Matchmaking request from {UserState.DisplayName} accepted by {game.OpponentId}");

                game.MatchMade(opponentId);
                _logger.LogInformation("MatchMade");
                OnStateChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _logger.LogInformation("Skipping message for game {gameId} as we have no game associated with that id.", game.Id);
            }
            return Task.CompletedTask;
        }

        private Task HandleResultsMessage(Message message)
        {
            try
            {
                _logger.LogInformation("Results message arrived.");
                var gameId = message.UserProperties["gameId"].ToString();
                var game = Games.Single(x => x.Id == gameId);
                var round = JsonConvert.DeserializeObject<Round>(Encoding.UTF8.GetString(message.Body));
                game.Rounds.Add(round);
                game.PlayMade = false;

                game.GameOver = message.UserProperties.ContainsKey("Complete");

                OnStateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling result {ex}", ex);
            }
            return Task.CompletedTask;
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

            await _matchmakingSendTopic.SendAsync(gameStartMessage);

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
            _stoppingCts.Cancel();
            await _playTopic?.CloseAsync();
            await _resultsSession?.CloseAsync();
            await _resultsSessionClient?.CloseAsync();
            await _matchmakingTopic?.CloseAsync();
            await _matchmakingSendTopic?.CloseAsync();
            await _matchmakingSessionClient?.CloseAsync();
            await _matchmakingSession?.CloseAsync();
            await _matchmakingClient?.CloseAsync();
            await _resultsTask;
            await _matchmakingTask;
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