using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RecyclerHome", "LAGZYA","1.0.1")]
    [Description("Глобальный LAGZYA")] 
    public class RecyclerHome : RustPlugin
    {
        #region Cfg

        private static ConfigData cfg { get; set; }

        private class ConfigData
        {
            [JsonProperty("Название переработчика")] public string RecyclerName = "Домашний переработчик";
            [JsonProperty("Пермишен для крафта")] public string Perm = "recyclerhome.craft";
            [JsonProperty("Пермишен для выдачи")] public string Perm2 = "recyclerhome.give";
            [JsonProperty("Разрешить поднимать чужой переработчик?")] public bool canupnotowner = true;
            [JsonProperty("Разрешить подбирать переработчик в зоне запрета строительства?")] public bool canupbuildingblock = true;
            [JsonProperty("Разрешить ставить переработчик на землю?")] public bool ter = true;
            [JsonProperty("Предметы для крафта")] public Dictionary<string, int> needCraft = new Dictionary<string, int>();
            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData
                {
                    needCraft = new Dictionary<string, int>()
                    {
                        ["wood"] = 100,
                        ["stones"] = 500,
                    }
                };
                return newConfig;
            }
        }
        protected override void LoadDefaultConfig()
        {
            cfg = ConfigData.GetNewConf();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(cfg);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig(); try { cfg = Config.ReadObject<ConfigData>(); }catch { LoadDefaultConfig(); } NextTick(SaveConfig);
        }
        private void OnServerInitialized()
        {
            if(!permission.PermissionExists(cfg.Perm))
                permission.RegisterPermission(cfg.Perm, this);
            if(!permission.PermissionExists(cfg.Perm2))
                permission.RegisterPermission(cfg.Perm2, this);
        }
        #endregion
        [ConsoleCommand("giverecycler")]
        private void GiveRecycler(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, cfg.Perm2))
            {
                SendReply(player, "Нет прав!");
                return;
            }
            if (arg?.Args == null)
            {
                if (player != null)
                    SendReply(player, "Вы делаете что-то не так!");
                else
                    Puts("Вы делаете что-то не так!");
                return;
            }
            
            
            var targetPlayer = BasePlayer.Find(arg.Args[0]);
            if (targetPlayer == null)
            {
                if (player != null)
                    SendReply(player, "Игрок не найден!");
                else
                    Puts("Игрок не найден!");
                return;
            }
            var item = ItemManager.CreateByName("box.wooden.large", 1,1797067639);
            item.name = cfg.RecyclerName;
            if (!targetPlayer.inventory.GiveItem(item))
                item.Drop(targetPlayer.inventory.containerMain.dropPosition, targetPlayer.inventory.containerMain.dropVelocity);
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null || prefab == null || target.player == null) return null;
            if (!prefab.fullName.Contains("box.wooden.large") || planner.skinID != 1797067639) return null;
            if (cfg.ter) return null;
            RaycastHit rHit;
            if (Physics.Raycast(new Vector3(target.position.x,target.position.y+1, target.position.z), Vector3.down, out rHit, 2f, LayerMask.GetMask(new string[] {"Construction"})) && rHit.GetEntity() != null) return null;
            SendReply(target.player, "Нельзя ставить на землю!");
            return false;
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity.ShortPrefabName == "box.wooden.large" && entity.skinID == 1797067639)
            {
                var gameObject = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", entity.transform.position, entity.transform.rotation);
                gameObject.OwnerID = entity.OwnerID;
                gameObject.Spawn();
                entity.Kill();
            }
        }
        [ChatCommand("craftrecycler")]
        private void CraftRecycler(BasePlayer player)
        {
            if(!permission.UserHasPermission(player.UserIDString, cfg.Perm))
            {
                SendReply(player, "Нет прав!");
                return;
            }
            var text = "<size=18><color=green>НЕДОСТАТОЧНО РЕСУРСОВ</color></size>\n";
            var count = 0;
            foreach (var sp in cfg.needCraft)
            {
                var findItem = player.inventory.AllItems().ToList().Find(p => p.info.shortname == sp.Key);
                if (findItem?.amount >= sp.Value)
                    count++;
                else
                    if(findItem == null)
                        text += $"Недостаточно {sp.Key}, еще нужно {sp.Value}\n";
                    else
                        text += $"Недостаточно {sp.Key}, еще нужно {sp.Value-findItem.amount}\n";
            }
            if (count == cfg.needCraft.Count)
            {
                foreach (var sp in cfg.needCraft)
                    player.inventory.Take(null, ItemManager.FindItemDefinition(sp.Key).itemid, sp.Value);
                var item = ItemManager.CreateByName("box.wooden.large", 1,1797067639);
                item.name = cfg.RecyclerName;
                if (!player.inventory.GiveItem(item))
                {
                    SendReply(player, "Инвентарь был полон переработчик выпал на земелю!");
                    item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                }
                else
                    SendReply(player, "Вы скрафтили переработчик!"); 
            }
            else
                SendReply(player, text);
        }
        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (info?.HitEntity?.OwnerID != 0 && info.HitEntity.ShortPrefabName == "recycler_static")
            {
                if (player.IsBuildingBlocked() && !cfg.canupbuildingblock)                 
                {
                    SendReply(player, "Вы в зоне запрета строительства!");
                    return null;
                }
                if (info.HitEntity.OwnerID != player.userID && !cfg.canupnotowner)
                {
                    SendReply(player, "Вы не владелец переработчика");
                    return null;
                }
                info.HitEntity.Kill();
                var item = ItemManager.CreateByName("box.wooden.large", 1,1797067639);
                item.name = cfg.RecyclerName;
                if (!player.inventory.GiveItem(item))
                {
                    SendReply(player, "Инвентарь был полон переработчик выпал на земелю!");
                    item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                }
                else
                    SendReply(player, "Вы подобрали переработчик!");
            }
            return null;
        }
    }
}