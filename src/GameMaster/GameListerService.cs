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
            var results = await GameData.GetGamesForPlayer(request.Player);
            var reply = new GameListReply();
            foreach (var result in results)
            {
                reply.Games.Add(new GameItem
                {
                    GameId = result.GameId,
                    Opponent = result.OpponentId, 
                    Player = request.Player
                });
            }
            return reply;
        }
    }
}
