using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace GameMaster
{
    [ApiController]
    [Route("/game")]
    public class GameController : ControllerBase
    {
        private static ConcurrentDictionary<string, GameState> _games = new ConcurrentDictionary<string, GameState>();

        [HttpGet("{gameId}")]
        public async Task<ActionResult<GameState>> GetGameAsync(string gameId)
        {
            await Task.Yield();

            _games.TryGetValue(gameId, out var game);
            if (game == null)
            {
                return NotFound();
            }

            return game;
        }

        [HttpPut]
        public async Task<ActionResult<string>> CreateGameAsync([FromBody] UserInfo[] players)
        {
            await Task.Yield();

            var gameId = Guid.NewGuid().ToString();
            var gameState = new GameState()
            {
                GameId = gameId,
                Players = players,
                Moves = new List<PlayerMove>(),
            };

            _games.TryAdd(gameId, gameState);
            return "\"" + gameId + "\"";
        }

        [HttpPost("{gameId}")]
        public async Task<ActionResult<GameState>> PlayAsync(string gameId, PlayerMove move)
        {
            await Task.Yield();

            _games.TryGetValue(gameId, out var game);
            if (game == null)
            {
                return NotFound();
            }

            lock (game)
            {
                if (!game.Players.Any(p => p.Username == move.Player.Username))
                {
                    return BadRequest("Player is not part of this game.");
                }

                if (game.Moves.Any(p => p.Player.Username == move.Player.Username))
                {
                    return BadRequest("Player has already made a move.");
                }

                game.Moves.Add(move);
                if (game.IsComplete)
                {
                    var (shape0, shape1) = (game.Moves[0].Move, game.Moves[1].Move);
                    if (shape0 == shape1)
                    {
                        // Draw
                    } 
                    else if ((((int)shape0 - (int)shape1) % 3) == 2)
                    {
                        // Player0 wins!
                        game.Winner = game.Players[0];
                    }
                    else
                    {
                        // Player1 wins!
                        game.Winner = game.Players[1];
                    }
                }

                return game;
            }
        }
    }
}