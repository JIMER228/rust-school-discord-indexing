using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Stuff", "molik", "1.0.1")]
    [Description("команда стафа")]

    public class Stuff : RustPlugin
    {
          [PluginReference] Plugin IQChat;
        private bool OnlineStuff = true;
        private bool OnlineModerator = true;
        private bool Online = true;
        private string SendMessageCol = "Нельзя отправлять так часто тикеты! Подождите {0:0} сек. и повторите еще раз!";
        private double Cooldown = 120f;
        private string IQChatPrefix = "Base Rust";
        private string AdminsOnlineColor = "#FF0000";
        private string ModeratorsOnlineColor = "#FFA500";
        private string GlmoderOnlineColor = "#B22222";
        private string OnlineColor = "#00FFFF";
        private string IQChatSteamID = "76561199185509039";
        private string ModerPerm = "stuff.moder";
        private string GlmoderPerm = "stuff.glmoder";
        private string OnlinePerm = "stuff.online";
        private string AdminPerm = "stuff.admin";
        private static Stuff Instance;
        private Dictionary<BasePlayer, DateTime> Cooldowns = new Dictionary<BasePlayer, DateTime>();
        Dictionary<string, string> Commands;

         void OnServerInitialized()
        {    
            Instance = this;
            if (IQChat == null)
            {
                PrintError("Missing plugin dependency IQChat");
                Interface.Oxide.UnloadPlugin(Name);
            }
            PermissionService.RegisterPermissions(this, new List<string>() { ModerPerm, GlmoderPerm, AdminPerm, OnlinePerm });
        }
        string col(string colour, string text)
        {
            return $"<color={colour}>{text}</color>";
        } 
        private List<BasePlayer> adminOnline => BasePlayer.activePlayerList.Where(p => PermissionService.HasPermission(p.userID, AdminPerm)).ToList();
        private List<BasePlayer> moderOnline => BasePlayer.activePlayerList.Where(p => PermissionService.HasPermission(p.userID, ModerPerm)).ToList();
        private List<BasePlayer> glmoderOnline => BasePlayer.activePlayerList.Where(p => PermissionService.HasPermission(p.userID, GlmoderPerm)).ToList();
        private List<BasePlayer> OnlinePlayer => BasePlayer.activePlayerList.Where(p => PermissionService.HasPermission(p.userID, OnlinePerm)).ToList();

        [ChatCommand("stuff")]
        void onCommandAdmins(BasePlayer player, string command, string[] args)
        {
            if (!OnlineStuff) return;

            if (adminOnline.Count <= 0)
            { IQChat?.Call("API_ALERT_PLAYER", player, "На данный момент нет администраторов и модераторов в сети.", IQChatPrefix, IQChatSteamID); return; }

            string msg = $"{col(AdminsOnlineColor, "<color=#18FFD1> Admins Online and Moderators Online</color>:")}\n";
            msg += string.Join("", adminOnline.Select(p => $"\n> {p.displayName} -> Admin\n").ToArray());
            msg += string.Join("\n", glmoderOnline.Select(p => $"\n> {p.displayName} -> Glmoder").ToArray());
            msg += string.Join("\n", moderOnline.Select(p => $"\n> {p.displayName} -> Moder").ToArray());
            IQChat?.Call("API_ALERT_PLAYER", player, msg, IQChatPrefix, IQChatSteamID);
        }
        [ChatCommand("online")]
        void onCommandOnline(BasePlayer player, string command, string[] args)
        {
            if (!Online) return;
            string msg = $"{col(OnlineColor, "Online:")}\n";
            msg += string.Join("\n", OnlinePlayer.Select(p => $"> {p.displayName}").ToArray());
            IQChat?.Call("API_ALERT_PLAYER", player, msg, IQChatPrefix, IQChatSteamID);
        }

        #region Permission Service

       public static class PermissionService
       {
        public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

        public static bool HasPermission(ulong uid, string permissionName)
        {
            return !string.IsNullOrEmpty(permissionName) && permission.UserHasPermission(uid.ToString(), permissionName);
        }

        public static void RegisterPermissions(Plugin owner, List<string> permissions)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            if (permissions == null) throw new ArgumentNullException("commands");

            foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
            {
                permission.RegisterPermission(permissionName, owner);
            }
        }
      }
    #endregion
    }
}