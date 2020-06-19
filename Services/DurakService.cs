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
        private readonly IDurakLobbyAdapter lobbyAdapter;

        public DurakService(IDurakLobbyAdapter lobbyAdapter)
        {
            this.lobbyAdapter = lobbyAdapter;
        }

        public override async Task DurakStreaming(IAsyncStreamReader<DurakRequest> requestStream, IServerStreamWriter<DurakReply> responseStream, ServerCallContext context)
        {
            var player = new Player
            {
                Username = "DurakPlayer" + new Random().Next(100000, 900000).ToString(),
                DurakStreamReply = responseStream
            };

            // DECKBOX

            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var currentMessage = requestStream.Current;

                switch (requestStream.Current.RequestCase)
                {
                    case DurakRequest.RequestOneofCase.PlayRequest:
                    {
                        await lobbyAdapter.CreateLobby(player);

                    } break;
                    case DurakRequest.RequestOneofCase.TurnRequest:
                    {
                        var lobby = lobbyAdapter.GetLobby(player);

                        if (lobby == null)
                        {
                            new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                        }

                        await lobbyAdapter.HandleTurn(lobby, player, currentMessage.TurnRequest.Card);
                    } break;
                    case DurakRequest.RequestOneofCase.EndAttackRequest:
                    {
                        var lobby = lobbyAdapter.GetLobby(player);

                        if (lobby == null)
                        {
                            new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                        }

                        await lobbyAdapter.HandleEndAttack(lobby);
                    } break;
                    case DurakRequest.RequestOneofCase.EndDefenceRequest:
                    {
                        var lobby = lobbyAdapter.GetLobby(player);

                        if (lobby == null)
                        {
                            new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                        }

                        await lobbyAdapter.HandleEndDefence(lobby);
                    } break;
                    case DurakRequest.RequestOneofCase.EndAddingRequest:
                    {
                        var lobby = lobbyAdapter.GetLobby(player);

                        if (lobby == null)
                        {
                            new RpcException(new Grpc.Core.Status(new StatusCode(), "Лобби не найден"));
                        }

                        await lobbyAdapter.HandleEndAdding(lobby);
                    }
                        break;
                }
            }
        }



    }
}
