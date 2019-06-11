
using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rochambot;

namespace RobbyBot
{
    public class MoveMaker
    {
        private IConfiguration _configuration;
        private string _botId;
        private ILogger<MoveMaker> _logger;
        private TopicClient _playTopicClient;

        static readonly Random Random = new Random((int)DateTime.Now.Ticks);

        public MoveMaker(IConfiguration config, ILogger<MoveMaker> logger)
        {
            _configuration = config;
            _botId = config["botId"];
            _logger = logger;
            _playTopicClient = new TopicClient(_configuration["AzureServiceBusConnectionString"], _configuration["PlayTopic"]);
        }
        public async Task MakeMove(string gameId)
        {
            var shape = (Shape)Random.Next(1,3);
            var playMessage = new Message{
                Label = "playshape",
                ReplyToSessionId = _botId
            };
            _logger.LogInformation($"Making Move {shape}");
            playMessage.UserProperties.Add("gameId", gameId);
            playMessage.Body = JsonSerializer.ToBytes(shape);
            await _playTopicClient.SendAsync(playMessage);
        }
    }
}