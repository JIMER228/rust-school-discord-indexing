using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("zEcoLootUI", "EcoSmile", "2.0.8")]
    class zEcoLootUI : RustPlugin
    {
        string perm = "zecolootui.edit";

        Dictionary<string, string> prefabcontaner = new Dictionary<string, string>()
        {
            ["assets/bundled/prefabs/radtown/crate_basic.prefab"] = "Маленький деревянный ящик",
            ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = "Элитный ящик",
            ["assets/bundled/prefabs/radtown/crate_mine.prefab"] = "Ящик из пещер",
            ["assets/bundled/prefabs/radtown/crate_tools.prefab"] = "Ящик с инструментами",
            ["assets/bundled/prefabs/radtown/crate_normal.prefab"] = "Зеленый военный ящик",
            ["assets/bundled/prefabs/radtown/crate_normal_2.prefab"] = "Обычный деревянный ящик",
            ["assets/bundled/prefabs/radtown/crate_normal_2_food.prefab"] = "Ящик с едой",
            ["assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab"] = "Ящик с медециной",
            ["assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab"] = "Большой подводный ящик",
            ["assets/bundled/prefabs/radtown/crate_underwater_basic.prefab"] = "Малый подводный ящик",
            ["assets/bundled/prefabs/radtown/foodbox.prefab"] = "Корзинка с едой",
            ["assets/bundled/prefabs/radtown/loot_barrel_1.prefab"] = "Синяя бочка у дорог",
            ["assets/bundled/prefabs/radtown/loot_barrel_2.prefab"] = "Ржавая бочка у дорог",
            ["assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab"] = "Ржавая бочка на РТ",
            ["assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab"] = "Синяя бочка на РТ",
            ["assets/bundled/prefabs/radtown/minecart.prefab"] = "Тележка с углем",
            ["assets/prefabs/npc/m2bradley/bradley_crate.prefab"] = "Бредли ящик",
            ["assets/bundled/prefabs/radtown/oil_barrel.prefab"] = "Нефтяная бочка",
            ["assets/prefabs/npc/patrol helicopter/heli_crate.prefab"] = "Вертолетный ящик",
            ["assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab"] = "Ящик с электронным замком",
            ["assets/prefabs/misc/supply drop/supply_drop.prefab"] = "Аир дроп",
            ["assets/bundled/prefabs/autospawn/resource/loot/trash-pile-1.prefab"] = "Корзинка с припасами",
            ["assets/bundled/prefabs/radtown/vehicle_parts.prefab"] = "Ящик с частями",
            ["assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate_oilrig.prefab"] = "Ящик с электронным замком с Нефтевышки",
            ["assets/prefabs/misc/xmas/sleigh/presentdrop.prefab"] = "Новогодний дроп",
            ["assets/bundled/prefabs/radtown/dmloot/dm ammo.prefab"] = "DM AMMO",
            ["assets/bundled/prefabs/radtown/dmloot/dm c4.prefab"] = "DM C4",
            ["assets/bundled/prefabs/radtown/dmloot/dm construction tools.prefab"] = "DM CONSTRUCTION TOOLS",
            ["assets/bundled/prefabs/radtown/dmloot/dm res.prefab"] = "DM RES",
            ["assets/bundled/prefabs/radtown/dmloot/dm food.prefab"] = "DM FOOD",
            ["assets/bundled/prefabs/radtown/dmloot/dm tier1 lootbox.prefab"] = "DM TIER1 LOOTBOX",
            ["assets/bundled/prefabs/radtown/dmloot/dm tier2 lootbox.prefab"] = "DM TIER2 LOOTBOX",
            ["assets/bundled/prefabs/radtown/dmloot/dm tier3 lootbox.prefab"] = "DM TIER3 LOOTBOX",
            ["assets/bundled/prefabs/radtown/loot_component_test.prefab"] = "LOOT_COMPONENT_TEST",
            ["assets/content/props/roadsigns/roadsign1.prefab"] = "ROADSIGN 1",
            ["assets/content/props/roadsigns/roadsign2.prefab"] = "ROADSIGN 2",
            ["assets/content/props/roadsigns/roadsign3.prefab"] = "ROADSIGN 3",
            ["assets/content/props/roadsigns/roadsign4.prefab"] = "ROADSIGN 4",
            ["assets/content/props/roadsigns/roadsign5.prefab"] = "ROADSIGN 5",
            ["assets/content/props/roadsigns/roadsign6.prefab"] = "ROADSIGN 6",
            ["assets/content/props/roadsigns/roadsign7.prefab"] = "ROADSIGN 7",
            ["assets/content/props/roadsigns/roadsign8.prefab"] = "ROADSIGN 8",
            ["assets/content/props/roadsigns/roadsign9.prefab"] = "ROADSIGN 9",
            ["assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab"] = "GIFTBOX_LOOT",
            ["assets/prefabs/resource/diesel barrel/diesel_barrel_world.prefab"] = "DIESEL_BARREL_WORLD",
            ["assets/scripts/entity/misc/visualstoragecontainer/visualshelvestest.prefab"] = "VISUALSHELVESTEST",
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_medical.prefab"] = "Подводный ящик с Медециной",
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"] = "Подводный ящик с Едой 1",
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_food_2.prefab"] = "Подводный ящик с Едой 2",
            ["assets/bundled/prefabs/radtown/underwater_labs/vehicle_parts.prefab"] = "Подводный ящик с запчастями",
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab"] = "Подводный ящик с Амуницией",
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_fuel.prefab"] = "Подводный ящик с Топливом",
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_tools.prefab"] = "Инструментальный подводный ящик",
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab"] = "Элитный подводный ящик",
            ["assets/bundled/prefabs/radtown/underwater_labs/tech_parts_2.prefab"] = "Подводный ящик с Тех.Деталями 2",
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab"] = "Военный подводный ящик",
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab"] = "Подводный Синий ящик",
            ["assets/bundled/prefabs/radtown/underwater_labs/tech_parts_1.prefab"] = "Подводный ящик с Тех.Деталями 1",
            ["assets/content/vehicles/trains/wagons/subents/wagon_crate_normal.prefab"] = "Военный ящик с Вагона.",
            ["assets/content/vehicles/trains/wagons/subents/wagon_crate_normal_2.prefab"] = "Деревянный ящик с Вагона",
            ["assets/content/vehicles/trains/wagons/subents/wagon_crate_normal_2_food.prefab"] = "Продовольственный с Вагона",
            ["assets/content/vehicles/trains/wagons/subents/wagon_crate_normal_2_medical.prefab"] = "Медицинский ящик с Вагона",
        };


        PluginConfig config;

        public class CustomItem
        {
            public string ShortName;
            public ulong SkinID;
            public string CustomName;
            public int MaxAmount;
            public int MinAmount;
            public int DropChance;
        }

        public class PluginConfig
        {
            [JsonProperty("Пресеты кастомных предметов")]
            public List<CustomItem> customItems;
            [JsonProperty("Настройка контейнеров")]
            public Dictionary<string, LootSetting> crateSetting;
        }

        public class LootSetting
        {
            [JsonProperty("Исключить данный тип ящиков из заполнения?")]
            public bool DisableCrate = false;
            [JsonProperty("Рандомить количество слотов в ящике? (false - будет браться максимум)")]
            public bool RandomItemAmount;
            [JsonProperty("Максимум слотов")]
            public int MaxItemInCrate;
            [JsonProperty("Минимум слотов")]
            public int MinItemInCrate;
            [JsonProperty("Удалять ящик если не был залутан до конца?")]
            public bool DeleteCrate;
            [JsonProperty("Интервал удаления ящика (минуты)")]
            public int DeleteTimer;
        }

        Dictionary<string, LootSetting> ConfigSetup()
        {
            Dictionary<string, LootSetting> setting = new Dictionary<string, LootSetting>();
            foreach (var value in prefabcontaner)
            {
                var container = GameManager.server.FindPrefab(value.Key)?.GetComponent<LootContainer>();
                setting.Add(value.Value, new LootSetting()
                {
                    DisableCrate = false,
                    RandomItemAmount = false,
                    MaxItemInCrate = container.inventorySlots,
                    MinItemInCrate = container.inventorySlots,
                    DeleteCrate = true,
                    DeleteTimer = 5
                });
            }

            return setting;
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig()
            {
                customItems = new List<CustomItem>()
                {
                    new CustomItem()
                    {
                        ShortName = "",
                        SkinID = 0,
                        CustomName = "",
                        MinAmount = 0,
                        MaxAmount = 1,
                        DropChance = 0

                    },
                    new CustomItem()
                    {
                        ShortName = "",
                        SkinID = 0,
                        CustomName = "",
                        MinAmount = 0,
                        MaxAmount = 1,
                        DropChance = 0
                    },
                },
                crateSetting = ConfigSetup()
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            foreach (KeyValuePair<string, string> value in prefabcontaner.Where(x => !config.crateSetting.ContainsKey(x.Value)))
            {
                PrintWarning($"Added New crate {value.Value}");
                var container = GameManager.server.FindPrefab(value.Key)?.GetComponent<LootContainer>();
                config.crateSetting.Add(value.Value, new LootSetting()
                {
                    DisableCrate = false,
                    RandomItemAmount = false,
                    MaxItemInCrate = container.inventorySlots,
                    MinItemInCrate = container.inventorySlots,
                    DeleteCrate = true,
                    DeleteTimer = 5
                });
            }
            if (config.customItems == null)
            {
                config.customItems = new List<CustomItem>()
                {
                    new CustomItem()
                    {
                        ShortName = "",
                        SkinID = 0,
                        CustomName = "",
                        MinAmount = 1,
                        MaxAmount = 1,
                        DropChance = 0
                    },
                    new CustomItem()
                    {
                        ShortName = "",
                        SkinID = 0,
                        CustomName = "",
                        MinAmount = 1,
                        MaxAmount = 1,
                        DropChance = 0
                    },
                };
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        bool init = false;

        Dictionary<string, List<ItemSetting>> lootSettings = new Dictionary<string, List<ItemSetting>>();

        void OnServerInitialized()
        {
            version = $"{this.Version.Major}_{this.Version.Minor}_{this.Version.Patch}";
            ChekNewItem();
            GetDefaultLoot();
            RestoreBase();
            LootTimer();
            permission.RegisterPermission(perm, this);
            PrintWarning($"Registered permission {perm}");

            timer.In(10f, () =>
            {
                var containers = BaseNetworkable.serverEntities.OfType<LootContainer>();
                filling = ServerMgr.Instance.StartCoroutine(FirsLoad(containers));
            });

        }


        Coroutine filling;
        IEnumerator FirsLoad(IEnumerable<LootContainer> containers)
        {
            PrintWarning($"Started Initialize Loot table!!!");
            yield return null;
            foreach (var data in prefabcontaner)
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile($"zEcoLootUI/{data.Value}"))
                    yield return lootSettings[data.Key] = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{data.Value}");
                else
                    yield return null;
            }
            foreach (var container in containers)
            {
                if (!prefabcontaner.ContainsKey(container.PrefabName))
                {
                    PrintWarning($"Container not found {container.PrefabName}");
                    continue;
                }
                if (!lootSettings.ContainsKey(container.PrefabName))
                {
                    yield return lootSettings[container.PrefabName] = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{prefabcontaner[container.PrefabName]}");
                }
            }
            int initProcess = 0;
            float cdTime = UnityEngine.Time.realtimeSinceStartup;
            foreach (var container in containers)
            {
                yield return ServerMgr.Instance.StartCoroutine(FillContaner(container));
                initProcess++;
                if (cdTime < UnityEngine.Time.realtimeSinceStartup)
                {
                    PrintWarning($"Initialize loot table please wait! [{initProcess}/{containers.Count()}]");
                    cdTime = UnityEngine.Time.realtimeSinceStartup + 1f;
                }
            }
            PrintWarning($"Initialize loot table please wait! [{initProcess}/{containers.Count()}]");
            PrintWarning($"Initialize Loot table has been ended!!!");
            init = true;
        }

        string version = $"";

        private Dictionary<BasePlayer, bool> visionImage = new Dictionary<BasePlayer, bool>();

        void RestoreBase()
        {
            foreach (var prefab in prefabcontaner)
            {
                List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{prefab.Value}");
                foreach (var loot in itemlist)
                {
                    if (loot.version == $"{this.Version.Major}_{this.Version.Minor}_{this.Version.Patch}") continue;
                    loot.version = version;

                    if (loot.shortname == "chocholate")
                        loot.shortname = "chocolate";

                    var it = ItemManager.FindItemDefinition(loot.shortname);

                    if (it.condition.enabled && (loot.Condition == 0f || loot.Condition > 100f))
                    {
                        var fraction = UnityEngine.Random.Range(it.condition.foundCondition.fractionMin, it.condition.foundCondition.fractionMax);
                        var condition = fraction * 100f;
                        loot.Condition = condition;
                    }
                    loot.category = it.category;
                    loot.HasCondition = it.condition.enabled;
                }
                Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefab.Value}", itemlist);
            }
        }
        bool isUloading = false;
        void Unload()
        {
            isUloading = true;
            if (filling != null)
                ServerMgr.Instance.StopCoroutine(filling);

            foreach (var pl in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(pl, "zEcoLootUI.Main.UI");
            }

            foreach (var ent in BaseNetworkable.serverEntities.OfType<LootContainer>())
            {
                ent.SpawnLoot();
            }
        }

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, perm)) return;
            if (!(info.HitEntity is LootContainer)) return;
            var ent = info.HitEntity;
            if (ent.ShortPrefabName == "stocking_large_deployed" || ent.ShortPrefabName == "stocking_small_deployed") return;
            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI");
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Parent = "Overlay",
                Name = "zEcoLootUI.Main.UI",
                Components =
                {
                    new CuiImageComponent{Color = "0 0 0 0.85", Sprite = "assets/content/ui/ui.background.transparent.radial.psd", Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiNeedsCursorComponent{ },
                    new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                }
            });
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI",
                Name = $"zEcoLootUI.Main.UI.text",
                Components =
                    {
                        new CuiTextComponent{Text = $"Редактируется:\n{(prefabcontaner.ContainsKey(info.HitEntity.PrefabName) ? prefabcontaner[info.HitEntity.PrefabName] : info.HitEntity.ShortPrefabName)}", Align = TextAnchor.MiddleCenter, FontSize = 22},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0.9", AnchorMax = $"0.5 0.9", OffsetMin = $"-250 -64", OffsetMax = $"250 80",}
                    }
            });

            CuiHelper.AddUi(player, container);
            DrawPanel(player, info.HitEntity.PrefabName);
            ServerMgr.Instance.StartCoroutine(FillContaner(ent as LootContainer));
        }

        void DrawPanel(BasePlayer player, string prefab)
        {
            if (coroutine != null)
                ServerMgr.Instance.StopCoroutine(coroutine);

            var container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI.BG");
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI",
                Name = "zEcoLootUI.Main.UI.BG",
                Components =
                {
                    new CuiImageComponent{Color = "0 0 0 0.0"},
                    new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                }
            });
            CuiHelper.AddUi(player, container);
            DrawCrateitem(player, prefab);
        }

        void DrawCrateitem(BasePlayer player, string prefab, int page = 0)
        {
            ServerMgr.Instance.StartCoroutine(DrawCrateItem(player, prefab, page));
        }

        Coroutine coroutine;

        [ConsoleCommand("AddSlot")]
        void AddSlot(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            var prefab = string.Join(" ", arg.Args.Skip(1));
            var type = arg.Args[0];

            if (!prefabcontaner.ContainsKey(prefab))
            {
                PrintError($"Контейнер не найден # 00. Сообщите автору EcoSmile: {prefab}");
                return;
            }
            if (!config.crateSetting.ContainsKey(prefabcontaner[prefab]))
            {
                PrintError($"Контейнер не найден # 002. Сообщите автору EcoSmile: {prefab}");
                return;
            }

            if (type == "min")
            {
                config.crateSetting[prefabcontaner[prefab]].MinItemInCrate++;
                if (config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate < config.crateSetting[prefabcontaner[prefab]].MinItemInCrate)
                    config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate = config.crateSetting[prefabcontaner[prefab]].MinItemInCrate;
                Config.WriteObject(config);
            }
            else if (type == "max")
            {
                config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate++;
                Config.WriteObject(config);
            }
            CuiHelper.DestroyUi(player, type == "min" ? $"zEcoLootUI.Main.UI.BG.Slot.MinText" : $"zEcoLootUI.Main.UI.BG.Slot.MaxText");
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.BG",
                Name = type == "min" ? $"zEcoLootUI.Main.UI.BG.Slot.MinText" : $"zEcoLootUI.Main.UI.BG.Slot.MaxText",
                Components =
                    {
                        new CuiTextComponent{Text = type == "min" ? $"{config.crateSetting[prefabcontaner[prefab]].MinItemInCrate}" : $"{config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate}", Align = TextAnchor.MiddleCenter, FontSize = 22},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = type == "min" ? $"0.15 0.98" : "0.15 0.935", AnchorMax =type == "min" ? $"0.15 0.98" : "0.15 0.935", OffsetMin = $"-30 -15", OffsetMax = $"30 15",}
                    }
            });
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("RemSlot")]
        void RemSlot(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            var prefab = string.Join(" ", arg.Args.Skip(1));
            if (!prefabcontaner.ContainsKey(prefab))
            {
                PrintError($"Not Fount (RemSlot): {prefab}");
                return;
            }
            var type = arg.Args[0];
            if (type == "min")
            {
                config.crateSetting[prefabcontaner[prefab]].MinItemInCrate--;
                if (config.crateSetting[prefabcontaner[prefab]].MinItemInCrate < 1)
                    config.crateSetting[prefabcontaner[prefab]].MinItemInCrate = 1;
                Config.WriteObject(config);
            }
            else if (type == "max")
            {
                config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate--;
                if (config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate < 1)
                    config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate = 1;
                if (config.crateSetting[prefabcontaner[prefab]].MinItemInCrate > config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate)
                    config.crateSetting[prefabcontaner[prefab]].MinItemInCrate = config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate;
            }
            CuiHelper.DestroyUi(player, type == "min" ? $"zEcoLootUI.Main.UI.BG.Slot.MinText" : $"zEcoLootUI.Main.UI.BG.Slot.MaxText");
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.BG",
                Name = type == "min" ? $"zEcoLootUI.Main.UI.BG.Slot.MinText" : $"zEcoLootUI.Main.UI.BG.Slot.MaxText",
                Components =
                    {
                        new CuiTextComponent{Text = type == "min" ? $"{config.crateSetting[prefabcontaner[prefab]].MinItemInCrate}" : $"{config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate}", Align = TextAnchor.MiddleCenter, FontSize = 22},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = type == "min" ? $"0.15 0.98" : "0.15 0.935", AnchorMax = type == "min" ? $"0.15 0.98" : "0.15 0.935", OffsetMin = $"-30 -15", OffsetMax = $"30 15",}
                    }
            });
            CuiHelper.AddUi(player, container);
        }

        IEnumerator DrawCrateItem(BasePlayer player, string prefab, int page = 0)
        {
            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI.BG");
            if (!visionImage.ContainsKey(player))
                visionImage.Add(player, false);
            //List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{prefabcontaner[prefab]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintWarning($"Повторная загрузка: zEcoLootUI/{prefabcontaner[prefab]}.json");
                yield return lootSettings[prefab] = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{prefabcontaner[prefab]}");
            }
            var itemList = lootSettings[prefab];
            var container = new CuiElementContainer();

            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI",
                Name = "zEcoLootUI.Main.UI.BG",
                Components =
                {
                    new CuiImageComponent{Color = "0 0 0 0.0"},
                    new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                }
            });

            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.BG",
                Name = "Close.btn",
                Components =
                {
                    new CuiButtonComponent{Sprite = "assets/icons/close.png", Close = "zEcoLootUI.Main.UI"},
                    new CuiRectTransformComponent{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-45 -45", OffsetMax = "-15 -15"}
                }
            });

            container.Add(new CuiButton()
            {
                Button = { Command = $"AddSlot min {prefab}", Color = "0 1 0 0.3" },
                Text = { Text = "+Slot", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.1 0.98", AnchorMax = "0.1 0.98", OffsetMin = "-30 -15", OffsetMax = "30 15" }
            }, "zEcoLootUI.Main.UI.BG", "AddSlot");


            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.BG",
                Name = $"zEcoLootUI.Main.UI.BG.Slot.MinText",
                Components =
                    {
                        new CuiTextComponent{Text = $"{config.crateSetting[prefabcontaner[prefab]].MinItemInCrate}", Align = TextAnchor.MiddleCenter, FontSize = 22},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.15 0.98", AnchorMax = $"0.15 0.98", OffsetMin = $"-30 -15", OffsetMax = $"30 15",}
                    }
            });

            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.BG",
                Name = $"zEcoLootUI.Main.UI.BG.Slot",
                Components =
                    {
                        new CuiTextComponent{Text = $"MIN: ", Align = TextAnchor.MiddleCenter, FontSize = 22},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.98", AnchorMax = $"0.05 0.98", OffsetMin = $"-30 -15", OffsetMax = $"30 15",}
                    }
            });

            container.Add(new CuiButton()
            {
                Button = { Command = $"RemSlot min {prefab}", Color = "0 1 0 0.3" },
                Text = { Text = "-Slot", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.2 0.98", AnchorMax = "0.2 0.98", OffsetMin = "-30 -15", OffsetMax = "30 15" }
            }, "zEcoLootUI.Main.UI.BG", "RemSlot");

            //////////////////////////////////////////////////////////////////////////////////////////////
            ///
            container.Add(new CuiButton()
            {
                Button = { Command = $"AddSlot max {prefab}", Color = "0 1 0 0.3" },
                Text = { Text = "+Slot", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.1 0.935", AnchorMax = "0.1 0.935", OffsetMin = "-30 -15", OffsetMax = "30 15" }
            }, "zEcoLootUI.Main.UI.BG", "AddSlot");


            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.BG",
                Name = $"zEcoLootUI.Main.UI.BG.Slot.MaxText",
                Components =
                    {
                        new CuiTextComponent{Text = $"{config.crateSetting[prefabcontaner[prefab]].MaxItemInCrate}", Align = TextAnchor.MiddleCenter, FontSize = 22},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.15 0.935", AnchorMax = $"0.15 0.935", OffsetMin = $"-30 -15", OffsetMax = $"30 15",}
                    }
            });

            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.BG",
                Name = $"zEcoLootUI.Main.UI.BG.Slot",
                Components =
                    {
                        new CuiTextComponent{Text = $"MAX: ", Align = TextAnchor.MiddleCenter, FontSize = 22},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.935", AnchorMax = $"0.05 0.935", OffsetMin = $"-30 -15", OffsetMax = $"30 15",}
                    }
            });

            container.Add(new CuiButton()
            {
                Button = { Command = $"RemSlot max {prefab}", Color = "0 1 0 0.3" },
                Text = { Text = "-Slot", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.2 0.935", AnchorMax = "0.2 0.935", OffsetMin = "-30 -15", OffsetMax = "30 15" }
            }, "zEcoLootUI.Main.UI.BG", "RemSlot");
            //////////////////////////////////////////////////////////////////////////////////

            container.Add(new CuiButton()
            {
                Button = { Command = $"Add {prefab}", Color = "0 1 0 0.3" },
                Text = { Text = "ДОБАВИТЬ", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.4 0.05", AnchorMax = "0.4 0.05", OffsetMin = "-60 -15", OffsetMax = "60 15" }
            }, "zEcoLootUI.Main.UI.BG", "AddBtn");

            container.Add(new CuiButton()
            {
                Button = { Command = $"hidelootimage {prefab}", Color = "1 1 1 0.3" },
                Text = { Text = visionImage.ContainsKey(player) ? visionImage[player] ? "Показать картинки" : "Скрыть картинки" : "Скрыть картинки", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.5 0.05", AnchorMax = "0.5 0.05", OffsetMin = "-60 -20", OffsetMax = "40 20" }
            }, "zEcoLootUI.Main.UI.BG", "HideImageBtn");

            container.Add(new CuiButton()
            {
                Button = { Command = $"AddInv {prefab}", Color = "1 1 1 0.2" },
                Text = { Text = "ДОБАВИТЬ ИЗ ИНВЕТАРЯ", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.6 0.05", AnchorMax = "0.6 0.05", OffsetMin = "-80 -15", OffsetMax = "80 15" }
            }, "zEcoLootUI.Main.UI.BG", "AddBtn");

            container.Add(new CuiButton()
            {
                Button = { Command = $"clearall {prefab}", Color = "1 0 0 0.3" },
                Text = { Text = "Очистить полностью", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.4 0.05", AnchorMax = "0.4 0.05", OffsetMin = "-215 -15", OffsetMax = "-65 15" }
            }, "zEcoLootUI.Main.UI.BG", "ClearBtn");

            CuiHelper.AddUi(player, container);


            var starthorisontal = 0.05f;
            var startvertical = 0.8f;
            var horisontal = starthorisontal;
            var vertical = startvertical;
            var widthBtn = 32;
            var hightBtn = 32;
            int count = 0;
            int i = page == 0 ? 0 : page * 60;
            foreach (var item in itemList.Skip(page * 60).Take(60))
            {
                var itemContainer = new CuiElementContainer();

                if (!visionImage[player])
                {
                    itemContainer.Add(new CuiElement()
                    {
                        Parent = "zEcoLootUI.Main.UI.BG",
                        Name = $"zEcoLootUI.Main.UI.BG.{i}",
                        Components =
                            {
                                new CuiImageComponent{ ItemId = ItemManager.FindItemDefinition(item.shortname).itemid, SkinId = item.skin, Color = "1 1 1 1" },
                                new CuiRectTransformComponent { AnchorMin = $"{horisontal} {vertical}", AnchorMax = $"{horisontal} {vertical}", OffsetMin = $"-{widthBtn} -{hightBtn}", OffsetMax = $"{widthBtn} {hightBtn}",}
                            }
                    });
                }
                else
                {
                    itemContainer.Add(new CuiElement()
                    {
                        Parent = "zEcoLootUI.Main.UI.BG",
                        Name = $"zEcoLootUI.Main.UI.BG.{i}",
                        Components =
                    {
                        new CuiImageComponent{ Color = "1 1 1 0.1" },
                        new CuiRectTransformComponent { AnchorMin = $"{horisontal} {vertical}", AnchorMax = $"{horisontal} {vertical}", OffsetMin = $"-{widthBtn} -{hightBtn}", OffsetMax = $"{widthBtn} {hightBtn}",}
                    }
                    });
                }

                itemContainer.Add(new CuiElement()
                {
                    Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                    Name = $"zEcoLootUI.Main.UI.BG.{i}.Btn_Delete",
                    Components =
                    {
                    new CuiButtonComponent{Color = "1 0 0 0.4", Command =  $"Delete {page} {i} {prefab}"},
                    new CuiRectTransformComponent{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-20 -16", OffsetMax = "0 0" }
                    }
                });

                itemContainer.Add(new CuiElement()
                {
                    Parent = $"zEcoLootUI.Main.UI.BG.{i}.Btn_Delete",
                    Name = $"zEcoLootUI.Main.UI.BG.{i}.Btn_Delete.Txt",
                    Components =
                    {
                    new CuiTextComponent{Text = "Del", Align = TextAnchor.MiddleCenter, FontSize = 12},
                    new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
                if (item.IsBp)
                    itemContainer.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                        Name = $"zEcoLootUI.Main.UI.BG.{i}.IsBP",
                        Components =
                        {
                            //new CuiRawImageComponent{Png = GetItemImage("blueprintbase") },
                            new CuiRawImageComponent{Sprite ="assets/icons/blueprint.png"},
                    new CuiRectTransformComponent{AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -20", OffsetMax = "20 0" }
                        }
                    });
                else
                {
                    if (item.HasCondition)
                    {
                        itemContainer.Add(new CuiElement()
                        {
                            Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                            Name = $"zEcoLootUI.Main.UI.BG.{i}.condition.BG",
                            Components =
                        {
                        new CuiImageComponent{Color = "1 1 1 0.5"},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "1 1", OffsetMax = "5 62"}
                        }
                        });
                        var cond = 62 * item.Condition / 100f;
                        itemContainer.Add(new CuiElement()
                        {
                            Parent = $"zEcoLootUI.Main.UI.BG.{i}.condition.BG",
                            Name = $"zEcoLootUI.Main.UI.BG.{i}.condition",
                            Components =
                        {
                            new CuiImageComponent{Color = "0 0.5 0 1"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = $"4 {cond}"}
                        }
                        });

                        itemContainer.Add(new CuiElement()
                        {
                            Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                            Name = $"zEcoLootUI.Main.UI.BG.{i}.Btn_Condition",
                            Components =
                    {
                        new CuiButtonComponent{Color = "1 1 1 0.3", Command =  $"condition {page} {i} {prefab}"},
                        new CuiRectTransformComponent{AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1 -11", OffsetMax = "25 -1" }
                    }
                        });
                        itemContainer.Add(new CuiElement()
                        {
                            Parent = $"zEcoLootUI.Main.UI.BG.{i}.Btn_Condition",
                            Name = $"zEcoLootUI.Main.UI.BG.{i}.Btn_Condition.Txt",
                            Components =
                    {
                        new CuiTextComponent{Text = "cond", Align = TextAnchor.MiddleCenter, FontSize = 9, Color = "1 1 1 1"},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                        });
                    }
                }


                itemContainer.Add(new CuiElement()
                {
                    Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                    Name = $"zEcoLootUI.Main.UI.BG.{i}.text",
                    Components =
                    {
                        new CuiTextComponent{Text = string.IsNullOrEmpty(item.custonname) ? ItemManager.FindItemDefinition(item.shortname).displayName.english : item.custonname, Align = TextAnchor.MiddleCenter},
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"-{widthBtn+5} -40", OffsetMax = $"{widthBtn+5} -5",}
                    }
                });

                itemContainer.Add(new CuiButton()
                {
                    Button = { Command = $"BPChange {page} {i} {prefab}", Color = "1 1 1 0.3" },
                    Text = { Text = "BP", Align = TextAnchor.MiddleCenter, FontSize = 8 },
                    RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"45 41", OffsetMax = $"66 50", }
                }, $"zEcoLootUI.Main.UI.BG.{i}", $"ButtonMinBP.{i}");

                itemContainer.Add(new CuiElement()
                {
                    Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                    Name = $"zEcoLootUI.Main.UI.BG.{i}.textmin",
                    Components =
                    {
                        new CuiTextComponent{Text = $"min - {item.minamount}" , Align = TextAnchor.MiddleRight, FontSize = 9},
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"-{widthBtn+10} 15", OffsetMax = $"{widthBtn+10} 35",}
                    }
                });

                itemContainer.Add(new CuiButton()
                {
                    Button = { Command = $"editfeil {page} {i} minminus {prefab}", Color = "1 1 1 0.3" },
                    Text = { Text = "edit", Align = TextAnchor.MiddleCenter, FontSize = 8 },
                    RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"45 21", OffsetMax = $"66 30", }
                }, $"zEcoLootUI.Main.UI.BG.{i}", $"ButtonMinMinus.{i}");

                itemContainer.Add(new CuiElement()
                {
                    Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                    Name = $"zEcoLootUI.Main.UI.BG.{i}.textmax",
                    Components =
                    {
                        new CuiTextComponent{Text = $"max - {item.maxamount}" , Align = TextAnchor.MiddleRight, FontSize = 9},
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"-{widthBtn+10} 5", OffsetMax = $"{widthBtn+10} 25",}
                    }
                });


                itemContainer.Add(new CuiButton()
                {
                    Button = { Command = $"editfeil {page} {i} maxminus {prefab}", Color = "1 1 1 0.3" },
                    Text = { Text = "edit", Align = TextAnchor.MiddleCenter, FontSize = 8 },
                    RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"45 10", OffsetMax = $"66 19", }
                }, $"zEcoLootUI.Main.UI.BG.{i}", $"ButtonMaxMinus.{i}");

                itemContainer.Add(new CuiElement()
                {
                    Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                    Name = $"zEcoLootUI.Main.UI.BG.{i}.textchance",
                    Components =
                    {
                        new CuiTextComponent{Text = $"chance - {item.chansedrop}%" , Align = TextAnchor.MiddleRight, FontSize = 9},
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"-{widthBtn+10} -10", OffsetMax = $"{widthBtn+10} 15",}
                    }
                });

                itemContainer.Add(new CuiButton()
                {
                    Button = { Command = $"editfeil {page} {i} chanceminus {prefab}", Color = "1 1 1 0.3" },
                    Text = { Text = "edit", Align = TextAnchor.MiddleCenter, FontSize = 8 },
                    RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"45 -1", OffsetMax = $"66 8", }
                }, $"zEcoLootUI.Main.UI.BG.{i}", $"ButtonMaxChanceMinus.{i}");


                horisontal += 0.08f;
                if (horisontal > 0.95)
                {
                    horisontal = starthorisontal;
                    vertical -= 0.15f;
                }


                count++;
                i++;
                CuiHelper.DestroyUi(player, $"zEcoLootUI.Main.UI.BG.{i}");
                CuiHelper.AddUi(player, itemContainer);
                yield return null;
            }

            if (count == 0 && page > 0)
            {
                page--;
                if (coroutine != null)
                    ServerMgr.Instance.StopCoroutine(coroutine);
                DrawCrateitem(player, prefab, page);
                yield break;
            }

            if (page > 0)
            {
                var continerBack = new CuiElementContainer();
                continerBack.Add(new CuiButton()
                {
                    Button = { Command = $"pages {page - 1} {prefab}", Color = "1 1 1 0.5" },
                    Text = { Text = "Назад", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.02 0.02", OffsetMax = "60 30" }
                }, "zEcoLootUI.Main.UI.BG", "ButtonBack");
                CuiHelper.AddUi(player, continerBack);
            }
            if (count == 60)
            {
                var continerNext = new CuiElementContainer();
                continerNext.Add(new CuiButton()
                {
                    Button = { Command = $"pages {page + 1} {prefab}", Color = "1 1 1 0.5" },
                    Text = { Text = "Далее", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.98 0.02", AnchorMax = "0.98 0.02", OffsetMin = "-60 0", OffsetMax = "0 30" }
                }, "zEcoLootUI.Main.UI.BG", "ButtonNext");
                CuiHelper.AddUi(player, continerNext);
            }
        }

        void RedrawItem(BasePlayer player, ItemSetting item, int i, string prefab, int page)
        {
            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI.Adding.BG");
            CuiHelper.DestroyUi(player, $"zEcoLootUI.Main.UI.BG.{i}.textmin");
            CuiHelper.DestroyUi(player, $"zEcoLootUI.Main.UI.BG.{i}.IsBP");
            CuiHelper.DestroyUi(player, $"zEcoLootUI.Main.UI.BG.{i}.textmax");
            CuiHelper.DestroyUi(player, $"zEcoLootUI.Main.UI.BG.{i}.condition.BG");
            CuiHelper.DestroyUi(player, $"zEcoLootUI.Main.UI.BG.{i}.Btn_Condition");
            CuiHelper.DestroyUi(player, $"ButtonMinMinus.{i}");
            CuiHelper.DestroyUi(player, $"ButtonMaxMinus.{i}");
            CuiHelper.DestroyUi(player, $"zEcoLootUI.Main.UI.BG.{i}.textchance");
            CuiHelper.DestroyUi(player, $"ButtonMaxChancePlus.{i}");
            CuiHelper.DestroyUi(player, $"ButtonMaxChanceMinus.{i}");
            CuiHelper.DestroyUi(player, $"ButtonMinBP.{i}");

            var container = new CuiElementContainer();
            if (item.IsBp)
                container.Add(new CuiElement()
                {
                    Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                    Name = $"zEcoLootUI.Main.UI.BG.{i}.IsBP",
                    Components =
                        {
                            new CuiImageComponent{ ItemId = ItemManager.blueprintBaseDef.itemid, SkinId = 0, Color = "1 1 1 1" },
                            new CuiRectTransformComponent{AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -20", OffsetMax = "20 0" }
                        }
                });
            else
            {
                if (item.HasCondition)
                {
                    container.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                        Name = $"zEcoLootUI.Main.UI.BG.{i}.condition.BG",
                        Components =
                        {
                        new CuiImageComponent{Color = "1 1 1 0.5"},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "1 1", OffsetMax = "5 62"}
                        }
                    });
                    var cond = 62 * item.Condition / 100f;
                    container.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.BG.{i}.condition.BG",
                        Name = $"zEcoLootUI.Main.UI.BG.{i}.condition",
                        Components =
                        {
                            new CuiImageComponent{Color = "0 0.5 0 1"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = $"4 {cond}"}
                        }
                    });

                    container.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                        Name = $"zEcoLootUI.Main.UI.BG.{i}.Btn_Condition",
                        Components =
                    {
                        new CuiButtonComponent{Color = "1 1 1 0.3", Command =  $"condition {page} {i} {prefab}"},
                        new CuiRectTransformComponent{AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1 -11", OffsetMax = "25 -1" }
                    }
                    });
                    container.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.BG.{i}.Btn_Condition",
                        Name = $"zEcoLootUI.Main.UI.BG.{i}.Btn_Condition.Txt",
                        Components =
                    {
                        new CuiTextComponent{Text = "cond", Align = TextAnchor.MiddleCenter, FontSize = 9, Color = "1 1 1 1"},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                    });
                }
            }

            container.Add(new CuiButton()
            {
                Button = { Command = $"BPChange {page} {i} {prefab}", Color = "1 1 1 0.3" },
                Text = { Text = "BP", Align = TextAnchor.MiddleCenter, FontSize = 8 },
                RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"45 41", OffsetMax = $"66 50", }
            }, $"zEcoLootUI.Main.UI.BG.{i}", $"ButtonMinBP.{i}");

            container.Add(new CuiElement()
            {
                Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                Name = $"zEcoLootUI.Main.UI.BG.{i}.textmin",
                Components =
                    {
                        new CuiTextComponent{Text = $"min - {item.minamount}" , Align = TextAnchor.MiddleRight, FontSize = 9},
                        //new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"-42 15", OffsetMax = $"42 35",}
                    }
            });

            container.Add(new CuiButton()
            {
                Button = { Command = $"editfeil {page} {i} minminus {prefab}", Color = "1 1 1 0.3" },
                Text = { Text = "edit", Align = TextAnchor.MiddleCenter, FontSize = 8 },
                RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"45 21", OffsetMax = $"66 30", }
            }, $"zEcoLootUI.Main.UI.BG.{i}", $"ButtonMinMinus.{i}");

            container.Add(new CuiElement()
            {
                Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                Name = $"zEcoLootUI.Main.UI.BG.{i}.textmax",
                Components =
                    {
                        new CuiTextComponent{Text = $"max - {item.maxamount}" , Align = TextAnchor.MiddleRight, FontSize = 9},
                        //new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"-42 5", OffsetMax = $"42 25",}
                    }
            });


            container.Add(new CuiButton()
            {
                Button = { Command = $"editfeil {page} {i} maxminus {prefab}", Color = "1 1 1 0.3" },
                Text = { Text = "edit", Align = TextAnchor.MiddleCenter, FontSize = 8 },
                RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"45 10", OffsetMax = $"66 19", }
            }, $"zEcoLootUI.Main.UI.BG.{i}", $"ButtonMaxMinus.{i}");

            container.Add(new CuiElement()
            {
                Parent = $"zEcoLootUI.Main.UI.BG.{i}",
                Name = $"zEcoLootUI.Main.UI.BG.{i}.textchance",
                Components =
                    {
                        new CuiTextComponent{Text = $"chance - {item.chansedrop}%" , Align = TextAnchor.MiddleRight, FontSize = 9},
                        //new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"-{32+10} -10", OffsetMax = $"{32+10} 15",}
                    }
            });

            container.Add(new CuiButton()
            {
                Button = { Command = $"editfeil {page} {i} chanceminus {prefab}", Color = "1 1 1 0.3" },
                Text = { Text = "edit", Align = TextAnchor.MiddleCenter, FontSize = 8 },
                RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"45 -1", OffsetMax = $"66 8", }
            }, $"zEcoLootUI.Main.UI.BG.{i}", $"ButtonMaxChanceMinus.{i}");

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("hidelootimage")]
        void HideImage(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args);

            if (visionImage.ContainsKey(player))
                visionImage[player] = !visionImage[player];
            else
                visionImage.Add(player, false);

            DrawPanel(player, prefab);

        }

        [ConsoleCommand("chanceminus")]
        void chanceminus(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            prefab = prefab.Remove(prefab.LastIndexOf(' '));
            //List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (chanceminus): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);

            int amount = 0;

            if (!int.TryParse(arg.Args.LastOrDefault(), out amount))
                amount = itemList[index].chansedrop;

            if (amount > 100)
                amount = 100;
            if (amount < 0)
                amount = 0;

            itemList[index].chansedrop = amount;

            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            RedrawItem(player, itemList[index], index, prefab, page);
        }

        [ConsoleCommand("minplus")]
        void minplus(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            //var itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (minplus): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            itemList[index].minamount++;
            if (itemList[index].minamount > itemList[index].maxamount)
                itemList[index].maxamount = itemList[index].minamount;
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            RedrawItem(player, itemList[index], index, prefab, page);
        }

        [ConsoleCommand("BPChange")]
        void BPChange(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            //List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (BPChange): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            itemList[index].IsBp = !itemList[index].IsBp;
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            RedrawItem(player, itemList[index], index, prefab, page);
        }

        [ConsoleCommand("minminus")]
        void minminus(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            prefab = prefab.Remove(prefab.LastIndexOf(' '));
            //List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (minminus): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            int amount = 0;

            if (!int.TryParse(arg.Args.LastOrDefault(), out amount))
                amount = itemList[index].minamount;

            itemList[index].minamount = amount;
            if (itemList[index].minamount > itemList[index].maxamount)
                itemList[index].maxamount = itemList[index].minamount;

            if (itemList[index].minamount < 1)
                itemList[index].minamount = 1;

            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            RedrawItem(player, itemList[index], index, prefab, page);
        }

        void EditCondition(BasePlayer player, string prefab, int page, int index)
        {
            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI.Adding.BG");
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI",
                Name = "zEcoLootUI.Main.UI.Adding.BG",
                Components =
                {
                    new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.BG", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                }
            });
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.BG",
                Name = "MinAmountUnput.Panel",
                Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0.8"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -100", OffsetMax = "250 100",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = "MinAmountUnput.Panel",
                Name = "MinAmountUnput.Panel.HeaderTxT",
                Components =
                    {
                        new CuiTextComponent{Text = $"ВВЕДИТЕ ПРОЧНОСТЬ B %", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-250 -50", OffsetMax = "250 50",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = "MinAmountUnput.Panel",
                Name = "MinAmountUnput.BG",
                Components =
                    {
                        new CuiImageComponent{Color = "1 1 1 0.4"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -25", OffsetMax = "150 25",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = "MinAmountUnput.BG",
                Name = "MinAmountUnput",
                Components =
                    {
                        new CuiInputFieldComponent{Color = "0 0 0 1", FontSize = 22, Command = $"conditionvalue {page} {index} {prefab}", Align = TextAnchor.MiddleLeft},
                        new CuiRectTransformComponent{AnchorMin = "0.01 0", AnchorMax = "0.99 1",}
                    }
            });
            CuiHelper.AddUi(player, container);
        }

        void EditField(BasePlayer player, string cmd, string prefab, int page, int index)
        {
            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI.Adding.BG");
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI",
                Name = "zEcoLootUI.Main.UI.Adding.BG",
                Components =
                {
                    new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.BG", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                }
            });
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.BG",
                Name = "MinAmountUnput.Panel",
                Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0.8"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -100", OffsetMax = "250 100",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = "MinAmountUnput.Panel",
                Name = "MinAmountUnput.Panel.HeaderTxT",
                Components =
                    {
                        new CuiTextComponent{Text = $"ВВЕДИТЕ КОЛИЧЕСТВО", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-250 -50", OffsetMax = "250 50",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = "MinAmountUnput.Panel",
                Name = "MinAmountUnput.BG",
                Components =
                    {
                        new CuiImageComponent{Color = "1 1 1 0.4"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -25", OffsetMax = "150 25",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = "MinAmountUnput.BG",
                Name = "MinAmountUnput",
                Components =
                    {
                        new CuiInputFieldComponent{Color = "0 0 0 1", FontSize = 22, Command = $"{cmd} {page} {index} {prefab}", Align = TextAnchor.MiddleLeft},
                        new CuiRectTransformComponent{AnchorMin = "0.01 0", AnchorMax = "0.99 1",}
                    }
            });
            CuiHelper.AddUi(player, container);

        }

        [ConsoleCommand("condition")]
        void condition(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            string prefab = string.Join(" ", arg.Args.Skip(2));
            int page = arg.GetInt(0);
            int index = arg.GetInt(1);
            BasePlayer player = arg.Player();
            EditCondition(player, prefab, page, index);
        }

        [ConsoleCommand("conditionvalue")]
        void conditionvalue(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            prefab = prefab.Remove(prefab.LastIndexOf(' '));
            //List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (minminus): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            int amount = 0;
            if (!int.TryParse(arg.Args.LastOrDefault(), out amount))
                amount = itemList[index].maxamount;
            var reply = 0;
            if (reply == 0) { }
            itemList[index].Condition = amount;
            if (itemList[index].Condition < 0f)
                itemList[index].Condition = 0f;
            if (itemList[index].Condition > 100f)
                itemList[index].Condition = 100f;

            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            RedrawItem(player, itemList[index], index, prefab, page);
        }

        [ConsoleCommand("editfeil")]
        void editfeild(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            int page = arg.GetInt(0);
            int index = arg.GetInt(1);
            string cmd = arg.Args[2];
            string prefab = string.Join(" ", arg.Args.Skip(3));
            BasePlayer player = arg.Player();
            EditField(player, cmd, prefab, page, index);
        }

        [ConsoleCommand("maxminus")]
        void maxminus(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            prefab = prefab.Remove(prefab.LastIndexOf(' '));
            //List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (maxminus): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            int amount = 0;
            if (!int.TryParse(arg.Args.LastOrDefault(), out amount))
                amount = itemList[index].maxamount;
            var reply = 0;
            if (reply == 0) { }
            itemList[index].maxamount = amount;
            if (itemList[index].maxamount < 1)
                itemList[index].maxamount = 1;
            if (itemList[index].maxamount < itemList[index].minamount)
                itemList[index].minamount = itemList[index].maxamount;
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            RedrawItem(player, itemList[index], index, prefab, page);
        }

        [ConsoleCommand("Add")]
        void Add_cmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args);
            docs.Clear();
            docs.Add(prefab);
            DrawAddPanel(player);
        }

        [ConsoleCommand("clearall")]
        void ClearAllBtn(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args);
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (clearall): {prefab}");
                return;
            }
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", new List<ItemSetting>());
            CuiHelper.DestroyUi(arg.Player(), "zEcoLootUI.Main.UI.Adding.BG");
            DrawCrateitem(arg.Player(), prefab);
        }

        [ConsoleCommand("AddInv")]
        void Addinv_cmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            if (player.inventory.containerMain.itemList.Count == 0) return;
            string prefab = string.Join(" ", arg.Args);
            //List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (AddInv): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            foreach (var it in player.inventory.containerMain.itemList)
            {
                List<ItemContent> contentList = new List<ItemContent>();
                if (it.contents != null)
                {
                    foreach (var content in it.contents.itemList)
                    {
                        contentList.Add(new ItemContent() { ShortName = content.info.shortname, Amount = content.amount, Condition = content.condition * 100f / content.maxCondition, });
                    }
                }
                itemList.Add(new ItemSetting()
                {
                    IsBp = it.IsBlueprint(),
                    shortname = it.info.shortname,
                    minamount = it.amount,
                    maxamount = it.amount,
                    chansedrop = 50,
                    skin = it.skin,
                    custonname = it.name,
                    Condition = it.condition * 100f / it.maxCondition,
                    HasCondition = it.hasCondition,
                    version = version,
                    Content = contentList
                });
            }

            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            CuiHelper.DestroyUi(arg.Player(), "zEcoLootUI.Main.UI.Adding.BG");
            DrawCrateitem(arg.Player(), prefab);
        }


        [ConsoleCommand("customitem")]
        void SetCustomItem(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var prefab = docs[0];
            var index = arg.GetInt(0);
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (AddInv): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            var citem = config.customItems[index];
            itemList.Add(new ItemSetting()
            {
                IsBp = false,
                shortname = citem.ShortName,
                minamount = citem.MinAmount,
                maxamount = citem.MinAmount,
                chansedrop = citem.DropChance,
                skin = citem.SkinID,
                Condition = 100,
                HasCondition = ItemManager.FindItemDefinition(citem.ShortName).condition.enabled,
                Content = new List<ItemContent>(),
                version = version,
                custonname = citem.CustomName
            });
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            CuiHelper.DestroyUi(arg.Player(), "zEcoLootUI.Main.UI.Adding.BG");
            DrawCrateitem(arg.Player(), prefab);
        }

        [ConsoleCommand("additem")]
        void SetItem(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var prefab = docs[0];
            //List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[docs[0]]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (AddInv): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            var it = ItemManager.FindItemDefinition(docs[2]);
            var fraction = UnityEngine.Random.Range(it.condition.foundCondition.fractionMin, it.condition.foundCondition.fractionMax);
            itemList.Add(new ItemSetting()
            {
                IsBp = bool.Parse(docs[7]),
                shortname = docs[2],
                minamount = int.Parse(docs[3]),
                maxamount = int.Parse(docs[4]),
                chansedrop = int.Parse(docs[5]),
                skin = ulong.Parse(docs[6]),
                Condition = fraction * 100f,
                HasCondition = it.condition.enabled,
                Content = new List<ItemContent>(),
                version = version,
                custonname = it.displayName.english
            });
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            CuiHelper.DestroyUi(arg.Player(), "zEcoLootUI.Main.UI.Adding.BG");
            DrawCrateitem(arg.Player(), docs[0]);
        }

        List<string> docs = new List<string>();
        void DrawAddPanel(BasePlayer player, ulong playerid = 0)
        {
            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI.Adding.BG");
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI",
                Name = "zEcoLootUI.Main.UI.Adding.BG",
                Components =
                {
                    new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.BG", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                }
            });
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.BG",
                Name = "zEcoLootUI.Main.UI.Adding.Panel",
                Components =
                {
                    new CuiImageComponent{Color = "0 0 0 0.8"},
                    new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -300", OffsetMax = "400 300",}
                }
            });

            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                Name = "zEcoLootUI.Main.UI.Adding.Panel.HeaderTxT",
                Components =
                    {
                        new CuiTextComponent{Text = "ВЫБЕРИТЕ КАТЕГОРИЮ ДОБАВЛЯЕМОГО ПРЕДМЕТА", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.95", AnchorMax = "0.5 0.95", OffsetMin = "-360 -32", OffsetMax = "360 32"}
                    }
            });

            if (docs.Count > 1)
            {
                container.Add(new CuiButton()
                {
                    Button = { Command = $"reset", Color = "1 0 0 0.5" },
                    Text = { Text = "СБРОСИТЬ", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.98 0.98", AnchorMax = "0.98 0.98", OffsetMin = "-64 -32", OffsetMax = "0 0" }
                }, "zEcoLootUI.Main.UI.Adding.Panel", "ButtonNext");
            }

            var starthorisontal = 0.05f;
            var startvertical = 0.9f;
            var horisontal = starthorisontal;
            var vertical = startvertical;
            var widthBtn = 28;
            var hightBtn = 10;
            foreach (var item in (ItemCategory[])Enum.GetValues(typeof(ItemCategory)))
            {
                if (item == ItemCategory.All || item == ItemCategory.Search || item == ItemCategory.Common || item == ItemCategory.Favourite) continue;
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                    Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item}",
                    Components =
                    {
                        new CuiButtonComponent{Command = $"AddParams {1} {item}", Color = docs.Count > 1 && docs[1] == item.ToString() ? "0.16 0.61 0.13 0.8" : "0.16 0.61 0.13 0.4" },
                        new CuiRectTransformComponent { AnchorMin = $"{horisontal} {vertical}", AnchorMax = $"{horisontal} {vertical}", OffsetMin = $"-{widthBtn} -{hightBtn}", OffsetMax = $"{widthBtn} {hightBtn}",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = $"zEcoLootUI.Main.UI.Adding.Panel.{item}",
                    Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item}.Txt",
                    Components =
                    {
                        new CuiTextComponent{Text = $"{item}", Align = TextAnchor.MiddleCenter, FontSize = 10},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });

                horisontal += 0.0817f;
                if (horisontal > 0.95)
                {
                    horisontal = starthorisontal;
                    vertical -= 0.045f;
                }
            }
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                Name = $"zEcoLootUI.Main.UI.Adding.Panel.Custom",
                Components =
                    {
                        new CuiButtonComponent{Command = $"addcustomitems {prefabcontaner}", Color = "0.16 0.61 0.13 0.4"},
                        new CuiRectTransformComponent { AnchorMin = $"{horisontal} {vertical}", AnchorMax = $"{horisontal} {vertical}", OffsetMin = $"-{widthBtn} -{hightBtn}", OffsetMax = $"{widthBtn+10} {hightBtn}",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = $"zEcoLootUI.Main.UI.Adding.Panel.Custom",
                Name = $"zEcoLootUI.Main.UI.Adding.Panel..Custom",
                Components =
                    {
                        new CuiTextComponent{Text = $"Custom Items", Align = TextAnchor.MiddleCenter, FontSize = 10},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
            });
            if (docs.Count == 2)
            {
                starthorisontal = 0.05f;
                startvertical = 0.8f;
                horisontal = starthorisontal;
                vertical = startvertical;
                widthBtn = 25;
                hightBtn = 25;
                foreach (var item in ItemManager.itemList.Where(x => x.category.ToString() == docs[1]))
                {
                    container.Add(new CuiElement()
                    {
                        Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                        Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item.shortname}",
                        Components =
                        {
                            new CuiImageComponent{ ItemId = ItemManager.FindItemDefinition(item.shortname).itemid, SkinId = 0, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = $"{horisontal} {vertical}", AnchorMax = $"{horisontal} {vertical}", OffsetMin = $"-{widthBtn} -{hightBtn}", OffsetMax = $"{widthBtn} {hightBtn}",}
                        }
                    });
                    container.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.Adding.Panel.{item.shortname}",
                        Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item.shortname}.AddBTN",
                        Components =
                        {
                            new CuiButtonComponent{Command = $"AddParams {2} {item.shortname}", Color = docs.Count > 2 && docs[2] == item.shortname ? "0.16 0.61 0.13 0.8" : "0.16 0.61 0.13 0.4" },
                            new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"-25 -18.5", OffsetMax = $"25 -2",}
                        }
                    });

                    container.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.Adding.Panel.{item.shortname}.AddBTN",
                        Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item.shortname}.Txt",
                        Components =
                    {
                        new CuiTextComponent{Text = $"ДОБАВИТЬ", Align = TextAnchor.MiddleCenter, FontSize = 10},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                    });
                    horisontal += 0.075f;
                    if (horisontal > 0.95)
                    {
                        horisontal = starthorisontal;
                        vertical -= 0.12f;
                    }
                }
            }
            if (docs.Count == 3)
            {
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                    Name = "zEcoLootUI.Main.UI.Adding.Panel.MinAmount",
                    Components =
                        {
                            new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.Panel.MinAmount", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                        }
                });
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel.MinAmount",
                    Name = "MinAmountUnput.Panel",
                    Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0.8"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -100", OffsetMax = "250 100",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "MinAmountUnput.Panel",
                    Name = "MinAmountUnput.Panel.HeaderTxT",
                    Components =
                    {
                        new CuiTextComponent{Text = $"ВВЕДИТЕ МИНИМАЛЬНОЕ КОЛИЧЕСТВО ПРЕДМЕТА\nВыбранный предмет: <b>{docs[2]}</b>", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-250 -50", OffsetMax = "250 50",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "MinAmountUnput.Panel",
                    Name = "MinAmountUnput.BG",
                    Components =
                    {
                        new CuiImageComponent{Color = "1 1 1 0.4"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -25", OffsetMax = "150 25",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "MinAmountUnput.BG",
                    Name = "MinAmountUnput",
                    Components =
                    {
                        new CuiInputFieldComponent{Color = "0 0 0 1", FontSize = 22, Command = $"AddParams {3}", Align = TextAnchor.MiddleLeft},
                        new CuiRectTransformComponent{AnchorMin = "0.01 0", AnchorMax = "0.99 1",}
                    }
                });
            }
            if (docs.Count == 4)
            {
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                    Name = "zEcoLootUI.Main.UI.Adding.Panel.MaxAmount",
                    Components =
                        {
                            new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.Panel.MaxAmount", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                        }
                });
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel.MaxAmount",
                    Name = "MaxAmountUnput.Panel",
                    Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0.8"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -100", OffsetMax = "250 100",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "MaxAmountUnput.Panel",
                    Name = "MaxAmountUnput.Panel.HeaderTxT",
                    Components =
                    {
                        new CuiTextComponent{Text = $"ВВЕДИТЕ МАКСИМАЛЬНОЕ КОЛИЧЕСТВО ПРЕДМЕТА\nВыбранный предмет: <b>{docs[2]}</b>", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-250 -50", OffsetMax = "250 50",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "MaxAmountUnput.Panel",
                    Name = "MaxAmountUnput.BG",
                    Components =
                    {
                        new CuiImageComponent{Color = "1 1 1 0.4"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -25", OffsetMax = "150 25",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "MaxAmountUnput.BG",
                    Name = "MaxAmountUnput",
                    Components =
                    {
                        new CuiInputFieldComponent{Color = "0 0 0 1", FontSize = 22, Command = $"AddParams {4}", Align = TextAnchor.MiddleLeft},
                        new CuiRectTransformComponent{AnchorMin = "0.01 0", AnchorMax = "0.99 1",}
                    }
                });
            }

            if (docs.Count == 5)
            {
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                    Name = "zEcoLootUI.Main.UI.Adding.Panel.DropChance",
                    Components =
                        {
                            new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.Panel.DropChance", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                        }
                });
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel.DropChance",
                    Name = "DropChanceUnput.Panel",
                    Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0.8"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -100", OffsetMax = "250 100",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "DropChanceUnput.Panel",
                    Name = "DropChanceUnput.Panel.HeaderTxT",
                    Components =
                    {
                        new CuiTextComponent{Text = $"ВВЕДИТЕ ШАНС ДРОПА ПРЕДМЕТА (0 - 100)\nВыбранный предмет: <b>{docs[2]}</b>", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-250 -50", OffsetMax = "250 50",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "DropChanceUnput.Panel",
                    Name = "DropChanceUnput.BG",
                    Components =
                    {
                        new CuiImageComponent{Color = "1 1 1 0.4"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -25", OffsetMax = "150 25",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "DropChanceUnput.BG",
                    Name = "DropChanceUnput",
                    Components =
                    {
                        new CuiInputFieldComponent{Color = "0 0 0 1", FontSize = 22, Command = $"AddParams {5}", Align = TextAnchor.MiddleLeft, CharsLimit = 3},
                        new CuiRectTransformComponent{AnchorMin = "0.01 0", AnchorMax = "0.99 1",}
                    }
                });
            }

            if (docs.Count == 6)
            {
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                    Name = "zEcoLootUI.Main.UI.Adding.Panel.SkinID",
                    Components =
                        {
                            new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.Panel.SkinID", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                        }
                });
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel.SkinID",
                    Name = "SkinIDUnput.Panel",
                    Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0.8"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -100", OffsetMax = "250 100",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "SkinIDUnput.Panel",
                    Name = "SkinIDUnput.Panel.HeaderTxT",
                    Components =
                    {
                        new CuiTextComponent{Text = $"ВВЕДИТЕ SKINID ПРЕДМЕТА (0 - стандартный)\nВыбранный предмет: <b>{docs[2]}</b>", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-250 -50", OffsetMax = "250 50",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "SkinIDUnput.Panel",
                    Name = "SkinIDUnput.BG",
                    Components =
                    {
                        new CuiImageComponent{Color = "1 1 1 0.4"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -25", OffsetMax = "150 25",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "SkinIDUnput.BG",
                    Name = "SkinIDUnput",
                    Components =
                    {
                        new CuiInputFieldComponent{Color = "0 0 0 1", FontSize = 22, Command = $"AddParams {6}", Align = TextAnchor.MiddleLeft},
                        new CuiRectTransformComponent{AnchorMin = "0.01 0", AnchorMax = "0.99 1",}
                    }
                });
            }

            if (docs.Count == 7)
            {
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                    Name = "zEcoLootUI.Main.UI.Adding.Panel.isBp",
                    Components =
                        {
                            new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.Panel.isBp", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                        }
                });
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel.isBp",
                    Name = "isBpUnput.Panel",
                    Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0.8"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -100", OffsetMax = "250 100",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "isBpUnput.Panel",
                    Name = "isBpUnput.Panel.HeaderTxT",
                    Components =
                    {
                        new CuiTextComponent{Text = $"Предмет является чертежом?\nВыбранный предмет: <b>{docs[2]}</b>", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-250 -50", OffsetMax = "250 50",}
                    }
                });


                container.Add(new CuiButton()
                {
                    Button = { Command = $"AddParams {7} {true}", Color = "1 1 1 0.3" },
                    Text = { Text = "ДА", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.4 0.5", AnchorMax = "0.4 0.5", OffsetMin = "-30 -15", OffsetMax = "30 15" }
                }, "isBpUnput.Panel", "TrueBtn");

                container.Add(new CuiButton()
                {
                    Button = { Command = $"AddParams {7} {false}", Color = "1 1 1 0.3" },
                    Text = { Text = "НЕТ", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.6 0.5", AnchorMax = "0.6 0.5", OffsetMin = "-30 -15", OffsetMax = "30 15" }
                }, "isBpUnput.Panel", "FalseBtn");

            }

            if (docs.Count == 8)
            {
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                    Name = "zEcoLootUI.Main.UI.Adding.Panel.LastInfo",
                    Components =
                        {
                            new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.Panel.LastInfo", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                        }
                });
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel.LastInfo",
                    Name = "LastInfo.Panel",
                    Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0.8"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -150", OffsetMax = "250 150",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "LastInfo.Panel",
                    Name = "LastInfo.Panel.HeaderTxT",
                    Components =
                    {
                        new CuiTextComponent{Text = $"ИТОГОВАЯ ИНФОРМАЦИЯ:", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-250 -50", OffsetMax = "250 50",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "LastInfo.Panel",
                    Name = "LastInfo.Panel.HeaderTxT.List",
                    Components =
                    {
                        new CuiTextComponent{Text = $"Выбранный предмет: <b>{docs[2]}</b>" +
                        $"\nПредмет чертеж? {docs[7]}" +
                        $"\nМинимум предмета: {docs[3]}" +
                        $"\nМаксимум предмета: {docs[4]}" +
                        $"\nШанс дропа: {docs[5]}" +
                        $"\nSkinID предмета: {docs[6]}" +
                        $"\nКастомное имя предмета можно настроить в файле:" +
                        $"\ndata/zEcoLootUI/{prefabcontaner[docs[0]]}.json\n\n" +
                        $"\nЕсли все правильно нажмите ДОБАВИТЬ, иначе щелкните по свободному месту на экране и нажмите кнопку СБРОС", Align = TextAnchor.MiddleLeft, FontSize = 14},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.48", AnchorMax = "0.5 0.48", OffsetMin = "-200 -200", OffsetMax = "250 200",}
                    }
                });

                container.Add(new CuiButton()
                {
                    Button = { Command = $"additem", Color = "0 1 0 0.3" },
                    Text = { Text = "ДОБАВИТЬ", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.5 0.05", AnchorMax = "0.5 0.05", OffsetMin = "-60 -10", OffsetMax = "60 10" }
                }, "LastInfo.Panel", "AddBtn");

            }

            CuiHelper.AddUi(player, container);
        }


        [ConsoleCommand("reset")]
        void CloseTab(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var prefab = docs[0];
            docs.Clear();
            docs.Add(prefab);
            DrawAddPanel(arg.Player());
        }

        [ConsoleCommand("AddParams")]
        void AddParams(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            var newobject = arg.Args[1];
            int index = int.Parse(arg.Args[0]);

            if (index == 5 && int.Parse(newobject) < 0)
                newobject = "0";
            else if (index == 5 && int.Parse(newobject) > 100)
                newobject = "100";

            if (docs.Count > index)
                docs[index] = newobject;
            else
                docs.Add(newobject);

            DrawAddPanel(player);
        }

        [ConsoleCommand("delete")]
        void Delete_cmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            int page = arg.GetInt(0);
            int index = arg.GetInt(1);
            //List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            if (!lootSettings.ContainsKey(prefab))
            {
                PrintError($"Not Fount (AddInv): {prefab}");
                return;
            }
            var itemList = lootSettings[prefab];
            itemList.Remove(itemList[index]);
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefabcontaner[prefab]}", itemList);
            DrawCrateitem(player, prefab, page);
        }

        [ConsoleCommand("pages")]
        void CmdConsoleNexPrevPage(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            if (!player.IsAdmin) return;
            string prefab = "‌​‌‌‍﻿﻿";
            prefab = string.Join(" ", arg.Args.Skip(1));
            int page = arg.GetInt(0);
            if (page < 0) page = 0;
            DrawCrateitem(player, prefab, page);
        }

        public class ItemSetting
        {
            [JsonProperty("Категория предмета")]
            public ItemCategory category;
            [JsonProperty("Предмет - чертеж?")]
            public bool IsBp;
            [JsonProperty("Шортнейм предмета")]
            public string shortname;
            [JsonProperty("Максимальное количество предмета")]
            public int maxamount;
            [JsonProperty("Минимальное количество предмета")]
            public int minamount;
            [JsonProperty("Шанс дропа предмета (в %)")]
            public int chansedrop;
            [JsonProperty("Кастомное название предмета")]
            public string custonname;
            [JsonProperty("SkinID предмета")]
            public ulong skin;
            [JsonProperty("Прочность предмета")]
            public float Condition;
            [JsonProperty("Инвентарь предмета")]
            public List<ItemContent> Content;

            public bool HasCondition;

            public string version = "";

        }
        public class ItemContent
        {
            [JsonProperty("Шортнейм предмета")]
            public string ShortName;
            [JsonProperty("Прочность предмета")]
            public float Condition;
            [JsonProperty("Количество предмета")]
            public int Amount;
        }

        [ChatCommand("updateloot")]
        void updateloot(BasePlayer player)
        {
            if (!player.IsAdmin) return;

            if (filling != null)
                ServerMgr.Instance.StopCoroutine(filling);

            var containers = BaseNetworkable.serverEntities.OfType<LootContainer>();
            filling = ServerMgr.Instance.StartCoroutine(FirsLoad(containers));

            SendReply(player, "Лут обновлен");
        }

        [ConsoleCommand("updateloot")]
        void updateloot_cmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;

            if (filling != null)
                ServerMgr.Instance.StopCoroutine(filling);

            var containers = BaseNetworkable.serverEntities.OfType<LootContainer>();
            filling = ServerMgr.Instance.StartCoroutine(FirsLoad(containers));
            Puts("Лут обновлен");
        }

        [ConsoleCommand("addcustomitems")]
        void AddCustomUI(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            var container = new CuiElementContainer();
            var starthorisontal = 0.05f;
            var startvertical = 0.9f;
            var horisontal = starthorisontal;
            var vertical = startvertical;
            var widthBtn = 28;
            var hightBtn = 10;
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.BG",
                Name = "zEcoLootUI.Main.UI.Adding.Panel",
                Components =
                {
                    new CuiImageComponent{Color = "0 0 0 0.8"},
                    new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -300", OffsetMax = "400 300",}
                }
            });
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                Name = "zEcoLootUI.Main.UI.Adding.Panel.HeaderTxT",
                Components =
                    {
                        new CuiTextComponent{Text = "ВЫБЕРИТЕ КАТЕГОРИЮ ДОБАВЛЯЕМОГО ПРЕДМЕТА", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.95", AnchorMax = "0.5 0.95", OffsetMin = "-360 -32", OffsetMax = "360 32"}
                    }
            });

            foreach (var item in (ItemCategory[])Enum.GetValues(typeof(ItemCategory)))
            {
                if (item == ItemCategory.All || item == ItemCategory.Search || item == ItemCategory.Common || item == ItemCategory.Favourite) continue;
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                    Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item}",
                    Components =
                    {
                        new CuiButtonComponent{Command = $"AddParams {1} {item}", Color = docs.Count > 1 && docs[1] == item.ToString() ? "0.16 0.61 0.13 0.8" : "0.16 0.61 0.13 0.4" },
                        new CuiRectTransformComponent { AnchorMin = $"{horisontal} {vertical}", AnchorMax = $"{horisontal} {vertical}", OffsetMin = $"-{widthBtn} -{hightBtn}", OffsetMax = $"{widthBtn} {hightBtn}",}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = $"zEcoLootUI.Main.UI.Adding.Panel.{item}",
                    Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item}.Txt",
                    Components =
                    {
                        new CuiTextComponent{Text = $"{item}", Align = TextAnchor.MiddleCenter, FontSize = 10},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });

                horisontal += 0.0817f;
                if (horisontal > 0.95)
                {
                    horisontal = starthorisontal;
                    vertical -= 0.045f;
                }
            }
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                Name = $"zEcoLootUI.Main.UI.Adding.Panel.Custom",
                Components =
                    {
                        new CuiButtonComponent{Command = $"", Color = "0.16 0.61 0.13 0.8"},
                        new CuiRectTransformComponent { AnchorMin = $"{horisontal} {vertical}", AnchorMax = $"{horisontal} {vertical}", OffsetMin = $"-{widthBtn} -{hightBtn}", OffsetMax = $"{widthBtn+10} {hightBtn}",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = $"zEcoLootUI.Main.UI.Adding.Panel.Custom",
                Name = $"zEcoLootUI.Main.UI.Adding.Panel..Custom",
                Components =
                    {
                        new CuiTextComponent{Text = $"Custom Items", Align = TextAnchor.MiddleCenter, FontSize = 10},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
            });
            starthorisontal = 0.05f;
            startvertical = 0.78f;
            horisontal = starthorisontal;
            vertical = startvertical;
            widthBtn = 25;
            hightBtn = 25;
            if (config.customItems.Count <= 0)
            {
                container.Add(new CuiElement()
                {
                    Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                    Name = $"zEcoLootUI.Main.UI.Adding.Panel.info",
                    Components =
                    {
                        new CuiTextComponent{Text = $"НЕТ НЕ ОДНОГО КАСТОМНОГО ПРЕДМЕТА", Align = TextAnchor.MiddleCenter, FontSize = 28},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                });
            }
            else
            {
                int index = 0;
                foreach (var item in config.customItems)
                {
                    container.Add(new CuiElement()
                    {
                        Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                        Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item.ShortName}",
                        Components =
                        {
                            new CuiImageComponent{ ItemId = ItemManager.FindItemDefinition(item.ShortName).itemid, SkinId = item.SkinID, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = $"{horisontal} {vertical}", AnchorMax = $"{horisontal} {vertical}", OffsetMin = $"-{widthBtn} -{hightBtn}", OffsetMax = $"{widthBtn} {hightBtn}",}
                        }
                    });
                    container.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.Adding.Panel.{item.ShortName}",
                        Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item.ShortName}.AddBTN",
                        Components =
                        {
                            new CuiButtonComponent{Command = $"addcustom {index}", Color = "0.16 0.61 0.13 0.4" },
                            new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = $"-25 -18.5", OffsetMax = $"25 -2",}
                        }
                    });

                    container.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.Adding.Panel.{item.ShortName}.AddBTN",
                        Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item.ShortName}.Txt",
                        Components =
                        {
                        new CuiTextComponent{Text = $"ДОБАВИТЬ", Align = TextAnchor.MiddleCenter, FontSize = 10},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                    });
                    container.Add(new CuiElement()
                    {
                        Parent = $"zEcoLootUI.Main.UI.Adding.Panel.{item.ShortName}",
                        Name = $"zEcoLootUI.Main.UI.Adding.Panel.{item.ShortName}.name",
                        Components =
                        {
                        new CuiTextComponent{Text = $"{item.CustomName}", Align = TextAnchor.UpperCenter, FontSize = 10},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                    });
                    horisontal += 0.075f;
                    index++;
                    if (horisontal > 0.95)
                    {
                        horisontal = starthorisontal;
                        vertical -= 0.12f;
                    }
                }
            }

            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI.Adding.Panel");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("addcustom")]
        void AddCustom(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            var index = arg.GetInt(0);
            string prefab = docs[0];
            var cItem = config.customItems[index];
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.Panel",
                Name = "zEcoLootUI.Main.UI.Adding.Panel.LastInfo",
                Components =
                        {
                            new CuiButtonComponent{Close = "zEcoLootUI.Main.UI.Adding.Panel.LastInfo", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1",}
                        }
            });
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.Adding.Panel.LastInfo",
                Name = "LastInfo.Panel",
                Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0.8"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -150", OffsetMax = "250 150",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = "LastInfo.Panel",
                Name = "LastInfo.Panel.HeaderTxT",
                Components =
                    {
                        new CuiTextComponent{Text = $"ИТОГОВАЯ ИНФОРМАЦИЯ:", Align = TextAnchor.MiddleCenter, FontSize = 18},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-250 -50", OffsetMax = "250 50",}
                    }
            });
            container.Add(new CuiElement()
            {
                Parent = "LastInfo.Panel",
                Name = "LastInfo.Panel.HeaderTxT.List",
                Components =
                    {
                        new CuiTextComponent{Text = $"Выбранный предмет: <b>{cItem.ShortName}</b>" +
                        $"\nПредмет чертеж? false" +
                        $"\nМинимум предмета: {cItem.MinAmount}" +
                        $"\nМаксимум предмета: {cItem.MaxAmount}" +
                        $"\nШанс дропа: {cItem.DropChance}" +
                        $"\nSkinID предмета: {cItem.SkinID}" +
                        $"\nКастомное имя предмета: {cItem.CustomName}" +
                        $"\ndata/zEcoLootUI/{prefabcontaner[prefab]}.json\n\n" +
                        $"", Align = TextAnchor.MiddleLeft, FontSize = 14},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.48", AnchorMax = "0.5 0.48", OffsetMin = "-200 -200", OffsetMax = "250 200",}
                    }
            });

            container.Add(new CuiButton()
            {
                Button = { Command = $"customitem {index}", Color = "0 1 0 0.3" },
                Text = { Text = "ДОБАВИТЬ", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.5 0.05", AnchorMax = "0.5 0.05", OffsetMin = "-60 -10", OffsetMax = "60 10" }
            }, "LastInfo.Panel", "AddBtn");

            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI.Adding.Panel.LastInfo");
            CuiHelper.AddUi(player, container);
        }

        Dictionary<LootContainer, int> OpenedContaner = new Dictionary<LootContainer, int>();

        object OnLootSpawn(LootContainer container)
        {
            if (isUloading) return null;
            if (!init) return null;
            if (!prefabcontaner.ContainsKey(container.PrefabName)) return null;
            if (!config.crateSetting.ContainsKey(prefabcontaner[container.PrefabName])) return null;
            if (config.crateSetting[prefabcontaner[container.PrefabName]].DisableCrate) return null;

            if (Interface.CallHook("CanUILootSpawn", container) != null)
                return null;

            ServerMgr.Instance.StartCoroutine(FillContaner(container));
            return false;
        }

        IEnumerator FillContaner(LootContainer container)
        {
            if (container == null)
            {
                yield break;
            }

            if (container.inventory == null)
            {
                yield break;
            }

            if (!prefabcontaner.ContainsKey(container.PrefabName))
            {
                yield break;
            }

            if (!config.crateSetting.ContainsKey(prefabcontaner[container.PrefabName]))
            {
                yield break;
            }

            if (!config.crateSetting.ContainsKey(prefabcontaner[container.PrefabName]))
            {
                yield break;
            }
            if (!lootSettings.ContainsKey(container.PrefabName))
            {
                PrintWarning($"Повторная загрузка: zEcoLootUI/{prefabcontaner[container.PrefabName]}.json");
                yield return lootSettings[container.PrefabName] = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{prefabcontaner[container.PrefabName]}");
            }


            foreach (var item in container.inventory.itemList.ToList())
            {
                item.Remove(0f);
            }

            container.inventory.Clear();
            ItemManager.DoRemoves();

            yield return null;

            int slots = config.crateSetting[prefabcontaner[container.PrefabName]].RandomItemAmount ? UnityEngine.Random.Range(config.crateSetting[prefabcontaner[container.PrefabName]].MinItemInCrate, config.crateSetting[prefabcontaner[container.PrefabName]].MaxItemInCrate + 1) : config.crateSetting[prefabcontaner[container.PrefabName]].MaxItemInCrate;

            container.inventory.capacity = slots;
            container.inventorySlots = slots;

            var itemlist = lootSettings[container.PrefabName];
            if (itemlist.Count() <= 0) yield break;

            if (itemlist.Any(x => x.shortname == "scrap"))
            {
                var scrapindex = itemlist.IndexOf(itemlist.Find(x => x.shortname == "scrap"));
                Item scrap = ItemManager.CreateByName("scrap", UnityEngine.Random.Range(itemlist[scrapindex].minamount, itemlist[scrapindex].maxamount));
                scrap.MoveToContainer(container.inventory);
            }

            int maxTry = 100;
            int itemCount = 0;

            while (itemCount < slots && maxTry > 0)
            {
                maxTry--;
                var items = itemlist.Where(x => !container.inventory.itemList.Any(y => y.info.shortname == x.shortname && y.skin == x.skin)).ToList();
                if (items.Count <= 0) break;
                var sumChance = items.Sum(x => x.chansedrop) + 1;
                var dropchance = UnityEngine.Random.Range(0, sumChance);
                foreach (var item in items)
                {
                    dropchance -= item.chansedrop;
                    if (dropchance > 0) continue;

                    ItemDefinition def = ItemManager.FindItemDefinition(item.shortname);
                    if (def == null) continue;
                    Item newitem;
                    if (item.IsBp)
                    {
                        newitem = ItemManager.Create(ItemManager.blueprintBaseDef);
                        newitem.blueprintTarget = def.itemid;
                    }
                    else
                    {
                        var amount = UnityEngine.Random.Range(item.minamount, item.maxamount);
                        newitem = ItemManager.Create(def, amount, item.skin);

                        if (!string.IsNullOrEmpty(item.custonname))
                            newitem.name = item.custonname;

                        if (newitem.hasCondition)
                        {
                            newitem.condition = newitem.maxCondition * (item.Condition / 100f);
                        }
                        newitem.OnVirginSpawn();
                        if (item.Content != null && item.Content.Count > 0)
                        {
                            newitem.contents.itemList.Clear();
                            foreach (var content in item.Content)
                            {
                                Item contentItem = ItemManager.CreateByName(content.ShortName, content.Amount, 0);
                                if (content.Condition > 0)
                                {
                                    contentItem.condition = content.Condition;
                                }
                                contentItem.MoveToContainer(newitem.contents);
                            }
                        }
                    }
                    if (!newitem.MoveToContainer(container.inventory))
                    {
                        newitem.Remove(0.1f);
                        break;
                    }
                    itemCount++;
                    break;
                }
            }

            ItemManager.DoRemoves();

            Interface.Oxide.CallHook("UILootSpawned", container);
        }

        void LootTimer()
        {
            timer.Every(1f, () =>
            {
                foreach (var container in OpenedContaner.Keys.ToList())
                {
                    if (container == null)
                    {
                        OpenedContaner.Remove(container);
                        continue;
                    }

                    if (OpenedContaner.ContainsKey(container))
                    {
                        OpenedContaner[container]--;
                        if (OpenedContaner[container] <= 0)
                        {
                            OpenedContaner.Remove(container);
                            container.Kill();
                        }
                    }
                }
            });
        }

        List<LootContainer> handledContainers = new List<LootContainer>();

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (player == null || entity == null) return;
            if (!player || !entity || entity?.IsDestroyed == true) return;
            if (entity.PrefabName.Contains("stocking")) return;
            var container = entity.GetComponent<LootContainer>();
            if (!container) return;

            if (container.inventory?.itemList == null)
            {
                PrintError("This should never happen. Data: {0} (IsDestroyed: {1})", entity, entity?.IsDestroyed);
                return;
            }
            if (!OpenedContaner.ContainsKey(container) && prefabcontaner.ContainsKey(container.PrefabName) && config.crateSetting.ContainsKey(prefabcontaner[container.PrefabName]) && config.crateSetting[prefabcontaner[container.PrefabName]].DeleteCrate && container.inventory.itemList.Count > 0)
            {
                OpenedContaner.Add(container, config.crateSetting[prefabcontaner[container.PrefabName]].DeleteTimer * 60);
            }
        }

        OldItemClass oldItemClass;
        public class OldItemClass
        {
            public int Protocol;
            public Dictionary<string, List<string>> ItemBase = new Dictionary<string, List<string>>();
        }

        void ChekNewItem()
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"zEcoLootUI/ItemBase"))
            {
                oldItemClass = new OldItemClass();
                Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/ItemBase", oldItemClass);
            }
            oldItemClass = Interface.Oxide.DataFileSystem.ReadObject<OldItemClass>($"zEcoLootUI/ItemBase");
            if (oldItemClass.Protocol == Rust.Protocol.network) return;
            oldItemClass.Protocol = Rust.Protocol.network;

            foreach (var container in prefabcontaner)
            {
                var prefab = container.Key;
                var lootContainer = GameManager.server.FindPrefab(prefab)?.GetComponent<LootContainer>();
                if (lootContainer == null) return;
                List<ItemSetting> itemamount = new List<ItemSetting>();
                if (lootContainer.scrapAmount > 0)
                    itemamount.Add(new ItemSetting { shortname = "scrap", minamount = lootContainer.scrapAmount, maxamount = lootContainer.scrapAmount + 5, chansedrop = 100, custonname = "", IsBp = false, skin = 0, Condition = 0, Content = new List<ItemContent>() });

                if (lootContainer.lootDefinition != null)
                    GetLootSpawn(lootContainer.lootDefinition, ref itemamount);

                else if (lootContainer.LootSpawnSlots.Length > 0)
                {
                    LootContainer.LootSpawnSlot[] lootSpawnSlots = lootContainer.LootSpawnSlots;
                    for (int i = 0; i < lootSpawnSlots.Length; i++)
                    {
                        LootContainer.LootSpawnSlot lootSpawnSlot = lootSpawnSlots[i];
                        GetLootSpawn(lootSpawnSlot.definition, ref itemamount);
                    }
                }
                foreach (var it in itemamount)
                {
                    if (oldItemClass.ItemBase.ContainsKey(lootContainer.ShortPrefabName) && oldItemClass.ItemBase[lootContainer.ShortPrefabName].Contains(it.shortname)) continue;
                    if (oldItemClass.ItemBase.ContainsKey(lootContainer.ShortPrefabName) && !oldItemClass.ItemBase[lootContainer.ShortPrefabName].Contains(it.shortname))
                        oldItemClass.ItemBase[lootContainer.ShortPrefabName].Add(it.shortname);
                    if (!oldItemClass.ItemBase.ContainsKey(lootContainer.ShortPrefabName))
                    {
                        oldItemClass.ItemBase.Add(lootContainer.ShortPrefabName, new List<string>());
                        oldItemClass.ItemBase[lootContainer.ShortPrefabName].Add(it.shortname);
                    }
                    PrintWarning($"Обнаружен новый предмет лута!!! - {it.shortname}. Добавлен в ящик - {container.Value} со стандартными настройками!");
                    List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{container.Value}");
                    if (itemlist.Any(x => x.shortname == it.shortname))
                    {
                        PrintError("Error #331! Пожалуйста сообщите разработчику EcoSmile");
                        continue;
                    }
                    itemlist.Add(new ItemSetting()
                    {
                        IsBp = it.IsBp,
                        shortname = it.shortname,
                        minamount = it.minamount,
                        maxamount = it.maxamount,
                        chansedrop = it.chansedrop,
                        Condition = it.Condition,
                        HasCondition = it.HasCondition,
                        skin = it.skin,
                        custonname = "",
                        version = version,
                        Content = new List<ItemContent>()
                    });
                    Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{container.Value}", itemlist);
                }
            }
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/ItemBase", oldItemClass);
        }

        void GetDefaultLoot()
        {
            foreach (var container in prefabcontaner)
            {
                if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"zEcoLootUI/{container.Value}"))
                {
                    var prefab = container.Key;
                    var lootContainer = GameManager.server.FindPrefab(prefab)?.GetComponent<LootContainer>();
                    if (lootContainer == null) return;

                    List<ItemContent> itemContents = new List<ItemContent>();

                    List<ItemSetting> itemamount = new List<ItemSetting>();
                    if (lootContainer.scrapAmount > 0)
                        itemamount.Add(new ItemSetting
                        {
                            shortname = "scrap",
                            minamount = lootContainer.scrapAmount,
                            maxamount = lootContainer.scrapAmount,
                            chansedrop = 100,
                            custonname = "",
                            IsBp = false,
                            skin = 0,
                            Condition = 0,
                            Content = new List<ItemContent>(),
                            version = version,
                        });

                    if (lootContainer.lootDefinition != null)
                        GetLootSpawn(lootContainer.lootDefinition, ref itemamount);

                    else if (lootContainer.LootSpawnSlots.Length > 0)
                    {
                        LootContainer.LootSpawnSlot[] lootSpawnSlots = lootContainer.LootSpawnSlots;
                        for (int i = 0; i < lootSpawnSlots.Length; i++)
                        {
                            LootContainer.LootSpawnSlot lootSpawnSlot = lootSpawnSlots[i];
                            GetLootSpawn(lootSpawnSlot.definition, ref itemamount);
                        }
                    }
                    List<ItemSetting> itemamountf = new List<ItemSetting>();
                    foreach (var item in itemamount)
                    {
                        var gitem = itemamount.Where(x => x.shortname == item.shortname);
                        var MinAmount = item.minamount;
                        var MaxAmount = item.maxamount;
                        foreach (var it in gitem)
                        {
                            if (it.minamount < item.minamount)
                                MinAmount = it.minamount;
                            if (item.maxamount < it.maxamount)
                                MaxAmount = it.maxamount;
                        }
                        if (!itemamountf.Any(x => x.shortname == item.shortname))
                            itemamountf.Add(new ItemSetting()
                            {
                                Content = item.Content,
                                shortname = item.shortname,
                                chansedrop = item.chansedrop,
                                custonname = item.custonname,
                                IsBp = item.IsBp,
                                skin = item.skin,
                                minamount = MinAmount,
                                maxamount = MaxAmount,
                                Condition = item.Condition,
                                HasCondition = item.HasCondition,
                                category = item.category,
                                version = version,
                            });

                    }

                    Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{container.Value}", itemamountf);
                }
            }
        }

        void GetLootSpawn(LootSpawn lootSpawn, ref List<ItemSetting> items)
        {

            if (lootSpawn.subSpawn != null && lootSpawn.subSpawn.Length > 0)
            {
                foreach (var entry in lootSpawn.subSpawn)
                    GetLootSpawn(entry.category, ref items);
                return;
            }

            if (lootSpawn.items != null && lootSpawn.items.Length > 0)
            {
                foreach (var it in lootSpawn.items)
                {
                    var fraction = UnityEngine.Random.Range(it.itemDef.condition.foundCondition.fractionMin, it.itemDef.condition.foundCondition.fractionMax);
                    items.Add(new ItemSetting
                    {
                        shortname = it.itemDef.shortname,
                        minamount = (int)it.startAmount,
                        maxamount = (int)it.maxAmount > 0 ? (int)it.maxAmount : (int)it.startAmount,
                        chansedrop = it.itemDef.rarity == Rust.Rarity.Common ? 100 : it.itemDef.rarity == Rust.Rarity.Uncommon ? 75 : it.itemDef.rarity == Rust.Rarity.Rare ? 50 : it.itemDef.rarity == Rust.Rarity.VeryRare ? 25 : 100,
                        custonname = "",
                        IsBp = it.itemDef.spawnAsBlueprint,
                        HasCondition = it.itemDef.condition.enabled,
                        skin = 0,
                        Condition = fraction * 100f,
                        Content = new List<ItemContent>()
                    });
                }
            }
        }


        private class SteampoweredResult
        {
            public Response response;
            public class Response
            {
                [JsonProperty("result")]
                public int result;

                [JsonProperty("resultcount")]
                public int resultcount;

                [JsonProperty("publishedfiledetails")]
                public List<PublishedFiled> publishedfiledetails;
                public class PublishedFiled
                {
                    [JsonProperty("publishedfileid")]
                    public ulong publishedfileid;

                    [JsonProperty("result")]
                    public int result;

                    [JsonProperty("creator")]
                    public string creator;

                    [JsonProperty("creator_app_id")]
                    public int creator_app_id;

                    [JsonProperty("consumer_app_id")]
                    public int consumer_app_id;

                    [JsonProperty("filename")]
                    public string filename;

                    [JsonProperty("file_size")]
                    public int file_size;

                    [JsonProperty("preview_url")]
                    public string preview_url;

                    [JsonProperty("hcontent_preview")]
                    public string hcontent_preview;

                    [JsonProperty("title")]
                    public string title;

                    [JsonProperty("description")]
                    public string description;

                    [JsonProperty("time_created")]
                    public int time_created;

                    [JsonProperty("time_updated")]
                    public int time_updated;

                    [JsonProperty("visibility")]
                    public int visibility;

                    [JsonProperty("banned")]
                    public int banned;

                    [JsonProperty("ban_reason")]
                    public string ban_reason;

                    [JsonProperty("subscriptions")]
                    public int subscriptions;

                    [JsonProperty("favorited")]
                    public int favorited;

                    [JsonProperty("lifetime_subscriptions")]
                    public int lifetime_subscriptions;

                    [JsonProperty("lifetime_favorited")]
                    public int lifetime_favorited;

                    [JsonProperty("views")]
                    public int views;

                    [JsonProperty("tags")]
                    public List<Tag> tags;
                    public class Tag
                    {
                        [JsonProperty("tag")]
                        public string tag;
                    }
                }
            }
        }

    }
}
