using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Rust;
using System.Resources;

namespace Oxide.Plugins
{
    [Info("SeedOre", "modded by North", "2.1.1")]
    public class SeedOre : RustPlugin
    {
        #region Config

        private ConfigData cfg;

        private class ConfigData
        {
            [JsonProperty("ID скина для семечки")]
            public ulong skinId = 1923097247;

            [JsonProperty("Время роста одной стадии (в секундах)")]
            public int growthTime = 30;

            [JsonProperty("Шанс выпадения семечка руды при добыче (%)")]
            public int dropChance = 10;

            [JsonProperty("Множитель добычи")]
            public float gatheringMultiplier = 1.5f;

            [JsonProperty("Автоматически плавить добытые ресурсы")]
            public bool autoSmelt = true;

            [JsonProperty("Разрешить посадку только в грядках")]
            public bool restrictToPlanters = false;

            [JsonProperty("Список руд, которые могут появиться")]
            public List<string> oreList;

            public static ConfigData DefaultConfig()
            {
                return new ConfigData
                {
                    oreList = new List<string>
                    {
                        "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab",
                        "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab"
                    }
                };
            }
        }

        protected override void LoadDefaultConfig() => cfg = ConfigData.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(cfg);
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                PrintError("Ошибка чтения файла конфигурации, используются значения по умолчанию.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        #endregion

        #region Data

        private readonly List<ulong> activeSeeds = new List<ulong>();

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            if (cfg.restrictToPlanters)
                Subscribe(nameof(CanBuild));
            else
                Unsubscribe(nameof(CanBuild));
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner?.skinID != cfg.skinId || !prefab.fullName.Contains("corn.entity")) return null;

            if (target.entity == null ||
                (target.entity.ShortPrefabName != "planter.large.deployed" &&
                 target.entity.ShortPrefabName != "planter.small.deployed"))
            {
                ReplyToPlayer(planner.GetOwnerPlayer(), "Семена можно сажать только в грядки.");
                return false;
            }
            return null;
        }

        private void OnEntitySpawned(GrowableEntity entity)
        {
            if (entity?.skinID != cfg.skinId || entity.ShortPrefabName != "corn.entity") return;

            var player = BasePlayer.FindByID(entity.OwnerID);
            if (player == null) return;

            var orePrefab = cfg.oreList.GetRandom();
            var ore = GameManager.server.CreateEntity(orePrefab, entity.transform.position) as OreResourceEntity;

            if (ore == null) return;

            ore.health = ore.MaxHealth();

            if (ore.stages != null && ore.stages.Count > 2)
            {
                ore.health = ore.stages[2].health; // Устанавливаем здоровье для третьей стадии
            }

            ore.Spawn();
            entity.SetParent(ore, true, true);
            activeSeeds.Add(ore.net.ID.Value);

            NextTick(() =>
            {
                if (entity.GetPlanter() != null)
                {
                    entity.GetPlanter().AddChild(ore);
                }
            });
        }

        private object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser?.GetComponent<ResourceEntity>()?.skinID != cfg.skinId) return null;

            if (cfg.autoSmelt)
            {
                var cookable = item.info.GetComponent<ItemModCookable>();
                if (cookable?.becomeOnCooked != null)
                {
                    var smeltedItem = ItemManager.Create(cookable.becomeOnCooked, (int)(item.amount * cfg.gatheringMultiplier));
                    player.GiveItem(smeltedItem, BaseEntity.GiveItemReason.ResourceHarvested);
                    return true;
                }
            }

            item.amount = (int)(item.amount * cfg.gatheringMultiplier);
            return null;
        }

        #endregion

        #region Helper Methods

        private void ReplyToPlayer(BasePlayer player, string message)
        {
            player?.SendConsoleCommand("chat.add", 0, $"<color=#87CEEB>SeedOre</color>: {message}");
        }

        [ChatCommand("giveseedore")]
        private void GiveSeedCommand(BasePlayer player)
        {
            if (!player.IsAdmin) return;

            var item = ItemManager.CreateByName("seed.corn", 10, cfg.skinId);
            item.name = "Семечко руды";
            if (!player.inventory.GiveItem(item))
                item.Drop(player.transform.position, player.GetDropVelocity());
        }

        #endregion
    }
}
