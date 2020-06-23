using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DurakServer.Models;
using DurakServer.Providers;
using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.AspNetCore.Connections.Features;

namespace DurakServer.Adapters
{
    public interface IDurakLobbyAdapter
    {
        Task CreateLobby(Player player);
        Lobby GetLobby(Player player);
        Task HandleTurn(Lobby lobby, Player player, Card card);
        Task HandleEndAttack(Lobby lobby, Player originalPlayer);
        Task HandleEndDefence(Lobby lobby);
        Task HandleEndAdding(Lobby lobby);
    }

    public class DurakLobbyAdapter : IDurakLobbyAdapter
    {
        private readonly IDurakLobbyProvider durakLobbyProvider;
        private static int _endAttackStep = 1;
        private static int _prevRiverCount = 0;
        public DurakLobbyAdapter(IDurakLobbyProvider durakLobbyProvider)
        {
            this.durakLobbyProvider = durakLobbyProvider;
        }
        public async Task CreateLobby(Player iPlayer)
        {
            if (durakLobbyProvider.WaitList.Count > 1)
            {
                var enemyPlayers = durakLobbyProvider.WaitList;
                //var enemy = durakLobbyProvider.WaitList.First();

                var players = new List<Player>
                {
                    iPlayer
                };
                players.AddRange(enemyPlayers);

                var lobby = GenerateLobby(players);

                await BroadcastLobbyAsync(lobby);
            }
            else
            {
                durakLobbyProvider.WaitList.Add(iPlayer);
            }
        }
        private Lobby GenerateLobby(List<Player> players)
        {
            var lobby = new Lobby
            {
                Players = players
            };

            foreach (var player in players)
            {
                FillHand(lobby.DeckBox, player);
            }

            durakLobbyProvider.Lobbies.Add(lobby);

            SetDurakRoles(players, lobby);

            return lobby;
        }
        private async Task BroadcastLobbyAsync(Lobby lobby)
        {
            foreach (var player in lobby.Players.ToList())
            {
                var lobbyReply = new LobbyReply();

                lobbyReply.IPlayer = new DurakNetPlayer
                {
                    Username = player.Username,
                    Role = player.Role
                };
                lobbyReply.IPlayer.Hand.AddRange(player.Hand);

                foreach (var enemy in lobby.Players)
                {
                    if (enemy.Username != player.Username)
                    {
                        lobbyReply.EnemyPlayers.Add(new DurakNetPlayer
                        {
                            Username = enemy.Username,
                            Role = enemy.Role,
                        });
                        lobbyReply.EnemyPlayers.FirstOrDefault(x => x.Username.Equals(enemy.Username)).Hand.AddRange(enemy.Hand);
                    }
                }

                // Проверить хэнды соперников
                lobbyReply.DeckBox.AddRange(lobby.DeckBox.ShuffledDeckList);
                
                lobbyReply.Trump = lobby.DeckBox.GetTrumpCard();

                await player.DurakStreamReply.WriteAsync(
                    new DurakReply
                    {
                        LobbyReply = lobbyReply
                    });
            }
        }
        private void SetDurakRoles(List<Player> players, Lobby lobby)
        {
            var trump = lobby.DeckBox.GetTrumpCard();

            var minRankTrump = Rank.None;
            minRankTrump = (from player in players from card in player.Hand where card.Suit == trump.Suit select card.Rank).Min();

            //Проверка
            if (minRankTrump == Rank.None)
            {
                players[0].Role = Role.Attacker;
                players[1].Role = Role.Defender;
                players[2].Role = Role.Waiter;
            } else
            {
                int defenderIndex = 0;
                int i = 0;
                foreach (var player in players)
                {
                    foreach (var card in player.Hand)
                    {
                        if (card.Suit == trump.Suit && card.Rank == minRankTrump)
                        {
                            player.Role = Role.Attacker;
                            defenderIndex = i+1;
                        }
                    }
                    i++;
                }

                if (defenderIndex < players.Count)
                {
                    players[defenderIndex].Role = Role.Defender;

                    if (defenderIndex + 1 < players.Count)
                        players[defenderIndex + 1].Role = Role.Waiter;
                    else
                        players[0].Role = Role.Waiter;

                } else if (defenderIndex >= players.Count)
                {
                    players[0].Role = Role.Defender;
                    players[1].Role = Role.Waiter;
                }
            }

            foreach (var player in players)
            {
                if (player.Role != Role.Attacker && player.Role != Role.Defender && player.Role != Role.Waiter)
                {
                    player.Role = Role.Inactive;
                }
            }
        }
        private void FillHand(DeckBox deckBox, Player player)
        {
            // Заполнение руки
            for (int cards = player.Hand.Count; cards < 6; cards++)
            {
                if (deckBox.ShuffledDeckList.Count == 0) break;
                var card = deckBox.DrawCardFromShuf();
                player.Hand.Add(card);
            }
        }
        public async Task HandleTurn(Lobby lobby, Player player, Card card)
        {
                var reply = new DurakReply
                {
                    TurnReply = new TurnReply { Card = card }
                };

                foreach (var somePlayer in lobby.Players)
                {
                    if (somePlayer.Hand.Contains(card) && somePlayer.Role == Role.Attacker)
                    {
                        lobby.River.Attacker.Add(card);
                    } else if (somePlayer.Hand.Contains(card) && somePlayer.Role == Role.Defender)
                    {
                        lobby.River.Defender.Add(card);
                    } else if (somePlayer.Hand.Contains(card) && somePlayer.Role == Role.Adder)
                    {
                        lobby.River.Adder.Add(card);
                    }
                }

                player.Hand.Remove(card);

                if (_endAttackStep == 3)
                {
                    await HandleEndAttack(lobby, player);
                }

                foreach (var somePlayer in lobby.Players)
                {
                    await somePlayer.DurakStreamReply.WriteAsync(reply);
                }

                //foreach (var somePlayer in lobby.Players)
                //{
                //        switch (somePlayer.Role)
                //        {
                //            case Role.Attacker:
                //                {
                //                    if (lobby.River.Attacker.Count == lobby.River.Defender.Count &&
                //                        lobby.River.Attacker.Count > 0)
                //                    {
                //                        reply.TurnReply.Status = Status.CanAttack;
                //                        await somePlayer.DurakStreamReply.WriteAsync(reply);
                //                    }
                //                    else if (lobby.River.Attacker.Count > lobby.River.Defender.Count)
                //                    {
                //                        reply.TurnReply.Status = Status.CanNothing;
                //                        await somePlayer.DurakStreamReply.WriteAsync(reply);
                //                    }
                //                }
                //                break;
                //            case Role.Defender:
                //                {
                //                    if (lobby.River.Attacker.Count > lobby.River.Defender.Count)
                //                    {
                //                        reply.TurnReply.Status = Status.CanDefence;
                //                        await somePlayer.DurakStreamReply.WriteAsync(reply);
                //                    }
                //                    else if (lobby.River.Attacker.Count == lobby.River.Defender.Count || lobby.River.Adder.Count > 0)
                //                    {
                //                        reply.TurnReply.Status = Status.CanNothing;
                //                        await somePlayer.DurakStreamReply.WriteAsync(reply);
                //                    } 
                //                }
                //                break;
                //            case Role.Adder:
                //            {
                //                reply.TurnReply.Status = Status.CanPass;
                //                await somePlayer.DurakStreamReply.WriteAsync(reply);
                //            }
                //                break;
                //            case Role.Waiter:
                //                {

                //                }
                //                break;
                //        }
                //}
        }
        public async Task HandleEndAttack(Lobby lobby, Player originalPlayer)
        {
            switch (_endAttackStep)
            {
                case 1:
                    {
                        foreach (var player in lobby.Players)
                        {
                            switch (player.Role)
                            {
                                case Role.Attacker:
                                    player.Role = Role.FormerAttacker;
                                    break;
                                case Role.Waiter:
                                    player.Role = Role.Attacker;
                                    break;
                            }
                        }

                        _prevRiverCount = lobby.River.Defender.Count + lobby.River.Attacker.Count;
                        _endAttackStep = 2;
                    }
                    break;
                case 2:
                    {
                        if (_prevRiverCount == lobby.River.Defender.Count + lobby.River.Attacker.Count)
                        {
                            foreach (var player in lobby.Players)
                            {
                                FillHand(lobby.DeckBox, player);
                                switch (player.Role)
                                {
                                    case Role.FormerAttacker:
                                        player.Role = Role.Waiter;
                                        break;
                                    case Role.Defender:
                                        player.Role = Role.Attacker;
                                        break;
                                    case Role.Attacker:
                                        player.Role = Role.Defender;
                                        break;
                                }
                            }

                            lobby.River.Attacker.Clear();
                            lobby.River.Defender.Clear();
                            _endAttackStep = 1;
                        }
                        else
                        {
                            foreach (var player in lobby.Players)
                            {
                                switch (player.Role)
                                {
                                    case Role.FormerAttacker:
                                        player.Role = Role.Attacker;
                                        break;
                                    case Role.Attacker:
                                        player.Role = Role.FormerAttacker;
                                        break;
                                }
                            }

                            _prevRiverCount = lobby.River.Defender.Count + lobby.River.Attacker.Count;
                            _endAttackStep = 3;
                        }
                    }
                    break;
                // Case 3 is handled in HandleTurn method since we need to wait of throwing one card from new attacker
                case 3:
                    if (_prevRiverCount == lobby.River.Defender.Count + lobby.River.Attacker.Count)
                    {
                        foreach (var player in lobby.Players)
                        {
                            FillHand(lobby.DeckBox, player);
                            switch (player.Role)
                            {
                                case Role.FormerAttacker:
                                    player.Role = Role.Defender;
                                    break;
                                case Role.Defender:
                                    player.Role = Role.Attacker;
                                    break;
                                case Role.Attacker:
                                    player.Role = Role.Waiter;
                                    break;
                            }
                        }

                        lobby.River.Attacker.Clear();
                        lobby.River.Defender.Clear();
                        _endAttackStep = 1;
                    }
                    else
                    {
                        foreach (var player in lobby.Players)
                        {
                            switch (player.Role)
                            {
                                case Role.FormerAttacker:
                                    player.Role = Role.Attacker;
                                    break;
                            }
                        }
                        _endAttackStep = 4;
                    }
                    break;
                case 4:
                {
                    originalPlayer.Role = Role.FormerAttacker;
                    _endAttackStep = 5;
                }
                    break;
                case 5:
                    {
                        foreach (var player in lobby.Players)
                        {
                            FillHand(lobby.DeckBox, player);
                            switch (player.Role)
                            {
                                case Role.Attacker:
                                    player.Role = Role.Defender;
                                    break;
                                case Role.FormerAttacker:
                                    player.Role = Role.Waiter;
                                    break;
                                case Role.Defender:
                                    player.Role = Role.Attacker;
                                    break;
                            }
                        }

                        lobby.River.Attacker.Clear();
                        lobby.River.Defender.Clear();
                        _endAttackStep = 1;
                    }
                    break;
            }

            foreach (var player in lobby.Players)
            {
                var reply = new DurakReply
                {
                    EndAttackReply = new EndAttackReply
                    {
                        IPlayer = new DurakNetPlayer
                        {
                            Role = player.Role,
                            Username = player.Username,
                        },
                    }
                };
                reply.EndAttackReply.IPlayer.Hand.AddRange(player.Hand);

                foreach (var enemyPlayer in lobby.Players)
                {
                    if (enemyPlayer.Username != player.Username)
                    {
                        DurakNetPlayer EnemyPlayer = new DurakNetPlayer
                        {
                            Role = enemyPlayer.Role,
                            Username = enemyPlayer.Username
                        };
                        EnemyPlayer.Hand.AddRange(enemyPlayer.Hand);
                        reply.EndAttackReply.EnemyPlayers.Add(EnemyPlayer);
                    }
                }

                await player.DurakStreamReply.WriteAsync(reply);
            }
        }
        public async Task HandleEndDefence(Lobby lobby)
        {
            foreach (var player in lobby.Players)
            {
                if (player.Role == Role.Attacker)
                {
                    player.Role = Role.Adder;
                }
            }

            foreach (var player in lobby.Players)
            {
                var enemyPlayer = lobby.Players.FirstOrDefault(x => !x.Username.Equals(player.Username));

                var reply = new DurakReply
                {
                    EndDefenceReply = new EndDefenceReply
                    {
                        IPlayer = new DurakNetPlayer
                        {
                            Role = player.Role,
                            Username = player.Username,
                        },
                        EnemyPlayer = new DurakNetPlayer
                        {
                            Role = enemyPlayer.Role,
                            Username = enemyPlayer.Username,
                        }
                    }
                };

                reply.EndDefenceReply.IPlayer.Hand.AddRange(player.Hand);
                reply.EndDefenceReply.EnemyPlayer.Hand.AddRange(enemyPlayer.Hand);

                await player.DurakStreamReply.WriteAsync(reply);
            }
        }
        public async Task HandleEndAdding(Lobby lobby)
        {
            foreach (var player in lobby.Players)
            {
                if (player.Role == Role.Defender)
                {
                    player.Hand.AddRange(lobby.River.Attacker);
                    player.Hand.AddRange(lobby.River.Defender);
                    player.Hand.AddRange(lobby.River.Adder);
                }

                if (player.Role == Role.Adder)
                {
                    player.Role = Role.Attacker;
                }
                FillHand(lobby.DeckBox, player);
            }

            lobby.River.Attacker.Clear();
            lobby.River.Defender.Clear();
            lobby.River.Adder.Clear();

            foreach (var player in lobby.Players)
            {
                var enemyPlayer = lobby.Players.FirstOrDefault(x => !x.Username.Equals(player.Username));

                Debug.Assert(enemyPlayer != null, nameof(enemyPlayer) + " != null");

                var reply = new DurakReply
                {
                    EndAddingReply = new EndAddingReply
                    {
                        IPlayer = new DurakNetPlayer
                        {
                            Username = player.Username,
                            Role = player.Role
                        },
                        EnemyPlayer = new DurakNetPlayer
                        {
                            Username = enemyPlayer.Username,
                            Role = enemyPlayer.Role
                        }
                    }
                };

                reply.EndAddingReply.IPlayer.Hand.AddRange(player.Hand);
                reply.EndAddingReply.EnemyPlayer.Hand.AddRange(enemyPlayer.Hand);

                await player.DurakStreamReply.WriteAsync(reply);
            }
        }
        public Lobby GetLobby(Player player) => durakLobbyProvider.Lobbies.FirstOrDefault(x => x.Players.Contains(player));
    }
}