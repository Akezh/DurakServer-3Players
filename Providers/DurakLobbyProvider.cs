using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DurakServer.Models;

namespace DurakServer.Providers
{
    public interface IDurakLobbyProvider
    {
        List<Lobby> Lobbies { get; }
        List<Player> WaitList { get; }
    }

    public class DurakLobbyProvider : IDurakLobbyProvider
    {
        public DurakLobbyProvider()
        {
            Lobbies = new List<Lobby>();
            WaitList = new List<Player>();
        }

        public List<Lobby> Lobbies { get; }
        public List<Player> WaitList { get; }
    }
}
