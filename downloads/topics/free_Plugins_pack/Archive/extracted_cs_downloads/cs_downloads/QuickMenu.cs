using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Linq;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("QuickMenu", "Craft", "1.1.1")]
    class QuickMenu : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary, ServerRewards, Economics;
        private void OnServerInitialized()
		{
			if (ImageLibrary == null)return;
			ImageLibrary.Call("AddImage", configData.tplj, "logotp");
			ChatCommand();
		}
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "title text size")]
            public int bitizsize;

            [JsonProperty(PropertyName = "Title text content")]
            public string biaotineirong;

            [JsonProperty(PropertyName = "title min offset")]
            public string biaotiMin;

            [JsonProperty(PropertyName = "title max offset")]
            public string biaotimax;

            [JsonProperty(PropertyName = "background min offset")]
            public string zbjmin;

            [JsonProperty(PropertyName = "background max offset")]
            public string zbjmax;

            [JsonProperty(PropertyName = "background color")]
            public string zbjys;

            [JsonProperty(PropertyName = "Discord")]
            public string Discord;

            [JsonProperty(PropertyName = "Left title")]
            public string zuo1;

            [JsonProperty(PropertyName = "middle title 1")]
            public string zhong1;

            [JsonProperty(PropertyName = "middle title 2")]
            public string zhong2;

            [JsonProperty(PropertyName = "middle title 3")]
            public string zhong3;

            [JsonProperty(PropertyName = "right title")]
            public string you1;

            [JsonProperty(PropertyName = "Image URL")]
            public string tplj;

            [JsonProperty(PropertyName = "Image min offset")]
            public string tpzxpy;

            [JsonProperty(PropertyName = "Image max offset")]
            public string tpzdpy;

            [JsonProperty(PropertyName = "Image color")]
            public string tpys;

            [JsonProperty(PropertyName = "close button text size")]
            public int guanbisize;

            [JsonProperty(PropertyName = "close button text")]
            public string guanbiwz;

            [JsonProperty(PropertyName = "close button color")]
            public string guanbiys;

            [JsonProperty(PropertyName = "close button min offset")]
            public string guanbimin;

            [JsonProperty(PropertyName = "close button max offset")]
            public string guanbimax;

            [JsonProperty(PropertyName = "Middle button function")]
            public bool zhongjian = true;

            [JsonProperty(PropertyName = "online player")]
            public bool zaixianwanjia = true;

            [JsonProperty(PropertyName = "game time")]
            public bool yxtime = true;

            [JsonProperty(PropertyName = "actual time")]
            public bool xstime = true;

            [JsonProperty(PropertyName = "coin balance")]
            public bool jinbiyue = true;

            [JsonProperty(PropertyName = "Point balance")]
            public bool jifenyue = true;
			
			[JsonProperty(PropertyName = "Chat Open Ui command")]
            public string ChatOpenUiCmd = "q";
			
			[JsonProperty(PropertyName = "Chat Middle Key command")]
            public string ChatMiddleKeyCmd = "z";

           [JsonProperty(PropertyName = "= = = = = = = = = = = = = [ button settings] = = = = = = = = = = = = =")]
            public List<AnNiuData> AnNiset;
        }

         class AnNiuData
        {
            [JsonProperty(PropertyName = "button text size")]
            public int AnWeiZi;

            [JsonProperty(PropertyName = "button color")]
            public string Anyanse;

            [JsonProperty(PropertyName = "button text")]
            public string AnText;

            [JsonProperty(PropertyName = "button text color")]
            public string Anwbys;

            [JsonProperty(PropertyName = "Button Min Offset")]
            public string AnMin;

            [JsonProperty(PropertyName = "button max offset")]
            public string AnMax;

            [JsonProperty(PropertyName = "button command")]
            public string AnCommand;

        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                bitizsize = 45,
                biaotineirong = "<color=#FFcc00>QuickMenu</color>",
                biaotiMin = "0.025 0.9",
                biaotimax = "0.65 0.98",
                zbjmin = "0.025 0.05",
                zbjmax = "0.975 0.95",
                zbjys = "0 0 0 .5",
                Discord = "<size=26><color=#FFFFFFFF>Welcome join Discord: Discord@123</color></size>",
                zuo1 = "<size=20><color=#FFFFFFFF>Vip</color></size>",
                zhong1 = "<size=18><color=#FFFFFFFF>Call Vehicles</color></size>",
                zhong2 = "<size=20><color=#FFFFFFFF>Help</color></size>",
                zhong3 = "<size=20><color=#FFFFFFFF>Settings</color></size>",
                you1 = "<size=20><color=#FFFFFFFF>Teleportation</color></size>",
                tpzxpy = "0.4 0.02",
                tpzdpy = "0.55 0.2",
                tpys = "1 1 1 0.8",
                tplj = "",
                guanbisize = 26,
                guanbiwz = "close",
                guanbiys = "1 1 1 .7",
                guanbimin = "0.888 0.888",
                guanbimax = "0.99 0.99",

                AnNiset = new List<AnNiuData>
                {
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 14, AnText = "",  AnMin = "0.025 0.899", AnMax = "0.35 0.9",AnCommand = ""
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "RemoveTool",  AnMin = "0.02 0.76", AnMax = "0.16 0.82",AnCommand = "/remove"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "Kit",  AnMin = "0.17 0.76", AnMax = "0.31 0.82",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "BackPack",  AnMin = "0.02 0.69", AnMax = "0.16 0.75",AnCommand = "/backpack"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "Shop",  AnMin = "0.17 0.69", AnMax = "0.31 0.75",AnCommand = "/shop"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "addable1",  AnMin = "0.02 0.62", AnMax = "0.16 0.68",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "addable2",  AnMin = "0.17 0.62", AnMax = "0.31 0.68",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "addable3",  AnMin = "0.02 0.55", AnMax = "0.16 0.61",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "addable4",  AnMin = "0.17 0.55", AnMax = "0.31 0.61",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "addable5",  AnMin = "0.02 0.48", AnMax = "0.16 0.54",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "addable6",  AnMin = "0.17 0.48", AnMax = "0.31 0.54",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "addable7",  AnMin = "0.02 0.41", AnMax = "0.31 0.47",AnCommand = "/kit"
                    },
                    //VIPHX
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "VIP horizontal line",  AnMin = "0.02 0.349", AnMax = "0.15 0.35",AnCommand = ""
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "VIP1",  AnMin = "0.02 0.28", AnMax = "0.16 0.34",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "VIP2",  AnMin = "0.17 0.28", AnMax = "0.31 0.34",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "VIP3",  AnMin = "0.02 0.21", AnMax = "0.16 0.27",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "VIP4",  AnMin = "0.17 0.21", AnMax = "0.31 0.27",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "VIP5",  AnMin = "0.02 0.14", AnMax = "0.16 0.20",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "VIP6",  AnMin = "0.17 0.14", AnMax = "0.31 0.20",AnCommand = "/kit"
                    },
                    //ZJHX
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "vehicle horizontal line",  AnMin = "0.33 0.799", AnMax = "0.46 0.80",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "unlock vehicle",  AnMin = "0.52 0.80", AnMax = "0.63 0.84",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "vehicle1",  AnMin = "0.33 0.75", AnMax = "0.42 0.79",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "kill",  AnMin = "0.425 0.75", AnMax = "0.475 0.79",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "vehicle2",  AnMin = "0.485 0.75", AnMax = "0.575 0.79",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "kill",  AnMin = "0.58 0.75", AnMax = "0.63 0.79",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "vehicle3",  AnMin = "0.33 0.70", AnMax = "0.42 0.74",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "kill",  AnMin = "0.425 0.70", AnMax = "0.475 0.74",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "vehicle4",  AnMin = "0.485 0.70", AnMax = "0.575 0.74",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "kill",  AnMin = "0.58 0.70", AnMax = "0.63 0.74",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "vehicle5",  AnMin = "0.33 0.65", AnMax = "0.42 0.69",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "kill",  AnMin = "0.425 0.65", AnMax = "0.475 0.69",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "vehicle6",  AnMin = "0.485 0.65", AnMax = "0.575 0.69",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "kill",  AnMin = "0.58 0.65", AnMax = "0.63 0.69",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "vehicle7",  AnMin = "0.485 0.60", AnMax = "0.575 0.64",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "kill",  AnMin = "0.58 0.60", AnMax = "0.63 0.64",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "vehicle8",  AnMin = "0.33 0.60", AnMax = "0.42 0.64",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "kill",  AnMin = "0.425 0.60", AnMax = "0.475 0.64",AnCommand = "/kit"
                    },
                    //BZHX
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "Help horizontal line",  AnMin = "0.33 0.549", AnMax = "0.46 0.55",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "Help 1",  AnMin = "0.33 0.5", AnMax = "0.475 0.54",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "Help 2",  AnMin = "0.485 0.5", AnMax = "0.63 0.54",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "Help 3",  AnMin = "0.33 0.45", AnMax = "0.475 0.49",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "Help 4",  AnMin = "0.485 0.45", AnMax = "0.63 0.49",AnCommand = "/kit"
                    },
                    //SZHX
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "settings horizontal line",  AnMin = "0.33 0.399", AnMax = "0.46 0.40",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "settings 1",  AnMin = "0.33 0.35", AnMax = "0.42 0.39",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "settings 2",  AnMin = "0.425 0.35", AnMax = "0.525 0.39",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "settings 3",  AnMin = "0.53 0.35", AnMax = "0.63 0.39",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "settings 4",  AnMin = "0.33 0.3", AnMax = "0.42 0.34",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "settings 5",  AnMin = "0.425 0.3", AnMax = "0.525 0.34",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "settings 6",  AnMin = "0.53 0.3", AnMax = "0.63 0.34",AnCommand = "/kit"
                    },
                    //TPHX
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP horizontal line",  AnMin = "0.65 0.799", AnMax = "0.77 0.8",AnCommand = "/kit"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP friend",  AnMin = "0.65 0.73", AnMax = "0.98 0.79",AnCommand = "/fmenu"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "Home list",  AnMin = "0.65 0.68", AnMax = "0.98 0.72",AnCommand = "/home list"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 18, AnText = "save home 1",  AnMin = "0.65 0.63", AnMax = "0.72 0.67",AnCommand = "/home add 1"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.725 0.63", AnMax = "0.785 0.67",AnCommand = "/home 1"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.79 0.63", AnMax = "0.81 0.67",AnCommand = "/home remove 1"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 18, AnText = "save home 2",  AnMin = "0.82 0.63", AnMax = "0.89 0.67",AnCommand = "/home add 2"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.895 0.63", AnMax = "0.955 0.67",AnCommand = "/home 2"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.96 0.63", AnMax = "0.98 0.67",AnCommand = "/home remove 2"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 18, AnText = "save home 3",  AnMin = "0.65 0.58", AnMax = "0.72 0.62",AnCommand = "/home add 3"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.725 0.58", AnMax = "0.785 0.62",AnCommand = "/home 3"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.79 0.58", AnMax = "0.81 0.62",AnCommand = "/home remove 3"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 18, AnText = "save home 4",  AnMin = "0.82 0.58", AnMax = "0.89 0.62",AnCommand = "/home add 4"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.895 0.58", AnMax = "0.955 0.62",AnCommand = "/home 4"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.96 0.58", AnMax = "0.98 0.62",AnCommand = "/home remove 4"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 18, AnText = "save home 5",  AnMin = "0.65 0.53", AnMax = "0.72 0.57",AnCommand = "/home add 5"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.725 0.53", AnMax = "0.785 0.57",AnCommand = "/home 5"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.79 0.53", AnMax = "0.81 0.57",AnCommand = "/home remove 5"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 18, AnText = "save home 6",  AnMin = "0.82 0.53", AnMax = "0.89 0.57",AnCommand = "/home add 6"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.895 0.53", AnMax = "0.955 0.57",AnCommand = "/home 6"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.96 0.53", AnMax = "0.98 0.57",AnCommand = "/home remove 6"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 18, AnText = "save home 7",  AnMin = "0.65 0.48", AnMax = "0.72 0.52",AnCommand = "/home add 7"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.725 0.48", AnMax = "0.785 0.52",AnCommand = "/home 7"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.79 0.48", AnMax = "0.81 0.52",AnCommand = "/home remove 7"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 18, AnText = "save home 8",  AnMin = "0.82 0.48", AnMax = "0.89 0.52",AnCommand = "/home add 8"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.895 0.48", AnMax = "0.955 0.52",AnCommand = "/home 8"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.96 0.48", AnMax = "0.98 0.52",AnCommand = "/home remove 8"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 18, AnText = "save home 9",  AnMin = "0.65 0.43", AnMax = "0.72 0.47",AnCommand = "/home add 9"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.725 0.43", AnMax = "0.785 0.47",AnCommand = "/home 9"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.79 0.43", AnMax = "0.81 0.47",AnCommand = "/home remove 9"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 16, AnText = "save home 10",  AnMin = "0.82 0.43", AnMax = "0.89 0.47",AnCommand = "/home add 10"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.895 0.43", AnMax = "0.955 0.47",AnCommand = "/home 10"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.96 0.43", AnMax = "0.98 0.47",AnCommand = "/home remove 10"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 16, AnText = "save home 11",  AnMin = "0.65 0.38", AnMax = "0.72 0.42",AnCommand = "/home add 11"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.725 0.38", AnMax = "0.785 0.42",AnCommand = "/home 11"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.79 0.38", AnMax = "0.81 0.42",AnCommand = "/home remove 11"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 16, AnText = "save home 12",  AnMin = "0.82 0.38", AnMax = "0.89 0.42",AnCommand = "/home add 12"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.895 0.38", AnMax = "0.955 0.42",AnCommand = "/home 12"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.96 0.38", AnMax = "0.98 0.42",AnCommand = "/home remove 12"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 16, AnText = "save home 13",  AnMin = "0.65 0.33", AnMax = "0.72 0.37",AnCommand = "/home add 13"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.725 0.33", AnMax = "0.785 0.37",AnCommand = "/home 13"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.79 0.33", AnMax = "0.81 0.37",AnCommand = "/home remove 13"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 16, AnText = "save home 14",  AnMin = "0.82 0.33", AnMax = "0.89 0.37",AnCommand = "/home add 14"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.895 0.33", AnMax = "0.955 0.37",AnCommand = "/home 14"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.96 0.33", AnMax = "0.98 0.37",AnCommand = "/home remove 14"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 16, AnText = "save home 15",  AnMin = "0.65 0.28", AnMax = "0.72 0.32",AnCommand = "/home add 15"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.725 0.28", AnMax = "0.785 0.32",AnCommand = "/home 15"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.79 0.28", AnMax = "0.81 0.32",AnCommand = "/home remove 15"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 16, AnText = "save home 16",  AnMin = "0.82 0.28", AnMax = "0.89 0.32",AnCommand = "/home add 16"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "TP",  AnMin = "0.895 0.28", AnMax = "0.955 0.32",AnCommand = "/home 16"
                    },
                    new AnNiuData
                    {
                        Anyanse = "0.8 0.8 0.8 0.6", Anwbys = "1 1 1 0.9", AnWeiZi = 19, AnText = "X",  AnMin = "0.96 0.28", AnMax = "0.98 0.32",AnCommand = "/home remove 16"
                    },
                }
            };
            SaveConfig(config);
        }
		
		#region Config
        private bool Changed = false;

        private Dictionary<ulong, screen> QuickMenuInfo = new Dictionary<ulong, screen>();
        class screen
        {
            public bool open;
        }
		#endregion
		
		#region GUI Display
        private void OpenQuickMenu(BasePlayer player)
        {
            if (!QuickMenuInfo.ContainsKey(player.userID))
                ui(player);
				return;
        }

        private void DestroyQuickMenu(BasePlayer player)
        {
            if (QuickMenuInfo.ContainsKey(player.userID))
            if (QuickMenuInfo[player.userID].open)
            QuickMenuInfo[player.userID].open = false;
		    foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(current, "zhujiemian");
            }

        }
        #endregion
		
        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        void Unload()
        {
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(current, "zhujiemian");
                SaveData();
            }
        }

        void ui(BasePlayer player)
        {
            if (ImageLibrary == null)
            {
                PrintWarning("The ImageLibrary plugin is not installed, the QuickMenu does not work properly!");
                return;
            }
            CuiHelper.DestroyUi(player, "zhujiemian");
            var elements = new CuiElementContainer();
            var zaixianwanjia = BasePlayer.activePlayerList.Count;
            var youxishijian = TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm");
            var xianshishijian = System.DateTime.Now.ToString("HH:mm");
            var jinbiyue = (Interface.Oxide.CallHook("Balance", player.UserIDString) ?? 0.0);
            var jifenyue = (Interface.Oxide.CallHook("CheckPoints", player.userID) ?? 0);

            var QuickUI = elements.Add(new CuiPanel {Image ={ FadeIn = 0.5f,Material = "assets/content/ui/uibackgroundblur.mat",Color = "0 0 0 0.5" },RectTransform = { AnchorMax = configData.zbjmax, AnchorMin = configData.zbjmin},CursorEnabled = true,}, "Overlay", "zhujiemian");
            elements.Add(new CuiLabel { Text = { Text = configData.biaotineirong, FontSize = configData.bitizsize, Color = "1 1 1 1"}, RectTransform = { AnchorMax = configData.biaotimax, AnchorMin = configData.biaotiMin } }, "zhujiemian");
            if (configData.zaixianwanjia == true){ elements.Add(new CuiLabel { Text = { Text =$"<b>Online players: {zaixianwanjia}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.17", AnchorMax = "0.98 0.20" } }, "zhujiemian");}
            if (configData.yxtime == true){ elements.Add(new CuiLabel { Text = { Text =$"<b>game time: {youxishijian}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.135", AnchorMax = "0.98 0.165" } }, "zhujiemian");}
            if (configData.yxtime == true){ elements.Add(new CuiLabel { Text = { Text =$"<b>actual time: {xianshishijian}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.09", AnchorMax = "0.98 0.130"} }, "zhujiemian");}
            if (Economics != null && configData.jinbiyue == true){elements.Add(new CuiLabel { Text = { Text =$"<b>coin balance：{jinbiyue}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.055", AnchorMax = "0.98 0.095"} }, "zhujiemian");}
            if (ServerRewards != null && configData.jifenyue == true){elements.Add(new CuiLabel { Text = { Text =$"<b>Point balance：{jifenyue}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.01", AnchorMax = "0.98 0.06"} }, "zhujiemian");}
            elements.Add(new CuiLabel { Text = { Text = configData.Discord , FontSize = 15, Color = "1 1 1 1" }, RectTransform = {AnchorMin = "0.025 0.8", AnchorMax = "0.5 0.89"} }, "zhujiemian");
            elements.Add(new CuiLabel { Text = { Text = configData.zuo1 , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.02 0.34", AnchorMax = "0.31 0.385"} }, "zhujiemian");
            elements.Add(new CuiLabel { Text = { Text = configData.zhong1 , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.33 0.80", AnchorMax = "0.63 0.835"} }, "zhujiemian");
            elements.Add(new CuiLabel { Text = { Text = configData.zhong2 , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.33 0.55", AnchorMax = "0.63 0.585"} }, "zhujiemian");
            elements.Add(new CuiLabel { Text = { Text = configData.zhong3 , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.33 0.4", AnchorMax = "0.63 0.435"} }, "zhujiemian");
            elements.Add(new CuiLabel { Text = { Text = configData.you1 , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.65 0.80", AnchorMax = "0.98 0.84"} }, "zhujiemian");
            elements.Add(new CuiElement{Parent = "zhujiemian",Components ={new CuiRawImageComponent{Color = configData.tpys, Png = (string) ImageLibrary.Call("GetImage", "logotp"),},new CuiRectTransformComponent { AnchorMin = configData.tpzxpy, AnchorMax = configData.tpzdpy },}});
            elements.Add(new CuiButton { Button = { Command = "closeui", Color = configData.guanbiys }, RectTransform = { AnchorMax = configData.guanbimax, AnchorMin = configData.guanbimin }, Text = { Text = configData.guanbiwz, Color = "1 1 1 0.7", FontSize = configData.guanbisize, Align=TextAnchor.MiddleCenter } }, QuickUI);
            
            int Max_sl = configData.AnNiset.Count;
            for (int i = 0; i < Max_sl; i++)
            {
                var data = configData.AnNiset[i];
                {
                   elements.Add(new CuiButton { Button = { Command = $"closeui1 chat.say \"{data.AnCommand}\"", Color = data.Anyanse }, RectTransform = { AnchorMax = data.AnMax, AnchorMin = data.AnMin }, Text = { Text = data.AnText, Color = data.Anwbys, FontSize = data.AnWeiZi, Align=TextAnchor.MiddleCenter } }, QuickUI);
                }

            }
            CuiHelper.AddUi(player, elements);
        }

        private StoredData _storedData; 
        void Init()
        {    
            if (!LoadConfigVariables())
            {
                PrintError("There is an error in the configuration file, please check the configuration file and fix it! ! !");
                return;
            }
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("QuickMenu");
        }

        private class StoredData
        {
            public Hash<ulong, bool> MiddleMouseButtonEnable = new Hash<ulong, bool>();
        }

        void OnPlayerConnected(BasePlayer player)
        {
           if (!_storedData.MiddleMouseButtonEnable.ContainsKey(player.userID))
            {
                _storedData.MiddleMouseButtonEnable[player.userID] = true;
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("QuickMenu", _storedData);

		void OnPlayerInput(BasePlayer player, InputState input)
        {
		    if (!input.WasJustReleased(BUTTON.FIRE_THIRD) || player == null) return;if (_storedData.MiddleMouseButtonEnable[player.userID] == false) return;if (configData.zhongjian == false) return;
	    	screen cdinfo;if (!QuickMenuInfo.TryGetValue(player.userID, out cdinfo)){QuickMenuInfo[player.userID] = cdinfo = new screen();}cdinfo.open = !cdinfo.open;if (cdinfo.open){ui(player);}else{DestroyQuickMenu(player);}
        }

        private void OnServerSave()
        {
            SaveData();
        }
		
		private void ChatCommand()
        {
            cmd.AddChatCommand(configData.ChatOpenUiCmd, this, "callui");
			cmd.AddChatCommand(configData.ChatMiddleKeyCmd, this, "zjgn");
        }

        void callui(BasePlayer player)
        {
            screen cdinfo;if (!QuickMenuInfo.TryGetValue(player.userID, out cdinfo)){QuickMenuInfo[player.userID] = cdinfo = new screen();}cdinfo.open = !cdinfo.open;if (cdinfo.open){ui(player);}else{DestroyQuickMenu(player);}
        }

        void zjgn(BasePlayer player)
        {
            if (configData.zhongjian == false)
            {
                player.ChatMessage($"<color=#FFCC00>【QuickMenu】</color>The administrator has disabled the middle mouse button function");
                return;
            } 

            if (_storedData.MiddleMouseButtonEnable[player.userID] == true)
            {
                _storedData.MiddleMouseButtonEnable[player.userID] = false;
                player.ChatMessage($"<color=#FFCC00>【QuickMenu】</color>Middle button function is off");
            }
            else
            {
                _storedData.MiddleMouseButtonEnable[player.userID] = true;
                player.ChatMessage($"<color=#FFCC00>【QuickMenu】</color>Middle button function is on");
            }
        }
        
        [ConsoleCommand("closeui")]
        void callclose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)return;
            CuiHelper.DestroyUi(arg.Player(), "zhujiemian");
			QuickMenuInfo[player.userID].open = false;
        }

        [ConsoleCommand("closeui1")]
        private void callclose1(ConsoleSystem.Arg arg)
        {
            var cmd = "";
            var player = arg.Connection.player as BasePlayer;

            if (player == null)return;
            CuiHelper.DestroyUi(player, "zhujiemian");
			QuickMenuInfo[player.userID].open = false;
            cmd = string.Join(" ", arg.Args.Skip(0).ToArray());
            player.Command(cmd);
            Effect.server.Run("assets/prefabs/tools/flashlight/effects/turn_on.prefab", (BaseEntity)player, 0U, Vector3.zero, Vector3.zero);
        }
    }
}