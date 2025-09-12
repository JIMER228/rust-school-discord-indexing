using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

// ReSharper disable MemberHidesStaticFromOuterClass

namespace Oxide.Plugins
{
    [Info("Referrals", "Mevent", "1.3.1")]
    public class Referrals : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary = null, Notify = null, UINotify = null;

        private const string Layer = "UI.Referrals";

        private static Referrals _instance;
        
        private bool _enabledImageLibrary;
        
        #endregion

        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = {"ref", "referal"};

            [JsonProperty(PropertyName = "Commands to activate the promo code",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] ActivePromoCommands =
            {
                "promo",
                "code"
            };

            [JsonProperty(PropertyName = "Permission (example: referrals.use)")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = "Work with Notify?")]
            public bool UseNotify = true;

            [JsonProperty(PropertyName = "Use auto-wipe?")]
            public bool AutoWipe = false;

            [JsonProperty(PropertyName = "Promo Code Chars")]
            public string PromoCodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            [JsonProperty(PropertyName = "Promo Code Length")]
            public int PromoCodeLength = 8;

            [JsonProperty(PropertyName = "Minimum play time (seconds)")]
            public int MinPlayTime = 3600;

            [JsonProperty(PropertyName = "Awards", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Award> Awards = new List<Award>
            {
                new Award
                {
                    InvitesAmount = 1,
                    ID = 1,
                    Type = ItemConf.ItemType.Item,
                    Image = string.Empty,
                    Title = string.Empty,
                    Command = string.Empty,
                    Kit = string.Empty,
                    Plugin = new PluginItem(),
                    DisplayName = string.Empty,
                    ShortName = "wood",
                    Skin = 0,
                    Amount = 20000,
                    ShowDescription = false,
                    Description = new List<string>()
                },
                new Award
                {
                    InvitesAmount = 2,
                    ID = 2,
                    Type = ItemConf.ItemType.Item,
                    Image = string.Empty,
                    Title = string.Empty,
                    Command = string.Empty,
                    Kit = string.Empty,
                    Plugin = new PluginItem(),
                    DisplayName = string.Empty,
                    ShortName = "stones",
                    Skin = 0,
                    Amount = 15000,
                    ShowDescription = false,
                    Description = new List<string>()
                },
                new Award
                {
                    InvitesAmount = 5,
                    ID = 3,
                    Type = ItemConf.ItemType.Item,
                    Image = string.Empty,
                    Title = string.Empty,
                    Command = string.Empty,
                    Kit = string.Empty,
                    Plugin = new PluginItem(),
                    DisplayName = string.Empty,
                    ShortName = "leather",
                    Skin = 0,
                    Amount = 2400,
                    ShowDescription = false,
                    Description = new List<string>()
                },
                new Award
                {
                    InvitesAmount = 7,
                    ID = 4,
                    Type = ItemConf.ItemType.Item,
                    Image = string.Empty,
                    Title = string.Empty,
                    Command = string.Empty,
                    Kit = string.Empty,
                    Plugin = new PluginItem(),
                    DisplayName = string.Empty,
                    ShortName = "cloth",
                    Skin = 0,
                    Amount = 2300,
                    ShowDescription = false,
                    Description = new List<string>()
                },
                new Award
                {
                    InvitesAmount = 10,
                    ID = 5,
                    Type = ItemConf.ItemType.Item,
                    Image = string.Empty,
                    Title = string.Empty,
                    Command = string.Empty,
                    Kit = string.Empty,
                    Plugin = new PluginItem(),
                    DisplayName = string.Empty,
                    ShortName = "lowgradefuel",
                    Skin = 0,
                    Amount = 1500,
                    ShowDescription = false,
                    Description = new List<string>()
                }
            };

            [JsonProperty(PropertyName = "Give an award to a player who activates a promo code?")]
            public bool GiveSelfAward = true;

            [JsonProperty(PropertyName = "Award for the player who activates the promo code",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<SelfAward> SelfAwards = new List<SelfAward>
            {
                new SelfAward
                {
                    Type = ItemConf.ItemType.Item,
                    Image = string.Empty,
                    Title = string.Empty,
                    Command = string.Empty,
                    Kit = string.Empty,
                    Plugin = new PluginItem(),
                    DisplayName = string.Empty,
                    ShortName = "wood",
                    Skin = 0,
                    Amount = 20000,
                    Chance = 50
                },
                new SelfAward
                {
                    Type = ItemConf.ItemType.Item,
                    Image = string.Empty,
                    Title = string.Empty,
                    Command = string.Empty,
                    Kit = string.Empty,
                    Plugin = new PluginItem(),
                    DisplayName = string.Empty,
                    ShortName = "stones",
                    Skin = 0,
                    Amount = 15000,
                    Chance = 50
                },
                new SelfAward
                {
                    Type = ItemConf.ItemType.Item,
                    Image = string.Empty,
                    Title = string.Empty,
                    Command = string.Empty,
                    Kit = string.Empty,
                    Plugin = new PluginItem(),
                    DisplayName = string.Empty,
                    ShortName = "leather",
                    Skin = 0,
                    Amount = 2400,
                    Chance = 50
                }
            };

            [JsonProperty(PropertyName = "Interface")]
            public ColorsInfo Colors = new ColorsInfo
            {
                Color1 = new IColor("#0E0E10"),
                Color2 = new IColor("#161617"),
                Color3 = new IColor("#FFFFFF"),
                Color4 = new IColor("#4B68FF"),
                Color5 = new IColor("#FFFFFF", 5),
                Color6 = new IColor("#FFFFFF", 20),
                Color7 = new IColor("#4B68FF", 33),
                Color8 = new IColor("#74884A")
            };
        }

        private class ColorsInfo
        {
            [JsonProperty(PropertyName = "Color 1")]
            public IColor Color1;

            [JsonProperty(PropertyName = "Color 2")]
            public IColor Color2;

            [JsonProperty(PropertyName = "Color 3")]
            public IColor Color3;

            [JsonProperty(PropertyName = "Color 4")]
            public IColor Color4;

            [JsonProperty(PropertyName = "Color 5")]
            public IColor Color5;

            [JsonProperty(PropertyName = "Color 6")]
            public IColor Color6;

            [JsonProperty(PropertyName = "Color 7")]
            public IColor Color7;

            [JsonProperty(PropertyName = "Color 8")]
            public IColor Color8;
        }

        private class IColor
        {
            [JsonProperty(PropertyName = "HEX")] public string Hex;

            [JsonProperty(PropertyName = "Opacity (0 - 100)")]
            public readonly float Alpha;

            [JsonIgnore] private string _color;

            [JsonIgnore]
            public string Get
            {
                get
                {
                    if (string.IsNullOrEmpty(_color))
                        _color = GetColor();

                    return _color;
                }
            }

            private string GetColor()
            {
                if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

                var str = Hex.Trim('#');
                if (str.Length != 6) throw new Exception(Hex);
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
            }

            public IColor()
            {
            }

            public IColor(string hex, float alpha = 100)
            {
                Hex = hex;
                Alpha = alpha;
            }
        }

        private abstract class ItemConf
        {
            public enum ItemType
            {
                Item,
                Command,
                Plugin,
                Kit
            }

            [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public ItemType Type;

            [JsonProperty(PropertyName = "Image")] public string Image;

            [JsonProperty(PropertyName = "Title")] public string Title;

            [JsonProperty(PropertyName = "Command (%steamid%)")]
            public string Command;

            [JsonProperty(PropertyName = "Kit")] public string Kit;

            [JsonProperty(PropertyName = "Plugin")]
            public PluginItem Plugin;

            [JsonProperty(PropertyName = "Display Name (empty - default)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;

            [JsonIgnore] private string _publicTitle;

            [JsonIgnore]
            public string PublicTitle
            {
                get
                {
                    if (string.IsNullOrEmpty(_publicTitle))
                        _publicTitle = GetName();

                    return _publicTitle;
                }
            }

            [JsonIgnore] private ItemDefinition _def;

            [JsonIgnore]
            public ItemDefinition Definition
            {
                get
                {
                    if (_def == null) _def = ItemManager.FindItemDefinition(ShortName);

                    return _def;
                }
            }

            private string GetName()
            {
                if (!string.IsNullOrEmpty(Title))
                    return Title;

                if (!string.IsNullOrEmpty(DisplayName))
                    return DisplayName;

                var def = Definition;
                if (!string.IsNullOrEmpty(ShortName) && def != null)
                    return def.displayName.translated;

                return string.Empty;
            }

            public void Get(BasePlayer player, int count = 1)
            {
                switch (Type)
                {
                    case ItemType.Item:
                        ToItem(player, count);
                        break;
                    case ItemType.Command:
                        ToCommand(player, count);
                        break;
                    case ItemType.Plugin:
                        Plugin.Get(player, count);
                        break;
                    case ItemType.Kit:
                        ToKit(player, count);
                        break;
                }
            }

            private void ToKit(BasePlayer player, int count)
            {
                if (string.IsNullOrEmpty(Kit)) return;

                for (var i = 0; i < count; i++)
                    Interface.Oxide.CallHook("GiveKit", player, Kit);
            }

            private void ToItem(BasePlayer player, int count)
            {
                var def = Definition;
                if (def == null)
                {
                    Debug.LogError($"Error creating item with ShortName '{ShortName}'");
                    return;
                }

                GetStacks(def, Amount * count)?.ForEach(stack =>
                {
                    var newItem = ItemManager.Create(def, stack, Skin);
                    if (newItem == null)
                    {
                        _instance?.PrintError($"Error creating item with ShortName '{ShortName}'");
                        return;
                    }

                    if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

                    player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
                });
            }

            private void ToCommand(BasePlayer player, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    var command = Command.Replace("\n", "|")
                        .Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace(
                            "%username%",
                            player.displayName, StringComparison.OrdinalIgnoreCase);

                    foreach (var check in command.Split('|')) _instance?.Server.Command(check);
                }
            }

            private static List<int> GetStacks(ItemDefinition item, int amount)
            {
                var list = Pool.GetList<int>();
                var maxStack = item.stackable;

                if (maxStack == 0) maxStack = 1;

                while (amount > maxStack)
                {
                    amount -= maxStack;
                    list.Add(maxStack);
                }

                list.Add(amount);

                return list;
            }
        }

        private class SelfAward : ItemConf
        {
            [JsonProperty(PropertyName = "Chance")]
            public float Chance;
        }

        private class Award : ItemConf
        {
            [JsonProperty(PropertyName = "Invites Amount")]
            public int InvitesAmount;

            [JsonProperty(PropertyName = "ID")] public int ID;

            [JsonProperty(PropertyName = "Show Description")]
            public bool ShowDescription;

            [JsonProperty(PropertyName = "Description", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Description = new List<string>();

            [JsonIgnore] private string _description = string.Empty;

            public string GetDescription()
            {
                if (string.IsNullOrEmpty(_description) && Description != null)
                    _description = string.Join("\n", Description);

                return _description ?? string.Empty;
            }
        }

        private class PluginItem
        {
            [JsonProperty(PropertyName = "Hook")] public string Hook = "Deposit";

            [JsonProperty(PropertyName = "Plugin name")]
            public string Plugin = "Economics";

            [JsonProperty(PropertyName = "Amount")]
            public int Amount = 1;

            [JsonProperty("(GameStores) Store ID in the service")]
            public readonly string ShopID = "UNDEFINED";

            [JsonProperty("(GameStores) Server ID in the service")]
            public readonly string ServerID = "UNDEFINED";

            [JsonProperty("(GameStores) Secret Key")]
            public readonly string SecretKey = "UNDEFINED";

            public void Get(BasePlayer player, int count = 1)
            {
                var plug = _instance?.plugins.Find(Plugin);
                if (plug == null)
                {
                    _instance?.PrintError($"Plugin '{Plugin}' not found !!! ");
                    return;
                }

                switch (Plugin)
                {
                    case "Economics":
                    {
                        plug.Call(Hook, player.userID, (double) Amount * count);
                        break;
                    }
                    case "RustStore":
                    {
                        plug.CallHook(Hook, player.userID, Amount * count, new Action<string>(result =>
                        {
                            if (result == "SUCCESS")
                            {
                                Interface.Oxide.LogDebug(
                                    $"Player {player.displayName} ({player.UserIDString}) received {Amount * count} to the balance in {plug}");
                                return;
                            }

                            Interface.Oxide.LogDebug(
                                $"The balance of the player {player.userID}  has not been changed, error: {result}");
                        }));
                        break;
                    }
                    case "GameStoresRUST":
                    {
                        _instance?.webrequest.Enqueue(
                            $"https://gamestores.ru/api/?shop_id={ShopID}&secret={SecretKey}&server={ServerID}&action=moneys&type=plus&steam_id={player.UserIDString}&amount={Amount * count}",
                            "", (code, response) =>
                            {
                                switch (code)
                                {
                                    case 0:
                                    {
                                        _instance?.PrintError("Api does not responded to a request");
                                        break;
                                    }
                                    case 200:
                                    {
                                        Interface.Oxide.LogDebug(
                                            $"Player {player.displayName} ({player.UserIDString}) received {Amount * count} to the balance in {plug}");
                                        break;
                                    }
                                    case 404:
                                    {
                                        _instance?.PrintError("Please check your configuration! [404]");
                                        break;
                                    }
                                }
                            }, _instance);
                        break;
                    }
                    default:
                    {
                        plug.Call(Hook, player.userID, Amount * count);
                        break;
                    }
                }
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
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Activated")]
            public bool Activated;

            [JsonProperty(PropertyName = "Promo Code")]
            public string PromoCode;

            [JsonProperty(PropertyName = "Play Time")]
            public int PlayTime;

            [JsonProperty(PropertyName = "Received Awards", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<int> ReceivedAwards = new List<int>();

            [JsonProperty(PropertyName = "Invited Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> InvitedPlayers = new List<ulong>();
        }

        private PlayerData GetPlayerData(BasePlayer player)
        {
            return GetPlayerData(player.userID);
        }

        private PlayerData GetPlayerData(ulong member)

        {
            if (!_data.Players.ContainsKey(member))
                _data.Players.Add(member, new PlayerData
                {
                    PromoCode = GetRandomPromoCode()
                });

            return _data.Players[member];
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

            AddCovalenceCommand(_config.Commands, nameof(CmdOpenUi));
            AddCovalenceCommand(_config.ActivePromoCommands, nameof(CmdActivePromo));

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            if (_config.MinPlayTime > 0)
                timer.Every(1, DataHandle);
        }

        private void OnServerSave()
        {
            timer.In(Random.Range(2f, 7f), SaveData);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

            SaveData();

            _instance = null;
        }

        private void OnNewSave(string filename)
        {
            if (!_config.AutoWipe) return;

            if (_data == null)
                LoadData();

            _data?.Players.Clear();
            
            SaveData();

            PrintWarning($"Wipe detected! {Name} wiped!");
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            GetAvatar(player.userID,
                avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));

            var data = GetPlayerData(player);
            if (data == null || string.IsNullOrEmpty(player.displayName)) return;

            data.DisplayName = player.displayName;
        }

        #region Image Library

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "ImageLibrary") _enabledImageLibrary = true;
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "ImageLibrary") _enabledImageLibrary = false;
        }

        #endregion

        #endregion

        #region Commands

        [ConsoleCommand("UI_Referrals")]
        private void CmdConsoleReferrals(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "page":
                {
                    int page;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

                    var zPage = 0;
                    if (arg.HasArgs(3))
                        int.TryParse(arg.Args[2], out zPage);

                    MainUi(player, page, zPage);
                    break;
                }

                case "infoaward":
                {
                    int id;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out id)) return;

                    var item = _config.Awards.Find(x => x.ID == id);
                    if (item == null) return;

                    ShowDescription(player, item);
                    break;
                }

                case "getaward":
                {
                    int id;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out id)) return;

                    var item = _config.Awards.Find(x => x.ID == id);
                    if (item == null) return;

                    var data = GetPlayerData(player);
                    if (data == null || data.ReceivedAwards.Contains(id)) return;

                    data.ReceivedAwards.Add(id);

                    item.Get(player);

                    SendNotify(player, ReceivedItem, 0, item.PublicTitle);

                    MainUi(player);
                    break;
                }
            }
        }

        private void CmdOpenUi(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (_enabledImageLibrary == false)
            {
                SendNotify(player, NoILError, 1);

                BroadcastILNotInstalled();
                return;
            }
            
            if (!string.IsNullOrEmpty(_config.Permission) &&
                !permission.UserHasPermission(player.UserIDString, _config.Permission))
            {
                SendNotify(player, NoPermission, 1);
                return;
            }

            MainUi(player, first: true);
        }

        private void CmdActivePromo(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (!string.IsNullOrEmpty(_config.Permission) &&
                !permission.UserHasPermission(player.UserIDString, _config.Permission))
            {
                SendNotify(player, NoPermission, 1);
                return;
            }

            if (args.Length == 0)
            {
                cov.Reply(Msg(ErrorSyntax, cov.Id, command));
                return;
            }

            var data = GetPlayerData(player);
            if (data == null) return;

            if (data.Activated)
            {
                SendNotify(player, AlreadyActivated, 1);
                return;
            }

            if (_config.MinPlayTime > 0 && data.PlayTime < _config.MinPlayTime)
            {
                SendNotify(player, NotEnoughTime, 1,
                    FormatShortTime(player, TimeSpan.FromSeconds(_config.MinPlayTime - data.PlayTime)));
                return;
            }

            var promoCode = string.Join(" ", args);
            if (string.IsNullOrEmpty(promoCode))
                return;

            if (_data.Players.All(x => x.Value.PromoCode != promoCode))
            {
                SendNotify(player, PromoCode, 1, promoCode);
                return;
            }

            var check = _data.Players.FirstOrDefault(x => x.Value.PromoCode == promoCode);
            if (check.Value.InvitedPlayers.Contains(player.userID)) return;

            if (check.Value.PromoCode == data.PromoCode)
            {
                SendNotify(player, CannotActivateSelfPromo, 1);
                return;
            }

            check.Value.InvitedPlayers.Add(player.userID);
            data.Activated = true;

            var target = BasePlayer.FindByID(check.Key);
            if (target != null)
                SendNotify(target, ActivatedPromoCode, 0, player.displayName);

            SendNotify(player, ActivatedSelfPromoCode, 0, promoCode);

            if (_config.GiveSelfAward)
                GetSelfAward()?.Get(player);

            Interface.CallHook("OnPromoCodeActivated", player, promoCode);
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, int page = 0, int rPage = 0, bool first = false)
        {
            var data = GetPlayerData(player);
            if (data == null) return;

            var container = new CuiElementContainer();

            #region Background

            if (first)
            {
                CuiHelper.DestroyUi(player, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur.mat"
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
                        Command = "UI_Referrals close"
                    }
                }, Layer);
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-360 -250", OffsetMax = "360 300"
                },
                Image =
                {
                    Color = _config.Colors.Color1.Get
                }
            }, Layer, Layer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -45",
                    OffsetMax = "0 0"
                },
                Image = {Color = _config.Colors.Color2.Get}
            }, Layer + ".Main", Layer + ".Header");

            #region Title

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "30 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, TitleMenu),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = _config.Colors.Color3.Get
                }
            }, Layer + ".Header");

            #endregion

            #region Close

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-50 -35",
                    OffsetMax = "-25 -10"
                },
                Text =
                {
                    Text = Msg(player, CloseButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = _config.Colors.Color3.Get
                },
                Button =
                {
                    Close = Layer,
                    Color = _config.Colors.Color4.Get,
                    Command = "UI_Referrals close"
                }
            }, Layer + ".Header");

            #endregion

            #endregion

            #region My PromoCode

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-305 -130",
                    OffsetMax = "-85 -85"
                },
                Image =
                {
                    Color = _config.Colors.Color5.Get
                }
            }, Layer + ".Main", Layer + ".My.PromoCode");

            CreateOutLine(ref container, Layer + ".My.PromoCode", _config.Colors.Color6.Get, 1);

            container.Add(new CuiElement
            {
                Parent = Layer + ".My.PromoCode",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1",
                        Command = $"UI_Referrals page {page} {rPage}",
                        CharsLimit = 150,
                        Text = $"{data.PromoCode}",
                        NeedsKeyboard = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "20 0", OffsetMax = "-20 0"
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-305 -185",
                    OffsetMax = "-45 -145"
                },
                Text =
                {
                    Text = Msg(player, YourPromoDescription),
                    Align = TextAnchor.UpperLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 0.3"
                }
            }, Layer + ".Main");

            #endregion

            #region Enter PromoCode

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "25 -130",
                    OffsetMax = "245 -85"
                },
                Image =
                {
                    Color = _config.Colors.Color5.Get
                }
            }, Layer + ".Main", Layer + ".Enter.PromoCode");

            CreateOutLine(ref container, Layer + ".Enter.PromoCode", _config.Colors.Color6.Get, 1);

            container.Add(new CuiElement
            {
                Parent = Layer + ".Enter.PromoCode",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1",
                        Command = data.Activated ? "" : $"{_config.ActivePromoCommands[0]} ",
                        CharsLimit = 150,
                        NeedsKeyboard = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "20 0", OffsetMax = "-20 0"
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "25 -185",
                    OffsetMax = "285 -145"
                },
                Text =
                {
                    Text = Msg(player, EnterPromoDescription),
                    Align = TextAnchor.UpperLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 0.3"
                }
            }, Layer + ".Main");

            #endregion

            #region Invited Players

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-305 -500",
                    OffsetMax = "-85 -205"
                },
                Image =
                {
                    Color = "0 0 0 0"
                }
            }, Layer + ".Main", Layer + ".InvitedPlayers");

            CreateOutLine(ref container, Layer + ".InvitedPlayers", _config.Colors.Color6.Get, 1);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "20 -30", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, InvitedPlayersTitle),
                    Align = TextAnchor.LowerLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 0.5"
                }
            }, Layer + ".InvitedPlayers");

            #region List

            var amountOnPage = 10;
            var ySwitch = -40f;
            var height = 20f;
            var margin = 5f;

            var invitedPlayers = data.InvitedPlayers.FindAll(CheckInvitedPlayer);
            foreach (var member in invitedPlayers.Skip(page * amountOnPage).Take(amountOnPage))
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = $"20 {ySwitch - height}",
                        OffsetMax = $"0 {ySwitch}"
                    },
                    Image = {Color = "0 0 0 0"}
                }, Layer + ".InvitedPlayers", Layer + $".InvitedPlayers.Player.{member}");

                container.Add(new CuiElement
                {
                    Parent = Layer + $".InvitedPlayers.Player.{member}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary.Call<string>("GetImage", $"avatar_{member}")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "0 0", OffsetMax = "20 20"
                        }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "45 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = $"{GetPlayerData(member)?.DisplayName}",
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".InvitedPlayers.Player.{member}");

                ySwitch = ySwitch - height - margin;
            }

            #endregion

            #region Pages

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 0", AnchorMax = "1 0",
                    OffsetMin = "-40 30",
                    OffsetMax = "-20 50"
                },
                Text =
                {
                    Text = Msg(player, NextBtn),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _config.Colors.Color4.Get,
                    Command = invitedPlayers.Count > (page + 1) * amountOnPage
                        ? $"UI_Referrals page {page + 1}"
                        : ""
                }
            }, Layer + ".InvitedPlayers");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 0", AnchorMax = "1 0",
                    OffsetMin = "-40 55",
                    OffsetMax = "-20 75"
                },
                Text =
                {
                    Text = Msg(player, BackBtn),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _config.Colors.Color7.Get,
                    Command = page != 0 ? $"UI_Referrals page {page - 1}" : ""
                }
            }, Layer + ".InvitedPlayers");

            #endregion

            #endregion

            #region Awards

            amountOnPage = 3;

            ySwitch = -205;
            height = 90;
            margin = 10;

            var awards = GetAwards(player);
            foreach (var award in awards.Skip(rPage * amountOnPage).Take(amountOnPage))
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"25 {ySwitch - height}",
                        OffsetMax = $"305 {ySwitch}"
                    },
                    Image =
                    {
                        Color = "0 0 0 0"
                    }
                }, Layer + ".Main", Layer + $".Award.{award.ID}");

                CreateOutLine(ref container, Layer + $".Award.{award.ID}",
                    data.ReceivedAwards.Contains(award.ID) ? _config.Colors.Color8.Get : _config.Colors.Color6.Get,
                    1);

                if (!string.IsNullOrEmpty(award.Image))
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".Award.{award.ID}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = !string.IsNullOrEmpty(award.Image)
                                    ? ImageLibrary.Call<string>("GetImage", award.Image)
                                    : ImageLibrary.Call<string>("GetImage", award.ShortName, award.Skin)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "15 15", OffsetMax = "75 75"
                            }
                        }
                    });
                else
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".Award.{award.ID}",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                ItemId = award.Definition.itemid,
                                SkinId = award.Skin
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "15 15", OffsetMax = "75 75"
                            }
                        }
                    });

                #region Name

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0.5", AnchorMax = "1 1",
                        OffsetMin = "90 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = Msg(player, ItemNameTitle),
                        Align = TextAnchor.LowerLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 8,
                        Color = "1 1 1 0.5"
                    }
                }, Layer + $".Award.{award.ID}");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0.5",
                        OffsetMin = "90 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = $"{award.PublicTitle}",
                        Align = TextAnchor.UpperLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Award.{award.ID}");

                #endregion

                #region Invited

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0.5", AnchorMax = "1 1",
                        OffsetMin = "175 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = Msg(player, InvitedAmountTitle),
                        Align = TextAnchor.LowerLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 8,
                        Color = "1 1 1 0.5"
                    }
                }, Layer + $".Award.{award.ID}");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0.5",
                        OffsetMin = "175 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = $"{Mathf.Min(invitedPlayers.Count, award.InvitesAmount)}/{award.InvitesAmount}",
                        Align = TextAnchor.UpperLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Award.{award.ID}");

                #endregion

                #region Button

                if (data.ReceivedAwards.Contains(award.ID))
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                            OffsetMin = "-40 -15",
                            OffsetMax = "-10 15"
                        },
                        Text =
                        {
                            Text = Msg(player, ReceivedItemIcon),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = _config.Colors.Color8.Get
                        }
                    }, Layer + $".Award.{award.ID}");
                else if (invitedPlayers.Count >= award.InvitesAmount)
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                            OffsetMin = "-40 -15",
                            OffsetMax = "-10 15"
                        },
                        Text =
                        {
                            Text = Msg(player, ReceiveItemIcon),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = _config.Colors.Color4.Get,
                            Command = $"UI_Referrals getaward {award.ID}"
                        }
                    }, Layer + $".Award.{award.ID}");

                #endregion

                #region Info

                if (award.ShowDescription)
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 1", AnchorMax = "1 1",
                            OffsetMin = "-18 -18", OffsetMax = "-5 -5"
                        },
                        Text = {Text = ""},
                        Button =
                        {
                            Sprite = "assets/icons/warning.png",
                            Color = _config.Colors.Color6.Get,
                            Command = $"UI_Referrals infoaward {award.ID}"
                        }
                    }, Layer + $".Award.{award.ID}");

                #endregion

                ySwitch = ySwitch - height - margin;
            }

            #region Pages

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 0", AnchorMax = "1 0",
                    OffsetMin = "-50 55",
                    OffsetMax = "-30 75"
                },
                Text =
                {
                    Text = Msg(player, NextBtn),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _config.Colors.Color4.Get,
                    Command = awards.Count > (rPage + 1) * amountOnPage
                        ? $"UI_Referrals page {page} {rPage + 1}"
                        : ""
                }
            }, Layer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 0", AnchorMax = "1 0",
                    OffsetMin = "-50 80",
                    OffsetMax = "-30 100"
                },
                Text =
                {
                    Text = Msg(player, BackBtn),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _config.Colors.Color7.Get,
                    Command = rPage != 0 ? $"UI_Referrals page {page} {rPage - 1}" : ""
                }
            }, Layer + ".Main");

            #endregion

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void ShowDescription(BasePlayer player, Award award)
        {
            CuiHelper.DestroyUi(player, Layer + ".Notice");
            CuiHelper.AddUi(player, new CuiElementContainer
            {
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = "-150 -110", OffsetMax = "150 0"
                        },
                        Text =
                        {
                            Text = $"{award.GetDescription()}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        }
                    },
                    Layer + ".Main", Layer + ".Notice"
                }
            });
        }

        #endregion

        #region Utils

        private SelfAward GetSelfAward()
        {
            SelfAward item = null;
            var iteration = 0;
            do
            {
                iteration++;

                var randomItem = _config.SelfAwards.GetRandom();
                if (randomItem.Chance < 1 || randomItem.Chance > 100)
                    continue;

                if (Random.Range(0f, 100f) <= randomItem.Chance)
                    item = randomItem;
            } while (item == null && iteration < 1000);

            return item;
        }

        private static void CreateOutLine(ref CuiElementContainer container, string parent, string color,
            float size = 2)
        {
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{size} 0",
                        OffsetMax = $"-{size} {size}"
                    },
                    Image = {Color = color}
                },
                parent);
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{size} -{size}",
                        OffsetMax = $"-{size} 0"
                    },
                    Image = {Color = color}
                },
                parent);
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0 1",
                        OffsetMin = "0 0",
                        OffsetMax = $"{size} 0"
                    },
                    Image = {Color = color}
                },
                parent);
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"-{size} 0",
                        OffsetMax = "0 0"
                    },
                    Image = {Color = color}
                },
                parent);
        }

        #region Avatar

        private readonly Regex _regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

        private void GetAvatar(ulong userId, Action<string> callback)
        {
            if (callback == null) return;

            webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
            {
                if (code != 200 || response == null)
                    return;

                var avatar = _regex.Match(response).Groups[1].ToString();
                if (string.IsNullOrEmpty(avatar))
                    return;

                callback.Invoke(avatar);
            }, this);
        }

        #endregion

        private List<Award> GetAwards(BasePlayer player)
        {
            var result = new List<Award>();

            var data = GetPlayerData(player);

            var receivedAwards = _config.Awards.FindLast(x => data.ReceivedAwards.Contains(x.ID));
            if (receivedAwards != null)
                result.Add(receivedAwards);

            result.AddRange(_config.Awards.FindAll(x => !data.ReceivedAwards.Contains(x.ID))
                .OrderBy(x => x.InvitesAmount).ThenByDescending(x => data.InvitedPlayers.Count >= x.InvitesAmount));
            return result;
        }

        private string GetRandomPromoCode()
        {
            string promo;
            do
            {
                promo = new string(
                    Enumerable.Repeat(_config.PromoCodeChars, _config.PromoCodeLength)
                        .Select(s => s[Random.Range(0, s.Length)])
                        .ToArray());
            } while (_data.Players.Any(x => x.Value.PromoCode == promo));

            return promo;
        }

        private void DataHandle()
        {
            foreach (var check in _data.Players)
                check.Value.PlayTime++;
        }

        private bool CheckInvitedPlayer(ulong target)
        {
            var data = GetPlayerData(target);
            if (data == null) return false;

            return _config.MinPlayTime <= 0 || data.PlayTime >= _config.MinPlayTime;
        }

        private string FormatShortTime(BasePlayer player, TimeSpan time)
        {
            var list = Pool.GetList<string>();

            if (time.Days != 0)
                list.Add(Msg(player, DaysFormat, time.Days));

            if (time.Hours != 0)
                list.Add(Msg(player, HoursFormat, time.Hours));

            if (time.Minutes != 0)
                list.Add(Msg(player, MinutesFormat, time.Minutes));

            if (time.Seconds != 0)
                list.Add(Msg(player, SecondsFormat, time.Seconds));

            var result = string.Join(" ", list);
            Pool.FreeList(ref list);
            return result;
        }

        private void LoadImages()
        {
            if (!ImageLibrary)
            {
                BroadcastILNotInstalled();
            }
            else
            {
                _enabledImageLibrary = true;
                
                var imagesList = new Dictionary<string, string>();

                _config.Awards.ForEach(item =>
                {
                    if (!string.IsNullOrEmpty(item.Image))
                        imagesList.TryAdd(item.Image, item.Image);
                });

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }
        }

        private void BroadcastILNotInstalled()
        {
            for (var i = 0; i < 5; i++)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
            }
        }

        #endregion

        #region API

        private string GetPromoCode(BasePlayer member)
        {
            return GetPromoCode(member.userID);
        }

        private string GetPromoCode(string member)
        {
            return GetPromoCode(ulong.Parse(member));
        }

        private string GetPromoCode(ulong member)
        {
            return GetPlayerData(member)?.PromoCode;
        }

        #endregion

        #region Lang

        private const string
            NoILError = "NoILError",
            DaysFormat = "DaysFormat",
            HoursFormat = "HoursFormat",
            MinutesFormat = "MinutesFormat",
            SecondsFormat = "SecondsFormat",
            CloseButton = "CloseButton",
            TitleMenu = "TitleMenu",
            PromoCode = "PromoCode",
            ActivatedPromoCode = "ActivatedPromoCode",
            ActivatedSelfPromoCode = "ActivatedSelfPromoCode",
            CannotActivateSelfPromo = "CannotActivateSelfPromo",
            AlreadyActivated = "AlreadyActivated",
            ReceivedItem = "ReceivedItem",
            InvitedPlayersTitle = "InvitedPlayersTitle",
            NextBtn = "NextBtn",
            BackBtn = "BackBtn",
            ItemNameTitle = "ItemNameTitle",
            InvitedAmountTitle = "InvitedAmountTitle",
            ReceivedItemIcon = "ReceivedItemIcon",
            ReceiveItemIcon = "ReceiveItemIcon",
            YourPromoDescription = "YourPromoDescription",
            EnterPromoDescription = "EnterPromoDescription",
            NoPermission = "NoPermission",
            ErrorSyntax = "ErrorSyntax",
            NotEnoughTime = "NotEnoughTime";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [CloseButton] = "✕",
                [TitleMenu] = "Referral System",
                [PromoCode] = "Promo code '{0}' not found",
                [ActivatedPromoCode] = "Player '{0}' has activated your promo code!",
                [ActivatedSelfPromoCode] = "You have activated the promo code '{0}'!",
                [CannotActivateSelfPromo] = "You cannot activate your promo code!",
                [AlreadyActivated] = "You have already activated the promo code!",
                [ReceivedItem] = "Congratulations! You received '{0}'",
                [InvitedPlayersTitle] = "Invited players:",
                [NextBtn] = "▼",
                [BackBtn] = "▲",
                [ItemNameTitle] = "Item name",
                [InvitedAmountTitle] = "Invited:",
                [ReceivedItemIcon] = "✔",
                [ReceiveItemIcon] = "＋",
                [YourPromoDescription] =
                    "This is your promo code. You can share it with other players, if they activate it, you will receive prizes.",
                [EnterPromoDescription] =
                    "Enter the promo code of the player who invited you, in return he will receive a reward.",
                [NoPermission] = "You don't have permission to use this command!",
                [ErrorSyntax] = "Error syntax! Use: /{0} [promo code]",
                [NotEnoughTime] = "You need to play on the server for another {0}.",
                [DaysFormat] = " {0} d.",
                [HoursFormat] = " {0} h.",
                [MinutesFormat] = " {0} m.",
                [SecondsFormat] = " {0} s.",
                [NoILError] = "The plugin does not work correctly, contact the administrator!"
            }, this);
        }

        private string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, userid), obj);
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
            if (_config.UseNotify && (Notify != null || UINotify != null))
                Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion
    }
}