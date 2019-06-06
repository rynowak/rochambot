using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameList;
using Grpc.Core;

namespace CurrentGames
{
    public class GameListerService : GameLister.GameListerBase
    {
        public override Task<GameListReply> GetGameList(GameListRequest request, ServerCallContext context)
        {
            return Task.FromResult(new GameListReply
            {
            });
        }
    }
}