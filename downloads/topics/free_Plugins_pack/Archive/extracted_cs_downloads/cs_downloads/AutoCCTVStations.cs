using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

/*
 * // TODO: Add limits + Multi Permission support + Individual player UI Editors.
 *
 * This update 1.0.9
 * Imported patch submitted by wulf.
 * + removed un-used field
 */



namespace Oxide.Plugins
{
    [Info("Auto CCTV Stations", "Khan", "1.0.9")]
    [Description("Automatically adds monument cctv cameras to all placed computer stations.")]
    public class AutoCCTVStations : CovalencePlugin
    {
        #region Fields

        private bool _newsave;
        private Configuration _config;
        private Hash<ulong, int> _stationPage = new Hash<ulong, int>();
        private const string Admin = "autocctvstations.admin";
        private const string Use = "autocctvstations.use";
        private const string CCTV_OVERLAY = "AutoCCTVStationOverlay";
        private const string CCTV_CONTENT_NAME = "AutoCCTVStationContentName";
        private const string CCTV_Desc_Overlay = "AutoCCTVStationDescOverlay";
        private Dictionary<string, uint> _codes = new Dictionary<string, uint>();
        private Dictionary<CargoShip, List<CCTV_RC>> _cargoshipcctv = new Dictionary<CargoShip, List<CCTV_RC>>();

        #endregion

        #region Config

        internal class Configuration : SerializableConfiguration
        {
            [JsonProperty("Sets the CCTV Editor command")]
            public string CctvEditorCommand = "cctvedit";
            [JsonProperty("Enables Cargo Ship cameras ( Requires Cargo Ship CCTV plugin on umod ")]
            public bool EnableCargoCams = false;
            public Text Text = new Text();
            public Colors Colors = new Colors();
            public Dictionary<string, bool> Cameras = new Dictionary<string, bool>();
        }
        
        public class Text
        {
            [JsonProperty("CCTV Auto Station UI Editor")]
            public string EditorMsg = "CCTV Auto Station Editor ◝(⁰▿⁰)◜";
            [JsonProperty("UI - Back Button Text")]
            public string BackButtonText = "◀";
            [JsonProperty("UI - Forward Button Text")]
            public string ForwardButtonText = "▶";
            [JsonProperty("UI - Close Label")] 
            public string CloseButtonlabel = "✖";
        }

        public class Colors
        {
            [JsonProperty("UI - Button Toggle Color")]
            public string ToggleColor = "#9ab36d";
            public Color TextColor = new Color( "#01579b", 1f);
            public Color ButtonBackGroundColor = new Color("#0E0E10", 0.9f);
            public Color ButtonGreenText = new Color("#9ab36d", 0.431f);
            public Color ButtonGrey = new Color("#bfbfbf", 0.3f);
            public Color ButtonGreyText = new Color("#bfbfbf", 1f);
        }

        private void CheckConfig()
        {
            _codes.Clear();
            bool hasChanged = false;
            foreach (var controllable in RemoteControlEntity.allControllables)
            {
                CCTV_RC cctv = controllable as CCTV_RC;

                if (cctv == null || !cctv.IsStatic()) continue;

                if (!_config.Cameras.ContainsKey(cctv.rcIdentifier))
                {
                    _config.Cameras.Add(cctv.rcIdentifier, false);
                    hasChanged = true;
                }

                if (_config.Cameras.Count > 0 && _config.Cameras.ContainsKey(cctv.rcIdentifier) && _config.Cameras[cctv.rcIdentifier])
                    _codes.Add(cctv.rcIdentifier, cctv.net.ID);
            }

            if (hasChanged)
                SaveConfig();
        }

        private void UpdateConfig()
        {
            List<string> tempControllable = Facepunch.Pool.GetList<string>();

            foreach (var controllable in RemoteControlEntity.allControllables)
            {
                CCTV_RC cctv = controllable as CCTV_RC;
                if (cctv == null || !cctv.IsStatic()) 
                    continue;

                if (!tempControllable.Contains(cctv.rcIdentifier))
                    tempControllable.Add(cctv.rcIdentifier);
            }

            int count = 0;

            foreach (var camera in _config.Cameras.ToArray())
            {
                if (tempControllable.Contains(camera.Key)) 
                    continue;

                if (_config.Cameras.ContainsKey(camera.Key))
                {
                    _config.Cameras.Remove(camera.Key);
                    count++;
                }
            }

            if (count > 0)
                PrintWarning($"removed {count} previously enabled cameras, please update settings");

            SaveConfig();

            Facepunch.Pool.FreeList(ref tempControllable);
        }

        #endregion

        #region Updater

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() =>
                JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                            .ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue) token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults,
            Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        #endregion

        #region Oxide

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();

                if (_config == null)
                {
                    PrintWarning($"Generating Config File for {Name}");
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
            }
        }
        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void Unload()
        {
             _codes.Clear();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString.IsSteamId())
                {
                    DestroyUi(player, true);
                    CuiHelper.DestroyUi(player, "cctvstation");
                    CuiHelper.DestroyUi(player, "ButtonForwardstation");
                    CuiHelper.DestroyUi(player, "ButtonBackstation");
                }
            }

            _stationPage.Clear();
        }

        private void OnNewSave(string filename)
        {
            _newsave = true;
        }

        private void Init()
        {
            Unsubscribe(nameof(OnCargoShipCCTVSpawned));
            Unsubscribe(nameof(OnEntityKill));
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(Admin, this);
            permission.RegisterPermission(Use, this);
            CheckConfig();
            AddCovalenceCommand(_config.CctvEditorCommand, nameof(CmdCctvEditor));
            if (_config.EnableCargoCams)
            {
                Subscribe(nameof(OnCargoShipCCTVSpawned));
                Subscribe(nameof(OnEntityKill));
            }

            if (_newsave)
                UpdateConfig();
        }

        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            ComputerStation computerStation = gameObject.ToBaseEntity() as ComputerStation;

            NextFrame(() =>
            {
                if (computerStation == null) return;

                if (computerStation.OwnerID != 0UL && permission.UserHasPermission(computerStation.OwnerID.ToString(), Use))
                {
                    foreach (var cam in _codes)
                    {
                        if (computerStation.controlBookmarks.ContainsKey(cam.Key)) continue;
                        computerStation.controlBookmarks.Add(cam.Key, cam.Value);
                    }

                    computerStation.SendNetworkUpdateImmediate();
                }
            });
        }

        private void OnEntityMounted(ComputerStation station, BasePlayer player)
        {
            if (station == null || player != null && !permission.UserHasPermission(player.UserIDString, Use)) return;
            ShowStation(player, station);
        }

        //Remover tool calls kill & gives the item back which never triggers the dismount hook which results in the UI over lay bug. Added OnEntityKill hook to fix the problem
        // This patch will also fix it if any owner level player ent kills the stations while a player is mounted.
        private void OnEntityKill(ComputerStation station)
        {
            if (station._mounted == null)
                return;

            station.DismountPlayer(station._mounted);
        }

        private void OnEntityDismounted(ComputerStation station, BasePlayer player)
        {
            if (station == null) return;
            CuiHelper.DestroyUi(player, "cctvstation");
            CuiHelper.DestroyUi(player, "ButtonForwardstation");
            CuiHelper.DestroyUi(player, "ButtonBackstation");
        }

        private void OnCargoShipCCTVSpawned(CargoShip boat, List<CCTV_RC> cams)
        {
            _cargoshipcctv[boat] = cams;

            foreach (var station in BaseNetworkable.serverEntities.OfType<ComputerStation>())
            {
                if (station.isStatic || station.OwnerID != 0UL && !permission.UserHasPermission(station.OwnerID.ToString(), Use)) continue;
                foreach (var cam in cams)
                {
                    if (!station.controlBookmarks.ContainsKey(cam.rcIdentifier))
                        station.controlBookmarks.Add(cam.rcIdentifier, cam.net.ID);
                }
                station.SendNetworkUpdateImmediate();
            }
        }

        private void OnEntityKill(CargoShip boat)
        {
            if (boat == null || !_cargoshipcctv.ContainsKey(boat)) return;

            List<CCTV_RC> cams = _cargoshipcctv[boat];

            foreach (var station in BaseNetworkable.serverEntities.OfType<ComputerStation>())
            {
                foreach (var cam in cams)
                {
                    station.controlBookmarks.Remove(cam.rcIdentifier);
                }
                station.SendNetworkUpdate();
            }

            _cargoshipcctv.Remove(boat);
        }

        private void OnBookmarkControlStarted(ComputerStation computerStation, BasePlayer player, string bookmarkName, IRemoteControllable remoteControllable)
        {
            if (!permission.UserHasPermission(player.UserIDString, Use)) return;
            ShowStation(player, computerStation, _stationPage[player.userID]);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_stationPage.ContainsKey(player.userID))
                _stationPage.Remove(player.userID);
        }

        #endregion

        #region CUI Editor

        private CuiElementContainer CreateOverlay()
        {
            return new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        Image = { Color = "0 0 0 0.98" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        CursorEnabled = true
                    },
                    "Overlay", CCTV_OVERLAY
                },
                {
                    new CuiLabel //Welcome Msg
                    {
                        Text =
                        {
                            Text = _config.Text.EditorMsg,
                            FontSize = 30,
                            Color = _config.Colors.ButtonGreyText.Rgb,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.302 0.65",
                            AnchorMax = "0.702 0.75"
                        }
                    },
                    CCTV_OVERLAY
                },
                {
                    new CuiButton //close button Label
                    {
                        Button =
                        {
                            Command = $"cctveditstations.close", 
                            Close = CCTV_OVERLAY,
                            Color = _config.Colors.ButtonGrey.Rgb
                        },
                        RectTransform = {AnchorMin = "0.444 0.11", AnchorMax = "0.54 0.16"},
                        Text =
                        {
                            Text = _config.Text.CloseButtonlabel,
                            FontSize = 20,
                            Color = _config.Colors.ButtonGreyText.Rgb,
                            Align = TextAnchor.MiddleCenter
                        }
                    },
                    CCTV_OVERLAY, "close"
                }
            };
        }

        private readonly CuiLabel _editorDescription = new CuiLabel
        {
            Text =
            {
                Text = "{editorDescription}",
                FontSize = 15,
                Align = TextAnchor.MiddleCenter
            },
            RectTransform =
            {
                AnchorMin = "0.17 0.63",
                AnchorMax = "0.81 0.68"
            }
        };

        private void CreateTab(ref CuiElementContainer container, string cam, int editorpageminus, int rowPos)
        {
            int numberPerRow = 5;

            float padding = 0.01f;
            float margin = (0.005f);

            float width = ((1f - (padding * (numberPerRow + 1))) / numberPerRow);
            float height = (width * 0.82f);

            int row = (int) Math.Floor((float) rowPos / numberPerRow);
            int col = (rowPos - (row * numberPerRow));

            container.Add(new CuiButton
            {
                Button =
                {
                    Command = $"autocctvstations.toggle {cam} {editorpageminus}",
                    Color = _config.Colors.ButtonBackGroundColor.Rgb //"0.5 0.5 0.5 0.5"
                },
                RectTransform = // 0.050 <width  padding> 0.056
                {
                    AnchorMin = $"{margin + (width * col) + (padding * col)} {(1f - padding) - ((row + 1) * height) - (padding * row)}",
                    AnchorMax = $"{margin + (width * (col + 1)) + (0.006f + padding * col)} {(1f - padding) - (row * height) - (padding * row)}"
                },
                Text =
                {
                    Text = $"{cam} \n <color={_config.Colors.ToggleColor}>{_config.Cameras[cam]}</color>",
                    Align = TextAnchor.MiddleCenter,
                    Color = _config.Colors.TextColor.Rgb,
                    //Font = "robotocondensed-regular.ttf",
                    FontSize = 12
                }
            }, CCTV_CONTENT_NAME);
        }

        private void CreateEditorChangePage(ref CuiElementContainer container, int from)
        {
            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"autocctvstations.page {(from - 1)}",
                        Color = _config.Colors.ButtonGrey.Rgb //"0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.344 0.11",
                        AnchorMax = "0.44 0.16"
                    },
                    Text =
                    {
                        Text = _config.Text.BackButtonText,
                        Color = _config.Colors.ButtonGreenText.Rgb,
                        FontSize = 30,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                CCTV_OVERLAY,
                "ButtonBack");

            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"autocctvstations.page {(from + 1)}",
                        Color = _config.Colors.ButtonGrey.Rgb //"0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.544 0.11",
                        AnchorMax = "0.64 0.16"
                    },
                    Text =
                    {
                        Text = _config.Text.ForwardButtonText,
                        Color = _config.Colors.ButtonGreenText.Rgb,
                        FontSize = 30,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                CCTV_OVERLAY,
                "ButtonForward");
        }

        private void DestroyUi(BasePlayer player, bool full = false)
        {
            CuiHelper.DestroyUi(player, CCTV_CONTENT_NAME);
            CuiHelper.DestroyUi(player, CCTV_Desc_Overlay);
            CuiHelper.DestroyUi(player, "ButtonForward");
            CuiHelper.DestroyUi(player, "ButtonBack");
            if (!full) return;
            CuiHelper.DestroyUi(player, CCTV_OVERLAY);
        }

        private void ShowEditor(BasePlayer player, int from = 0, bool fullPaint = false, bool refreshcounter = true)
        {
            CuiElementContainer container;

            _editorDescription.Text.Color = _config.Colors.TextColor.Rgb;
            int count = 0;
            foreach (var cam in _config.Cameras)
            {
                if (_config.Cameras[cam.Key])
                    count++;
            }
            _editorDescription.Text.Text = $"Enabled Count {count}";

            if (fullPaint)
            {
                CuiHelper.DestroyUi(player, CCTV_OVERLAY);
                container = CreateOverlay();
                if (!refreshcounter)
                    container.Add(_editorDescription, CCTV_OVERLAY, CCTV_Desc_Overlay);
            }
            else
            {
                container = new CuiElementContainer();
            }
            
            if (refreshcounter)
            {
                CuiHelper.DestroyUi(player, CCTV_Desc_Overlay);
                container.Add(_editorDescription, CCTV_OVERLAY, CCTV_Desc_Overlay);
            }

            CuiHelper.DestroyUi(player, CCTV_CONTENT_NAME);
            CuiHelper.DestroyUi(player, "ButtonForward");
            CuiHelper.DestroyUi(player, "ButtonBack");

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0.98"},
                RectTransform =
                {
                    AnchorMin = "0.223 0.2", 
                    AnchorMax = "0.763 0.63"
                }
            }, CCTV_OVERLAY, CCTV_CONTENT_NAME);

            int total = _config.Cameras.Count / 30;
            from = Mathf.Clamp(from, 0, total);

            int rowPos = 0;
            foreach (var data in _config.Cameras.Skip(from * 30).Take(30))
            {
                CreateTab(ref container, data.Key, from, rowPos);
                rowPos++;
            }

            if (total > 0)
                CreateEditorChangePage(ref container, from);

            CuiHelper.AddUi(player, container);
        }

        private void CmdCctvEditor(IPlayer player, string command, string[] args)
        {
            if (player == null || !player.HasPermission(Admin)) return;
            ShowEditor(player.Object as BasePlayer, 0, true);
        }

        #endregion

        #region Console Commands

        [Command("autocctvstations.page")]
        private void CmdEditorPageShow(IPlayer player, string command, string[] args)
        {
            if (player == null || !player.HasPermission(Admin)) return;
            if (args.Length == 0) return;
            BasePlayer p = player.Object as BasePlayer;
            ShowEditor(p, args[0].ToInt());
        }

        [Command("autocctvstations.toggle")]
        private void CmdToggleButtons(IPlayer player, string command, string[] args)
        {
            if (player == null || !player.HasPermission(Admin)) return;
            if (args.Length < 2) return;
            BasePlayer p = player.Object as BasePlayer;

            if (_config.Cameras.ContainsKey(args[0]))
                _config.Cameras[args[0]] = !_config.Cameras[args[0]];

            ShowEditor(p, args[1].ToInt());
        }

        [Command("cctveditstations.close")]
        private void ConsoleEditClose(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
                CheckConfig();
            SaveConfig();
        }

        [Command("autocctvstations.station.page")]
        private void ConsoleStation(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0) return;
            int page = args[0].ToInt();

            BasePlayer p = player.Object as BasePlayer;
            ComputerStation station = p.GetMounted() as ComputerStation;
            if (station == null) return;

            ShowStation(p, station, page);
        }

        #endregion
 
        #region CUI Station

        private readonly CuiLabel _stationDescription = new CuiLabel
        {
            Text =
            {
                Text = "{editorDescription}",
                FontSize = 15,
                Align = TextAnchor.MiddleCenter
            },
            RectTransform =
            {
                AnchorMin = "0.1 0.22",
                AnchorMax = "0.15 0.25"
            }
        };
        private void CreateStationChangePage(ref CuiElementContainer container, int from)
        {
            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"autocctvstations.station.page {(from - 1)}",
                        Color = _config.Colors.ButtonGrey.Rgb //"0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.046 0.1",
                        AnchorMax = "0.065 0.2"
                    },
                    Text =
                    {
                        Text = _config.Text.BackButtonText,
                        Color = _config.Colors.ButtonGreenText.Rgb,
                        FontSize = 25,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                "Overlay",
                "ButtonBackstation");

            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"autocctvstations.station.page {(from + 1)}",
                        Color = _config.Colors.ButtonGrey.Rgb //"0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.194 0.1",
                        AnchorMax = "0.215 0.2"
                    },
                    Text =
                    {
                        Text = _config.Text.ForwardButtonText,
                        Color = _config.Colors.ButtonGreenText.Rgb,
                        FontSize = 25,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                "Overlay",
                "ButtonForwardstation");
        }
        public string GenerateControlBookmarkString(ComputerStation station, int page)
        {
            string controlBookmarkString = "";
            foreach (KeyValuePair<string, uint> controlBookmark in station.controlBookmarks.Skip(page * 14).Take(14).ToArray())
            {
                controlBookmarkString += controlBookmark.Key;
                controlBookmarkString += ":";
                controlBookmarkString += controlBookmark.Value;
                controlBookmarkString += ";";
            }
            return controlBookmarkString;
        }
        private void ShowStation(BasePlayer player, ComputerStation station, int from = 0)
        {
            _stationPage[player.userID] = from;
            CuiElementContainer container = new CuiElementContainer();

            int total = station.controlBookmarks.Count / 14;
            from = Mathf.Clamp(from, 0, total);

            station.ClientRPCPlayer<string>(null, player, "ReceiveBookmarks", GenerateControlBookmarkString(station, from));

            if (total > 0)
                CreateStationChangePage(ref container, from);

            _stationDescription.Text.Color = _config.Colors.TextColor.Rgb;
            _stationDescription.Text.Text = $"Page {from + 1}";
            container.Add(_stationDescription, "Overlay", "cctvstation");

            CuiHelper.DestroyUi(player, "cctvstation");
            CuiHelper.DestroyUi(player, "ButtonForwardstation");
            CuiHelper.DestroyUi(player, "ButtonBackstation");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region UI Colors

        public class Color
        {
            public string Hex;
            public float A;
            
            [JsonIgnore]
            public int R;
            [JsonIgnore]
            public int G;
            [JsonIgnore]
            public int B;
            
            [JsonIgnore]
            private string _rgb;            

            [JsonIgnore]
            public string Rgb
            {
                get
                {
                    if (_rgb != null) return _rgb;
                    
                    if (Hex.StartsWith("#"))
                        Hex = Hex.Substring(1);

                    if (Hex.Length == 6)
                    {
                        R = int.Parse(Hex.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                        G = int.Parse(Hex.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                        B = int.Parse(Hex.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                    }

                    _rgb = $"{(double)R / 255} {(double)G / 255} {(double)B / 255} {A}";

                    return _rgb;
                }
            }

            public Color(string hex, float alpha = 1f)
            {
                if (hex.StartsWith("#")) hex = hex.Substring(1);

                A = alpha;
                Hex = "#" + hex;
            }
        }

        #endregion
    }
}