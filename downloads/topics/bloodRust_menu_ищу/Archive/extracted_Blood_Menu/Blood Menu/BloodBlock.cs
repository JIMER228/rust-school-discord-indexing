using System.Linq;
using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("BloodBlock", "[LimePlugin] Chibubrik", "1.0.0")]
    class BloodBlock : RustPlugin
    {
        #region Вар
        [PluginReference] Plugin ImageLibrary;

        Dictionary<ulong, int> PlayerPage = new Dictionary<ulong, int>();
        #endregion

        #region Предметы
        public Configuration config;
        public class Configuration
        {
            [JsonProperty("Список предметов и их кулдаун")] public Dictionary<int, List<string>> items = new Dictionary<int, List<string>>();
            public static Configuration GetNewConfig()
            {
                return new Configuration
                {
                    items = new Dictionary<int, List<string>>()
                    {
                        [7200] = new List<string>() {
                            "shotgun.waterpipe",
                            "pistol.revolver"
                        },
                        [14400] = new List<string>() {
                            "flamethrower",
                            "military flamethrower",
                            "pistol.python",
                            "revolver.hc",
                            "pistol.semiauto",
                            "shotgun.double",
                            "grenade.molotov",
                            "shotgun.m4",
                            "t1_smg"
                        },
                        [21600] = new List<string>() {
                            "shotgun.pump",
                            "shotgun.spas12",
                            "pistol.m92",
                            "pistol.prototype17",
                            "rifle.semiauto",
                            "coffeecan.helmet",
                            "roadsign.jacket",
                            "roadsign.kilt"
                        },
                        [28800] = new List<string>() {
                            "rifle.sks",
                            "smg.2",
                            "smg.thompson",
                            "smg.mp5",
                            "rifle.m39",
                            "metal.facemask",
                            "metal.plate.torso"
                        },
                        [79200] = new List<string>() {
                            "rifle.ak",
                            "rifle.lr300",
                            "rifle.bolt",
                            "rifle.l96",
                            "homingmissile.launcher",
                            "ballista.static",
                            "ballista.mounted",
                            "batteringram",
                            "grenade.f1",
                            "heavy.plate.helmet",
                            "heavy.plate.jacket",
                            "heavy.plate.pants"
                        },
                        [86400] = new List<string>() {
                            "multiplegrenadelauncher",
                            "hmlmg",
                            "lmg.m249",
                            "minigun",
                            "catapult",
                            "grenade.beancan",
                            "surveycharge",
                            "explosive.satchel",
                            "submarine.torpedo.straight",
                            "ammo.grenadelauncher.he",
                            "catapult.ammo.explosive"
                        },
                        [100800] = new List<string>() {
                            "rocket.launcher",
                            "ammo.rifle.explosive",
                            "explosive.timed",
                            "ammo.rocket.hv",
                            "ammo.rocket.fire",
                            "ammo.rocket.basic",
                            "ammo.rocket.mlrs"
                        }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.items == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/VRULSrY.png", "VRULSrY");

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        void OnPlayerConnected(BasePlayer player) => PlayerPage[player.userID] = 1;

        object CanWearItem(PlayerInventory inventory, Item item, int targetSlot)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?)null;

            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                player.SendConsoleCommand($"note.inv 605467368 -1 \"<size=10>Предмет заблокирован</size>\"");
            }

            return isBlocked;
        }

        object CanEquipItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null)
                return null;

            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?)null;
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                player.SendConsoleCommand($"note.inv 605467368 -1 \"<size=10>Предмет заблокирован</size>\"");
            }

            return isBlocked;
        }

        object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId targetContainer, int targetSlot, int amount, ItemMoveModifier itemMoveModifier)
        {
            if (inventory == null || item == null)
                return null;

            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            ItemContainer container = inventory.FindContainer(targetContainer);
            if (container == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret)
            {
                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
                if (isBlocked == false)
                {
                    player.SendConsoleCommand($"note.inv 605467368 -1 \"<size=10>Предмет заблокирован</size>\"");
                    return true;
                }
            }

            return null;
        }

        object OnWeaponReload(BaseProjectile weapon, BasePlayer player)
        {
            if (player is NPCPlayer)
                return null;

            if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            var isBlocked = IsBlocked(weapon.primaryMagazine.ammoType) > 0 ? false : (bool?)null;
            if (isBlocked == false)
            {
                List<Item> list = player.inventory.FindItemsByItemID(weapon.primaryMagazine.ammoType.itemid).ToList<Item>();
                if (list.Count == 0)
                {
                    List<Item> list2 = new List<Item>();
                    player.inventory.FindAmmo(list2, weapon.primaryMagazine.definition.ammoTypes);
                    if (list2.Count > 0)
                        isBlocked = IsBlocked(list2[0].info) > 0 ? false : (bool?)null;
                }

                if (isBlocked == false)
                    player.SendConsoleCommand($"note.inv 605467368 -1 \"<size=10>Предмет заблокирован</size>\"");

                return isBlocked;
            }

            return null;
        }

        object OnMagazineReload(BaseProjectile weapon, IAmmoContainer desiredAmount, BasePlayer player)
        {
            if (player is NPCPlayer)
                return null;

            NextTick(() =>
            {
                var isBlocked = IsBlocked(weapon.primaryMagazine.ammoType) > 0 ? false : (bool?)null;
                if (isBlocked == false)
                {
                    player.GiveItem(ItemManager.CreateByItemID(weapon.primaryMagazine.ammoType.itemid, weapon.primaryMagazine.contents, 0UL), BaseEntity.GiveItemReason.Generic);
                    weapon.primaryMagazine.contents = 0;
                    weapon.GetItem().LoseCondition(weapon.GetItem().maxCondition);
                    weapon.SendNetworkUpdate();
                    player.SendNetworkUpdate();
                }
            });

            return null;
        }

        object OnMlrsFire(MLRS mlrs, BasePlayer player)
        {
            Puts($"{mlrs.GetItem()}");
            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
		{
			if (container == null || item == null || container.entityOwner == null)
				return null;

			if (container.entityOwner is AutoTurret || !(container.entityOwner is MLRS))
			{
				BasePlayer player = item.GetOwnerPlayer();
                if (player == null)
                    return null;

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
                if (isBlocked == false)
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
			}

			return null;
		}
        #endregion

        #region Методы
        double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);
        double IsBlocked(string shortname)
        {
            if (!config.items.SelectMany(p => p.Value).Contains(shortname))
                return 0;
            var blockTime = config.items.FirstOrDefault(p => p.Value.Contains(shortname)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            return lefTime > 0 ? lefTime : 0;
        }

        double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount;
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        public static string FormatShortTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result = $"{time.Days.ToString("0")}d ";
            if (time.Hours != 0)
                result += $"{time.Hours.ToString("0")}h ";
            if (time.Minutes != 0)
                result += $"{time.Minutes.ToString("0")}m";
            return result;
        }
        #endregion

        #region Команда
        [ConsoleCommand("block")]
        void ConsoleBlock(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "refresh")
                {
                    PlayerPage[player.userID] = 1;
                    BlockUI(player);
                }
                if (args.Args[0] == "page")
                {
                    var page = int.Parse(args.Args[1]);
                    if (PlayerPage[player.userID] == page) return;
                    PlayerPage[player.userID] = page;
                    BlockUI(player, PlayerPage[player.userID]);
                }
            }
        }
        #endregion

        #region Интерфейс
        void BlockUI(BasePlayer player, int page = 1)
        {
            CuiHelper.DestroyUi(player, "Block_UI");
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu_Block", "Block_UI");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = HexToCuiColor("#d1b283", 25) }
            }, "Block_UI", ".Text");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"<b>Внимание!</b> Эти предметы временно заблокированы после вайпа.", Color = HexToCuiColor("#d1b283", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, ".Text");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Block_UI", "BlockItem");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.65 0", AnchorMax = $"1 0.1", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.04", Command = "block refresh", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "Обновить раздел", Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#cec5bb", 40) }
            }, "BlockItem");

            float width = 0.1358f, height = 0.192f, startxBox = 0f, startyBox = 0.905f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in config.items.SelectMany(p => p.Value).Skip((page - 1) * 28).Take(28))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.1" }
                }, "BlockItem", ".Items");

                var colorImage = IsBlocked(check) > 0 ? "1 1 1 0.55" : "1 1 1 0.8";
                container.Add(new CuiElement
                {
                    Parent = ".Items",
                    Components =
                    {
                        new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(check).itemid, SkinId = 0, Color = colorImage, FadeIn = 1f },
                        new CuiRectTransformComponent { AnchorMin = "0 0.2", AnchorMax = "1 1", OffsetMin = "12 8", OffsetMax = "-12 -8" }
                    }
                });
                xmin += width + 0.008f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.008f;
                }

                var colorBox = IsBlocked(check) > 0 ? "0 0 0 0" : HexToCuiColor("#bbc47e", 15);
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = colorBox, Sprite = "assets/content/ui/ui.background.transparent.linear.psd" }
                }, ".Items");

                ;
                var text = IsBlocked(check) > 0 ? $"{FormatShortTime(TimeSpan.FromSeconds(IsBlocked(check)))}" : $"{ItemManager.FindDefinitionByPartialName(check).displayName.english}";
                var textColor = IsBlocked(check) > 0 ? "1 1 1 0.2" : HexToCuiColor("#bbc47e", 100);
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.2", OffsetMax = "0 0" },
                    Text = { Text = text, Color = textColor, Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                }, ".Items");

                if (IsBlocked(check) > 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = ".Items",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "VRULSrY"), Color = "1 1 1 0.2", FadeIn = 1f },
                            new CuiRectTransformComponent { AnchorMin = "0.05 0.815", AnchorMax = "0.178 0.949", OffsetMax = "0 0" }
                        }
                    });
                }
            }
            CuiHelper.AddUi(player, container);
            PagerUI(player, page);
        }

        void PagerUI(BasePlayer player, int currentPage)
        {
            CuiHelper.DestroyUi(player, "footer");
            var container = new CuiElementContainer();
            List<string> displayedPages = GetDisplayedPages(player, currentPage);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.64 0.098" },
                Image = { Color = "0 0 0 0" }
            }, "Block_UI", "footer");

            float width = 0.13f, height = 1f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (var z = 0; z < 7; z++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.01", Material = "assets/content/ui/uibackgroundblur.mat" },
                }, "footer");
                xmin += width + 0.0148f;
            }

            float x = 0f;
            for (int i = 0; i < displayedPages.Count; i++)
            {
                string page = displayedPages[i];

                if (page == "..")
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = $"{x} 0", AnchorMax = $"{x + 0.13f} 1", OffsetMax = "0 0" },
                        Text = { Text = $"..", Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf" }
                    }, "footer");
                }
                else
                {
                    int pageNum = int.Parse(page);
                    string buttonColor = pageNum == currentPage ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                    string text = pageNum == currentPage ? $"<b><color=#45403b>{page}</color></b>" : $"{page}";

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{x} 0", AnchorMax = $"{x + 0.13f} 1", OffsetMax = "0 0" },
                        Button = { Color = buttonColor, Command = $"block page {page}", Material = "assets/content/ui/uibackgroundblur.mat" },
                        Text = { Text = text, Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf" }
                    }, "footer");
                }

                x += 0.145f;
            }

            CuiHelper.AddUi(player, container);
        }

        private List<string> GetDisplayedPages(BasePlayer player, int currentPage)
        {
            var result = new List<string>();
            var wItems = config.items.SelectMany(p => p.Value).Count();
            int totalPages = (int)Math.Ceiling((decimal)wItems / 28);

            if (totalPages <= 7)
            {
                for (int i = 1; i <= totalPages; i++)
                {
                    result.Add(i.ToString());
                }
                return result;
            }

            result.Add("1");

            if (currentPage <= 4)
            {
                for (int i = 2; i <= 5; i++)
                {
                    result.Add(i.ToString());
                }
                result.Add("..");
                result.Add(totalPages.ToString());
            }
            else if (currentPage >= totalPages - 3)
            {
                result.Add("..");
                for (int i = totalPages - 4; i <= totalPages - 1; i++)
                {
                    result.Add(i.ToString());
                }
                result.Add(totalPages.ToString());
            }
            else
            {
                result.Add("..");
                for (int i = currentPage - 1; i <= currentPage + 1; i++)
                {
                    result.Add(i.ToString());
                }
                result.Add("..");
                result.Add(totalPages.ToString());
            }

            return result;
        }
        #endregion 

        #region Хелпер
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }
        #endregion
    }
}