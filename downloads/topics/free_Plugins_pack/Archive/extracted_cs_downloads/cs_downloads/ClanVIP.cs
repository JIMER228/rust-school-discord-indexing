using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ClanVIP", "Eh3ani", "1.2.1")]
    [Description("Gives VIP if clan has X+ members; Removes VIP when leaving clan or clan < X members.")]
    public class ClanVIP : CovalencePlugin
    {
        #region Configuration (Editable)

        // Minimum clan members required for VIP
        private const int minClanMembers = 4;

        // Name of the VIP group to grant/remove
        private const string vipGroup = "vip";

        #endregion

        // Reference to the Clans plugin
        [PluginReference] private Plugin Clans;

        void Init()
        {
            // Create VIP group if it doesn't exist
            if (!permission.GroupExists(vipGroup))
            {
                permission.CreateGroup(vipGroup, "VIP Group", 0);
                Puts($"Created '{vipGroup}' group.");
            }
        }

        // Hook: When a player joins a clan
        void OnClanMemberJoined(string userID, string tag)
        {
            if (Clans == null) return;

            // Get clan members after the new member joined
            var clan = Clans.Call<Newtonsoft.Json.Linq.JObject>("GetClan", tag);
            var members = clan?["members"]?.ToObject<List<string>>();
            if (members == null || members.Count < minClanMembers) return;

            // Give VIP to all members if clan has enough members
            foreach (var memberID in members)
                AddVip(memberID);
        }

        // Hook: When a player leaves a clan
        void OnClanMemberGone(string userID, string tag)
        {
            // Remove VIP from the leaving player
            RemoveVip(userID);  

            if (Clans == null) return;

            // Get remaining clan members after someone left
            var clan = Clans.Call<Newtonsoft.Json.Linq.JObject>("GetClan", tag);
            var members = clan?["members"]?.ToObject<List<string>>();
            
            // If clan members fall below required number, remove VIP from remaining members
            if (members == null || members.Count < minClanMembers)
            {
                if (members != null)
                {
                    foreach (var memberID in members)
                        RemoveVip(memberID);
                }
            }
        }

        // Adds VIP group to a player
        void AddVip(string userID)
        {
            if (!permission.UserHasGroup(userID, vipGroup))
            {
                permission.AddUserGroup(userID, vipGroup);
                var player = players.FindPlayerById(userID);
                player?.Message("You have been added to VIP.");
            }
        }

        // Removes VIP group from a player
        void RemoveVip(string userID)
        {
            if (permission.UserHasGroup(userID, vipGroup))
            {
                permission.RemoveUserGroup(userID, vipGroup);
                var player = players.FindPlayerById(userID);
                player?.Message("Your VIP status has been removed.");
            }
        }

        // Hook: Check player's VIP on server join
        void OnUserConnected(IPlayer player)
        {
            timer.Once(5f, () => CheckPlayerVip(player));
        }

        // Check and update VIP for a player
        void CheckPlayerVip(IPlayer player)
        {
            if (Clans == null) return;

            var clanTag = Clans.Call<string>("GetClanOf", player.Id);
            
            // Remove VIP if player is not in a clan
            if (string.IsNullOrEmpty(clanTag))
            {
                RemoveVip(player.Id);
                return;
            }

            // Get clan members to check if clan size is sufficient
            var clanMembers = Clans.Call<List<string>>("GetClanMembers", player.Id);
            if (clanMembers != null && clanMembers.Count >= minClanMembers)
                AddVip(player.Id);
            else
                RemoveVip(player.Id);
        }
    }
}
