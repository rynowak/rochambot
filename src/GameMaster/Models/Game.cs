using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameMaster
{
    public class Game
    {
        public Game()
        {
            Rounds = new List<Round>();
        }

        [JsonProperty(PropertyName = "id")]
        public string GameId { get; set; }
        public int NumberOfTurnsNeededToWin { get; set; } = 3; // default is best-to-3
        public List<Round> Rounds { get; set; }
        public string Winner { get; set; }
        [JsonProperty(PropertyName="player")]
        public string PlayerId { get; set; }
        [JsonProperty(PropertyName="opponent")]
        public string OpponentId { get; set; }
        public DateTime DateStarted { get; set; }
    }
}