using System.Collections.Generic;
using System.Linq;

namespace DurakServer.Models
{
    public class RolesCollection
    {
        public List<PlayerRoleTracker> RolesCollections;

        public RolesCollection()
        {
            RolesCollections = new List<PlayerRoleTracker>();
        }
        public IEnumerator<PlayerRoleTracker> GetEnumerator()
        {
            foreach (PlayerRoleTracker player in RolesCollections)
            {
                yield return player;
            }
        }
        public void Add(string username, Role role)
        {
            RolesCollections.Add(new PlayerRoleTracker(username, role));
        }
        public Role Get(string username)
        {
            foreach (PlayerRoleTracker player in RolesCollections)
                if (player.Username.Equals(username))
                    return player.Role;

            return Role.Inactive;
        }
        public void Update(string username, Role role)
        {
            foreach (PlayerRoleTracker player in RolesCollections)
            {
                if (player.Username.Equals(username))
                {
                    player.Role = role;
                    return;
                }
            }
        }
        public bool ContainsKey(string username)
        {
            foreach (PlayerRoleTracker player in RolesCollections)
                if (player.Username.Equals(username))
                    return true;

            return false;
        }

        public int Count()
        {
            return RolesCollections.Count();
        }
    }
}
