using Newtonsoft.Json;
using System.Collections.Generic;
   namespace Oxide.Plugins
{
    [Info("CheckPermissions", "Scrooge", "1.0.0")]
    [Description("Данный плагин проверяет игроков на наличие недоброжелательных  пермишенсов")]
    public class CheckPermissions : RustPlugin
    {
        private void OnServerInitialized()
        {
            permission.RegisterPermission(config.BlockSettingsPidoras.PermissionToIgnore, this);
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            foreach(var perms in config.BlockSettingsPidoras.BanPermissions)
            {
                if(permission.UserHasPermission(player.UserIDString, perms))
                {
                    if (config.BlockSettingsPidoras.WhiteListPlayer.Contains(player.userID))
                    {
                        PrintWarning($"{player.displayName} имеет белый лист и он небыл забанен!");
                        return;
                    }
                    if(config.BlockSettingsPidoras.BanAdmin)
                    {
                        if (player.IsAdmin)
                        {
                            PrintWarning($"{player.displayName} имеет овнерку и он небыл забанен!");
                            if(player.IsAdmin) return;
                        }
                    }
                    if (permission.UserHasPermission(player.UserIDString, config.BlockSettingsPidoras.PermissionToIgnore))
                    {
                        PrintWarning($"{player.displayName} имеет пермишенс и он небыл забанен!");
                        return;
                    }
                    timer.Once(2f, () => {
                            {
                                Server.Command($"ban {player.userID} {config.BlockSettingsPidoras.ReasonBan}");
                            }
                    });
                }
            }
		}
        void OnUserPermissionGranted(string id, string permName)
        {
            var player = BasePlayer.Find(id);
            foreach(var perms in config.BlockSettingsPidoras.BanPermissions)
            {
                if(permission.UserHasPermission(player.UserIDString, perms))
                {
                    if (config.BlockSettingsPidoras.WhiteListPlayer.Contains(player.userID))
                    {
                        PrintWarning($"{player.displayName} имеет белый лист и он небыл забанен!");
                        return;
                    }
                    if(config.BlockSettingsPidoras.BanAdmin)
                    {
                        if (player.IsAdmin)
                        {
                            PrintWarning($"{player.displayName} имеет овнерку и он небыл забанен!");
                            if(player.IsAdmin) return;
                        }
                    }
                    if (permission.UserHasPermission(player.UserIDString, config.BlockSettingsPidoras.PermissionToIgnore))
                    {
                        PrintWarning($"{player.displayName} имеет пермишенс и он небыл забанен!");
                        return;
                    }
                    timer.Once(2f, () => {
                            {
                                Server.Command($"ban {player.userID} {config.BlockSettingsPidoras.ReasonBan}");
                            }
                    });
                }
            }
        }
        static PluginConfig config;
        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
            PrintWarning("По вопросам пишите в дискорде - yascrooge");
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            Config.WriteObject(config, true);
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        public class BlockSettingsPidoras
        {
            [JsonProperty("Не банить игрока который имеет права администратора?")]
            public bool BanAdmin = false;

            [JsonProperty("Пермишенс который игнорирует игроков")]
            public string PermissionToIgnore = "CheckPermissions.ignore";

            [JsonProperty("Причина для бана")]
            public string ReasonBan = "DETECT";

            [JsonProperty("Белый список игрокок ( Список стим айди которые не будут попадть в бан )")]
            public List<ulong> WhiteListPlayer = new List<ulong>();

            [JsonProperty("Список пермишенсов по которым банить игрока")]
            public List<string> BanPermissions = new List<string>();
        }
        private class PluginConfig
        {
            [JsonProperty("Общая настройка плагина")]
            public BlockSettingsPidoras BlockSettingsPidoras = new BlockSettingsPidoras();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    BlockSettingsPidoras = new BlockSettingsPidoras()
                    {
                        BanAdmin = false,
                        PermissionToIgnore = "CheckPermissions.ignor",
                        ReasonBan = "DETECT",
                        WhiteListPlayer = new List<ulong>()
                        {
                            76561198415422196,
                            76561198415422198
                        },
                        BanPermissions = new List<string>()
                        {
                            "oxide.grant",
                            "oxide.reload"
                        }
                    },
                };
            }
        }
    }
}