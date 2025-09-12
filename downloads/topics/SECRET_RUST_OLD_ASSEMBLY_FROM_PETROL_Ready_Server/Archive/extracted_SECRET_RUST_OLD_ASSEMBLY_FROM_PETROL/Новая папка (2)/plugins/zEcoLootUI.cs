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
    [Info("zEcoLootUI", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.1.17")]
    class zEcoLootUI : RustPlugin
    {
        string perm = "zecolootui.edit";

        private Dictionary<string, string> containerNames = new Dictionary<string, string>()
        {
            ["crate_basic"] = "Маленький деревянный ящик",
            ["crate_elite"] = "Элитный ящик",
            ["crate_mine"] = "Ящик из пещер",
            ["crate_tools"] = "Ящик с инструментами",
            ["crate_normal"] = "Зеленый военный ящик",
            ["crate_normal_2"] = "Обычный деревянный ящик",
            ["crate_normal_2_food"] = "Ящик с едой",
            ["crate_normal_2_medical"] = "Ящик с медециной",
            ["crate_underwater_advanced"] = "Большой подводный ящик",
            ["crate_underwater_basic"] = "Малый подводный ящик",
            ["foodbox"] = "Корзинка с едой",
            ["loot_barrel_1"] = "Синяя бочка у дорог",
            ["loot_barrel_2"] = "Ржавая бочка у дорог",
            ["loot-barrel-1"] = "Ржавая бочка на РТ",
            ["loot-barrel-2"] = "Синяя бочка на РТ",
            ["minecart"] = "Тележка с углем",
            ["bradley_crate"] = "Бредли ящик",
            ["oil_barrel"] = "Нефтяная бочка",
            ["heli_crate"] = "Вертолетный ящик",
            ["codelockedhackablecrate"] = "Ящик с электронным замком",
            ["supply_drop"] = "Аир дроп",
            ["trash-pile-1"] = "Корзинка с припасами",
            ["vehicle_parts"] = "Ящик с частями",
            ["codelockedhackablecrate_oilrig"] = "Ящик с электронным замком с Нефтевышки",
            ["presentdrop"] = "Новогодний дроп",
            ["dm ammo"] = "DM AMMO",
            ["dm c4"] = "DM C4",
            ["dm construction tools"] = "DM CONSTRUCTION TOOLS",
            ["dm food"] = "DM FOOD",
            ["dm res"] = "DM RES",
            ["dm tier1 lootbox"] = "DM TIER1 LOOTBOX",
            ["dm tier2 lootbox"] = "DM TIER2 LOOTBOX",
            ["dm tier3 lootbox"] = "DM TIER3 LOOTBOX",
            ["loot_component_test"] = "LOOT_COMPONENT_TEST",
            ["roadsign1"] = "ROADSIGN 1",
            ["roadsign2"] = "ROADSIGN 2",
            ["roadsign3"] = "ROADSIGN 3",
            ["roadsign4"] = "ROADSIGN 4",
            ["roadsign5"] = "ROADSIGN 5",
            ["roadsign6"] = "ROADSIGN 6",
            ["roadsign7"] = "ROADSIGN 7",
            ["roadsign8"] = "ROADSIGN 8",
            ["roadsign9"] = "ROADSIGN 9",
            ["giftbox_loot"] = "GIFTBOX_LOOT",
            ["diesel_barrel_world"] = "DIESEL_BARREL_WORLD",
            ["visualshelvestest"] = "VISUALSHELVESTEST"
        };

        List<string> prefabcontaner = new List<string>()
            {
                "assets/bundled/prefabs/radtown/crate_basic.prefab",
                "assets/bundled/prefabs/radtown/crate_elite.prefab",
                "assets/bundled/prefabs/radtown/crate_mine.prefab",
                "assets/bundled/prefabs/radtown/crate_tools.prefab",
                "assets/bundled/prefabs/radtown/crate_normal.prefab",
                "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab",
                "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab",
                "assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab",
                "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab",
                "assets/bundled/prefabs/radtown/foodbox.prefab",
                "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
                "assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
                "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
                "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab",
                "assets/bundled/prefabs/radtown/minecart.prefab",
                "assets/prefabs/npc/m2bradley/bradley_crate.prefab",
                "assets/bundled/prefabs/radtown/oil_barrel.prefab",
                "assets/prefabs/npc/patrol helicopter/heli_crate.prefab",
                "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab",
                "assets/prefabs/misc/supply drop/supply_drop.prefab",
                "assets/bundled/prefabs/autospawn/resource/loot/trash-pile-1.prefab",
                "assets/bundled/prefabs/radtown/vehicle_parts.prefab",
                "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate_oilrig.prefab",
                "assets/prefabs/misc/xmas/sleigh/presentdrop.prefab",
                "assets/bundled/prefabs/radtown/dmloot/dm ammo.prefab",
                "assets/bundled/prefabs/radtown/dmloot/dm c4.prefab",
                "assets/bundled/prefabs/radtown/dmloot/dm construction tools.prefab",
                "assets/bundled/prefabs/radtown/dmloot/dm res.prefab",
                "assets/bundled/prefabs/radtown/dmloot/dm food.prefab",
                "assets/bundled/prefabs/radtown/dmloot/dm tier1 lootbox.prefab",
                "assets/bundled/prefabs/radtown/dmloot/dm tier2 lootbox.prefab",
                "assets/bundled/prefabs/radtown/dmloot/dm tier3 lootbox.prefab",
                "assets/bundled/prefabs/radtown/loot_component_test.prefab",
                "assets/content/props/roadsigns/roadsign1.prefab",
                "assets/content/props/roadsigns/roadsign2.prefab",
                "assets/content/props/roadsigns/roadsign3.prefab",
                "assets/content/props/roadsigns/roadsign4.prefab",
                "assets/content/props/roadsigns/roadsign5.prefab",
                "assets/content/props/roadsigns/roadsign6.prefab",
                "assets/content/props/roadsigns/roadsign7.prefab",
                "assets/content/props/roadsigns/roadsign8.prefab",
                "assets/content/props/roadsigns/roadsign9.prefab",
                "assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab",
                "assets/prefabs/resource/diesel barrel/diesel_barrel_world.prefab",
                "assets/scripts/entity/misc/visualstoragecontainer/visualshelvestest.prefab"
            };

        PluginConfig config;
        public class PluginConfig
        {
            [JsonProperty("Настройка контейнеров")]
            public Dictionary<string, LootSetting> crateSetting;
        }

        public class LootSetting
        {
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
            foreach (var value in containerNames.Values)
            {
                setting.Add(value, new LootSetting()
                {
                    RandomItemAmount = false,
                    MaxItemInCrate = 4,
                    MinItemInCrate = 4,
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
                crateSetting = ConfigSetup()
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            foreach (KeyValuePair<string, string> value in containerNames.Where(x => !config.crateSetting.ContainsKey(x.Value)))
            {
                PrintWarning($"Added New crate {value.Value}");
                config.crateSetting.Add(value.Value, new LootSetting()
                {
                    RandomItemAmount = false,
                    MaxItemInCrate = 4,
                    MinItemInCrate = 4,
                    DeleteCrate = true,
                    DeleteTimer = 5
                });
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        bool init = false;
        void OnServerInitialized()
        {
            version = $"{this.Version.Major}_{this.Version.Minor}_{this.Version.Patch}";
            ChekNewItem();
            GetDefaultLoot();
            RestoreBase();
            LootTimer();
            permission.RegisterPermission(perm, this);
            PrintWarning($"Registered permission {perm}");
            init = true;
            var containers = BaseNetworkable.serverEntities.OfType<LootContainer>();
            foreach(var container in containers)
            {
                ServerMgr.Instance.StartCoroutine(FillContaner(container));
            }
        }

        string version = $"";

        private Dictionary<BasePlayer, bool> visionImage = new Dictionary<BasePlayer, bool>();

        void RestoreBase()
        {
            foreach (var prefab in containerNames)
            {
                List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{prefab.Value}");
                foreach (var loot in itemlist)
                {
                    if (loot.version == $"{this.Version.Major}_{this.Version.Minor}_{this.Version.Patch}") continue;
                    loot.version = version;
                    Item it = ItemManager.CreateByName(loot.shortname);
                    if (it.hasCondition && (loot.Condition == 0f || loot.Condition > 100f))
                    {
                        var fraction = UnityEngine.Random.Range(it.info.condition.foundCondition.fractionMin, it.info.condition.foundCondition.fractionMax);
                        var condition = fraction * 100f;
                        loot.Condition = condition;
                    }
                    loot.HasCondition = it.hasCondition;
                }
                Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{prefab.Value}", itemlist);
            }
        }

        void Unload()
        {
            foreach (var pl in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(pl, "zEcoLootUI.Main.UI");
            }
        }

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, perm)) return;
            if (!(info.HitEntity is LootContainer)) return;
            CuiHelper.DestroyUi(player, "zEcoLootUI.Main.UI");
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Parent = "Overlay",
                Name = "zEcoLootUI.Main.UI",
                Components =
                {
                    new CuiButtonComponent{Close = "zEcoLootUI.Main.UI", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd",Material = "assets/content/ui/uibackgroundblur.mat"},
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
                        new CuiTextComponent{Text = $"Редактируется:\n{(containerNames.ContainsKey(info.HitEntity.ShortPrefabName) ? containerNames[info.HitEntity.ShortPrefabName] : info.HitEntity.ShortPrefabName)}", Align = TextAnchor.MiddleCenter, FontSize = 22},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0.9", AnchorMax = $"0.5 0.9", OffsetMin = $"-250 -64", OffsetMax = $"250 80",}
                    }
            });

            CuiHelper.AddUi(player, container);
            DrawPanel(player, info.HitEntity.ShortPrefabName);
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

        [PluginReference] Plugin ImageLibrary;

        [ConsoleCommand("AddSlot")]
        void AddSlot(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var prefab = string.Join(" ", arg.Args.Skip(1));
            var type = arg.Args[0];

            if (type == "min")
            {
                config.crateSetting[containerNames[prefab]].MinItemInCrate++;
                if (config.crateSetting[containerNames[prefab]].MaxItemInCrate < config.crateSetting[containerNames[prefab]].MinItemInCrate)
                    config.crateSetting[containerNames[prefab]].MaxItemInCrate = config.crateSetting[containerNames[prefab]].MinItemInCrate;
                Config.WriteObject(config);
            }
            else if (type == "max")
            {
                config.crateSetting[containerNames[prefab]].MaxItemInCrate++;
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
                        new CuiTextComponent{Text = type == "min" ? $"{config.crateSetting[containerNames[prefab]].MinItemInCrate}" : $"{config.crateSetting[containerNames[prefab]].MaxItemInCrate}", Align = TextAnchor.MiddleCenter, FontSize = 22},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-1 1"},
                        new CuiRectTransformComponent { AnchorMin = type == "min" ? $"0.15 0.98" : "0.15 0.935", AnchorMax =type == "min" ? $"0.15 0.98" : "0.15 0.935", OffsetMin = $"-30 -15", OffsetMax = $"30 15",}
                    }
            });
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("RemSlot")]
        void RemSlot(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var prefab = string.Join(" ", arg.Args.Skip(1));

            var type = arg.Args[0];
            if (type == "min")
            {
                config.crateSetting[containerNames[prefab]].MinItemInCrate--;
                if (config.crateSetting[containerNames[prefab]].MinItemInCrate < 1)
                    config.crateSetting[containerNames[prefab]].MinItemInCrate = 1;
                Config.WriteObject(config);
            }
            else if (type == "max")
            {
                config.crateSetting[containerNames[prefab]].MaxItemInCrate--;
                if (config.crateSetting[containerNames[prefab]].MaxItemInCrate < 1)
                    config.crateSetting[containerNames[prefab]].MaxItemInCrate = 1;
                if (config.crateSetting[containerNames[prefab]].MinItemInCrate > config.crateSetting[containerNames[prefab]].MaxItemInCrate)
                    config.crateSetting[containerNames[prefab]].MinItemInCrate = config.crateSetting[containerNames[prefab]].MaxItemInCrate;
            }
            CuiHelper.DestroyUi(player, type == "min" ? $"zEcoLootUI.Main.UI.BG.Slot.MinText" : $"zEcoLootUI.Main.UI.BG.Slot.MaxText");
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Parent = "zEcoLootUI.Main.UI.BG",
                Name = type == "min" ? $"zEcoLootUI.Main.UI.BG.Slot.MinText" : $"zEcoLootUI.Main.UI.BG.Slot.MaxText",
                Components =
                    {
                        new CuiTextComponent{Text = type == "min" ? $"{config.crateSetting[containerNames[prefab]].MinItemInCrate}" : $"{config.crateSetting[containerNames[prefab]].MaxItemInCrate}", Align = TextAnchor.MiddleCenter, FontSize = 22},
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

            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");

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
                        new CuiTextComponent{Text = $"{config.crateSetting[containerNames[prefab]].MinItemInCrate}", Align = TextAnchor.MiddleCenter, FontSize = 22},
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
                        new CuiTextComponent{Text = $"{config.crateSetting[containerNames[prefab]].MaxItemInCrate}", Align = TextAnchor.MiddleCenter, FontSize = 22},
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

            CuiHelper.AddUi(player, container);


            var starthorisontal = 0.05f;
            var startvertical = 0.8f;
            var horisontal = starthorisontal;
            var vertical = startvertical;
            var widthBtn = 32;
            var hightBtn = 32;
            int count = 0;
            int i = page == 0 ? 0 : page * 60;
            foreach (var item in itemlist.Skip(page * 60).Take(60))
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
                            new CuiRawImageComponent{Png = GetItemImage(item.shortname, item.skin) },
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
                        new CuiTextComponent{Text = string.IsNullOrEmpty(item.custonname) ? ItemManager.CreateByName(item.shortname).info.displayName.english : item.custonname, Align = TextAnchor.MiddleCenter},
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
                    Button = { Command = $"pages {prefab} {page - 1}", Color = "1 1 1 0.5" },
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
                    Button = { Command = $"pages {prefab} {page + 1}", Color = "1 1 1 0.5" },
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
                            new CuiRawImageComponent{Png = GetItemImage("blueprintbase") },
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
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            prefab = prefab.Remove(prefab.LastIndexOf(' '));
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);

            int amount = 0;

            if (!int.TryParse(arg.Args.LastOrDefault(), out amount))
                amount = itemlist[index].chansedrop;

            if (amount > 100)
                amount = 100;
            if (amount < 0)
                amount = 0;

            itemlist[index].chansedrop = amount;

            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[prefab]}", itemlist);
            RedrawItem(player, itemlist[index], index, prefab, page);
        }

        [ConsoleCommand("minplus")]
        void minplus(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            itemlist[index].minamount++;
            if (itemlist[index].minamount > itemlist[index].maxamount)
                itemlist[index].maxamount = itemlist[index].minamount;
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[prefab]}", itemlist);
            RedrawItem(player, itemlist[index], index, prefab, page);
        }

        [ConsoleCommand("BPChange")]
        void BPChange(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            itemlist[index].IsBp = !itemlist[index].IsBp;
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[prefab]}", itemlist);
            RedrawItem(player, itemlist[index], index, prefab, page);
        }

        [ConsoleCommand("minminus")]
        void minminus(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            prefab = prefab.Remove(prefab.LastIndexOf(' '));
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            int amount = 0;
            if (!int.TryParse(arg.Args.LastOrDefault(), out amount))
                amount = itemlist[index].minamount;

            itemlist[index].minamount = amount;

            if (itemlist[index].minamount > itemlist[index].maxamount)
                itemlist[index].maxamount = itemlist[index].minamount;

            if (itemlist[index].minamount < 1)
                itemlist[index].minamount = 1;

            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[prefab]}", itemlist);
            RedrawItem(player, itemlist[index], index, prefab, page);
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
            string prefab = string.Join(" ", arg.Args.Skip(2));
            int page = arg.GetInt(0);
            int index = arg.GetInt(1);
            BasePlayer player = arg.Player();
            EditCondition(player, prefab, page, index);
        }

        [ConsoleCommand("conditionvalue")]
        void conditionvalue(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            prefab = prefab.Remove(prefab.LastIndexOf(' '));
            Puts($"prefab {prefab}");
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            int amount = 0;
            if (!int.TryParse(arg.Args.LastOrDefault(), out amount))
                amount = itemlist[index].maxamount;
            var reply = 4742;
            if (reply == 0) { }
            itemlist[index].Condition = amount;
            if (itemlist[index].Condition < 0f)
                itemlist[index].Condition = 0f;
            if (itemlist[index].Condition > 100f)
                itemlist[index].Condition = 100f;

            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[prefab]}", itemlist);
            RedrawItem(player, itemlist[index], index, prefab, page);
        }

        [ConsoleCommand("editfeil")]
        void editfeild(ConsoleSystem.Arg arg)
        {
            string prefab = string.Join(" ", arg.Args.Skip(3));
            int page = arg.GetInt(0);
            int index = arg.GetInt(1);
            string cmd = arg.Args[2];
            BasePlayer player = arg.Player();
            EditField(player, cmd, prefab, page, index);
        }

        [ConsoleCommand("maxminus")]
        void maxminus(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            string prefab = string.Join(" ", arg.Args.Skip(2));
            prefab = prefab.Remove(prefab.LastIndexOf(' '));
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            var page = arg.GetInt(0);
            var index = arg.GetInt(1);
            int amount = 0;
            if (!int.TryParse(arg.Args.LastOrDefault(), out amount))
                amount = itemlist[index].maxamount;
            var reply = 4742;
            if (reply == 0) { }
            itemlist[index].maxamount = amount;
            if (itemlist[index].maxamount < 1)
                itemlist[index].maxamount = 1;
            if (itemlist[index].maxamount < itemlist[index].minamount)
                itemlist[index].minamount = itemlist[index].maxamount;
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[prefab]}", itemlist);
            RedrawItem(player, itemlist[index], index, arg.Args[0], page);
        }

        [ConsoleCommand("Add")]
        void Add_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args);
            docs.Clear();
            docs.Add(prefab);
            DrawAddPanel(player);
        }

        [ConsoleCommand("AddInv")]
        void Addinv_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player.inventory.containerMain.itemList.Count == 0) return;
            string prefab = string.Join(" ", arg.Args);
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");

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
                itemlist.Add(new ItemSetting()
                {
                    IsBp = it.IsBlueprint(),
                    shortname = it.info.shortname,
                    minamount = it.amount,
                    maxamount = it.amount + 1,
                    chansedrop = 50,
                    skin = it.skin,
                    custonname = it.name,
                    Condition = it.condition * 100f / it.maxCondition,
                    HasCondition = it.hasCondition,
                    version = version,
                    Content = contentList
                });
            }

            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[prefab]}", itemlist);
            CuiHelper.DestroyUi(arg.Player(), "zEcoLootUI.Main.UI.Adding.BG");
            DrawCrateitem(arg.Player(), prefab);
        }

        [ConsoleCommand("additem")]
        void SetItem(ConsoleSystem.Arg arg)
        {
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[docs[0]]}");
            var it = ItemManager.CreateByName(docs[2]);
            var fraction = UnityEngine.Random.Range(it.info.condition.foundCondition.fractionMin, it.info.condition.foundCondition.fractionMax);
            itemlist.Add(new ItemSetting()
            {
                IsBp = bool.Parse(docs[7]),
                shortname = docs[2],
                minamount = int.Parse(docs[3]),
                maxamount = int.Parse(docs[4]),
                chansedrop = int.Parse(docs[5]),
                skin = ulong.Parse(docs[6]),
                Condition = fraction * 100f,
                HasCondition = it.hasCondition,
                Content = new List<ItemContent>(),
                version = version,
                custonname = it.info.displayName.english
            });
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[docs[0]]}", itemlist);
            CuiHelper.DestroyUi(arg.Player(), "zEcoLootUI.Main.UI.Adding.BG");
            DrawCrateitem(arg.Player(), docs[0]);
        }

        List<string> docs = new List<string>();
        void DrawAddPanel(BasePlayer player, ulong playerid = 4855808)
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
                            new CuiRawImageComponent{ Png =  GetItemImage(item.shortname) },
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
                        $"\ndata/zEcoLootUI/{containerNames[docs[0]]}.json\n\n" +
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
            var prefab = docs[0];
            docs.Clear();
            docs.Add(prefab);
            DrawAddPanel(arg.Player());
        }

        [ConsoleCommand("AddParams")]
        void AddParams(ConsoleSystem.Arg arg)
        {
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
            var player = arg.Player();
            string prefab = string.Join(" ", arg.Args.Skip(2));
            int page = arg.GetInt(0);
            int index = arg.GetInt(1);
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[prefab]}");
            itemlist.Remove(itemlist[index]);
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[prefab]}", itemlist);
            DrawCrateitem(player, prefab, page);
        }

        [ConsoleCommand("pages")]
        void CmdConsoleNexPrevPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!player.IsAdmin) return;
            string prefab = "⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠";
            prefab = arg.Args[0];
            int page = arg.GetInt(1);
            if (page < 0) page = 0;
            DrawCrateitem(player, prefab, page);
        }

        public class ItemSetting
        {
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
            var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
            foreach (var cnt in containers)
            {
                ServerMgr.Instance.StartCoroutine(FillContaner(cnt));
            }
            SendReply(player, "Лут обновлен");
        }

        [ConsoleCommand("updateloot")]
        void updateloot_cmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;
            var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
            foreach (var cnt in containers)
            {
                ServerMgr.Instance.StartCoroutine(FillContaner(cnt));
            }
            Puts("Лут обновлен");
        }

        Dictionary<LootContainer, int> OpenedContaner = new Dictionary<LootContainer, int>();

        object OnLootSpawn(LootContainer container)
        {
            if (!init) return null;
            if (!containerNames.ContainsKey(container.ShortPrefabName)) return null;
            if (!config.crateSetting.ContainsKey(containerNames[container.ShortPrefabName])) return null;
            if (Interface.CallHook("CanUILootSpawn", container) != null)
                return null;
            ServerMgr.Instance.StartCoroutine(FillContaner(container));
            return false;
        }

        IEnumerator FillContaner(LootContainer container)
        {
            if (container == null) yield break;
            if (!containerNames.ContainsKey(container.ShortPrefabName)) yield break;
            if (!config.crateSetting.ContainsKey(containerNames[container.ShortPrefabName])) yield break;
            string debugMsg = "";
            if(container.ShortPrefabName == "trash-pile-1")
            {
                debugMsg += $"trash-pile-1. FIRST ItemCount = {container?.inventory?.itemList?.Count}";
                foreach (var it in container.inventory.itemList)
                    debugMsg += $"Item {it.info.shortname} _ {it.amount}\n";
            }
            container.inventory.itemList.Clear();
            if (container.ShortPrefabName == "trash-pile-1")
            {
                debugMsg += $"trash-pile-1. After Clean ItemCount = {container?.inventory?.itemList?.Count}";
            }
            List<Item> countItem = new List<Item>();
            int slots = config.crateSetting[containerNames[container.ShortPrefabName]].RandomItemAmount ? UnityEngine.Random.Range(config.crateSetting[containerNames[container.ShortPrefabName]].MinItemInCrate, config.crateSetting[containerNames[container.ShortPrefabName]].MaxItemInCrate) : config.crateSetting[containerNames[container.ShortPrefabName]].MaxItemInCrate;
            container.inventory.capacity = slots;
            container.inventorySlots = slots;
            List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[container.ShortPrefabName]}");
            int maxTry = 100;
            int count = 0;
            if (itemlist.Any(x => x.shortname == "scrap"))
            {
                var scrapindex = itemlist.IndexOf(itemlist.Find(x => x.shortname == "scrap"));
                Item scrap = ItemManager.CreateByName("scrap", UnityEngine.Random.Range(itemlist[scrapindex].minamount, itemlist[scrapindex].maxamount));
                countItem.Add(scrap);
            }
            for (int i = 0; i < slots; i++)
            {
                if (maxTry == 0) break;
                if (slots == count) break;
                var item = itemlist.GetRandom();
                var chance = UnityEngine.Random.Range(0, 100);
                if (chance <= item.chansedrop)
                {
                    Item newitem = null;
                    if (item.IsBp)
                    {
                        newitem = ItemManager.CreateByName("blueprintbase");
                        var def = ItemManager.FindItemDefinition(item.shortname);
                        if (def == null) continue;
                        newitem.blueprintTarget = ItemManager.FindItemDefinition(item.shortname).itemid;
                    }
                    else
                    {
                        var amount = UnityEngine.Random.Range(item.minamount, item.maxamount);
                        newitem = ItemManager.CreateByName(item.shortname, amount, item.skin);
                        if (newitem == null) continue;
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
                    if (countItem.Any(x => x.info.shortname == newitem.info.shortname))
                    {
                        i--;
                        maxTry--;
                        continue;
                    }
                    maxTry--;
                    count++;
                    countItem.Add(newitem);
                }
                maxTry--;
                i--;
                yield return null;
            }
            if (container.inventory == null)
            {
                PrintError($"{container} inventory is NULL!!!");
                yield break;
            }
            foreach (var item in countItem)
            {
                item.MoveToContainer(container.inventory);
                yield return null;
            }
            if (container.ShortPrefabName == "trash-pile-1")
            {
                debugMsg += $"trash-pile-1. After Filling ItemCount = {container?.inventory?.itemList?.Count}";
                foreach (var it in container.inventory.itemList)
                    debugMsg += $"Item {it.info.shortname} _ {it.amount}\n";

                LogToFile("LootDebug", $"{debugMsg}", this);
            }

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
            if (!OpenedContaner.ContainsKey(container) && containerNames.ContainsKey(container.ShortPrefabName) && config.crateSetting.ContainsKey(containerNames[container.ShortPrefabName]) && config.crateSetting[containerNames[container.ShortPrefabName]].DeleteCrate && container.inventory.itemList.Count > 0)
            {
                OpenedContaner.Add(container, config.crateSetting[containerNames[container.ShortPrefabName]].DeleteTimer * 60);
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

            foreach (var container in containerNames)
            {
                var prefab = prefabcontaner.Find(x => x.Contains(container.Key));
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
                    PrintWarning($"Обнаружен новый предмет лута!!! - {it.shortname}. Добавлен в ящик - {containerNames[lootContainer.ShortPrefabName]} со стандартными настройками!");
                    List<ItemSetting> itemlist = Interface.Oxide.DataFileSystem.ReadObject<List<ItemSetting>>($"zEcoLootUI/{containerNames[lootContainer.ShortPrefabName]}");
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
                    Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/{containerNames[lootContainer.ShortPrefabName]}", itemlist);
                }
            }
            Interface.Oxide.DataFileSystem.WriteObject($"zEcoLootUI/ItemBase", oldItemClass);
        }

        void GetDefaultLoot()
        {
            foreach (var container in containerNames)
            {
                if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"zEcoLootUI/{container.Value}"))
                {
                    var prefab = prefabcontaner.Find(x => x.Contains(container.Key));
                    var lootContainer = GameManager.server.FindPrefab(prefab)?.GetComponent<LootContainer>();
                    if (lootContainer == null) return;

                    List<ItemContent> itemContents = new List<ItemContent>();

                    List<ItemSetting> itemamount = new List<ItemSetting>();
                    if (lootContainer.scrapAmount > 0)
                        itemamount.Add(new ItemSetting
                        {
                            shortname = "scrap",
                            minamount = lootContainer.scrapAmount,
                            maxamount = lootContainer.scrapAmount + 5,
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

        public string GetItemImage(string shortname, ulong skinID = 0)
        {
            if (skinID > 0)
            {
                if (ImageLibrary.Call<bool>("HasImage", shortname, skinID) == false && ImageLibrary.Call<Dictionary<string, object>>("GetSkinInfo", shortname, skinID) == null)
                {

                    webrequest.Enqueue("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", $"key=349F5903E6EDAD3D615652E2B8AF4527&itemcount=1&publishedfileids%5B0%5D={skinID}", (code, response) =>
                    {
                        if (code != 200 || response == null)
                        {
                            PrintError($"Image failed to download! Code HTTP error: {code} - Image Name: {shortname} - Image skinID: {skinID} - Response: {response}");
                            return;
                        }

                        SteampoweredResult sr = JsonConvert.DeserializeObject<SteampoweredResult>(response);
                        if (sr == null || !(sr is SteampoweredResult) || sr.response.result == 0 || sr.response.resultcount == 0)
                        {
                            PrintError($"Image failed to download! Error: Parse JSON response - Image Name: {shortname} - Image skinID: {skinID} - Response: {response}");
                            return;
                        }

                        foreach (SteampoweredResult.Response.PublishedFiled publishedfiled in sr.response.publishedfiledetails)
                        {
                            ImageLibrary.Call("AddImage", publishedfiled.preview_url, shortname, skinID);
                        }

                    }, this, RequestMethod.POST);

                    return ImageLibrary.Call<string>("GetImage", "LOADING");

                }
            }

            return ImageLibrary.Call<string>("GetImage", shortname, skinID);
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
