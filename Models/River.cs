using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DurakServer.Models
{
    public class River
    {
        public List<Card> Attacker;
        public List<Card> Defender;
        public List<Card> Adder;
        public River()
        {
            Attacker = new List<Card>();
            Defender = new List<Card>();
            Adder = new List<Card>();
        }
    }
}
