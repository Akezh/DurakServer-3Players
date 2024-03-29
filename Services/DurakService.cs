﻿using System;
using System.Linq;
using System.Threading.Tasks;
using DurakServer.Adapters;
using DurakServer.Helpers;
using DurakServer.Models;
using DurakServer.Providers;
using Grpc.Core;

namespace DurakServer.Services
{
    public class DurakService : DurakGame.DurakGameBase
    {
        private readonly IDurakLobbyAdapter durakLobbyAdapter;
        private readonly IDurakLobbyProvider durakLobbyProvider;

        public DurakService(IDurakLobbyAdapter lobbyAdapter, IDurakLobbyProvider durakLobbyProvider)
        {
            this.durakLobbyAdapter = lobbyAdapter;
            this.durakLobbyProvider = durakLobbyProvider;
        }

        public override async Task DurakStreaming(IAsyncStreamReader<DurakRequest> requestStream, IServerStreamWriter<DurakReply> responseStream, ServerCallContext context)
        {
            var player = new Player
            {
                Username = "DurakPlayer" + new Random().Next(100000, 900000).ToString(),
                DurakStreamReply = responseStream
            };

            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var currentMessage = requestStream.Current;

                switch (requestStream.Current.RequestCase)
                {
                    case DurakRequest.RequestOneofCase.PlayRequest:
                        {
                            await durakLobbyAdapter.CreateLobby(player);
                        }
                        break;
                    case DurakRequest.RequestOneofCase.DialogRequest:
                        {
                            await durakLobbyAdapter.HandleDialogMessage(player, currentMessage.DialogRequest.Dialog);
                        }
                        break;
                    case DurakRequest.RequestOneofCase.TurnRequest:
                        {
                            await durakLobbyAdapter.HandleTurn(player, currentMessage.TurnRequest.Card);
                        }
                        break;
                    case DurakRequest.RequestOneofCase.EndAttackRequest:
                        {
                            await durakLobbyAdapter.HandleEndAttack(player);
                        }
                        break;
                    case DurakRequest.RequestOneofCase.EndDefenceRequest:
                        {
                            await durakLobbyAdapter.HandleEndDefence(player);
                        }
                        break;
                    case DurakRequest.RequestOneofCase.EndAddingRequest:
                        {
                            await durakLobbyAdapter.HandleEndAdding(player);
                        }
                        break;
                }
            }
        }

        public override async Task StartTimerStreaming(TimerRequest request, IServerStreamWriter<TimerReply> responseStream, ServerCallContext context)
        {
            var lobby = LobbyHelper.GetLobby(request.LobbyId, durakLobbyProvider);
            Player activeTimerPlayer = lobby.ActiveTimerPlayer;
            Player me = lobby.Players.FirstOrDefault(x => x.Username.Equals(request.Username));

            try
            {
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var timerEnded = false;
                    for (int i = 40; i > -2; i--)
                    {
                        await Task.Delay(1000);
                        if (!lobby.Players.Contains(me))
                        {
                            timerEnded = true;
                            break;
                        }

                        if (activeTimerPlayer.Equals(lobby.ActiveTimerPlayer) && lobby.ReactivateTimer == false)
                        {
                            await responseStream.WriteAsync(new TimerReply { Time = i, Username = lobby.ActiveTimerPlayer.Username });
                        }
                        else if (activeTimerPlayer.Equals(lobby.ActiveTimerPlayer) && lobby.ReactivateTimer 
                                 || !activeTimerPlayer.Equals(lobby.ActiveTimerPlayer))
                        {
                            i = 40;
                            lobby.ReactivateTimer = false;

                            await responseStream.WriteAsync(new TimerReply { Time = i, Username = lobby.ActiveTimerPlayer.Username });
                            activeTimerPlayer = lobby.ActiveTimerPlayer;
                        }

                        if (i < 0)
                        {
                            if (request.Username == activeTimerPlayer.Username)
                            {
                                await durakLobbyAdapter.HandleGameEnd(me, false, request.LobbyId);
                                return;
                            }

                            return;
                        }
                    }

                    if (timerEnded)
                        break;
                }
            }   
            catch
            {
                await durakLobbyAdapter.HandleGameEnd(me, false);
                return;
            }
            await durakLobbyAdapter.HandleGameEnd(me, false);
        }
    }
}