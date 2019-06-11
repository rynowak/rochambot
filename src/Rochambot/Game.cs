
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;

namespace Rochambot
{
    public class Game
    {
        public Game(string gameId)
        {
            Id = gameId;
            Rounds = new List<Round>();
        }

        public string Id { get; }
        public string OpponentId { get; private set; }

        //TODO: Should prob collapse Status into enum instead of string with bools.
        public string CurrentStatus { get; private set; }
        public bool ReadyToPlay { get; set; }
        public bool PlayMade { get; set; }

        public List<Round> Rounds { get; set; }

        public bool GameOver { get; set; }

        public bool Playable
        {
            get
            {
                return !(ReadyToPlay || GameOver);
            }
        }

        public void MatchMade(string opponentId)
        {
            OpponentId = opponentId;
            SetStatus("Ready to start");
            ReadyToPlay = true;
        }

        internal void SetStatus(string status)
        {
            CurrentStatus = status;
        }
    }

    public class Round
    {
        public Shape? PlayerShape { get; set; }
        public Shape? OpponentShape { get; set; }
        public string Summary { get; set; }
        //public DateTime RoundStarted { get; set; }
        //public DateTime RoundEnded { get; set; }
        public bool PlayerWins { get; set; }
    }
}