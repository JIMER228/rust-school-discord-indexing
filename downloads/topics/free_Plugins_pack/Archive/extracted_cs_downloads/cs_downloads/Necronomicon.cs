using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Necronomicon", "https://discord.gg/dNGbxafuJn", "1.0.8")]
    public class Necronomicon : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary, Notify;

        private static Necronomicon _instance;

        private const string Layer = "UI.Necronomicon";

        private readonly Dictionary<BasePlayer, ResearchTable> _tableByPlayer =
            new Dictionary<BasePlayer, ResearchTable>();

        private readonly Dictionary<ResearchTable, BasePlayer> _playerByTable =
            new Dictionary<ResearchTable, BasePlayer>();

        private readonly Dictionary<BasePlayer, BookData> _bookByPlayer = new Dictionary<BasePlayer, BookData>();

        private enum EconomyType
        {
            Plugin,
            Item
        }

        private readonly Dictionary<int, string> _nameById = new Dictionary<int, string>();

        private int[] _defaultBPs;

        private int[] _allBPs;

        #endregion

        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Book Item Settings")]
            public readonly ItemConf Book = new ItemConf
            {
                DisplayName = "Necromonicon",
                ShortName = "xmas.present.small",
                Skin = 2537078809
            };

            [JsonProperty(PropertyName = "Work with Notify?")]
            public readonly bool UseNotify = true;

            [JsonProperty(PropertyName = "Use auto-wipe?")]
            public readonly bool AutoWipe = true;

            [JsonProperty(PropertyName = "Delete BPs from the player when creating a book?")]
            public readonly bool DeleteBPs = false;

            [JsonProperty(PropertyName = "Cost")] public readonly int Cost = 100;

            [JsonProperty(PropertyName = "Permission")]
            public readonly string Permission = string.Empty;

            [JsonProperty(PropertyName = "Economy")]
            public readonly EconomyConf Economy = new EconomyConf
            {
                Enabled = true,
                Type = EconomyType.Plugin,
                AddHook = "Deposit",
                BalanceHook = "Balance",
                RemoveHook = "Withdraw",
                Plug = "Economics",
                ShortName = "scrap",
                DisplayName = string.Empty,
                Skin = 0
            };

            [JsonProperty(PropertyName = "Active Color")]
            public readonly IColor ActiveColor = new IColor("#74884A", 95);

            [JsonProperty(PropertyName = "Disactive Color")]
            public readonly IColor DisactiveColor = new IColor("#595651", 75);

            [JsonProperty(PropertyName = "Effect (empty - disable)")]
            public readonly string Effect = "assets/prefabs/deployable/research table/effects/research-success.prefab";

            [JsonProperty(PropertyName = "Enroll Button Settings")]
            public readonly InterfacePosition EnrollButton = new InterfacePosition
            {
                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                OffsetMin = "-15 -18",
                OffsetMax = "180 7"
            };
        }

        private class InterfacePosition
        {
            public string AnchorMin;

            public string AnchorMax;

            public string OffsetMin;

            public string OffsetMax;
        }

        private class IColor
        {
            [JsonProperty(PropertyName = "HEX")] public string Hex;

            [JsonProperty(PropertyName = "Opacity (0 - 100)")]
            public readonly float Alpha;

            public string Get()
            {
                if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

                var str = Hex.Trim('#');
                if (str.Length != 6) throw new Exception(Hex);
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
            }

            public IColor(string hex, float alpha)
            {
                Hex = hex;
                Alpha = alpha;
            }
        }

        private class ItemConf
        {
            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            public Item ToItem()
            {
                var item = ItemManager.CreateByName(ShortName, 1, Skin);
                if (item == null)
                {
                    Debug.LogError($"Error creating item with shortName: '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(DisplayName))
                    item.name = DisplayName;

                return item;
            }
        }

        private class EconomyConf
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Type (Plugin/Item)")] [JsonConverter(typeof(StringEnumConverter))]
            public EconomyType Type;

            [JsonProperty(PropertyName = "Plugin name")]
            public string Plug;

            [JsonProperty(PropertyName = "Balance add hook")]
            public string AddHook;

            [JsonProperty(PropertyName = "Balance remove hook")]
            public string RemoveHook;

            [JsonProperty(PropertyName = "Balance show hook")]
            public string BalanceHook;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Display Name (empty - default)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            public double ShowBalance(BasePlayer player)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = _instance?.plugins?.Find(Plug);
                        if (plugin == null) return 0;

                        return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.userID)));
                    }
   //                 case EconomyType.Item:
                    {
 //                       return ItemCount(player.inventory.AllItems(), ShortName, Skin);
                    }
                    default:
                        return 0;
                }
            }

            public void AddBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = _instance?.plugins?.Find(Plug);
                        if (plugin == null) return;

                        switch (Plug)
                        {
                            case "BankSystem":
                            case "ServerRewards":
                                plugin.Call(AddHook, player.userID, (int) amount);
                                break;
                            default:
                                plugin.Call(AddHook, player.userID, amount);
                                break;
                        }

                        break;
                    }
                    case EconomyType.Item:
                    {
                        var am = (int) amount;

                        var item = ToItem(am);
                        if (item == null) return;

                        player.GiveItem(item);
                        break;
                    }
                }
            }

            public bool RemoveBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        if (ShowBalance(player) < amount) return false;

                        var plugin = _instance?.plugins.Find(Plug);
                        if (plugin == null) return false;

                        switch (Plug)
                        {
                            case "BankSystem":
                            case "ServerRewards":
                                plugin.Call(RemoveHook, player.userID, (int) amount);
                                break;
                            default:
                                plugin.Call(RemoveHook, player.userID, amount);
                                break;
                        }

                        return true;
                    }
                    case EconomyType.Item:
                    {
 //                       var playerItems = player.inventory.AllItems();
                        var am = (int) amount;

  //                      if (ItemCount(playerItems, ShortName, Skin) < am) return false;

     //                   Take(playerItems, ShortName, Skin, am);
                        return true;
                    }
                    default:
                        return false;
                }
            }

            private Item ToItem(int amount)
            {
                var item = ItemManager.CreateByName(ShortName, amount, Skin);
                if (item == null)
                {
                    Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

                return item;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region Data

        private PluginData _data;

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Books", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<ulong, BookData> Books = new Dictionary<ulong, BookData>();
        }

        private class BookData
        {
            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<int> Items = new List<int>();

            [JsonConstructor]
            public BookData()
            {
            }

            public BookData(BasePlayer player)
            {
                player.PersistantPlayerInfo.unlockedItems.ForEach(item =>
                {
                    if (!_instance._defaultBPs.Contains(item))
                        Items.Add(item);
                });

                if (_instance._config.DeleteBPs)
                {
                    var persistantPlayerInfo = player.PersistantPlayerInfo;

                    Items.ForEach(itemId => player.PersistantPlayerInfo.unlockedItems.Remove(itemId));

                    player.PersistantPlayerInfo = persistantPlayerInfo;
                    player.SendNetworkUpdateImmediate();
                }
            }
        }

        private void GetBook(BasePlayer player)
        {
            var item = _config.Book.ToItem();
            if (item == null) return;

            _data.Books.Add(item.uid.Value, new BookData(player));

            player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }

        private void GetBook(BasePlayer player, List<int> items)
        {
            var item = _config.Book.ToItem();
            if (item == null) return;

            _data.Books.Add(item.uid.Value, new BookData {Items = items});

            player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;

            LoadData();
        }

        private void OnServerInitialized()
        {
            LoadImages();

            if (!string.IsNullOrEmpty(_config.Permission) && !permission.PermissionExists(_config.Permission))
                permission.RegisterPermission(_config.Permission, this);

            AddCovalenceCommand("necronomicon.give", nameof(CmdGive));

            _defaultBPs = ItemManager.bpList.FindAll(x => x.defaultBlueprint)
                .Select(x => x.targetItem.itemid).ToArray();

            _allBPs = ItemManager.bpList.FindAll(x => x.userCraftable && !x.defaultBlueprint)
                .Select(x => x.targetItem.itemid).ToArray();

            ItemManager.itemList.ForEach(item => _nameById[item.itemid] = item.shortname);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Layer);

            SaveData();

            _instance = null;
        }

        private void OnNewSave(string filename)
        {
            if (!_config.AutoWipe) return;

            if (_data == null)
                LoadData();

            _data.Books.Clear();

            SaveData();

            PrintWarning($"Wipe detected. {Name} wiped!");
        }

        private void OnLootEntity(BasePlayer player, ResearchTable table)
        {
            if (player == null || table == null) return;

            MainUi(player, table);
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null || item.info.shortname != _config.Book.ShortName ||
                item.skin != _config.Book.Skin) return;

            var table = container.entityOwner as ResearchTable;
            if (table == null) return;

            BasePlayer player;
            if (!_playerByTable.TryGetValue(table, out player) || player == null)
                return;

            MainUi(player, table);
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            OnItemAddedToContainer(container, item);
        }

        private void OnLootEntityEnd(BasePlayer player, ResearchTable table)
        {
            if (player == null || table == null) return;

            CuiHelper.DestroyUi(player, Layer);
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || item.info.shortname != _config.Book.ShortName || item.skin != _config.Book.Skin ||
                string.IsNullOrEmpty(action) || player == null)
                return null;

            switch (action)
            {
                case "unwrap":
                {
                    BookData data;
                    if (_data.Books.TryGetValue(item.uid.Value, out data))
                        UnlockBPs(player, data.Items);

                    item.Remove();

                    if (!string.IsNullOrEmpty(_config.Effect))
                        SendEffect(player, _config.Effect);

                    return true;
                }
                case "upgrade_item":
                    return true;
                default:
                    return null;
            }
        }

        #region Split

        private object CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem targetItem)
        {
            if (droppedItem == null || targetItem == null) return null;

            var item = droppedItem.GetItem();
            if (item != null && item.skin == _config.Book.Skin)
                return false;

            item = targetItem.GetItem();
            if (item != null && item.skin == _config.Book.Skin)
                return false;

            return null;
        }

        private object CanStackItem(Item item, Item targetItem)
        {
            if (item == null || targetItem == null) return null;

            return item.info.shortname == targetItem.info.shortname &&
                   (item.skin == _config.Book.Skin || targetItem.skin == _config.Book.Skin) &&
                   item.skin == targetItem.skin
                ? (object) false
                : null;
        }

        #endregion

        #endregion

        #region Commands

        private void CmdGive(IPlayer cov, string command, string[] args)
        {
            if (!cov.IsAdmin) return;

            if (args.Length < 2)
            {
                cov.Reply($"Error syntax! Use: /{command} [name/userId] [all/itemIDs...]");
                return;
            }

            var target = BasePlayer.FindAwakeOrSleeping(args[0]);
            if (target == null)
            {
                cov.Reply($"Player '{args[0]}' not found!");
                return;
            }

            var items = new List<int>();
            if (args[1].ToLower() == "all")
                items.AddRange(_allBPs);
            else
                foreach (var id in args.Skip(1))
                {
                    int itemId;
                    if (int.TryParse(id, out itemId) && ItemManager.FindItemDefinition(itemId)) items.Add(itemId);
                }

            if (items.Count == 0)
            {
                cov.Reply("Items not found!");
                return;
            }

            GetBook(target, items);
        }

        [ConsoleCommand("UI_Necronomicon")]
        private void CmdConsole(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "tryenroll":
                {
                    AcceptUi(player);
                    break;
                }

                case "enroll":
                {
                    if (_config.Economy.Enabled && !_config.Economy.RemoveBalance(player, _config.Cost))
                    {
                        SendNotify(player, NotMoney, 1);
                        return;
                    }

                    GetBook(player);
                    break;
                }

                case "cancel":
                {
                    MainUi(player, _tableByPlayer[player]);
                    break;
                }

                case "info":
                {
                    var page = 0;
                    if (arg.HasArgs(2))
                        int.TryParse(arg.Args[1], out page);

                    ShowBookUi(player, page);
                    break;
                }
            }
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, ResearchTable table)
        {
            if (player == null || table == null) return;

            if (!string.IsNullOrEmpty(_config.Permission) &&
                !permission.UserHasPermission(player.UserIDString, _config.Permission))
                return;

            _tableByPlayer[player] = table;
            _playerByTable[table] = player;

            BookData data = null;

            var book = table.inventory.itemList.Find(x =>
                x.info.shortname == _config.Book.ShortName && x.skin == _config.Book.Skin);
            if (book != null && _data.Books.TryGetValue(book.uid.Value, out data)) _bookByPlayer[player] = data;

            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = _config.EnrollButton.AnchorMin,
                    AnchorMax = _config.EnrollButton.AnchorMax,
                    OffsetMin = _config.EnrollButton.OffsetMin,
                    OffsetMax = _config.EnrollButton.OffsetMax
                },
                Text =
                {
                    Text = Msg(player, EnrollStudies, _config.Cost),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 0.85"
                },
                Button =
                {
                    Color = !_config.Economy.Enabled || _config.Economy.ShowBalance(player) >= _config.Cost
                        ? _config.ActiveColor.Get()
                        : _config.DisactiveColor.Get(),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    Command = "UI_Necronomicon tryenroll"
                }
            }, "Overlay", Layer);

            if (data != null)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0 1",
                        OffsetMin = "-30 0", OffsetMax = "-5 0"
                    },
                    Text =
                    {
                        Text = Msg(player, InfoButton),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Color = "1 1 1 0.85"
                    },
                    Button =
                    {
                        Color = _config.DisactiveColor.Get(),
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Command = "UI_Necronomicon info"
                    }
                }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void AcceptUi(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0.19 0.19 0.18 0.3",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                }
            }, "Overlay", Layer);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-250 0", OffsetMax = "250 30"
                },
                Text =
                {
                    Text = Msg(player, BookTitle),
                    Align = TextAnchor.UpperCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 24,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-250 30", OffsetMax = "250 60"
                },
                Text =
                {
                    Text = Msg(player, SureToBuy),
                    Align = TextAnchor.LowerCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 24,
                    Color = "1 1 1 0.5"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-105 -35", OffsetMax = "-5 -5"
                },
                Text =
                {
                    Text = Msg(player, AcceptTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 0.85"
                },
                Button =
                {
                    Color = _config.ActiveColor.Get(),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    Command = "UI_Necronomicon enroll",
                    Close = Layer
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "5 -35", OffsetMax = "105 -5"
                },
                Text =
                {
                    Text = Msg(player, CancelTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 0.85"
                },
                Button =
                {
                    Color = _config.DisactiveColor.Get(),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    Command = "UI_Necronomicon cancel",
                    Close = Layer
                }
            }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ShowBookUi(BasePlayer player, int page = 0)
        {
            BookData data;
            if (!_bookByPlayer.TryGetValue(player, out data)) return;

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0.19 0.19 0.18 0.3",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                CursorEnabled = true
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button =
                {
                    Color = "0 0 0 0",
                    Close = Layer,
                    Command = "UI_Necronomicon cancel"
                }
            }, Layer);

            #region Title

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-250 -60", OffsetMax = "250 -30"
                },
                Text =
                {
                    Text = Msg(player, InfoTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 24,
                    Color = "1 1 1 1"
                }
            }, Layer);

            #endregion

            #region Items

            var Size = 60f;
            var Margin = 10f;
            var itemsOnString = 16;
            var strings = 8;
            var totalAmount = itemsOnString * strings;

            var constSwitch = -(itemsOnString * Size + (itemsOnString - 1) * Margin) / 2f;
            var xSwitch = constSwitch;

            var ySwitch = -80f;

            var i = 1;
            foreach (var item in data.Items.Skip(page * totalAmount).Take(totalAmount))
            {
                container.Add(new CuiElement
                {
                    Name = Layer + $".Item.{i}",
                    Parent = Layer,
                    Components =
                    {
                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "blueprintbase")},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = $"{xSwitch} {ySwitch - Size}",
                            OffsetMax = $"{xSwitch + Size} {ySwitch}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Item.{i}",
                    Components =
                    {
                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", _nameById[item])},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

                if (i % itemsOnString == 0)
                {
                    xSwitch = constSwitch;
                    ySwitch = ySwitch - Size - Margin;
                }
                else
                {
                    xSwitch += Size + Margin;
                }

                i++;
            }

            #endregion

            #region Pages

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "-20 15",
                    OffsetMax = "20 55"
                },
                Text =
                {
                    Text = $"{page + 1}",
                    Align = TextAnchor.MiddleCenter,
                    FontSize = 24,
                    Font = "robotocondensed-regular.ttf",
                    Color = "1 1 1 0.95"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "-80 15",
                    OffsetMax = "-40 55"
                },
                Text =
                {
                    Text = Msg(player, BackButton),
                    Align = TextAnchor.MiddleCenter,
                    FontSize = 24,
                    Font = "robotocondensed-regular.ttf",
                    Color = "1 1 1 0.95"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = page != 0 ? $"UI_Necronomicon info {page - 1}" : ""
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "40 15",
                    OffsetMax = "80 55"
                },
                Text =
                {
                    Text = Msg(player, NextButton),
                    Align = TextAnchor.MiddleCenter,
                    FontSize = 24,
                    Font = "robotocondensed-regular.ttf",
                    Color = "1 1 1 0.95"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = data.Items.Count > (page + 1) * totalAmount ? $"UI_Necronomicon info {page + 1}" : ""
                }
            }, Layer);

            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private static int ItemCount(Item[] items, string shortname, ulong skin)
        {
            return items.Where(item =>
                    item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
                .Sum(item => item.amount);
        }

        private static void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
        {
            var num1 = 0;
            if (iAmount == 0) return;

            var list = Pool.GetList<Item>();

            foreach (var item in itemList)
            {
                if (item.info.shortname != shortname ||
                    skinId != 0 && item.skin != skinId || item.isBroken) continue;

                var num2 = iAmount - num1;
                if (num2 <= 0) continue;
                if (item.amount > num2)
                {
                    item.MarkDirty();
                    item.amount -= num2;
                    num1 += num2;
                    break;
                }

                if (item.amount <= num2)
                {
                    num1 += item.amount;
                    list.Add(item);
                }

                if (num1 == iAmount)
                    break;
            }

            foreach (var obj in list)
                obj.RemoveFromContainer();

            Pool.FreeList(ref list);
        }

        private static void SendEffect(BasePlayer player, string effect)
        {
            EffectNetwork.Send(new Effect(effect, player, 0, new Vector3(), new Vector3()), player.Connection);
        }

        private static void UnlockBPs(BasePlayer player, List<int> items)
        {
            var persistantPlayerInfo = player.PersistantPlayerInfo;

            ItemManager.bpList
                .FindAll(bp =>
                    items.Contains(bp.targetItem.itemid) &&
                    !persistantPlayerInfo.unlockedItems.Contains(bp.targetItem.itemid))
                .ForEach(bp => persistantPlayerInfo.unlockedItems.Add(bp.targetItem.itemid));

            player.PersistantPlayerInfo = persistantPlayerInfo;
            player.SendNetworkUpdateImmediate();
            player.ClientRPCPlayer(null, player, "UnlockedBlueprint", 0);
        }

        private void LoadImages()
        {
            if (!ImageLibrary)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var itemIcons = new List<KeyValuePair<string, ulong>>();

                ItemManager.itemList.ForEach(item => itemIcons.Add(new KeyValuePair<string, ulong>(item.shortname, 0)));

                itemIcons.Add(new KeyValuePair<string, ulong>("blueprintbase", 0));

                if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);
            }
        }

        #endregion

        #region Lang

        private const string
            NotMoney = "NotMoney",
            EnrollStudies = "EnrollStudies",
            BookTitle = "BookTitle",
            SureToBuy = "SureToBuy",
            AcceptTitle = "AcceptTitle",
            CancelTitle = "CancelTitle",
            InfoTitle = "InfoTitle",
            InfoButton = "InfoButton",
            BackButton = "BackButton",
            NextButton = "NextButton";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [NotMoney] = "You don't have enough money!",
                [EnrollStudies] = "Enroll studies for {0}$",
                [BookTitle] = "NECRONOMICON",
                [SureToBuy] = "ARE YOU SURE YOU WANT TO BUY",
                [AcceptTitle] = "Accept",
                [CancelTitle] = "Cancel",
                [InfoTitle] = "BLUEPRINTS IN BOOK",
                [InfoButton] = "i",
                [BackButton] = "<",
                [NextButton] = ">"
            }, this);
        }

        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            SendReply(player, Msg(player, key, obj));
        }

        private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
        {
            if (Notify && _config.UseNotify)
                Notify?.Call("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion
    }
}