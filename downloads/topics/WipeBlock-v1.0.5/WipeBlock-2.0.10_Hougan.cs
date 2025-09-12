using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Apex;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("WipeBlock", "Hougan / rostov114", "2.0.10")]
    [Description("Блокировка предметов для вашего сервера!")]
    public class WipeBlockHougan : RustPlugin
    {
        #region Classes
        private class Configuration
        {
            public class Interface
            {
                [JsonProperty("Сдвиг панели по вертикале (если некорректно отображается при текущих настройках)")]
                public int Margin = 0;
                [JsonProperty("Текст на первой строке")]
                public string FirstString = "БЛОКИРОВКА ПРЕДМЕТОВ";
                [JsonProperty("Текст на второй строке")]
                public string SecondString = "НАЖМИТЕ ЧТОБЫ УЗНАТЬ БОЛЬШЕ";
                [JsonProperty("Название сервера")]
                public string ServerName = "%CONFIG%";
            }

            public class Block 
            {
                [JsonProperty("Сдвиг блокировки в секундах ('2340' - на 2340 секунд вперёд, '-2340' на 2340 секунд назад)")]
                public int TimeMove = 0;
                [JsonProperty("Настройки блокировки предметов")]
                public Dictionary<int, List<string>> BlockItems;
                [JsonProperty("Названия категорий в интерфейсе")]
                public Dictionary<string, string> CategoriesName;
            }
            
            [JsonProperty("Настройки интерфейса плагина")]
            public Interface SInterface;

            [JsonProperty("Настройки текущей блокировки")]
            public Block SBlock;

            [JsonProperty("Включить поддержку экспериментальных функций")]
            public bool Experimental = true;

            public static Configuration GetDefaultConfiguration()
            {
                var newConfiguration = new Configuration();
                newConfiguration.SInterface = new Interface();
                newConfiguration.SBlock = new Block();
                newConfiguration.SBlock.CategoriesName = new Dictionary<string, string>
                {
                    ["Weapon"] = "ОРУЖИЯ",
                    ["Ammunition"] = "БОЕПРИПАСОВ",
                    ["Medical"] = "МЕДИЦИНЫ",
                    ["Food"] = "ЕДЫ",
                    ["Traps"] = "ЛОВУШЕК",
                    ["Tool"] = "ИНСТРУМЕНТОВ",
                    ["Construction"] = "КОНСТРУКЦИЙ",
                    ["Resources"] = "РЕСУРСОВ",
                    ["Items"] = "ПРЕДМЕТОВ",
                    ["Component"] = "КОМПОНЕНТОВ",
                    ["Misc"] = "ПРОЧЕГО",
                    ["Attire"] = "ОДЕЖДЫ"
                };
                newConfiguration.SBlock.BlockItems = new Dictionary<int,List<string>>
                {
                    [1800] = new List<string>
                    {
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
                        "grenade.beancan",
                        "surveycharge"
                    },
                    [43200] = new List<string>
                    {
                        "rifle.bolt",
                        "rifle.ak",
                        "rifle.lr300",
                        "metal.facemask",
                        "metal.plate.torso",
                        "rifle.l96",
                        "rifle.m39"
                    },
                    [64800] = new List<string>
                    {
                        "ammo.rifle.explosive",
                        "ammo.rocket.basic",
                        "ammo.rocket.fire",
                        "ammo.rocket.hv",
                        "rocket.launcher",
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

        #endregion
        
        #region Variables

        [PluginReference] 
        private Plugin ImageLibrary, Duels, Battles;
        private Configuration settings = null;

        private List<string> Gradients = new List<string> { "518eef","5CAD4F","5DAC4E","5EAB4E","5FAA4E","60A94E","61A84E","62A74E","63A64E","64A54E","65A44E","66A34E","67A24E","68A14E","69A04E","6A9F4E","6B9E4E","6C9D4E","6D9C4E","6E9B4E","6F9A4E","71994E","72984E","73974E","74964E","75954E","76944D","77934D","78924D","79914D","7A904D","7B8F4D","7C8E4D","7D8D4D","7E8C4D","7F8B4D","808A4D","81894D","82884D","83874D","84864D","86854D","87844D","88834D","89824D","8A814D","8B804D","8C7F4D","8D7E4D","8E7D4D","8F7C4D","907B4C","917A4C","92794C","93784C","94774C","95764C","96754C","97744C","98734C","99724C","9B714C","9C704C","9D6F4C","9E6E4C","9F6D4C","A06C4C","A16B4C","A26A4C","A3694C","A4684C","A5674C","A6664C","A7654C","A8644C","A9634C","AA624B","AB614B","AC604B","AD5F4B","AE5E4B","B05D4B","B15C4B","B25B4B","B35A4B","B4594B","B5584B","B6574B","B7564B","B8554B","B9544B","BA534B","BB524B","BC514B","BD504B","BE4F4B","BF4E4B","C04D4B","C14C4B","C24B4B","C44B4B" };
        
        private string Layer = "UI_2340InstanceBlock";
        private string LayerBlock = "UI_2340Block";
        private string LayerInfoBlock = "UI_2340InfoBlock"; 

        private string IgnorePermission = "wipeblock.ignore";

        private Dictionary<ulong, int> UITimer = new Dictionary<ulong, int>();

        private Coroutine UpdateAction;
        #endregion

        #region Initialization
        
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
            if (!ImageLibrary)
            {
                PrintError("ImageLibrary not found, plugin will not work!");
                return;
            }

            foreach (string item in settings.SBlock.BlockItems.SelectMany(p => p.Value))
            {
                if (!ImageLibrary.Call<bool>("HasImage", item))
                {
                    ImageLibrary.Call("AddImage", ImageLibrary.Call<string>("GetImageURL", item), item);
                }
            }

            permission.RegisterPermission(IgnorePermission, this);

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
        #endregion

        #region Hooks
        private object CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
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

        private object CanEquipItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null)
                return null;
            
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

        private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;
            
            if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
            if (isBlocked == false && !playerOnDuel(player))
            {
                List<Item> list = player.inventory.FindItemIDs(projectile.primaryMagazine.ammoType.itemid).ToList<Item>();
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
                    SendReply(player, $"<color=#709EF0>K</color><color=#7598EC>u</color><color=#7A92E8>l</color><color=#808CE4>l</color><color=#8586E0>a</color><color=#8B80DC>n</color><color=#907AD8>m</color><color=#9674D4>a</color><color=#9B6ED0>y</color><color=#A168CD>a</color><color=#A662C9> </color><color=#AC5CC5>ç</color><color=#B156C1>a</color><color=#B751BD>l</color><color=#BC4BB9>ı</color><color=#C245B5>ş</color><color=#C73FB1>t</color><color=#CD39AE>ı</color><color=#D233AA>ğ</color><color=#D82DA6>ı</color><color=#DD27A2>n</color><color=#E3219E> </color><color=#E81B9A>m</color><color=#EE1596>ü</color><color=#F30F92>h</color><color=#F90A8F>i</color><color=#F70C8B>m</color><color=#F50F88>m</color><color=#F41285>a</color><color=#F21582>t</color><color=#F1177E> </color><color=#EF1A7B>y</color><color=#EE1D78>a</color><color=#EC2075>s</color><color=#EB2271>a</color><color=#E9256E>k</color><color=#E8286B>l</color><color=#E62B68>ı</color><color=#E52D64>d</color><color=#E33061>ı</color><color=#E2335E>r</color><color=#E0365B>.</color>");
                }

                return isBlocked;
            }

            return null;
        }
        
        private object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            NextTick(() =>
            {
                var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    player.GiveItem(ItemManager.CreateByItemID(projectile.primaryMagazine.ammoType.itemid, projectile.primaryMagazine.contents, 0UL), BaseEntity.GiveItemReason.Generic);
                    projectile.primaryMagazine.contents = 0;
                    projectile.GetItem().LoseCondition(projectile.GetItem().maxCondition);
                    projectile.SendNetworkUpdate();
                    player.SendNetworkUpdate();

                    PrintError($"[{DateTime.Now.ToShortTimeString()}] {player} пытался взломать систему блокировки!");
                    SendReply(player, $"<color=#B4B673>S</color><color=#B5B475>ü</color><color=#B6B377>r</color><color=#B7B279>e</color><color=#B9B17B>k</color><color=#BAB07D>l</color><color=#BBAF7F>i</color><color=#BCAD81> </color><color=#BEAC83>y</color><color=#BFAB86>a</color><color=#C0AA88>s</color><color=#C1A98A>a</color><color=#C3A88C>k</color><color=#C4A68E>l</color><color=#C5A590>ı</color><color=#C6A492> </color><color=#C8A394>m</color><color=#C9A297>e</color><color=#CAA199>r</color><color=#CB9F9B>m</color><color=#CD9E9D>i</color><color=#CE9D9F> </color><color=#CF9CA1>k</color><color=#D09BA3>o</color><color=#D29AA5>y</color><color=#D399A8>m</color><color=#D497AA>a</color><color=#D696AC>y</color><color=#D795AE>a</color><color=#D894B0> </color><color=#D993B2>u</color><color=#DB92B4>ğ</color><color=#DC90B6>r</color><color=#DD8FB8>a</color><color=#DE8EBB>s</color><color=#E08DBD>t</color><color=#E18CBF>ı</color><color=#E28BC1>ğ</color><color=#E389C3>ı</color><color=#E588C5>n</color><color=#E687C7>ı</color><color=#E786C9>z</color><color=#E885CC> </color><color=#EA84CE>i</color><color=#EB82D0>ç</color><color=#EC81D2>i</color><color=#ED80D4>n</color><color=#EF7FD6> </color><color=#F07ED8>s</color><color=#F17DDA>i</color><color=#F37CDD>l</color><color=#F37ADD>a</color><color=#F378DD>h</color><color=#F376DE>ı</color><color=#F374DE>n</color><color=#F372DF>ı</color><color=#F370DF>z</color><color=#F36EE0> </color><color=#F36CE0>k</color><color=#F46AE1>ı</color><color=#F468E1>r</color><color=#F466E2>ı</color><color=#F464E2>l</color><color=#F463E2>d</color><color=#F461E3>ı</color><color=#F45FE3>!</color>");
                }
            });

            return null;
        }

        private void OnPlayerConnected(BasePlayer player, bool first = true)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player, first));
                return;
            }

            if (settings.Experimental)
            {
                if (first)
                {
                    foreach (string item in settings.SBlock.BlockItems.SelectMany(p => p.Value))
                        SendFilePng(player, item);
                }
            }

            if (!IsAnyBlocked())
                return;

            DrawBlockInfo(player);
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer)
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

            if (container.entityOwner is AutoTurret)
            {
                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    DrawInstanceBlock(player, item);
                    return true;
                }
            }

            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            if (container == null || item == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret)
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
        #endregion

        #region GUI
        private void DrawBlockInfo(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, LayerInfoBlock);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-180 -35", OffsetMax = "-10 -15"},
                Image = { Color = "0 0 0 0" }
            }, "Hud", LayerInfoBlock);

            container.Add(new CuiButton
            {
                RectTransform = {  AnchorMin = "-3 0", AnchorMax = "1 1.5", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "wipeblock.ui.open" },
                Text = { Text = settings.SInterface.FirstString, Font = "robotocondensed-bold.ttf", Color = HexToRustFormat("#FFFFFF5A"), Align = TextAnchor.UpperRight, FontSize = 20 }, 
            }, LayerInfoBlock);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-3 -0.2", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "wipeblock.ui.open" },
                Text = { Text = settings.SInterface.SecondString, Font = "robotocondensed-bold.ttf", Color = HexToRustFormat("#FFFFFF5A"), Align = TextAnchor.LowerRight, FontSize = 12 }, 
            }, LayerInfoBlock);

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("wipeblock.ui.open")]
        private void cmdConsoleDrawBlock(ConsoleSystem.Arg args)
        {
            if (args.Player() == null)
                return;

            DrawBlockGUI(args.Player());
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

        [ChatCommand("blok")]
        private void cmdChatDrawBlock(BasePlayer player)
        {
            DrawBlockGUI(player);
        }

        [ConsoleCommand("wipeblock.ui.close")]
        private void cmdConsoleCloseUI(ConsoleSystem.Arg args)
        {
            if (args.Player() == null)
                return;

            args.Player()?.SetFlag(BaseEntity.Flags.Reserved3, false);
        }

        private void DrawBlockGUI(BasePlayer player)
        {
            if (player.HasFlag(BaseEntity.Flags.Reserved3))
                return;

            if (UITimer.ContainsKey(player.userID))
            {
                if (UITimer[player.userID] == (int)UnityEngine.Time.realtimeSinceStartup)
                    return;
                else
                    UITimer[player.userID] = (int)UnityEngine.Time.realtimeSinceStartup;
            }
            else
            {
                UITimer.Add(player.userID, (int)UnityEngine.Time.realtimeSinceStartup);
            }

            player.SetFlag(BaseEntity.Flags.Reserved3, true); 

            CuiHelper.DestroyUi(player, LayerBlock);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-441.5 {-298 + settings.SInterface.Margin - 20}", OffsetMax = $"441.5 {298 + settings.SInterface.Margin - 20}" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", LayerBlock);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9", Close = LayerBlock, Material = "assets/content/ui/uibackgroundblur.mat", Command = "wipeblock.ui.close"},
                Text = { Text = "" }
            }, LayerBlock);
            
            container.Add(new CuiElement
            {
                Parent = LayerBlock, 
                Name = LayerBlock + ".Header",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.928602340154", AnchorMax = "1.015 0.9998464", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = LayerBlock + ".Header",
                Components =
                {
                    new CuiTextComponent {Text = $"<color=#CD3740>|</color><color=#C94046> </color><color=#C64A4D>Y</color><color=#C35454>A</color><color=#BF5E5B>S</color><color=#BC6762>A</color><color=#B97169>K</color><color=#B57B6F>L</color><color=#B28576>I</color><color=#AF8F7D> </color><color=#AC9884>O</color><color=#A8A28B>L</color><color=#A5AC92>A</color><color=#A2B698>N</color><color=#9EBF9F> </color><color=#9BC9A6>E</color><color=#98D3AD>Ş</color><color=#96D9B3>Y</color><color=#96D7B7>A</color><color=#96D4BC>L</color><color=#96D2C0>A</color><color=#96D0C5>R</color><color=#96CEC9> </color><color=#96CCCE>V</color><color=#96CAD2>E</color><color=#96C8D7> </color><color=#96C5DB>S</color><color=#96C3E0>Ü</color><color=#96C1E4>R</color><color=#96BFE9>E</color><color=#96BDED>L</color><color=#96BBF2>E</color><color=#96B8F6>R</color><color=#96B6FB>İ</color><color=#90B2FC> </color><color=#88ADFC>|</color> {settings.SInterface.ServerName}", FontSize = 30, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                } 
            });

            Dictionary<string, Dictionary<ItemDefinition, string>> blockedItemsGroups = new Dictionary<string, Dictionary<ItemDefinition, string>>();
            FillBlockedItems(blockedItemsGroups);
            var blockedItemsNew = blockedItemsGroups.OrderByDescending(p => p.Value.Count);

            int newString = 0;
            double totalUnblockTime = 0;
            for (int t = 0; t < blockedItemsNew.Count(); t++)
            {
                var blockedCategory = blockedItemsNew.ElementAt(t).Value.OrderBy(p => IsBlocked(p.Value));
                
                container.Add(new CuiElement
                {
                    Parent = LayerBlock,
                    Name = LayerBlock + ".Category",
                    Components =
                    {
                        new CuiImageComponent { Color = "0 0 0 0" },
                        new CuiRectTransformComponent { AnchorMin = $"0 {0.889  - (t) * 0.17 - newString * 0.123}", AnchorMax = $"1.015 {0.925  - (t) * 0.17 - newString * 0.123}", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = LayerBlock + ".Category",
                    Components =
                    {
                        new CuiTextComponent { Color = "1 1 1 1", Text = $" {blockedItemsNew.ElementAt(t).Key}", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });

                for (int i = 0; i < blockedCategory.Count(); i++)
                {
                    if (i == 12)
                    {
                        newString++;
                    }
                    float margin = Mathf.CeilToInt(blockedCategory.Count() - Mathf.CeilToInt((float) (i + 1) / 12) * 12);
                    if (margin < 0)
                    {
                        margin *= -1;
                    }
                    else
                    {
                        margin = 0;
                    }
                    
                    var blockedItem = blockedCategory.ElementAt(i);
                    container.Add(new CuiElement
                    {
                        Parent = LayerBlock,
                        Name = LayerBlock + $".{blockedItem.Key.shortname}",
                        Components =
                        {
                            new CuiImageComponent { FadeIn = 0.5f, Color = HexToRustFormat((blockedItem.Value + "96"))},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0.008608246 + i * 0.0837714 + ((float) margin / 2) * 0.0837714 - (Math.Floor((double) i / 12) * 12 * 0.0837714)}" +
                                            $" {0.7618223 - (t) * 0.17 - newString * 0.12}", 
                                
                                AnchorMax = $"{0.08415613 + i * 0.0837714 + ((float) margin / 2) * 0.0837714 - (Math.Floor((double) i / 12) * 12 * 0.0837714)}" +
                                            $" {0.8736619  - (t) * 0.17 - newString * 0.12}", OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent { Distance = "1 1", Color = "0 0 0 0.1"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerBlock + $".{blockedItem.Key.shortname}",
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 0.5f,  Png = ImageLibrary?.Call<string>("GetImage", blockedItem.Key.shortname) },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"}
                        }
                    });

                    double unblockTime = IsBlocked(blockedItem.Key);
                    totalUnblockTime += unblockTime;

                    string text = unblockTime > 0
                            ? $"<size=11><color=#A8D988>A</color><color=#B9CF96>Z</color><color=#CAC6A4> </color><color=#DBBDB2>K</color><color=#EDB4C0>A</color><color=#D58FCC>L</color><color=#B564D8>D</color><color=#9639E3>I</color></size>\n<size=14>{TimeSpan.FromSeconds(unblockTime).ToShortString()}</size>"
                            : "<size=12><color=#3BAB17>A</color><color=#33B245>K</color><color=#2BBA73>T</color><color=#23C1A0>İ</color><color=#1BC9CF>F</color></size>";

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { FadeIn = 0.5f,Text = "", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        Button = { Color = "0 0 0 0.5"},
                    }, LayerBlock + $".{blockedItem.Key.shortname}", $"Time.{blockedItem.Key.shortname}");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { FadeIn = 0.5f,Text = text, FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter},
                        Button = { Color = "0 0 0 0" },
                    }, $"Time.{blockedItem.Key.shortname}", $"Time.{blockedItem.Key.shortname}.Update");
                }
            }

            CuiHelper.AddUi(player, container);

            if (totalUnblockTime > 0)
                ServerMgr.Instance.StartCoroutine(StartUpdate(player, totalUnblockTime));
        }

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        private void DrawInstanceBlock(BasePlayer player, Item item)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();
            string inputText = "<color=#0fe2f5>[{name}]</color> <color=#F5FE1F>B</color><color=#EDFA23>u</color><color=#E6F727> </color><color=#DEF42B>e</color><color=#D7F12F>ş</color><color=#CFED33>y</color><color=#C8EA37>a</color><color=#C0E73B> </color><color=#B9E440>y</color><color=#B2E044>a</color><color=#AADD48>s</color><color=#A3DA4C>a</color><color=#9BD750>k</color><color=#94D354>l</color><color=#8CD058>ı</color><color=#85CD5D>d</color><color=#7DCA61>ı</color><color=#7BC667>r</color><color=#81C170>.</color><color=#88BD79> </color><color=#8EB882>S</color><color=#95B38C>ü</color><color=#9BAF95>r</color><color=#A2AA9E>e</color><color=#A8A5A7>s</color><color=#AFA1B1>i</color><color=#B59CBA>=</color><color=#f50f1a>{1}</color>".Replace("{name}", item.info.displayName.english).Replace("{1}", $"{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked(item.info)).TotalHours))} <color=#47D310>S</color><color=#2EC386>a</color><color=#15B3FC>a</color><color=#58D9E3>t</color> {TimeSpan.FromSeconds(IsBlocked(item.info)).Minutes} <color=#47D310>d</color><color=#39CA52>a</color><color=#2BC194>k</color><color=#1DB8D6>i</color><color=#25BCF6>k</color><color=#4BD1E8>a</color><color=#70E6DA>.</color>");
            
            container.Add(new CuiPanel
            {
                FadeOut = 1f,
                Image = { FadeIn = 1f, Color = "0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.62 0.95" },
                CursorEnabled = false
            }, "Overlay", Layer);
            
            container.Add(new CuiElement
            {
                FadeOut = 1f,
                Parent = Layer,
                Name = Layer + ".Hide",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Hide",
                Name = Layer + ".Destroy1",
                FadeOut = 1f,
                Components =
                {
                    new CuiImageComponent { Color = "0.4 0.4 0.4 0.7"},
                    new CuiRectTransformComponent { AnchorMin = "0 0.62", AnchorMax = "1.1 0.85" }
                }
                
            });
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Color = "0.9 0.9 0.9 1", Text = "<color=#E9E62F>></color><color=#D1E44A>H</color><color=#B9E366>e</color><color=#A2E182>y</color><color=#8AE09E>y</color><color=#73DFBA> </color><color=#5BDDD6>D</color><color=#44DCF2>o</color><color=#39D9EB>s</color><color=#31D5DF>t</color><color=#28D2D3>u</color><color=#20CEC7>m</color><color=#17CBBB><</color>", FontSize = 22, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, Layer + ".Destroy1", Layer + ".Destroy5");
            container.Add(new CuiButton
            {
                FadeOut = 1f,
                RectTransform = { AnchorMin = "0 0.29", AnchorMax = "1.1 0.61" },
                Button = {FadeIn = 1f, Color = "0.3 0.3 0.3 0.5" },
                Text = { Text = "" }
            }, Layer + ".Hide", Layer + ".Destroy2");
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Text = inputText, FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.85 0.85 0.85 1" , Font = "robotocondensed-regular.ttf"},
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "10 0.9" }
            }, Layer + ".Hide", Layer + ".Destroy3");
            CuiHelper.AddUi(player, container);

            timer.Once(3f, () =>
            {
                CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
            });
        }

        #endregion

        #region Functions
        private string GetGradient(int t)
        {
            var LeftTime = UnBlockTime(t) - CurrentTime();
            return Gradients[Math.Min(99, Math.Max(Convert.ToInt32((float) LeftTime / t * 100), 0))];
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

        private double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount + settings.SBlock.TimeMove;

        private double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);

        private void FillBlockedItems(Dictionary<string, Dictionary<ItemDefinition, string>> fillDictionary)
        {
            foreach (var category in settings.SBlock.BlockItems)
            {
                string categoryColor = GetGradient(category.Key);
                foreach (var item in category.Value)
                {
                    ItemDefinition definition = ItemManager.FindItemDefinition(item);
                    string catName = settings.SBlock.CategoriesName[definition.category.ToString()];
                
                    if (!fillDictionary.ContainsKey(catName))
                        fillDictionary.Add(catName, new Dictionary<ItemDefinition, string>());
                
                    if (!fillDictionary[catName].ContainsKey(definition))
                        fillDictionary[catName].Add(definition, categoryColor);
                }
            }
        }

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
                Subscribe(nameof(OnReloadWeapon));
                Subscribe(nameof(OnReloadMagazine));
                Subscribe(nameof(CanAcceptItem));
                Subscribe(nameof(CanMoveItem));
            }
            else
            {
                Unsubscribe(nameof(CanWearItem));
                Unsubscribe(nameof(CanEquipItem));
                Unsubscribe(nameof(OnReloadWeapon));
                Unsubscribe(nameof(OnReloadMagazine));
                Unsubscribe(nameof(CanAcceptItem));
                Unsubscribe(nameof(CanMoveItem));
            }
        }
        #endregion

        #region Utils
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

            if (Battles != null)
                if (Battles.Call<bool>("IsPlayerOnBattle", player.userID))
                    return true;

            return false;
        }

        private void SendFilePng(BasePlayer player, string imageName, ulong imageId = 0)
        {
            if (!ImageLibrary.Call<bool>("HasImage", imageName, imageId))
                return;

            uint crc = ImageLibrary.Call<uint>("GetImage", imageName, imageId);
            byte[] array = FileStorage.server.Get(crc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

            if (array == null)
                return;

            CommunityEntity.ServerInstance.ClientRPCEx<uint, uint, byte[]>(new Network.SendInfo(player.net.connection)
            {
                channel = 2,
                method = Network.SendMethod.Reliable
            }, null, "CL_ReceiveFilePng", crc, (uint)array.Length, array);
        }
        #endregion

        #region Coroutines
        private IEnumerator StartUpdate(BasePlayer player, double totalUnblockTime)
        {
            while (player.HasFlag(BaseEntity.Flags.Reserved3) && player.IsConnected && totalUnblockTime > 0)
            {
                totalUnblockTime = 0;

                foreach (var check in settings.SBlock.BlockItems.SelectMany(p => p.Value))
                {
                    CuiElementContainer container = new CuiElementContainer();
                    ItemDefinition blockedItem = ItemManager.FindItemDefinition(check);
                    CuiHelper.DestroyUi(player, $"Time.{blockedItem.shortname}.Update");

                    double unblockTime = IsBlocked(blockedItem);
                    totalUnblockTime += unblockTime;

                    string text = unblockTime > 0
                            ? $"<size=12><color=#C2AC15>Y</color><color=#D0A239>A</color><color=#DF985E>S</color><color=#ED8E83>A</color><color=#F18495>K</color><color=#E77A8C>L</color><color=#DD7083>I</color></size>\n<size=14>{TimeSpan.FromSeconds(unblockTime).ToShortString()}</size>"
                            : "<size=14><color=#CDC0FE>A</color><color=#C0CBEE>K</color><color=#B3D6DE>T</color><color=#A6E1CE>İ</color><color=#99ECBE>F</color></size>";

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text          = { Text        = text, FontSize   = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter},
                        Button        = { Color     = "0 0 0 0" }, 
                    }, $"Time.{blockedItem.shortname}", $"Time.{blockedItem.shortname}.Update");

                    CuiHelper.AddUi(player, container);
                }

                yield return new WaitForSeconds(1);
            }

            player.SetFlag(BaseEntity.Flags.Reserved3, false);
            yield break;
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
        #endregion
    }
}