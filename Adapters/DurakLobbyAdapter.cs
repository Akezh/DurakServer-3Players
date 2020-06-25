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
        Task HandleFinishGameRound(Lobby lobby);
    }

    public class DurakLobbyAdapter : IDurakLobbyAdapter
    {
        private readonly IDurakLobbyProvider durakLobbyProvider;
        private static int _endAttackStep = 1;
        private static int _endAddingStep = 1;
        private static int _prevRiverCount = 0;
        private static Dictionary<string, Role> initialRoundRoles = new Dictionary<string, Role>(); // key - username, value - role, Считываем начальные роли игроков, чтобы в конце раунда присвоить им правильные значения.
        //private static pair
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
        private void UpdateDurakRoles(Lobby lobby, bool areCardsBeatenSuccessfully)
        {
            if (areCardsBeatenSuccessfully)
            {
                // The defence was successful since all 12 cards were in the river: attacker -> waiter, defender -> attacker, waiter -> defender
                foreach (KeyValuePair<string, Role> initialPlayer in initialRoundRoles)
                {
                    foreach (var player in lobby.Players)
                    {
                        if (player.Username.Equals(initialPlayer.Key))
                        {
                            switch (initialPlayer.Value)
                            {
                                case Role.Attacker:
                                    player.Role = Role.Waiter;
                                    break;
                                case Role.Defender:
                                    player.Role = Role.Attacker;
                                    break;
                                case Role.Waiter:
                                    player.Role = Role.Defender;
                                    break;
                            }
                        }
                    }
                }
            }
            else
            {
                // The defence failed and all defender becames a waiter
                foreach (KeyValuePair<string, Role> initialPlayer in initialRoundRoles)
                {
                    foreach (var player in lobby.Players)
                    {
                        if (player.Username.Equals(initialPlayer.Key))
                        {
                            switch (initialPlayer.Value)
                            {
                                case Role.Attacker:
                                    player.Role = Role.Defender;
                                    break;
                                case Role.Defender:
                                    player.Role = Role.Waiter;
                                    break;
                                case Role.Waiter:
                                    player.Role = Role.Attacker;
                                    break;
                            }
                        }
                    }
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

            // initialRoundRoles заполняется как только в ривере нету карт
            if (lobby.River.Attacker.Count == 0 && lobby.River.Defender.Count == 0)
            {
                foreach (var somePlayer in lobby.Players)
                {
                    if (initialRoundRoles.ContainsKey(somePlayer.Username))
                        initialRoundRoles[somePlayer.Username] = somePlayer.Role;
                    else
                        initialRoundRoles.Add(somePlayer.Username, somePlayer.Role);
                }
            }

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
                            }

                            UpdateDurakRoles(lobby, true);
                            _endAttackStep = 1;

                            lobby.River.Attacker.Clear();
                            lobby.River.Defender.Clear();
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
                    // Two players click "Done", so the defender successfully beats the card: attacker -> waiter, defender -> attacker, waiter -> defender
                    if (_prevRiverCount == lobby.River.Defender.Count + lobby.River.Attacker.Count)
                    {
                        foreach (var player in lobby.Players)
                        {
                            FillHand(lobby.DeckBox, player);
                        }

                        UpdateDurakRoles(lobby, true);
                        _endAttackStep = 1;

                        lobby.River.Attacker.Clear();
                        lobby.River.Defender.Clear();
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
                        // updateInitialRoles, Defender successfully beats all cards: attacker -> waiter, defender -> attacker, waiter -> defender
                        foreach (var player in lobby.Players)
                        {
                            FillHand(lobby.DeckBox, player);
                        }

                        UpdateDurakRoles(lobby, true);
                        _endAttackStep = 1;

                        lobby.River.Attacker.Clear();
                        lobby.River.Defender.Clear();
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
                if (player.Role == Role.Attacker) 
                    player.Role = Role.Adder;

            foreach (var player in lobby.Players)
            {
                var reply = new DurakReply
                {
                    EndDefenceReply = new EndDefenceReply
                    {
                        IPlayer = new DurakNetPlayer
                        {
                            Role = player.Role,
                            Username = player.Username,
                        },
                    }
                };
                reply.EndDefenceReply.IPlayer.Hand.AddRange(player.Hand);

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
                        reply.EndDefenceReply.EnemyPlayers.Add(EnemyPlayer);
                    }
                }

                await player.DurakStreamReply.WriteAsync(reply);
            }
        }
        public async Task HandleEndAdding(Lobby lobby)
        {
            switch (_endAddingStep)
            {
                case 1:
                {
                    foreach (var player in lobby.Players)
                    {
                        switch (player.Role)
                        {
                            case Role.Adder:
                                player.Role = Role.Waiter;
                                break;
                            case Role.Waiter:
                                player.Role = Role.Adder;
                                break;
                            case Role.FormerAttacker:
                                player.Role = Role.Adder;
                                break;
                        }
                    }

                    _endAddingStep = 2;
                }
                    break;
                case 2:
                    {
                        // We need to update according to initial Roles
                        // Защитник берет карты и мы должны defender -> waiter, waiter -> attacker, attacker -> defender

                        foreach (var player in lobby.Players)
                        {
                            FillHand(lobby.DeckBox, player);
                            if (player.Role == Role.Defender)
                            {
                                player.Hand.AddRange(lobby.River.Attacker);
                                player.Hand.AddRange(lobby.River.Defender);
                                player.Hand.AddRange(lobby.River.Adder);
                            }
                        }

                        UpdateDurakRoles(lobby, false);
                        _endAttackStep = 1;
                        _endAddingStep = 1;

                        lobby.River.Attacker.Clear();
                        lobby.River.Defender.Clear();
                        lobby.River.Adder.Clear();
                    }
                    break;
            }

            foreach (var player in lobby.Players)
            {
                var reply = new DurakReply
                {
                    EndAddingReply = new EndAddingReply
                    {
                        IPlayer = new DurakNetPlayer
                        {
                            Role = player.Role,
                            Username = player.Username,
                        },
                    }
                };
                reply.EndAddingReply.IPlayer.Hand.AddRange(player.Hand);

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
                        reply.EndAddingReply.EnemyPlayers.Add(EnemyPlayer);
                    }
                }

                await player.DurakStreamReply.WriteAsync(reply);
            }
        }
        public async Task HandleFinishGameRound(Lobby lobby)
        {
            foreach (var player in lobby.Players)
            {
                FillHand(lobby.DeckBox, player);
            }

            UpdateDurakRoles(lobby, true);
            _endAddingStep = 1;
            _endAttackStep = 1;

            lobby.River.Attacker.Clear();
            lobby.River.Defender.Clear();

            foreach (var player in lobby.Players)
            {
                var reply = new DurakReply
                {
                    FinishGameRoundReply = new FinishGameRoundReply
                    {
                        IPlayer = new DurakNetPlayer
                        {
                            Role = player.Role,
                            Username = player.Username,
                        },
                    }
                };
                reply.FinishGameRoundReply.IPlayer.Hand.AddRange(player.Hand);

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
                        reply.FinishGameRoundReply.EnemyPlayers.Add(EnemyPlayer);
                    }
                }

                await player.DurakStreamReply.WriteAsync(reply);
            }
        }
        public Lobby GetLobby(Player player) => durakLobbyProvider.Lobbies.FirstOrDefault(x => x.Players.Contains(player));
    }
}