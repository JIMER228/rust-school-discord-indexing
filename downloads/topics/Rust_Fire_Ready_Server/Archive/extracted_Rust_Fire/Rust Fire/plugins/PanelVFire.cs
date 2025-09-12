using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PanelVFire", "fermenspwnz", "0.0.5")]
    [Description("Панелька FireEdition.")]
    class PanelVFire : RustPlugin
    {

        Plugin ImageLibrary => Interface.Oxide.RootPluginManager.GetPlugin("ImageLibrary");
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        List<ulong> _players = new List<ulong>();

        #region Config
        static Dictionary<string, string> imagelist = new Dictionary<string, string>
        {
            {"https://gspics.org/images/2019/03/11/mUaUn.png","chelnok"},
            {"https://gspics.org/images/2020/01/31/5YWiN.png","heli"},
            {"https://gspics.org/images/2020/01/31/5Ywx7.png","plane"},
            {"https://gspics.org/images/2019/03/11/mUOGE.png","cargo"},
            {"https://gspics.org/images/2019/03/24/UCU8o.png","tank"}
        };


        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Информационная строка (если пусто, то выключена)")]
            public List<string> messages;
            [JsonProperty("Информационная строка | Частота обновлений в секундах")]
            public float infotimer;
            [JsonProperty("Нижняя строка")]
            public string down;
            [JsonProperty("Верхняя строка")]
            public string up;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    messages = new List<string>
                    {
                        "Максимум человек в команде - <color=#ff9e59>[ 3 ]</color>",
                        "Выбрать скин для оружия <color=#ff9e59>/skin</color>"
                    },
                    infotimer = 60f,
                    up = "VK.COM/SETFPS",
                    down = "Информация о сервере <color=#ff9e59>VK.COM/SETFPS</color>"
                };
            }
        }
        #endregion

        #region Function
        string[] panels = { "boat", "tank", "FarmGUI3", "online", "plane", "helis", "chelnok", "message" };
        void DestroyUI(Network.Connection player)
        {
            foreach (var z in panels) CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player }, null, "DestroyUI", z);
        }
        #endregion

        #region Hooks
        void Unload()
        {
            foreach (var z in panels) CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", z);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            NextTick(() =>
            {
                FarmGUI(TypeGui.Online);
            });
        }

        void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintWarning("Image Library не обнаружен, отгружаем Панель.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            GUIjsonfon = GUIjsonfon.Replace("{down}", config.down).Replace("{up}", config.up);
            foreach (var z in imagelist) AddImage(z.Key, z.Value);
            if (config.messages != null && config.messages.Count() > 0)
            {
                new PluginTimers(this).Every(config.infotimer, () => FarmGUI(TypeGui.Message));
            }
            ships = UnityEngine.Object.FindObjectsOfType<CargoShip>().Count();
            planes = UnityEngine.Object.FindObjectsOfType<CargoPlane>().Count();
            helicopters = UnityEngine.Object.FindObjectsOfType<BaseHelicopter>().Count();
            tanks = UnityEngine.Object.FindObjectsOfType<BradleyAPC>().Count();
            chinooks = UnityEngine.Object.FindObjectsOfType<CH47HelicopterAIController>().Count();
            timer.Once(1f, () => FarmGUI(TypeGui.All));
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                timer.Once(2, () => OnPlayerConnected(player));
                return;
            }
            NextTick(() =>
            {
                if(player == null) return;
                MainGUI(player.net.connection);
                FarmGUI(TypeGui.Online);
            });
        }



        int helicopters = 0;
        int planes = 0;
        int ships = 0;
        int tanks = 0;
        int chinooks = 0;

        void OnEntityKill(BaseNetworkable Entity)
        {
            if (Entity == null) return;
            if (Entity is BaseHelicopter)
            {
                helicopters--;
                if (helicopters == 0) FarmGUI(TypeGui.Heli);
            }
            else if (Entity is CargoPlane)
            {
                planes--;
                if (planes == 0) FarmGUI(TypeGui.Plane);
            }
            else if (Entity is CargoShip)
            {
                ships--;
                if (ships == 0) FarmGUI(TypeGui.Ship);
            }
            else if (Entity is BradleyAPC)
            {
                tanks--;
                if (tanks == 0) FarmGUI(TypeGui.Tank);
            }
            else if (Entity is CH47HelicopterAIController)
            {
                chinooks--;
                if (chinooks == 0) FarmGUI(TypeGui.Chinook);
            }
        }

        private void OnEntitySpawned(BaseNetworkable Entity)
        {
            if (Entity == null) return;
            if (Entity is BaseHelicopter)
            {
                helicopters++;
                if (helicopters == 1) FarmGUI(TypeGui.Heli);
            }
            else if (Entity is CargoPlane)
            {
                planes++;
                if (planes == 1) FarmGUI(TypeGui.Plane);
            }
            else if (Entity is CargoShip)
            {
                ships++;
                if (ships == 1) FarmGUI(TypeGui.Ship);
            }
            else if (Entity is BradleyAPC)
            {
                tanks++;
                if (tanks == 1) FarmGUI(TypeGui.Tank);
            }
            else if (Entity is CH47HelicopterAIController)
            {
                chinooks++;
                if (chinooks == 1) FarmGUI(TypeGui.Chinook);
            }
        }
        #endregion

        [ConsoleCommand("gategui")]
        void gategui(ConsoleSystem.Arg arg)
        {
            ulong userid = arg.Connection.userid;
            if (cooldown.ContainsKey(userid) && cooldown[userid] > DateTime.Now)
            {
                arg.Player().Command("chat.add", 2, 0, "Не так часто!");
                return;
            }
            if (!_players.Contains(userid))
            {
                _players.Add(userid);
                CloseGUI(arg.Connection);
            }
            else
            {
                _players.Remove(userid);
                MainGUI(arg.Connection);
                cooldown[userid] = DateTime.Now.AddSeconds(10);
            }
        }

        #region GUI
        enum TypeGui
        {
            Message, All, Online, Heli, Tank, Chinook, Plane, Ship
        }

        void CloseGUI(Network.Connection connect)
        {
            DestroyUI(connect);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = connect }, null, "AddUI", GUIjsondisable);
        }

        Dictionary<ulong, DateTime> cooldown = new Dictionary<ulong, DateTime>();
        void MainGUI(Network.Connection connect)
        {
            if(connect == null) return;
            FarmGUI(TypeGui.All, new List<Network.Connection> { connect });
        } 



        string GUIjsonmessage = "[{\"name\":\"message\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"fadeIn\":\"1\",\"fontSize\":\"14\",\"color\":\"1 1 1 0.85\",\"text\":\"{text}\",\"align\":\"LowerCenter\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.3\",\"distance\":\"0.6 0.6\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.495 0\",\"anchormax\":\"0.495 0\",\"offsetmin\":\"-500 1\",\"offsetmax\":\"500 28\"}]}]";
        string GUIjsonfon = "[{\"name\":\"FarmGUI3\", \"parent\":\"Hud\", \"components\":[{\"type\":\"UnityEngine.UI.Image\", \"color\":\"0 0 0 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\"}]},{\"name\":\"7a37bb60454b43e995e914a1ccc042e8\",\"parent\":\"FarmGUI3\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"material\":\"assets/content/ui/namefontmaterial.mat\",\"color\":\"0.95 0.95 0.95 0.0125\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"205.6 32.2\",\"offsetmax\":\"405 58.7\"}]},{\"name\":\"aq212asd3sa\",\"parent\":\"FarmGUI3\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"fontSize\":\"14\",\"font\":\"robotocondensed-regular.ttf\",\"color\":\"1 1 1 1\",\"text\":\"{down}\",\"align\":\"UpperCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"205.6 3.2\",\"offsetmax\":\"427 31.7\"}]},{\"name\":\"aq2123sa\",\"parent\":\"FarmGUI3\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"font\":\"robotocondensed-regular.ttf\",\"fontSize\":\"16\",\"color\":\"1 1 1 0.85\",\"text\":\"{up}\",\"align\":\"LowerCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"205.6 58.7\",\"offsetmax\":\"405 83.7\"}]},{\"name\":\"123asdsasd\", \"parent\":\"FarmGUI3\", \"components\":[{\"type\":\"UnityEngine.UI.Image\", \"color\":\"0 0 0 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\"}]},{\"name\":\"c92e68d80c68458885a5c487f497b9f8\",\"parent\":\"FarmGUI3\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"gategui\",\"material\":\"assets/content/ui/namefontmaterial.mat\",\"color\":\"0.95 0.95 0.95 0.0125\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"407 32.2\",\"offsetmax\":\"427 58.7\"}]},{\"parent\":\"c92e68d80c68458885a5c487f497b9f8\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\">\",\"fontSize\":20,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\"}]}]";
        string GUIjsondisable = "[{\"name\":\"FarmGUI3\", \"parent\":\"Hud\", \"components\":[{\"type\":\"UnityEngine.UI.Image\", \"color\":\"0 0 0 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\"}]},{\"name\":\"c92e68d80c68458885a5c487f497b9f8\",\"parent\":\"FarmGUI3\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"gategui\",\"material\":\"assets/content/ui/namefontmaterial.mat\",\"color\":\"0.95 0.95 0.95 0.0075\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"407 32.2\",\"offsetmax\":\"427 58.7\"}]},{\"parent\":\"c92e68d80c68458885a5c487f497b9f8\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"<\",\"fontSize\":20,\"align\":\"MiddleCenter\", \"color\":\"1 1 1 0.15\"},{\"type\":\"RectTransform\"}]}]";
        string GUIjsononline = "[{\"name\":\"online\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"fontSize\":\"15\",\"color\":\"1 1 1 1\",\"font\":\"robotocondensed-regular.ttf\",\"text\":\"{text}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"210.6 33.7\",\"offsetmax\":\"262 57.2\"}]}]";
        string GUIjsononplane = "[{\"name\":\"plane\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"color\":\"{color}\",\"png\":\"{png}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"267.5 33.7\",\"offsetmax\":\"291 57.2\"}]}]";
        string GUIjsononship = "[{\"name\":\"boat\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"color\":\"{color}\",\"png\":\"{png}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"350 33.7\",\"offsetmax\":\"373.5 57.2\"}]}]";
        string GUIjsonontank = "[{\"name\":\"tank\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"color\":\"{color}\",\"png\":\"{png}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"377.5 33.7\",\"offsetmax\":\"401 57.2\"}]}]";
        string GUIjsononheli = "[{\"name\":\"helis\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"color\":\"{color}\",\"png\":\"{png}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"294.8 33.7\",\"offsetmax\":\"318.5 57.2\"}]}]";
        string GUIjsononchinook = "[{\"name\":\"chelnok\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"color\":\"{color}\",\"png\":\"{png}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"322.5 33.7\",\"offsetmax\":\"346 57.2\"}]}]"; // \"322.5 33.7\",\"offsetmax\":\"346 57.2\"

        //205.6 70.7\",\"offsetmax\":\"405 90.7
        private void FarmGUI(TypeGui funct = TypeGui.All, List<Network.Connection> sendto = null)
        {
            if(sendto == null) sendto = Network.Net.sv.connections.Where(x => !_players.Contains(x.userid)).ToList();
            if (funct == TypeGui.All)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "FarmGUI3");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "AddUI", GUIjsonfon);
            }
            if (funct == TypeGui.All || funct == TypeGui.Message)
            {
                string text = GUIjsonmessage.Replace("{text}", config.messages[Random.Range(0, config.messages.Count)]);
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "message");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "AddUI", text);
            }
            if (funct == TypeGui.All || funct == TypeGui.Online)
            {
                string text = GUIjsononline.Replace("{text}", $"{BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "online");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "AddUI", text);
            }
            if (funct == TypeGui.All || funct == TypeGui.Plane)
            {
                string gui = GUIjsononplane.Replace("{png}", GetImage("plane")).Replace("{color}", planes > 0 ? "0.2 1 1 0.7" : "1 1 1 0.85");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "plane");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "AddUI", gui);
            }
            if (funct == TypeGui.All || funct == TypeGui.Ship)
            {
                string gui = GUIjsononship.Replace("{png}", GetImage("cargo")).Replace("{color}", ships > 0 ? "0 0.7 1 0.7" : "1 1 1 0.85");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "boat");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "AddUI", gui);
            }
            if (funct == TypeGui.All || funct == TypeGui.Tank)
            {
                string gui = GUIjsonontank.Replace("{png}", GetImage("tank")).Replace("{color}", tanks > 0 ? "0.2 0.9 0.5 0.7" : "1 1 1 0.85");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "tank");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "AddUI", gui);
            }
            if (funct == TypeGui.All || funct == TypeGui.Heli)
            {
                string gui = GUIjsononheli.Replace("{png}", GetImage("heli")).Replace("{color}", helicopters > 0 ? "1 0 0 0.7" : "1 1 1 0.85");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "helis");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "AddUI", gui);
            }
            if (funct == TypeGui.All || funct == TypeGui.Chinook)
            {
                string gui = GUIjsononchinook.Replace("{png}", GetImage("chelnok")).Replace("{color}", chinooks > 0 ? "0 0.9 0 0.7" : "1 1 1 0.85");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "chelnok");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "AddUI", gui);
            }

        }
        #endregion
    }
}