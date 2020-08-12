using System;
using System.Collections.Generic;
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
                            //var lobby = HandleThreadSafeLobby(durakLobbyAdapter.CheckLobby(player));
                            //await durakLobbyAdapter.HandleEndAttack(lobby, player);
                            //await OneOf(async delegate (Task) =>
                            //durakLobbyAdapter.HandleEndAttack(lobby, player), player);
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
            Player activeTimerPlayer = lobby.activeTimerPlayerUsername;

            try
            {
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    for (int i = 1000; i > -2; i--)
                    {
                        await Task.Delay(1000);

                        //if (activeTimerPlayer.Equals(lobby.activeTimerPlayerUsername) && lobby.reactivateTimer == false)
                        //{
                        //    await responseStream.WriteAsync(new TimerReply { Time = i, Username = lobby.activeTimerPlayerUsername.Username });
                        //}
                        //else if ((activeTimerPlayer.Equals(lobby.activeTimerPlayerUsername) && lobby.reactivateTimer == true) || (!activeTimerPlayer.Equals(lobby.activeTimerPlayerUsername)))
                        //{
                        //    i = 40;
                        //    lobby.reactivateTimer = false;

                        //    await responseStream.WriteAsync(new TimerReply { Time = i, Username = lobby.activeTimerPlayerUsername.Username });
                        //    activeTimerPlayer = lobby.activeTimerPlayerUsername;
                        //}

                        //if (i < 0)
                        //{
                        //    await durakLobbyAdapter.HandleGameEnd(activeTimerPlayer, false);
                        //    return;
                        //}
                    }
                }
            }
            catch
            {   
                lobby.RemovePlayer(activeTimerPlayer);
                await durakLobbyAdapter.HandleGameEnd(activeTimerPlayer, false);
                //await EndLobbyWhenTimerProblem(lobby, client, mirrorClient);
            }


        }


    }
}
