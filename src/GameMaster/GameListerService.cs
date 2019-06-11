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
                var newGameItem = new GameItem
                {
                    Id = game.GameId,
                    OpponentId = game.OpponentId, 
                    PlayerId = game.PlayerId,
                };

                foreach (var round in game.Rounds)
                {
                    newGameItem.Rounds.Add(new RoundItem
                    {
                        Completed = round.Completed,
                        OpponentShape = round.OpponentShape.HasValue ? round.OpponentShape.Value.ToString() : null,
                        PlayerShape = round.PlayerShape.HasValue ? round.PlayerShape.Value.ToString() : null,
                        PlayerWins = round.PlayerWins,
                        Summary = round.Summary
                    });
                }

                reply.Games.Add(newGameItem);
            }
            return reply;
        }
    }
}
