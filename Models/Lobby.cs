using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DurakServer.Models
{
    public class Lobby
    {
        public List<Player> Players { get; set; } = new List<Player>();
        public DeckBox DeckBox { get; }
        public River River { get; set; }
        public RolesCollection initialRoundRoles { get; set; }
        public int EndAttackStep;
        public int EndAddingStep;
        public int PrevRiverCount;
        public bool TwoPlayersLeft;
        public Lobby()
        {
            DeckBox = new DeckBox();
            River = new River();
            initialRoundRoles = new RolesCollection();
            EndAttackStep = 1;
            EndAddingStep = 1;
            PrevRiverCount = 0;
            TwoPlayersLeft = false;
        }
    }
}
