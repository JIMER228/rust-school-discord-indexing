using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("(Atlas) Teleport", "Ilovepatatos", "1.0.0")]
    public class AtlasTeleport : CovalencePlugin
    {
        private const string CONFIGS_FILENAME = nameof(AtlasTeleport);

        private static AtlasTeleport s_PluginInstance;

        private Configuration m_InternalConfigs;
        private DataFileSystem m_ConfigsFolder;

        private readonly Dictionary<ulong, List<ulong>> m_PlayerToOptionsCache = new Dictionary<ulong, List<ulong>>();

#region Configs

        [Serializable]
        private class Configuration
        {
            public string UsePerms = "teleport.admin";
            public int MaxCacheSize = 20;
        }

        private Configuration Configs
        {
            get
            {
                if (m_InternalConfigs == null)
                    LoadConfig();

                return m_InternalConfigs;
            }
            set { m_InternalConfigs = value; }
        }

        private DataFileSystem ConfigsFolder => m_ConfigsFolder ?? (m_ConfigsFolder = new DataFileSystem($"{Interface.Oxide.ConfigDirectory}"));

        protected override void LoadConfig()
        {
            if (ConfigsFolder.ExistsDatafile(CONFIGS_FILENAME))
            {
                try
                {
                    Configs = ConfigsFolder.ReadObject<Configuration>(CONFIGS_FILENAME);
                    SaveConfig();
                }
                catch (Exception ex)
                {
                    Log($"The configuration file seems to be corrupted!\n{ex}");
                    LoadDefaultConfig();
                }
            }
            else
            {
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Configs = new Configuration();
        }

        protected override void SaveConfig()
        {
            ConfigsFolder.WriteObject(CONFIGS_FILENAME, Configs);
        }

#endregion

#region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MISSING_ARGUMENT"] = "Use <color=#B1FF6B>/tp <player name or id></color> to teleport to someone",
                ["NO_TARGET_FOUND"] = "Couldn't find target <color=#B1FF6B>{target}</color>!",
                ["MULTIPLE_PLAYERS_FOUND"] = "Found multiple players:\n{players}",

                ["TELEPORTED"] = "You have been teleported by an admin!",
                ["TELEPORTING_TO"] = "You've teleported to <color=#B1FF6B>{target}</color>!",
                ["TELEPORTING_TO_ME"] = "You've teleported <color=#B1FF6B>{target}</color> to yourself!",

                ["CACHE_FORMAT"] = "<color=#FFA500>{index}</color> - {target}",
                ["CACHE_MORE"] = "<color=#FFA500>And {remaining} more...</color>",
            }, this);
        }

        public static string Lang(IPlayer iPlayer, string key)
        {
            return s_PluginInstance.lang.GetMessage(key, s_PluginInstance, iPlayer?.Id);
        }

        public static string Lang(string key)
        {
            return Lang((IPlayer)null, key);
        }

        public static string Lang(BasePlayer player, string key)
        {
            IPlayer iPlayer = player != null ? player.IPlayer : null;
            return Lang(iPlayer, key);
        }

#endregion

#region Commands

        [UsedImplicitly]
        [Command("tp")]
        private void TeleportToPlayer(IPlayer iPlayer, string cmd, string[] args)
        {
            if (!iPlayer.HasPermission(Configs.UsePerms))
                return;

            string playerNameOrID = args.GetString(0);
            bool includeSleepers = args.GetBool(1);
            TeleportToPlayer(iPlayer.Object as BasePlayer, playerNameOrID, includeSleepers);
        }

        [UsedImplicitly]
        [Command("atphere")]
        private void TeleportPlayerToMe(IPlayer iPlayer, string cmd, string[] args)
        {
            if (!iPlayer.HasPermission(Configs.UsePerms))
                return;

            string playerNameOrID = args.GetString(0);
            TeleportPlayerToMe(iPlayer.Object as BasePlayer, playerNameOrID);
        }

#endregion

#region Hooks

        [UsedImplicitly]
        [HookMethod(nameof(Init))]
        private void Init()
        {
            Log($"Initializing {nameof(AtlasTeleport)}...");
            s_PluginInstance = this;
            permission.RegisterPermission(Configs.UsePerms, this);
        }

        [UsedImplicitly]
        [HookMethod(nameof(Unload))]
        private void Unload()
        {
            Log($"Unloading {nameof(AtlasTeleport)}...");
            s_PluginInstance = null;
        }

        [UsedImplicitly]
        [HookMethod(nameof(OnPlayerDisconnected))]
        private void OnPlayerDisconnected(BasePlayer player)
        {
            m_PlayerToOptionsCache.Remove(player.userID);
        }

#endregion

#region Methods

        private void TeleportToPlayer(BasePlayer player, string playerNameOrID, bool includeSleepers)
        {
            if (string.IsNullOrEmpty(playerNameOrID))
            {
                string text = Lang(player, "MISSING_ARGUMENT");
                player.ChatMessage(text);
            }
            else
            {
                BasePlayer target = GetSinglePlayer(player, playerNameOrID, includeSleepers);
                if (target == null) return;

                Log($"{player} is teleporting to {target} at {DateTime.Now}");
                player.Teleport(target);

                string text = Lang(player, "TELEPORTING_TO");
                text = text.Replace("{target}", target.displayName);
                player.ChatMessage(text);
            }
        }

        private void TeleportPlayerToMe(BasePlayer player, string playerNameOrID)
        {
            if (string.IsNullOrEmpty(playerNameOrID))
            {
                string text = Lang(player, "MISSING_ARGUMENT");
                player.ChatMessage(text);
            }
            else
            {
                BasePlayer target = GetSinglePlayer(player, playerNameOrID, false);
                if (target == null) return;

                Log($"{player} is teleporting {target} to self at {DateTime.Now}");
                target.Teleport(player);

                string text = Lang(player, "TELEPORTING_TO_ME");
                text = text.Replace("{target}", target.displayName);
                player.ChatMessage(text);

                text = Lang(target, "TELEPORTED");
                target.ChatMessage(text);
            }
        }

#endregion

#region Utility

        private BasePlayer GetSinglePlayer(BasePlayer player, string playerNameOrID, bool includeSleepers)
        {
            BasePlayer cache = GetPlayerByCache(player.userID, playerNameOrID);
            if (cache != null) return cache;

            var list = Facepunch.Pool.GetList<BasePlayer>();
            GetTargetPlayers(playerNameOrID, includeSleepers, list);

            if (list.Count == 1)
            {
                BasePlayer target = list[0];
                Facepunch.Pool.FreeList(ref list);
                return target;
            }

            if (list.Count > 1)
            {
                var ids = list.Select(x => x.userID.Get()).ToList();
                m_PlayerToOptionsCache[player.userID] = ids;

                string format = FormatPlayers(player, Configs.MaxCacheSize, list);
                Facepunch.Pool.FreeList(ref list);

                string text = Lang(player, "MULTIPLE_PLAYERS_FOUND");
                text = text.Replace("{players}", format);

                player.ChatMessage(text);
                return null;
            }

            {
                string text = Lang(player, "NO_TARGET_FOUND");
                text = text.Replace("{target}", playerNameOrID);

                player.ChatMessage(text);
                return null;
            }
        }

        private BasePlayer GetPlayerByCache(ulong requesterID, string playerNameOrID)
        {
            if (!m_PlayerToOptionsCache.ContainsKey(requesterID))
                return null;

            if (!int.TryParse(playerNameOrID, out int value))
                return null;

            var list = m_PlayerToOptionsCache[requesterID];
            if (value < 0 || value > list.Count - 1) return null;

            ulong targetID = list[value];
            return BasePlayer.FindByID(targetID);
        }

        private static void GetTargetPlayers(string playerNameOrID, bool includeSleepers, ICollection<BasePlayer> list)
        {
            var enumerable = includeSleepers ? BasePlayer.allPlayerList.Concat(BasePlayer.sleepingPlayerList) : BasePlayer.activePlayerList;
            foreach (BasePlayer target in enumerable)
            {
                if (target == null || list.Contains(target))
                    continue;

                if (target.UserIDString == playerNameOrID || target.displayName.Contains(playerNameOrID, StringComparison.OrdinalIgnoreCase))
                    list.Add(target);
            }
        }

        private static string FormatPlayers(BasePlayer player, int max, IReadOnlyList<BasePlayer> targets)
        {
            StringBuilder sb = new StringBuilder();
            string format = Lang(player, "CACHE_FORMAT");

            for (var i = 0; i < targets.Count; i++)
            {
                BasePlayer target = targets[i];

                string text = format.Replace("{index}", $"{i}");
                text = text.Replace("{target}", target.displayName);
                sb.AppendLine(text);

                if (i < max)
                    continue;

                string remaining = Lang(player, "CACHE_MORE");
                remaining = remaining.Replace("{remaining}", $"{targets.Count - i}");
                sb.AppendLine(remaining);

                break;
            }

            return sb.ToString();
        }

#endregion
    }
}

public static class StringArrayEx
{
    public static bool HasArgs(this string[] args, int index)
    {
        return args != null && args.Length > index;
    }

    public static string GetString(this string[] args, int index, string fallback = "")
    {
        return args.HasArgs(index) ? args[index] : fallback;
    }

    public static bool GetBool(this string[] args, int index, bool fallback = false)
    {
        string s = args.GetString(index);
        return bool.TryParse(s, out bool result) ? result : fallback;
    }

    public static float GetFloat(this string[] args, int index, float fallback = 0)
    {
        string s = args.GetString(index);
        return float.TryParse(s, out float result) ? result : fallback;
    }

    public static int GetInt(this string[] args, int index, int fallback = 0)
    {
        string s = args.GetString(index);
        return int.TryParse(s, out int result) ? result : fallback;
    }

    public static uint GetUint(this string[] args, int index, uint fallback = 0)
    {
        string s = args.GetString(index);
        return uint.TryParse(s, out uint result) ? result : fallback;
    }

    public static long GetLong(this string[] args, int index, long fallback = 0)
    {
        string s = args.GetString(index);
        return long.TryParse(s, out long result) ? result : fallback;
    }

    public static ulong GetUlong(this string[] args, int index, ulong fallback = 0)
    {
        string s = args.GetString(index);
        return ulong.TryParse(s, out ulong result) ? result : fallback;
    }
}