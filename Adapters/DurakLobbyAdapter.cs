using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DurakServer.Helpers;
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
        Task HandleDialogMessage(Player senderPlayer, Dialog dialog);
        Task HandleTurn(Player player, Card card);
        Task HandleEndAttack(Player senderPlayer);
        Task HandleEndDefence(Player senderPlayer);
        Task HandleEndAdding(Player senderPlayer);
        Task HandleFinishGameRound(Player senderPlayer);
        Task EnableTwoPlayersMode(Player senderPlayer);
        Task HandleGameEnd(Player senderPlayer, bool finishedByLogic, int? lobbyId = null);
    }

    public class DurakLobbyAdapter : IDurakLobbyAdapter
    {
        private readonly IDurakLobbyProvider durakLobbyProvider;

        public DurakLobbyAdapter(IDurakLobbyProvider durakLobbyProvider)
        {
            this.durakLobbyProvider = durakLobbyProvider;
        }
        public async Task CreateLobby(Player iPlayer)
        {
            if (durakLobbyProvider.WaitList.Count > 1)
            {
                var enemyPlayers = durakLobbyProvider.WaitList;

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
                Id = 1,
                Players = players
            };

            foreach (var player in players)
            {
                FillHand(lobby.DeckBox, player);
            }

            durakLobbyProvider.Lobbies.Add(lobby);
            SetDurakRoles(players, lobby);
            SetActiveTimerPlayer(lobby);

            return lobby;
        }
        private async Task BroadcastLobbyAsync(Lobby lobby)
        {
            foreach (var player in lobby.Players)
            {
                var lobbyReply = new LobbyReply
                {
                    Id = lobby.Id,

                    IPlayer = new DurakNetPlayer
                    {
                        Username = player.Username,
                        Role = player.Role
                    }
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

            // Определяем у кого наименьший козырь
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
        private void UpdateDurakRoles(Player senderPlayer, bool areCardsBeatenSuccessfully)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            foreach (PlayerRoleTracker initialPlayer in lobby.initialRoundRoles)
            {
                foreach (var player in lobby.Players)
                {
                    if (lobby.TwoPlayersLeft == false)
                    {
                        if (player.Username.Equals(initialPlayer.Username))
                        {
                            // The defence was successful since all 12 cards were in the river: attacker -> waiter, defender -> attacker, waiter -> defender
                            switch (initialPlayer.role)
                            {
                                case Role.Attacker:
                                    if (areCardsBeatenSuccessfully) player.Role = Role.Waiter;
                                    else player.Role = Role.Defender;
                                    break;
                                case Role.Defender:
                                    if (areCardsBeatenSuccessfully) player.Role = Role.Attacker;
                                    else player.Role = Role.Waiter;
                                    break;
                                case Role.Waiter:
                                    if (areCardsBeatenSuccessfully) player.Role = Role.Defender;
                                    else player.Role = Role.Attacker;
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (player.Username.Equals(initialPlayer.Username))
                        {
                            if (player.Role == Role.Inactive) continue;

                            switch (initialPlayer.role)
                            {
                                case Role.Attacker:
                                    if (areCardsBeatenSuccessfully) player.Role = Role.Defender;
                                    else player.Role = Role.Attacker;
                                    break;
                                case Role.Defender:
                                    if (areCardsBeatenSuccessfully) player.Role = Role.Attacker;
                                    else player.Role = Role.Defender;
                                    break;
                                case Role.Adder:
                                    player.Role = Role.Attacker;
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
                if (deckBox.ShuffledDeckList.Count <= 0) return;
                var card = deckBox.DrawCardFromShuf();

                // Правильно проверить на null
                if (card != null)
                    player.Hand.Add(card);
            }
        }
        private void FillHandInSequence(Player senderPlayer)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            foreach (PlayerRoleTracker initialPlayer in lobby.initialRoundRoles)
                if (initialPlayer.role == Role.Attacker)
                    foreach (var player in lobby.Players)
                        if (player.Username.Equals(initialPlayer.Username))
                        {
                            FillHand(lobby.DeckBox, player);
                            break;
                        }

            foreach (PlayerRoleTracker initialPlayer in lobby.initialRoundRoles)
                if (initialPlayer.role == Role.Waiter)
                    foreach (var player in lobby.Players)
                        if (player.Username.Equals(initialPlayer.Username))
                        {
                            FillHand(lobby.DeckBox, player);
                            break;
                        }

            foreach (PlayerRoleTracker initialPlayer in lobby.initialRoundRoles)
                if (initialPlayer.role == Role.Defender)
                    foreach (var player in lobby.Players)
                        if (player.Username.Equals(initialPlayer.Username))
                        {
                            FillHand(lobby.DeckBox, player);
                            break;
                        }
        }
        public async Task HandleDialogMessage(Player senderPlayer, Dialog dialog)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            var reply = new DurakReply
            {
                DialogReply = new DialogReply { Dialog = dialog, Username = senderPlayer.Username }
            };

            foreach (var player in lobby.Players) await player.DurakStreamReply.WriteAsync(reply);
        }
        public async Task HandleTurn(Player senderPlayer, Card card)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            var reply = new DurakReply
            {
                TurnReply = new TurnReply { Card = card }
            };

            if (lobby.River.Attacker.Count == 0 && lobby.River.Defender.Count == 0)
            {
                foreach (var player in lobby.Players)
                {
                    if (lobby.initialRoundRoles.ContainsKey(player.Username))
                        lobby.initialRoundRoles.Update(player.Username, player.Role);
                    else
                        lobby.initialRoundRoles.Add(player.Username, player.Role);
                }
            }

            foreach (var player in lobby.Players)
            {
                if (player.Hand.Contains(card) && player.Role == Role.Attacker)
                    lobby.River.Attacker.Add(card);
                else if (player.Hand.Contains(card) && player.Role == Role.Defender)
                    lobby.River.Defender.Add(card);
                else if (player.Hand.Contains(card) && player.Role == Role.Adder)
                    lobby.River.Adder.Add(card);
            }

            senderPlayer.Hand.Remove(card);

            SetActiveTimerPlayer(lobby);

            foreach (var player in lobby.Players)
            {
                try
                {
                    await player.DurakStreamReply.WriteAsync(reply);
                }
                catch
                {
                    continue;
                }
            }

            if (lobby.EndAttackStep == 3) await HandleEndAttack(senderPlayer);

            // When The Game Ends
            if (lobby.DeckBox.ShuffledDeckList.Count == 0 && senderPlayer.Hand.Count == 0 && lobby.TwoPlayersLeft == true)
            {
                senderPlayer.Role = Role.Inactive;
                await HandleGameEnd(senderPlayer, true);
            }

            // If player has no more cards, set him as an inactive
            if (lobby.DeckBox.ShuffledDeckList.Count == 0 && senderPlayer.Hand.Count == 0 && lobby.TwoPlayersLeft == false)
            {
                lobby.TwoPlayersLeft = true;

                if (senderPlayer.Role == Role.Defender)
                {
                    await HandleFinishGameRound(senderPlayer);
                } else if (senderPlayer.Role == Role.Attacker || senderPlayer.Role == Role.Adder)
                {
                    await EnableTwoPlayersMode(senderPlayer);
                }
            }

            if (lobby.River.Attacker.Count + lobby.River.Defender.Count == 12) await HandleFinishGameRound(senderPlayer);
        }
        public async Task HandleEndAttack(Player senderPlayer)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            if (lobby.TwoPlayersLeft == true)
            {
                await DefenderBeatsCards(senderPlayer);
            }
            else
            {
                switch (lobby.EndAttackStep)
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

                            lobby.PrevRiverCount = lobby.River.Defender.Count + lobby.River.Attacker.Count;
                            lobby.EndAttackStep = 2;
                        }
                        break;
                    case 2:
                        {
                            if (lobby.PrevRiverCount == lobby.River.Defender.Count + lobby.River.Attacker.Count)
                            {
                                await DefenderBeatsCards(senderPlayer);
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

                                lobby.PrevRiverCount = lobby.River.Defender.Count + lobby.River.Attacker.Count;
                                lobby.EndAttackStep = 3;
                            }
                        }
                        break;
                    // Case 3 is handled in HandleTurn method since we need to wait of throwing one card from new attacker
                    case 3:
                        if (lobby.PrevRiverCount == lobby.River.Defender.Count + lobby.River.Attacker.Count)
                        {
                            await DefenderBeatsCards(senderPlayer);
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
                            lobby.EndAttackStep = 4;
                        }
                        break;
                    case 4:
                        {
                            senderPlayer.Role = Role.FormerAttacker;
                            lobby.EndAttackStep = 5;
                        }
                        break;
                    case 5:
                        {
                            await DefenderBeatsCards(senderPlayer);
                        }
                        break;
                }
            }

            SetActiveTimerPlayer(lobby);

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
        public async Task HandleEndDefence(Player senderPlayer)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            foreach (var player in lobby.Players)
                if (player.Role == Role.Attacker)
                    player.Role = Role.Adder;

            if (lobby.River.Attacker.Count == 6) DefenderTakesCards(senderPlayer);

            SetActiveTimerPlayer(lobby);

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
        public async Task HandleEndAdding(Player senderPlayer)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            if (lobby.TwoPlayersLeft == true)
            {
                DefenderTakesCards(senderPlayer);
            } else
            {
                switch (lobby.EndAddingStep)
                {
                    case 1:
                        {
                            foreach (var player in lobby.Players)
                            {
                                if (player.Username.Equals(senderPlayer.Username))
                                    player.Role = Role.Waiter;
                                else if (player.Role == Role.Waiter || player.Role == Role.FormerAttacker)
                                    player.Role = Role.Adder;
                            }

                            lobby.EndAddingStep = 2;
                            if (lobby.River.Attacker.Count + lobby.River.Adder.Count == 6) DefenderTakesCards(senderPlayer);
                        }
                        break;
                    case 2:
                        DefenderTakesCards(senderPlayer);
                        break;
                }
            }

            SetActiveTimerPlayer(lobby);

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
        public async Task HandleFinishGameRound(Player senderPlayer)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            await DefenderBeatsCards(senderPlayer);

            SetActiveTimerPlayer(lobby);

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
        public async Task EnableTwoPlayersMode(Player senderPlayer)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            if (senderPlayer.Role == Role.Attacker)
            {
                foreach (var player in lobby.Players)
                    if (!player.Username.Equals(senderPlayer.Username) && player.Role != Role.Defender && player.Role != Role.Inactive)
                        player.Role = Role.Attacker;
            } else if (senderPlayer.Role == Role.Adder)
            {
                foreach (var player in lobby.Players)
                    if (!player.Username.Equals(senderPlayer.Username) && player.Role != Role.Defender && player.Role != Role.Inactive)
                    {
                        if (lobby.EndAddingStep == 1)
                        {
                            player.Role = Role.Adder;
                        }
                        else if (lobby.EndAddingStep == 2)
                        {
                            senderPlayer.Role = Role.Inactive;
                            lobby.TwoPlayersLeft = true;

                            await HandleEndAdding(senderPlayer);
                        }
                    }
            }

            // Эту функцию вызывает attacker или adder который выбросил свою последнюю карту
            senderPlayer.Role = Role.Inactive;
            lobby.TwoPlayersLeft = true;
            await CreateGameEndReply(senderPlayer);

            lobby.EndAddingStep = 1;

            // Update initial roles for 2 players
            foreach (var player in lobby.Players)
            {
                if (lobby.initialRoundRoles.ContainsKey(player.Username))
                    lobby.initialRoundRoles.Update(player.Username, player.Role);
                else
                    lobby.initialRoundRoles.Add(player.Username, player.Role);
            }

            SetActiveTimerPlayer(lobby);

            foreach (var player in lobby.Players)
            {
                var reply = new DurakReply
                {
                    EnableTwoPlayersModeReply = new EnableTwoPlayersModeReply
                    {
                        IPlayer = new DurakNetPlayer
                        {
                            Role = player.Role,
                            Username = player.Username,
                        },
                    }
                };
                reply.EnableTwoPlayersModeReply.IPlayer.Hand.AddRange(player.Hand);

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
                        reply.EnableTwoPlayersModeReply.EnemyPlayers.Add(EnemyPlayer);
                    }
                }

                await player.DurakStreamReply.WriteAsync(reply);
            }
        }
        public async Task CreateGameEndReply(Player senderPlayer)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            WinnerPlayer winner = new WinnerPlayer();
            winner.Username = senderPlayer.Username;
            winner.BeetCount = 10;

            List<WinnerPlayer> winners = new List<WinnerPlayer>();
            winners.Add(winner);

            DurakReply DurakGameEndReply = new DurakReply
            {
                GameEndReply = new GameEndReply { }
            };
            DurakGameEndReply.GameEndReply.WinnerPlayers.AddRange(winners);

            foreach (var player in lobby.Players)
                await player.DurakStreamReply.WriteAsync(DurakGameEndReply);

            lobby.RemovePlayer(senderPlayer);
        }
        public async Task HandleGameEnd(Player senderPlayer, bool finishedByLogic, int? lobbyId = null)
        {
            var lobby = lobbyId.HasValue 
                ? LobbyHelper.GetLobby(lobbyId.Value, durakLobbyProvider) 
                : LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            var reply = new DurakReply { GameEndReply = new GameEndReply { } };
            var winners = new List<WinnerPlayer>();

            if (finishedByLogic)
            {

                foreach (var player in lobby.Players)
                {
                    WinnerPlayer winner = new WinnerPlayer();
                    if (player.Role == Role.Inactive)
                    {
                        winner.Username = player.Username;
                        winner.BeetCount = 5;
                    }
                    else
                    {
                        winner.Username = player.Username;
                        winner.BeetCount = 3;
                    }
                    winners.Add(winner);
                }
            }
            else
            {
                switch (lobby.Players.Count())
                {
                    // 1 Игрок ранее уже выиграл. А когда вы играли 1 на 1, у 1 игрока закончился таймер
                    case 2:
                    {
                        foreach (var player in lobby.Players)
                        {
                            WinnerPlayer winner = new WinnerPlayer() { Username = player.Username, BeetCount = 9 };
                            winners.Add(winner);
                        }

                        break;
                    }
                    case 1:
                    {
                        foreach (var player in lobby.Players)
                        {
                            WinnerPlayer winner = new WinnerPlayer() { Username = player.Username, BeetCount = 8 };
                            winners.Add(winner);
                        }

                        break;
                    }
                }
            }

            reply.GameEndReply.WinnerPlayers.AddRange(winners);
            foreach (var player in lobby.Players) await player.DurakStreamReply.WriteAsync(reply);
            lobby.ClearPlayers();
        }
        public void DefenderTakesCards(Player senderPlayer)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);

            FillHandInSequence(senderPlayer);
            foreach (var player in lobby.Players)
            {
                if (player.Role == Role.Defender)
                {
                    player.Hand.AddRange(lobby.River.Attacker);
                    player.Hand.AddRange(lobby.River.Defender);
                    player.Hand.AddRange(lobby.River.Adder);
                }
            }

            UpdateDurakRoles(senderPlayer, false);
            lobby.EndAttackStep = 1;
            lobby.EndAddingStep = 1;

            lobby.River.Attacker.Clear();
            lobby.River.Defender.Clear();
            lobby.River.Adder.Clear();
        }
        public async Task DefenderBeatsCards(Player senderPlayer)
        {
            var lobby = LobbyHelper.HandleThreadSafeLobby(senderPlayer, durakLobbyProvider);
            FillHandInSequence(senderPlayer);
            
            foreach (var player in lobby.Players)
            {
                if (player.Role == Role.Defender && player.Hand.Count == 0)
                {
                    player.Role = Role.Inactive;
                    lobby.TwoPlayersLeft = true;

                    await CreateGameEndReply(player);
                }
            }

            Player sameLobbyPlayer = lobby.Players.First();
            UpdateDurakRoles(sameLobbyPlayer, true);
            lobby.EndAttackStep = 1;
            lobby.EndAddingStep = 1;

            lobby.River.Attacker.Clear();
            lobby.River.Defender.Clear();
        }
        public void SetActiveTimerPlayer(Lobby lobby)
        {
            Player formerActiveTimerPlayer = lobby.activeTimerPlayer;

            foreach (var player in lobby.Players)
            {
                if (player.Role == Role.Adder)
                {
                    if (!player.Username.Equals(formerActiveTimerPlayer))
                        lobby.reactivateTimer = false;
                    else
                        lobby.reactivateTimer = true;

                    lobby.activeTimerPlayer = player;

                    return;
                }
            }

            if (lobby.River.Attacker.Count == lobby.River.Defender.Count && lobby.River.Adder.Count == 0)
            {
                foreach (var player in lobby.Players)
                {
                    if (player.Role == Role.Attacker)
                    {
                        if (!player.Username.Equals(formerActiveTimerPlayer))
                            lobby.reactivateTimer = false;
                        else
                            lobby.reactivateTimer = true;

                        lobby.activeTimerPlayer = player;
                        return;
                    }
                }
            }
            else if (lobby.River.Attacker.Count > lobby.River.Defender.Count && lobby.River.Adder.Count == 0)
            {
                foreach (var player in lobby.Players)
                {
                    if (player.Role == Role.Defender)
                    {
                        if (!player.Username.Equals(formerActiveTimerPlayer))
                            lobby.reactivateTimer = false;
                        else
                            lobby.reactivateTimer = true;

                        lobby.activeTimerPlayer = player;
                        return;
                    }
                }
            }

        }
    }
}