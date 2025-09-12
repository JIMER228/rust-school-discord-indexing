using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Text;
using Network;
using ConVar;
using Object = System.Object;

namespace Oxide.Plugins
{
    [Info("TPWipeBlock", "ds:alone_sempai / https://topplugin.ru/", "9.0.0")]
    class TPWipeBlock : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary, Duel, ArenaTournament;
        private static ConfigData config;
        private string CONF_IgnorePermission = "block.ignore";
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Начало отрисовки столбцов(требуется для центрирования)")]
            public float StartUI = 0.188f;
            [JsonProperty(PropertyName = "Информация плагина")]
            public string Info = "Ахуенный плагин скилов всем советую, а кто не купит, тот гомосек";
            [JsonProperty(PropertyName = "Блокировка предметов")]
            public Dictionary<int, List<string>> items;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                items = new Dictionary<int, List<string>>
                {
                    [7200] = new List<string>() 
                    {
                       "crossbow",
                       "shotgun.waterpipe",
                       "flamethrower",
                       "bucket.helmet",
                       "pistol.revolver",
                       "riot.helmet"
                    },
                    [14400] = new List<string>() 
                    {
                        "pistol.python",
                        "pistol.semiauto",
                        "shotgun.double",
                        "coffeecan.helmet",
                        "pistol.m92",
                        "roadsign.jacket"
                    },
                    [21600] = new List<string>() 
                    {
                        "rifle.semiauto",
                        "shotgun.pump",
                        "smg.2",
                        "smg.mp5",
                        "smg.thompson",
                        "shotgun.spas12"
                    },
                    [36000] = new List<string>() 
                    {
                        "rifle.m39",
                        "metal.facemask",
                        "rifle.bolt",
                        "grenade.f1",
                        "hmlmg",
                        "metal.plate.torso"
                    },
                    [64800] = new List<string>() 
                    {
                        "heavy.plate.helmet",
                        "heavy.plate.jacket",
                        "heavy.plate.pants",
                        "rifle.ak.ice",
                        "metal.plate.torso.icevest",
                        "metal.facemask.icemask"
                    },
                    [86400] = new List<string>() 
                    {
                        "rifle.ak",
                        "rifle.lr300",
                        "rifle.l96",
                        "grenade.beancan",
                        "explosive.satchel",
                        "ammo.rifle.explosive"
                    },
                    [1008000] = new List<string>() 
                    {
                        "lmg.m249",
                        "rocket.launcher",
                        "explosive.timed",
                        "rifle.ak.diver",
                        "multiplegrenadelauncher",
                        "homingmissile.launcher"
                    },
                }
            };
        }
        

         protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId itemContainer, int num, int num2)
		{
			if (inventory == null || item == null) return null;

			var player = inventory.GetComponent<BasePlayer>();
			if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission)) return null;

			var container = inventory.FindContainer(itemContainer);
			if (container == null || container.entityOwner == null) return null;

			if ((container.entityOwner is AutoTurret || container.entityOwner is MLRS))
			{
				var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    MessBlockUi(player, item.info.shortname);
                    timer.Once(0.8f, () =>
                    {
                        CuiHelper.DestroyUi(player, Layer);
                    });
                    return true;
                }
			}
			
			return null;
		}

        object CanAcceptItem(ItemContainer container, Item item)
		{
			if (container == null || item == null || container.entityOwner == null) return null;

			if (container.entityOwner is AutoTurret || container.entityOwner is MLRS)
			{
				var player = item.GetOwnerPlayer();
                if (player == null) return null;
				if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission)) return null;

				var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false)
				{
					MessBlockUi(player, item.info.shortname);
                    timer.Once(0.8f, () =>
                    {
                        CuiHelper.DestroyUi(player, Layer);
                    });
					return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
				}
			}

			return null;
		}
        
        private object CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (!player.userID.IsSteamId())
            {
                return null;
            }
            if (playerOnDuel(player)) return null;
            
            if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
                return null;
            
            var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;
                
                MessBlockUi(player, item.info.shortname);
                timer.Once(0.8f, () =>
                {
                    CuiHelper.DestroyUi(player, Layer);
                });
            }
            return isBlocked;
        }

        private object CanEquipItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null) return null;
            if (playerOnDuel(player)) return null;
            
            if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
                return null;

            var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;
                MessBlockUi(player, item.info.shortname);
                timer.Once(3.8f, () =>
                {
                    CuiHelper.DestroyUi(player, Layer);
                });
            }
            return isBlocked;
        }

        object OnWeaponReload(BaseProjectile projectile, BasePlayer player)
        {
            if (!player.userID.IsSteamId())
            {
                return null;
            }
            if (playerOnDuel(player)) return null;
            
            if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
                return null;
            
            if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
            if (isBlocked == false) {
                MessBlockUi(player, projectile.primaryMagazine.ammoType.shortname);
                timer.Once(2f, () =>
                {
                    CuiHelper.DestroyUi(player, Layer);
                });

                return isBlocked;
            }

            return null;
        }

        object OnMagazineReload(BaseProjectile projectile, IAmmoContainer desiredAmount, BasePlayer player)
        {
            if (!player.userID.IsSteamId())
            {
                return null;
            }
            if (playerOnDuel(player)) return null;
            
            if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
                return null;

            NextTick(() =>
            {
                var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    projectile.primaryMagazine.contents = 0;
                    projectile.SendNetworkUpdate();
                    player.SendNetworkUpdate();
                    MessBlockUi(player, projectile.primaryMagazine.ammoType.shortname);
                    timer.Once(2f, () =>
                    {
                        CuiHelper.DestroyUi(player, Layer);
                    });
                }
            });

            return null;
        }

        private bool playerOnDuel(BasePlayer player)
        {
            if (plugins.Find("ArenaTournament") && (bool)plugins.Find("ArenaTournament").Call("IsOnTournament", player.userID)) return true;
			if (plugins.Find("Duel") && (bool)plugins.Find("Duel").Call("IsPlayerOnActiveDuel",player)) return true;
			if (plugins.Find("OneVSOne") && (bool)plugins.Find("OneVSOne").Call("IsEventPlayer",player)) return true;
			return false;
        }
        
        private void OnServerInitialized() 
        {
            permission.RegisterPermission(CONF_IgnorePermission, this);
            ImageLibrary.Call("AddImage", "https://i.ibb.co/Z86bfB5/w87B22i.png", "fonblock");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/sVx3xnr/FCXe9ai.png", "backgrounditems");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/K508qnN/7h9jNUc.png", "block_1");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/0KCQM4N/2.png", "block_2");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/TtYqb4T/jYVjrBb.png", "block_3");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/c2wQvBQ/Ky0UEV6.png", "block_4");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/bby1Y47/EVd7ox3.png", "block_5");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/7R3tCDs/PMYIQsW.png", "block_6");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/LnL38hC/VRULSrY.png", "castleimage");
            foreach (var check in config.items.SelectMany(p => p.Value))
                ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{check}.png", check);
        }
         
        private void MessBlockUi(BasePlayer player, string shortname)
         {
             CuiHelper.DestroyUi(player, Layer);
             CuiElementContainer container = new CuiElementContainer();
             container.Add(new CuiPanel
             {
                 Image = {Color = HexToRustFormat("#534E489E")},
                 RectTransform =
                     {AnchorMin = "0.5 0.9", AnchorMax = "0.5 0.9", OffsetMin = "-120 -25", OffsetMax = "120 50"},
                 CursorEnabled = false,
             }, "Overlay", Layer);
             
             container.Add(new CuiElement
             {
                 Parent = Layer,
                 Name = Layer + ".BlockItem",
                 Components =
                 {
                     new CuiImageComponent {Color = "1 1 1 0.1"},
                     new CuiRectTransformComponent { AnchorMin = "0.01586128 0.05839238", AnchorMax = "0.2891653 0.9208925" }
                 }
             });
             
             container.Add(new CuiElement
             {
                 Parent = Layer + ".BlockItem",
                 Components =
                 {
                     new CuiRawImageComponent
                     {
                         Png = (string) ImageLibrary.Call("GetImage", $"{shortname}")
                     },
                     new CuiRectTransformComponent
                         {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 1", OffsetMax = "-5 -1"}
                 }
             });
             
             container.Add(new CuiElement
             {
                 Parent = Layer,
                 Components =
                 {
                     new CuiTextComponent()
                     {
                         Color = "1 1 1 1",
                         Text = "Предмет заблокирован, для получения дополнительной информации пишите /block",
                         FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf"
                     },
                     new CuiRectTransformComponent {AnchorMin = "0.3204 0.0833925", AnchorMax = "0.9802345 0.9458925"},
                 }
             });

             CuiHelper.AddUi(player, container);
         }

        private const string Layer = "lay";
        void BlockUi(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = Layer + ".Main",
                Parent = ".Mains",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "fonblock"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = "Menu_UI", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer + ".Main");

            float width = 0.05f, height = 0.5f, startxBox = config.StartUI, startyBox = 0.71f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in config.items)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMin = "2 1", OffsetMax = "-2 -1" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Main", "Items");
                xmin += width + 0.012f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }

                float width1 = 1f, height1 = 0.156f, startxBox1 = 0f, startyBox1 = 1f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
                foreach (var item in check.Value)
                {
                    var color = IsBlocked(item) > 0 ? "1 1 1 0.2" : "1 1 1 1";
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = xmin1 + " " + ymin1, AnchorMax = (xmin1 + width1) + " " + (ymin1 + height1 * 1), OffsetMax = "0 0" },
                        Image = { Color = "0 0 0 0" }
                    }, "Items", "Settings");
                    xmin1 += width1;
                    if (xmin1 + width1 >= 1)
                    {
                        xmin1 = startxBox1;
                        ymin1 -= height1 + 0.013f;
                    }

                    container.Add(new CuiElement
                    {
                        Parent = "Settings",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "backgrounditems"), FadeIn = 1f },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = "Settings",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", item), Color = color, FadeIn = 1f },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 10", OffsetMax = "-10 -10" }
                        }
                    });
                }
                var block = check.Value.Count() == 6 ? "block_6" : check.Value.Count() == 5 ? "block_5" : check.Value.Count() == 4 ? "block_4" : check.Value.Count() == 3 ? "block_3" : check.Value.Count() == 2 ? "block_2" : "block_1";
                var anchors = check.Value.Count() == 6 ? "0" : check.Value.Count() == 5 ? "0.17" : check.Value.Count() == 4 ? "0.335" : check.Value.Count() == 3 ? "0.505" : check.Value.Count() == 2 ? "0.675" : "0.843";
                if (IsBlocked(check.Value.ElementAt(0)) > 0) {
                    container.Add(new CuiElement
                    {
                        Parent = "Items",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", block), FadeIn = 1f },
                            new CuiRectTransformComponent { AnchorMin = $"0 {anchors}", AnchorMax = $"1 1", OffsetMin = "-6 -6", OffsetMax = "6 6" }
                        }
                    });
                }

                var anchortext = check.Value.Count() == 6 ? "6.1" : check.Value.Count() == 5 ? "5" : check.Value.Count() == 4 ? "4" : check.Value.Count() == 3 ? "2.8" : check.Value.Count() == 2 ? "1.8" : "0.65";
                var text = IsBlocked(check.Value.ElementAt(0)) > 0 ? $"{FormatShortTime(TimeSpan.FromSeconds(IsBlocked(check.Value.ElementAt(0))))}" : "";
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 {anchortext}", OffsetMax = "0 0" },
                    Text = { Text = text, Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-bold.ttf" }
                }, "Settings");

                var offmin = check.Value.Count() == 6 ? 2.95f : check.Value.Count() == 5 ? 2.34f : check.Value.Count() == 4 ? 1.85 : check.Value.Count() == 3 ? 1.25 : check.Value.Count() == 2 ? 0.76 : 0.15;
                if (IsBlocked(check.Value.ElementAt(0)) > 0) {
                    container.Add(new CuiElement
                    {
                        Parent = "Settings",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "castleimage"), Color = "1 1 1 1", FadeIn = 1f },
                            new CuiRectTransformComponent { AnchorMin = $"0.37 {offmin + 0.33}", AnchorMax = $"0.63 {offmin + 0.67}", OffsetMax = $"0 0" }
                        }
                    });
                }
            }

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("blockdesc")]
        void DescUI(ConsoleSystem.Arg args) {
            var player = args.Player();
            CuiHelper.DestroyUi(player, Layer + ".Main" + ".Description");
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = Layer + ".Main" + ".Description",
                Parent = Layer + ".Main",
                Components = {
                    new CuiRectTransformComponent { AnchorMin = $"0.58 0.6", AnchorMax = $"0.8 0.8" },
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.8", AnchorMax = "0.9 1" },
                Text = { Text = $"Описание блокировки", Color = "1 1 1 0.65",FontSize = 14, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, Layer + ".Main" + ".Description");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0", AnchorMax = "1 0.7" },
                Text = { Text = $"{config.Info}", Color = "1 1 1 0.65",FontSize = 12, Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperLeft }
            }, Layer + ".Main" + ".Description");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.9 0.82", AnchorMax = "0.98 0.98" },
                Button = { Close = Layer + ".Main" + ".Description", Color = "1 1 1 0" },
                Text = { Text = "" }
            }, Layer + ".Main" + ".Description");

            CuiHelper.AddUi(player, container);
        }

        private double IsBlocked(string shortName)
        {
            if (!config.items.SelectMany(p => p.Value).Contains(shortName))
                return 0;
            var blockTime = config.items.FirstOrDefault(p => p.Value.Contains(shortName)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            return lefTime > 0 ? lefTime : 0;
        }

        private bool BlockTimeGui(string shortName)
        {
            var blockTime = config.items.FirstOrDefault(p => p.Value.Contains(shortName)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            if (lefTime > 0)
            {
                return true;
            }

            return false;
        }
        private double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount;
        private double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        public static string FormatShortTime(TimeSpan time) 
        {
            string result = string.Empty;
            if (time.Days != 0) {
                result = $"{time.Days.ToString("00")}:";
            }
            result += $"{time.Hours.ToString("00")}:";
            result += $"{time.Minutes.ToString("00")}";
            return result;
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
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
    }
}