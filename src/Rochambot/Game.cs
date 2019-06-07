
using System;
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
        }

        public string Id { get; }
        public string OpponentId { get; private set; }
        public string CurrentStatus { get; private set; }

        public void MatchMade(string opponentId)
        {
            OpponentId = opponentId;
            SetStatus("Ready to start");
        }

        internal void SetStatus(string status)
        {
            CurrentStatus = status;
        }
    }
}