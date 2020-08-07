using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;

namespace DurakServer.Models
{
    public class Player
    {
        public string Username { get; set; }
        public List<Card> Hand { get; set; } = new List<Card>();
        public IServerStreamWriter<DurakReply> DurakStreamReply { get; set; }
        public IServerStreamWriter<TimerReply> TimerStreamReply { get; set; }
        public Role Role { get; set; }
    }
}
