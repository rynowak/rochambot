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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GameMaster
{
    public class GameMaster : IHostedService
    {
        private const string MatchMakingTopic = "MatchmakingTopic";
        private const string PlayTopic = "plays";
        private const string AzureServiceBusConnectionString = "AzureServiceBusConnectionString";
        private static string Name = nameof(GameMaster).ToLower();
        private readonly IConfiguration _configuration;
        private readonly ILogger<GameMaster> _logger;
        private readonly GameData _gameData;
        private ManagementClient _managementClient;
        private ISubscriptionClient _matchmakingSubscriptionClient;
        private SessionClient _matchmakingSessionClient;
        private ISubscriptionClient _playSubscriptionClient;

        private static List<Game> _games;

        public GameMaster(ILogger<GameMaster> logger, 
            IConfiguration configuration,
            GameData gameData)
        {
            _configuration = configuration;
            _logger = logger;
            _gameData = gameData;
            _games = new List<Game>();
        }

        public async Task StartAsync(CancellationToken token)
        {
            await VerifyMatchMakingSubscriptionExists();

            _matchmakingSubscriptionClient = new SubscriptionClient(
               _configuration[GameMaster.AzureServiceBusConnectionString],
               _configuration[GameMaster.MatchMakingTopic],
               "gamemaster");
            _matchmakingSessionClient = new SessionClient(
                _configuration[GameMaster.AzureServiceBusConnectionString],
                _configuration[GameMaster.MatchMakingTopic]);

            //_matchmakingSession = await _matchmakingSessionClient.AcceptMessageSessionAsync();
            _matchmakingSubscriptionClient.RegisterSessionHandler(OnMatchmakingMessageReceived, new SessionHandlerOptions(OnMessageHandlingException)
            {
                AutoComplete = true
            });

            _resultsTopic = new TopicClient(_configuration[GameMaster.AzureServiceBusConnectionString], "results");

            _playSubscriptionClient = new SubscriptionClient(_configuration[GameMaster.AzureServiceBusConnectionString], "plays", "gamemaster");
            _playSubscriptionClient.RegisterMessageHandler(HandlePlayMessage, new MessageHandlerOptions(HandlePlayError)
            {
                AutoComplete = true
            }); ;
        }

        private Task HandlePlayError(ExceptionReceivedEventArgs arg)
        {
            throw new NotImplementedException();
        }

        private object _lock = new object();
        private IMessageSession _matchmakingSession;
        private TopicClient _resultsTopic;

        private async Task HandlePlayMessage(Message message, CancellationToken arg3)
        {
            try
            {
                Message resultMessage1 = null;
                Message resultMessage2 = null;
                lock (_lock)
                {
                    var gameId = message.UserProperties["gameId"].ToString();
                    var game = _games.FirstOrDefault(x => x.GameId == gameId);
                    var playerId = message.ReplyToSessionId;
                    if (game == null)
                    {
                        throw new Exception("Out of order messages");
                    }

                    var round = game.Rounds.FirstOrDefault(x => x.RoundEnded == DateTime.MinValue);
                    if (round == null)
                    {
                        round = new Round();
                        game.Rounds.Add(round);
                    }

                    if (playerId == game.PlayerId)
                    {
                        round.PlayerShape = JsonSerializer.Parse<Shape>(message.Body);
                    }
                    else
                    {
                        round.OpponentShape = JsonSerializer.Parse<Shape>(message.Body);
                    }

                    if (_gameData.IsCurrentRoundComplete(game))
                    {
                        round.RoundEnded = DateTime.Now;
                        round = round.DetermineScore();

                        resultMessage1 = new Message
                        {
                            SessionId = game.PlayerId,
                            Body = JsonSerializer.ToBytes(round)
                        };
                        resultMessage1.UserProperties.Add("gameId", game.GameId);

                        if (_gameData.IsGameComplete(game))
                        {
                            resultMessage1.Label = "GameComplete";
                        }
                        else
                        {
                            resultMessage2 = new Message
                            {
                                SessionId = game.OpponentId,
                                Body = JsonSerializer.ToBytes(round)
                            };
                            resultMessage2.UserProperties.Add("gameId", game.GameId);
                        }
                    }
                }

                if (resultMessage1 != null)
                {
                    await _resultsTopic.SendAsync(resultMessage1);
                }

                if (resultMessage2 != null)
                {
                    await _resultsTopic.SendAsync(resultMessage2);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("error parsing play message.", ex);
            }
        }

        public async Task StopAsync(CancellationToken token)
        {
            await _matchmakingSessionClient?.CloseAsync();
            await _playSubscriptionClient?.CloseAsync();
            await _matchmakingSession?.CloseAsync();
            await _matchmakingSubscriptionClient?.CloseAsync();
            await _managementClient?.CloseAsync();
        }

        private Task OnMatchmakingMessageReceived(IMessageSession session, Message message, CancellationToken arg3)
        {
            try
            {
                lock (_lock)
                {
                    _logger.LogInformation($"Received message: {message.SystemProperties.SequenceNumber}");

                    var gameId = message.UserProperties["gameId"].ToString();
                    var opponentId = message.UserProperties["opponentId"].ToString();
                    var playerId = message.SessionId;

                    if (!string.IsNullOrEmpty(playerId))
                    {
                        Game game = null;

                        game = new Game
                        {
                            GameId = gameId,
                            PlayerId = playerId,
                            OpponentId = opponentId,
                            DateStarted = DateTime.UtcNow
                        };
                        _logger.LogInformation($"Game created for {playerId} to play {opponentId}");


                        //if (!(await _gameData.GameExists(playerId, gameId)))
                        //{
                        //    //game = await _gameData.CreateGame(playerId, gameId, opponentId);
                        //    game = new Game
                        //    {
                        //        GameId = gameId,
                        //        PlayerId = playerId,
                        //        OpponentId = opponentId,
                        //        DateStarted = DateTime.UtcNow
                        //    };
                        //    _logger.LogInformation($"Game created for {playerId} to play {opponentId}");
                        //}
                        //else
                        //{
                        //    //game = await _gameData.GetGame(playerId, gameId);
                        //    game = new Game
                        //    {
                        //        GameId = gameId,
                        //        PlayerId = playerId,
                        //        DateStarted = DateTime.UtcNow
                        //    };
                        //    _logger.LogInformation($"Game retrieved for {playerId} to play {opponentId}");
                        //}

                        _games.Add(game);
                    }
                }
                //await session.CompleteAsync(message.SystemProperties.LockToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unknown error in matchmaking", ex);
            }
            return Task.CompletedTask;
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