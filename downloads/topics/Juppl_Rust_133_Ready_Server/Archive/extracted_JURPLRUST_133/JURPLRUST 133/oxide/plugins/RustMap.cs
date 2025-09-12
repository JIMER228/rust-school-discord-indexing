// Reference: System.Drawing
using Network;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
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

namespace Oxide.Plugins
{
    [Info("RustMap", "", "1.2.15")]

    class RustMap : RustPlugin
    {
        #region Classes
        [PluginReference] Plugin Clans;
        [PluginReference] Plugin Friends;
        [PluginReference] Plugin MutualPermission;
        [PluginReference] Plugin NTeleportation;
        [PluginReference] Plugin Teleport;
        [PluginReference] Plugin CustomQuarry;
        [PluginReference] Plugin HomesGUI;

        class MapMarker
        {
            public Transform transform;
            public int Rotation = -1;
            public Vector2 anchorPosition { get { return m_Instance.ToScreenCoords(position); } }
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
                return new MapPlayer(player) { alpha = m_Instance.playerIconAlpha, size = m_Instance.playerIconSize };
            }
        }

        #endregion

        #region Configuration
        static int worldSize;
        string beancanKey;
        float mapAlpha;
        float mapSize;
        bool spawnsSupport;
        bool clanSupport;
        bool friendsSupport;
        float playerIconSize;
        float playerIconAlpha;
        bool playerCoordinates;
        bool monuments;
        bool caves;
        bool powersub;
        float monumentIconSize;
        float monumentIconAlpha;
        bool monumentIconNames;
        int monumentsFontSize;
        bool car;
        bool plane;
        float planeIconSize;
        float planeIconAlpha;
        bool planeDrop;
        bool sethome;
        float sethomeIconSize;
        float planeDropIconSize;
        float planeDropIconAlpha;
        bool heli;
        float heliIconSize;
        float heliIconAlpha;
        bool heliDrop;
        bool quarry;
        float quarryIconAlpha;
        float quarryIconSize;
        float heliDropIconSize;
        float heliDropIconAlpha;
        string textColor;
        string textColor1;
        float bannedSize;
        string mapUrl;
	    string keybind = "m";


        protected override void LoadDefaultConfig()
        {
            Config["Ключ Beancan (Генерация карты с beancan.io)"] = beancanKey = GetConfig("Ключ Beancan (Генерация карты с beancan.io)", "");
            Config["Кастомная карта (http:// или с папки data/RustMap)"] = mapUrl = GetConfig("Кастомная карта (http:// или с папки data/RustMap)", "Map.jpg");
            Config["Прозрачность карты"] = mapAlpha = GetConfig("Прозрачность карты", 0.6f);
            Config["Размер карты"] = mapSize = GetConfig("Размер карты", 0.40f);

            Config["Отображать местоположение карьеров (CustomQuarry)"] = quarry = GetConfig("Отображать местоположение карьеров(CustomQuarry)", false);
            Config["Размер иконки карьера"] = quarryIconSize = GetConfig("Размер иконки карьера", 0.03f);
            Config["Прозрачность иконки карьера"] = quarryIconAlpha = GetConfig("Прозрачность иконки карьера", 0.99f);

            Config["Подержка Spawns"] = spawnsSupport = GetConfig("Подержка Spawns", false);

            Config["Отображать местоположение соклановцев"] = clanSupport = GetConfig("Отображать местоположение соклановцев", false);
            Config["Отображать местоположение друзей"] = friendsSupport = GetConfig("Отображать местоположение друзей", true);
            Config["Цвет текста (RED, GREEN, BLUE, ALPHA)"] = textColor = GetConfig("Цвет текста (RED, GREEN, BLUE, ALPHA)", "0 0.7 0 0.7");
            Config["Цвет текста кастомных иконок (Homes)"] = textColor1 = GetConfig("Цвет текста кастомных иконок (Homes)", "1 1 1 1");

            Config["Отображать местоположение SETHOME игроков? (Поддержка NTeleportation, Teleport, HomesGUI)"] = sethome = GetConfig("Отображать местоположение SETHOME игроков? (Поддержка NTeleportation, Teleport, HomesGUI)", false);
            Config["Размер иконок SETHOME"] = sethomeIconSize = GetConfig("Размер иконок SETHOME", 0.03f);

            Config["Размер иконки игрока"] = playerIconSize = GetConfig("Размер иконки игрока", 0.03f);
            Config["Прозрачность иконки игрока"] = playerIconAlpha = GetConfig("Прозрачность иконки игрока", 0.99f);
            Config["Показывать текущие координаты игрока"] = playerCoordinates = GetConfig("Показывать текущие координаты игрока", true);

            Config["Отображать местоположение монументов"] = monuments = GetConfig("Отображать местоположение монументов", true);
            Config["Показывать пещеры"] = caves = GetConfig("Показывать пещеры", true);
            Config["Показывать подстанции"] = powersub = GetConfig("Показывать подстанции", true);
            Config["Размер иконок монументов"] = monumentIconSize = GetConfig("Размер иконок монументов", 0.03f);
            Config["Размер иконки забаненого игрока"] = bannedSize = GetConfig("Размер иконки забаненого игрока", 0.04f);
            Config["Прозрачность иконок монументов"] = monumentIconAlpha = GetConfig("Прозрачность иконок монументов", 0.99f);
            Config["Показывать название монументов"] = monumentIconNames = GetConfig("Показывать название монументов", true);
            Config["Размер шрифта монументов"] = monumentsFontSize = GetConfig("Размер шрифта монументов", 13);

            Config["Отображать местоположение самолета"] = plane = GetConfig("Отображать местоположение самолета", true);
            Config["Размер иконок самолёта"] = planeIconSize = GetConfig("Размер иконок самолёта", 0.035f);
            Config["Прозрачность иконок самолёта"] = planeIconAlpha = GetConfig("Прозрачность иконок самолёта", 0.99f);

            Config["Отображать местоположение вертолёта"] = heli = GetConfig("Отображать местоположение вертолёта", true);
            Config["Размер иконок вертолёта"] = heliIconSize = GetConfig("Размер иконок вертолёта", 0.035f);
            Config["Прозрачность иконок вертолёта"] = heliIconAlpha = GetConfig("Прозрачность иконок вертолёта", 0.99f);

            Config["Отображать местоположение cброшенного груза"] = planeDrop = GetConfig("Отображать местоположение cброшенного груза", true);
            Config["Размер иконок cброшенного груза"] = planeDropIconSize = GetConfig("Размер иконок cброшенного груза", 0.045f);
            Config["Прозрачность иконок cброшенного груза"] = planeDropIconAlpha = GetConfig("Прозрачность иконок cброшенного груза", 0.95f);

            Config["Отображать местоположение ящиков с вертолёта"] = heliDrop = GetConfig("Отображать местоположение ящиков с вертолёта", true);
            Config["Размер иконок ящиков с вертолёта"] = heliDropIconSize = GetConfig("Размер иконок ящиков с вертолёта", 0.03f);
            Config["Прозрачность иконок ящиков с вертолёта"] = heliDropIconAlpha = GetConfig("Прозрачность иконок ящиков с вертолёта", 0.99f);
            SaveConfig();
            if (!string.IsNullOrEmpty(mapUrl))
            {
                if (!mapUrl.ToLower().Contains("http"))
                {
                    mapUrl = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + mapUrl;
                }

            }
        }

        T GetConfig<T>(string name, T defaultValue)
            => Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion

        #region Fields

        static RustMap m_Instance;
        private string mapIconJson = @"[{""name"":""{0}"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.RawImage"",""sprite"":""assets/content/textures/generic/fulltransparent.tga"",""png"":""{3}"",""color"":""1 1 1 {4}""},{""type"":""RectTransform"",""anchormin"":""{1}"",""anchormax"":""{2}""}]}]";
        private string mapIconTextJson = @"[{""name"":""{0}"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{3}"",""align"":""MiddleCenter"",""fontSize"":12,""color"":""{color}""},{""type"": ""UnityEngine.UI.Outline"",""color"": ""0 0 0 1""},{""type"":""RectTransform"",""anchormin"":""{1}"",""anchormax"":""{2}""}]}]";
        private string mapIconTextJsonIcon = @"[{""name"":""{0}"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{3}"",""align"":""MiddleCenter"",""fontSize"":9,""color"":""{color1}""},{""type"": ""UnityEngine.UI.Outline"",""color"": ""0 0 0 1""},{""type"":""RectTransform"",""anchormin"":""{1}"",""anchormax"":""{2}""}]}]";
        private string mapJson = "[{\"name\":\"map_mainImage\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"Assets/Content/UI/UI.Background.Tile.psd\",\"color\":\"1 1 1 {1}\",\"png\":\"{0}\"},{\"type\":\"RectTransform\",\"anchormin\":\"{2}\",\"anchormax\":\"{3}\"}]}]";
        private string mapCoordsTextJson = @"[{""name"":""map_coordinates"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{0}"",""align"":""MiddleCenter"",""fontSize"":18},{""type"": ""UnityEngine.UI.Outline"",""color"": ""0 0 0 1"",""distance"": ""0.5 -0.5""},{""type"":""RectTransform"",""anchormin"":""0 0.95"",""anchormax"":""1 1""}]}]";

        Dictionary<BasePlayer, MapPlayer> mapPlayers = new Dictionary<BasePlayer, MapPlayer>();
        Dictionary<BasePlayer, MapPlayer> subscribers = new Dictionary<BasePlayer, MapPlayer>();
        List<MapMarker> temporaryMarkers = new List<MapMarker>();

        const string MARKER_BANNED_PERM = "rustmap.banned";
        const string ALL_PLAYERS_PERM = "rustmap.allplayers";
        const string HOMES_PERM = "rustmap.homes";

        string monumentsJson;
        bool init = false;
        #endregion

        #region Oxide Hooks

        private List<BasePlayer> AllPlayerUsers = new List<BasePlayer>();

        [ChatCommand("map")]
        void cmdMapControl(BasePlayer player, string command, string[] args)
        {
            if (!init || player == null) return;
            if (args.Count() >= 1 && args[0] == "all")
            {
                if (!permission.UserHasPermission(player.UserIDString, ALL_PLAYERS_PERM))
                {
                    PrintToChat(player, "У вас нет прав на использование этой команды!", this, player.UserIDString);
                    return;
                }
                if (AllPlayerUsers.Contains(player))
                    AllPlayerUsers.Remove(player);
                else
                    AllPlayerUsers.Add(player);
                CloseMap(player);
                OpenMap(player);
                PrintToChat(player, "Режим администрирования карты включен!", this, player.UserIDString);
                return;
            }


            if (subscribers.Keys.Contains(player))
            {
                CloseMap(player);
            }
            else
            {
                OpenMap(player);
            }
        }
        [ConsoleCommand("map.open")]
        void ConsoleMap(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (AllPlayerUsers.Contains(player))
                AllPlayerUsers.Remove(player);
            else
                AllPlayerUsers.Add(player);
            if (subscribers.Keys.Contains(player))
            {
                CloseMap(player);
            }
            else
            {
                OpenMap(player);
            }

            return;
        }
        [ConsoleCommand("map.all")]
        void ConsoleMapAll(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, ALL_PLAYERS_PERM))
            {
                PrintToChat(player, "У вас нет прав на использование этой команды!", this, player.UserIDString);
                return;
            }
            if (AllPlayerUsers.Contains(player))
                AllPlayerUsers.Remove(player);
            else
                AllPlayerUsers.Add(player);
            CloseMap(player);
            OpenMap(player);
            PrintToChat(player, "Режим администрирования карты включен!", this, player.UserIDString);
            return;

        }

        void OnServerInitialized()
        {
            m_Instance = this;
            PermissionService.RegisterPermissions(this, new List<string>() { MARKER_BANNED_PERM, ALL_PLAYERS_PERM, HOMES_PERM });
            LoadDefaultConfig();
            worldSize = (int)World.Size;
            var anchorMin = new Vector2(0.5f - mapSize * 0.5f, 0.5f - mapSize * 0.800f);
            var anchorMax = new Vector2(0.5f + mapSize * 0.5f, 0.5f + mapSize * 0.930f);
            mapJson = mapJson.Replace("{2}", $"{anchorMin.x} {anchorMin.y}").Replace("{3}", $"{anchorMax.x} {anchorMax.y}");
            mapIconTextJson = mapIconTextJson.Replace("{color}", textColor);
            mapIconTextJsonIcon = mapIconTextJsonIcon.Replace("{color1}", textColor1);

            InitFileManager();
            m_FileManager.StartCoroutine(DownloadMapImage());

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);

            timer.Every(0.0f, () =>
            {
                foreach (var mm in temporaryMarkers)
                    if (mm.NeedRedraw())
                    {
                        ++mm.counter;
                        foreach (var sub in subscribers)
                            DrawMapMarker(sub.Key, mm);
                    }
                foreach (var sub in subscribers)
                {
                    RedrawPlayers(sub.Value);
                }
            });
            BansUpdate();
            timer.Every(20f, BansUpdate);

            foreach (var entity in BaseNetworkable.serverEntities.Select(p => p as BaseEntity).Where(p => p != null))
                OnEntitySpawned(entity);

        }

        void OnPlayerInit(BasePlayer player)
        {
			player.Command("bind m chat.say /map");  
            if (!mapPlayers.ContainsKey(player)) mapPlayers[player] = MapPlayer.Create(player);
        }

        void Unload()
        {
            m_FileManager.SaveData();
            if (!init) return;
            foreach (var sub in subscribers.Keys)
            {
                CuiHelper.DestroyUi(sub, "map_mainImage");
            }
            UnityEngine.Object.Destroy(FileManagerObject);

        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null) return;
            if (plane && entity is CargoPlane)
                AddTemporaryMarker("plane", true, planeIconSize, planeIconAlpha, entity.transform);
            if (planeDrop && entity is SupplyDrop)
            {
                AddTemporaryMarker("mapsupply", false, planeDropIconSize, planeDropIconAlpha, entity.transform);
            }
            if (heli && entity is BaseHelicopter)
            {
                AddTemporaryMarker("heli", true, heliIconSize, heliIconAlpha, entity.transform);
            }
            if (heliDrop && entity is HelicopterDebris)
            {
                AddTemporaryMarker("helidebris", false, heliDropIconSize, heliDropIconAlpha, entity.transform);
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity?.net?.ID == null) return;
            if (entity is CargoPlane || entity is SupplyDrop || entity is BaseHelicopter || entity is HelicopterDebris)
            {
                var transform = entity.transform;
                var mm = temporaryMarkers.Find(p => p.transform == transform);
                if (mm != null)
                    RemoveTemporaryMarker(mm);
            }
        }
        #endregion

        #region Core

        void OpenMap(BasePlayer player)
        {
            CuiHelper.AddUi(player, mapJson);
            CuiHelper.AddUi(player, monumentsJson);
            foreach (var mm in temporaryMarkers)
                DrawMapMarker(player, mm);

            List<ulong> members = new List<ulong>();
            if (PermissionService.HasPermission(player.userID, MARKER_BANNED_PERM))
            {
                foreach (var mm in bannedMarkers.Values)
                    DrawMapMarker(player, mm);
            }
            if (PermissionService.HasPermission(player.userID, ALL_PLAYERS_PERM) && AllPlayerUsers.Contains(player))
            {
                mapPlayers[player].clanTeam = BasePlayer.activePlayerList.Where(p => p != player).Select(MapPlayer.Create).ToList();
            }
            else
            {
                mapPlayers[player].clanTeam = new List<MapPlayer>();
                if (clanSupport)
                {
                    var clanmates = Clans.Call("GetClanMembers", player.userID);
                    if (clanmates != null)
                        members.AddRange(clanmates as List<ulong>);
                }
                if (friendsSupport)
                {
                    var friends = Friends?.Call("GetFriends", player.userID);
                    if (friends != null)
                        members.AddRange(friends as ulong[]);
                    var MutualFr = MutualPermission?.Call("GetFriends", player.userID) as ulong[];
                    if (MutualFr != null)
                        members.AddRange(MutualFr as ulong[]);
                }
            }

            var homes = GetHomes(player);
            if (sethome && PermissionService.HasPermission(player.userID, HOMES_PERM) && homes != null)
                foreach (var home in homes)
                {
                    var anchors = ToAnchors(home.Value, sethomeIconSize);
                    DrawIconNull(player, "sethome" + home.Key, "sethome" + home.Key, anchors, images["sethome"], 1.0f, $"Дом: \"{home.Key}\"", 8, false);
                }
            if (quarry && CustomQuarry != null)
            {
                var playerQuarries = GetPlayerQuarries(player.userID);
                if (playerQuarries != null)
                {
                    foreach (var quarry in playerQuarries)
                    {
                        var anchors = ToAnchors(quarry.Key, quarryIconSize);
                        DrawIconNull(player, "quarry" + quarry.Value, "quarry" + quarry.Value, anchors, images["quarry"], 1.0f, "Топливо: " + quarry.Value, 2, false);
                    }
                }
            }
            if (members != null && members.Count > 0)
            {
                var onlineMembers = BasePlayer.activePlayerList.Where(p => members.Contains(p.userID) && p != player).ToList();
                mapPlayers[player].clanTeam.AddRange(onlineMembers.Select(MapPlayer.Create).ToList());
            }
            subscribers[player] = mapPlayers[player];
            RedrawPlayers(mapPlayers[player]);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            subscribers.Remove(player);
            mapPlayers.Remove(player);
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
                {
                    DrawMapPlayer(player, tmMapPlayer, true);
                }
            }
            DrawMapPlayer(player, mapPlayer);
        }
        // HOME

        void CloseMap(BasePlayer player)
        {
            subscribers.Remove(player);
            mapPlayers[player].OnCloseMap();
            CuiHelper.DestroyUi(player, "map_mainImage");
        }



        void DrawMapPlayer(BasePlayer player, MapPlayer mp, bool friend = false)
        {
            if (mp.NeedRedraw())
            {
                if (!friend && playerCoordinates)
                {
                    CuiHelper.DestroyUi(player, "map_coordinates");
                    var curX = ((float)Math.Round(mp.transform.position.x, 1)).ToString();
                    var curZ = ((float)Math.Round(mp.transform.position.z, 1)).ToString();
                    CuiHelper.AddUi(player, Format(mapCoordsTextJson, "<size=20>" + curX + " <color=#EF015A>/</color> " + curZ + "</size>"));
                }
                var pos = mp.player.transform.position;
                var anchors = ToAnchors(pos, playerIconSize);
                var png = !friend ? PlayerPng(mp.Rotation) : FriendPng(mp.Rotation);
                if (png == null)
                {
                    PrintError($"{friend}", mp.Rotation.ToString());
                    png = FriendPng(mp.Rotation - 2);
                }
                if (!InMap(pos))
                {
                    CuiHelper.DestroyUi(player, "mapPlayer" + mp.player.userID + (mp.counter));
                    CuiHelper.DestroyUi(player, "mapPlayer" + mp.player.userID + (mp.counter) + "text");
                    return;
                }
                DrawIcon(player, "mapPlayer" + mp.player.userID + (mp.counter), "mapPlayer" + mp.player.userID + (++mp.counter), anchors, png, mp.alpha, mp.player.displayName);
            }
        }

        void AddTemporaryMarker(string png, bool rotSupport, float size, float alpha, Transform transform, string name = "")
        {
            var mm = MapMarker.Create(transform);
            mm.name = string.IsNullOrEmpty(name) ? transform.GetInstanceID().ToString() : name;
            mm.png = png;
            mm.rotSupport = rotSupport;
            mm.size = size;
            mm.alpha = alpha;
            mm.fontsize = 12;
            mm.position = transform.position;
            temporaryMarkers.Add(mm);
            foreach (var sub in subscribers)
                DrawMapMarker(sub.Key, mm);
        }

        void RemoveTemporaryMarkerByName(string name)
        {
            var mm = temporaryMarkers.FirstOrDefault(p => p.name == name);
            if (mm != null)
                RemoveTemporaryMarker(mm);
        }

        void RemoveTemporaryMarker(MapMarker mm)
        {
            temporaryMarkers.Remove(mm);
            foreach (var sub in subscribers)
                CuiHelper.DestroyUi(sub.Key, mm.transform.GetInstanceID().ToString() + mm.counter);
        }

        void DrawIcon(BasePlayer player, string lastName, string name, string[] anchors, string png, float alpha, string text = null, int fontsize = 12, bool destroy = true)
        {
            if (destroy)
            {
                timer.Once(0.05f, () => {
                    CuiHelper.DestroyUi(player, lastName);
                });
                CuiHelper.DestroyUi(player, lastName + "text");
            }
            CuiHelper.AddUi(player, Format(mapIconJson, name, anchors[0], anchors[1], png, alpha));
            if (!string.IsNullOrEmpty(text))
                CuiHelper.AddUi(player, Format(mapIconTextJson, name + "text", anchors[2], anchors[3], text.Replace("\"", ""), fontsize));


        }
        void DrawIconNull(BasePlayer player, string lastName, string name, string[] anchors, string png, float alpha, string text = null, int fontsize = 12, bool destroy = true)
        {
            if (destroy)
            {
                timer.Once(0.05f, () => {
                    CuiHelper.DestroyUi(player, lastName);
                });
                CuiHelper.DestroyUi(player, lastName + "text");
            }
            CuiHelper.AddUi(player, Format(mapIconJson, name, anchors[0], anchors[1], png, alpha));
            if (!string.IsNullOrEmpty(text))
                CuiHelper.AddUi(player, Format(mapIconTextJsonIcon, name + "text", anchors[2], anchors[3], text.Replace("\"", ""), fontsize));


        }

        void DrawMapMarker(BasePlayer player, MapMarker mm)
        {
            if (mm.transform == null) return;
            var pos = mm.transform.position;
            var anchors = ToAnchors(pos, mm.size);
            var png = mm.png;
            if (mm.rotSupport)
                png += GetRotation(mm.transform.rotation.eulerAngles.y);

            if (images[png] == null)
            {
                PrintError("PNG = NULL: " + png);
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

        #endregion

        #region Banned

        List<ulong> bannedCache = new List<ulong>();
        Dictionary<BasePlayer, MapMarker> bannedMarkers = new Dictionary<BasePlayer, MapMarker>();

        void AddBannedMarker(BasePlayer player)
        {
            var transform = player.transform;
            var mm = MapMarker.Create(transform);
            mm.png = "banned";
            mm.rotSupport = false;
            mm.name = transform.GetInstanceID().ToString();
            mm.alpha = 1f;
            mm.size = bannedSize;
            mm.position = transform.position;
            bannedMarkers[player] = mm;

            foreach (var sub in subscribers)
                if (PermissionService.HasPermission(sub.Key.userID, MARKER_BANNED_PERM))
                    DrawMapMarker(sub.Key, mm);
        }
        void RemoveBannedMarker(BasePlayer player)
        {
            var mm = bannedMarkers[player];
            bannedMarkers.Remove(player);
            foreach (var sub in subscribers)
                CuiHelper.DestroyUi(sub.Key, mm.transform.GetInstanceID().ToString() + mm.counter);
        }

        void BansUpdate()
        {
            var unlisted = ServerUsers.GetAll(ServerUsers.UserGroup.Banned).Select(p => p.steamid).Except(bannedCache).ToList();
            bannedCache.AddRange(unlisted);
            if (unlisted.Count == 0) return;
            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                if (unlisted.Contains(player.userID))
                {
                    AddBannedMarker(player);
                }
            }
        }

        void OnPlayerBanned(Connection connection, string reason)
        {
            var userId = connection.userid;
            if (bannedCache.Contains(userId)) return;

            var user = BasePlayer.activePlayerList.Find(p => p.userID == userId);
            if (user == null)
            {
                user = BasePlayer.sleepingPlayerList.Find(p => p.userID == userId);
                if (user == null) return;
                AddBannedMarker(user);
                return;
            }
            AddBannedMarker(user);
        }

        void OnEntityDeath(BaseCombatEntity entity)
        {
            var player = entity as BasePlayer;
            if (player == null || !player.IsSleeping()) return;
            if (bannedMarkers.ContainsKey(player))
            {
                RemoveBannedMarker(player);
            }
        }

        #endregion

        #region Markers

        void FindStaticMarkers()
        {
            if (!this.monuments)
            {
                monumentsJson = "";
                return;
            }
            var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();
            var container = new CuiElementContainer();
            foreach (var monument in monuments)
            {
                var anchors = ToAnchors(monument.transform.position, monumentIconSize);
                string png;
                string text = null;
                if (monument.Type == MonumentType.Cave && caves)
                    png = "cave";
                else
                if (monument.name.Contains("lighthouse"))
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
                else if (monument.name.Contains("water_treatment_plant_1"))
                {
                    png = "watertreatment";
                    text = "Водонапорка";
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
                else if (monument.name.Contains("radtown_small_3"))
                {
                    png = "radtown";
                    text = "Редтаун";
                }
                else if (monument.name.Contains("power_sub") && powersub)
                    png = "powersub";

                else if (monument.name.Contains("launch_site_1"))
                {
                    png = "launchsite";
                    text = "Аэродром";
                }

                else
                {
                    Puts("MAP IGNORE: " + monument.name);
                    continue;
                }

                container.Add(new CuiElement()
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = "map_mainImage",
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Png = images[png],
                            Color = $"1 1 1 {monumentIconAlpha}"
                        },

                        new CuiRectTransformComponent() {AnchorMin = anchors[0], AnchorMax = anchors[1]}
                    }
                });
                if (!string.IsNullOrEmpty(text) && monumentIconNames)
                {
                    container.Add(new CuiElement()
                    {
                        Name = CuiHelper.GetGuid(),
                        Parent = "map_mainImage",
                        Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = text, FontSize = monumentsFontSize, Align = TextAnchor.MiddleCenter,
                        },
                        new CuiOutlineComponent()
                    {
                        Color = "0 0 0 1"
                    },
                        new CuiRectTransformComponent() {AnchorMin = anchors[2], AnchorMax = anchors[3]}
                    }
                    });
                }
            }
            if (Spawns != null)
            {
                bool left = true;
                container.AddRange(
                    GetSpawnZones()
                        .Select(spawn => ToAnchors(spawn.Key, (float)spawn.Value * 2 / worldSize))
                        .Select(anchors =>
                        {
                            var element = new CuiElement()
                            {
                                Name = CuiHelper.GetGuid(),
                                Parent = "map_mainImage",
                                Components =
                                {
                                    new CuiRawImageComponent()
                                    {
                                        Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                        Png = left ? images["spawnLeft"] : images["spawnRight"]
                                    },
                                    new CuiRectTransformComponent() {AnchorMin = anchors[0], AnchorMax = anchors[1]}
                                }
                            };
                            left = false;
                            return element;
                        }));
            }
            this.monumentsJson = container.ToJson();
        }

        [PluginReference]
        Plugin Spawns;

        Dictionary<Vector3, int> GetSpawnZones() => Spawns.Call<Dictionary<Vector3, int>>("GetSpawnZones");

        Dictionary<string, Vector3> GetHomes(BasePlayer player)
        {
            var a1 = (Dictionary<string, Vector3>)NTeleportation?.Call("GetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a2 = (Dictionary<string, Vector3>)Teleport?.Call("ApiGetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a3 = (Dictionary<string, Vector3>)HomesGUI?.Call("GetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            return a1.Concat(a2).Concat(a3).GroupBy(p => p.Key).ToDictionary(p => p.Key, p => p.First().Value);
        }




        Dictionary<Vector3, int> GetPlayerQuarries(ulong userId)
        {
            return CustomQuarry.Call("GetPlayerQuarries", userId) as Dictionary<Vector3, int>;
        }
        string[] ToAnchors(Vector3 position, float size)
        {
            Vector2 center = ToScreenCoords(position);
            size *= 0.5f;
            return new[]
            {
                $"{center.x - size} {center.y - size}",
                $"{center.x + size} {center.y + size}",
                $"{center.x - 0.1} {center.y - size-0.04f}",
                $"{center.x + 0.1} {center.y - size+0.02}"
            };
        }

        Vector2 ToScreenCoords(Vector3 vec)
        {
            return new Vector2((vec.x + (int)World.Size * 0.5f) / (int)World.Size, (vec.z + (int)World.Size * 0.5f) / (int)World.Size);
        }

        static int GetRotation(float angle)
        {
            if (angle > 348.75f && angle < 11.25f)
                return 16;
            if (angle > 11.25f && angle < 33.75f)
                return 1;
            if (angle > 33.75f && angle < 56.25f)
                return 2;
            if (angle > 56.25f && angle < 78.75f)
                return 3;
            if (angle > 78.75f && angle < 101.25f)
                return 4;
            if (angle > 101.25f && angle < 123.75f)
                return 5;
            if (angle > 123.75f && angle < 146.25F)
                return 6;
            if (angle > 146.25F && angle < 168.75D)
                return 7;
            if (angle > 168.75F && angle < 191.25D)
                return 8;
            if (angle > 191.25F && angle < 213.4D)
                return 9;
            if (angle > 213.75F && angle < 236.25D)
                return 10;
            if (angle > 236.25F && angle < 258.75D)
                return 11;
            if (angle > 258.75D && angle < 281.25D)
                return 12;
            if (angle > 281.25D && angle < 303.75D)
                return 13;
            if (angle > 303.75D && angle < 326.25D)
                return 14;
            if (angle > 326.25D && angle < 348.75D)
                return 15;
            return 16;
        }

        #endregion

        #region API

        private void DisableMaps(BasePlayer player)
        {
            if (subscribers.Keys.Contains(player))
            {
                CloseMap(player);
            }
        }

        #endregion

        #region Map Generation - Credits to Calytic, Nogrod, kraz and beancan.io for the awesome looking map images and API to make this possible!

        bool mapLoaded = false;
        IEnumerator DownloadMapImage()
        {

            if (!string.IsNullOrEmpty(mapUrl))
            {
                images[MapFilename] = mapUrl;
            }



            if (images.ContainsKey(MapFilename))
            {
                yield return CommunityEntity.ServerInstance.StartCoroutine(LoadImages());
                mapLoaded = true;
                yield break;
            }
            if (string.IsNullOrEmpty(beancanKey))
            {
                PrintError("Вы должны указать действительный ключ API (Beancan.io) или указать ссылку на картинку в <Кастомная карта> (http:// или Data)!\nЧто бы получить ключ Beanсan посетите сайт 'beancan.io'!");
                yield break;
            }
            PrintWarning("Attempting to contact beancan.io to download your map image!");

            GetQueueID();
        }

        void GetQueueID()
        {
            var url = "http://beancan.io/map-queue-generate?" + $"level={ConVar.Server.level}&seed={World.Seed}&size={TerrainMeta.Size.x}&key={beancanKey}";
            webrequest.EnqueuePost(url, null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response))
                {
                    if (code == 403)
                        PrintError($"Error: {code} - Invalid API key. Unable to download map image");
                    else PrintWarning($"Error: {code} - Couldn't get an answer from beancan.io. Unable to download map image. Please try again in a few minutes");
                }
                else CheckAvailability(response);
            }, this);
        }
        void CheckAvailability(string queueId)
        {
            webrequest.EnqueuePost("http://beancan.io/map-queue/" + $"{queueId}", null, (code, response) =>
            {
                if (string.IsNullOrEmpty(response))
                {
                    PrintWarning($"Error: {code} - Couldn't get an answer from beancan.io");
                }
                else ProcessResponse(queueId, response);
            }, this);
        }
        void ProcessResponse(string queueId, string response)
        {
            switch (response)
            {
                case "-1":
                    PrintWarning("Your map is still in the queue to be generated. Checking again in 10 seconds");
                    break;
                case "0":
                    PrintWarning("Your map is still being generated. Checking again in 10 seconds");
                    break;
                case "1":
                    GetMapURL(queueId);
                    return;
                default:
                    PrintWarning($"Error retrieving map: Invalid response from beancan.io: Response code {response}");
                    return;
            }
            timer.Once(10, () => CheckAvailability(queueId));
        }
        void GetMapURL(string queueId)
        {
            var url = "http://beancan.io/map-queue-image/" + $"{queueId}";
            webrequest.EnqueuePost(url, null, (code, response) =>
            {
                if (string.IsNullOrEmpty(response))
                {
                    PrintWarning($"Error: {code} - Couldn't get an answer from beancan.io");
                }
                else
                {
                    images[MapFilename] = response;
                    Puts(response);
                    CommunityEntity.ServerInstance.StartCoroutine(DownloadMapImage());
                }
            }, this);
        }

        #endregion

        #region Helpers

        string Format(string value, params object[] args)
        {
            var result = new StringBuilder(value);
            for (int i = 0; i < args.Length; i++)
                if (args[i] == null)
                {
                    throw new NullReferenceException();
                }
                else
                {
                    result.Replace("{" + i + "}", args[i].ToString());
                }
            return result.ToString();
        }

        #endregion

        #region Images

        IEnumerator LoadImages()
        {
            foreach (var name in imagesKeys)
            {
                yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(name, images[name], name == MapFilename ? 1440 : -1));
                images[name] = m_FileManager.GetPng(name);
            }
            mapJson = Format(mapJson, MapPng(), mapAlpha);
            FindStaticMarkers();
            init = true;
            Puts(MapPng());
            Puts("Изображения карты успешно загружены!");
        }

        string MapFilename => $"{ConVar.Server.level}_{World.Seed}_{TerrainMeta.Size.x}";
        List<string> imagesKeys => images.Keys.ToList();
        string PlayerPng(int rot) => images[imagesKeys[rot - 1]];
        string FriendPng(int rot) => images[imagesKeys[15 + rot]];

        string PlanePng(int rot) => images[imagesKeys[31 + rot]];

        string MapPng() => images[MapFilename];

        Dictionary<string, string> images = new Dictionary<string, string>()
        {
            {"player1", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player1.png"},
            {"player2", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player2.png"},
            {"player3", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player3.png"},
            {"player4", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player4.png"},
            {"player5", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player5.png"},
            {"player6", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player6.png"},
            {"player7", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player7.png"},
            {"player8", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player8.png"},
            {"player9", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player9.png"},
            {"player10", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player10.png"},
            {"player11", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player11.png"},
            {"player12", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player12.png"},
            {"player13", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player13.png"},
            {"player14", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player14.png"},
            {"player15", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player15.png"},
            {"player16", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "player16.png"},

            {"friend1", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend1.png"},
            {"friend2", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend2.png"},
            {"friend3", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend3.png"},
            {"friend4", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend4.png"},
            {"friend5", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend5.png"},
            {"friend6", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend6.png"},
            {"friend7", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend7.png"},
            {"friend8", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend8.png"},
            {"friend9", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend9.png"},
            {"friend10", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend10.png"},
            {"friend11", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend11.png"},
            {"friend12", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend12.png"},
            {"friend13", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend13.png"},
            {"friend14", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend14.png"},
            {"friend15", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend15.png"},
            {"friend16", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "friend16.png"},

            {"plane1", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane1.png"},
            {"plane2", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane2.png"},
            {"plane3", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane3.png"},
            {"plane4", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane4.png"},
            {"plane5", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane5.png"},
            {"plane6", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane6.png"},
            {"plane7", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane7.png"},
            {"plane8", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane8.png"},
            {"plane9", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane9.png"},
            {"plane10", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane10.png"},
            {"plane11", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane11.png"},
            {"plane12", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane12.png"},
            {"plane13", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane13.png"},
            {"plane14", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane14.png"},
            {"plane15", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane15.png"},
            {"plane16", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "plane16.png"},

            {"heli1", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli1.png"},
            {"heli2", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli2.png"},
            {"heli3", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli3.png"},
            {"heli4", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli4.png"},
            {"heli5", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli5.png"},
            {"heli6", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli6.png"},
            {"heli7", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli7.png"},
            {"heli8", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli8.png"},
            {"heli9", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli9.png"},
            {"heli10", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli10.png"},
            {"heli11", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli11.png"},
            {"heli12", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli12.png"},
            {"heli13", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli13.png"},
            {"heli14", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli14.png"},
            {"heli15", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli15.png"},
            {"heli16", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "heli16.png"},

            {"lighthouse", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "lighthouse.png" },
            { "special", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "special.png" },
            { "militarytunnel", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "militarytunnel.png"},
            { "airfield", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "airfield.png" },
            { "trainyard", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "trainyard.png" },
            { "gasstation", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "gasstation.png" },
            { "supermarket", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "supermarket.png" },
            { "watertreatment", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "watertreatment.png" },
            { "warehouse", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "warehouse.png" },
            { "satellitedish", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "satellitedish.png" },
            { "spheretank", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "spheretank.png" },
            { "radtown", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "radtown.png" },
            { "powerplant", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "powerplant.png" },
            { "harbor", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "harbor.png" },
            { "powersub", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "powersub.png" },
            { "cave", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "cave.png" },
            { "launchsite", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "launchsite.png" },
            { "spawnRight", "http://i.imgur.com/gUpyctl.png" },
            { "spawnLeft", "http://i.imgur.com/mmabTrn.png" },
            { "mapsupply","file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "mapsupply.png" },
            { "helidebris", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "helidebris.png" },
            { "banned", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "banned.png" },
            { "sethome", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "homes.png" },
            { "treasurebox", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "treasurebox.png" },
             { "quarry", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "quarry.png" }

        };

        #endregion

        #region File Manager

        private GameObject FileManagerObject;
        private FileManager m_FileManager;

        /// <summary>
        /// Инициализация скрипта взаимодействующего с файлами сервера
        /// </summary>
        void InitFileManager()
        {
            FileManagerObject = new GameObject("MAP_FileManagerObject");
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
        }


        class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;

            public bool IsFinished => needed == loaded;
            const ulong MaxActiveLoads = 10;
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

            public string GetPng(string name) => files[name].Png;

            private void Awake()
            {
                files = dataFile.ReadObject<Dictionary<string, FileInfo>>() ?? new Dictionary<string, FileInfo>();
            }

            public IEnumerator LoadFile(string name, string url, int size = -1)
            {
                if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png)) yield break;
                files[name] = new FileInfo() { Url = url };
                needed++;
                yield return StartCoroutine(LoadImageCoroutine(name, url, size));
            }

            IEnumerator LoadImageCoroutine(string name, string url, int size = -1)
            {
                MemoryStream stream = new MemoryStream();

                using (WWW www = new WWW(url))
                {
                    yield return www;

                    {
                        if (string.IsNullOrEmpty(www.error))
                        {
                            var bytes = size == -1 ? www.bytes : Resize(www.bytes, size);
                            stream.Write(bytes, 0, bytes.Length);
                            var entityId = CommunityEntity.ServerInstance.net.ID;
                            var crc32 = FileStorage.server.Store(stream, FileStorage.Type.png, entityId).ToString();
                            files[name].Png = crc32;
                        }
                    }
                }
                loaded++;
            }

            static byte[] Resize(byte[] bytes, int size)
            {
                System.Drawing.Image img = (Bitmap)(new ImageConverter().ConvertFrom(bytes));
                Bitmap cutPiece = new Bitmap(size, size);
                System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(cutPiece);
                graphic.DrawImage(img, new Rectangle(0, 0, size, size), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                graphic.Dispose();
                MemoryStream ms = new MemoryStream();
                cutPiece.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }

        #endregion

        #region Permission Service

        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

            public static bool HasPermission(ulong uid, string permissionName)
            {
                return !string.IsNullOrEmpty(permissionName) && permission.UserHasPermission(uid.ToString(), permissionName);
            }

            public static void RegisterPermissions(Plugin owner, List<string> permissions)
            {
                if (owner == null) throw new ArgumentNullException("owner");
                if (permissions == null) throw new ArgumentNullException("commands");

                foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }

        #endregion
    }
}