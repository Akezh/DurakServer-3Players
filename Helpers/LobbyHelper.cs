using DurakServer.Models;
using DurakServer.Providers;
using Grpc.Core;
using System;
using System.Linq;

namespace DurakServer.Helpers
{
    public static class LobbyHelper
    {
        private static Tuple<bool, Lobby> CheckLobby(Player player, IDurakLobbyProvider durakLobbyProvider)
        {
            if (player == null) return new Tuple<bool, Lobby>(false, null);

            var lobby = GetLobby(player, durakLobbyProvider);
            if (lobby == null) return new Tuple<bool, Lobby>(false, null);
            return new Tuple<bool, Lobby>(true, lobby);
        }

        public static Lobby GetLobby(Player player, IDurakLobbyProvider durakLobbyProvider) => durakLobbyProvider
           .Lobbies
           .ToArray()
           .FirstOrDefault(x => x.Players.Contains(player));
        public static Lobby GetLobby(int id, IDurakLobbyProvider durakLobbyProvider) => durakLobbyProvider
            .Lobbies
            .ToArray()
            .FirstOrDefault(x => x.Id.Equals(id));

        public static Lobby HandleThreadSafeLobby(Player player, IDurakLobbyProvider durakLobbyProvider)
        {
            var tuple = CheckLobby(player, durakLobbyProvider);

            if (tuple.Item1)
            {
                return tuple.Item2;
            }
            else
            {
                throw new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найдено"));
            }
        }
    }
}
