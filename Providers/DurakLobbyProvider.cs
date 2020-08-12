using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DurakServer.Models;

namespace DurakServer.Providers
{
    public interface IDurakLobbyProvider
    {
        BlockingCollection<Lobby> Lobbies { get; }
        List<Player> WaitList { get; }
    }

    public class DurakLobbyProvider : IDurakLobbyProvider
    {
        public DurakLobbyProvider()
        {
            Lobbies = new BlockingCollection<Lobby>();
            WaitList = new List<Player>();
        }

        public BlockingCollection<Lobby> Lobbies { get; }
        public List<Player> WaitList { get; }
    }
}
