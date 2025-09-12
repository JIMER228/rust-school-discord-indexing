using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("RustMap", "TopPlugins", "2.4.2")]
    [Description("Спасибо за приобретение на topplugin.ru")]
    class RustMap : RustPlugin
    {
        [PluginReference] Plugin Clans, Friends, NTeleportation, Teleport, Teleportation, CustomQuarry, HomesGUI, PlayersClasses;
        public class GenerateMapSave
        {
            public string MapSeed { get; set; }
            public int WorldSize { get; set; }

            public GenerateMapSave(string MapSeed, int WorldSize)
            {
                this.MapSeed = MapSeed;
                this.WorldSize = WorldSize;
            }
        }

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

        bool generated;
        float mapAlpha = 1f;
        float mapSize = 0.48f;
        bool clanSupport = false;
        bool SleepSup = true;
        bool friendsSupport = false;
        float playerIconSize = 0.03f;
        float playerIconAlpha = 0.99f;
        bool playerCoordinates = false;
        bool monuments = true;
        bool caves = true;
        bool water = true;
        bool powersub = true;
        float monumentIconSize = 0.036f;
        float monumentIconAlpha = 0.99f;
        bool monumentIconNames = true;
        float MapUpdate = 0.3f;
        bool raidhomes = false;
        float raidhomeSIze = 0.06f;
        int monumentsFontSize = 11;
        bool plane = true;
        float planeIconSize = 0.035f;
        float planeIconAlpha = 0.99f;
        float TimeToClose = 10f;
        bool NewHeli = true;
        float NewheliIconSize = 0.035f;
        float NewheliIconAlpha = 0.99f;
        bool ship = true;
        float CargoIconSize = 0.05f;
        float CargoIconAlpha = 0.99f;
        bool swamp = true;
        bool Oilrig = false;
        bool NewHeliCrate = true;
        float NewHeliCrateIconSize = 0.05f;
        float NewHeliCrateIconAlpha = 0.99f;
        bool planeDrop = true;
        bool sethome = false;
        float sethomeIconSize = 0.03f;
        float deathIconSize = 0.03f;
        float planeDropIconSize = 0.045f;
        float planeDropIconAlpha = 0.95f;
        bool heli = true;
        float heliIconSize = 0.035f;
        float heliIconAlpha = 0.99f;
        bool heliDrop = true;
        bool quarry = false;
        float quarryIconSize = 0.03f;
        float heliDropIconSize = 0.03f;
        float heliDropIconAlpha = 0.99f;
        bool vendingMachine = false;
        bool vendingMachineEmpty = false;
        float vendingMachineIconSize = 0.02f;
        float vendingMachineIconAlpha = 0.99f;
        string textColor = "0 0.7 0 0.7";
        string textColor1 = "1 1 1 1";
        float bannedSize = 0.04f;
        string mapUrl = "Map.jpg";
        string MapText = "Помощь по карте <color=#ff9d00>/map menu</color>";
        private string version;
        bool NPCVendingMachine = false;
        bool MapGen = false;

        protected override void LoadDefaultConfig()
        {
            GetVariable("Основные", "Генерация карты через RustMapAPI (Укажите изображение Map.jpg)", ref MapGen);
            GetVariable("Основные", "Изображение карты (https:// или с папки data/RustMap)", ref mapUrl);
            GetVariable("Прозрачность элементов", "Прозрачность карты", ref mapAlpha);
            GetVariable("Размеры элементов", "Размер карты", ref mapSize);
            GetVariable("Элементы", "Отображать местоположение магазинов с Аванпоста", ref NPCVendingMachine);
            GetVariable("Элементы", "Отображать местоположение карьеров (CustomQuarry)", ref quarry);
            GetVariable("Размеры элементов", "Размер иконки карьера", ref quarryIconSize);
            GetVariable("Элементы", "Отображать дома которые рейдят(Нужен NoEscape)", ref raidhomes);
            GetVariable("Размеры элементов", "Размер иконок домов, которые рейдят", ref raidhomeSIze);
            GetVariable("Элементы", "Отображать местоположение соклановцев", ref clanSupport);
            GetVariable("Элементы", "Отображать местоположение друзей (Friends & Team)", ref friendsSupport);
            GetVariable("Элементы", "Отображать местоположение спальников игрока", ref SleepSup);
            GetVariable("Основные", "Цвет текста (RED, GREEN, BLUE, ALPHA)", ref textColor);
            GetVariable("Основные", "Цвет текста кастомных иконок (Homes)", ref textColor1);
            GetVariable("Основные", "Текст внизу карты (советуем оставить /map menu)", ref MapText);
            GetVariable("Элементы", "Отображать местоположение SETHOME игроков? (Поддержка NTeleportation, Teleport, HomesGUI)", ref sethome);
            GetVariable("Размеры элементов", "Размер иконки SETHOME", ref sethomeIconSize);
            GetVariable("Размеры элементов", "Размер иконки DEATH", ref deathIconSize);
            GetVariable("Размеры элементов", "Размер иконки игрока", ref playerIconSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконки игрока", ref playerIconAlpha);
            GetVariable("Элементы", "Показывать текущие координаты игрока", ref playerCoordinates);
            GetVariable("Элементы", "Отображать местоположение монументов", ref monuments);
            GetVariable("Элементы", "Показывать пещеры", ref caves);
            GetVariable("Элементы", "Показывать водонапорные башни", ref water);
            GetVariable("Элементы", "Показывать подстанции", ref powersub);
            GetVariable("Размеры элементов", "Размер иконок монументов", ref monumentIconSize);
            GetVariable("Размеры элементов", "Размер иконки забаненного игрока", ref bannedSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконок монументов", ref monumentIconAlpha);
            GetVariable("Прозрачность элементов", "Показывать название монументов", ref monumentIconNames);
            GetVariable("Размеры элементов", "Размер шрифта монументов", ref monumentsFontSize);
            GetVariable("Элементы", "Отображать местоположение самолета", ref plane);
            GetVariable("Размеры элементов", "Размер иконки самолёта", ref planeIconSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконки самолёта", ref planeIconAlpha);
            GetVariable("Элементы", "Отображать местоположение cброшенного груза с Чинука", ref NewHeliCrate);
            GetVariable("Размеры элементов", "Размер иконки груза с Чинука", ref NewHeliCrateIconSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконки груза с Чинука", ref NewHeliCrateIconAlpha);
            GetVariable("Основные", "Частота обновлений карты", ref MapUpdate);
            GetVariable("Основные", "Время до автоматического закрытия карты после ее открытия", ref TimeToClose);
            GetVariable("Элементы", "Отображать местоположение грузового вертолёта", ref NewHeli);
            GetVariable("Размеры элементов", "Размер иконки грузового вертолёта", ref NewheliIconSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконки грузового вертолёта", ref NewheliIconAlpha);
            GetVariable("Элементы", "Отображать местоположение вертолёта", ref heli);
            GetVariable("Размеры элементов", "Размер иконок вертолёта", ref heliIconSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконок вертолёта", ref heliIconAlpha);
            GetVariable("Элементы", "Отображать местоположение cброшенного груза", ref planeDrop);
            GetVariable("Размеры элементов", "Размер иконок cброшенного груза", ref planeDropIconSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконок cброшенного груза", ref planeDropIconAlpha);
            GetVariable("Элементы", "Отображать местоположение ящиков с вертолёта", ref heliDrop);
            GetVariable("Размеры элементов", "Размер иконок ящиков с вертолёта", ref heliDropIconSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконок ящиков с вертолёта", ref heliDropIconAlpha);
            GetVariable("Элементы", "Отображать местоположение корабля", ref ship);
            GetVariable("Размеры элементов", "Размер иконки корабля", ref CargoIconSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконки корабля", ref CargoIconAlpha);
            GetVariable("Элементы", "Отображать местоположение болот", ref swamp);
            GetVariable("Элементы", "Отображать местоположение OilRig(Тестовая функция - Выходит за границы!)", ref Oilrig);
            GetVariable("Элементы", "Отображать местоположение торговых автоматов", ref vendingMachine);
            GetVariable("Элементы", "Отображать местоположение пустых торговых автоматов", ref vendingMachineEmpty);
            GetVariable("Размеры элементов", "Размер иконок торговых автоматов", ref vendingMachineIconSize);
            GetVariable("Прозрачность элементов", "Прозрачность иконок торговых автоматов", ref vendingMachineIconAlpha);
            SaveConfig();
            if (!string.IsNullOrEmpty(mapUrl))
            {
                if (!mapUrl.ToLower().Contains("http"))
                {
                    mapUrl = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + mapUrl;
                }
            }
        }

        private bool GetVariable<T>(string MainMenu, string Key, ref T var)
        {
            if (Config[MainMenu, Key] != null)
            {
                var = (T)Convert.ChangeType(Config[MainMenu, Key], typeof(T));
                return false;
            }
            Config[MainMenu, Key] = var;
            return true;
        }

        static RustMap m_Instance;
        private string mapIconJson = @"[{""name"":""{0}"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.RawImage"",""sprite"":""assets/content/textures/generic/fulltransparent.tga"",""png"":""{3}"",""color"":""1 1 1 {4}""},{""type"":""RectTransform"",""anchormin"":""{1}"",""anchormax"":""{2}""}]}]";
        private string mapIconTextJson = @"[{""name"":""{0}"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{3}"",""align"":""MiddleCenter"",""fontSize"":12,""color"":""{color}""},{""type"": ""UnityEngine.UI.Outline"",""color"": ""0 0 0 1""},{""type"":""RectTransform"",""anchormin"":""{1}"",""anchormax"":""{2}""}]}]";
        private string mapIconTextJsonIcon = @"[{""name"":""{0}"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{3}"",""align"":""MiddleCenter"",""fontSize"":9,""color"":""{color1}""},{""type"": ""UnityEngine.UI.Outline"",""color"": ""0 0 0 1""},{""type"":""RectTransform"",""anchormin"":""{1}"",""anchormax"":""{2}""}]}]";
        private string mapJson = "[{\"name\":\"map_mainImage\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"Assets/Content/UI/UI.Background.Tile.psd\",\"color\":\"1 1 1 {1}\",\"png\":\"{0}\"},{\"type\":\"RectTransform\",\"anchormin\":\"{2}\",\"anchormax\":\"{3}\"}]}]";
        private string mapCoordsTextJson = @"[{""name"":""map_coordinates"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{0}"",""align"":""MiddleCenter"",""fontSize"":18},{""type"": ""UnityEngine.UI.Outline"",""color"": ""0 0 0 1"",""distance"": ""0.5 -0.5""},{""type"":""RectTransform"",""anchormin"":""0 0.95"",""anchormax"":""1 1""}]}]";
        private string mapMenuJson = @"[{""name"":""map_menu"",""parent"":""map_mainImage"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{0}"",""align"":""MiddleLeft"",""fontSize"":16},{""type"": ""UnityEngine.UI.Outline"",""color"": ""0 0 0 1"",""distance"": ""0.5 -0.5""},{""type"":""RectTransform"",""anchormin"":""0.02 0.001"",""anchormax"":""1 0.05""}]}]";

        Dictionary<BasePlayer, MapPlayer> mapPlayers = new Dictionary<BasePlayer, MapPlayer>();
        Dictionary<BasePlayer, MapPlayer> subscribers = new Dictionary<BasePlayer, MapPlayer>();
        List<MapMarker> temporaryMarkers = new List<MapMarker>();

        const string MAP_ADMIN = "rustmap.admin";
        static string mapSeed;
        static int worldSize;
        string monumentsJson;
        bool init = false;

        private List<BasePlayer> AllPlayerUsers = new List<BasePlayer>();

        private Timer CloseMapTimer;

        [ChatCommand("map")]
        void cmdMapControl(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsConnected) return;
            if (!init || player == null)
            {
                SendReply(player, "В данный момент карта не работает!\nОбратитесь к администрации");
                return;
            }
            CuiHelper.DestroyUi(player, "mapmenu_2");
            if (sethome)
            {
                if (args.Count() >= 1 && args[0] == "homes")
                {
                    if (data.MapPlayerData[player.userID].Homes == true)
                    {
                        data.MapPlayerData[player.userID].Homes = data.MapPlayerData[player.userID].Homes = false;
                        SendReply(player, $"<color=orange>Map homes</color>: False");
                        return;

                    }
                    if (data.MapPlayerData[player.userID].Homes == false)
                    {
                        data.MapPlayerData[player.userID].Homes = data.MapPlayerData[player.userID].Homes = true;
                        SendReply(player, $"<color=orange>Map homes</color>: True");
                        return;
                    }
                }
            }
            if (friendsSupport)
            {
                if (args.Count() >= 1 && args[0] == "friends")
                {
                    if (data.MapPlayerData[player.userID].Friends == true)
                    {
                        data.MapPlayerData[player.userID].Friends = data.MapPlayerData[player.userID].Friends = false;
                        SendReply(player, $"<color=orange>Map friends</color>: False");
                        return;

                    }
                    if (data.MapPlayerData[player.userID].Friends == false)
                    {
                        data.MapPlayerData[player.userID].Friends = data.MapPlayerData[player.userID].Friends = true;
                        SendReply(player, $"<color=orange>Map Friends</color>: True");
                        return;
                    }
                }
            }
            if (clanSupport)
            {
                if (args.Count() >= 1 && args[0] == "clans")
                {
                    if (data.MapPlayerData[player.userID].Clans == true)
                    {
                        data.MapPlayerData[player.userID].Clans = data.MapPlayerData[player.userID].Clans = false;
                        SendReply(player, $"<color=orange>Map Clans</color>: False");
                        return;

                    }
                    if (data.MapPlayerData[player.userID].Clans == false)
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
                    if (data.MapPlayerData[player.userID].AllPlayers == true)
                    {
                        data.MapPlayerData[player.userID].AllPlayers = data.MapPlayerData[player.userID].AllPlayers = false;
                        SendReply(player, $"<color=orange>Map AllPlayers</color>: False");
                        return;

                    }
                    if (data.MapPlayerData[player.userID].AllPlayers == false)
                    {
                        data.MapPlayerData[player.userID].AllPlayers = data.MapPlayerData[player.userID].AllPlayers = true;
                        SendReply(player, $"<color=orange>Map AllPlayers</color>: True");
                        return;
                    }
                }
                if (args.Count() >= 1 && args[0] == "bans")
                {
                    if (data.MapPlayerData[player.userID].BanPlayers == true)
                    {
                        data.MapPlayerData[player.userID].BanPlayers = data.MapPlayerData[player.userID].BanPlayers = false;
                        SendReply(player, $"<color=orange>Map BanPlayers</color>: False");
                        return;

                    }
                    if (data.MapPlayerData[player.userID].BanPlayers == false)
                    {
                        data.MapPlayerData[player.userID].BanPlayers = data.MapPlayerData[player.userID].BanPlayers = true;
                        SendReply(player, $"<color=orange>Map BanPlayers</color>: True");
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
                if (permission.UserHasPermission(player.UserIDString, MAP_ADMIN))
                {
                    if (args.Count() >= 1 && args[0] == "menu")
                    {
                        MenuUI(player);
                        return;
                    }

                    if (data.MapPlayerData[player.userID].AllPlayers == true)
                    {
                        AllPlayerUsers.Add(player);
                    }
                    if (data.MapPlayerData[player.userID].AllPlayers == false)
                    {
                        if (AllPlayerUsers.Contains(player))
                            AllPlayerUsers.Remove(player);
                    }
                    OpenMap(player);
                    return;
                }
                if (!permission.UserHasPermission(player.UserIDString, MAP_ADMIN))
                {
                    if (args.Count() >= 1 && args[0] == "menu")
                    {
                        MenuUI(player);
                        return;
                    }
                    OpenMap(player);
                }
            }
        }

        string button = "[{\"name\":\"CuiElement\",\"parent\":\"mapmenu_2\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"Assets/Content/UI/UI.Background.Tile.psd\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.03 {amin}\",\"anchormax\":\"0.97 {amax}\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"CuiElement\",\"parent\":\"CuiElement\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":16,\"align\":\"MiddleCenter\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.2784314\",\"distance\":\"1 -1\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"CuiElement\",\"parent\":\"CuiElement\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{command}\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]}]";
        private string MapMenu = "[{\"name\":\"mapmenu_2\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"Assets/Content/UI/UI.Background.Tile.psd\",\"color\":\"0.1647059 0.1647059 0.1647059 1\"},{\"type\":\"NeedsCursor\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.3704246 0.1914063\",\"anchormax\":\"0.6222548 0.7773438\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"mapmenu_3\",\"parent\":\"mapmenu_2\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"Assets/Content/UI/UI.Background.Tile.psd\",\"color\":\"0.8431373 0.3372549 0.2509804 1\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0.9359605\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"mapmenu_4\",\"parent\":\"mapmenu_3\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"<b>Меню карты</b>\",\"fontSize\":18,\"align\":\"MiddleCenter\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.2784314\",\"distance\":\"1 -1\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"mapmenu_5\",\"parent\":\"mapmenu_2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"\",\"align\":\"MiddleRight\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.01492521 0.004926026\",\"anchormax\":\"1 0.06896545\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"mapmenu_6\",\"parent\":\"mapmenu_2\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"Assets/Content/UI/UI.Background.Tile.psd\",\"color\":\"0.2728048 0.2728048 0.2728048 1\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0.8431373 0.3372549 0.2509804 1\",\"distance\":\"0 -1\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.51 0.07635458\",\"anchormax\":\"0.97 0.135468\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"mapmenu_7\",\"parent\":\"mapmenu_6\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"☓ <b>Закрыть</b>\",\"fontSize\":16,\"align\":\"MiddleCenter\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.2784314\",\"distance\":\"1 -1\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"button1\",\"parent\":\"mapmenu_6\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"close\":\"mapmenu_2\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\"}]},{\"name\":\"mapmenu_8\",\"parent\":\"mapmenu_2\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"Assets/Content/UI/UI.Background.Tile.psd\",\"color\":\"0.8431373 0.3372549 0.2509804 1\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0.8431373 0.3372549 0.2509804 1\",\"distance\":\"0 -1\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.03 0.07731612\",\"anchormax\":\"0.48 0.1364295\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"mapmenu_9\",\"parent\":\"mapmenu_8\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"♦ <b>Открыть карту</b>\",\"fontSize\":16,\"align\":\"MiddleCenter\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.2784314\",\"distance\":\"1 -1\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"button2\",\"parent\":\"mapmenu_8\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"map.open\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\"}]}]";

        void MenuUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "mapmenu_2");
            CuiHelper.AddUi(player, MapMenu.Replace("{version}", Version.ToString()));
            var container = new CuiElementContainer();
            double amin = 0.82;
            double amax = 0.91;
            if (friendsSupport)
            {
                var color = data.MapPlayerData[player.userID].Friends ? "0.85 0.34 0.25 1.00" : "0.33 0.33 0.33 1.00";
                CuiHelper.AddUi(player, button.Replace("{text}", "Друзья на карте").Replace("{command}", "cmd.menu friends").Replace("{amin}", amin.ToString()).Replace("{amax}", amax.ToString()).Replace("{color}", color));

                amin = (amin - 0.11);
                amax = (amax - 0.11);
            }
            if (clanSupport)
            {
                var color = data.MapPlayerData[player.userID].Clans ? "0.85 0.34 0.25 1.00" : "0.33 0.33 0.33 1.00";
                CuiHelper.AddUi(player, button.Replace("{text}", "Соклановцы на карте").Replace("{command}", "cmd.menu clans").Replace("{amin}", amin.ToString()).Replace("{amax}", amax.ToString()).Replace("{color}", color));
                amin = (amin - 0.11);
                amax = (amax - 0.11);
            }
            if (SleepSup)
            {
                var color = data.MapPlayerData[player.userID].Sleep ? "0.85 0.34 0.25 1.00" : "0.33 0.33 0.33 1.00";
                CuiHelper.AddUi(player, button.Replace("{text}", "Спальники на карте").Replace("{command}", "cmd.menu sleep").Replace("{amin}", amin.ToString()).Replace("{amax}", amax.ToString()).Replace("{color}", color));
                amin = (amin - 0.11);
                amax = (amax - 0.11);
            }
            if (sethome)
            {
                var color = data.MapPlayerData[player.userID].Homes ? "0.85 0.34 0.25 1.00" : "0.33 0.33 0.33 1.00";
                CuiHelper.AddUi(player, button.Replace("{text}", "Отображение домов на карте").Replace("{command}", "cmd.menu homes").Replace("{amin}", amin.ToString()).Replace("{amax}", amax.ToString()).Replace("{color}", color));
                amin = (amin - 0.11);
                amax = (amax - 0.11);
            }
            var color1 = data.MapPlayerData[player.userID].Death ? "0.85 0.34 0.25 1.00" : "0.33 0.33 0.33 1.00";
            CuiHelper.AddUi(player, button.Replace("{text}", "Отображение последней смерти на карте").Replace("{command}", "cmd.menu death").Replace("{amin}", amin.ToString()).Replace("{amax}", amax.ToString()).Replace("{color}", color1));
            amin = (amin - 0.11);
            amax = (amax - 0.11);
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, MAP_ADMIN))
            {
                CuiHelper.AddUi(player, button.Replace("{text}", "Админ раздел").Replace("{command}", "").Replace("{amin}", amin.ToString()).Replace("{amax}", amax.ToString()).Replace("{color}", "0.85 0.34 0.25 0"));
                amin = (amin - 0.11);
                amax = (amax - 0.11);
                var color = data.MapPlayerData[player.userID].AllPlayers ? "0.85 0.34 0.25 1.00" : "0.33 0.33 0.33 1.00";
                CuiHelper.AddUi(player, button.Replace("{text}", "Отобразить всех игроков на карте").Replace("{command}", "cmd.menu allplayers").Replace("{amin}", amin.ToString()).Replace("{amax}", amax.ToString()).Replace("{color}", color));
                amin = (amin - 0.11);
                amax = (amax - 0.11);
                var color2 = data.MapPlayerData[player.userID].BanPlayers ? "0.85 0.34 0.25 1.00" : "0.33 0.33 0.33 1.00";
                CuiHelper.AddUi(player, button.Replace("{text}", "Отобразить забаненых игроков на карте").Replace("{command}", "cmd.menu banplayers").Replace("{amin}", amin.ToString()).Replace("{amax}", amax.ToString()).Replace("{color}", color2));
                amin = (amin - 0.11);
                amax = (amax - 0.11);
            }

        }

        [ConsoleCommand("cmd.menu")]
        private void CmdMenuControll(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            switch (arg.Args[0])
            {
                case "friends":
                    if (data.MapPlayerData[player.userID].Friends)
                    {
                        data.MapPlayerData[player.userID].Friends = false;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].Friends = true;
                    }
                    if (data.MapPlayerData[player.userID].Teams)
                    {
                        data.MapPlayerData[player.userID].Teams = false;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].Teams = true;
                    }
                    break;
                case "sleep":
                    if (data.MapPlayerData[player.userID].Sleep)
                    {
                        data.MapPlayerData[player.userID].Sleep = false;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].Sleep = true;
                    }
                    break;
                case "clans":
                    if (data.MapPlayerData[player.userID].Clans)
                    {
                        data.MapPlayerData[player.userID].Clans = false;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].Clans = true;
                    }
                    break;
                case "homes":
                    if (data.MapPlayerData[player.userID].Homes)
                    {
                        data.MapPlayerData[player.userID].Homes = false;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].Homes = true;
                    }
                    break;
                case "allplayers":
                    if (data.MapPlayerData[player.userID].AllPlayers)
                    {
                        data.MapPlayerData[player.userID].AllPlayers = false;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].AllPlayers = true;
                    }
                    break;
                case "banplayers":
                    if (data.MapPlayerData[player.userID].BanPlayers)
                    {
                        data.MapPlayerData[player.userID].BanPlayers = false;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].BanPlayers = true;
                    }
                    break;
                case "death":
                    if (data.MapPlayerData[player.userID].Death)
                    {
                        data.MapPlayerData[player.userID].Death = false;
                    }
                    else
                    {
                        data.MapPlayerData[player.userID].Death = true;
                    }
                    break;
            }
            MenuUI(player);
        }

        [ConsoleCommand("map.wipe")]
        private void MapWipeCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            LoadData();
            m_FileManager.WipeData();
            Interface.Oxide.ReloadPlugin(Title);
        }

        [ConsoleCommand("map.open")]
        void ConsoleMap(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!init)
            {
                SendReply(player, "В данный момент карта не работает!\nОбратитесь к администрации");
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
            return;
        }

        public bool IsActiveMap()
        {
            GenerateMapSave generateMap = Interface.Oxide.DataFileSystem.ReadObject<GenerateMapSave>(@"RustMap/" + "MapRender");
            if (generateMap == null || generateMap.MapSeed != mapSeed || generateMap.WorldSize != worldSize)
            {
                return false;
            }
            return true;
        }

        IEnumerator GenerateMap()
        {
            string mapName = MapName();
            byte[] MapData = MapRender.Render();
            yield return new WaitForSeconds(3);
            Stream stream = new MemoryStream(MapData);
            Image mapimg = Image.FromStream(stream);
            mapimg.Save(Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap/" + mapName + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
            generated = true;
            yield break;
        }

        IEnumerator CheckPlugin()
        {
            /*string pathMap = @"RustMap/" + "MapRender";

            if ((Interface.Oxide.DataFileSystem.ExistsDatafile(pathMap) && IsActiveMap()) || mapUrl.ToLower().Contains("http"))
            {
                Puts("Карта обнаружена, начинается инициализцая!");
                Initialize();
            }
            else if (MapGen)
            {
                Puts("Запущена генерация карты");
                yield return CommunityEntity.ServerInstance.StartCoroutine(GenerateMap());
                if (generated)
                {
                    Puts("Карта успешно сгенерирована, запущена инициализация");
                    Interface.Oxide.DataFileSystem.WriteObject(pathMap, new GenerateMapSave(World.Seed.ToString(), (int)World.Size));
                    Initialize();
                }
            }
            else
            {
                Puts("Включите Авто-Генерацию либо укажите ссылку на своё изоброжение.");
                Unload();
            }*/
            
            Puts("Карта обнаружена, начинается инициализцая!");
            Initialize();
            yield break;
        }

        private Timer mtimer;
        void OnServerInitialized()
        {
            PrintWarning($"-----------------------------------");
            PrintError($"                RustMap            ");
            PrintError($"          Version =  {Version}     ");
            PrintWarning($"------------------------------------");

            MapPlayerData = Interface.Oxide.DataFileSystem.GetFile("RustMap/MapPlayerData");
            LoadData();
            PermissionService.RegisterPermissions(this, new List<string>() { MAP_ADMIN, permissionName });
            LoadDefaultConfig();
            CommunityEntity.ServerInstance.StartCoroutine(CheckPlugin());
        }

        private void Initialize()
        {
            m_Instance = this;
            worldSize = (int)World.Size;
            mapSeed = World.Seed.ToString();
            var anchorMin = new Vector2(0.5f - mapSize * 0.5f, 0.5f - mapSize * 0.800f);
            var anchorMax = new Vector2(0.5f + mapSize * 0.5f, 0.5f + mapSize * 0.930f);
            mapJson = mapJson.Replace("{2}", $"{anchorMin.x} {anchorMin.y}").Replace("{3}", $"{anchorMax.x} {anchorMax.y}");
            mapIconTextJson = mapIconTextJson.Replace("{color}", textColor);
            mapIconTextJsonIcon = mapIconTextJsonIcon.Replace("{color1}", textColor1);
            InitFileManager();
            m_FileManager.StartCoroutine(DownloadMapImage());
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerInit(player);
            }
            mtimer = timer.Every(MapUpdate, () => { foreach (var mm in temporaryMarkers) if (mm.NeedRedraw()) { ++mm.counter; foreach (var sub in subscribers) DrawMapMarker(sub.Key, mm); } foreach (var sub in subscribers) { RedrawPlayers(sub.Value); } });
            BansUpdate();
            timer.Every(20f, BansUpdate);

            foreach (var entity in BaseNetworkable.serverEntities.Select(p => p as BaseEntity).Where(p => p != null))
                OnEntitySpawned(entity);
        }

        void LoadData()
        {
            try
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile("RustMap/ImagesList"))
                {
                    Dictionary<string, string> ImageList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>("RustMap/ImagesList");
                    images = ImageList;
                }
                else
                {
                    PrintWarning("Дата Изоброжений создана");
                    Interface.Oxide.DataFileSystem.WriteObject("RustMap/ImagesList", images);
                }
                data = Interface.GetMod().DataFileSystem.ReadObject<DataStorage>("RustMap/MapPlayerData");
            }
            catch
            {
                Interface.Oxide.DataFileSystem.WriteObject("RustMap/ImagesList", images);
                data = new DataStorage();
            }
        }

        void OnServerSave()
        {
            PlayerSaveData();
        }

        private void PlayerSaveData()
        {
            MapPlayerData.WriteObject(data);
        }

        class DataStorage
        {
            public Dictionary<ulong, MAPDATA> MapPlayerData = new Dictionary<ulong, MAPDATA>();
            public DataStorage() { }
        }

        DataStorage data;
        private DynamicConfigFile MapPlayerData;

        class MAPDATA
        {
            public string Name;
            public bool Homes;
            public bool Friends;
            public bool Clans;
            public bool Death;
            public bool AllPlayers;
            public bool Teams;
            public bool Sleep;
            public bool BanPlayers;
            public Vector3 CustomIcon;
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (!mapPlayers.ContainsKey(player)) mapPlayers[player] = MapPlayer.Create(player);
            if (!data.MapPlayerData.ContainsKey(player.userID))
            {

                data.MapPlayerData.Add(player.userID, new MAPDATA()
                {
                    Name = player.displayName,
                    Homes = false,
                    Friends = true,
                    Clans = false,
                    AllPlayers = false,
                    BanPlayers = false,
                    Death = true,
                    Teams = true,
                    Sleep = true,
                    CustomIcon = Vector3.zero
                });
            }
            else
            {
                data.MapPlayerData[player.userID].Name = player.displayName.ToString();
            }
        }

        void OnNewSave()
        {
            LoadData();
            PrintWarning("Обнаружен вайп. Обновляем карту!");
            Interface.Oxide.DataFileSystem.WriteObject("RustMap/Images", new Dictionary<string, FileInfo>());
        }

        void Unload()
        {
            if (m_FileManager != null)
            {
                m_FileManager.SaveData();

            }
            foreach (var pl in data.MapPlayerData)
            {
                if (pl.Value.CustomIcon != Vector3.zero)
                    pl.Value.CustomIcon = pl.Value.CustomIcon = Vector3.zero;
            }
            PlayerSaveData();

            if (!init) return;
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "map_mainImage"); ;
            }
            if (FileManagerObject != null)
                UnityEngine.Object.Destroy(FileManagerObject);

        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null) return;
            if (plane && entity is CargoPlane)
                AddTemporaryMarker("plane", true, planeIconSize, planeIconAlpha, entity.transform);

            if (planeDrop && entity is SupplyDrop)
            {
                AddTemporaryMarker("mapsupply", false, planeDropIconSize, planeDropIconAlpha, entity.transform, "name");
            }

            if (NewHeli && entity is CH47Helicopter)
            {
                AddTemporaryMarker("newheli", true, NewheliIconSize, NewheliIconAlpha, entity.transform);
            }
            if (NewHeliCrate && entity is HackableLockedCrate)
            {
                if (!(entity.GetParentEntity() is CargoShip))
                    AddTemporaryMarker("newhelicreate", false, NewHeliCrateIconSize, NewHeliCrateIconAlpha, entity.transform);
            }
            if (heli && entity is BaseHelicopter)
            {
                AddTemporaryMarker("heli", true, heliIconSize, heliIconAlpha, entity.transform);
            }
            if (ship && entity is CargoShip)
            {
                AddTemporaryMarker("ship", true, CargoIconSize, CargoIconAlpha, entity.transform);
            }
            if (heliDrop && entity is LockedByEntCrate)
            {
                AddTemporaryMarker("helidebris", false, heliDropIconSize, heliDropIconAlpha, entity.transform);
            }
            if (vendingMachine && entity is VendingMachine)
            {
                if (!NPCVendingMachine)
                    if (entity is NPCVendingMachine) return;

                if (vendingMachineEmpty || !((VendingMachine)entity).IsInventoryEmpty())
                {
                    AddTemporaryMarker("vending", false, vendingMachineIconSize, vendingMachineIconAlpha, entity.transform);
                }
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity?.net?.ID == null) return;
            if (entity is CargoPlane || entity is SupplyDrop || entity is BaseHelicopter || entity is HelicopterDebris || entity is VendingMachine)
            {
                var transform = entity.transform;
                var mm = temporaryMarkers.Find(p => p.transform == transform);
                if (mm != null)
                    RemoveTemporaryMarker(mm);
            }
        }

        void OpenMap(BasePlayer player)
        {
            if (!init || player == null)
            {
                SendReply(player, "В данный момент карта не работает!\nОбратитесь к администрации");
                return;
            }
            if (CloseMapTimer != null) CloseMapTimer.Destroy();
            CuiHelper.AddUi(player, mapJson);
            CuiHelper.AddUi(player, monumentsJson);
            foreach (var mm in temporaryMarkers)
                DrawMapMarker(player, mm);
            List<ulong> members = new List<ulong>();
            if (PermissionService.HasPermission(player.userID, MAP_ADMIN))
            {
                if (data.MapPlayerData[player.userID].BanPlayers == true)
                {
                    foreach (var mm in bannedMarkers.Values)
                        DrawMapMarker(player, mm);
                }
            }
            if (data.MapPlayerData[player.userID].AllPlayers)
            {
                mapPlayers[player].clanTeam = BasePlayer.activePlayerList.Where(p => p != player).Select(MapPlayer.Create).ToList();

            }
            else
            {
                mapPlayers[player].clanTeam = new List<MapPlayer>();
                if (clanSupport)
                {
                    if (data.MapPlayerData[player.userID].Clans)
                    {
                        var clanmates = Clans.Call("GetClanMembers", player.userID);
                        if (clanmates != null)
                            members.AddRange(clanmates as List<ulong>);
                    }
                }
                if (friendsSupport)
                {
                    if (data.MapPlayerData[player.userID].Teams)
                    {
                        var teams = GetTeamMembers(player);
                        if (teams != null)
                            members.AddRange(teams);
                    }

                    if (data.MapPlayerData[player.userID].Friends)
                    {
                        var friends = Friends?.Call("GetFriends", player.userID);
                        if (friends != null)
                            members.AddRange(friends as ulong[]);

                        var classes = PlayersClasses?.Call("GetPlayers", player.userID);
                        if (classes != null)
                            members.AddRange(classes as List<ulong>);
                    }
                }
            }
            var homes = GetHomes(player);
            if (sethome && homes != null)

                foreach (var home in homes)
                {
                    var anchors = ToAnchors(home.Value, sethomeIconSize);
                    if (data.MapPlayerData[player.userID].Homes == true)
                    {
                        DrawIconNull(player, "sethome" + home.Key, "sethome" + home.Key, anchors, images["sethome"], 1.0f, $"Дом: \"{home.Key}\"", 8, false);
                    }
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
            if (data.MapPlayerData[player.userID].CustomIcon != Vector3.zero)
            {

                var anchors = ToAnchors(data.MapPlayerData[player.userID].CustomIcon, 0.03f, "yes");
                DrawIconNull(player, "custom", "custom", anchors, images["custom"], 1.0f, "Ваша метка", 4, false);
            }
            if (members != null && members.Count > 0)
            {
                var onlineMembers = BasePlayer.activePlayerList.Where(p => members.Contains(p.userID) && p != player).ToList();
                mapPlayers[player].clanTeam.AddRange(onlineMembers.Select(MapPlayer.Create).ToList());
            }
            if (raidhomes)
            {
                var zones = GetRaidZones(player);
                if (zones != null)
                    foreach (var zone in zones)
                    {
                        var anchors = ToAnchors(zone, raidhomeSIze);
                        DrawIcon(player, "raidhome" + zone, "raidhome" + zone, anchors, images["raidhome"], 0.95f, null, 12, false);
                    }
            }

            if (SleepSup)
            {
                var bags = SleepingBag.FindForPlayer(player.userID, true);
                if (data.MapPlayerData[player.userID].Sleep)
                {
                    foreach (var bagg in bags)
                    {
                        var anchors = ToAnchors(bagg.transform.position, deathIconSize);
                        DrawIcon(player, "sleep" + bagg, "sleep" + bagg, anchors, images["sleep"], 0.95f, null, 12, false);
                    }
                }
            }



            if (playerDic.ContainsKey(player.userID))
            {
                if (data.MapPlayerData[player.userID].Death)
                {
                    var anchors = ToAnchors(playerDic[player.userID].ToVector3(), deathIconSize);
                    DrawIconNull(player, "death" + player.userID, "death" + player.userID, anchors, images["death"], 1.0f, $"Ты умер здесь", 10, false);
                }
            }
            subscribers[player] = mapPlayers[player];
            RedrawPlayers(mapPlayers[player]);

            CloseMapTimer = timer.Once(TimeToClose, () =>
            {
                if (player == null) return;
                CloseMap(player);
            });
        }


        private List<ulong> GetTeamMembers(BasePlayer player)
        {
            if (player.currentTeam == 0)
                return null;
            return RelationshipManager.Instance.FindTeam(player.currentTeam).members.Where(p => p.ToString() != player.UserIDString).ToList();
        }


        private Dictionary<ulong, string> playerDic = new Dictionary<ulong, string>();

        string permissionName = "rustmap.death";

        private object OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
                return null;
            if (playerDic.ContainsKey(player.userID))
            {
                playerDic.Remove(player.userID);
            }
            playerDic.Add(player.userID, player.transform.position.ToString());
            return null;
        }

        [PluginReference]
        Plugin NoEscape;

        List<Vector3> GetRaidZones(BasePlayer player)
        {
            return (List<Vector3>)NoEscape?.Call("ApiGetOwnerRaidZones", player.userID);
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
            //data.MapPlayerData[player.userID].CustomIcon = data.MapPlayerData[player.userID].CustomIcon = Vector3.zero;
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

        void CloseMap(BasePlayer player)
        {
            if (CloseMapTimer != null) timer.Destroy(ref CloseMapTimer);
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
                //if (png == null)
                //{
                    //PrintError($"{friend}", mp.Rotation.ToString());
                    //png = FriendPng(mp.Rotation - 2);
                //}
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
            if (png == null) return;

            if (destroy)
            {
                timer.Once(0.05f, () =>
                {
                    CuiHelper.DestroyUi(player, lastName);
                });
                CuiHelper.DestroyUi(player, lastName + "text");
            }
            CuiHelper.AddUi(player, Format(mapMenuJson, $"{MapText}"));
            CuiHelper.AddUi(player, Format(mapIconJson, name, anchors[0], anchors[1], png, alpha));
            if (!string.IsNullOrEmpty(text))
                CuiHelper.AddUi(player, Format(mapIconTextJson, name + "text", anchors[2], anchors[3], text.Replace("\"", ""), fontsize));
        }

        void DrawIconNull(BasePlayer player, string lastName, string name, string[] anchors, string png, float alpha, string text = null, int fontsize = 12, bool destroy = true)
        {
            if (destroy)
            {
                timer.Once(0.05f, () =>
                {
                    CuiHelper.DestroyUi(player, lastName);
                });
                CuiHelper.DestroyUi(player, lastName + "text");
            }
            CuiHelper.AddUi(player, Format(mapIconJson, name, anchors[0], anchors[1], png, alpha));
            if (!string.IsNullOrEmpty(text))
                CuiHelper.AddUi(player, Format(mapIconTextJsonIcon, name + "text", anchors[2], anchors[3], text.Replace("\"", ""), fontsize));
        }

        void DrawDeath(BasePlayer player, string lastName, string name, string[] anchors, string png, float alpha, string text = null, int fontsize = 12, bool destroy = true)
        {
            if (destroy)
            {
                timer.Once(0.05f, () =>
                {
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
            if (!images.ContainsKey(png))
            {
                PrintWarning("Image error");
                return;
            }
            //if (images[png] == null)
            //{
                //PrintError("PNG = NULL: " + png);
                //return;
            //}
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
                if (PermissionService.HasPermission(sub.Key.userID, MAP_ADMIN))
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

        void AddMarkerApi(string url, bool rotSupport, float size, float alpha, Transform transform, string name = "")
        {
            if (String.IsNullOrEmpty(name)) name = transform.GetInstanceID() + "Icon";

            MapMarker mp = MapMarker.Create(transform);
            mp.name = name;
            mp.size = size;
            mp.png = name;
            mp.rotSupport = rotSupport;
            mp.alpha = alpha;
            mp.fontsize = 12;
            mp.position = transform.position;
            temporaryMarkers.Add(mp);

            CommunityEntity.ServerInstance.StartCoroutine(LoadImage(name, url));
            PrintWarning("Generated new icon");
        }

        void OnPlayerBanned(Network.Connection connection, string reason)
        {
            var userId = connection.userid;
            if (bannedCache.Contains(userId)) return;

            var user = BasePlayer.FindByID(userId);
            if (user == null)
            {
                user = BasePlayer.FindSleeping(userId);
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
                    text = "АЭС";
                }
                else if (monument.name.Contains("military_tunnel_1"))
                {
                    png = "militarytunnel";
                    text = "Военные Туннели";
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
                    text = "Водоочистные";
                }
                else if (monument.name.Contains("water_well") && water)
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
                    text = "Город Бандитов";
                }
                else if (monument.name.Contains("radtown_small_3"))
                {
                    png = "radtown";
                    text = "РедТаун";
                }
                else if (monument.name.Contains("power_sub") && powersub)
                    png = "powersub";

                else if (monument.name.Contains("swamp") && swamp)
                    png = "swamp";

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
                    text = "Космодром";
                }

                else if (monument.name.Contains("OilrigAI2") && Oilrig)

                {
                    png = "OilrigAI";
                    text = "Нефтяная вышка 2";
                }

                else if (monument.name.Contains("OilrigAI") && Oilrig)

                {
                    png = "OilrigAI";
                    text = "Нефтяная вышка 1";
                }

                else if (monument.name.Contains("excavator_1"))

                {
                    png = "excavator";
                    text = "Экскаватор";
                }
                else
                {
                    //Puts($"Выключенные объекты согласно конфигурации: {monument.name}");
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

                        new CuiRectTransformComponent() { AnchorMin = anchors[0], AnchorMax = anchors[1] }
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
            Text = text,
            FontSize = monumentsFontSize,
            Align = TextAnchor.MiddleCenter,
        },
                        new CuiOutlineComponent()
                        {
                            Color = "0 0 0 1"
                        },
                        new CuiRectTransformComponent() { AnchorMin = anchors[2], AnchorMax = anchors[3] }
                    }
                    });
                }
            }
            if (SpawnsControl != null)
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
        Plugin SpawnsControl;

        Dictionary<Vector3, int> GetSpawnZones() => SpawnsControl.Call<Dictionary<Vector3, int>>("GetSpawnZones");

        Dictionary<string, Vector3> GetHomes(BasePlayer player)
        {
            var a1 = (Dictionary<string, Vector3>)NTeleportation?.Call("GetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a2 = (Dictionary<string, Vector3>)Teleport?.Call("ApiGetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a3 = (Dictionary<string, Vector3>)Teleportation?.Call("GetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a4 = (Dictionary<string, Vector3>)HomesGUI?.Call("GetPlayerHomes", player.UserIDString) ?? new Dictionary<string, Vector3>();
            return a1.Concat(a2).Concat(a3).Concat(a4).GroupBy(p => p.Key).ToDictionary(p => p.Key, p => p.First().Value);
        }

        Dictionary<Vector3, int> GetPlayerQuarries(ulong userId)
        {
            return CustomQuarry.Call("GetPlayerQuarries", userId) as Dictionary<Vector3, int>;
        }

        string[] ToAnchors(Vector3 position, float size, string yes = "")
        {
            Vector2 center = ToScreenCoords(position);
            if (yes == "yes") center.y = center.y + 0.02f;
            size *= 0.5f;
            return new[]
            {
                $"{center.x - size} {center.y - size}",
                $"{center.x + size} {center.y + size}",
                $"{center.x - 0.1} {center.y - size-0.04f}",
                $"{center.x + 0.1} {center.y - size+0.02}"
            };
        }

        /*Vector2 ToScreenCoords(Vector3 vec)
        {
            return new Vector2((vec.x + (int)World.Size * 0.5f) / (int)World.Size, (vec.z + (int)World.Size * 0.5f) / (int)World.Size);
        }*/
        
        Vector2 ToScreenCoords(Vector3 pos)
        {
            float pad = 2048 * 0.01f;
            pos*=0.85f;
            pos+=new Vector3(World.Size * 0.5f, 0, World.Size * 0.5f);
            pos+=new Vector3(pad * 0.5f, 0, pad * 0.5f);
            pos/=(World.Size+pad);
        
            return new Vector2(pos.x, pos.z);
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

        private string MapName()
        {
            string splitElement = @"\";
            var split = mapUrl.Split(splitElement[0]);
            return split[split.Count() - 1].Replace(".jpg", "");
        }

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

        private System.Random random = new System.Random();

        bool mapLoaded = true;

        IEnumerator DownloadMapImage()
        {
            if (!string.IsNullOrEmpty(mapUrl))
            {
                images[MapFilename] = mapUrl;

                if (images.ContainsKey(MapFilename))
                {
                    yield return CommunityEntity.ServerInstance.StartCoroutine(LoadImages());
                    mapLoaded = true;
                    yield break;
                }
            }
        }

        IEnumerator LoadImage(string imageName, string url)
        {
            yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(imageName, url, imageName == MapFilename + 1 ? 1440 : -1));
            if (!images.ContainsKey(imageName)) images.Add(imageName, url);
            images[imageName] = m_FileManager.GetPng(imageName);
        }

        IEnumerator LoadImages()
        {
            yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(MapFilename, images[MapFilename], MapFilename == MapFilename + 1 ? 1440 : -1));
            images[MapFilename] = m_FileManager.GetPng(MapFilename);

            foreach (var image in imagesKeys)
            {
                if (image == MapFilename) continue;
                yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(image, images[image], image == MapFilename + 1 ? 1440 : -1));
                images[image] = m_FileManager.GetPng(image);

            }

            mapJson = Format(mapJson, MapPng(), mapAlpha);
            FindStaticMarkers();
            init = true;
            Puts("Изображение карты успешно загружено!");
            Interface.Call("OnMapInitialized");
            m_FileManager.SaveData();
        }

        string MapFilename => $"{ConVar.Server.level}_{World.Seed}_{TerrainMeta.Size.x}";
        List<string> imagesKeys => images.Keys.ToList();
        string PlayerPng(int rot) => images[imagesKeys[rot - 1]];
        string FriendPng(int rot) => images[imagesKeys[14 + rot]];

        string PlanePng(int rot) => images[imagesKeys[31 + rot]];

        string MapPng() => images[MapFilename];
        [HookMethod("RaidHomePng")]
        string RaidHomePng() => images["raidhome"];

        public Dictionary<string, string> images = new Dictionary<string, string>()
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

            {"newheli1", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli1.png"},
            {"newheli2", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli2.png"},
            {"newheli3", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli3.png"},
            {"newheli4", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli4.png"},
            {"newheli5", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli5.png"},
            {"newheli6", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli6.png"},
            {"newheli7", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli7.png"},
            {"newheli8", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli8.png"},
            {"newheli9", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli9.png"},
            {"newheli10", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli10.png"},
            {"newheli11", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli11.png"},
            {"newheli12", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli12.png"},
            {"newheli13", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli13.png"},
            {"newheli14", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli14.png"},
            {"newheli15", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli15.png"},
            {"newheli16", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newheli16.png"},

            {"ship1", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship1.png"},
            {"ship2", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship2.png"},
            {"ship3", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship3.png"},
            {"ship4", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship4.png"},
            {"ship5", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship5.png"},
            {"ship6", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship6.png"},
            {"ship7", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship7.png"},
            {"ship8", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship8.png"},
            {"ship9", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship9.png"},
            {"ship10", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship10.png"},
            {"ship11", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship11.png"},
            {"ship12", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship12.png"},
            {"ship13", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship13.png"},
            {"ship14", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship14.png"},
            {"ship15", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship15.png"},
            {"ship16", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "ship16.png"},

            
            { "newhelicreate", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "newhelicreate.png"},
            
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
            {"special", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "special.png" },
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
            { "OilrigAI", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "OilrigAI.png" },
            { "excavator", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "excavator.png" },
            { "spawnLeft", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "spawnLeft" },
            { "spawnRight", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "spawnRight" },
            { "raidhome", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "raidhome.png" },
            { "mapsupply","file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "mapsupply.png" },
            { "avanpost","file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "avanpost.png" },
            { "helidebris", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "helidebris.png" },
            { "banned", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "banned.png" },
            { "vending", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "vending.png" },
            { "sethome", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "homes.png" },
            { "treasurebox", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "treasurebox.png" },
            { "death", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "death.png" },
            { "sleep", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "sleep.png" },
            { "meteor", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "meteor" },
            { "rad", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "radhouse.png" },
            { "quarry", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "quarry.png" },
            { "cquarry", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "cquarry.png" },
            { "dump", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "dump.png" },
            { "water", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "water.png" },
            { "custom", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "custom.png" },
            { "bandit", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "bandit.png" },
            { "swamp", "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "RustMap" + Path.DirectorySeparatorChar + "icons" + Path.DirectorySeparatorChar + "swamp.png" },
        };

        private GameObject FileManagerObject;
        private FileManager m_FileManager;

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
            int MaxActivesLoads = 6677;
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
                files.Clear();
                SaveData();
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

                using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return webRequest.SendWebRequest();
                    if (webRequest.isNetworkError || webRequest.isHttpError) { yield break; }
                    byte[] bytes = webRequest.downloadHandler.data;
                    var entityId = CommunityEntity.ServerInstance.net.ID;
                    var crc32 = FileStorage.server.Store(bytes, FileStorage.Type.png, entityId).ToString();
                    files[name].Png = crc32;
                }
                loaded++;
            }
        }

        #region MapGenerator

        public static class MapRender
        {
            private static readonly Vector3 StartColor = new Vector3(0.3243134f, 0.3970588f, 0.1956099f);
            private static readonly Vector4 WaterColor = new Vector4(0.2696689f, 0.4205476f, 0.5660378f, 1f);
            private static readonly Vector4 GravelColor = new Vector4(0.1397059f, 0.1326214f, 0.1140246f, 0.372f);
            private static readonly Vector4 DirtColor = new Vector4(0.3222273f, 0.375f, 0.2288603f, 1f);
            private static readonly Vector4 SandColor = new Vector4(1f, 0.8250507f, 0.4485294f, 1f);
            private static readonly Vector4 GrassColor = new Vector4(0.4509804f, 0.5529412f, 0.2705882f, 1f);
            private static readonly Vector4 ForestColor = new Vector4(0.5529412f, 0.44f, 0.2705882f, 1f);
            private static readonly Vector4 RockColor = new Vector4(0.4234429f, 0.4852941f, 0.3140138f, 1f);
            private static readonly Vector4 SnowColor = new Vector4(0.8088235f, 0.8088235f, 0.8088235f, 1f);
            private static readonly Vector4 PebbleColor = new Vector4(0.1215686f, 0.4196078f, 0.627451f, 1f);
            private static readonly Vector4 OffShoreColor = new Vector4(0.1662958f, 0.2593377f, 0.3490566f, 1f);
            private static readonly Vector3 SunDirection = Vector3.Normalize(new Vector3(0.95f, 227f * (float)Math.E / 215f, 2.37f));
            private static readonly Vector3 Half = new Vector3(0.5f, 0.5f, 0.5f);

            private static Terrain terrain;
            private static TerrainHeightMap terrainHeightMap;
            private static TerrainSplatMap terrainSplatMap;
            private static int mapRes;
            public static byte[] Render()
            {


                int waterOffset = 0;
                int halfWaterOffset = waterOffset / 2;

                var imageWidth = 0;
                var imageHeight = 0;
                var background = OffShoreColor;
                
                terrain = TerrainMeta.Terrain;
                terrainHeightMap = TerrainMeta.HeightMap;
                terrainSplatMap = TerrainMeta.SplatMap;

                if (terrainHeightMap == null || terrainSplatMap == null)
                    return null;
                mapRes = terrain.terrainData.alphamapResolution;
                if (mapRes <= 0)
                    return null;

                int widthWithWater = mapRes + halfWaterOffset;

                imageWidth = mapRes + waterOffset;
                imageHeight = mapRes + waterOffset;
                UnityEngine.Color[] colorArray = new UnityEngine.Color[imageWidth * imageHeight];
                Array2D<UnityEngine.Color> output = new Array2D<UnityEngine.Color>(colorArray, imageWidth, imageHeight);
                Parallel.For(-halfWaterOffset, imageHeight - halfWaterOffset, row =>
                {
                    for (int col = -halfWaterOffset; col < widthWithWater; col++)
                    {
                        float terrainHeight = GetHeight(row, col);
                        float sun = Math.Max(Vector3.Dot(GetNormal(row, col), SunDirection), 0.0f);
                        Vector3 pixel = Vector3.Lerp(StartColor, GravelColor, GetSplat(row, col, 128) * GravelColor.w);
                        pixel = Vector3.Lerp(pixel, PebbleColor, GetSplat(row, col, 64) * PebbleColor.w);
                        pixel = Vector3.Lerp(pixel, RockColor, GetSplat(row, col, 8) * RockColor.w);
                        pixel = Vector3.Lerp(pixel, DirtColor, GetSplat(row, col, 1) * DirtColor.w);
                        pixel = Vector3.Lerp(pixel, GrassColor, GetSplat(row, col, 16) * GrassColor.w);
                        pixel = Vector3.Lerp(pixel, ForestColor, GetSplat(row, col, 32) * ForestColor.w);
                        pixel = Vector3.Lerp(pixel, SandColor, GetSplat(row, col, 4) * SandColor.w);
                        pixel = Vector3.Lerp(pixel, SnowColor, GetSplat(row, col, 2) * SnowColor.w);
                        float waterDepth = -terrainHeight;
                        if (waterDepth > 0.0f)
                        {
                            pixel = Vector3.Lerp(pixel, WaterColor, Mathf.Clamp(0.5f + waterDepth / 5.0f, 0.0f, 1f));
                            pixel = Vector3.Lerp(pixel, OffShoreColor, Mathf.Clamp(waterDepth / 50f, 0.0f, 1f));
                            sun = 0.5f;
                        }

                        pixel += (sun - 0.5f) * 0.5f * pixel;
                        pixel = (pixel - Half) * 0.87f + Half;
                        pixel *= 1f;

                        output.setValue(row + halfWaterOffset, col + halfWaterOffset, new Color(pixel.x, pixel.y, pixel.z));
                    }
                });
                background = output[0, 0];
                return EncodeToJPG(imageWidth, imageHeight, colorArray);

            }

            private static float GetHeight(int x, int y)
            {
                return terrainHeightMap.GetHeight(x, y);
            }

            private static Vector3 GetNormal(int x, int y)
            {
                return terrainHeightMap.GetNormal(x, y);
            }

            private static float GetSplat(int x, int y, int mask)
            {
                return terrainSplatMap.GetSplat(x, y, mask);
            }

            private static byte[] EncodeToJPG(int width, int height, UnityEngine.Color[] pixels)
            {
                Texture2D tex = null;
                try
                {
                    tex = new Texture2D(width, height);
                    tex.SetPixels(pixels);
                    tex.Apply();
                    return tex.EncodeToJPG(85);
                }
                finally
                {
                    if (tex != null)
                        UnityEngine.Object.Destroy(tex);
                }
            }

            public struct Array2D<T>
            {
                public T[] _items;
                public int _width;
                public int _height;

                public Array2D(T[] items, int width, int height)
                {
                    _items = items;
                    _width = width;
                    _height = height;
                }


                public void setValue(int x, int y, T value)
                {
                    int num = Mathf.Clamp(x, 0, this._width - 1);
                    _items[Mathf.Clamp(y, 0, this._height - 1) * this._width + num] = value;

                }

                public T this[int x, int y]
                {
                    get
                    {
                        int num = Mathf.Clamp(x, 0, this._width - 1);
                        return _items[Mathf.Clamp(y, 0, this._height - 1) * this._width + num];
                    }
                }
            }
        }

        #endregion

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
    }
}