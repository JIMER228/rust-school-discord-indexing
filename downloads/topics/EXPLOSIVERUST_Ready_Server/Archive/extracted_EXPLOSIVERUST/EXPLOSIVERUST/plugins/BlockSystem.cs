using System.Linq;
using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("BlockSystem", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    class BlockSystem : RustPlugin
    {
        #region Вар
        [PluginReference] Plugin ImageLibrary, Duel;
        #endregion

        #region Предметы
        Dictionary<int, List<string>> settings = new Dictionary<int, List<string>>() 
        {
            [0] = new List<string>() 
            {
                "crossbow",
                "shotgun.waterpipe",
                "flamethrower",
                "bucket.helmet",
                "riot.helmet"
            },
            [14400] = new List<string>() 
            {
                "pistol.revolver",
                "pistol.python",
                "pistol.semiauto",
                "shotgun.double",
                "coffeecan.helmet",
                "roadsign.jacket",
                "roadsign.kilt"
            },
            [21600] = new List<string>() 
            {
                "rifle.semiauto",
                "pistol.m92",
                "shotgun.pump",
                "shotgun.spas12"
            },
            [36000] = new List<string>() 
            {
                "smg.2",
                "smg.thompson",
                "smg.mp5",
                "rifle.m39",
                "metal.facemask",
                "metal.plate.torso"
            },
            [64800] = new List<string>() 
            {
                "rifle.bolt",
                "grenade.f1",
                "heavy.plate.helmet",
                "heavy.plate.jacket",
                "heavy.plate.pants"
            },
            [86400] = new List<string>() 
            {
                "rifle.ak",
                "rifle.lr300",
                "rifle.l96",
                "grenade.beancan",
                "explosive.satchel",
                "ammo.rifle.explosive",
                "ammo.grenadelauncher.he"
            },
            [100800] = new List<string>() 
            {
                "lmg.m249",
                "rocket.launcher",
                "explosive.timed"
            },
        };
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            foreach (var check in settings.SelectMany(p => p.Value))
                ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{check}.png", check);
        }

        object CanWearItem(PlayerInventory inventory, Item item) {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (playerOnDuel(player)) return null;
            
            if (isBlocked == false) {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                player.SendConsoleCommand($"note.inv 605467368 -1 \"<size=10>Предмет заблокирован</size>\"");
            }

            return isBlocked;
        }

        object CanEquipItem(PlayerInventory inventory, Item item) 
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null) return null;
            if (playerOnDuel(player)) return null;
            
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (isBlocked == false) {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                player.SendConsoleCommand($"note.inv 605467368 -1 \"<size=10>Предмет заблокирован</size>\"");
            }

            return isBlocked;
        }

        object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer) 
        {
            if (inventory == null || item == null) return null;

            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null) return null;
            if (playerOnDuel(player)) return null;

            ItemContainer container = inventory.FindContainer(targetContainer);
            if (container == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret) {
                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false) {
                    player.SendConsoleCommand($"note.inv 605467368 -1 \"<size=10>Предмет заблокирован</size>\"");
                    return true;
                }
            }

            return null;
        }

        object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer) return null;
            if (playerOnDuel(player)) return null;
            
            if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
            if (isBlocked == false) {
                List<Item> list = player.inventory.FindItemIDs(projectile.primaryMagazine.ammoType.itemid).ToList<Item>();
                if (list.Count == 0) {
                    List<Item> list2 = new List<Item>();
                    player.inventory.FindAmmo(list2, projectile.primaryMagazine.definition.ammoTypes);
                    if (list2.Count > 0)
                        isBlocked = IsBlocked(list2[0].info) > 0 ? false : (bool?) null;
                }

                if (isBlocked == false) 
                    player.SendConsoleCommand($"note.inv 605467368 -1 \"<size=10>Предмет заблокирован</size>\"");

                return isBlocked;
            }

            return null;
        }

        object OnReloadMagazine(BasePlayer player, BaseProjectile projectile) 
        {
            if (player is NPCPlayer) return null;
            if (playerOnDuel(player)) return null;

            NextTick(() => {
                var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
                if (isBlocked == false) {
                    player.GiveItem(ItemManager.CreateByItemID(projectile.primaryMagazine.ammoType.itemid, projectile.primaryMagazine.contents, 0UL), BaseEntity.GiveItemReason.Generic);
                    projectile.primaryMagazine.contents = 0;
                    projectile.GetItem().LoseCondition(projectile.GetItem().maxCondition);
                    projectile.SendNetworkUpdate();
                    player.SendNetworkUpdate();
                }
            });

            return null;
        }

        object CanAcceptItem(ItemContainer container, Item item) 
        {
            if (container == null || item == null || container.entityOwner == null) return null;

            if (container.entityOwner is AutoTurret) {
                BasePlayer player = item.GetOwnerPlayer();
                if (player == null) return null;
                if (playerOnDuel(player)) return null;

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
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
            if (!settings.SelectMany(p => p.Value).Contains(shortname))
                return 0;
            var blockTime = settings.FirstOrDefault(p => p.Value.Contains(shortname)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            return lefTime > 0 ? lefTime : 0;
        }

        double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount;
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        public static string FormatShortTime(TimeSpan time) 
        {
            string result = string.Empty;
            result = $"{time.Hours.ToString("00")}:";
            result += $"{time.Minutes.ToString("00")}";
            return result;
        }

        bool playerOnDuel(BasePlayer player)
        {
			if (plugins.Find("Duel") && (bool)plugins.Find("Duel").Call("IsPlayerOnActiveDuel",player)) return true;
			if (plugins.Find("OneVSOne") && (bool)plugins.Find("OneVSOne").Call("IsEventPlayer",player)) return true;
			return false;
        }
        #endregion

        #region Интерфейс
        void BlockUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.09 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu", "Block");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.855", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<b><size=25>БЛОКИРОВКА ПРЕДМЕТОВ</size></b>\nЗдесь вы можете узнать когда будет доступен тот, или иной предмет.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Block");

            float width = 0.9f, height = 0.12f, startxBox = 0.02f, startyBox = 0.86f - height, xmin = startxBox, ymin = startyBox;
            for (int z = 0; z < settings.Count(); z++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMin = "2 1", OffsetMax = "-2 -1" },
                    Image = { Color = "0 0 0 0" }
                }, "Block", "Items");
                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.09 1", OffsetMax = "0 0" },
                    Text = { Text = $"{TimeSpan.FromSeconds(settings.ElementAt(z).Key).TotalHours}Ч.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Items");

                float width1 = 0.13f, height1 = 1f, startxBox1 = 0.09f, startyBox1 = 1f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
                var item = settings.ElementAt(z).Value;
                foreach (var check in item)
                {
                    var color = IsBlocked(check) > 0 ? "0.81 0.30 0.30 0.3" : "0.10 0.13 0.19 1";
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = xmin1 + " " + ymin1, AnchorMax = (xmin1 + width1) + " " + (ymin1 + height1 * 1), OffsetMin = "1 0", OffsetMax = "-1 0" },
                        Image = { Color = color }
                    }, "Items", "Settings");
                    xmin1 += width1;

                    container.Add(new CuiElement
                    {
                        Parent = "Settings",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", check), FadeIn = 1f },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "4 4", OffsetMax = "-4 -4" }
                        }
                    });

                    var text = IsBlocked(check) > 0 ? $"{FormatShortTime(TimeSpan.FromSeconds(IsBlocked(check)))}" : "<size=10>\n\n                 ✔</size>";
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = text, Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf" }
                    }, "Settings");
                }
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion 
    }
}