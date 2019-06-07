using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Rochambot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace GameMaster
{
    public class GameData
    {
        private readonly IConfiguration _configuration;
        private readonly CosmosClient _cosmosClient;
        private readonly CosmosContainer _gamesContainer;

        public GameData(IConfiguration configuration)
        {
            _configuration = configuration;
            _cosmosClient = new CosmosClient(_configuration["CosmosEndpointUri"], _configuration["CosmosAccountKey"]);
            _gamesContainer = _cosmosClient.Databases["Rochambot"].Containers["Games"];
        }

        public async Task<bool> GameExists(string playerId, string gameId)
        {
            var response = await _gamesContainer.Items.ReadItemAsync<Game>(playerId, gameId);
            return response.StatusCode == HttpStatusCode.Found;
        }

        public async Task<Game> CreateGame(string playerId, string gameId, string opponentId)
        {
            await _gamesContainer.Items.CreateItemAsync<Game>(playerId, new Game 
            { 
                GameId = gameId, 
                PlayerId = playerId, 
                OpponentId = opponentId, 
                DateStarted = DateTime.UtcNow
            });
            return await GetGame(playerId, gameId);
        }

        public async Task<Game> GetGame(string playerId, string gameId)
        {
            var game = await _gamesContainer.Items.ReadItemAsync<Game>(playerId, gameId);
            return game.Resource;
        }

        public bool IsGameComplete(Game game)
        {
            if(game.Rounds == null || game.Rounds.Count() == 0) return false;

            var playerWins = game.Rounds.Where(x => x.PlayerWins).Count();
            var opponentWins = game.Rounds.Where(x => !x.PlayerWins).Count();

            return (playerWins >= game.NumberOfTurnsNeededToWin) || (opponentWins >= game.NumberOfTurnsNeededToWin);
        }

        public bool IsCurrentRoundComplete(Game game)
        {
            if(game.Rounds == null || !game.Rounds.Any()) return true; // this is a new game, start a new turn
            return (game.Rounds.Last().PlayerShape.HasValue && game.Rounds.Last().OpponentShape.HasValue);
        }

        public async Task<Game> PlayerTurn(string playerId, string gameId, Shape shape)
        {
            var game = await GetGame(playerId, gameId);

            if(IsCurrentRoundComplete(game))
            {
                game.Rounds.Add(new Round 
                {
                    RoundStarted = DateTime.UtcNow
                });
            }

            game.Rounds.Last().PlayerShape = shape;

            await _gamesContainer.Items.ReplaceItemAsync<Game>(playerId, gameId, game);
            return game;
        }

        public async Task<Game> OpponentTurn(string playerId, string gameId, Shape shape)
        {
            var game = await GetGame(playerId, gameId);

            if(IsCurrentRoundComplete(game))
            {
                game.Rounds.Add(new Round 
                {
                    RoundStarted = DateTime.UtcNow
                });
            }

            game.Rounds.Last().OpponentShape = shape;

            await _gamesContainer.Items.ReplaceItemAsync<Game>(playerId, gameId, game);
            return game;
        }

        public async Task<Game> SaveScore(string playerId, string gameId)
        {
            var game = await GetGame(playerId, gameId);
            
            game.Rounds.Last().DetermineScore();

            if(IsGameComplete(game))
            {
                var playerWins = game.Rounds.Where(x => x.PlayerWins).Count();
                var opponentWins = game.Rounds.Where(x => !x.PlayerWins).Count();
                if(playerWins > opponentWins) game.Winner = game.PlayerId;
                else game.Winner = game.OpponentId;
            }

            await _gamesContainer.Items.ReplaceItemAsync<Game>(playerId, gameId, game);
            return game;
        }

        public async Task<IEnumerable<Game>> GetGamesForPlayer(string playerId)
        {
            var sqlQueryText = "SELECT * FROM g WHERE g.PlayerId = '" + playerId + "'";

            CosmosSqlQueryDefinition queryDefinition = new CosmosSqlQueryDefinition(sqlQueryText);
            CosmosResultSetIterator<Game> queryResultSetIterator = 
                _gamesContainer.Items.CreateItemQuery<Game>(queryDefinition, playerId);

            List<Game> games = new List<Game>();

            while (queryResultSetIterator.HasMoreResults)
            {
                CosmosQueryResponse<Game> currentResultSet = await queryResultSetIterator.FetchNextSetAsync();
                foreach (Game game in currentResultSet)
                {
                    Console.WriteLine("\tRead {0}\n", game);
                    games.Add(game);
                }
            }

            return games;
        }
    }
}