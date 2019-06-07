using System;
using Rochambot;

namespace GameMaster
{
    public class Round
    {
        public Shape? PlayerShape { get; set; }
        public Shape? OpponentShape { get; set; }
        public string Summary { get; set; }
        public DateTime RoundStarted { get; set; }
        public DateTime RoundEnded { get; set; }
        public bool PlayerWins { get; set; }
    }
}