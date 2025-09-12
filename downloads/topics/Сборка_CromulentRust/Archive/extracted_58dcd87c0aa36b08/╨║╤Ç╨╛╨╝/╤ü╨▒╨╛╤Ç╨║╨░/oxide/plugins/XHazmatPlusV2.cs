using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("XHazmatPlusV2", "Monster", "2.0.6")]
    class XHazmatPlusV2 : RustPlugin
    {
        [PluginReference] Plugin XL96Plus;
        private void OnServerInitialized()
        {
            foreach (var hazmatSetting in config.Hazmat.Values)
            {
                if (hazmatSetting.poperm && !string.IsNullOrEmpty(hazmatSetting.Permissions))
                {
                    string permissionName = $"{this.Title}.{hazmatSetting.Permissions.ToLower()}";

                    if (!permission.PermissionExists(permissionName))
                        permission.RegisterPermission(permissionName, this);
                }
            }
            PrintWarning("\n-----------------------------\n" +
            "     Author - Monster\n" +
            "     VK - vk.com/idannopol\n" +
            "     Discord - Monster#4837\n" +
            "     Config - v.3910\n" +
            "-----------------------------");
        }

        private class HazmatConfig
        {

            [JsonProperty("Общие настройки")]
            public GeneralSettings Setting;

            internal class Crates
            {
                [JsonProperty("Имя ящика")] public string NameCrate;
                [JsonProperty("Шанс выпадения")] public float ChanceDrop;
            }

            public static HazmatConfig GetNewConfiguration()
            {
                return new HazmatConfig
                {
                    Setting = new GeneralSettings
                    {
                        PluginCLoot = false
                    },
                    Hazmat = new Dictionary<ulong, HazmatSetting>
                    {
                        [2301872901] = new HazmatSetting("Улучшенный хазмат", "hazmatsuit", 1, 0.9f, 0.05f, new Dictionary<string, int> { ["cloth"] = 100, ["leather"] = 100 }, true, true, true, true, true, new List<Crates> { new Crates { NameCrate = "crate_normal_2", ChanceDrop = 35.0f } }),
                        [2301873172] = new HazmatSetting("Двухслойный хазмат", "hazmatsuit_scientist", 3, 0.75f, 0.15f, new Dictionary<string, int> { ["cloth"] = 100, ["leather"] = 100 }, true, true, true, true, true, new List<Crates> { new Crates { NameCrate = "crate_normal_2", ChanceDrop = 25.0f } }),
                        [2301873842] = new HazmatSetting("Трехслойный хазмат", "hazmatsuit_scientist_peacekeeper", 5, 0.5f, 0.2f, new Dictionary<string, int> { ["cloth"] = 100, ["leather"] = 100 }, true, true, true, true, true, new List<Crates> { new Crates { NameCrate = "crate_normal_2", ChanceDrop = 15.0f } }),
                        [2301874415] = new HazmatSetting("Бронированный хазмат", "scientistsuit_heavy", 0, 0.25f, 0.4f, new Dictionary<string, int> { ["cloth"] = 100, ["leather"] = 100 }, true, true, true, true, true, new List<Crates> { new Crates { NameCrate = "crate_normal_2", ChanceDrop = 5.0f } })
                    }
                };
            }

            internal class HazmatSetting
            {
                [JsonProperty("Имя улучшенного хазмата")] public string NameHazmat;
                [JsonProperty("Внешний вид улучшенного хазмата")] public string ShortnameHazmat;
                [JsonProperty("Кол-во снижаемых ед. радиации. 0 - не накапливает. > 0 - снижает радиацию на указанное кол-во ед.")] public int ValueRadiation;
                [JsonProperty("Процент получаемого урона в улучшеном хазмате. 1.0 - 100%")] public float ValueDamage;
                [JsonProperty("Процент урона от шипов. 1.0 - 100%")] public float ValueDamageThorns;
                [JsonProperty("Список ресурсов после переработки")] public Dictionary<string, int> ItemList;

                [JsonProperty("Включить доступ по пермишону?")] public bool poperm = false;
                [JsonProperty("Пермишон")] public string Permissions = "";

                [JsonProperty("Включить кастомные предметы после переработке улучшенного хазмата")] public bool RecyclerHazmat;
                [JsonProperty("Включить выпадение хазмата из ящиков с определенным шансом")] public bool CrateHazmat;
                [JsonProperty("Включить снижение/не накопление радиации")] public bool RadiationHazmat;
                [JsonProperty("Включить процент защиты от любого урона")] public bool DamageHazmat;
                [JsonProperty("Включить урон от шипов")] public bool DamageThorns;

                [JsonProperty("Настройка шанса выпадения из ящиков и бочек")]
                public List<Crates> Crate;

                public HazmatSetting(string namehazmat, string shortnamehazmat, int valueradiation, float valuedamage, float valuedamagethorns, Dictionary<string, int> itemlist, bool recyclerhazmat, bool cratehazmat, bool radiationhazmat, bool damagehazmat, bool damagethorns, List<Crates> crates)
                {
                    NameHazmat = namehazmat; ShortnameHazmat = shortnamehazmat; ValueRadiation = valueradiation; ValueDamage = valuedamage; ValueDamageThorns = valuedamagethorns; ItemList = itemlist; RecyclerHazmat = recyclerhazmat; CrateHazmat = cratehazmat; RadiationHazmat = radiationhazmat; DamageHazmat = damagehazmat; DamageThorns = damagethorns; Crate = crates;
                }
            }
            [JsonProperty("Список улучшенных хазматов")]
            public Dictionary<ulong, HazmatSetting> Hazmat;
            internal class GeneralSettings
            {
                [JsonProperty("Есть плагин на кастомный лут")] public bool PluginCLoot;
                [JsonProperty("Fix XL96PLUS?")] public bool fixxl96 = true;
            }
        }

        private void LootSpawn(LootContainer lootContainer)
        {
            foreach (var hazmat in config.Hazmat)
                foreach (var crate in hazmat.Value.Crate)
                {
                    if (crate.NameCrate == lootContainer.ShortPrefabName && config.Hazmat[hazmat.Key].CrateHazmat)
                        if (UnityEngine.Random.Range(0, 100) <= crate.ChanceDrop)
                        {
                            Item item = ItemManager.CreateByName(hazmat.Value.ShortnameHazmat, 1, hazmat.Key);
                            item.name = hazmat.Value.NameHazmat;

                            item.MoveToContainer(lootContainer.inventory);
                        }
                }
        }
        protected override void SaveConfig() => Config.WriteObject(config);

        private void OnLootSpawn(LootContainer lootContainer)
        {
            if (config.Setting.PluginCLoot)
                NextTick(() => LootSpawn(lootContainer));
            else
                LootSpawn(lootContainer);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null) return;

            BasePlayer player = entity.ToPlayer();
            BasePlayer target = info.InitiatorPlayer;
            Item weapon = target?.GetActiveItem();
            if (player == null || player is NPCPlayer) return;
            foreach (var customitem in player.inventory.containerWear.itemList.ToList())
            {
                if (config.Hazmat.ContainsKey(customitem.skin))
                {
                    var hazmat = config.Hazmat[customitem.skin];
                    if (hazmat.poperm && !string.IsNullOrEmpty(hazmat.Permissions))
                    {
                        if (!permission.UserHasPermission(player.UserIDString, $"{this.Title}.{hazmat.Permissions}"))
                        {
                            Effect effect = new Effect("ASSETS/BUNDLED/PREFABS/FX/INVITE_NOTICE.PREFAB".ToLower(), player, 0, new Vector3(), new Vector3());
                            EffectNetwork.Send(effect, player.Connection);
                            return;
                        }
                    }
                    if (config.Setting.fixxl96 == true)
                    {
                        if (target != null)
                        {
                            if (info.Weapon != null && info.Weapon.name != null && XL96Plus != null)
                            {
                                if (info.Weapon.name.Contains("l96"))
                                {
                                    if (!permission.UserHasPermission(player.UserIDString, "xl96plus.ignore"))
                                    {
                                        if (weapon.name == (string)XL96Plus?.Call("GetL96ConfigValue", "NameL96"))
                                        {
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (hazmat.RadiationHazmat)
                        if (info.damageTypes?.Get(Rust.DamageType.Radiation) > 0)
                            if (hazmat.ValueRadiation == 0)
                                player.metabolism.radiation_poison.value = hazmat.ValueRadiation;
                            else
                                player.metabolism.radiation_poison.value -= hazmat.ValueRadiation;

                    if (hazmat.DamageHazmat)
                        info.damageTypes.ScaleAll(hazmat.ValueDamage);

                    if (hazmat.DamageThorns)
                        if (target != null) target.Hurt(info.damageTypes.Total() * hazmat.ValueDamageThorns);

                }
            }
        }


        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<HazmatConfig>();
            }
            catch
            {
                PrintWarning("Configuration read error! Creating a default configuration!");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        private HazmatConfig config;

        private object OnItemRecycle(Item item, Recycler recycler)
        {
            if (config.Hazmat.ContainsKey(item.skin) && config.Hazmat[item.skin].RecyclerHazmat)
            {
                var hazmat = config.Hazmat[item.skin];

                if (item.info.shortname.Equals(hazmat.ShortnameHazmat) && item.skin.Equals(item.skin))
                {
                    foreach (var items in hazmat.ItemList)
                    {
                        Item itemc = ItemManager.CreateByName(items.Key, items.Value);
                        recycler.MoveItemToOutput(itemc);
                    }

                    item.RemoveFromWorld();
                    item.RemoveFromContainer();

                    return false;
                }
            }

            return null;
        }

        object CanWearItem(PlayerInventory inventory, Item item, int targetSlot)
        {
            if (config.Hazmat.ContainsKey(item.skin) && IsAnyItemWear(inventory.baseEntity))
            {
                SendReply(inventory.baseEntity, $"Нельзя надеть более одной особой одежды");
                return false;
            }
            return null;
        }

        bool IsAnyItemWear(BasePlayer player)
        {
            foreach (var item in player.inventory.containerWear.itemList)
                if (config.Hazmat.ContainsKey(item.skin))
                    return true;
            return false;
        }
        protected override void LoadDefaultConfig() => config = HazmatConfig.GetNewConfiguration();



        [ConsoleCommand("hz_give")]
        void HazmatUPGive(ConsoleSystem.Arg args)
        {
            if (args.Player() == null || args.Player().IsAdmin)
            {
                BasePlayer player = BasePlayer.FindByID(ulong.Parse(args.Args[0]));
                ulong skinID = ulong.Parse(args.Args[1]);
                var hazmat = config.Hazmat[skinID];

                Item item = ItemManager.CreateByName(hazmat.ShortnameHazmat, 1, skinID);
                item.name = hazmat.NameHazmat;
                player.GiveItem(item);
            }
        }

    }
}