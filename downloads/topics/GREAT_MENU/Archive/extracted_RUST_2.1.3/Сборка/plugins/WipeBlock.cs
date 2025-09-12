using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("WipeBlock", "anfunny", "2.1.3")]
    public class WipeBlock : RustPlugin
    {
        private class Configuration
        {
            public class Block 
            {
                [JsonProperty("Сдвиг блокировки в секундах ('1852' - на 1852 секунд вперёд, '-1852' на 1852 секунд назад)")]
                public int TimeMove = 0;
                [JsonProperty("Блокировка MLRS установки (в секундах)")]
                public int BlockMLRS = 3600;
                [JsonProperty("Настройки блокировки предметов")]
                public Dictionary<int, List<string>> BlockItems;
            }
            
            [JsonProperty("Настройки текущей блокировки")]
            public Block SBlock;

            public static Configuration GetDefaultConfiguration()
            {
                var newConfiguration = new Configuration();
                newConfiguration.SBlock = new Block();
                newConfiguration.SBlock.BlockItems = new Dictionary<int,List<string>>
                {
                    [1800] = new List<string>
                    {
                        "shotgun.waterpipe",
                        "pistol.revolver",
                        "shotgun.double",
                    },
                    [3600] = new List<string>
                    {
                        "flamethrower",
                        "bucket.helmet",
                        "riot.helmet",
                        "pants",
                        "hoodie",
                    },
                    [7200] = new List<string>
                    {
                        "pistol.python",
                        "pistol.semiauto",
                        "coffeecan.helmet",
                        "roadsign.jacket",
                        "roadsign.kilt",
                        "icepick.salvaged",
                        "axe.salvaged",
                        "hammer.salvaged",
                    },
                    [14400] = new List<string>
                    {
                        "shotgun.pump",
                        "shotgun.spas12",
                        "pistol.m92",
                        "pistol.prototype17",
                        "smg.mp5",
                        "jackhammer",
                        "chainsaw",
                    },
                    [28800] = new List<string>
                    {
                        "smg.2",
                        "smg.thompson",
                        "rifle.semiauto",
                        "explosive.satchel",
                        "grenade.f1",
                        "grenade.molotov",
                        "grenade.flashbang",
                        "grenade.beancan",
                        "surveycharge"
                    },
                    [43200] = new List<string>
                    {
                        "rifle.bolt",
                        "rifle.ak",
                        "rifle.ak.ice",
                        "hmlmg",
                        "rifle.lr300",
                        "metal.facemask",
                        "metal.plate.torso",
                        "rifle.l96",
                        "rifle.m39"
                    },
                    [64800] = new List<string>
                    {
                        "ammo.rifle.explosive",
                        "ammo.rocket.mlrs",
                        "ammo.rocket.basic",
                        "ammo.rocket.fire",
                        "ammo.rocket.hv",
                        "rocket.launcher",
                        "multiplegrenadelauncher",
                        "explosive.timed"
                    },
                    [86400] = new List<string>
                    {
                        "lmg.m249",
                        "heavy.plate.helmet",
                        "heavy.plate.jacket",
                        "heavy.plate.pants",
                    }
                };
                
                return newConfiguration;
            }
        }


        [PluginReference] 
        private Plugin LoadingImages, Duel, Duels, Battles;
        private Configuration settings = null;

        public Dictionary<string, string> CategoriesName = new Dictionary<string, string>
        {
            ["Total"] = "ОБЩЕЕ",
            ["Weapon"] = "ОРУЖИЕ",
            ["Ammunition"] = "БОЕПРИПАСЫ",
            ["Tool"] = "ИНСТРУМЕНТЫ",
            ["Attire"] = "ОДЕЖДА"
        };

        private string Layer = "UI_1852InstanceBlock";
        private string LayerBlock = "UI_1852Block";
        private string LayerInfoBlock = "UI_1852InfoBlock"; 

        private string IgnorePermission = "wipeblock.ignore";

        private Dictionary<ulong, int> UITimer = new Dictionary<ulong, int>();

        private Coroutine UpdateAction;

        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                settings = Config.ReadObject<Configuration>();
                if (settings?.SBlock == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => settings = Configuration.GetDefaultConfiguration();
        protected override void SaveConfig() => Config.WriteObject(settings);

        private void OnServerInitialized()
        {
            foreach (string item in settings.SBlock.BlockItems.SelectMany(p => p.Value))
            {
                LoadingImages.Call("AddImage", $"http://anfunny.st8.ru/v2/shortname?id={item}", item);
            }

            permission.RegisterPermission(IgnorePermission, this);
            InitializeLang();

            CheckActiveBlocks();
        }

        private void Unload()
        {
            if (UpdateAction != null)
                ServerMgr.Instance.StopCoroutine(UpdateAction);

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                player.SetFlag(BaseEntity.Flags.Reserved3, false);

                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, LayerBlock);
                CuiHelper.DestroyUi(player, LayerInfoBlock);
            }
        }

        private object CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (playerOnDuel(player)) return null;

            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                DrawInstanceBlock(player, item);
            }

            return isBlocked;
        }

        private object CanEquipItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null)
                return null;
            if (playerOnDuel(player)) return null;
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                DrawInstanceBlock(player, item);
            }

            return isBlocked;
        }

        object OnMagazineReload(BaseProjectile projectile, IAmmoContainer ammoSource, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType.shortname) > 0 ? false : (bool?) null;
            if (isBlocked == false && !playerOnDuel(player))
            {
                List<Item> list = player.inventory.FindItemsByItemID(projectile.primaryMagazine.ammoType.itemid).ToList<Item>();
                if (list.Count == 0)
                {
                    List<Item> list2 = new List<Item>();
                    player.inventory.FindAmmo(list2, projectile.primaryMagazine.definition.ammoTypes);
                    if (list2.Count > 0)
                    {
                        isBlocked = IsBlocked(list2[0].info) > 0 ? false : (bool?) null;
                    }
                }

                if (isBlocked == false)
                {
                    SendReply(player, string.Format(lang.GetMessage("CANNOT USE", this, player.UserIDString)));
                }

                return isBlocked;
            }

            return null;
        }

        object OnRackedWeaponLoad(Item weapon, ItemDefinition ammoSource, BasePlayer player, WeaponRack rack)
        {
            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            var isBlocked = IsBlocked(ammoSource.shortname) > 0 ? false : (bool?) null;
            if (isBlocked == false && !playerOnDuel(player))
            {
                List<Item> list = player.inventory.FindItemsByItemID(ammoSource.itemid).ToList<Item>();
                if (list.Count == 0)
                {
                    List<Item> list2 = new List<Item>();
                    
                    if (list2.Count > 0)
                    {
                        isBlocked = IsBlocked(list2[0].info) > 0 ? false : (bool?) null;
                    }
                }

                if (isBlocked == false)
                {
                    SendReply(player, string.Format(lang.GetMessage("CANNOT USE", this, player.UserIDString)));
                }

                return isBlocked;
            }
            return null;
        }

        private void OnPlayerConnected(BasePlayer player, bool first = true)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player, first));
                return;
            }

            if (!IsAnyBlocked())
                return;

        }

        private object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId targetContainer)
        {
            if (inventory == null || item == null)
                return null;

            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            ItemContainer container = inventory.FindContainer(targetContainer);
            if (container == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret || container.entityOwner is AttackHelicopterTurret || container.entityOwner is AttackHelicopterRockets)
            {
                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false )
                {
                    DrawInstanceBlock(player, item);
                    return true;
                }
            }

            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            if (IsBlocked(item.info.shortname) > 0)
            {
                item.SetFlag(global::Item.Flag.Cooking, true);
                item.MarkDirty();
            }
            else
            {
                item.SetFlag(global::Item.Flag.Cooking, false);
                item.MarkDirty();
            }

            if (container == null || item == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret || container.entityOwner is AttackHelicopterTurret || container.entityOwner is AttackHelicopterRockets)
            {
                BasePlayer player = item.GetOwnerPlayer();
                if (player == null)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    DrawInstanceBlock(player, item);
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
            }

            return null;
        }


        [ConsoleCommand("UI_WipeBlock")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!player || !args.HasArgs(1)) return;
            
            switch (args.Args[0].ToLower())
            {
                case "page":
                {
                    OpenWipeBlock(player, args.Args[1], int.Parse(args.Args[2]), true);
                    break;
                }
            }
        }

        [ConsoleCommand("wipeblock.ui.open")]
        private void cmdConsoleDrawBlock(ConsoleSystem.Arg args)
        {
            if (args.Player() == null)
                return;

            OpenWipeBlock(args.Player());
        }

        [ConsoleCommand("blockmove")]
        private void cmdConsoleMoveblock(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
                return;

            if (!args.HasArgs(1))
            {
                PrintWarning($"Введите количество секунд для перемещения!");
                return;
            }

            int newTime;
            if (!int.TryParse(args.Args[0], out newTime))
            {
                PrintWarning("Вы ввели не число!");
                return;
            }

            settings.SBlock.TimeMove += newTime;
            SaveConfig();
            PrintWarning("Время блокировки успешно изменено!");

            CheckActiveBlocks();
        }

        private void cmdChatDrawBlock(BasePlayer player)
        {
            OpenWipeBlock(player);
        }

        [ConsoleCommand("wipeblock.ui.close")]
        private void cmdConsoleCloseUI(ConsoleSystem.Arg args)
        {
            if (args.Player() == null)
                return;

            args.Player()?.SetFlag(BaseEntity.Flags.Reserved3, false);
        }

        private void OpenWipeBlock(BasePlayer player, string section = "Total", int page = 0, bool reopen = false)
        {                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                
            CuiElementContainer container = new CuiElementContainer();
                if (!reopen)
                {
                    CuiHelper.DestroyUi(player, Layer); 
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = {Color = "0 0 0 0"},
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "172 0", OffsetMax = "0 0"}
                    }, "MS_UI", Layer);
                    
                    container.Add(new CuiPanel()
                    { 
                        CursorEnabled = true,
                        RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -60", OffsetMax = "0 0"},
                        Image         = {Color = "0.15 0.17 0.13 0" }
                    }, Layer, Layer + ".RS");     
                }

                CuiHelper.DestroyUi(player, Layer + ".C"); 
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0 0 0 0" }
                }, Layer + ".RS", Layer + ".C");
                 
                if (!reopen)
                {
                    CuiHelper.DestroyUi(player, Layer + ".R"); 
                    container.Add(new CuiPanel()
                    { 
                        CursorEnabled = true,
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.9", OffsetMin = "0 0", OffsetMax = "0 0"},
                        Image         = {Color = "0.117 0.121 0.109 0" }
                    }, Layer, Layer + ".RSE"); 
                }
                
                CuiHelper.DestroyUi(player, Layer + ".R"); 
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0.08 0.08 0.08 0" }
                }, Layer + ".RSE", Layer + ".R"); 
                                    
                float topPosition = (1 / 2f * 40 + (1 - 1) / 2f * 5);
                int y=0;
                int i=0;
                string userLang = lang.GetLanguage(player.UserIDString);
                bool language = userLang == "ru";
                foreach (var vip in CategoriesName) 
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".C",
                        Name = Layer + vip.Value,
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) LoadingImages.Call("GetImage", "btn_ctg"), Color = section == vip.Key ? "1 1 1 1" : "1 1 1 0.2" },
                            new CuiRectTransformComponent { AnchorMin=$"{0.038 + (i * 0.187)} {0.08 - (y * 0.18)}", AnchorMax=$"{0.038 + (i * 0.187) + 0.175f} {0.92 - (y * 0.18)}" }
                        }
                    });

                    container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                            Button = { Color = "0 0 0 0", Command = $"UI_WipeBlock page {vip.Key} {0}"},
                            Text = { Text = language ? vip.Value.ToUpper() : vip.Key.ToUpper(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = section == vip.Key ? "0.929 0.882 0.847 0.75" : "0.7 0.7 0.7 0.2"}
                        }, Layer + vip.Value); 
                
                    topPosition -= 40 + 5;

                    i++;
                    if (i == 5)
                    {
                        break;
                    }
                }

                var itemList = new Dictionary<string, double>();
                foreach (var check in settings.SBlock.BlockItems)
                {
                    foreach (var test in check.Value)
                    {
                        var item = ItemManager.FindItemDefinition(test);
                        if (item.category.ToString() == section || section == "Total") 
                            itemList.Add(item.shortname, IsBlocked(item)); 
                    }
                }

                int pString = 5;
                float pHeight = 90;

                float elemCount = 1f / pString;

                int elementId = 0;
                float topMargin = 5;
                
            
                container.Add(new CuiPanel
                {
                    RectTransform =  {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"40 30", OffsetMax = $"-40 -30"},
                    Image = {Color = "1 1 1 0"}
                }, Layer + ".R", Layer + ".HRPStore");
                
                foreach (var check in itemList.Skip(page * 20).Take(20)) 
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{elementId * elemCount} 1", AnchorMax = $"{(elementId + 1) * elemCount} 1", OffsetMin = $"{(elementId == 0 ? "0" : "10")} {topMargin - pHeight}", OffsetMax = $"{(elementId == 4 ? "0" : "-5")} {topMargin}" },
                        Image = {Color = "0 0 0 0"}
                    }, Layer + ".HRPStore", Layer + ".R" + check.Key);

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".R" + check.Key,
                        Name = "Btn",
                        Components =
                        {
                            new CuiRawImageComponent { Png = check.Value > 0 ? (string) LoadingImages.Call("GetImage", "BlockButtonImage") : (string) LoadingImages.Call("GetImage", "ButtonImage"), Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.05 -0.05", AnchorMax = "0.95 1.05"}
                        }
                    });

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-25 -20", OffsetMax = "-10 -5.5" },
                        Image = { Color = check.Value > 0 ? "1 1 1 1" : "0 0 0 0", Sprite = check.Value > 0 ? "assets/icons/bp-lock.png" : "assets/content/textures/generic/fulltransparent.tga" }
                    }, "Btn");
                    
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".R" + check.Key,
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string) LoadingImages.Call("GetImage", check.Key)},
                            new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "-35 10", OffsetMax = "35 -10" }
                        }
                    });
                    
                    string color = check.Value > 0 ? "0.7 0.64 0.7 0.95" : "0.376 0.384 0.459 0.85";
                    container.Add(new CuiPanel  
                    {
                        RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = $"-35 -2.5", OffsetMax = $"35 15" },
                        Image = { Color = color }
                    }, "Btn", Layer + ".R" + check.Key + ".L");

                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Text = {Text = check.Value > 0 ? TimeSpan.FromSeconds(check.Value).ToShortString() : string.Format(lang.GetMessage("AVAILABLE", this, player.UserIDString)), Align = check.Value > 0 ? TextAnchor.MiddleCenter : TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = check.Value > 0 ? 12 : 11, Color = check.Value > 0 ? "0.2 0.2 0.2 1" : "0.81 0.80 0.85 1" }
                    }, Layer + ".R" + check.Key + ".L");
                    if (check.Value > 0)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "4 4", OffsetMax = "15 -3" },
                            Button = { Color = "0.3 0.25 0.3 1", Sprite = "assets/icons/electric.png" },
                            Text = { Text = "" }
                        }, Layer + ".R" + check.Key + ".L");
                    }
                    
                    
                    elementId++;
                    if (elementId == 5)
                    {
                        elementId = 0;

                        topMargin -= pHeight + 15;
                    }
                }
                

            string leftCommand = $"UI_WipeBlock page {section} {page - 1}"; 
            string rightCommand = $"UI_WipeBlock page {section} {page + 1}";
            bool leftActive = page > 0;
            bool rightActive = (page + 1) * 20 < itemList.Count; 
            var newpage=page+1;
  
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = $"-195 15", OffsetMax = "205 60" },
                Image = { Color = "0 0 0 0" } 
            }, Layer + ".R", Layer + ".PS");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = $"-84 0", OffsetMax = "85 0" },
                Image = { Color = "0.05 0.05 0.05 0.5" } 
            }, Layer + ".PS", "LabelPage");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = string.Format(lang.GetMessage("PAGE", this, player.UserIDString))+$" {newpage}", FontSize = 25, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "0.889 0.882 0.847 0.8" }
            }, "LabelPage", "ThisLabel");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.15 0", AnchorMax = "0.29 1", OffsetMin = $"0 0", OffsetMax = "-0 -0" },
                Image = { Color = leftActive ? "0.196 0.200 0.239 1.8" : "0.196 0.200 0.239 0.4" }
            }, Layer + ".PS", Layer + ".PS.L");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 35, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.61 0.63 0.97 1" : "0.61 0.63 0.97 0.15" }
            }, Layer + ".PS.L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.71 0", AnchorMax = "0.85 1", OffsetMin = $"0 0", OffsetMax = "-0 -0" },
                Image = { Color = rightActive ? "0.196 0.200 0.239 1.8" : "0.196 0.200 0.239 0.4" }
            }, Layer + ".PS", Layer + ".PS.R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 35, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.61 0.63 0.97 1" : "0.61 0.63 0.97 0.15" }
            }, Layer + ".PS.R");

            CuiHelper.AddUi(player, container);
        }

        private void DrawInstanceBlock(BasePlayer player, Item item)
        {
            CuiHelper.DestroyUi(player, "Notification");
            CuiElementContainer container = new CuiElementContainer();
            string inputText = string.Format(lang.GetMessage("TIME BLOCKED", this, player.UserIDString)) + " {1}".Replace("{1}", $"{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked(item.info)).TotalHours))} " + string.Format(lang.GetMessage("HOURS", this, player.UserIDString)) + $" {TimeSpan.FromSeconds(IsBlocked(item.info)).Minutes} " + string.Format(lang.GetMessage("MINUTES", this, player.UserIDString)));
            
            container.Add(new CuiPanel
            {
                FadeOut = 1f,
                Image = { FadeIn = 1f, Color = "0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-200 -150", OffsetMax = "200 -100" },
                CursorEnabled = false
            }, "Overlay", "Notification");

            container.Add(new CuiPanel
            {
                FadeOut = 1f,
                Image = { FadeIn = 1f, Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
            }, "Notification", "Main");

            container.Add(new CuiElement
            {
                FadeOut = 1f,
                Parent = "Main",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) LoadingImages.Call("GetImage", "PanelButtonsImage"), Color = "1 1 1 1", FadeIn = 1f },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-15 -5", OffsetMax = "15 5" }
                }
            }); 

            container.Add(new CuiPanel
            {
                FadeOut = 1f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "55 0" },
                Image = { Color = "0.65 0.65 0.65 1", Sprite = "assets/icons/info.png" }
            }, "Main");

            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Color = "0.9 0.9 0.9 1", Text = string.Format(lang.GetMessage("ITEM BLOCKED", this, player.UserIDString)), FontSize = 22, Align = TextAnchor.UpperCenter, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.08 0", AnchorMax = "1 1" }
            }, "Main");

            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Text = inputText, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.85 0.85 0.85 1" , Font = "robotocondensed-regular.ttf"},
                RectTransform = { AnchorMin = "0.1 0", AnchorMax = "1 0.75" }
            }, "Main");
            
            CuiHelper.AddUi(player, container);

            timer.Once(2f, () => CuiHelper.DestroyUi(player, "Notification"));
        }

        private double IsBlockedCategory(int t) => IsBlocked(settings.SBlock.BlockItems.ElementAt(t).Value.First());
        private bool IsAnyBlocked() => UnBlockTime(settings.SBlock.BlockItems.Last().Key) > CurrentTime();
        private double IsBlocked(string shortname) 
        {
            if (!settings.SBlock.BlockItems.SelectMany(p => p.Value).Contains(shortname))
                return 0;

            var blockTime = settings.SBlock.BlockItems.FirstOrDefault(p => p.Value.Contains(shortname)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            
            return lefTime > 0 ? lefTime : 0;
        }

        private double IsBlockedMLRS()
        {
            var blockTime = settings.SBlock.BlockMLRS;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            
            return lefTime > 0 ? lefTime : 0;
        }

        object CanMountEntity(BasePlayer player, MLRS entity)
        {
          var timeLeft = IsBlockedMLRS();

          if (timeLeft > 0)
          {
            string inputText = string.Format(lang.GetMessage("MLRS BLOCKED", this, player.UserIDString)) + " {timeSpan}".Replace("{timeSpan}", $"{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(timeLeft).TotalHours))} " + string.Format(lang.GetMessage("HOURS", this, player.UserIDString)) + $" {TimeSpan.FromSeconds(timeLeft).Minutes} " + string.Format(lang.GetMessage("MINUTES", this, player.UserIDString)));
            SendReply(player, inputText);
            return false;
          }

          return null;
        }

        private double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount + settings.SBlock.TimeMove;

        private double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);

        private void CheckActiveBlocks()
        {
            if (IsAnyBlocked())
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    OnPlayerConnected(player, false);

                UpdateAction = ServerMgr.Instance.StartCoroutine(UpdateInfoBlock());

                SubscribeHooks(true);
            }
            else
                SubscribeHooks(false);
        }
        
        private void SubscribeHooks(bool subscribe)
        {
            if (subscribe)
            {
                Subscribe(nameof(CanWearItem));
                Subscribe(nameof(CanEquipItem));
                Subscribe(nameof(CanAcceptItem));
                Subscribe(nameof(CanMoveItem));
            }
            else
            {
                Unsubscribe(nameof(CanWearItem));
                Unsubscribe(nameof(CanEquipItem));
                Unsubscribe(nameof(CanAcceptItem));
                Unsubscribe(nameof(CanMoveItem));
            }
        }
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
        
        public static string ToShortString(TimeSpan timeSpan)
        {
            int i = 0;
            string resultText = "";
            if (timeSpan.Days > 0)
            {
                resultText += timeSpan.Days + " День";
                i++;
            }
            if (timeSpan.Hours > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Час";
                i++;
            }
            if (timeSpan.Minutes > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Мин.";
                i++;
            }
            if (timeSpan.Seconds > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Сек.";
                i++;
            }

            return resultText;
        }
        
        private void GetConfig<T>(string menu, string key, ref T varObject)
        {
            if (Config[menu, key] != null)
            {
                varObject = Config.ConvertValue<T>(Config[menu, key]);
            }
            else
            {
                Config[menu, key] = varObject;
            }
        }

        private bool playerOnDuel(BasePlayer player)
        {
            if (Duels != null)
                if (Duels.Call<bool>("inDuel", player))
                    return true;

            if (Duel != null)
            {
                var result = (bool)Duel?.Call("IsPlayerOnActiveDuel", player);
                if (result)
                    return true;
            }
               
            if (Battles != null)
                if (Battles.Call<bool>("IsPlayerOnBattle", player.userID))
                    return true;

            return false;
        }

        private IEnumerator UpdateInfoBlock()
        {
            while (true)
            {
                if (!IsAnyBlocked())
                {
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                        CuiHelper.DestroyUi(player, LayerInfoBlock);

                    SubscribeHooks(false);
                    this.UpdateAction = null;
                    yield break;
                }

                yield return new WaitForSeconds(30);
            }
        }

        private void InitializeLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AVAILABLE"] = "AVAILABLE",    
                ["PAGE"] = "PAGE:",    
                ["ITEM BLOCKED"] = "THE ITEM IS BLOCKED!",   
                ["TIME BLOCKED"] = "The item is temporarily blocked on",  
                ["MLRS BLOCKED"] = "Wipeblock <color=#81B67A>MLRS</color> will be in effect for another",
				["CANNOT USE"] = "This type of ammunition is temporarily <color=#ff6161>blocked!</color>",
                ["HOURS"] = "hours", 
                ["MINUTES"] = "minutes",                                         
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AVAILABLE"] = "ДОСТУПНО",         
                ["PAGE"] = "СТРАНИЦА:",    
                ["ITEM BLOCKED"] = "ПРЕДМЕТ ЗАБЛОКИРОВАН!",    
                ["TIME BLOCKED"] = "Предмет временно заблокирован на",    
                ["MLRS BLOCKED"] = "Вайпблок <color=#81B67A>MLRS</color> будет действовать еще", 
				["CANNOT USE"] = "Данный тип боеприпасов временно <color=#ff6161>заблокирован!</color>",
                ["HOURS"] = "часов",   
                ["MINUTES"] = "минут",                                 
            }, this, "ru");            
        }
    }
}