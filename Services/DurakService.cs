using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DurakServer.Adapters;
using DurakServer.Models;
using DurakServer.Providers;
using Grpc.Core;

namespace DurakServer.Services
{
    public class DurakService : DurakGame.DurakGameBase
    {
        private readonly IDurakLobbyAdapter durakLobbyAdapter;

        public DurakService(IDurakLobbyAdapter lobbyAdapter)
        {
            this.durakLobbyAdapter = lobbyAdapter;
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

                    } break;
                    case DurakRequest.RequestOneofCase.TurnRequest:
                    {
                        var lobby = durakLobbyAdapter.GetLobby(player);

                        if (lobby == null)
                        {
                            new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                        }

                        await durakLobbyAdapter.HandleTurn(lobby, player, currentMessage.TurnRequest.Card);
                    } break;
                    case DurakRequest.RequestOneofCase.EndAttackRequest:
                    {
                        var lobby = durakLobbyAdapter.GetLobby(player);

                        if (lobby == null)
                        {
                            new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                        }

                        await durakLobbyAdapter.HandleEndAttack(lobby, player);
                    } break;
                    case DurakRequest.RequestOneofCase.EndDefenceRequest:
                    {
                        var lobby = durakLobbyAdapter.GetLobby(player);

                        if (lobby == null)
                        {
                            new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                        }

                        await durakLobbyAdapter.HandleEndDefence(lobby);
                    } break;
                    case DurakRequest.RequestOneofCase.EndAddingRequest:
                    {
                        var lobby = durakLobbyAdapter.GetLobby(player);

                        if (lobby == null)
                        {
                            new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                        }

                        await durakLobbyAdapter.HandleEndAdding(lobby, player);
                    }
                        break;
                    case DurakRequest.RequestOneofCase.FinishGameRoundRequest:
                    {
                        var lobby = durakLobbyAdapter.GetLobby(player);

                        if (lobby == null)
                        {
                            new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                        }

                        await durakLobbyAdapter.HandleFinishGameRound(lobby);
                    }
                        break;
                    case DurakRequest.RequestOneofCase.EnableTwoPlayersModeRequest:
                        {
                            var lobby = durakLobbyAdapter.GetLobby(player);

                            if (lobby == null)
                            {
                                new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                            }

                            await durakLobbyAdapter.EnableTwoPlayersMode(lobby, player);
                        }
                        break;

                }
            }
        }

        public override async Task StartTimerStreaming(TimerRequest request, IServerStreamWriter<TimerReply> responseStream, ServerCallContext context)
        {
            var lobby = durakLobbyAdapter.GetLobby(request.LobbyId);
            string activeUsername = lobby.activeTimerPlayerUsername;

            try
            {
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    for (int i = 40; i > -2; i--)
                    {
                        await Task.Delay(1000);
                        
                        if (activeUsername.Equals(lobby.activeTimerPlayerUsername) && lobby.reactivateTimer == false)
                        {
                            await responseStream.WriteAsync(new TimerReply { Time = i, Username = lobby.activeTimerPlayerUsername });
                        } else
                        {
                            i = 40;
                            lobby.reactivateTimer = false;

                            await responseStream.WriteAsync(new TimerReply { Time = i, Username = lobby.activeTimerPlayerUsername });
                            activeUsername = lobby.activeTimerPlayerUsername;
                        }

                    }
                }
            }
            catch
            {
                //await EndLobbyWhenTimerProblem(lobby, client, mirrorClient);
            }
        }

    }
}
