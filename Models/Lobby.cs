using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DurakServer.Models
{
    public class Lobby
    {
        public int Id { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public DeckBox DeckBox { get; }
        public River River { get; set; }
        public RolesCollection initialRoundRoles { get; set; }
        public RolesCollection winners { get; set; }
        public int EndAttackStep;
        public int EndAddingStep;
        public int PrevRiverCount;
        public bool TwoPlayersLeft;
        public string activeTimerPlayerUsername;
        public bool reactivateTimer;
        public Lobby()
        {
            Id = 0;
            DeckBox = new DeckBox();
            River = new River();
            initialRoundRoles = new RolesCollection();
            winners = new RolesCollection();
            EndAttackStep = 1;
            EndAddingStep = 1;
            PrevRiverCount = 0;
            TwoPlayersLeft = false;
            activeTimerPlayerUsername = "";
            reactivateTimer = false;
        }
    }
}
