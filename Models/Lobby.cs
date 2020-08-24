using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DurakServer.Models
{
    public class Lobby
    {
        private readonly ConcurrentDictionary<string, Player> _players;
        public IEnumerable<Player> Players
        {
            get => _players.Values.ToList();
            set
            {
                foreach (var player in value)
                {
                    _players.TryAdd(player.Username, player);
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
            _players = new ConcurrentDictionary<string, Player>();
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
            _players.TryRemove(player.Username, out _);
        }

        public void ClearPlayers()
        {
            _players.Clear(); 
        }
    }
}
