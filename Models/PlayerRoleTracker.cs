using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DurakServer.Models
{
    public class PlayerRoleTracker
    {
        public string Username;
        public Role Role;
        public PlayerRoleTracker()
        {
        }
        public PlayerRoleTracker(string username, Role role)
        {
            Username = username;
            this.Role = role;
        }
    }
}
