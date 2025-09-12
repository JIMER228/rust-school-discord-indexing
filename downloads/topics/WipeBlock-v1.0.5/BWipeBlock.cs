using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Boloto Wipe Block", "https://discord.gg/TrJ7jnS233", "1.0.4")]
    public class BWipeBlock : RustPlugin
    {
        #region [Vars]
        [PluginReference]
        private readonly Plugin ImageLibrary;

        private const string Layer = "WipeBlock.Layer";
        private const string NLayer = "WipeBlock.Notify";
        private const string IgnorePermission = "WipeBlock.ignore";
        private const bool LanguageEn = false;

        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        private static double CurrentTime()
        {
            return DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        }

        private double IsBlocked(ItemDefinition itemDefinition)
        {
            return IsBlocked(itemDefinition.shortname);
        }

        private double UnBlockTime(int amount)
        {
            return SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds
                + amount;
        }

        private readonly Dictionary<string, int> _itemIds = new Dictionary<string, int>();
        private readonly List<BasePlayer> openUI = new List<BasePlayer>();
        #endregion [Vars]

        #region [Config]
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
            {
                UpdateConfigValues();
            }

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                config.PluginVersion = Version;
                if (Version == new VersionNumber(1, 0, 0))
                {
                    //
                }

                PrintWarning("Config checked completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private sealed class PluginConfig
        {
            [JsonProperty(LanguageEn ? "Blocking items" : "Блокировка предметов")]
            public Dictionary<string, int> blockItems;

            [JsonProperty(
                LanguageEn ? "Items that can't be thrown" : "Предметы которые нельзя кидать"
            )]
            public List<string> blockItemsThrown;

            [JsonProperty("Config version")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    blockItems = new Dictionary<string, int>()
                    {
                        ["pistol.revolver"] = 1800,
                        ["shotgun.Double"] = 1800,
                        ["pistol.semiauto"] = 3600,
                        ["pistol.python"] = 3600,
                        ["pistol.m92"] = 3600,
                        ["pistol.prototype17"] = 3600,
                        ["shotgun.pump"] = 3600,
                        ["coffeecan.helmet"] = 3600,
                        ["roadsign.jacket"] = 3600,
                        ["roadsign.kilt"] = 3600,
                        ["smg.2"] = 4200,
                        ["smg.thompson"] = 4200,
                        ["shotgun.spas12"] = 4200,
                        ["rifle.semiauto"] = 4200,
                        ["smg.mp5"] = 5600,
                        ["rifle.m39"] = 5600,
                        ["metal.facemask"] = 5600,
                        ["metal.facemask.icemask"] = 5600,
                        ["metal.plate.torso"] = 5600,
                        ["metal.plate.torso.icevest"] = 5600,
                        ["metal.facemask.hockey"] = 5600,
                        ["rifle.ak"] = 7200,
                        ["rifle.ak.ice"] = 7200,
                        ["rifle.bolt"] = 7200,
                        ["rifle.l96"] = 7200,
                        ["rifle.lr300"] = 7200,
                        ["hmlmg"] = 75600,
                        ["lmg.m249"] = 75600,
                        ["heavy.plate.helmet"] = 75600,
                        ["heavy.plate.jacket"] = 75600,
                        ["heavy.plate.pants"] = 75600,
                        ["grenade.f1"] = 75600,
                        ["grenade.beancan"] = 75600,
                        ["explosive.satchel"] = 84400,
                        ["submarine.torpedo.straight"] = 84400,
                        ["ammo.rocket.mlrs"] = 84400,
                        ["multiplegrenadelauncher"] = 84400,
                        ["explosive.timed"] = 84400,
                        ["rocket.launcher"] = 84400,
                        ["ammo.rifle.explosive"] = 84400,
                        ["ammo.rocket.basic"] = 84400,
                        ["ammo.rocket.fire"] = 84400,
                        ["ammo.rocket.hv"] = 84400,
                    },
                    blockItemsThrown = new List<string>()
                    {
                        "grenade.flashbang.deployed",
                        "grenade.molotov.deployed",
                    },
                    PluginVersion = new VersionNumber(),
                };
            }
        }
        #endregion [Config]

        #region [ImageLibrary]
        private bool HasImage(string imageName, ulong imageId = 0)
        {
            return (bool)ImageLibrary.Call("HasImage", imageName, imageId);
        }

        private bool AddImage(string url, string shortname, ulong skin = 0)
        {
            return (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        }

        private string GetImage(string shortname, ulong skin = 0)
        {
            return (string)ImageLibrary?.Call("GetImage", shortname, skin);
        }
        #endregion [ImageLibrary]

        #region [Oxide-Api]
        private void OnServerInitialized()
        {
            if (ImageLibrary == null)
            {
                PrintError("ImageLibrary plugin is not installed!");
            }

            if (!permission.PermissionExists(IgnorePermission))
            {
                permission.RegisterPermission(IgnorePermission, this);
            }

            AddImage("https://s01.yapfiles.com/files/1568209/vignettefade0.png", $"{Name}.Background");
            AddImage("https://i.postimg.cc/V6rG9J5S/Group-11-1-1.png", "ItemFon");
            AddImage("https://i.postimg.cc/Gt5jZ44x/uHTdwjY.png", $"{Name}.BlockFon");

            cmd.AddChatCommand("block", this, "MainUi");

            CheckPlayers();
        }

        private void Unload()
        {
            foreach (BasePlayer player in openUI)
            {
                if (!player.IsConnected)
                {
                    continue;
                }

                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, NLayer);
            }
        }
        #endregion [Oxide-Api]

        #region [Rust-Api]
        private object CanWearItem(PlayerInventory inventory, Item item)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (!IsValid(player))
            {
                return null;
            }

            bool? isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
            if (isBlocked == false)
            {
                NotifyBlock(player, item.info.shortname);
                return false;
            }

            return null;
        }

        private object CanEquipItem(PlayerInventory inventory, Item item, int targetPos)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (!IsValid(player))
            {
                return null;
            }

            bool? isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
            if (isBlocked == false)
            {
                NotifyBlock(player, item.info.shortname);
                return false;
            }

            return null;
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer)
        {
            if (inventory == null || item == null)
            {
                return null;
            }

            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (!IsValid(player))
            {
                return null;
            }

            ItemContainer container = inventory.FindContainer(new ItemContainerId(targetContainer));
            if (container == null || container.entityOwner == null)
            {
                return null;
            }

            bool? isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
            if (
                container.entityOwner is AutoTurret
                || (
                    container.entityOwner.ShortPrefabName.Contains("helicopter")
                    && isBlocked == false
                )
            )
            {
                NotifyBlock(player, item.info.shortname);
                return true;
            }

            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            if (container == null || item == null || container.entityOwner == null)
            {
                return null;
            }

            if (
                container.entityOwner is AutoTurret
                || container.entityOwner.ShortPrefabName.Contains("helicopter")
            )
            {
                BasePlayer player = item.GetOwnerPlayer();
                if (!IsValid(player))
                {
                    return null;
                }

                bool? isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
                if (isBlocked == false)
                {
                    NotifyBlock(player, item.info.shortname);
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
            }

            return null;
        }

        private object OnWeaponReload(BaseProjectile projectile, BasePlayer player)
        {
            if (!IsValid(player))
            {
                return null;
            }

            bool? isBlocked =
                IsBlocked(projectile.primaryMagazine.ammoType.shortname) > 0 ? false : (bool?)null;
            if (isBlocked == false)
            {
                SendReply(
                    player,
                    LanguageEn
                        ? "You <color=#81B67A>can't</color> use this type of ammunition!"
                        : "Вы <color=#81B67A>не можете</color> использовать этот тип боеприпасов!"
                );
                return false;
            }

            return null;
        }

        private object OnMagazineReload(
            BaseProjectile projectile,
            int desiredAmount,
            BasePlayer player
        )
        {
            if (!IsValid(player))
            {
                return null;
            }

            NextTick(() =>
            {
                bool? isBlocked =
                    IsBlocked(projectile.primaryMagazine.ammoType.shortname) > 0
                        ? false
                        : (bool?)null;
                if (isBlocked == false)
                {
                    player.GiveItem(
                        ItemManager.CreateByItemID(
                            projectile.primaryMagazine.ammoType.itemid,
                            projectile.primaryMagazine.contents
                        )
                    );
                    projectile.primaryMagazine.contents = 0;
                    projectile.GetItem().LoseCondition(projectile.GetItem().maxCondition);
                    projectile.SendNetworkUpdate();
                    player.SendNetworkUpdate();
                }
            });

            return null;
        }

        private object CanMountEntity(BasePlayer player, MLRS entity)
        {
            if (!IsValid(player))
            {
                return null;
            }

            bool? isBlocked = IsBlocked("ammo.rocket.mlrs") > 0 ? false : (bool?)null;
            if (isBlocked == false)
            {
                SendReply(
                    player,
                    LanguageEn
                        ? $"MLRS installation will be available through <color=#eb7d6a>{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked("ammo.rocket.mlrs")).TotalHours))}h{TimeSpan.FromSeconds(IsBlocked("ammo.rocket.mlrs")).Minutes}m</color>"
                        : $"Установка MLRS будет доступна через <color=#eb7d6a>{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked("ammo.rocket.mlrs")).TotalHours))}ч{TimeSpan.FromSeconds(IsBlocked("ammo.rocket.mlrs")).Minutes}м</color>"
                );
                return false;
            }

            return null;
        }

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            OnExplosiveThrown(player, entity);
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!IsValid(player))
            {
                return;
            }

            if (!config.blockItemsThrown.Contains(entity.ShortPrefabName))
            {
                return;
            }

            entity.Kill();
            SendReply(
                player,
                LanguageEn
                    ? "You <color=#81B67A>can't</color> throw this item!"
                    : "Вы <color=#81B67A>не можете</color> кидать этот предмет!"
            );
        }
        #endregion [Rust-Api]

        #region [ConsoleCommand]
        [ConsoleCommand("cmdCloseWipeBlock")]
        private void cmdRemoveUi(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            CuiHelper.DestroyUi(player, Layer);
            openUI.Remove(player);
        }

        [ConsoleCommand("changePage")]
        private void cmdChangePage(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            AvailableItem(player, int.Parse(arg.Args[0]));
        }

        [ConsoleCommand("changeBlockPage")]
        private void cmdChangeBlockPage(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            NotAvailableItem(player, int.Parse(arg.Args[0]));
        }

        [ConsoleCommand("changeButton")]
        private void cmdChnageButton(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (arg.Args[0] == "OpenItem")
            {
                CuiHelper.DestroyUi(player, Layer + ".Main.BlockItem");

                MenuButton(player, arg.Args[0]);
                AvailableItem(player);
            }
            else if (arg.Args[0] == "BlockItem")
            {
                CuiHelper.DestroyUi(player, Layer + ".Main.LayerItem");

                MenuButton(player, arg.Args[0]);
                NotAvailableItem(player);
            }
        }
        #endregion [ConsoleCommand]

        #region [Ui]
        private void MainUi(BasePlayer player)
        {
            #region [Vars]
            if (openUI.Contains(player))
            {
                return;
            }

            if (!openUI.Contains(player))
            {
                openUI.Add(player);
            }

            CuiElementContainer container = new CuiElementContainer();
            #endregion [Vars]

            #region [Parrent]
            container.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image =
                    {
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Color = "0 0 0 0.7",
                    },
                },
                "Overlay",
                Layer
            );

            container.Add(
                new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = GetImage($"{Name}.Background"),
                            Color = "1 1 1 1",
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    },
                }
            );

            container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image =
                    {
                        Color = "0 0 0 0.35",
                        Material = "assets/content/ui/uibackgroundblur.mat",
                        Sprite = "assets/content/ui/ui.background.transparent.radial.psd",
                    },
                },
                Layer
            );
            #endregion [Parrent]

            #region [Main-Ui]
            container.Add(
                new CuiPanel
                {
                    Image = { Color = "0.3773585 0.3755785 0.3755785 0.65" },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-200 -205",
                        OffsetMax = "203 166",
                    },
                    CursorEnabled = true,
                },
                Layer,
                Layer + ".Main"
            );

            container.Add(
                new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-43 -243",
                        OffsetMax = "46 -215",
                    },
                    Text =
                    {
                        Text = LanguageEn ? "CLOSE" : "ЗАКРЫТЬ",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 11,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 0.65",
                    },
                    Button =
                    {
                        Color = "0.3773585 0.3755785 0.3755785 0.65",
                        Command = "cmdCloseWipeBlock",
                    },
                },
                Layer
            );
            #endregion [Main-Ui]

            #region [Text]
            container.Add(
                new CuiLabel
                {
                    Text =
                    {
                        Text = LanguageEn ? "TIME LOCKS" : "ВРЕМЕННЫЕ БЛОКИРОВКИ",
                        Color = "1 1 1 0.85",
                        FontSize = 32,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.UpperCenter,
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-393 166",
                        OffsetMax = "395 245",
                    },
                },
                Layer
            );
            #endregion [Text]

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
            AvailableItem(player);
            MenuButton(player, "OpenItem");
        }

        private void MenuButton(BasePlayer player, string Name)
        {
            #region [Vars]
            CuiElementContainer container = new CuiElementContainer();
            #endregion [Vars]

            #region [Parrent]
            container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5" },
                    Image = { Color = "0 0 0 0" },
                },
                Layer,
                Layer + ".MenuButton"
            );
            #endregion [Parrent]

            #region [Button]
            container.Add(
                new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-107 174",
                        OffsetMax = "-2 199",
                    },
                    Text =
                    {
                        Text = LanguageEn ? "ACCESSIBLE" : "ДОСТУПНЫЕ",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 11,
                        Align = TextAnchor.MiddleCenter,
                        Color = Name == "OpenItem" ? "1 1 1 0.65" : "1 1 1 0.3",
                    },
                    Button =
                    {
                        Color =
                            Name == "OpenItem"
                                ? "0.3773585 0.3755785 0.3755785 0.65"
                                : "0.3773585 0.3755785 0.3755785 0.45",
                        Command = "changeButton OpenItem",
                    },
                },
                Layer + ".MenuButton"
            );

            container.Add(
                new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "10 174",
                        OffsetMax = "122.5 199",
                    },
                    Text =
                    {
                        Text = LanguageEn ? "IN BLOCK" : "В БЛОКИРОВКЕ",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 11,
                        Align = TextAnchor.MiddleCenter,
                        Color = Name == "BlockItem" ? "1 1 1 0.65" : "1 1 1 0.3",
                    },
                    Button =
                    {
                        Color =
                            Name == "BlockItem"
                                ? "0.3773585 0.3755785 0.3755785 0.65"
                                : "0.3773585 0.3755785 0.3755785 0.45",
                        Command = "changeButton BlockItem",
                    },
                },
                Layer + ".MenuButton"
            );
            #endregion [Button]

            CuiHelper.DestroyUi(player, Layer + ".MenuButton");
            CuiHelper.AddUi(player, container);
        }

        private void AvailableItem(BasePlayer player, int page = 0)
        {
            #region [Vars]
            CuiElementContainer container = new CuiElementContainer();

            List<KeyValuePair<string, int>> Items = config
                .blockItems.Where(x => !BlockTimeGui(x.Key))
                .OrderBy(x => x.Value)
                .ToList();
            List<KeyValuePair<string, int>> ItemsBlock = Items.Skip(20 * page).Take(20).ToList();
            #endregion [Vars]

            #region [Main-Ui]
            container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0" },
                },
                Layer + ".Main",
                Layer + ".Main.LayerItem"
            );
            #endregion [Main-Ui]

            #region [Items]
            for (int i = 0, x = 0, y = 0; i < 20; i++)
            {
                if (ItemsBlock.Count - 1 >= i)
                {
                    string Name = ItemManager
                        .FindItemDefinition(ItemsBlock[i].Key)
                        ?.displayName?.english;

                    if (string.IsNullOrEmpty(Name))
                    {
                        Name = "UNKNOWN";
                    }

                    if (Name == "Double Barrel Shotgun")
                    {
                        Name = "Double Barrel";
                    }

                    if (Name == "Semi-Automatic Pistol")
                    {
                        Name = "Semi-Automatic";
                    }

                    if (Name == "Ice Metal Chest Plate")
                    {
                        Name = "Ice Metal Chest";
                    }

                    container.Add(
                        new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{0.036 + (x * 0.187)} {0.735 - (y * 0.226)}",
                                AnchorMax = $"{0.206 + (x * 0.187)} {0.945 - (y * 0.226)}",
                            },
                            Image = { Color = "0 0 0 0.5" },
                        },
                        Layer + ".Main.LayerItem",
                        Layer + ".Main" + ".LayerItem" + $".Item{i}"
                    );

                    container.Add(
                        new CuiElement
                        {
                            Parent = Layer + ".Main" + ".LayerItem" + $".Item{i}",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    ItemId = FindItemID(ItemsBlock[i].Key),
                                    SkinId = 0,
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.15 0.25",
                                    AnchorMax = "0.85 0.9",
                                },
                            },
                        }
                    );

                    container.Add(
                        new CuiElement
                        {
                            Parent = Layer + ".Main" + ".LayerItem" + $".Item{i}",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"{Name}",
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 8,
                                    Align = TextAnchor.MiddleCenter,
                                    Color = "1 1 1 0.85",
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 0.28",
                                },
                            },
                        }
                    );

                    container.Add(
                        new CuiElement
                        {
                            Parent = Layer + ".Main" + ".LayerItem" + $".Item{i}",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = GetImage("ItemFon"),
                                    Color = "1 1 1 1",
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                },
                            },
                        }
                    );
                }
                else
                {
                    container.Add(
                        new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{0.036 + (x * 0.187)} {0.735 - (y * 0.226)}",
                                AnchorMax = $"{0.206 + (x * 0.187)} {0.945 - (y * 0.226)}",
                            },
                            Image = { Color = "0 0 0 0" },
                        },
                        Layer + ".Main.LayerItem",
                        Layer + ".Main" + ".LayerItem" + $".Item{i}"
                    );

                    container.Add(
                        new CuiElement
                        {
                            Parent = Layer + ".Main" + ".LayerItem" + $".Item{i}",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = GetImage($"{Name}.BlockFon"),
                                    Color = "0 0 0 1",
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                },
                            },
                        }
                    );

                    container.Add(
                        new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Image = { Color = "0 0 0 0.5" },
                        },
                        Layer + ".Main" + ".LayerItem" + $".Item{i}"
                    );
                }

                x++;
                if (x == 5)
                {
                    x = 0;
                    y++;
                }
            }
            #endregion [Items]

            #region [Page]
            container.Add(
                new CuiButton
                {
                    Button =
                    {
                        Color = "0.3773585 0.3755785 0.3755785 0.65",
                        Command = Items.Skip(20 * (page + 1)).Any() ? $"changePage {page + 1}" : "",
                    },
                    Text =
                    {
                        Text = ">",
                        FontSize = 18,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = Items.Skip(20 * (page + 1)).Any() ? "1 1 1 0.65" : "1 1 1 0.15",
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "207.5 -13",
                        OffsetMax = "234 13",
                    },
                },
                Layer + ".Main.LayerItem"
            );

            container.Add(
                new CuiButton
                {
                    Button =
                    {
                        Color = "0.3773585 0.3755785 0.3755785 0.65",
                        Command = page >= 1 ? $"changePage {page - 1}" : "",
                    },
                    Text =
                    {
                        Text = "<",
                        FontSize = 18,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = page >= 1 ? "1 1 1 0.65" : "1 1 1 0.15",
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-234 -13",
                        OffsetMax = "-207.5 13",
                    },
                },
                Layer + ".Main.LayerItem"
            );
            #endregion [Page]

            CuiHelper.DestroyUi(player, Layer + ".Main.LayerItem");
            CuiHelper.AddUi(player, container);
        }

        private void NotAvailableItem(BasePlayer player, int page = 0)
        {
            #region [Vars]
            CuiElementContainer container = new CuiElementContainer();

            List<KeyValuePair<string, int>> Items = config
                .blockItems.Where(x => BlockTimeGui(x.Key))
                .OrderBy(x => x.Value)
                .ToList();
            List<KeyValuePair<string, int>> ItemsBlock = Items.Skip(20 * page).Take(20).ToList();
            #endregion [Vars]

            #region [Main-Ui]
            container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0" },
                },
                Layer + ".Main",
                Layer + ".Main.BlockItem"
            );
            #endregion [Main-Ui]

            #region [Items]
            for (int i = 0, x = 0, y = 0; i < 20; i++)
            {
                container.Add(
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = $"{0.036 + (x * 0.187)} {0.735 - (y * 0.226)}",
                            AnchorMax = $"{0.206 + (x * 0.187)} {0.945 - (y * 0.226)}",
                        },
                        Image = { Color = "0 0 0 0" },
                    },
                    Layer + ".Main.BlockItem",
                    Layer + ".Main" + ".BlockItem" + $".Item{i}"
                );

                if (ItemsBlock.Count - 1 >= i)
                {
                    string Name = ItemManager
                        .FindItemDefinition(ItemsBlock[i].Key)
                        ?.displayName?.english;

                    if (string.IsNullOrEmpty(Name))
                    {
                        Name = "UNKNOWN";
                    }

                    if (Name == "Double Barrel Shotgun")
                    {
                        Name = "Double Barrel";
                    }

                    if (Name == "Semi-Automatic Pistol")
                    {
                        Name = "Semi-Automatic";
                    }

                    if (Name == "Ice Metal Chest Plate")
                    {
                        Name = "Ice Metal Chest";
                    }

                    container.Add(
                        new CuiElement
                        {
                            Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    ItemId = FindItemID(ItemsBlock[i].Key),
                                    SkinId = 0,
                                    Color = "1 1 1 0.35",
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.15 0.25",
                                    AnchorMax = "0.85 0.9",
                                },
                            },
                        }
                    );

                    container.Add(
                        new CuiElement
                        {
                            Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"{Name}",
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 8,
                                    Align = TextAnchor.MiddleCenter,
                                    Color = "1 1 1 0.85",
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 0.28",
                                },
                            },
                        }
                    );

                    double time = IsBlocked(ItemsBlock[i].Key);
                    if (time >= 3600)
                    {
                        container.Add(
                            new CuiElement
                            {
                                Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                                Name = Layer + ".Main.BlockItem" + $".Item{i}" + "Time",
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = LanguageEn
                                            ? $"<color=#FFFFFF>{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(time).TotalHours))}h{TimeSpan.FromSeconds(time).Minutes}m</color>"
                                            : $"<color=#FFFFFF>{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(time).TotalHours))}ч{TimeSpan.FromSeconds(time).Minutes}м</color>",
                                        FontSize = 12,
                                        Align = TextAnchor.MiddleCenter,
                                        Font = "robotocondensed-regular.ttf",
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.04 0",
                                        AnchorMax = "1 1",
                                    },
                                },
                            }
                        );
                    }
                    else if (time <= 3600 && time > 60)
                    {
                        container.Add(
                            new CuiElement
                            {
                                Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                                Name = Layer + ".Main.BlockItem" + $".Item{i}" + "Time",
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = LanguageEn
                                            ? $"<color=#FFFFFF>{TimeSpan.FromSeconds(time).Minutes}m</color>"
                                            : $"<color=#FFFFFF>{TimeSpan.FromSeconds(time).Minutes}м</color>",
                                        FontSize = 12,
                                        Align = TextAnchor.MiddleCenter,
                                        Font = "robotocondensed-regular.ttf",
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.04 0",
                                        AnchorMax = "1 1",
                                    },
                                },
                            }
                        );
                    }
                    else if (time <= 60 && time > 0)
                    {
                        container.Add(
                            new CuiElement
                            {
                                Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                                Name = Layer + ".Main.BlockItem" + $".Item{i}" + "Time",
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = LanguageEn
                                            ? $"<color=#FFFFFF>{TimeSpan.FromSeconds(time).Seconds}s</color>"
                                            : $"<color=#FFFFFF>{TimeSpan.FromSeconds(time).Seconds}с</color>",
                                        FontSize = 12,
                                        Align = TextAnchor.MiddleCenter,
                                        Font = "robotocondensed-regular.ttf",
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.04 0",
                                        AnchorMax = "1 1",
                                    },
                                },
                            }
                        );
                    }
                }

                container.Add(
                    new CuiElement
                    {
                        Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage($"{Name}.BlockFon"),
                                Color = "0 0 0 1",
                            },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                        },
                    }
                );

                container.Add(
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Image = { Color = "0 0 0 0.5" },
                    },
                    Layer + ".Main" + ".BlockItem" + $".Item{i}"
                );

                x++;
                if (x == 5)
                {
                    x = 0;
                    y++;
                }
            }
            #endregion [Items]

            #region [Page]
            container.Add(
                new CuiButton
                {
                    Button =
                    {
                        Color = "0.3773585 0.3755785 0.3755785 0.65",
                        Command = Items.Skip(20 * (page + 1)).Any()
                            ? $"changeBlockPage {page + 1}"
                            : "",
                    },
                    Text =
                    {
                        Text = ">",
                        FontSize = 18,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = Items.Skip(20 * (page + 1)).Any() ? "1 1 1 0.65" : "1 1 1 0.15",
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "207.5 -13",
                        OffsetMax = "234 13",
                    },
                },
                Layer + ".Main.BlockItem"
            );

            container.Add(
                new CuiButton
                {
                    Button =
                    {
                        Color = "0.3773585 0.3755785 0.3755785 0.65",
                        Command = page >= 1 ? $"changeBlockPage {page - 1}" : "",
                    },
                    Text =
                    {
                        Text = "<",
                        FontSize = 18,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = page >= 1 ? "1 1 1 0.65" : "1 1 1 0.15",
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-234 -13",
                        OffsetMax = "-207.5 13",
                    },
                },
                Layer + ".Main.BlockItem"
            );
            #endregion [Page]

            CuiHelper.DestroyUi(player, Layer + ".Main.BlockItem");
            CuiHelper.AddUi(player, container);
        }

        private void NotifyBlock(BasePlayer player, string shortname)
        {
            CuiHelper.DestroyUi(player, NLayer);
            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        Image =
                        {
                            Color = "0.5 0.5 0.5 0.25",
                            Material = "assets/icons/greyout.mat",
                        },
                        RectTransform =
                        {
                            AnchorMin = "1 0.5",
                            AnchorMax = "1 0.5",
                            OffsetMin = "-245.182 -155.661",
                            OffsetMax = "-2.618 -102.735",
                        },
                        CursorEnabled = false,
                    },
                    "Overlay",
                    NLayer
                },
                new CuiElement
                {
                    Parent = NLayer,
                    Name = NLayer + ".BlockItem",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.49 0.44 0.38 0.75",
                            Material = "assets/icons/greyout.mat",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.01586128 0.08839238",
                            AnchorMax = "0.1925 0.9208925",
                        },
                    },
                },
                new CuiElement
                {
                    Parent = NLayer + ".BlockItem",
                    Components =
                    {
                        new CuiImageComponent { ItemId = FindItemID(shortname), SkinId = 0 },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    },
                },
                new CuiElement
                {
                    Parent = NLayer,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Color = "1 1 1 0.65",
                            Text = LanguageEn ? "Item locked!" : "Предмет заблокирован!",
                            FontSize = 14,
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-bold.ttf",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.215 0.585",
                            AnchorMax = "1 1",
                        },
                        new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.15 0.15" },
                    },
                },
                new CuiElement
                {
                    Parent = NLayer,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Color = "1 1 1 0.65",
                            Text = LanguageEn
                                ? "Information on blocked items - /block"
                                : "Информация о заблокированных\nпредметах - /block",
                            FontSize = 12,
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-regular.ttf",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.215 0",
                            AnchorMax = "1 0.7",
                        },
                        new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.15 0.15" },
                    },
                },
                {
                    new CuiButton
                    {
                        Button =
                        {
                            Color = "0.9 0 0 0.65",
                            Material = "assets/icons/greyout.mat",
                            Close = NLayer,
                        },
                        RectTransform = { AnchorMin = "0.94 0.725", AnchorMax = "0.995 0.98" },
                    },
                    NLayer,
                    "CloseX"
                },
                new CuiElement
                {
                    Parent = "CloseX",
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Color = "1 1 1 0.65",
                            Text = "✘",
                            FontSize = 12,
                            Font = "robotocondensed-regular.ttf",
                            Align = TextAnchor.MiddleCenter,
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.35 0.35" },
                    },
                },
            };

            timer.Once(15f, () => CuiHelper.DestroyUi(player, NLayer));
            CuiHelper.AddUi(player, container);
        }
        #endregion [Ui]

        #region [Func]
        private double IsBlocked(string shortName)
        {
            int value;
            if (!config.blockItems.TryGetValue(shortName, out value))
            {
                return 0;
            }

            double blockTime = UnBlockTime(value) - CurrentTime();
            return blockTime > 0 ? blockTime : 0;
        }

        private bool BlockTimeGui(string shortName)
        {
            double blockTime = UnBlockTime(config.blockItems[shortName]) - CurrentTime();
            return blockTime > 0;
        }

        private bool IsValid(BasePlayer player)
        {
            return player?.userID.IsSteamId() == true
                && !permission.UserHasPermission(player.UserIDString, IgnorePermission);
        }

        private int FindItemID(string shortName)
        {
            int val;
            if (_itemIds.TryGetValue(shortName, out val))
            {
                return val;
            }

            ItemDefinition definition = ItemManager.FindItemDefinition(shortName);
            if (definition == null)
            {
                return 0;
            }

            val = definition.itemid;
            _itemIds[shortName] = val;
            return val;
        }

        private void CheckBlockedItem(BasePlayer player, Item item)
        {
            if (!IsValid(player))
            {
                return;
            }

            bool? isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
            if (isBlocked == false)
            {
                if (item.MoveToContainer(player.inventory.containerMain))
                {
                    player.Command(
                        "note.inv",
                        item.info.itemid,
                        item.amount,
                        !string.IsNullOrEmpty(item.name) ? item.name : string.Empty,
                        (int)BaseEntity.GiveItemReason.PickedUp
                    );
                }
                else
                {
                    item.Drop(
                        player.inventory.containerMain.dropPosition,
                        player.inventory.containerMain.dropVelocity
                    );
                }
            }
        }

        private void CheckPlayers()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                player
                    .inventory.containerBelt.itemList.ToList()
                    .ForEach(item => CheckBlockedItem(player, item));
                player
                    .inventory.containerWear.itemList.ToList()
                    .ForEach(item => CheckBlockedItem(player, item));
            }
        }
        #endregion [Func]
    }
}
