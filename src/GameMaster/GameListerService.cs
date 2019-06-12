using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;

namespace GameMaster
{
    public class GameListerService : GameLister.GameListerBase
    {
        public GameListerService(GameData gameData)
        {
            GameData = gameData;
        }

        public GameData GameData { get; }

        public override async Task<GameListReply> GetGameList(GameListRequest request, ServerCallContext context)
        {
            var games = await GameData.GetGamesForPlayer(request.Player);
            var reply = new GameListReply();
            foreach (var game in games)
            {
                //TODO: We should create an actual query for this
                //instead of pulling back the completed games.
                if (GameData.IsGameComplete(game)) continue;

                var newGameItem = new GameItem
                {
                    Id = game.GameId,
                    OpponentId = game.OpponentId, 
                    PlayerId = game.PlayerId,
                };

                foreach (var round in game.Rounds)
                {
                    if (round.OpponentShape.HasValue && round.PlayerShape.HasValue)
                    {
                        newGameItem.Rounds.Add(new RoundItem
                        {
                            Completed = round.Completed,
                            OpponentShape = round.OpponentShape.HasValue ? round.OpponentShape?.ToString() : "None",
                            PlayerShape = round.PlayerShape.HasValue ? round.PlayerShape?.ToString() : "None",
                            PlayerWins = round.PlayerWins,
                            Summary = round.Summary ?? ""
                        });
                    }
                }

                reply.Games.Add(newGameItem);
            }
            return reply;
        }
    }
}
