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
        public Lobby()
        {
            DeckBox = new DeckBox();
            River = new River();
        }
    }
}
