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

namespace Rochambot
{
    public class GameClient : IAsyncDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GameClient> _logger;

        private readonly ITopicClient _matchmakingTopic;
        private readonly ISubscriptionClient _matchmakingClient;
        private readonly ISessionClient _matchmakingSessionClient;

        private string _id = Guid.NewGuid().ToString();

        private IMessageSession _matchmakingSession;

        public GameClient(IConfiguration configuration,
                          ILogger<GameClient> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _matchmakingTopic = new TopicClient(_configuration["AzureServiceBusConnectionString"], "matchmaking");
            _matchmakingClient = new SubscriptionClient(_configuration["AzureServiceBusConnectionString"], "matchmaking", "webapp");
            _matchmakingSessionClient = new SessionClient(_configuration["AzureServiceBusConnectionString"], EntityNameHelper.FormatSubscriptionPath("matchmaking", "webapp"));

            Games = new List<Game>();
        }

        public IList<Game> Games {get;}
        public Game CurrentGame {get;}
        public Opponent Opponent { get; private set; }

        public event EventHandler OnStateChanged;

        public async Task SetPlayerId()
        {
            _matchmakingSession = await _matchmakingSessionClient.AcceptMessageSessionAsync(_id);
            _matchmakingClient.RegisterSessionHandler(HandleMatchmakingMessage, HandleMatchmakingError);
        }

        public async Task RequestGameAsync()
        {

            var gameId = Guid.NewGuid().ToString();

            _logger.LogInformation("Sending matchmaker message.");

            var gameStartMessage = new Message
            {
                ReplyToSessionId = _id
            };

            gameStartMessage.Label = "gamerequest";
            gameStartMessage.UserProperties.Add("gameId", gameId);

            await _matchmakingTopic.SendAsync(gameStartMessage);

            var game = new Game(gameId);
            this.Games.Add(game);

            //var gameData = await _session.ReceiveAsync();

            //Opponent = new Opponent(gameData.ReplyToSessionId);

            //await _session.CompleteAsync(gameData.SystemProperties.LockToken);
        }

        private async Task HandleMatchmakingMessage(IMessageSession messageSession, Message message, CancellationToken cancellationToken)
        {
            var gameId = message.UserProperties["gameId"].ToString();
            var oponentId = message.UserProperties["oponentId"].ToString();

            var game = Games.Single(x => x.Id == gameId);
            game.MatchMade(oponentId);
            _logger.LogInformation("MatchMade");
            //await messageSession.CompleteAsync(message.SystemProperties.LockToken);
            OnStateChanged?.Invoke(this, EventArgs.Empty);
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
            await _matchmakingSessionClient?.CloseAsync();
            await _matchmakingClient?.CloseAsync();
        }
    }
}