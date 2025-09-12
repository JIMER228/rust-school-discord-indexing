using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Color = UnityEngine.Color;
namespace Oxide.Plugins
{
    [Info("RustMap", "Menevt", "2.1.53b")]
    class RustMap : RustPlugin
    {
        [PluginReference] Plugin Clans, Friends, MutualPermission, NTeleportation, Teleport, Teleportation, SleepingbagHome, HomesGUI, PlayersClasses, ImageLibrary, QuarryCapture, NoEscape;

        [PluginReference] Plugin SpawnsControl;
        Dictionary<Vector3, int> GetSpawnZones() => SpawnsControl?.Call<Dictionary<Vector3, int>>("GetSpawnZones");

        class MapMarker
        {
            public Transform transform;
            public int Rotation = -1;
            public Vector2 anchorPosition
            {
                get
                {
                    return m_Instance.ToScreenCoords(position);
                }
            }
            public Vector2 position = Vector2.zero;
            public string name;
            public string png;
            public bool rotSupport = false;
            public string text;
            public float size;
            public float alpha;
            public int fontsize;
            public ulong counter = 0;
            public bool inMap = true;
            protected MapMarker(Transform transform)
            {
                this.transform = transform;
            }
            public virtual bool NeedRedraw()
            {
                if (transform == null) return false;
                var lastRot = Rotation;
                if (rotSupport) Rotation = GetRotation(transform.eulerAngles.y);
                var lastPos = position;
                position = transform.position;
                position.y = 0;
                if (rotSupport && Rotation != lastRot)
                {
                    position = lastPos;
                    return true;
                }
                if (Vector3.Distance(position, lastPos) > 0.5) return true;
                position = lastPos;
                return false;
            }
            public static MapMarker Create(Transform transform)
            {
                return new MapMarker(transform);
            }
        }

        class MapPlayer : MapMarker
        {
            public BasePlayer player;
            public List<MapPlayer> clanTeam = new List<MapPlayer>();
            protected MapPlayer(BasePlayer player) : base(player.transform)
            {
                this.player = player;
            }
            public override bool NeedRedraw()
            {
                var lastRot = Rotation;
                if (player == null || transform == null) return false;
                Rotation = GetRotation(player.eyes.rotation.eulerAngles.y);
                var lastPos = position;
                position = transform.position;
                position.y = 0;
                if (Rotation != lastRot)
                {
                    position = lastPos;
                    return true;
                }
                if (Vector3.Distance(position, lastPos) > 0.5)
                {
                    return true;
                }
                position = lastPos;
                return false;
            }
            public void OnCloseMap()
            {
                Rotation = -1;
                position = Vector2.zero;
                clanTeam?.ForEach(p => p.OnCloseMap());
            }
            public static MapPlayer Create(BasePlayer player)
            {
                return new MapPlayer(player)
                {
                    alpha = 1,
                    size = 0.3f
                };
            }
        }

        private PluginConfig config;
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за заказ плагина у разработчика OxideBro. Если вы передадите этот плагин сторонним лицам знайте - это лишает вас гарантированных обновлений!");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion != Version)
                UpdateConfigValues();

            SaveConfig();

            if (!string.IsNullOrEmpty(config.mainSettings.mapUrl))
            {
                if (!config.mainSettings.mapUrl.ToLower().Contains("http"))
                {
                    config.mainSettings.mapUrl = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + config.mainSettings.mapUrl;
                }
                DataMap = config.mainSettings.mapUrl;
            }
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            if (Author != _("BkvqrOeb - EhfgCyhtva.eh")) Author = _("Cyhtva nhgube BkvqrOeb - EhfgCyhtva.eh");
            Config.WriteObject(config);
        }

        public class MainSettings
        {
            [JsonProperty("Кастомная карта (http:// или с папки data/RustMap)")]
            public string mapUrl = "";

            [JsonProperty("Частота обновления иконок на карте")]
            public float MapUpdate = 0.3f;

            [JsonProperty("Время до автоматического закрытия карты после ее открытия")]
            public int TimeToClose = 10;

            [JsonProperty("Прозрачность карты")]
            public float mapAlpha = 1f;

            [JsonProperty("Размер карты")]
            public float mapSize = 0.5f;

            [JsonProperty("Отображать местоположение монументов")]
            public bool monuments = true;

            [JsonProperty("Отображать названия монументов")]
            public bool monumentIconNames = true;

            [JsonProperty("Размер шрифта названия монументов")]
            public int monumentsFontSize = 13;

            [JsonProperty("Отображать пещеры")]
            public bool caves = true;

            [JsonProperty("Отображать водонапорные башни")]
            public bool water = true;

            [JsonProperty("Отображать подстанции")]
            public bool powersub = true;

            [JsonProperty("Отображать местоположение cброшенного груза с грузового вертолёта")]
            public bool NewHeliCrate = true;

            [JsonProperty("Отображать местоположение грузового вертолёта (чинука)")]
            public bool NewHeli = true;

            [JsonProperty("Отображать местоположение магазинов с Сompound (Аванпост)")]
            public bool NPCVendingMachine = false;

            [JsonProperty("Отображать местоположение самолета")]
            public bool plane = true;

            [JsonProperty("Отображать местоположение танка")]
            public bool bradley = true;

            [JsonProperty("Отображать местоположение вертолёта")]
            public bool heli = true;

            [JsonProperty("Отображать местоположение cброшенного груза (Аирдроп)")]
            public bool planeDrop = true;

            [JsonProperty("Отображать местоположение ящиков с вертолёта")]
            public bool heliDrop = true;

            [JsonProperty("Отображать местоположение корабля")]
            public bool ship = true;

            [JsonProperty("Отображать местоположение торговых автоматов")]
            public bool vendingMachine = true;

            [JsonProperty("Отображать местоположение пустых торговых автоматов")]
            public bool vendingMachineEmpty = true;

            [JsonProperty("Показывать текущие координаты игрока")]
            public bool playerCoordinates = true;

            [JsonProperty("Поддержка Clans (Отображение сокланов на карте)")]
            public bool clanSupport = false;

            [JsonProperty("Поддержка Friends (Отображение друзей на карте)")]
            public bool friendsSupport = false;

            [JsonProperty("Поддержка Внутриигровой системы друзей (Отображение друзей на карте)")]
            public bool TeamSupport = false;

            [JsonProperty("Поддержка NoEscape (Отображение рейда дома игрока)")]
            public bool noescapeSupport = false;

            [JsonProperty("Поддержка NoEscape (Отображение всех рейдов на сервере по привилегии)")]
            public bool noescapeSupportAdmin = false;

            [JsonProperty("Поддержка Teleportation, NTeleportation, Teleport (Отображение сохраненных точек игроков)")]
            public bool teleportSupport = false;
        }


        class PluginConfig
        {
            [JsonProperty("Основные настройки")]
            public MainSettings mainSettings;
            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    mainSettings = new MainSettings(),
                    PluginVersion = new VersionNumber(),
                };
            }
        }

        static RustMap m_Instance;
        private string mapIconJson = @"[{""name"":""{0}"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.RawImage"",""sprite"":""assets/content/textures/generic/fulltransparent.tga"",""png"":""{3}"",""color"":""{color}""},{""type"":""RectTransform"",""anchormin"":""{1}"",""anchormax"":""{2}""}]}]";
        private string mapIconTextJsonIcon = @"[{""name"":""{0}"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{3}"",""font"":""RobotoCondensed-Regular.ttf"",""align"":""MiddleCenter"",""fontSize"":{4},""color"":""{color1}""},{""type"": ""UnityEngine.UI.Outline"",""color"": ""0 0 0 0.6""},{""type"":""RectTransform"",""anchormin"":""{1}"",""anchormax"":""{2}""}]}]";
        private string mapCoordsTextJson = @"[{""name"":""map_coordinates"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{0}"",""font"":""RobotoCondensed-Regular.ttf"",""align"":""MiddleCenter"",""fontSize"":18},{""type"": ""UnityEngine.UI.Outline"",""color"": ""0 0 0 0.3"",""distance"": ""0.5 -0.5""},{""type"":""RectTransform"",""anchormin"":""0 0.95"",""anchormax"":""1 1""}]}]";

        Dictionary<BasePlayer, MapPlayer> mapPlayers = new Dictionary<BasePlayer, MapPlayer>();
        Dictionary<BasePlayer, MapPlayer> subscribers = new Dictionary<BasePlayer, MapPlayer>();
        List<MapMarker> temporaryMarkers = new List<MapMarker>();
        const string MAP_ADMIN = "rustmap.admin";
        string monumentsJson;
        bool init = false;
        private List<BasePlayer> AllPlayerUsers = new List<BasePlayer>();
        [ChatCommand("map")]
        void cmdMapControl(BasePlayer player, string command, string[] args)
        {
            if (!init || player == null)
            {
                SendReply(player, "Извините! В данный момент карта не активирована");
                return;
            }
            CuiHelper.DestroyUi(player, "maphelp_2");
            if (config.mainSettings.teleportSupport)
            {
                if (args.Count() >= 1 && args[0] == "homes")
                {
                    if (data.MapPlayerData[player.userID].Homes)
                    {
                        data.MapPlayerData[player.userID].Homes = data.MapPlayerData[player.userID].Homes = false;
                        SendReply(player, $"<color=orange>Map homes</color>: False");
                        return;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].Homes = data.MapPlayerData[player.userID].Homes = true;
                        SendReply(player, $"<color=orange>Map homes</color>: True");
                        return;
                    }
                }
            }
            if (config.mainSettings.friendsSupport)
            {
                if (args.Count() >= 1 && args[0] == "friends")
                {
                    if (data.MapPlayerData[player.userID].Friends)
                    {
                        data.MapPlayerData[player.userID].Friends = data.MapPlayerData[player.userID].Friends = false;
                        SendReply(player, $"<color=orange>Map friends</color>: False");
                        return;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].Friends = data.MapPlayerData[player.userID].Friends = true;
                        SendReply(player, $"<color=orange>Map Friends</color>: True");
                        return;
                    }
                }
            }
            if (config.mainSettings.clanSupport)
            {
                if (args.Count() >= 1 && args[0] == "clans")
                {
                    if (data.MapPlayerData[player.userID].Clans)
                    {
                        data.MapPlayerData[player.userID].Clans = data.MapPlayerData[player.userID].Clans = false;
                        SendReply(player, $"<color=orange>Map Clans</color>: False");
                        return;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].Clans = data.MapPlayerData[player.userID].Clans = true;
                        SendReply(player, $"<color=orange>Map Clans</color>: True");
                        return;
                    }
                }
            }
            if (permission.UserHasPermission(player.UserIDString, MAP_ADMIN))
            {
                if (args.Count() >= 1 && args[0] == "players")
                {
                    if (data.MapPlayerData[player.userID].AllPlayers)
                    {
                        data.MapPlayerData[player.userID].AllPlayers = data.MapPlayerData[player.userID].AllPlayers = false;
                        SendReply(player, $"<color=orange>Map AllPlayers</color>: False");
                        return;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].AllPlayers = data.MapPlayerData[player.userID].AllPlayers = true;
                        SendReply(player, $"<color=orange>Map AllPlayers</color>: True");
                        return;
                    }
                }
            }
            if (subscribers.Keys.Contains(player))
            {
                CloseMap(player);
            }
            else
            {
                if (args.Count() >= 1 && args[0] == "help")
                {
                    return;
                }
                if (permission.UserHasPermission(player.UserIDString, MAP_ADMIN))
                {
                    if (data.MapPlayerData[player.userID].AllPlayers)
                        AllPlayerUsers.Add(player);
                    else
                        if (AllPlayerUsers.Contains(player)) AllPlayerUsers.Remove(player);
                }
                OpenMap(player);
            }
        }
        [ConsoleCommand("map.open")]
        void ConsoleMap(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!init) return;
            if (subscribers.Keys.Contains(player))
                CloseMap(player);
            else
                OpenMap(player);
            return;
        }

        private Timer mtimer;


        void Loaded()
        {
            m_Instance = this;
            LoadData();
            permission.RegisterPermission(MAP_ADMIN, this);
            permission.RegisterPermission(permissionName, this);
            anchorMin = new Vector2(0.5f - config.mainSettings.mapSize * 0.5f, 0.5f - config.mainSettings.mapSize * 0.800f);
            anchorMax = new Vector2(0.5f + config.mainSettings.mapSize * 0.5f, 0.5f + config.mainSettings.mapSize * 0.930f);
            mapIconTextJsonIcon = mapIconTextJsonIcon.Replace("{color1}", "1 1 1 1");
        }

        void OnServerInitialized()
        {
            _terrainTexture = TerrainTexturing.Instance;
            if (_terrainTexture == null)
                return;
            _terrain = _terrainTexture.GetComponent<Terrain>();
            if (_terrain == null) return;
            _heightMap = _terrainTexture.GetComponent<TerrainHeightMap>();
            if (_heightMap == null)
                return;
            _splatMap = _terrainTexture.GetComponent<TerrainSplatMap>();
            if (_splatMap == null)
                return;
         
            foreach (var entity in BaseNetworkable.serverEntities.Select(p => p as BaseEntity).Where(p => p != null)) OnEntitySpawned(entity);
            InitFileManager();

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            mtimer = timer.Every(config.mainSettings.MapUpdate, () =>
            {
                foreach (var mm in temporaryMarkers)
                    if (mm.NeedRedraw())
                    {
                        ++mm.counter;
                        foreach (var sub in subscribers) DrawMapMarker(sub.Key, mm);
                    }

                foreach (var sub in subscribers)
                {
                    RedrawPlayers(sub.Value);
                }
            });
            timer.Every(1f, TimerHandler);
        }

        [ConsoleCommand("map.update")]
        void cmdMapUpdateImages(ConsoleSystem.Arg args)
        {
            if (args.Connection != null) return;
            PrintWarning("Image update started, please wait .....");
            m_FileManager.WipeData();
        }


        void OnNewSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"RustMap/Images", new sbyte());
            PrintWarning("Wipe detected, clears image data");
        }

        Dictionary<BasePlayer, int> timers = new Dictionary<BasePlayer, int>();

        void TimerHandler()
        {
            foreach (var player in timers.Keys.ToList())
            {
                if (player == null)
                {
                    timers.Remove(player);
                    continue;
                }
                var seconds = --timers[player];
                if (seconds <= 0)
                {
                    CloseMap(player);
                    continue;
                }
            }
        }


        void LoadData()
        {
            try
            {
                MapPlayerData = Interface.Oxide.DataFileSystem.GetFile("RustMap/MapPlayerData");
                data = Interface.GetMod().DataFileSystem.ReadObject<DataStorage>("RustMap/MapPlayerData");
                CustomIcons = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, CustomMapIcons>>("RustMap/CustomIcons");
                CustomText = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, CustomMapTexts>>("RustMap/CustomText");

            }
            catch
            {
                data = new DataStorage();
                CustomIcons = new Dictionary<string, CustomMapIcons>();
                CustomText = new Dictionary<string, CustomMapTexts>();
            }
        }


        private byte[] GetBytesImage()
        {
            return ImageToByteArray(DataMap);
        }

        void OnServerSave()
        {
            PlayerSaveData();
            Interface.Oxide.DataFileSystem.WriteObject($"RustMap/CustomIcons", CustomIcons);
            Interface.Oxide.DataFileSystem.WriteObject($"RustMap/CustomText", CustomText);
        }

        private void PlayerSaveData()
        {
            if (data != null) MapPlayerData.WriteObject(data);
        }
        class DataStorage
        {
            public Dictionary<ulong, MAPDATA> MapPlayerData = new Dictionary<ulong, MAPDATA>();
            public DataStorage() { }
        }
        class MAPDATA
        {
            public string Name;
            public bool Homes;
            public bool Raid = false;
            public bool AllRaids = false;
            public bool Friends;
            public bool Clans;
            public bool Death;
            public bool AllPlayers;
            public bool Teams;
        }
        DataStorage data;
        private DynamicConfigFile MapPlayerData;
        void OnPlayerConnected(BasePlayer player)
        {
            if (!mapPlayers.ContainsKey(player)) mapPlayers[player] = MapPlayer.Create(player);
            if (!data.MapPlayerData.ContainsKey(player.userID))
            {
                data.MapPlayerData.Add(player.userID, new MAPDATA()
                {
                    Name = player.displayName,
                    Homes = false,
                    Friends = false,
                    Clans = false,
                    AllPlayers = false,
                    Death = true,
                    Teams = false,
                }
                );
            }
            if (data.MapPlayerData[player.userID].Name != player.displayName)
            {
                data.MapPlayerData[player.userID].Name = player.displayName;
            }
        }

        void Unload()
        {
            OnServerSave();
            foreach (var sub in subscribers.Keys)
                CuiHelper.DestroyUi(sub, "map_mainPanel");
            if (FileManagerObject != null)
                UnityEngine.Object.Destroy(FileManagerObject);
        }

 void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!timers.ContainsKey(player)) return;
            if (!input.WasJustReleased(BUTTON.FIRE_SECONDARY)) return;

            timers.Remove(player);
            CuiElementContainer container = new CuiElementContainer();

            CuiHelper.DestroyUi(player, "map_infomouse");

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "map_mainPanel");
            CuiHelper.AddUi(player, container);
            subscribers.Remove(player);
            mapPlayers[player].OnCloseMap();
        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null) return;
            if (config.mainSettings.plane && entity is CargoPlane) AddTemporaryMarker("plane", true, 0.03f, 1, entity.transform);
            if (config.mainSettings.planeDrop && entity is SupplyDrop)
                AddTemporaryMarker("mapsupply", false, 0.04f, 1, entity.transform, "");
            if (config.mainSettings.NewHeli && entity is CH47Helicopter)
                AddTemporaryMarker("newheli", true, 0.03f, 1, entity.transform);
            if (config.mainSettings.NewHeliCrate && entity is HackableLockedCrate)
            {
                var crate = entity as HackableLockedCrate;
                if (!(entity.GetParentEntity() is CargoShip))
                    AddTemporaryMarker("newhelicreate", false, 0.03f, 1, entity.transform);
            }
            if (config.mainSettings.bradley && entity is BradleyAPC)
                AddTemporaryMarker("bradley", true, 0.03f, 1, entity.transform);
            if (config.mainSettings.heli && entity is BaseHelicopter)
                AddTemporaryMarker("heli", true, 0.03f, 1, entity.transform);
            if (config.mainSettings.ship && entity is CargoShip)
                AddTemporaryMarker("ship", true, 0.03f, 1, entity.transform);
            if (config.mainSettings.heliDrop && entity.ShortPrefabName.Contains("heli_crate"))
                AddTemporaryMarker("helidebris", false, 0.03f, 1, entity.transform);
            if (config.mainSettings.vendingMachine && entity is VendingMachine)
            {
                if (entity is NPCVendingMachine) return;
                if (config.mainSettings.vendingMachineEmpty && !((VendingMachine)entity).IsInventoryEmpty())
                    AddTemporaryMarker("vending", false, 0.03f, 1, entity.transform);
            }
        }

        List<Vector3> GetQuarries()
        {
            return QuarryCapture?.Call("GetQuarries") as List<Vector3>;
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity?.net?.ID == null) return;
            if (entity is CargoPlane || entity is SupplyDrop || entity is BaseHelicopter || entity is HelicopterDebris || entity is VendingMachine)
            {
                var transform = entity.transform;
                var mm = temporaryMarkers.Find(p => p.transform == transform);
                if (mm != null) RemoveTemporaryMarker(mm);
            }
        }

        List<ulong> GetMembersClan(ulong playerId = 0)
        {
            if (Clans)
            {
                List<ulong> Members = new List<ulong>();
                if (Clans.ResourceId == 2087)
                {
                    var clan1tag = (string)Clans.Call("GetClanOf", playerId);
                    if (clan1tag != null)
                    {
                        var members1 = ((JObject)Clans.Call("GetClan", clan1tag))["members"].ToObject<List<ulong>>();
                        if (members1 != null) return members1;
                    }
                    return null;
                }
                if (Clans.ResourceId == 14)
                {
                    var clanmates = Clans.Call("GetClanMembers", playerId) as List<string>;
                    if (clanmates != null)
                    {
                        var list = clanmates.Select(ulong.Parse).ToList();
                        return list;
                    }
                }
            }
            return null;
        }

        Vector2 anchorMin = new Vector2();
        Vector2 anchorMax = new Vector2();

        void OpenMap(BasePlayer player)
        {
            if (player == null) return;
            if (!init)
            {
                SendReply(player, "Извините! В данный момент карта не активирована");
                return;
            }

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {

                Name = "map_mainPanel",
                Parent = "Hud",
                Components =
                    {
                        new CuiImageComponent { Color = $"1 1 1 0"},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax =  $"1 1" },
                    }
            });

           

            container.Add(new CuiElement
            {

                Parent = "map_mainPanel",
                Components =
                    {
                        new CuiButtonComponent { Color = $"1 1 1 0", Close = "map_mainPanel" },
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax =  $"1 1" },
                    }
            });

            container.Add(new CuiElement
            {
                Name = "map_mainImage",
                Parent = "map_mainPanel",
                Components =
                    {
                        new CuiRawImageComponent { Color = $"1 1 1 {config.mainSettings.mapAlpha}", Png = MapPng()  },
                        new CuiRectTransformComponent { AnchorMin = $"{anchorMin.x} {anchorMin.y}", AnchorMax =  $"{anchorMax.x} {anchorMax.y}" }
                    }
            });

            container.Add(new CuiElement
            {
                Name = "map_infomouse",
                Parent = "map_mainImage",
                FadeOut = 1f,
                Components =
                            {
                                new CuiTextComponent
                                    {
                                        Color="0.64 0.64 0.64 0.5", Text = "НАЖМИ ПРАВУЮ КНОПКУ МЫШИ ЧТОБЫ УПРАВЛЯТЬ КАРТОЙ", Align = TextAnchor.LowerCenter, FontSize = (int)TextSize(13),FadeIn = 0.5f, Font = "robotocondensed-regular.ttf"
                                    }
                                , new CuiRectTransformComponent
                                    {
                                        AnchorMin= "0 0.01", AnchorMax="1 0.1"
                                    },

                                },
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.8 0.01", AnchorMax = "0.99 0.1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "map_mainImage", "map_settings");


            container.Add(new CuiElement
            {
                Parent = "map_settings",
                Components =
                 {
                                new CuiRawImageComponent
                                    {  Color="0.64 0.64 0.64 1", Png = m_FileManager.GetPng("settings") },
                                new CuiRectTransformComponent { AnchorMin= "0 0", AnchorMax="0.17 0.38" },
                },
            });
            
            container.Add(new CuiElement
            {
                Parent = "map_settings",
                Components =
                            {
                                new CuiTextComponent
                                    {
                                        Color="0.64 0.64 0.64 1", Text = "НАСТРОЙКИ", Align = TextAnchor.LowerLeft, FontSize = (int)TextSize(13)/*, Font = "robotocondensed-regular.ttf"*/
                                    }
                                , new CuiRectTransformComponent
                                    {
                                        AnchorMin= "0.23 0.16", AnchorMax="1 0.6"
                                    },

                                },
            });

            container.Add(new CuiElement
            {
                Parent = "map_settings",
                Components =
                            {
                                new CuiTextComponent
                                    {
                                        Color="0.64 0.64 0.64 1", Text = "Нажми чтобы открыть", Align = TextAnchor.LowerLeft, FontSize = (int)TextSize(9), Font = "robotocondensed-regular.ttf"
                                    }
                                , new CuiRectTransformComponent
                                    {
                                        AnchorMin= "0.23 0", AnchorMax="1 1"
                                    },

                                },
            });


            container.Add(new CuiElement
            {
                Parent = "map_settings",
                Components =
                            {
                                new CuiButtonComponent
                                    {
                                        Color="0.64 0.64 0.64 0", Command = "map_settings"
                                    }
                                , new CuiRectTransformComponent
                                    {
                                        AnchorMin= "0 0", AnchorMax="1 0.5"
                                    },

                                },
            });
            
            CuiHelper.DestroyUi(player, "map_mainPanel");

           

            CuiHelper.AddUi(player, container);
            CuiHelper.AddUi(player, monumentsJson);

            if (SpawnsControl != null)
            {
                var zone = GetSpawnZones().FirstOrDefault();
                var anchor = ToAnchors(zone.Key, (float)zone.Value * 2 / World.Size);


                DrawIcon(player, "RaidZones" + zone, "RaidZones" + zone, anchor, images["RaidZones"], 0.95f, null, 12, false);

            }
            foreach (var mm in temporaryMarkers) DrawMapMarker(player, mm);
            List<ulong> members = new List<ulong>();
            if (data.MapPlayerData[player.userID].AllPlayers && permission.UserHasPermission(player.UserIDString, MAP_ADMIN))
            {
                mapPlayers[player].clanTeam = BasePlayer.activePlayerList.ToList().Where(p => p != player).Select(MapPlayer.Create).ToList();
            }
            else
            {
                mapPlayers[player].clanTeam = new List<MapPlayer>();
                if (config.mainSettings.clanSupport)
                {
                    if (data.MapPlayerData[player.userID].Clans)
                    {
                        var clanmates = GetMembersClan(player.userID);
                        if (clanmates != null)
                            members.AddRange(clanmates);
                    }
                }
                if (config.mainSettings.TeamSupport)
                    if (data.MapPlayerData[player.userID].Teams)
                    {
                        var teams = GetTeamMembers(player);
                        if (teams != null) members.AddRange(teams);
                    }
                if (config.mainSettings.friendsSupport)
                {
                    if (data.MapPlayerData[player.userID].Friends)
                    {
                        var friends = Friends?.Call("GetFriends", player.userID);
                        if (friends != null) members.AddRange(friends as ulong[]);
                        var classes = PlayersClasses?.Call("GetPlayers", player.userID);
                        if (classes != null) members.AddRange(classes as List<ulong>);
                        var MutualFr = MutualPermission?.Call("GetFriends", player.userID, "Map") as ulong[];
                        if (MutualFr != null)
                            members.AddRange(MutualFr as ulong[]);
                    }
                }
            }
            var homes = GetHomes(player);
            if (config.mainSettings.teleportSupport && homes != null)
                foreach (var home in homes)
                {
                    var anchors = ToAnchors(home.Value, 0.03f);
                    if (data.MapPlayerData[player.userID].Homes)
                        DrawButtonIcon(player, "sethome" + home.Key, "sethome" + home.Key, anchors, images["sethome"], 1.0f, $"home {home.Key}", $"Дом: {home.Key}", "1 1 1 1", 10, false);
                }

            if (members != null && members.Count > 0)
            {
                var onlineMembers = BasePlayer.activePlayerList.ToList().Where(p => members.Contains(p.userID) && p != player).ToList();
                mapPlayers[player].clanTeam.AddRange(onlineMembers.Select(MapPlayer.Create).ToList());
            }
            if (config.mainSettings.noescapeSupport && data.MapPlayerData[player.userID].Raid)
            {
                var zones = GetRaidZones(player.userID);
                if (zones != null) foreach (var zone in zones)
                    {
                        var anchors = ToAnchors(zone, 0.06f);
                        DrawIcon(player, "raidhome" + zone, "raidhome" + zone, anchors, images["raidhome"], 0.95f, null, 12, false);
                    }
            }

            if (config.mainSettings.noescapeSupportAdmin && data.MapPlayerData[player.userID].AllRaids && permission.UserHasPermission(player.UserIDString, MAP_ADMIN))
            {
                var zones = AdminGetRaidZones();
                if (zones != null) foreach (var zone in zones)
                    {
                        var anchors = ToAnchors(zone, 0.06f);
                        DrawIcon(player, "raidhome" + zone, "raidhome" + zone, anchors, images["raidhome"], 0.95f, null, 12, false);
                    }
            }

            if (playerDic.ContainsKey(player.userID))
            {
                if (data.MapPlayerData[player.userID].Death)
                {
                    var anchors = ToAnchors(playerDic[player.userID].ToVector3(), 0.03f);
                    DrawIconNull(player, "death" + player.userID, "death" + player.userID, anchors, images["death"], 1.0f, $"Ты умер здесь", null, 10, false);
                }
            }

            if (QuarryCapture)
            {
                var playerQuarries = GetQuarries();
                if (playerQuarries != null)
                {
                    foreach (var quarry in playerQuarries)
                    {
                        var anchors = ToAnchors(quarry, 0.03f);
                        DrawIconNull(player, "quarry" + quarry, "quarry" + quarry, anchors, images["quarry"], 1.0f, "УНИКАЛЬНЫЙ КАРЬЕР", null, 9, false);
                    }
                }
            }

            if (CustomIcons.Count > 0)
            {
                foreach (var icon in CustomIcons)
                {
                    var size = icon.Value.Size <= 0 ? 0.04f : icon.Value.Size;
                    var anchors = ToAnchors(icon.Value.position, size);
                    DrawIconNull(player, icon.Key, icon.Key, anchors, icon.Value.PNG, 1.0f, icon.Value.NAME, null, 10, false);
                }
            }

            if (CustomText.Count > 0)
            {
                foreach (var icon in CustomText)
                {
                    var size = 0.2f;
                    var anchors = ToAnchors(icon.Value.position, size);
                    DrawIconNull(player, icon.Key, icon.Key, anchors, null, 1.0f, icon.Value.NAME, icon.Value.Color, icon.Value.Size, false, 0, 1);
                }
            }

            
            subscribers[player] = mapPlayers[player];
            RedrawPlayers(mapPlayers[player]);
            timers[player] = int.Parse(config.mainSettings.TimeToClose.ToString());
        }


        [ConsoleCommand("map_settings")]
        void cmdOpenMapSettings(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;

            if (subscribers.ContainsKey(player)) subscribers.Remove(player);
            mapPlayers[player].OnCloseMap();


            if (args.Args != null && args.Args.Length > 0 && args.Args[0] == "set")
            {
                switch (args.Args[1])
                {
                    case "friends":
                        if (data.MapPlayerData[player.userID].Friends)
                            data.MapPlayerData[player.userID].Friends = false;
                        else
                            data.MapPlayerData[player.userID].Friends = true;
                        break;
                    case "team":
                        if (data.MapPlayerData[player.userID].Teams)
                            data.MapPlayerData[player.userID].Teams = false;
                        else
                            data.MapPlayerData[player.userID].Teams = true;
                        break;
                    case "clans":
                        if (data.MapPlayerData[player.userID].Clans)
                            data.MapPlayerData[player.userID].Clans = false;
                        else
                            data.MapPlayerData[player.userID].Clans = true;
                        break;
                    case "homes":
                        if (data.MapPlayerData[player.userID].Homes)
                            data.MapPlayerData[player.userID].Homes = false;
                        else
                            data.MapPlayerData[player.userID].Homes = true;
                        break;
                    case "allplayers":
                        if (data.MapPlayerData[player.userID].AllPlayers)
                            data.MapPlayerData[player.userID].AllPlayers = false;
                        else
                            data.MapPlayerData[player.userID].AllPlayers = true;
                        break;
                    case "death":
                        if (data.MapPlayerData[player.userID].Death)
                            data.MapPlayerData[player.userID].Death = false;
                        else
                            data.MapPlayerData[player.userID].Death = true;
                        break;
                    case "raid":
                        if (data.MapPlayerData[player.userID].Raid)
                            data.MapPlayerData[player.userID].Raid = false;
                        else
                            data.MapPlayerData[player.userID].Raid = true;
                        break;
                    case "allraids":
                        if (data.MapPlayerData[player.userID].AllRaids)
                            data.MapPlayerData[player.userID].AllRaids = false;
                        else
                            data.MapPlayerData[player.userID].AllRaids = true;
                        break;
                }
            }

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {

                Name = "map_Mainsettings",
                Parent = "map_mainImage",
                Components =
                    {
                        new CuiImageComponent { Color = $"0 0 0 0.8", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax =  $"1 1" },
                        new CuiNeedsCursorComponent{ }
                    }
            });


            container.Add(new CuiElement
            {
                Parent = "map_Mainsettings",
                Components =
                            {
                                new CuiTextComponent
                                    {
                                        Color="1 1 1 1", Text = "НАСТРОЙКА ОТОБРАЖЕНИЯ НА КАРТЕ", Align = TextAnchor.MiddleCenter, FontSize = (int)TextSize(25), Font = "robotocondensed-regular.ttf"
                                    }
                                , new CuiRectTransformComponent
                                    {
                                        AnchorMin= "0 0.7", AnchorMax="1 0.9"
                                    },

                                },
            });


            container.Add(new CuiElement
            {

                Name = "map_Mainsettings.menu",
                Parent = "map_Mainsettings",
                Components =
                    {
                        new CuiImageComponent { Color = $"0 0 0 0"},
                        new CuiRectTransformComponent { AnchorMin = $"0.1 0.1", AnchorMax =  $"0.9 0.7" },
                    }
            });


            double AnchorMin = 0;
            double AnchorMax = 0.33;

            double TwoAnchorMin = 0.85;
            double TwoAnchorMax = 1;


            if (config.mainSettings.friendsSupport)
            {
                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.menu",
                    Name = "map_Mainsettings.friends",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 0"},
                        new CuiRectTransformComponent { AnchorMin = $"{AnchorMin} {TwoAnchorMin}", AnchorMax = $"{AnchorMax} {TwoAnchorMax}" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.friends",
                    Components =
                    {
                        new CuiTextComponent { Text = $"ДРУЗЬЯ", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(18)},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.friends",
                    Name = "map_Mainsettings.friends.buttons",
                    Components =
                    {
                        new CuiImageComponent {  Color = "1 1 1 0.1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.05", AnchorMax = $"0.95 0.5"},
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = data.MapPlayerData[player.userID].Friends ? "0.59 0.83 0.60 1" : "1 1 1 0.1", Command = !data.MapPlayerData[player.userID].Friends ? "map_settings set friends" : "" },
                    Text = { Text = "ВКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.49 0.98" },
                }, "map_Mainsettings.friends.buttons");

                container.Add(new CuiButton
                {
                    Button = { Color = !data.MapPlayerData[player.userID].Friends ? "0.81 0.55 0.55 1" : "1 1 1 0.1", Command = data.MapPlayerData[player.userID].Friends ? "map_settings set friends" : "" },
                    Text = { Text = "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0.51 0", AnchorMax = $"1 0.98" },
                }, "map_Mainsettings.friends.buttons");

                AnchorMin = AnchorMax;
                AnchorMax = AnchorMax + 0.33;



            }
            if (config.mainSettings.clanSupport)
            {
                container.Add(new CuiElement
                {

                    Name = "map_Mainsettings.clans",
                    Parent = "map_Mainsettings.menu",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 0"},
                        new CuiRectTransformComponent { AnchorMin = $"{AnchorMin} {TwoAnchorMin}", AnchorMax = $"{AnchorMax} {TwoAnchorMax}" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.clans",
                    Components =
                    {
                        new CuiTextComponent { Text = $"СОКЛАНОВЦЫ", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(18)},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.clans",
                    Name = "map_Mainsettings.clans.buttons",
                    Components =
                    {
                        new CuiImageComponent {  Color = "1 1 1 0.1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.05", AnchorMax = $"0.95 0.5"},
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = data.MapPlayerData[player.userID].Clans ? "0.59 0.83 0.60 1" : "1 1 1 0.1", Command = !data.MapPlayerData[player.userID].Clans ? "map_settings set clans" : "" },
                    Text = { Text = "ВКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.49 0.98" },
                }, "map_Mainsettings.clans.buttons");

                container.Add(new CuiButton
                {
                    Button = { Color = !data.MapPlayerData[player.userID].Clans ? "0.81 0.55 0.55 1" : "1 1 1 0.1", Command = data.MapPlayerData[player.userID].Clans ? "map_settings set clans" : "" },
                    Text = { Text = "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0.51 0", AnchorMax = $"1 0.98" },
                }, "map_Mainsettings.clans.buttons");

                AnchorMin = AnchorMax;
                AnchorMax = AnchorMax + 0.33;
            }

            if (config.mainSettings.TeamSupport)
            {
                container.Add(new CuiElement
                {

                    Name = "map_Mainsettings.team",
                    Parent = "map_Mainsettings.menu",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 0"},
                        new CuiRectTransformComponent { AnchorMin = $"{AnchorMin} {TwoAnchorMin}", AnchorMax = $"{AnchorMax} {TwoAnchorMax}" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.team",
                    Components =
                    {
                        new CuiTextComponent { Text = $"КОМАНДА", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(18)},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.team",
                    Name = "map_Mainsettings.team.buttons",
                    Components =
                    {
                        new CuiImageComponent {  Color = "1 1 1 0.1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.05", AnchorMax = $"0.95 0.5"},
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = data.MapPlayerData[player.userID].Teams ? "0.59 0.83 0.60 1" : "1 1 1 0.1", Command = !data.MapPlayerData[player.userID].Teams ? "map_settings set team" : "" },
                    Text = { Text = "ВКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.49 0.98" },
                }, "map_Mainsettings.team.buttons");

                container.Add(new CuiButton
                {
                    Button = { Color = !data.MapPlayerData[player.userID].Teams ? "0.81 0.55 0.55 1" : "1 1 1 0.1", Command = data.MapPlayerData[player.userID].Teams ? "map_settings set team" : "" },
                    Text = { Text = "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0.51 0", AnchorMax = $"1 0.98" },
                }, "map_Mainsettings.team.buttons");

                AnchorMin = AnchorMax;
                AnchorMax = AnchorMax + 0.33;

            }

            if (config.mainSettings.teleportSupport)
            {
                if (AnchorMin >= 0.7f)
                {
                    AnchorMin = 0;
                    AnchorMax = 0.33;
                    TwoAnchorMin = 0.65;
                    TwoAnchorMax = 0.8;
                }
               
                container.Add(new CuiElement
                {

                    Name = "map_Mainsettings.homes",
                    Parent = "map_Mainsettings.menu",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 0"},
                        new CuiRectTransformComponent {AnchorMin = $"{AnchorMin} {TwoAnchorMin}", AnchorMax = $"{AnchorMax} {TwoAnchorMax}" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.homes",
                    Components =
                    {
                        new CuiTextComponent { Text = $"ДОМА", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(18)},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.homes",
                    Name = "map_Mainsettings.homes.buttons",
                    Components =
                    {
                        new CuiImageComponent {  Color = "1 1 1 0.1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.05", AnchorMax = $"0.95 0.5"},
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = data.MapPlayerData[player.userID].Homes ? "0.59 0.83 0.60 1" : "1 1 1 0.1", Command = !data.MapPlayerData[player.userID].Homes ? "map_settings set homes" : "" },
                    Text = { Text = "ВКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.49 0.98" },
                }, "map_Mainsettings.homes.buttons");

                container.Add(new CuiButton
                {
                    Button = { Color = !data.MapPlayerData[player.userID].Homes ? "0.81 0.55 0.55 1" : "1 1 1 0.1", Command = data.MapPlayerData[player.userID].Homes ? "map_settings set homes" : "" },
                    Text = { Text = "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0.51 0", AnchorMax = $"1 0.98" },
                }, "map_Mainsettings.homes.buttons");
                AnchorMin = AnchorMax;
                AnchorMax = AnchorMax + 0.33;

            }

            if (permission.UserHasPermission(player.UserIDString, permissionName))
            {
                if (AnchorMin >= 0.7)
                {
                    AnchorMin = 0;
                    AnchorMax = 0.33;
                    TwoAnchorMin = 0.65;
                    TwoAnchorMax = 0.8;
                }
                container.Add(new CuiElement
                {

                    Name = "map_Mainsettings.death",
                    Parent = "map_Mainsettings.menu",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 0"},
                        new CuiRectTransformComponent { AnchorMin = $"{AnchorMin} {TwoAnchorMin}", AnchorMax = $"{AnchorMax} {TwoAnchorMax}" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.death",
                    Components =
                    {
                        new CuiTextComponent { Text = $"ОТОБРАЖЕНИЕ СМЕРТИ", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15)},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.death",
                    Name = "map_Mainsettings.death.buttons",
                    Components =
                    {
                        new CuiImageComponent {  Color = "1 1 1 0.1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.05", AnchorMax = $"0.95 0.5"},
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = data.MapPlayerData[player.userID].Death ? "0.59 0.83 0.60 1" : "1 1 1 0.1", Command = !data.MapPlayerData[player.userID].Death ? "map_settings set death" : "" },
                    Text = { Text = "ВКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.49 0.98" },
                }, "map_Mainsettings.death.buttons");

                container.Add(new CuiButton
                {
                    Button = { Color = !data.MapPlayerData[player.userID].Death ? "0.81 0.55 0.55 1" : "1 1 1 0.1", Command = data.MapPlayerData[player.userID].Death ? "map_settings set death" : "" },
                    Text = { Text = "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0.51 0", AnchorMax = $"1 0.98" },
                }, "map_Mainsettings.death.buttons");

                AnchorMin = AnchorMax;
                AnchorMax = AnchorMax + 0.33;
            }

            if (config.mainSettings.noescapeSupport)
            {
                if (AnchorMin >= 0.7)
                {
                    AnchorMin = 0;
                    AnchorMax = 0.33;
                    TwoAnchorMin = 0.65;
                    TwoAnchorMax = 0.8;
                }
                container.Add(new CuiElement
                {

                    Name = "map_Mainsettings.raid",
                    Parent = "map_Mainsettings.menu",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 0"},
                        new CuiRectTransformComponent { AnchorMin = $"{AnchorMin} {TwoAnchorMin}", AnchorMax = $"{AnchorMax} {TwoAnchorMax}" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.raid",
                    Components =
                    {
                        new CuiTextComponent { Text = $"РЕЙД", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(18)},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.raid",
                    Name = "map_Mainsettings.raid.buttons",
                    Components =
                    {
                        new CuiImageComponent {  Color = "1 1 1 0.1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.05", AnchorMax = $"0.95 0.5"},
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = data.MapPlayerData[player.userID].Raid ? "0.59 0.83 0.60 1" : "1 1 1 0.1", Command = !data.MapPlayerData[player.userID].Raid ? "map_settings set raid" : "" },
                    Text = { Text = "ВКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.49 0.98" },
                }, "map_Mainsettings.raid.buttons");

                container.Add(new CuiButton
                {
                    Button = { Color = !data.MapPlayerData[player.userID].Raid ? "0.81 0.55 0.55 1" : "1 1 1 0.1", Command = data.MapPlayerData[player.userID].Raid ? "map_settings set raid" : "" },
                    Text = { Text = "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0.51 0", AnchorMax = $"1 0.98" },
                }, "map_Mainsettings.raid.buttons");

                AnchorMin = AnchorMax;
                AnchorMax = AnchorMax + 0.33;
            }

            if (permission.UserHasPermission(player.UserIDString, MAP_ADMIN))
            {
                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.menu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"АДМИН РАЗДЕЛ", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(18)},
                        new CuiRectTransformComponent { AnchorMin = $"0 0.45", AnchorMax = $"1 0.6" },
                    }
                });

                AnchorMin = 0;
                AnchorMax = 0.33;


                container.Add(new CuiElement
                {

                    Name = "map_Mainsettings.allplayers",
                    Parent = "map_Mainsettings.menu",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 0"},
                        new CuiRectTransformComponent { AnchorMin = $"{AnchorMin} 0.35", AnchorMax = $"{AnchorMax} 0.5" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.allplayers",
                    Components =
                    {
                        new CuiTextComponent { Text = $"ВСЕ ИГРОКИ", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(18)},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "map_Mainsettings.allplayers",
                    Name = "map_Mainsettings.allplayers.buttons",
                    Components =
                    {
                        new CuiImageComponent {  Color = "1 1 1 0.1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.05", AnchorMax = $"0.95 0.5"},
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = data.MapPlayerData[player.userID].AllPlayers ? "0.59 0.83 0.60 1" : "1 1 1 0.1", Command = !data.MapPlayerData[player.userID].AllPlayers ? "map_settings set allplayers" : "" },
                    Text = { Text = "ВКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.49 0.98" },
                }, "map_Mainsettings.allplayers.buttons");

                container.Add(new CuiButton
                {
                    Button = { Color = !data.MapPlayerData[player.userID].AllPlayers ? "0.81 0.55 0.55 1" : "1 1 1 0.1", Command = data.MapPlayerData[player.userID].AllPlayers ? "map_settings set allplayers" : "" },
                    Text = { Text = "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                    RectTransform = { AnchorMin = $"0.51 0", AnchorMax = $"1 0.98" },
                }, "map_Mainsettings.allplayers.buttons");

                AnchorMin = AnchorMax;
                AnchorMax = AnchorMax + 0.33;

                if (config.mainSettings.noescapeSupportAdmin)
                {
                    container.Add(new CuiElement
                    {

                        Name = "map_Mainsettings.allraids",
                        Parent = "map_Mainsettings.menu",
                        Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 0"},
                        new CuiRectTransformComponent { AnchorMin = $"{AnchorMin} 0.35", AnchorMax = $"{AnchorMax} 0.5" },
                    }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = "map_Mainsettings.allraids",
                        Components =
                    {
                        new CuiTextComponent { Text = $"ВСЕ РЕЙДЫ", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(18)},
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = "map_Mainsettings.allraids",
                        Name = "map_Mainsettings.allraids.buttons",
                        Components =
                    {
                        new CuiImageComponent {  Color = "1 1 1 0.1"},
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.05", AnchorMax = $"0.95 0.5"},
                    }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Color = data.MapPlayerData[player.userID].AllRaids ? "0.59 0.83 0.60 1" : "1 1 1 0.1", Command = !data.MapPlayerData[player.userID].AllRaids ? "map_settings set allraids" : "" },
                        Text = { Text = "ВКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                        RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.49 0.98" },
                    }, "map_Mainsettings.allraids.buttons");

                    container.Add(new CuiButton
                    {
                        Button = { Color = !data.MapPlayerData[player.userID].AllRaids ? "0.81 0.55 0.55 1" : "1 1 1 0.1", Command = data.MapPlayerData[player.userID].AllRaids ? "map_settings set allraids" : "" },
                        Text = { Text = "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15) },
                        RectTransform = { AnchorMin = $"0.51 0", AnchorMax = $"1 0.98" },
                    }, "map_Mainsettings.allraids.buttons");
                }
            }

            container.Add(new CuiButton
            {
                Button = { Color = "0.59 0.83 0.60 1", Command = "map.open" },
                Text = { Text = "ОТКРЫТЬ КАРТУ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15), Color = "1 1 1 1" },
                RectTransform = { AnchorMin = $"0.15 0.05", AnchorMax = $"0.49 0.13" },
            }, "map_Mainsettings.menu");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0.2", Close = "map_Mainsettings" },
                Text = { Text = "ЗАКРЫТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(15), Color = "1 1 1 1" },
                RectTransform = { AnchorMin = $"0.51 0.05", AnchorMax = $"0.85 0.13" },
            }, "map_Mainsettings.menu");

            CuiHelper.DestroyUi(player, "map_Mainsettings");
            CuiHelper.AddUi(player, container);
        }

        double TextSize(int size)
            => Math.Round(size * 2 * config.mainSettings.mapSize);

        private List<ulong> GetTeamMembers(BasePlayer player)
        {
            if (player.currentTeam == 0) return null;
            var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
            if (team == null) return null;
            return team.members.Where(p => p.ToString() != player.UserIDString).ToList();
        }

        private Dictionary<ulong, string> playerDic = new Dictionary<ulong, string>();

        string permissionName = "rustmap.death";

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName)) return null;
            if (playerDic.ContainsKey(player.userID))
            {
                playerDic.Remove(player.userID);
            }
            playerDic.Add(player.userID, player.transform.position.ToString());
            return null;
        }

        List<Vector3> GetRaidZones(ulong player)
            => (List<Vector3>)NoEscape?.Call("ApiGetOwnerRaidZones", player);

        List<Vector3> AdminGetRaidZones()
          => (List<Vector3>)NoEscape?.Call("ApiGetAllRaidZones");

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (subscribers.ContainsKey(player)) subscribers.Remove(player);
            if (mapPlayers.ContainsKey(player)) mapPlayers.Remove(player);
            if (timers.ContainsKey(player)) timers.Remove(player);
            foreach (var sub in mapPlayers)
            {
                var toRemove = sub.Value.clanTeam.Where(p => p.player == player).ToList();
                foreach (var teammate in toRemove)
                {
                    CuiHelper.DestroyUi(sub.Key, teammate.name + teammate.counter);
                    CuiHelper.DestroyUi(sub.Key, teammate.name + teammate.counter + "text");
                }
                toRemove.ForEach(p => sub.Value.clanTeam.Remove(p));
            }
        }

        void RedrawPlayers(MapPlayer mapPlayer)
        {
            var player = mapPlayer.player;
            if (mapPlayer.clanTeam != null)
            {
                foreach (var tmMapPlayer in mapPlayer.clanTeam)
                    DrawMapPlayer(player, tmMapPlayer, true);
            }
            DrawMapPlayer(player, mapPlayer);
        }

        void CloseMap(BasePlayer player)
        {
            if (player == null) return;
            if (timers.ContainsKey(player)) timers.Remove(player);
            subscribers.Remove(player);
            mapPlayers[player].OnCloseMap();
            CuiHelper.DestroyUi(player, "map_mainPanel");
        }
        void DrawMapPlayer(BasePlayer player, MapPlayer mp, bool friend = false)
        {
            if (mp.NeedRedraw())
            {
                if (!friend && config.mainSettings.playerCoordinates)
                {
                    CuiHelper.DestroyUi(player, "map_coordinates");
                    var curX = ((float)Math.Round(mp.transform.position.x, 1)).ToString();
                    var curZ = ((float)Math.Round(mp.transform.position.z, 1)).ToString();

                    var text = $"<size={(int)TextSize(18)}><color=#EF015A>X:</color> {curX} <color=#EF015A>Z:</color> {curZ} | КВАДРАТ <color=#EF015A>{GetGrid(player.transform.position)}</color></size>";
                    CuiHelper.AddUi(player, Format(mapCoordsTextJson, text));
                }
                var pos = mp.player.transform.position;
                var anchors = ToAnchors(pos, 0.03f);
                var png = !friend ? PlayerPng(mp.Rotation) : FriendPng(mp.Rotation);
                if (png == null)
                    png = !friend ? PlayerPng(mp.Rotation - 2) : FriendPng(mp.Rotation - 2);
                if (!InMap(pos))
                {
                    CuiHelper.DestroyUi(player, "mapPlayer" + mp.player.userID + (mp.counter));
                    CuiHelper.DestroyUi(player, "mapPlayer" + mp.player.userID + (mp.counter) + "text");
                    return;
                }
                DrawIcon(player, "mapPlayer" + mp.player.userID + (mp.counter), "mapPlayer" + mp.player.userID + (++mp.counter), anchors, png, mp.alpha, mp.player.displayName, 12, true, mp.player.userID);
            }
        }

        private void AddTemporaryURLMarker(string iconURL, string iconName, Vector3 pos, string text = null, float size = 0)
        {
            if (string.IsNullOrEmpty(iconURL) || string.IsNullOrEmpty(iconName) || pos == null) return;
            ApiAddPointUrl(iconURL, iconName, pos, text);
        }

        private void RemoveTemporaryMarkerByName(string iconName)
        {
            if (!CustomIcons.ContainsKey(iconName)) return;
            ApiRemovePointUrl(iconName);
        }

        [ChatCommand("addPoint")]
        void cmdAddPointMap(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, MAP_ADMIN)) return;

            if (args.Length < 3)
            {
                SendReply(player, "Вы не верно ввели команду, используйте:\n<color=#ffe100>/addpoint</color> IconURL IconKey MapText");
                return;
            }

            if (CustomIcons.ContainsKey(args[1]))
            {
                SendReply(player, "Иконка <color=#ffe100>{0}</color> уже существует, используйте другое имя", args[1]);
                return;
            }

            var fullNameString = string.Join(" ", args).Substring(args[0].Length + args[1].Length + 1).Trim();

            AddTemporaryURLMarker(args[0], args[1], player.transform.position, fullNameString);
            SendReply(player, "иконка <color=#ffe100>{0} ({1})</color> на карте успешно добавлена. Чтобы удалить:\n<color=#ffe100>/removepoint</color> {0}", args[1], fullNameString);
        }

        [ChatCommand("addText")]
        void cmdAddTextMap(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, MAP_ADMIN)) return;

            if (args.Length < 2)
            {
                SendReply(player, "Вы не верно ввели команду, используйте:\n<color=#ffe100>/addtext</color> IconKey Размер-Текста Цвет(#HEX) ТЕКСТ\nПример: /addtext key 12 #FFFFF ТЕКСТ НА КАРТЕ");
                return;
            }

            if (CustomText.ContainsKey(args[0]))
            {
                SendReply(player, "Такой текст <color=#ffe100>{0}</color> уже существует, используйте другое имя", args[0]);
                return;
            }

            int size = 9;
            if (args.Length > 2 && !int.TryParse(args[1], out size))
            {
                SendReply(player, "Вы не верно указали размер текста, используйте число от 9 до 20");
                return;
            }
            var color = args[2].StartsWith("#") ? args[2] : null;
            int skipCount = color != null ? color.Length + 3 : 1;
            var fullNameString = string.Join(" ", args).Substring(args[0].Length + args[1].Length + skipCount).Trim();
            ApiAddPointText(args[0], player.transform.position, fullNameString, size, color);
            SendReply(player, "Текст <color=#ffe100>{0} ({1})</color> на карте успешно добавлена. Чтобы удалить:\n<color=#ffe100>/removetext</color> {0}", args[0], fullNameString);
        }

        [ChatCommand("removeText")]
        void cmdRemoveTextMap(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, MAP_ADMIN)) return;
            if (args.Length == 0)
            {
                SendReply(player, "Данного ключа в списке текста на карте не существует, проверте правильность ввода");
                return;
            }
            if (!CustomText.ContainsKey(args[0]))
            {
                SendReply(player, "Вы не верно ввели команду, используйте:\n<color=#ffe100>/removeText</color> IconKey чтобы удалить текстовую отметку на карте");
                return;
            }
            ApiRemovePointText(args[0]);
            SendReply(player, "Текст <color=#ffe100>{0}</color> успешно удален", args[0]);
        }

        [ChatCommand("removePoint")]
        void cmdRemovePointMap(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, MAP_ADMIN) ) return;
            ApiRemovePointUrl(args[0]);
            SendReply(player, "Иконка <color=#ffe100>{0}</color> успешно удалена", args[0]);
        }

        static Dictionary<string, CustomMapIcons> CustomIcons = new Dictionary<string, CustomMapIcons>();

        static Dictionary<string, CustomMapTexts> CustomText = new Dictionary<string, CustomMapTexts>();

        public class CustomMapIcons
        {
            public string URL;
            public string PNG;
            public string NAME;
            public float Size;
            public Vector3 position;
        }
        public class CustomMapTexts
        {
            public string NAME;
            public int Size;
            public string Color;
            public Vector3 position;
        }

        void ApiAddPointUrl(string url, string iconName, Vector3 pos, string title, float size = 0)
        {
            if (string.IsNullOrEmpty(url) || !url.EndsWith(".jpg") && !url.EndsWith(".png") || string.IsNullOrEmpty(iconName) || pos == null) return;
            CustomIcons.Add(iconName, new CustomMapIcons()
            {
                NAME = title,
                position = pos,
                URL = url,
                Size = size,
            });
            ServerMgr.Instance.StartCoroutine(LoadCustomImage(CustomIcons[iconName], iconName));
        }

        void ApiAddPointText(string iconName, Vector3 pos, string title, int size = 0, string color = null)
        {
            if (string.IsNullOrEmpty(iconName) || string.IsNullOrEmpty(title) || pos == null) return;
            CustomText.Add(iconName, new CustomMapTexts()
            {
                NAME = title,
                Size = size,
                position = pos,
                Color = color
            });
        }


        IEnumerator LoadCustomImage(CustomMapIcons icon, string name)
        {
            yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(name, icon.URL));
            if (m_FileManager.GetPng(name) != null)
            {
                icon.PNG = m_FileManager.GetPng(name);
                yield break;
            }
            yield return LoadCustomImage(icon, name);
        }

        bool ApiRemovePointUrl(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return false;
            if (CustomIcons.ContainsKey(iconName))
            {
                CustomIcons.Remove(iconName);
                return true;
            }
            return false;
        }
        bool ApiRemovePointText(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return false;
            if (CustomText.ContainsKey(iconName))
            {
                CustomText.Remove(iconName);
                return true;
            }
            return false;
        }

        void AddTemporaryMarker(string png, bool rotSupport, float size, float alpha, Transform transform, string name = "")
        {
            var mm = MapMarker.Create(transform);
            mm.name = string.IsNullOrEmpty(name) ? transform.GetInstanceID().ToString() : name;
            mm.png = png;
            mm.rotSupport = rotSupport;
            mm.size = size;
            mm.alpha = alpha;
            mm.text = name;
            mm.fontsize = 12;
            mm.position = transform.position;
            temporaryMarkers.Add(mm);
            foreach (var sub in subscribers) DrawMapMarker(sub.Key, mm);
        }

        void RemoveTemporaryMarker(MapMarker mm)
        {
            temporaryMarkers.Remove(mm);
            foreach (var sub in subscribers) CuiHelper.DestroyUi(sub.Key, mm.transform.GetInstanceID().ToString() + mm.counter);
        }

        void DrawIcon(BasePlayer player, string lastName, string name, string[] anchors, string png, float alpha, string text = null, int fontsize = 12, bool destroy = true, ulong SteamID = 0)
        {
            if (destroy)
            {
                timer.Once(0.05f, () =>
                {
                    CuiHelper.DestroyUi(player, lastName);
                }
                );
                CuiHelper.DestroyUi(player, lastName + "text");
            }
            var color = $"1 1 1 {alpha}";
            var image = images.FirstOrDefault(x => x.Value == png).Key;
            if (image.Contains("newheli") || image.Contains("heli"))
            {
                color = "1 1 1 1";
                if (image.Contains("newhelicreate")) color = $"1 1 1 {alpha}";
            }

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = name,
                Parent = "map_mainImage",
                Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = png,  },
                        new CuiRectTransformComponent { AnchorMin = anchors[0], AnchorMax =  anchors[1] },
                    }
            });

            if (name.Contains("mapPlayer") && permission.UserHasPermission(player.UserIDString, MAP_ADMIN) /*&& player.userID != SteamID*/)
                container.Add(new CuiElement
                {
                    Parent = name,
                    Components =
                    {
                        new CuiButtonComponent { Color = "1 1 1 0", Command = $"map.menu player ui {name} {SteamID}",  },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax =  "1 1" },
                    }
                });


            if (!string.IsNullOrEmpty(text))
            {
                if (text != player.displayName)
                    container.Add(new CuiElement
                    {
                        Name = name + "text",
                        Parent = name,
                        Components =
                    {
                        new CuiTextComponent { Color = color, Text = text.Replace("\"", ""), FontSize = (int)TextSize(fontsize), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "-3 1", AnchorMax =  "4 1.8" },
                    }
                    });
            }
            CuiHelper.AddUi(player, container);
        }


        void DrawButtonIcon(BasePlayer player, string lastName, string name, string[] anchors, string png, float alpha, string command, string text = null, string color = "1 1 1 1", int fontsize = 12, bool destroy = true)
        {
            if (destroy)
            {
                timer.Once(0.05f, () =>
                {
                    CuiHelper.DestroyUi(player, lastName);
                }
                );
                CuiHelper.DestroyUi(player, lastName + "text");
            }

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement()
            {
                Name = name,
                Parent = "map_mainImage",
                Components = {
                        new CuiRawImageComponent() {
                               Color=$"1 1 1 {alpha}", Png = png,
                        }
                        , new CuiRectTransformComponent() {
                            AnchorMin= anchors[0], AnchorMax= anchors[1]
                        }
                    }
            }
            );




            if (!string.IsNullOrEmpty(text))
            {
                container.Add(new CuiElement
                {
                    Parent = name,
                    Components =
                        {
                            new CuiTextComponent { Color = color, Text = text, FontSize = (int)TextSize(fontsize), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                            new CuiRectTransformComponent { AnchorMin = "-3 1", AnchorMax =  "4 1.6"},
                        }
                });
            }

            container.Add(new CuiElement()
            {
                Parent = name,
                Components = {
                        new CuiButtonComponent() {
                               Color=$"1 1 1 0", Command = command
                        }
                        , new CuiRectTransformComponent() {
                            AnchorMin="0 0", AnchorMax="1 1"
                        }
                    }
            });
            CuiHelper.AddUi(player, container);

        }

        void DrawIconNull(BasePlayer player, string lastName, string name, string[] anchors, string png, float alpha, string text = null, string color = null, int fontsize = 12, bool destroy = true, int pos1 = 2, int pos2 = 3)
        {
            if (destroy)
            {
                timer.Once(0.05f, () =>
                {
                    CuiHelper.DestroyUi(player, lastName);
                }
                );
                CuiHelper.DestroyUi(player, lastName + "text");
            }
            if (!string.IsNullOrEmpty(png)) CuiHelper.AddUi(player, Format(mapIconJson, name, anchors[0], anchors[1], png, alpha));


            if (!string.IsNullOrEmpty(text))
            {
                var textcolor = color ?? "#FFFFFF";
                text = text.Replace("\"", "");
                CuiHelper.AddUi(player, Format(mapIconTextJsonIcon, name + "text", anchors[pos1], anchors[pos2], $"<color={textcolor}>{text}</color>", (int)TextSize(fontsize)));
            }
        }

        void DrawDeath(BasePlayer player, string lastName, string name, string[] anchors, string png, float alpha, string text = null, int fontsize = 12, bool destroy = true)
        {
            if (destroy)
            {
                timer.Once(0.05f, () =>
                {
                    CuiHelper.DestroyUi(player, lastName);
                }
                );
                CuiHelper.DestroyUi(player, lastName + "text");
            }
            CuiHelper.AddUi(player, Format(mapIconJson, name, anchors[0], anchors[1], png, alpha));
            if (!string.IsNullOrEmpty(text)) CuiHelper.AddUi(player, Format(mapIconTextJsonIcon, name + "text", anchors[2], anchors[3], text.Replace("\"", ""), (int)TextSize(fontsize)));
        }

        void DrawMapMarker(BasePlayer player, MapMarker mm)
        {
            if (mm.transform == null) return;
            var pos = mm.transform.position;
            var anchors = ToAnchors(pos, mm.size);

            var png = mm.png;
            if (mm.rotSupport) png += GetRotation(mm.transform.rotation.eulerAngles.y);
            if (!images.ContainsKey(png))
            {
                PrintError("Png Not contains in data: " + png);
                return;
            }

            if (images[png] == null)
            {
                PrintError("PNG = NULL: " + png);
                return;
            }
            if (mm.inMap && !InMap(pos))
            {
                CuiHelper.DestroyUi(player, mm.name + (mm.counter - 1) + "text");
                CuiHelper.DestroyUi(player, mm.name + (mm.counter - 1));
                mm.inMap = false;
                return;
            }
            mm.inMap = InMap(pos);
            if (!mm.inMap)
            {
                return;
            }
            DrawIcon(player, mm.name + (mm.counter - 1), mm.name + (mm.counter), anchors, images[png], mm.alpha, mm.text);
        }
        bool InMap(Vector3 pos)
        {
            float halfSize = (int)TerrainMeta.Size.x * 0.5f;
            return pos.x < halfSize && pos.x > -halfSize && pos.z < halfSize && pos.z > -halfSize;
        }
        List<ulong> bannedCache = new List<ulong>();

        void FindStaticMarkers()
        {
            if (!this.config.mainSettings.monuments)
            {
                monumentsJson = "";
                return;
            }
            var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();
            var container = new CuiElementContainer();
            foreach (var monument in monuments)
            {
                var anchors = ToAnchors(monument.transform.position, 0.03f);
                string png;
                string text = null;
                if (monument.Type == MonumentType.Cave && config.mainSettings.caves) png = "cave";
                else if (monument.name.Contains("lighthouse"))
                {
                    png = "lighthouse";
                    text = "Маяк";
                }
                else if (monument.name.Contains("powerplant_1"))
                {
                    png = "powerplant";
                    text = "Ядерная станция";
                }
                else if (monument.name.Contains("military_tunnel_1"))
                {
                    png = "militarytunnel";
                    text = "Туннель";
                }
                else if (monument.name.Contains("airfield_1"))
                {
                    png = "airfield";
                    text = "Аэропорт";
                }
                else if (monument.name.Contains("trainyard_1"))
                {
                    png = "trainyard";
                    text = "Депо";
                }
                else if (monument.name.Contains("entrance"))
                {
                    png = "metro";
                    text = "";
                }
                else if (monument.name.Contains("junkyard_1"))
                {
                    png = "dump";
                    text = "Свалка";
                }
                else if (monument.name.Contains("compound"))
                {
                    png = "avanpost";
                    text = "Аванпост";
                }
                else if (monument.name.Contains("water_treatment_plant_1"))
                {
                    png = "watertreatment";
                    text = "Водонапорка";
                }
                else if (monument.name.Contains("water_well") && config.mainSettings.water)
                {
                    png = "water";
                }
                else if (monument.name.Contains("warehouse"))
                {
                    png = "warehouse";
                    text = "Склад";
                }
                else if (monument.name.Contains("satellite_dish"))
                {
                    png = "satellitedish";
                    text = "Антены";
                }
                else if (monument.name.Contains("excavator"))
                {
                    png = "excavator";
                    text = "Экскаватор";
                }
                else if (monument.name.ToLower().Contains("oilrig"))
                {
                    png = "oilrig";
                    text = "Нефтевышка";
                }
                else if (monument.name.Contains("gas_station"))
                {
                    png = "gasstation";
                    text = "Заправка";
                }
                else if (monument.name.Contains("supermarket"))
                {
                    png = "supermarket";
                    text = "Супермаркет";
                }
                else if (monument.name.Contains("sphere_tank"))
                {
                    png = "spheretank";
                    text = "Сфера";
                }
                else if (monument.name.Contains("harbor"))
                {
                    png = "harbor";
                    text = "Порт";
                }
                else if (monument.name.Contains("bandit_town"))
                {
                    png = "bandit";
                    text = "Бандитский город";
                }
                else if (monument.name.Contains("radtown_small_3"))
                {
                    png = "radtown";
                    text = "Редтаун";
                }
                else if (monument.name.Contains("power_sub") && config.mainSettings.powersub) png = "powersub";
                //else if (monument.name.Contains("swamp") && config.mainSettings.swamp) png = "swamp";
                else if (monument.name.Contains("mining_quarry_a"))
                {
                    png = "cquarry";
                    text = "Cерный карьер";
                }
                else if (monument.name.Contains("mining_quarry_b"))
                {
                    png = "cquarry";
                    text = "Каменный Карьер";
                }
                else if (monument.name.Contains("mining_quarry_c"))
                {
                    png = "cquarry";
                    text = "МВК Карьер";
                }
                else if (monument.name.Contains("launch_site_1"))
                {
                    png = "launchsite";
                    text = "Аэродром";
                }
                else
                {
                    Puts($"MAP IGNORE: {monument.name} Position: {monument.transform.position}");
                    continue;
                }
                
                container.Add(new CuiElement()
                {
                    Name = $"icon{text}",
                    Parent = "map_mainImage",
                    Components = {
                        new CuiRawImageComponent() {
                             Png=images[png], Color=$"1 1 1 1"
                        }
                        , new CuiRectTransformComponent() {
                            AnchorMin=anchors[0], AnchorMax=anchors[1]
                        }
                    }
                }
                );

                if (!string.IsNullOrEmpty(text) && config.mainSettings.monumentIconNames)
                {
                    container.Add(new CuiElement()
                    {
                        Parent = $"icon{text}",
                        Components = {
                            new CuiTextComponent() {
                                Text=text, FontSize=(int)TextSize(12), Align=TextAnchor.MiddleCenter,  Font = "RobotoCondensed-Regular.ttf",
                            }
                            , new CuiOutlineComponent() {
                                Color="0 0 0 0.5"
                            }
                            , new CuiRectTransformComponent() {
                                AnchorMin="-2 0.9", AnchorMax="3 2"
                            }
                        }
                    }
                    );
                }
                container.Add(new CuiElement()
                {
                    Parent = $"icon{text}",
                    Components = {
                        new CuiButtonComponent() {
                               Color=$"1 1 1 0", Command = $"map.menu monument {monument.transform.position}"
                        }
                        , new CuiRectTransformComponent() {
                            AnchorMin="0 0", AnchorMax="1 1"
                        }
                    }
                }
              );


               
            }
            monumentsJson = container.ToJson();
        }

        [ConsoleCommand("map.menu")]
        void CmdMapMenu(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, MAP_ADMIN)) return;

            if (args.Args == null) return;

            switch (args.Args[0])
            {
                case "monument":
                    var stringPos = args.FullString.Replace("monument ", "").Replace(",", "").Replace("(", "").Replace(")", "").Split();
                    float x = float.Parse(stringPos[0]);
                    float y = float.Parse(stringPos[1]);
                    float z = float.Parse(stringPos[2]);
                    var pos = GetGroundPosition(new Vector3(x, y, z));
                    player.Teleport(pos);
                    break;
                case "player":

                    switch (args.Args[1])
                    {
                        case "tp2me":
                            var target = BasePlayer.Find(args.Args[2]);
                            if (target == null) return;
                            target.Teleport(player.transform.position);
                            break;
                        case "tp2":
                            target = BasePlayer.Find(args.Args[2]);
                            if (target == null) return;
                            player.Teleport(target.transform.position);
                            break;
                        case "kick":
                            target = BasePlayer.Find(args.Args[2]);
                            if (target == null) return;
                            target.Kick("Kicked by admin");
                            break;
                        case "ban":
                            target = BasePlayer.Find(args.Args[2]);
                            if (target == null) return;
                            Server.Command("ban", target.userID, "Banned by admin");
                            break;
                        case "ui":
                            string parent = args.Args[2];
                            string SteamID = args.Args[3];
                            CuiElementContainer container = new CuiElementContainer();

                            container.Add(new CuiElement
                            {
                                Name = "map_player_menu",
                                Parent = parent,
                                Components =
                                {
                                    new CuiImageComponent { Color = "0 0 0 0.9"  },
                                    new CuiRectTransformComponent { AnchorMin = "-2 1.5", AnchorMax =  "3 6.5" },
                                }
                            });

                            container.Add(new CuiButton
                            {
                                Button = { Color = "0.41 0.54 0.67 0.9", Command = $"map.menu player tp2 {SteamID}" },
                                Text = { Text = "ТЕЛЕПОРТИРОВАТЬСЯ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(8) },
                                RectTransform = { AnchorMin = $"0 0.75", AnchorMax = $"1 0.99" },
                            }, "map_player_menu");

                            container.Add(new CuiButton
                            {
                                Button = { Color = "0.31 0.44 0.56 0.9", Command = $"map.menu player tp2me {SteamID}" },
                                Text = { Text = "ТЕЛЕПОРТИРОВАТЬ К СЕБЕ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(7) },
                                RectTransform = { AnchorMin = $"0 0.49", AnchorMax = $"1 0.74" },
                            }, "map_player_menu");

                            container.Add(new CuiButton
                            {
                                Button = { Color = "0.25 0.35 0.45 0.9", Command = $"map.menu player kick {SteamID}" },
                                Text = { Text = "КИКНУТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(8) },
                                RectTransform = { AnchorMin = $"0 0.23", AnchorMax = $"1 0.48" },
                            }, "map_player_menu");

                            container.Add(new CuiButton
                            {
                                Button = { Color = "0.21 0.29 0.37 0.9", Command = $"map.menu player ban {SteamID}" },
                                Text = { Text = "ЗАБЛОКИРОВАТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(8) },
                                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.23" },
                            }, "map_player_menu");


                            container.Add(new CuiButton
                            {
                                Button = { Color = "0.95 0.40 0.47 1.00", Close = $"map_player_menu" },
                                Text = { Text = "ЗАКРЫТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = (int)TextSize(11) },
                                RectTransform = { AnchorMin = $"0 -0.15", AnchorMax = $"1 0" },
                            }, "map_player_menu");

                            CuiHelper.DestroyUi(player, "map_player_menu");
                            CuiHelper.AddUi(player, container);
                            break;
                    }
                    break;
            }
        }

        static Vector3 GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[] {
                "Terrain", "World", "Default", "Construction", "Deployed"
            }
            )) && !hit.collider.name.Contains("rock_cliff")) y = Mathf.Max(hit.point.y, y);

            return new Vector3(pos.x, y, pos.z);
        }


        Dictionary<string, Vector3> GetHomes(BasePlayer player)
        {
            var a1 = (Dictionary<string, Vector3>)NTeleportation?.Call("GetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a2 = (Dictionary<string, Vector3>)Teleport?.Call("ApiGetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a3 = (Dictionary<string, Vector3>)Teleportation?.Call("GetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a4 = (Dictionary<string, Vector3>)HomesGUI?.Call("GetPlayerHomes", player.UserIDString) ?? new Dictionary<string, Vector3>();
            var a5 = (Dictionary<string, Vector3>)SleepingbagHome?.Call("GetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            return a1.Concat(a2).Concat(a3).Concat(a4).Concat(a5).GroupBy(p => p.Key).ToDictionary(p => p.Key, p => p.First().Value);
        }

        string[] ToAnchors(Vector3 position, float size)
        {
            Vector2 center = ToScreenCoords(position);
            size *= 0.5f;
            return new[] {
                $"{center.x - size} {center.y - size}", $"{center.x + size} {center.y + size}", $"{center.x - 0.1} {center.y - size - 0.04f}", $"{center.x + 0.1} {center.y - size + 0.02}"
            }
            ;
        }

        Vector2 ToScreenCoords(Vector3 vec)
        {
            Vector3 pos = vec;
            if (pos.x > ((int)ConVar.Server.worldsize / 2))
                pos.x = (int)ConVar.Server.worldsize / 2;
            if (pos.z > ((int)ConVar.Server.worldsize / 2))
                pos.z = (int)ConVar.Server.worldsize / 2;
            if (pos.x < -((int)ConVar.Server.worldsize / 2))
                pos.x = -(int)ConVar.Server.worldsize / 2;
            if (pos.z < -((int)ConVar.Server.worldsize / 2))
                pos.z = -(int)ConVar.Server.worldsize / 2;
            return new Vector2((pos.x + (int)ConVar.Server.worldsize * 0.5f) / (int)ConVar.Server.worldsize, (pos.z + (int)ConVar.Server.worldsize * 0.5f) / (int)ConVar.Server.worldsize);
        }

        static int GetRotation(float angle)
        {
            if (angle > 348.75f && angle < 11.25f) return 16;
            if (angle > 11.25f && angle < 33.75f) return 1;
            if (angle > 33.75f && angle < 56.25f) return 2;
            if (angle > 56.25f && angle < 78.75f) return 3;
            if (angle > 78.75f && angle < 101.25f) return 4;
            if (angle > 101.25f && angle < 123.75f) return 5;
            if (angle > 123.75f && angle < 146.25F) return 6;
            if (angle > 146.25F && angle < 168.75D) return 7;
            if (angle > 168.75F && angle < 191.25D) return 8;
            if (angle > 191.25F && angle < 213.4D) return 9;
            if (angle > 213.75F && angle < 236.25D) return 10;
            if (angle > 236.25F && angle < 258.75D) return 11;
            if (angle > 258.75D && angle < 281.25D) return 12;
            if (angle > 281.25D && angle < 303.75D) return 13;
            if (angle > 303.75D && angle < 326.25D) return 14;
            if (angle > 326.25D && angle < 348.75D) return 15;
            return 16;
        }

        private void DisableMaps(BasePlayer player)
        {
            if (subscribers.Keys.Contains(player))
                CloseMap(player);
        }

        void DownloadMapImage()
        {
            DownloadMapImages();
            if (images.ContainsKey(MapFilename))
                LoadImages();
        }

        void DownloadMapImages()
        {
            PrintWarning($"Начало загрузки все изображений RustMap. Ожидайте завершения");
            if (string.IsNullOrEmpty(config.mainSettings.mapUrl))
            {
                var image = GenerateImages();
                if (!images.ContainsKey(MapFilename))
                    images.Add(MapFilename, image);
                else
                    images[MapFilename] = image;
            }
            else
            {
                images[MapFilename] = config.mainSettings.mapUrl;
            }
            if (images.ContainsKey(MapFilename))
            {
                CommunityEntity.ServerInstance.StartCoroutine(LoadImages());
                return;
            }
            PrintError("Ошибка, попытка повторной генерации карты!");
            DownloadMapImages();
        }

        IEnumerator LoadImages()
        {
            int i = 0;
            int lastpercent = -1;
            foreach (var name in imagesKeys)
            {
                yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(name, images[name]));
                if (m_FileManager.GetPng(name) == null)
                    yield return new WaitForSeconds(3);
                images[name] = m_FileManager.GetPng(name);
                int percent = (int)(i / (float)imagesKeys.Count * 100);
                if (percent % 20 == 0 && percent != lastpercent)
                {
                    Puts($"Идёт загрузка всех изображений, загружено: {percent}%");
                    lastpercent = percent;
                }
                i++;
            }


            int custom = 0;
            foreach (var name in CustomIcons)
            {
                yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(name.Key, name.Value.URL));
                name.Value.PNG = m_FileManager.GetPng(name.Key);
                int percent = (int)(custom / (float)CustomIcons.Count * 100);
                if (percent % 20 == 0 && percent != lastpercent)
                {
                    Puts($"Идёт загрузка кастомных изображений, загружено: {percent}%");
                    lastpercent = percent;
                }
                custom++;
            }
            FindStaticMarkers();
            init = true;
            m_FileManager.SaveData();
            PrintWarning("Все изображения карты ({0}), успешно загружены", i + custom);
            Interface.CallHook("OnMapInitialized");
        }

        string MapFilename = (World.CanLoadFromUrl() ? World.Name : $"{ConVar.Server.level}_{World.Seed}") + $"_{TerrainMeta.Size.x}";

        string Format(string value, params object[] args)
        {
            var reply = 0;
            var result = new StringBuilder(value);
            for (int i = 0;
            i < args.Length;
            i++) if (args[i] == null)
                {
                    throw new NullReferenceException();
                }
                else
                {
                    result.Replace("{" + i + "}", args[i].ToString());
                }
            return result.ToString();
        }

        List<string> imagesKeys => images.Keys.ToList();

        string PlayerPng(int rot) => images[imagesKeys[rot - 1]];

        string FriendPng(int rot) => images[imagesKeys[14 + rot]];

        string PlanePng(int rot) => images[imagesKeys[31 + rot]];

        string MapPng() => images[MapFilename];

        Dictionary<string, string> images = new Dictionary<string, string>() {
                {
                "player1", "https://rustplugin.ru/rustmap/icons/player1.png"
            }
            , {
                "player2", "https://rustplugin.ru/rustmap/icons/player2.png"
            }
            , {
                "player3", "https://rustplugin.ru/rustmap/icons/player3.png"
            }
            , {
                "player4", "https://rustplugin.ru/rustmap/icons/player4.png"
            }
            , {
                "player5", "https://rustplugin.ru/rustmap/icons/player5.png"
            }
            , {
                "player6", "https://rustplugin.ru/rustmap/icons/player6.png"
            }
            , {
                "player7", "https://rustplugin.ru/rustmap/icons/player7.png"
            }
            , {
                "player8", "https://rustplugin.ru/rustmap/icons/player8.png"
            }
            , {
                "player9", "https://rustplugin.ru/rustmap/icons/player9.png"
            }
            , {
                "player10", "https://rustplugin.ru/rustmap/icons/player10.png"
            }
            , {
                "player11", "https://rustplugin.ru/rustmap/icons/player11.png"
            }
            , {
                "player12", "https://rustplugin.ru/rustmap/icons/player12.png"
            }
            , {
                "player13", "https://rustplugin.ru/rustmap/icons/player13.png"
            }
            , {
                "player14", "https://rustplugin.ru/rustmap/icons/player14.png"
            }
            , {
                "player15", "https://rustplugin.ru/rustmap/icons/player15.png"
            }
            , {
                "player16", "https://rustplugin.ru/rustmap/icons/player16.png"
            }
            , {
                "friend1", "https://rustplugin.ru/rustmap/icons/friend1.png"
            }
            , {
                "friend2", "https://rustplugin.ru/rustmap/icons/friend2.png"
            }
            , {
                "friend3", "https://rustplugin.ru/rustmap/icons/friend3.png"
            }
            , {
                "friend4", "https://rustplugin.ru/rustmap/icons/friend4.png"
            }
            , {
                "friend5", "https://rustplugin.ru/rustmap/icons/friend5.png"
            }
            , {
                "friend6", "https://rustplugin.ru/rustmap/icons/friend6.png"
            }
            , {
                "friend7", "https://rustplugin.ru/rustmap/icons/friend7.png"
            }
            , {
                "friend8", "https://rustplugin.ru/rustmap/icons/friend8.png"
            }
            , {
                "friend9", "https://rustplugin.ru/rustmap/icons/friend9.png"
            }
            , {
                "friend10", "https://rustplugin.ru/rustmap/icons/friend10.png"
            }
            , {
                "friend11", "https://rustplugin.ru/rustmap/icons/friend11.png"
            }
            , {
                "friend12", "https://rustplugin.ru/rustmap/icons/friend12.png"
            }
            , {
                "friend13", "https://rustplugin.ru/rustmap/icons/friend13.png"
            }
            , {
                "friend14", "https://rustplugin.ru/rustmap/icons/friend14.png"
            }
            , {
                "friend15", "https://rustplugin.ru/rustmap/icons/friend15.png"
            }
            , {
                "friend16", "https://rustplugin.ru/rustmap/icons/friend16.png"
            }
            , {
                "plane1", "https://rustplugin.ru/rustmap/icons/plane1.png"
            }
            , {
                "plane2", "https://rustplugin.ru/rustmap/icons/plane2.png"
            }
            , {
                "plane3", "https://rustplugin.ru/rustmap/icons/plane3.png"
            }
            , {
                "plane4", "https://rustplugin.ru/rustmap/icons/plane4.png"
            }
            , {
                "plane5", "https://rustplugin.ru/rustmap/icons/plane5.png"
            }
            , {
                "plane6", "https://rustplugin.ru/rustmap/icons/plane6.png"
            }
            , {
                "plane7", "https://rustplugin.ru/rustmap/icons/plane7.png"
            }
            , {
                "plane8", "https://rustplugin.ru/rustmap/icons/plane8.png"
            }
            , {
                "plane9", "https://rustplugin.ru/rustmap/icons/plane9.png"
            }
            , {
                "plane10", "https://rustplugin.ru/rustmap/icons/plane10.png"
            }
            , {
                "plane11", "https://rustplugin.ru/rustmap/icons/plane11.png"
            }
            , {
                "plane12", "https://rustplugin.ru/rustmap/icons/plane12.png"
            }
            , {
                "plane13", "https://rustplugin.ru/rustmap/icons/plane13.png"
            }
            , {
                "plane14", "https://rustplugin.ru/rustmap/icons/plane14.png"
            }
            , {
                "plane15", "https://rustplugin.ru/rustmap/icons/plane15.png"
            }
            , {
                "plane16", "https://rustplugin.ru/rustmap/icons/plane16.png"
            }
            , {
                "bradley1", "https://rustplugin.ru/rustmap/icons/bradley1.png"
            }
            , {
                "bradley2", "https://rustplugin.ru/rustmap/icons/bradley2.png"
            }
            , {
                "bradley3", "https://rustplugin.ru/rustmap/icons/bradley3.png"
            }
            , {
                "bradley4", "https://rustplugin.ru/rustmap/icons/bradley4.png"
            }
            , {
                "bradley5", "https://rustplugin.ru/rustmap/icons/bradley5.png"
            }
            , {
                "bradley6", "https://rustplugin.ru/rustmap/icons/bradley6.png"
            }
            , {
                "bradley7", "https://rustplugin.ru/rustmap/icons/bradley7.png"
            }
            , {
                "bradley8", "https://rustplugin.ru/rustmap/icons/bradley8.png"
            }
            , {
                "bradley9", "https://rustplugin.ru/rustmap/icons/bradley9.png"
            }
            , {
                "bradley10", "https://rustplugin.ru/rustmap/icons/bradley10.png"
            }
            , {
                "bradley11", "https://rustplugin.ru/rustmap/icons/bradley11.png"
            }
            , {
                "bradley12", "https://rustplugin.ru/rustmap/icons/bradley12.png"
            }
            , {
                "bradley13", "https://rustplugin.ru/rustmap/icons/bradley13.png"
            }
            , {
                "bradley14", "https://rustplugin.ru/rustmap/icons/bradley14.png"
            }
            , {
                "bradley15", "https://rustplugin.ru/rustmap/icons/bradley15.png"
            }
            , {
                "bradley16", "https://rustplugin.ru/rustmap/icons/bradley16.png"
            }
            , {
                "newheli1", "https://rustplugin.ru/rustmap/icons/newheli1.png"
            }
            , {
                "newheli2", "https://rustplugin.ru/rustmap/icons/newheli2.png"
            }
            , {
                "newheli3", "https://rustplugin.ru/rustmap/icons/newheli3.png"
            }
            , {
                "newheli4", "https://rustplugin.ru/rustmap/icons/newheli4.png"
            }
            , {
                "newheli5", "https://rustplugin.ru/rustmap/icons/newheli5.png"
            }
            , {
                "newheli6", "https://rustplugin.ru/rustmap/icons/newheli6.png"
            }
            , {
                "newheli7", "https://rustplugin.ru/rustmap/icons/newheli7.png"
            }
            , {
                "newheli8", "https://rustplugin.ru/rustmap/icons/newheli8.png"
            }
            , {
                "newheli9", "https://rustplugin.ru/rustmap/icons/newheli9.png"
            }
            , {
                "newheli10", "https://rustplugin.ru/rustmap/icons/newheli10.png"
            }
            , {
                "newheli11", "https://rustplugin.ru/rustmap/icons/newheli11.png"
            }
            , {
                "newheli12", "https://rustplugin.ru/rustmap/icons/newheli12.png"
            }
            , {
                "newheli13", "https://rustplugin.ru/rustmap/icons/newheli13.png"
            }
            , {
                "newheli14", "https://rustplugin.ru/rustmap/icons/newheli14.png"
            }
            , {
                "newheli15", "https://rustplugin.ru/rustmap/icons/newheli15.png"
            }
            , {
                "newheli16", "https://rustplugin.ru/rustmap/icons/newheli16.png"
            }
            , {
                "ship1", "https://rustplugin.ru/rustmap/icons/ship1.png"
            }
            , {
                "ship2", "https://rustplugin.ru/rustmap/icons/ship2.png"
            }
            , {
                "ship3", "https://rustplugin.ru/rustmap/icons/ship3.png"
            }
            , {
                "ship4", "https://rustplugin.ru/rustmap/icons/ship4.png"
            }
            , {
                "ship5", "https://rustplugin.ru/rustmap/icons/ship5.png"
            }
            , {
                "ship6", "https://rustplugin.ru/rustmap/icons/ship6.png"
            }
            , {
                "ship7", "https://rustplugin.ru/rustmap/icons/ship7.png"
            }
            , {
                "ship8", "https://rustplugin.ru/rustmap/icons/ship8.png"
            }
            , {
                "ship9", "https://rustplugin.ru/rustmap/icons/ship9.png"
            }
            , {
                "ship10", "https://rustplugin.ru/rustmap/icons/ship10.png"
            }
            , {
                "ship11", "https://rustplugin.ru/rustmap/icons/ship11.png"
            }
            , {
                "ship12", "https://rustplugin.ru/rustmap/icons/ship12.png"
            }
            , {
                "ship13", "https://rustplugin.ru/rustmap/icons/ship13.png"
            }
            , {
                "ship14", "https://rustplugin.ru/rustmap/icons/ship14.png"
            }
            , {
                "ship15", "https://rustplugin.ru/rustmap/icons/ship15.png"
            }
            , {
                "ship16", "https://rustplugin.ru/rustmap/icons/ship16.png"
            }
            , {
                "newhelicreate", "https://i.imgur.com/0Aq4cpP.png"
            }
            , {
                "heli1", "https://rustplugin.ru/rustmap/icons/heli1.png"
            }
            , {
                "heli2", "https://rustplugin.ru/rustmap/icons/heli2.png"
            }
            , {
                "heli3", "https://rustplugin.ru/rustmap/icons/heli3.png"
            }
            , {
                "heli4", "https://rustplugin.ru/rustmap/icons/heli4.png"
            }
            , {
                "heli5", "https://rustplugin.ru/rustmap/icons/heli5.png"
            }
            , {
                "heli6", "https://rustplugin.ru/rustmap/icons/heli6.png"
            }
            , {
                "heli7", "https://rustplugin.ru/rustmap/icons/heli7.png"
            }
            , {
                "heli8", "https://rustplugin.ru/rustmap/icons/heli8.png"
            }
            , {
                "heli9", "https://rustplugin.ru/rustmap/icons/heli9.png"
            }
            , {
                "heli10", "https://rustplugin.ru/rustmap/icons/heli10.png"
            }
            , {
                "heli11", "https://rustplugin.ru/rustmap/icons/heli11.png"
            }
            , {
                "heli12", "https://rustplugin.ru/rustmap/icons/heli12.png"
            }
            , {
                "heli13", "https://rustplugin.ru/rustmap/icons/heli13.png"
            }
            , {
                "heli14", "https://rustplugin.ru/rustmap/icons/heli14.png"
            }
            , {
                "heli15", "https://rustplugin.ru/rustmap/icons/heli15.png"
            }
            , {
                "heli16", "https://rustplugin.ru/rustmap/icons/heli16.png"
            },
            
            {
                "lighthouse", "https://rustplugin.ru/rustmap/icons/lighthouse.png"
            }
            , {
                "special", "https://rustplugin.ru/rustmap/icons/special.png"
            }
            , {
                "militarytunnel", "https://rustplugin.ru/rustmap/icons/militarytunnel.png"
            }
            , {
                "airfield", "https://rustplugin.ru/rustmap/icons/airfield.png"
            }
            , {
                "trainyard", "https://rustplugin.ru/rustmap/icons/trainyard.png"
            }
            , {
                "gasstation", "https://rustplugin.ru/rustmap/icons/gasstation.png"
            }
            , {
                "supermarket", "https://rustplugin.ru/rustmap/icons/supermarket.png"
            }
            , {
                "watertreatment", "https://rustplugin.ru/rustmap/icons/watertreatment.png"
            }
            , {
                "warehouse", "https://rustplugin.ru/rustmap/icons/warehouse.png"
            }
            , {
                "satellitedish", "https://rustplugin.ru/rustmap/icons/satellitedish.png"
            }
            , {
                "spheretank", "https://rustplugin.ru/rustmap/icons/spheretank.png"
            }
            , {
                "radtown", "https://rustplugin.ru/rustmap/icons/radtown.png"
            }
            , {
                "powerplant", "https://rustplugin.ru/rustmap/icons/powerplant.png"
            }
            , {
                "harbor", "https://rustplugin.ru/rustmap/icons/harbor.png"
            }
            , {
                "powersub", "https://rustplugin.ru/rustmap/icons/powersub.png"
            }
            , {
                "cave", "https://rustplugin.ru/rustmap/icons/cave.png"
            }
            , {
                "launchsite", "https://rustplugin.ru/rustmap/icons/launchsite.png"
            }
            , {
                "RaidZones", "https://i.imgur.com/LlQ3UGi.png"
            }
            , {
                "raidhome", "https://i.imgur.com/kTnl0hQ.png"
            }
            , {
                "mapsupply", "https://rustplugin.ru/rustmap/icons/mapsupply.png"
            }
            , {
                "avanpost", "https://rustplugin.ru/rustmap/icons/avanpost.png"
            }
            , {
                "helidebris", "https://rustplugin.ru/rustmap/icons/helidebris.png"
            }
            , {
                "banned", "https://rustplugin.ru/rustmap/icons/banned.png"
            }
            , {
                "vending", "https://rustplugin.ru/rustmap/icons/vending.png"
            }
            , {
                "sethome", "https://rustplugin.ru/rustmap/icons/homes.png"
            }
            , {
                "treasurebox", "https://rustplugin.ru/rustmap/icons/treasurebox.png"
            }
            , {
                "death", "https://rustplugin.ru/rustmap/icons/death.png"
            },
            { "meteor", "https://i.imgur.com/BFCdsOx.png"},

            {"minicopter", "https://i.imgur.com/mRFQQTU.png" },

             {"rad", "https://rustplugin.ru/rustmap/icons/radhouse.png"}
            , {
                "quarry", "https://rustplugin.ru/rustmap/icons/quarry.png"
            }
            , {
                "cquarry", "https://rustplugin.ru/rustmap/icons/cquarry.png"
            }
            , {
                "dump", "https://rustplugin.ru/rustmap/icons/dump.png"
            }
            , {
                "water", "https://rustplugin.ru/rustmap/icons/water.png"
            }

            , {
                "bandit", "https://rustplugin.ru/rustmap/icons/bandit.png"
            }
            , {
                "swamp", "https://rustplugin.ru/rustmap/icons/swamp.png"
            }
            ,
             {
                "excavator", "https://i.imgur.com/LgPlD6c.png"
            },
             {
                "oilrig", "https://i.imgur.com/jQQ7b8z.png"
            },
             {
                "settings", "https://rustplugin.ru/rustmap/icons/settings.png"
            },
              {
                "metro", "https://rustplugin.ru/rustmap/icons/metro.png"
            }
        }
        ;
        static string _(string i) => !string.IsNullOrEmpty(i) ? new string(i.Select(x => (x >= 'a' && x <= 'z') ? (char)((x - 'a' + 13) % 26 + 'a') : (x >= 'A' && x <= 'Z') ? (char)((x - 'A' + 13) % 26 + 'A') : x).ToArray()) : i;

        private string GetGrid(Vector3 pos)
        {
            char letter = 'A';
            var x = Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) % 26;
            var z = (Mathf.Floor(ConVar.Server.worldsize / 146.3f) - 1) - Mathf.Floor((pos.z + (ConVar.Server.worldsize / 2)) / 146.3f);
            letter = (char)(((int)letter) + x);
            return $"{letter}{z}";
        }

        private string GetMapImage()
        {
            if (string.IsNullOrEmpty(DataMap)) return null;
            return DataMap;
        }

        #region Map Renderer
        private Dictionary<int, byte[]> Images = new Dictionary<int, byte[]>();

        public string DataMap = "";

        public byte[] ImageToByteArray(string mapFilePath)
        {
            PrintWarning($"Изображение карты найдено: {mapFilePath}, начинаем преобразование");
            var mapImage = Image.FromFile(mapFilePath);
            using (var ms = new MemoryStream())
            {
                mapImage.Save(ms, mapImage.RawFormat);
                return ms.ToArray();
            }
        }

        private string GenerateImages()
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("RustMap/Maps/CreateDirectory"))
                Interface.Oxide.DataFileSystem.WriteObject($"RustMap/Maps/CreateDirectory", new sbyte());

            string _filename = (World.CanLoadFromUrl() ? World.Name : $"{ConVar.Server.level}_{ConVar.Server.worldsize}_{ConVar.Server.seed}") + ".jpg";

            if (Interface.Oxide.DataFileSystem.GetFiles($"{Interface.Oxide.DataDirectory}/RustMap/Maps/").FirstOrDefault(p => p.Contains(_filename)) == null)
            {
                PrintWarning("Начата генерация изображения карты. Сервер может подвиснуть...");
                byte[] array = Render();
                if (array == null)
                {
                    PrintError("Ошибка загрузки изображения карты, отпишитесь разработчику https://vk.com/mr.gr1me");
                    return null;
                }
                Images[8192] = array;
                ImageConverter imageConverter = new ImageConverter();
                for (int i = 4096; i >= 1024; i /= 2)
                {
                    Bitmap original = (Bitmap)imageConverter.ConvertFrom(array);
                    Bitmap bitmap = new Bitmap(original, i, i);
                    MemoryStream memoryStream = new MemoryStream();
                    bitmap.Save(memoryStream, ImageFormat.Jpeg);
                    byte[] value = memoryStream.ToArray();
                    byte[] ImageInArray = (byte[])imageConverter.ConvertTo(bitmap, typeof(byte[]));
                    memoryStream.Dispose();
                    Images[i] = value;
                    MemoryStream ms = new MemoryStream(value);
                    Image mapimage = Image.FromStream(ms);
                    Image returnImaget = Image.FromStream(ms);
                    returnImaget.Save(DataMap, ImageFormat.Jpeg);
                }
            }
            return "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "Maps" + Path.DirectorySeparatorChar + _filename;
        }

        private TerrainTexturing _terrainTexture;
        private Terrain _terrain;
        private TerrainHeightMap _heightMap;
        private TerrainSplatMap _splatMap;

        private struct Array2D<T>
        {
            public readonly T[] Items;

            public readonly int Width;

            public readonly int Height;

            public Array2D(T[] items, int width, int height)
            {
                Items = items;
                Width = width;
                Height = height;
            }

            public Array2D(int width, int height)
            {
                Items = new T[width * height];
                Width = width;
                Height = height;
            }

            public T this[int row, int col]
            {
                get
                {
                    if (row < 0 || row > Width - 1)
                    {
                        throw new IndexOutOfRangeException($"Get row out of range at {row} Min: 0 Max: {Width - 1}");
                    }

                    if (col < 0 || col > Height - 1)
                    {
                        throw new IndexOutOfRangeException($"Get col out of range at {col} Min: 0 Max: {Height - 1}");
                    }

                    return Items[col * Width + row];
                }
                set
                {
                    if (row < 0 || row > Width - 1)
                    {
                        throw new IndexOutOfRangeException($"Set row out of range at {row} Min: 0 Max: {Width - 1}");
                    }

                    if (col < 0 || col > Height - 1)
                    {
                        throw new IndexOutOfRangeException($"Set col out of range at {col} Min: 0 Max: {Height - 1}");
                    }

                    Items[col * Width + row] = value;
                }
            }

            public bool IsEmpty()
            {
                return Items == null || Width == 0 && Height == 0;
            }

            public Array2D<T> Splice(int startX, int startY, int width, int height)
            {
                if (startX < 0 || startX >= Width)
                {
                    throw new IndexOutOfRangeException($"startX is < 0 or greater than {Width}: {startX}");
                }

                if (startY < 0 || startY >= Height)
                {
                    throw new IndexOutOfRangeException($"startY is < 0 or greater than {Height}: {startY}");
                }

                if (width == 0 || startX + width > Width)
                {
                    throw new IndexOutOfRangeException($"width is < 0 or greater than {Width}: {width}");
                }

                if (height == 0 || startY + height > Height)
                {
                    throw new IndexOutOfRangeException($"height is < 0 or greater than {Height}: {height}");
                }

                Array2D<T> splice = new Array2D<T>(width, height);
                Array2D<T> copyThis = this;
                Parallel.For(0, width, x =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        splice[x, y] = copyThis[startX + x, startY + y];
                    }
                });

                return splice;
            }

            public Array2D<T> Clone()
            {
                return new Array2D<T>((T[])Items.Clone(), Width, Height);
            }
        }

        private static readonly Vector3 BackgroudColor = new Vector3(0.324313372f, 0.397058845f, 0.195609868f);
        private static readonly Vector4 Water = new Vector4(0.24f, 0.38f, 0.49f, 0.1f);
        private static readonly Vector4 Gravel = new Vector4(0.139705867f, 0.132621378f, 0.114024632f, 0.5f);
        private static readonly Vector4 DefaultDirtColor = new Vector4(0.17f, 0.15f, 0.16f, 0.4f);
        private static readonly Vector4 DefaultSandColor = new Vector4(0.56f, 0.51f, 0.44f, 1.00f);
        private static readonly Vector4 DefaultGrassColor = new Vector4(0.61f, 0.68f, 0.34f, 0.7f);
        private static readonly Vector4 DefaultForestColor = new Vector4(0.57f, 0.47f, 0.35f, 1.00f);
        private static readonly Vector4 DefaultRockColor = new Vector4(0.22f, 0.24f, 0.11f, 0.5f);
        private static readonly Vector4 DefaultSnowColor = new Vector4(0.8088235f, 0.8088235f, 0.8088235f, 0.85f);
        private static readonly Vector4 DefaultPebbleColor = new Vector4(0.121568628f, 0.419607848f, 0.627451f, 1f);
        private static readonly Vector4 DefaultOffShoreColor = new Vector4(0.166295841f, 0.259337664f, 0.3490566f, 1f);
        private static readonly Vector3 DefaultSunDirection = Vector3.Normalize(new Vector3(0.95f, 2.87f, 2.37f));
        private static readonly Vector3 DefaultHalf = new Vector3(0.5f, 0.5f, 0.5f);
        private static readonly Vector3 StartColor = new Vector3(0.324313372f, 0.397058845f, 0.195609868f);
        private static readonly Vector3 SunDirection = Vector3.Normalize(new Vector3(0.95f, 2.87f, 2.37f));


        private static readonly Vector4 GravelColor = new Vector4(0.139705867f, 0.132621378f, 0.114024632f, 0.372f);


        private byte[] Render()
        {
            int waterOffset = 0;
            int halfWaterOffset = waterOffset / 2;

            if (_heightMap == null || _splatMap == null || _terrain == null)
                return null;

            int mapSize = (int)(World.Size / 2);
            if (mapSize <= 0)
                return null;

            int imageWidth = mapSize + waterOffset;
            int imageHeight = mapSize + waterOffset;
            int widthWithWater = mapSize + halfWaterOffset;

            float offsetMultiplier = 1f / mapSize;

            Color[] array = new Color[imageWidth * imageHeight];

            var output = new Array2D<Color>(array, imageWidth, imageHeight);

            Parallel.For(-halfWaterOffset, imageHeight - halfWaterOffset, row =>
            {
                float offsetRow = row * offsetMultiplier;
                for (int col = -halfWaterOffset; col < widthWithWater; col++)
                {
                    float offsetCol = col * offsetMultiplier;
                    float terrainHeight = GetHeight(offsetRow, offsetCol);
                    float sun = Math.Max(Vector3.Dot(GetNormal(offsetRow, offsetCol), SunDirection), 0.0f);
                    Vector3 pixel = Vector3.Lerp(StartColor, GravelColor, GetSplat(row, col, 128) * GravelColor.w);
                    pixel = Vector3.Lerp(pixel, DefaultPebbleColor, GetSplat(offsetRow, offsetCol, 64) * DefaultPebbleColor.w);
                    pixel = Vector3.Lerp(pixel, DefaultRockColor, GetSplat(offsetRow, offsetCol, 8) * DefaultRockColor.w);
                    pixel = Vector3.Lerp(pixel, DefaultDirtColor, GetSplat(offsetRow, offsetCol, 1) * DefaultDirtColor.w);
                    pixel = Vector3.Lerp(pixel, DefaultGrassColor, GetSplat(offsetRow, offsetCol, 16) * DefaultGrassColor.w);
                    pixel = Vector3.Lerp(pixel, DefaultForestColor, GetSplat(offsetRow, offsetCol, 32) * DefaultForestColor.w);
                    pixel = Vector3.Lerp(pixel, DefaultSandColor, GetSplat(offsetRow, offsetCol, 4) * DefaultSandColor.w);
                    pixel = Vector3.Lerp(pixel, DefaultSnowColor, GetSplat(offsetRow, offsetCol, 2) * DefaultSnowColor.w);
                    float waterDepth = -terrainHeight;
                    if (waterDepth > 0)
                    {
                        pixel = Vector3.Lerp(pixel, Water, Mathf.Clamp(0.5f + waterDepth / 5.0f, 0.0f, 1f));
                        pixel = Vector3.Lerp(pixel, DefaultOffShoreColor, Mathf.Clamp(waterDepth / 50f, 0.0f, 1f));
                        sun = 0.5f;
                    }

                    pixel += (sun - 0.5f) * 0.5f * pixel;
                    pixel = (pixel - Half) * 0.87f + Half;
                    pixel *= 1;

                    output[row + halfWaterOffset, col + halfWaterOffset] = new Color(pixel.x, pixel.y, pixel.z);
                }
            });

            return EncodeToJPG(imageWidth, imageHeight, array);
        }
        private static readonly Vector3 Half = new Vector3(0.5f, 0.5f, 0.5f);

        float GetHeight(float x, float y)
        {
            return _heightMap.GetHeight(x, y);
        }

        Vector3 GetNormal(float x, float y)
        {
            return _heightMap.GetNormal(x, y);
        }

        float GetSplat(float x, float y, int mask)
        {
            return _splatMap.GetSplat(x, y, mask);
        }

        private static byte[] EncodeToJPG(int width, int height, Color[] pixels)
        {
            Texture2D texture2D = null;
            byte[] result;
            try
            {
                texture2D = new Texture2D(width, height);
                texture2D.SetPixels(pixels);
                texture2D.Apply();
                result = texture2D.EncodeToJPG(85);
            }
            finally
            {
                if (texture2D != null)
                    UnityEngine.Object.Destroy(texture2D);
            }
            return result;
        }


        private Bitmap ResizeImage(byte[] bytes, int targetWidth, int targetHeight)
        {
            using (MemoryStream original = new MemoryStream())
            {
                original.Write(bytes, 0, bytes.Length);
                using (Bitmap img = new Bitmap(Image.FromStream(original)))
                {
                    return new Bitmap(img, new Size(targetWidth, targetHeight));
                }
            }
        }

        #endregion

        #region File Manager

        private GameObject FileManagerObject;
        private FileManager m_FileManager;

        void InitFileManager()
        {
            FileManagerObject = new GameObject("MAP_FileManagerObject");
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
            DataMap = $"{Interface.Oxide.DataDirectory}/RustMap/Maps/" + (World.CanLoadFromUrl() ? World.Name : $"{ConVar.Server.level}_{ConVar.Server.worldsize}_{ConVar.Server.seed}") + ".jpg";
            MapFilename = (World.CanLoadFromUrl() ? World.Name : $"{ConVar.Server.level}_{World.Seed}") + $"_{TerrainMeta.Size.x}";
            DownloadMapImage();
        }

        class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();
            DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("RustMap/Images");

            private class FileInfo
            {
                public string Url;
                public string Png;
            }

            public void SaveData()
            {
                dataFile.WriteObject(files);
            }

            public void WipeData()
            {
                Interface.Oxide.DataFileSystem.WriteObject("RustMap/Images", new sbyte());
                Interface.Oxide.ReloadPlugin(m_Instance.Title);
            }

            public string GetPng(string name)
            {
                if (!files.ContainsKey(name)) return null;
                return files[name].Png;
            }

            private void Awake()
            {
                LoadData();
            }

            void LoadData()
            {
                try
                {
                    files = dataFile.ReadObject<Dictionary<string, FileInfo>>();
                }
                catch
                {
                    files = new Dictionary<string, FileInfo>();
                }
            }

            public IEnumerator LoadFile(string name, string url)
            {
                if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png)) yield break;
                files[name] = new FileInfo()
                {
                    Url = url
                }
                ;
                needed++;
                yield return StartCoroutine(LoadImageCoroutine(name, url));
            }
            IEnumerator LoadImageCoroutine(string name, string url)
            {
                using (WWW www = new WWW(url))
                {
                    yield return www;
                    {
                        if (string.IsNullOrEmpty(www.error))
                        {
                            var entityId = CommunityEntity.ServerInstance.net.ID;
                            var crc32 = FileStorage.server.Store(www.bytes, FileStorage.Type.png, entityId).ToString();
                            files[name].Png = crc32;
                        }
                    }
                }
                loaded++;
            }
        }
        #endregion
    }
}