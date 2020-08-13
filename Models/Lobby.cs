using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DurakServer.Models
{
    public class Lobby
    {
        private ConcurrentDictionary<string, Player> players;
        public IEnumerable<Player> Players
        {
            get
            {
                return players.Values;
            }
            set
            {
                foreach (var player in value)
                {
                    players.TryAdd(player.Username, player);
                }
            }
        }
        public Lobby()
        {
            Id = 0;
            DeckBox = new DeckBox();
            River = new River();
            InitialRoundRoles = new RolesCollection();
            EndAttackStep = 1;
            EndAddingStep = 1;
            PrevRiverCount = 0;
            TwoPlayersLeft = false;
            ReactivateTimer = false;
            players = new ConcurrentDictionary<string, Player>();
        }

        public int Id { get; set; }
        public DeckBox DeckBox { get; }
        public River River { get; set; }
        public RolesCollection InitialRoundRoles { get; set; }
        public int EndAttackStep;
        public int EndAddingStep;
        public int PrevRiverCount;
        public bool TwoPlayersLeft;
        public Player ActiveTimerPlayer;
        public bool ReactivateTimer;

        public void RemovePlayer(Player player)
        {
            players.TryRemove(player.Username, out _);
        }

        public void ClearPlayers()
        {
            players.Clear(); 
        }
    }
}
