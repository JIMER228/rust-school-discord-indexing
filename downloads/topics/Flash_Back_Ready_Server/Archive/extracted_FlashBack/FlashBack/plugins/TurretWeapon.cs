using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("TurretWeapon", "LAGZYA", "1.0.1")]
    public class TurretWeapon : RustPlugin
    {
        [PluginReference] private Plugin WipeBlock;
        private ConfigData Set { get; set; }
        public class ConfigData
        {
            [JsonProperty("Blacklist")]
            public List<string> weaponList = new List<string>();
            [JsonProperty("Permission(Ignore)")] public string perm;
            [JsonProperty("Wipeblock(Hougan/rostov114)")] public bool block;
            public static ConfigData GetNewConf()
            {
                var newconfig = new ConfigData();
                newconfig.perm = "turretweapon.ignore";
                newconfig.block = false;
                newconfig.weaponList = new List<string>()
                {
                    "lmg.m249", "rifle.l96", "rifle.bolt"
                };
                return newconfig;
            }
        }
        protected override void LoadDefaultConfig() => Set = ConfigData.GetNewConf();
        protected override void SaveConfig() => Config.WriteObject(Set);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Set = Config.ReadObject<ConfigData>();
                if (Set?.weaponList == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        private double IsBlock(string shortname)
        {
            return (double) WipeBlock?.Call("IsBlocked", shortname);
        }
           
        private void OnServerInitialized()
        {
           if(!permission.PermissionExists(Set.perm))
               permission.RegisterPermission(Set.perm, this);
               
        }
        private ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (container == null || item == null) return null;
            if (container.entityOwner?.ShortPrefabName != "autoturret_deployed") return null;
            var player = item.GetOwnerPlayer();
            if (player == null) return null;
            if (permission.UserHasPermission(player.UserIDString, Set.perm)) return null;
            if (WipeBlock && Set.block)
            {
               if (IsBlock(item.info.shortname) > 0)
               {
                   SendReply(player, "WipeBlock");
                   return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
               }
            }
            if (!Set.weaponList.Contains(item.info?.shortname)) return null;
            SendReply(player, "Block!");
            return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
        }
    }
}