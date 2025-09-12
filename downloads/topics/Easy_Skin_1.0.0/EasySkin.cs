using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Easy Skin", "Logan", "1.0.0")]
    class EasySkin : RustPlugin
    {
        public EasySkin easySkin;

        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Whitelisted Skins Only | If set to true, players only can use the whitelisted skins")]
            public bool WhitelistedSkinsOnly = false;
            [JsonProperty(PropertyName = "Whitelisted Skins | Skins that players are able to use")]
            public ulong[] WhitelistedSkins = {0};
            [JsonProperty(PropertyName = "Blacklisted Skins | Skins that players are not able to use")]
            public ulong[] BlacklistedSkins = { };
        }

        private bool LoadConfig()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }

        void Init()
        {
            permission.RegisterPermission("easyskin.admin", this);
            permission.RegisterPermission("easyskin.use", this);
            if (!LoadConfig())
            {
                Puts("Config file issue detected! Please delete the config file, or check for syntax errors and fix them!");
                return;
            }

            easySkin = this;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file");
            configData = new ConfigData();
            SaveConfig(configData);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        [ChatCommand("skin")] // Change skin to what command you would like it as
        void changeskin(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.userID.ToString(), "easyskin.use"))
            {
                if (args[0] != null)
                {
                    Item item = player.GetActiveItem();
                    uint skin = uint.Parse(args[0]);
                    if (item != null)
                    {
                        bool Pass = true;
                        if (configData.WhitelistedSkinsOnly)
                        {
                            Pass = false;
                            if (configData.WhitelistedSkins.Contains(skin))
                            {
                                Pass = true;
                            }
                        }
                        if (configData.BlacklistedSkins.Contains(skin))
                        {
                            Pass = false;
                        }


                        if (Pass)
                        {
                            item.skin = skin;
                            item.MarkDirty();
                            BaseEntity heldEntity = item.GetHeldEntity();
                            if (heldEntity != null)
                            {
                                heldEntity.skinID = skin;
                                heldEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            }
                        }
                    }
                }
            }
            else
            {
                SendReply(player, "You do not have permission to use this command!");
            }
        }

        [ChatCommand("cscreload")]
        void cscReload(BasePlayer player)
        {
            if (permission.UserHasPermission(player.userID.ToString(), "easyskin.admin"))
                LoadConfig();
        }
    }
}
 