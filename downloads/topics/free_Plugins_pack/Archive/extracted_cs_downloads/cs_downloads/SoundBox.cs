using System.Linq;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System.Collections;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using VLB;
using Network;
using UnityEngine.Networking;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("SoundBox", "Razor", "2.0.1")]
    [Description("Play some streams")]
    public class SoundBox : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary;
        private static SoundBox _;
        static System.Random random = new System.Random();
        public static Dictionary<string, FollowInfo> BoomBoxPlayerFollow = new Dictionary<string, FollowInfo>();
        private const string permAdmin = "soundbox.admin";
        private const string permUse = "soundbox.use";

        public class FollowInfo
        {
            public BoomBox boombox;
            public string CurrentName;
            public string CurrentUrl;
            public bool IsOn;
        }

        #region Init
        private void Init()
        {
            _ = this;
            RegisterPermissions();
        }

        private void OnServerInitialized()
        {
            InvokeHandler.Instance.StartCoroutine(DownloadSound());

            LoadImages();
        }

        private void LoadImages(int tries = 0)
        {
            if (tries > 10)
            {
                PrintWarning($"ImageLibrary failed to load images. Unloading Plugin!");
                covalence.Server.Command($"o.unload {Name}");
                return;
            }
            PrintWarning($"Waiting on ImageLibrary to load!");

            if (ImageLibrary == null)
            {
                tries++;
                timer.Once(10f, () => { LoadImages(tries); });
                return;
            }
            Dictionary<string, string> limagelist = new Dictionary<string, string>();
            limagelist.Add("SoundBoxBackgroundImage", configData.settings.BackgroundImage);

            PrintWarning("Sending Image List To LimgeLibrary");
            ImageLibrary?.Call("ImportImageList", Name, limagelist, 0UL, true, new Action(imagesReady));
        }

        private static string BackgroundImage;
        private static string ImageAdminPicks;
        private static string ImageHeader;
        private static string ImageSongButton;
        private static string ImageRemoveButton;

        private void imagesReady()
        {
            BackgroundImage = ImageLibrary?.Call<string>("GetImage", "SoundBoxBackgroundImage");
            PrintWarning("Images Sent to ImageLibrary And Ready");
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
                CuiHelper.DestroyUi(player, MainPanel);

            _ = null;
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permUse, this);
        }
        #endregion Init

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Image Links")]
            public Settings settings { get; set; }

            [JsonProperty(PropertyName = "General Settings")]
            public Settings2 settings2 { get; set; }

            [JsonProperty(PropertyName = "Musical Genres And Urls")]
            public Genres genres { get; set; }

            public class Settings
            {
                [JsonProperty(PropertyName = "Background Image URL")]
                public string BackgroundImage { get; set; }
            }

            public class Settings2
            {
                [JsonProperty(PropertyName = "Allow players to have there own list and URL's")]
                public bool AlowPlayerPicks { get; set; }
                [JsonProperty(PropertyName = "Players URL's must contain")]
                public List<string> MustContain { get; set; }
                [JsonProperty(PropertyName = "Players URL's can not contain")]
                public List<string> BlockedUrls { get; set; }
                [JsonProperty(PropertyName = "Total allowed URL's the player can have in his list")]
                public int TotalAllowedPlayerUrls { get; set; }
            }

            public class Genres
            {
                [JsonProperty(PropertyName = "Musical genres")]
                public Dictionary<string, List<genresInfo>> lists { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                settings = new ConfigData.Settings
                {
                    BackgroundImage = "https://i.ibb.co/wpv4GZx/soundbox.jpg"  //https://i.ibb.co/JKs9163/background.png
                },

                settings2 = new ConfigData.Settings2
                {
                    MustContain = new List<string>() { "http" },
                    BlockedUrls = new List<string>() { ".php", ".htm", "?" },
                    TotalAllowedPlayerUrls = 150
                },

                genres = new ConfigData.Genres
                {
                    lists = new Dictionary<string, List<genresInfo>>()
                    {
                        { "Rock", new List<genresInfo>() { } },
                        { "Disco", new List<genresInfo>() {  } },
                        { "Pop", new List<genresInfo>() { } },
                        { "Hip hop", new List<genresInfo>() { } },
                        { "Jazz", new List<genresInfo>() { } },
                        { "Blues", new List<genresInfo>() { } },
                        { "Rap", new List<genresInfo>() { } }
                    }
                },

                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(2, 0, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        public class genresInfo
        {
            [JsonProperty(PropertyName = "NAME")]
            public string urlName;
            [JsonProperty(PropertyName = "URL")]
            public string URL;
            [JsonProperty(PropertyName = "Contry code")]
            public string CountryCode;
        }
        #endregion Config

        #region Data
        public class TheData
        {
            public Dictionary<string, List<genresInfo>> SavedURLS = new Dictionary<string, List<genresInfo>>();
        }

        private bool DataFileExists(string path)
        {
            return Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/{path}");
        }

        private DynamicConfigFile GetDataFile(string path)
        {
            return Interface.Oxide.DataFileSystem.GetFile($"{Name}/{path}");
        }

        private bool TryLoadUrlList(string filename, out TheData data)
        {
            if (DataFileExists(filename))
            {
                var file = GetDataFile(filename);
                try
                {
                    data = file.ReadObject<TheData>();
                    return true;
                }
                catch (Exception ex)
                {
                    PrintError($"Error reading data from {file.Filename}: ${ex.Message}");
                }
            }
            data = new TheData();
            return false;
        }

        private bool TryLoadUrlCountry(string filename, out List<netRadio> data)
        {
            if (DataFileExists(filename))
            {
                var file = GetDataFile(filename);
                try
                {
                    data = file.ReadObject<List<netRadio>>();
                    return true;
                }
                catch (Exception ex)
                {
                    PrintError($"Error reading data from {file.Filename}: ${ex.Message}");
                }
            }
            data = new List<netRadio>();
            return false;
        }

        void SaveData(string filename, TheData data)
        {
            GetDataFile(filename).WriteObject(data);
        }

        void SaveDataUrl(string filename, List<netRadio> data)
        {
            GetDataFile(filename).WriteObject(data);
        }

        public class netRadio
        {
            public string name;
            public string url;
            public string tags;
        }
        #endregion Data

        #region Hooks
        private void CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            ModularCar vehicle = entity?.VehicleParent() as ModularCar;
            if (vehicle != null)
            {
                DestroyUI(player);
            }
        }
        private object OnBoomboxToggle(BoomBox boombox, BasePlayer player, bool toggle)
        {
            if (player == null || boombox == null || boombox.BaseEntity == null || !permission.UserHasPermission(player.UserIDString, permUse) && !permission.UserHasPermission(player.UserIDString, permAdmin))
                return null;

            if (boombox.BaseEntity is HeldBoomBox && !toggle)
                return null;

            if (!BoomBoxPlayerFollow.TryGetValue(player.UserIDString, out var myboombox))
            {
                myboombox = new FollowInfo() { boombox = boombox, CurrentName = "", CurrentUrl = "", IsOn = toggle };
                BoomBoxPlayerFollow.Add(player.UserIDString, myboombox);
            }
            else
                myboombox.boombox = boombox;

            myboombox.IsOn = toggle;

            BuildSounBoxUI(player);

            return false;
        }
        #endregion

        #region Commands
        public void ChangeUrl(BoomBox boxController, string url, BasePlayer player, bool autoReplay = true)
        {
            bool wasPlaying = false;
            if (url.ToLower().Contains("dropbox") && !url.ToLower().Contains("dropboxusercontent"))
            {
                url = url.Replace("dropbox", "dl.dropboxusercontent").Replace("www.", "").Replace("?dl=0", "").Replace("?dl=1", "").Trim();
            }

            if (boxController != null)
            {
                if (boxController.IsOn())
                {
                    wasPlaying = true;
                    boxController.ServerTogglePlay(false);
                }

                if (player != null)
                    boxController.AssignedRadioBy = player.userID;

                boxController.CurrentRadioIp = url;
                boxController.baseEntity.ClientRPC<string>((Connection)null, "OnRadioIPChanged", url);

                timer.Once(0.1f, () =>
                {
                    if (boxController != null)
                    {
                        if (autoReplay && wasPlaying && !boxController.IsOn())
                            boxController.ServerTogglePlay(true);
                        boxController.baseEntity.SendNetworkUpdateImmediate(true);
                    }
                });
            }
        }

        private string CombineWords(string oldstring)
        {
            string new_text = oldstring.Replace(" ", "_");
            return new_text;
        }
        #endregion HooksAndCommands

        #region New UI
        public const string MainPanel = "SoundBox.MainPanel";
        public const string MainPanelJon = "SoundBox.MainPanelJon";

        public const string MainPanelScroll = "SoundBox.MainPanelScroll";
        public const string MainPanelScroll1 = "SoundBox.MainPanelScroll1";
        public const string MainPanelScroll2 = "SoundBox.MainPanelScroll2";
        public const string MainBodyPanel = "SoundBox.MainBodyPanel";
        public const string MainPanelNav = "SoundBox.MainPanelNav";
        public const string MainPanelSub = "SoundBox.MainPanelSub";
        public const string MainPanelSub2 = "SoundBox.MainPanelSub2";
        public const string MainPanelSubSide = "SoundBox.MainPanelSubSide";
        public const string MainPanelSubSide1 = "SoundBox.MainPanelSubSide1";
        public const string MainPanelSubSide2 = "SoundBox.MainPanelSubSide2";
        public const string ButtonCUI = "SoundBox.ButtonCUI";
        public const string MainPanelPlay = "SoundBox.MainPanelPlay";
        public const string MainPanelPlayPanel = "SoundBox.MainPanelPlayPanel";

        public static ulong SecretKey = GenerateKey();
        public static ulong GenerateKey() => (ulong)UnityEngine.Random.Range(1, 9999999999999999);
        private const string command = "soundboxuicommand";
        private const string commandname = "soundboxuicommandname";
        private const string commandurl = "soundboxuicommandurl";

        private void BuildSounBoxUI(BasePlayer player, string config = "", int page = 1)
        {
            if (!BoomBoxPlayerFollow.TryGetValue(player.UserIDString, out var boombox))
                return;

            CuiElementContainer mainPanel = CreatePanel(MainPanel, "Overlay", "0 0 0 0", "0 0", "1 1", "0 0", "0 0");
            AddPanel(mainPanel, MainPanel, MainBodyPanel, "0 0 0 0.9", "0 0", "1 1", "0 0", "0 0");
            AddButton(mainPanel, MainBodyPanel, ButtonCUI, "", 14, "0 0 0 0", "0 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} close", "0 0", "1 1", "0 0", "0 0");
            AddPanel(mainPanel, MainBodyPanel, MainPanelSub, "0.192 0.180 0.184 0.9", "0.2 0.15", "0.8 0.85", "0 0", "0 0");
            AddImageUrl(mainPanel, MainPanelSub, "SoundBox.ImageBackground", BackgroundImage, "1 1 1 1", "0 0", "1 1");

            AddPanel(mainPanel, MainPanelSub, "SoundBox.Line", "0.541 0 0.768 1", "0.05 0.883", "0.95 0.889", "0 0", "0 0");

            CuiHelper.AddUi(player, mainPanel);
            if (string.IsNullOrEmpty(config))
                config = configData.genres.lists.Keys.First();
            BuildHeader(player, config);
            BuildAdminList(player, config, page);
            BuildHeaderButtons(player, config, page);
            PlayButton(player, boombox);
        }

        private void PlayButton(BasePlayer player, FollowInfo box)
        {
            CuiElementContainer mainPanel = CreatePanel(MainPanelPlay, MainPanelSub, "0 0 0 0", "0 0", "1 0.1", "0 0", "0 0");
            AddButton(mainPanel, MainPanelPlay, MainPanelPlayPanel, "", 0, "0 0 0 0", "0 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} toggleplay", "0.48 0.25", "0.52 0.75");
            CreateElement(mainPanel, MainPanelPlayPanel, $"SoundBox.Play", box.boombox.IsOn() ? "assets/icons/occupied.png" : "assets/icons/maximum.png", "0 0", "1 1", box.boombox.IsOn() ? "0.8 0 0 1" :"0.549 0.725 0.239 1", "");

            CuiHelper.AddUi(player, mainPanel);
        }

        private void BuildHeader(BasePlayer player, string config)
        {
            CuiElementContainer mainPanel = CreatePanel(MainPanelNav, MainPanelSub, "0.541 0 0.768 1", "0 0.95", "1 1");

            AddButton(mainPanel, MainPanelNav, "AdminPanel.Close2", lang.GetMessage("ExitMenu", this, player.UserIDString), 15, "1 1 0 1", "0 0 0 0", TextAnchor.MiddleRight, $"{command} {SecretKey} close", "0.8 0", "0.99 1");

            AddButton(mainPanel, MainPanelNav, "AdminPanel.Current", lang.GetMessage("SoundBox", this, player.UserIDString), 15, "1 1 0 1", "0 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey}", "0.4 0", "0.6 1");

            CuiHelper.AddUi(player, mainPanel);
        }

        private void BuildHeaderButtons(BasePlayer player, string config, int page = 1)
        {
            CuiElementContainer mainPanel = CreatePanel(MainPanelJon, MainPanelSub, "0 0 0 0", "0.09 0.897", "0.98 0.942");

            bool HasAdmin = permission.UserHasPermission(player.UserIDString, permAdmin);

            if (HasAdmin || configData.settings2.TotalAllowedPlayerUrls > 0)
            {
                AddButton(mainPanel, MainPanelNav, $"SoundBox.GearButton", "", 0, "1 1 1 1", "0.8 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} addurl {page - 1} {config}", "0.01 0.18", "0.03 0.82");
                CreateElement(mainPanel, $"SoundBox.GearButton", $"SoundBox.Gear", "assets/icons/gear.png", "0 0", "1 1", "1 1 1 1", "");
            }

            double a = 0, b = 0, c = 0.15, d = 1;
            int total = 0, count = 0;

            if (page + 6 > configData.genres.lists.Count)
                page = configData.genres.lists.Count - 5;
            else
                AddButton(mainPanel, MainPanelJon, $"SoundBox.pagenext", ">>", 20, "1 1 1 1", "0.8 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} pagenext {page + 1} {config}", "0.93 0", "0.96 1", "0 0", "0 0");
            if (page > 1)
                AddButton(mainPanel, MainPanelJon, $"SoundBox.pageprev", "<<", 20, "1 1 1 1", "0.8 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} pagenext {page - 1} {config}", "-0.035 0", "-0.005 1", "0 0", "0 0");

            foreach (var item in configData.genres.lists)
            {
                count++;
                if (count < page)
                    continue;

                total++;
                AddButton(mainPanel, MainPanelJon, $"SoundBox.genres{total}", lang.GetMessage(item.Key, this, player.UserIDString), 15, "1 1 1 1", item.Key == config ? "0.054 0.529 0.8 1" : "0.549 0.725 0.239 0.85", TextAnchor.MiddleCenter, $"{command} {SecretKey} genres {page} {item.Key}", $"{a} {b}", $"{c} {d}", "0 0", "0 0");
                a += 0.155; c += 0.155;
                if (total > 5) break;
            }

            CuiHelper.AddUi(player, mainPanel);
        }

        private void BuildAdminList(BasePlayer player, string config, int page = 1)
        {
            CuiElementContainer mainPanel = CreatePanel(MainPanelSubSide, MainPanelSub, "0 0 0 0", "0 0.1", "1 0.85", "0 0", "0 0");

            AddPanel(mainPanel, MainPanelSubSide, MainPanelScroll, "0.760 0.760 0.760 0", "0 0", "1 1", "0 0", "0 0"); //Needed to make mouse scrole work Heck If i Know Why
            Add_CuiScrollView(mainPanel, MainPanelScroll1, MainPanelScroll, "0 0", "1 1", "0 -5000", "0 0");
            AddPanel(mainPanel, MainPanelScroll1, MainPanelScroll2, "0.760 0.760 0.760 0", "0 0", "1 1", "0 0", "0 0");

            bool HasAdmin = permission.UserHasPermission(player.UserIDString, permAdmin);
            double a = 0.07, b = 0.996, c = 0.31, d = 1;
            int count = 0;
            int total = 0;

            if (!TryLoadUrlList($"PlayerSaves/{player.UserIDString}", out var data))
            {
                data.SavedURLS = new Dictionary<string, List<genresInfo>>();
                SaveData($"PlayerSaves/{player.UserIDString}", data);
            }

            string code = GetLanfPlayer(player);
            if (data.SavedURLS.TryGetValue(config, out var list))
            {
                foreach (var item in list)
                {
                    total++;
                    AddButton(mainPanel, MainPanelScroll2, $"SoundBox.url1{total}", $"{item.urlName}", 15, "1 1 1 1", "0.8 0.403 0.054 1", TextAnchor.MiddleCenter, $"{command} {SecretKey} station {item.URL} {item.urlName}", $"{a} {b}", $"{c} {d}", "0 0", "0 0");
                    AddButton(mainPanel, $"SoundBox.url1{total}", $"SoundBox.delete1{total}", "x", 9, "1 1 1 1", "0.8 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} userdelete {total - 1} {page} {config}", "0.95 0.5", "1 1", "0 0", "0 0");

                    count++;
                    a += 0.3; c += 0.3;
                    if (count == 3) { a = 0.07; b -= 0.006; c = 0.31; d -= 0.006; count = 0; }
                }
            }

            total = 0;

            foreach (var item in configData.genres.lists[config])
            {
                total++;
                if (!string.IsNullOrEmpty(item.CountryCode) && item.CountryCode != code)
                    continue;
                AddButton(mainPanel, MainPanelScroll2, $"SoundBox.urlList{total}", $"{item.urlName}", 15, "1 1 1 1", "0.8 0.403 0.054 1", TextAnchor.MiddleCenter, $"{command} {SecretKey} station {item.URL}", $"{a} {b}", $"{c} {d}", "0 0", "0 0");

                if (HasAdmin)
                    AddButton(mainPanel, $"SoundBox.urlList{total}", $"SoundBox.KillButton{total}", "x", 9, "1 1 1 1", "0.8 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} admindelete {total - 1} {page} {config}", "0.95 0.5", "1 1", "0 0", "0 0");

                count++;
                a += 0.3; c += 0.3;
                if (count == 3) { a = 0.07; b -= 0.006; c = 0.31; d -= 0.006; count = 0; }
            }

            total = 0;

            if (TryLoadUrlCountry($"StreamLists/{GetLanfPlayer(player)}/{config}", out var dataList))
            {
                foreach (var crap in dataList)
                {
                    total++;
                    AddButton(mainPanel, MainPanelScroll2, $"SoundBox.urlList{total}", $"{crap.name}", 15, "1 1 1 1", "0.8 0.403 0.054 1", TextAnchor.MiddleCenter, $"{command} {SecretKey} station {crap.url}", $"{a} {b}", $"{c} {d}", "0 0", "0 0");

                    count++;
                    a += 0.3; c += 0.3;
                    if (count == 3) { a = 0.07; b -= 0.006; c = 0.31; d -= 0.006; count = 0; }
                }
            }


            CuiHelper.AddUi(player, mainPanel);
        }

        private void BuildAddUrl(BasePlayer player, string config, int page)
        {
            bool HasAdmin = permission.UserHasPermission(player.UserIDString, permAdmin);

            CuiElementContainer mainPanel = CreatePanel(MainPanel, "Overlay", "0 0 0 0", "0 0", "1 1", "0 0", "0 0");
            AddPanel(mainPanel, MainPanel, MainBodyPanel, "0 0 0 0.9", "0 0", "1 1", "0 0", "0 0");
            AddButton(mainPanel, MainBodyPanel, ButtonCUI, "", 14, "0 0 0 0", "0 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} closeadd {page} {config}", "0 0", "1 1", "0 0", "0 0");
            AddPanel(mainPanel, MainBodyPanel, MainPanelSub, "0.054 0.529 0.8 1", "0.2 0.15", "0.8 0.85", "0 0", "0 0");
            CreateLable(mainPanel, MainPanelSub, "SoundBox.Information", lang.GetMessage("AddUrl", this, player.UserIDString), 18, "1 1 1 1", TextAnchor.UpperLeft, "0.2 0.5", "0.8 0.9");
            CreateLable(mainPanel, MainPanelSub, "SoundBox.Information", lang.GetMessage("AddUrlName", this, player.UserIDString), 18, "1 1 1 1", TextAnchor.MiddleLeft, "0.2 0.5", "0.5 0.55");
            AddPanel(mainPanel, MainPanelSub, "SoundBox.TextBoxName", "1 1 1 1", "0.2 0.45", "0.4 0.5", "0 0", "0 0");
            AddTextBox(mainPanel, "SoundBox.TextBoxName", "SoundBox.TextBoxName1", "0 0 0 1", "", 15, TextAnchor.MiddleLeft, 25, $"{commandname}", "0.005 0", "1 1");
            CreateLable(mainPanel, MainPanelSub, "SoundBox.InformationUrl", lang.GetMessage("AddUrLink", this, player.UserIDString), 18, "1 1 1 1", TextAnchor.MiddleLeft, "0.2 0.37", "0.5 0.42");
            AddPanel(mainPanel, MainPanelSub, "SoundBox.TextBoxUrl", "1 1 1 1", "0.2 0.32", "0.75 0.37", "0 0", "0 0");
            AddTextBox(mainPanel, "SoundBox.TextBoxUrl", "SoundBox.TextBoxUrl1", "0 0 0 1", "", 15, TextAnchor.MiddleLeft, 500, $"{commandurl}", "0.005 0", "1 1");

            AddButton(mainPanel, MainPanelSub, "SoundBox.TextBoxSave", lang.GetMessage("Save", this, player.UserIDString), 16, "1 1 1 1", "0.549 0.725 0.239 1", TextAnchor.MiddleCenter, $"{command} {SecretKey} saveurl {page} {config}", "0.2 0.15", "0.3 0.2", "0 0", "0 0");
            AddButton(mainPanel, MainPanelSub, "SoundBox.TextBoxCancel", lang.GetMessage("Cancel", this, player.UserIDString), 16, "1 1 1 1", "0.8 0 0 1", TextAnchor.MiddleCenter, $"{command} {SecretKey} closeadd {page} {config}", "0.7 0.15", "0.8 0.2", "0 0", "0 0");

            if (HasAdmin)
                AddButton(mainPanel, MainPanelSub, "SoundBox.TextBoxAdminSave", lang.GetMessage("AdminSave", this, player.UserIDString), 16, "1 1 1 1", "0.549 0.725 0.239 1", TextAnchor.MiddleCenter, $"{command} {SecretKey} adminsaveurl {page} {config}", "0.45 0.15", "0.55 0.2", "0 0", "0 0");

            CuiHelper.AddUi(player, mainPanel);
            BuildHeaderUrl(player, config, page);
        }

        private void BuildHeaderUrl(BasePlayer player, string config, int page)
        {
            CuiElementContainer mainPanel = CreatePanel(MainPanelNav, MainPanelSub, "0.541 0 0.768 1", "0 0.95", "1 1");
            AddButton(mainPanel, MainPanelNav, "AdminPanel.Close2", lang.GetMessage("ExitMenu", this, player.UserIDString), 15, "1 1 0 1", "0 0 0 0", TextAnchor.MiddleRight, $"{command} {SecretKey} closeadd {page} {config}", "0.8 0", "0.99 1");
            CreateLable(mainPanel, MainPanelNav, "SoundBox.InformationDisplay", string.Format(lang.GetMessage("AddingFor", this, player.UserIDString), config.ToUpper()), 15, "1 1 1 1", TextAnchor.MiddleCenter, "0.3 0", "0.7 1");
            AddButton(mainPanel, MainPanelNav, "AdminPanel.Current", lang.GetMessage("SoundBox", this, player.UserIDString), 15, "1 1 0 1", "0 0 0 0", TextAnchor.MiddleLeft, $"{command} {SecretKey}", "0.01 0", "0.16 1");

            CuiHelper.AddUi(player, mainPanel);
        }
        #endregion

        #region Ui Commands
        private void DestroyUI(BasePlayer player)
        {
            if (player != null)
            {
                CuiHelper.DestroyUi(player, MainPanel);
                if (!BoomBoxPlayerFollow.ContainsKey(player.UserIDString))
                    BoomBoxPlayerFollow.Remove(player.UserIDString);
            }
        }

        [ConsoleCommand(command)]
        private void UiActionCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args == null || arg.Args.Length < 2)
                return;

            BasePlayer player = arg.Player();

            if (!ulong.TryParse(arg.Args[0], out var key))
                return;

            if (key != SecretKey)
                return;

            if (BoomBoxPlayerFollow.TryGetValue(player.UserIDString, out var boombox) && boombox.boombox == null)
            {
                DestroyUI(player);
            }

            if (player != null)
            {
                switch (arg.Args[1])
                {
                    case "close":
                        {
                            DestroyUI(player);
                            break;
                        }

                    case "genres":
                        {
                            int position = Int32.Parse(arg.Args[2]);
                            string config = string.Join(" ", arg.Args.Skip(3).ToArray()).Trim();
                            BuildHeaderButtons(player, config, position);
                            BuildAdminList(player, config, position);
                            break;
                        }

                    case "toggleplay":
                        {
                            boombox.boombox.ServerTogglePlay(!boombox.boombox.IsOn());

                            PlayButton(player, boombox);
                            break;
                        }

                    case "station":
                        {
                            ChangeUrl(boombox.boombox, arg.Args[2], player);
                            string name = string.Join(" ", arg.Args.Skip(3).ToArray()).Trim();
                            boombox.CurrentName = name;
                            boombox.CurrentUrl = arg.Args[2];
                            break;
                        }

                    case "pagenext":
                        {
                            int position = Int32.Parse(arg.Args[2]);
                            string config = string.Join(" ", arg.Args.Skip(3).ToArray()).Trim();
                            BuildHeaderButtons(player, config, position);
                            break;
                        }

                    case "addurl":
                        {
                            int position = Int32.Parse(arg.Args[2]);
                            string config = string.Join(" ", arg.Args.Skip(3).ToArray()).Trim();
                            BuildAddUrl(player, config, position);
                            break;
                        }

                    case "admindelete":
                        {
                            int position = Int32.Parse(arg.Args[2]);
                            int position2 = Int32.Parse(arg.Args[3]);
                            string config = string.Join(" ", arg.Args.Skip(4).ToArray()).Trim();

                            configData.genres.lists[config].RemoveAt(position);
                            SaveConfig();

                            BuildSounBoxUI(player, config, position2);
                            break;
                        }

                    case "userdelete":
                        {
                            int position = Int32.Parse(arg.Args[2]);
                            int position2 = Int32.Parse(arg.Args[3]);
                            string config = string.Join(" ", arg.Args.Skip(4).ToArray()).Trim();

                            if (!TryLoadUrlList($"PlayerSaves/{player.UserIDString}", out var data))
                                return;

                            data.SavedURLS[config].RemoveAt(position);
                            SaveData($"PlayerSaves/{player.UserIDString}", data);

                            BuildSounBoxUI(player, config, position2);
                            break;
                        }

                    case "closeadd":
                        {
                            int position = Int32.Parse(arg.Args[2]);
                            string config = string.Join(" ", arg.Args.Skip(3).ToArray()).Trim();
                            BuildSounBoxUI(player, config, position);
                            break;
                        }

                    case "saveurl":
                        {
                            if (!inputtext.TryGetValue(player.UserIDString, out var name) || string.IsNullOrEmpty(name))
                            {
                                SendReply(player, lang.GetMessage("errorName", this, player.UserIDString));
                                return;
                            }

                            if (!inputtexturl.TryGetValue(player.UserIDString, out var url) || string.IsNullOrEmpty(url))
                            {
                                SendReply(player, lang.GetMessage("errorUrl", this, player.UserIDString));
                                return;
                            }

                            foreach (var keyContain in configData.settings2.MustContain)
                            {
                                if (!url.Contains(keyContain))
                                {
                                    SendReply(player, lang.GetMessage("MustContain", this, player.UserIDString), keyContain);
                                    return;
                                }
                            }

                            foreach (var keyNot in configData.settings2.BlockedUrls)
                            {
                                if (url.Contains("ropbox.com"))
                                    break;

                                if (url.Contains(keyNot))
                                {
                                    SendReply(player, lang.GetMessage("CanNotContain", this, player.UserIDString), keyNot);
                                    return;
                                }
                            }

                            if (!TryLoadUrlList($"PlayerSaves/{player.UserIDString}", out var data))
                                data.SavedURLS = new Dictionary<string, List<genresInfo>>();

                            int position = Int32.Parse(arg.Args[2]);
                            string config = string.Join(" ", arg.Args.Skip(3).ToArray()).Trim();

                            if (!data.SavedURLS.ContainsKey(config))
                                data.SavedURLS.Add(config, new List<genresInfo>());

                            data.SavedURLS[config].Add(new genresInfo() { urlName = name, URL = url });
                            SaveData($"PlayerSaves/{player.UserIDString}", data);
                            BuildSounBoxUI(player, config, position);
                            break;
                        }

                    case "adminsaveurl":
                        {
                            if (!inputtext.TryGetValue(player.UserIDString, out var name) || string.IsNullOrEmpty(name))
                            {
                                SendReply(player, lang.GetMessage("errorName", this, player.UserIDString));
                                return;
                            }

                            if (!inputtexturl.TryGetValue(player.UserIDString, out var url) || string.IsNullOrEmpty(url))
                            {
                                SendReply(player, lang.GetMessage("errorUrl", this, player.UserIDString));
                                return;
                            }

                            int position = Int32.Parse(arg.Args[2]);
                            string config = string.Join(" ", arg.Args.Skip(3).ToArray()).Trim();

                            configData.genres.lists[config].Add(new genresInfo() { URL = url, urlName = name, CountryCode = GetLanfPlayer(player) });
                            SaveConfig();
                            BuildSounBoxUI(player, config, position);
                            break;
                        }
                    default:
                        break;
                }
            }
        }
        #endregion

        #region UI Helpers
        private Dictionary<string, string> inputtext = new Dictionary<string, string>();
        [ConsoleCommand(commandname)]
        private void InputTextCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg.Args.Length <= 0)
            {
                if (inputtext.ContainsKey(player.UserIDString))
                    inputtext.Remove(player.UserIDString);
                return;
            }
            if (inputtext.ContainsKey(player.UserIDString))
                inputtext[player.UserIDString] = string.Join(" ", arg.Args);
            else
                inputtext.Add(player.UserIDString, string.Join(" ", arg.Args));
        }

        private Dictionary<string, string> inputtexturl = new Dictionary<string, string>();
        [ConsoleCommand(commandurl)]
        private void InputTextCallbackurl(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg.Args.Length <= 0)
            {
                if (inputtexturl.ContainsKey(player.UserIDString))
                    inputtexturl.Remove(player.UserIDString);
                return;
            }
            if (inputtexturl.ContainsKey(player.UserIDString))
                inputtexturl[player.UserIDString] = string.Join(" ", arg.Args);
            else
                inputtexturl.Add(player.UserIDString, string.Join(" ", arg.Args));
        }

        private static CuiElementContainer CreatePanel(string panelName, string parent, string color, string AnchorMin = "0.5 0", string AnchorMax = "0.5 0", string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            return new CuiElementContainer
            {
                new CuiElement
                {
                    Parent = parent, Name = panelName, DestroyUi = panelName,
                    Components = { new CuiImageComponent { Color = color }, new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax, } }
                }
            };
        }

        private static void AddPanel(CuiElementContainer container, string panelName, string panelButton, string color = "0.33 0.33 0.33 0.90", string AnchorMin = "0 0", string AnchorMax = "0 0", string OffsetMin = "-400 -200", string OffsetMax = "400 200")
        {
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = color },
                RectTransform = { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax, }
            }, panelName, panelButton, panelButton);
        }

        private static void CreateCountdown(CuiElementContainer container, string parent, int time, string text = "%TIME_LEFT%", int size = 12, string command = "", string color = "0.60 255 0 0.68", string AnchorMin = "0.5 0", string AnchorMax = "0.5 0", string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiCountdownComponent { StartTime = time, EndTime = 0, Step = 1, Command = command},
                    new CuiTextComponent  { Text = $"{text}", FontSize = size, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = color},
                    new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax },
                }
            });
        }

        private static void CreateLable(CuiElementContainer container, string parent, string panelN, string message, int size, string color, TextAnchor anchor, string ancorMin, string ancorMax, string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            container.Add(new CuiLabel
            {
                Text = { Text = message, FontSize = size, Align = anchor, Color = color, Font = "RobotoCondensed-Bold.ttf" },
                RectTransform = { AnchorMin = ancorMin, AnchorMax = ancorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax }
            }, parent, panelN);
        }

        private static void CreateElement(CuiElementContainer container, string parent, string name, string png, string ancorMin, string ancorMax, string color, string material = "assets/content/ui/uibackgroundblur-ingamemenu.mat")
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = name,
                DestroyUi = name,
                Components = { new CuiImageComponent { Sprite = png, Color = color }, new CuiRectTransformComponent { AnchorMin = ancorMin, AnchorMax = ancorMax } }
            });
        }

        private static void AddButton(CuiElementContainer container, string panelName, string panelButton, string text, int testSize, string colorT, string colorB, TextAnchor anchor = TextAnchor.MiddleCenter, string usaageCommand = "", string AnchorMin = "0 0", string AnchorMax = "0 0", string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            container.Add(new CuiButton
            {
                Text = { Text = text, FontSize = testSize, Align = anchor, Color = colorT },
                Button = { Command = usaageCommand, Color = colorB },
                RectTransform = { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax, }
            }, panelName, panelButton);
        }

        private static void AddTextBox(CuiElementContainer container, string parent, string panelName, string Color, string text, int size, TextAnchor Align, int charsLimit, string command, string AnchorMin = "0 0", string AnchorMax = "0 0", string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parent,
                Components =
                {
                    new CuiInputFieldComponent { NeedsKeyboard = true, Text = text, CharsLimit = charsLimit, Color = Color, IsPassword = false, Command = command, Font = "robotocondensed-regular.ttf", FontSize = size, Align = Align },
                    new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax }
                }
            });
        }

        private static void AddImage(CuiElementContainer container, string parent, string panelName, int itemID, ulong skinID, string color, string AnchorMin = "0 0", string AnchorMax = "1 1", string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parent,
                Components = { new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax },
                new CuiImageComponent { ItemId = itemID, SkinId = skinID, Color = color } }
            });
        }

        private static void AddImageUrl(CuiElementContainer container, string parent, string panelName, string image, string ColorB, string AnchorMin = "0 0", string AnchorMax = "1 1", string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parent,
                DestroyUi = panelName,
                Components =
                {
                    new CuiRawImageComponent { Png = image, Color = ColorB },
                    new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin,  OffsetMax = OffsetMax},
                }
            });
        }

        private static void AddImageAvatar(CuiElementContainer container, string parent, string panelName, string image, string ColorB, string AnchorMin = "0 0", string AnchorMax = "1 1", string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parent,
                DestroyUi = panelName,
                Components =
                {
                    new CuiRawImageComponent { SteamId = image, Color = ColorB },
                    new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin,  OffsetMax = OffsetMax},
                }
            });
        }

        private void Add_CuiScrollView(CuiElementContainer container, string name, string parent, string contentAnchorMin, string contentAnchorMax, string contentOffsetMin, string contentOffsetMax)
        {
            container.Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                DestroyUi = name,
                Components = {
                    new CuiScrollViewComponent
                    {
                        Vertical = true,
                        Horizontal = false,
                        ScrollSensitivity = 5f,
                        ContentTransform = new CuiRectTransform
                        {
                            AnchorMin = contentAnchorMin,
                            AnchorMax = contentAnchorMax,
                            OffsetMin = contentOffsetMin,
                            OffsetMax = contentOffsetMax
                        },

                        VerticalScrollbar = new CuiScrollbar { Invert = false, TrackColor = "0 0 0 0.5", HandleColor = "0.760 0.760 0.760 0.2", HighlightColor = "0.760 0.760 0.760 0.2", PressedColor = "0.760 0.760 0.760 0.2", }
                    }
                }
            });
        }
        #endregion

        #region Search For Stream
        public List<string> country = new List<string>();

        private IEnumerator DownloadSound(string info = "http://89.58.16.19/json/stations")
        {
            Dictionary<string, List<netRadio>> list = Pool.Get<Dictionary<string, List<netRadio>>>();
            Dictionary<string, Dictionary<string, List<netRadio>>> listType = Pool.Get<Dictionary<string, Dictionary<string, List<netRadio>>>>();

            UnityWebRequest www = UnityWebRequest.Get(info);

            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                PrintWarning(string.Format("Failed to download! Error: {0}", www.error));
                www.Dispose();
                yield break;
            }

            if (www?.downloadHandler?.data != null)
            {
                byte[] bytes = www.downloadHandler.data;

                yield return new WaitForSeconds(1);

                if (bytes.Length > 0)
                {
                    var json = JsonConvert.DeserializeObject<List<urlClass>>(www.downloadHandler.text);
                    if (json != null)
                    {
                        foreach (var key in json)
                        {
                            if (!string.IsNullOrEmpty(key.url_resolved) && !string.IsNullOrEmpty(key.name) && !string.IsNullOrEmpty(key.countrycode) && !string.IsNullOrEmpty(key.codec) && key.codec == "MP3")
                            {
                                string code = key.countrycode.ToLower();
                                if (!list.ContainsKey(code))
                                    list.Add(code, new List<netRadio>());

                                list[code].Add(new netRadio() { name = key.name, url = key.url_resolved, tags = key.tags });

                                if (!string.IsNullOrEmpty(key.tags))
                                {
                                    foreach (var configKey in configData.genres.lists)
                                    {
                                        if (key.tags.Contains(configKey.Key, StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (!listType.ContainsKey(code))
                                                listType.Add(code, new Dictionary<string, List<netRadio>>());

                                            if (!listType[code].ContainsKey(configKey.Key))
                                                listType[code].Add(configKey.Key, new List<netRadio>());

                                            listType[code][configKey.Key].Add(new netRadio() { name = key.name, url = key.url_resolved, tags = key.tags });
                                        }
                                    }
                                }
                            }
                        }
                        PrintWarning("Finished download of radio stations!");
                    }
                    else
                        PrintWarning("Could Not Download Streams!");
                }
                else
                    PrintWarning("Failed to download! Error: File Bytes = 0");
            }
            yield return new WaitForSeconds(2);
            foreach (var infoCode in list)
            {
                if (!country.Contains(infoCode.Key))
                    country.Add(infoCode.Key);

                TryLoadUrlCountry($"StreamLists/{infoCode.Key}/all", out var data);
                data = infoCode.Value;
                SaveDataUrl($"StreamLists/{infoCode.Key}", data);
            }

            foreach (var infoGen in listType)
            {
                foreach (var gen in infoGen.Value)
                {
                    TryLoadUrlCountry($"StreamLists/{infoGen.Key}/{gen.Key}", out var data);
                    data = gen.Value;
                    SaveDataUrl($"StreamLists/{infoGen.Key}/{gen.Key}", data);
                }
            }

            Pool.FreeUnmanaged(ref list);
            www.Dispose();
        }

        public class urlClass
        {
            public string changeuuid;
            public string stationuuid;
            public object serveruuid;
            public string name;
            public string url;
            public string url_resolved;
            public string homepage;
            public object favicon;
            public string tags;
            public string country;
            public string countrycode;
            public object iso_3166_2;
            public string state;
            public string language;
            public string languagecodes;
            public int votes;
            public string lastchangetime;
            public string lastchangetime_iso8601;
            public string codec;
            public int bitrate;
            public int hls;
            public int lastcheckok;
            public string lastchecktime;
            public string lastchecktime_iso8601;
            public string lastcheckoktime;
            public string lastcheckoktime_iso8601;
            public string lastlocalchecktime;
            public object lastlocalchecktime_iso8601;
            public string clicktimestamp;
            public object clicktimestamp_iso8601;
            public int clickcount;
            public int clicktrend;
            public int ssl_error;
            public object geo_lat;
            public object geo_long;
            public object geo_distance;
            public bool has_extended_info;
        }

        private static string GetLanfPlayer(BasePlayer player)
        {
            string info = _.lang.GetLanguage(player.UserIDString);
            if (!string.IsNullOrEmpty(info))
            {
                string first2 = info.Substring(0, 2);
                if (langToCountry.ContainsKey(first2))
                    return langToCountry[first2];
            }
            return "us";
        }

        private static Dictionary<string, string> langToCountry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "af", "za" }, // Afrikaans - South Africa
            { "ar", "sa" }, // Arabic - Saudi Arabia
            { "az", "az" }, // Azerbaijani - Azerbaijan
            { "be", "by" }, // Belarusian - Belarus
            { "bg", "bg" }, // Bulgarian - Bulgaria
            { "bn", "bd" }, // Bengali - Bangladesh
            { "ca", "es" }, // Catalan - Spain
            { "cs", "cz" }, // Czech - Czech Republic
            { "da", "dk" }, // Danish - Denmark
            { "de", "de" }, // German - Germany
            { "el", "gr" }, // Greek - Greece
            { "en", "us" }, // English - United States
            { "es", "es" }, // Spanish - Spain
            { "et", "ee" }, // Estonian - Estonia
            { "fa", "ir" }, // Persian - Iran
            { "fi", "fi" }, // Finnish - Finland
            { "fr", "fr" }, // French - France
            { "he", "il" }, // Hebrew - Israel
            { "hi", "in" }, // Hindi - India
            { "hr", "hr" }, // Croatian - Croatia
            { "hu", "hu" }, // Hungarian - Hungary
            { "id", "id" }, // Indonesian - Indonesia
            { "it", "it" }, // Italian - Italy
            { "ja", "jp" }, // Japanese - Japan
            { "ka", "ge" }, // Georgian - Georgia
            { "kk", "kz" }, // Kazakh - Kazakhstan
            { "ko", "kr" }, // Korean - South Korea
            { "lt", "lt" }, // Lithuanian - Lithuania
            { "lv", "lv" }, // Latvian - Latvia
            { "mk", "mk" }, // Macedonian - North Macedonia
            { "mn", "mn" }, // Mongolian - Mongolia
            { "ms", "my" }, // Malay - Malaysia
            { "nb", "no" }, // Norwegian Bokmal - Norway
            { "nl", "nl" }, // Dutch - Netherlands
            { "pl", "pl" }, // Polish - Poland
            { "pt", "br" }, // Portuguese - Brazil
            { "ro", "ro" }, // Romanian - Romania
            { "ru", "ru" }, // Russian - Russia
            { "sk", "sk" }, // Slovak - Slovakia
            { "sl", "si" }, // Slovenian - Slovenia
            { "sq", "al" }, // Albanian - Albania
            { "sr", "rs" }, // Serbian - Serbia
            { "sv", "se" }, // Swedish - Sweden
            { "th", "th" }, // Thai - Thailand
            { "tr", "tr" }, // Turkish - Turkey
            { "uk", "ua" }, // Ukrainian - Ukraine
            { "ur", "pk" }, // Urdu - Pakistan
            { "uz", "uz" }, // Uzbek - Uzbekistan
            { "vi", "vn" }, // Vietnamese - Vietnam
            { "zh", "cn" }  // Chinese - China
        };
        #endregion

        #region Localization
        public static void GameTips(BasePlayer player, string message, GameTip.Styles style = GameTip.Styles.Error)
        {
            if (player != null)
            {
                player.ShowToast(style, message, true);
            }
        }

        protected override void LoadDefaultMessages() => LoadDefaultMessagesNew();
        private void LoadDefaultMessagesNew()
        {
            var messages = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox By Razor</color>",
                ["errorName"] = "You must enter a display name.",
                ["MustContain"] = "The url must contain {0}",
                ["CanNotContain"] = "The url can not contain {0}",
                ["errorUrl"] = "You must enter a url.",
                ["RelaodKey"] = "Press the reload key to set the radio station",
                ["stream"] = "Use /station to set the radio station",
                ["NoBoomBoxFound"] = "There is no boombox here.",
                ["ExitMenu"] = "<color=#FFFF00>EXIT MENU</color>",
                ["AddingFor"] = "<color=#FFFF00>ADDING URL FOR {0}</color>",
                ["AddUrlName"] = "URL DISPLAY NAME",
                ["AddUrLink"] = "URL LINK - Must Contain http -",
                ["AddUrl"] = "You can add a URL here to play your own custom stream. Here's some important information to keep in mind:\n\n* MP3 files can be hosted using Dropbox share URLs.\n* You can find a variety of stream URLs at https://streamurl.link.",
                ["Save"] = "Save",
                ["AdminSave"] = "Save Public",
                ["Cancel"] = "Cancel",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages.ContainsKey(key.Key))
                    messages.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages, this);

            var messages_sr = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox od Razor-a</color>",
                ["errorName"] = "Morate uneti ime za prikaz.",
                ["MustContain"] = "URL mora sadržati {0}",
                ["CanNotContain"] = "URL ne može sadržati {0}",
                ["errorUrl"] = "Morate uneti URL.",
                ["RelaodKey"] = "Pritisnite taster za ponovo učitavanje da biste postavili radio stanicu.",
                ["stream"] = "Koristite /station da postavite radio stanicu.",
                ["NoBoomBoxFound"] = "Ovde nema boomboksa.",
                ["ExitMenu"] = "<color=#FFFF00>IZLAZ IZ MENIJA</color>",
                ["AddingFor"] = "<color=#FFFF00>DODAVANJE URL-a ZA {0}</color>",
                ["AddUrlName"] = "NAZIV PRIKAZA URL-a",
                ["AddUrLink"] = "URL LINK - Mora sadržati http -",
                ["AddUrl"] = "Ovde možete dodati URL da biste puštali svoj prilagođeni stream. Evo nekoliko važnih informacija koje treba imati na umu:\n\n* MP3 fajlovi mogu biti hostovani koristeći Dropbox deljene URL-ove.\n* Možete pronaći razne stream URL-ove na https://streamurl.link.",
                ["Save"] = "Spremi",
                ["AdminSave"] = "Spremi Javno",
                ["Cancel"] = "Otkaži",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_sr.ContainsKey(key.Key))
                    messages_sr.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_sr, this, "sr");

            var messages_vi = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox bởi Razor</color>",
                ["errorName"] = "Bạn phải nhập tên hiển thị.",
                ["MustContain"] = "URL phải chứa {0}",
                ["CanNotContain"] = "URL không thể chứa {0}",
                ["errorUrl"] = "Bạn phải nhập một URL.",
                ["RelaodKey"] = "Nhấn phím tải lại để cài đặt đài phát thanh.",
                ["stream"] = "Sử dụng /station để cài đặt đài phát thanh.",
                ["NoBoomBoxFound"] = "Không có boombox ở đây.",
                ["ExitMenu"] = "<color=#FFFF00>THOÁT MENU</color>",
                ["AddingFor"] = "<color=#FFFF00>ĐANG THÊM URL CHO {0}</color>",
                ["AddUrlName"] = "TÊN HIỂN THỊ URL",
                ["AddUrLink"] = "LIÊN KẾT URL - Phải chứa http -",
                ["AddUrl"] = "Bạn có thể thêm một URL ở đây để phát dòng tùy chỉnh của riêng bạn. Dưới đây là một số thông tin quan trọng cần lưu ý:\n\n* Tệp MP3 có thể được lưu trữ bằng URL chia sẻ của Dropbox.\n* Bạn có thể tìm thấy nhiều URL dòng phát tại https://streamurl.link.",
                ["Save"] = "Lưu",
                ["AdminSave"] = "Lưu Công Khai",
                ["Cancel"] = "Hủy",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_vi.ContainsKey(key.Key))
                    messages_vi.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_vi, this, "vi");

            var messages_ro = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox de la Razor</color>",
                ["errorName"] = "Trebuie să introduceți un nume de afișare.",
                ["MustContain"] = "URL-ul trebuie să conțină {0}",
                ["CanNotContain"] = "URL-ul nu poate conține {0}",
                ["errorUrl"] = "Trebuie să introduceți un URL.",
                ["RelaodKey"] = "Apăsați tasta de reîncărcare pentru a seta stația de radio.",
                ["stream"] = "Folosiți /station pentru a seta stația de radio.",
                ["NoBoomBoxFound"] = "Nu există un boombox aici.",
                ["ExitMenu"] = "<color=#FFFF00>IEȘIRE MENIU</color>",
                ["AddingFor"] = "<color=#FFFF00>ADĂUGARE URL PENTRU {0}</color>",
                ["AddUrlName"] = "NUME DE AFiȘARE URL",
                ["AddUrLink"] = "LINK URL - Trebuie să conțină http -",
                ["AddUrl"] = "Puteți adăuga un URL aici pentru a reda propriul stream personalizat. Iată câteva informații importante de care trebuie să țineți cont:\n\n* Fișierele MP3 pot fi găzduite folosind URL-uri de partajare Dropbox.\n* Puteți găsi o varietate de URL-uri de stream la https://streamurl.link.",
                ["Save"] = "Salvează",
                ["AdminSave"] = "Salvează Public",
                ["Cancel"] = "Anulează",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_ro.ContainsKey(key.Key))
                    messages_ro.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_ro, this, "ro");

            var messages_hu = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox a Razor-tól</color>",
                ["errorName"] = "Meg kell adnod egy megjelenítési nevet.",
                ["MustContain"] = "Az URL-nek tartalmaznia kell {0}",
                ["CanNotContain"] = "Az URL nem tartalmazhat {0}",
                ["errorUrl"] = "Meg kell adnod egy URL-t.",
                ["RelaodKey"] = "Nyomd meg a frissítő gombot a rádióállomás beállításához.",
                ["stream"] = "Használd a /station parancsot a rádióállomás beállításához.",
                ["NoBoomBoxFound"] = "Itt nincs boombox.",
                ["ExitMenu"] = "<color=#FFFF00>KILÉPÉS MENÜ</color>",
                ["AddingFor"] = "<color=#FFFF00>URL HOZZÁADÁSA A {0} SZÁMÁRA</color>",
                ["AddUrlName"] = "URL MEGJELENÍTÉSI NÉV",
                ["AddUrLink"] = "URL LINK - Tartalmaznia kell http-t -",
                ["AddUrl"] = "Itt adhatsz hozzá egy URL-t, hogy lejátszd a saját egyedi stream-edet. Íme néhány fontos információ, amire figyelned kell:\n\n* MP3 fájlokat lehet hosztolni a Dropbox megosztott URL-jeivel.\n* Különböző stream URL-eket találhatsz itt: https://streamurl.link.",
                ["Save"] = "Mentés",
                ["AdminSave"] = "Mentés nyilvános",
                ["Cancel"] = "Mégse",
                ["Rock"] = "Rock",
                ["Disco"] = "Diszkó",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_hu.ContainsKey(key.Key))
                    messages_hu.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_hu, this, "hu");

            var messages_he = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox מאת Razor</color>",
                ["errorName"] = "עליך להזין שם תצוגה.",
                ["MustContain"] = "ה-URL חייב להכיל {0}",
                ["CanNotContain"] = "ה-URL לא יכול להכיל {0}",
                ["errorUrl"] = "עליך להזין URL.",
                ["RelaodKey"] = "לחץ על מקש הרענון כדי להגדיר את תחנת הרדיו.",
                ["stream"] = "השתמש ב-/station כדי להגדיר את תחנת הרדיו.",
                ["NoBoomBoxFound"] = "אין כאן בומבוקס.",
                ["ExitMenu"] = "<color=#FFFF00>תפריט יציאה</color>",
                ["AddingFor"] = "<color=#FFFF00>הוספת URL ל-{0}</color>",
                ["AddUrlName"] = "שם הצגת ה-URL",
                ["AddUrLink"] = "קישור ל-URL - חייב להכיל http -",
                ["AddUrl"] = "תוכל להוסיף URL כאן כדי לשדר את הזרם המותאם אישית שלך. הנה מידע חשוב שיש לשים לב אליו:\n\n* קבצי MP3 יכולים להתארח באמצעות קישורי שיתוף Dropbox.\n* תוכל למצוא מגוון של URL-ים לזרמים בכתובת https://streamurl.link.",
                ["Save"] = "שמור",
                ["AdminSave"] = "שמור לציבור",
                ["Cancel"] = "ביטול",
                ["Rock"] = "רוק",
                ["Disco"] = "דיסקו",
                ["Pop"] = "פופ",
                ["Hip hop"] = "היפ הופ",
                ["Jazz"] = "ג'אז",
                ["Blues"] = "בלוז",
                ["Rap"] = "ראפ"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_he.ContainsKey(key.Key))
                    messages_he.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_he, this, "he");

            var messages_fi = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox Razorilta</color>",
                ["errorName"] = "Sinun täytyy syöttää näyttönimi.",
                ["MustContain"] = "URL-osoitteen täytyy sisältää {0}",
                ["CanNotContain"] = "URL-osoite ei voi sisältää {0}",
                ["errorUrl"] = "Sinun täytyy syöttää URL-osoite.",
                ["RelaodKey"] = "Paina latauspainiketta asettaaksesi radiokanavan.",
                ["stream"] = "Käytä /station asettaaksesi radiokanavan.",
                ["NoBoomBoxFound"] = "Täällä ei ole boomboxia.",
                ["ExitMenu"] = "<color=#FFFF00>POISTU VALIKOSTA</color>",
                ["AddingFor"] = "<color=#FFFF00>LISÄTÄÄN URL {0} :lle</color>",
                ["AddUrlName"] = "URL NÄYTTÖNIMI",
                ["AddUrLink"] = "URL LINKKI - Täytyy sisältää http -",
                ["AddUrl"] = "Voit lisätä URL-osoitteen tänne soittaaksesi omaa mukautettua virtaasi. Tässä on tärkeitä tietoja, jotka kannattaa pitää mielessä:\n\n* MP3-tiedostoja voidaan isännöidä Dropboxin jakamis-URL-osoitteilla.\n* Voit löytää erilaisia virta-URL-osoitteita osoitteesta https://streamurl.link.",
                ["Save"] = "Tallenna",
                ["AdminSave"] = "Tallenna Julkisesti",
                ["Cancel"] = "Peruuta",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_fi.ContainsKey(key.Key))
                    messages_fi.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_fi, this, "fi");

            var messages_el = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox από Razor</color>",
                ["errorName"] = "Πρέπει να εισάγετε ένα όνομα οθόνης.",
                ["MustContain"] = "Η url πρέπει να περιέχει {0}",
                ["CanNotContain"] = "Η url δεν μπορεί να περιέχει {0}",
                ["errorUrl"] = "Πρέπει να εισάγετε μια url.",
                ["RelaodKey"] = "Πατήστε το πλήκτρο επαναφόρτωσης για να ορίσετε τον σταθμό ραδιοφώνου.",
                ["stream"] = "Χρησιμοποιήστε /station για να ορίσετε τον σταθμό ραδιοφώνου.",
                ["NoBoomBoxFound"] = "Δεν υπάρχει boombox εδώ.",
                ["ExitMenu"] = "<color=#FFFF00>ΜΕΝΟΥ ΕΞΟΔΟΥ</color>",
                ["AddingFor"] = "<color=#FFFF00>ΠΡΟΣΘΗΚΗ URL ΓΙΑ {0}</color>",
                ["AddUrlName"] = "ΟΝΟΜΑ ΠΡΟΒΟΛΗΣ URL",
                ["AddUrLink"] = "ΣΥΝΔΕΣΜΟΣ URL - Πρέπει να περιέχει http -",
                ["AddUrl"] = "Μπορείτε να προσθέσετε μια URL εδώ για να παίξετε τη δική σας προσαρμοσμένη ροή. Εδώ είναι μερικές σημαντικές πληροφορίες που πρέπει να έχετε υπόψη:\n\n* Τα αρχεία MP3 μπορούν να φιλοξενούνται με τις URL κοινής χρήσης του Dropbox.\n* Μπορείτε να βρείτε διάφορες URL ροής στο https://streamurl.link.",
                ["Save"] = "Αποθήκευση",
                ["AdminSave"] = "Αποθήκευση Δημόσια",
                ["Cancel"] = "Ακύρωση",
                ["Rock"] = "Ροκ",
                ["Disco"] = "Ντίσκο",
                ["Pop"] = "Ποπ",
                ["Hip hop"] = "Χιπ χοπ",
                ["Jazz"] = "Τζαζ",
                ["Blues"] = "Μπλουζ",
                ["Rap"] = "Ραπ"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_el.ContainsKey(key.Key))
                    messages_el.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_el, this, "el");

            var messages_ca = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox per Razor</color>",
                ["errorName"] = "Heu d'introduir un nom per mostrar.",
                ["MustContain"] = "L'url ha de contenir {0}",
                ["CanNotContain"] = "L'url no pot contenir {0}",
                ["errorUrl"] = "Heu d'introduir una url.",
                ["RelaodKey"] = "Prem la tecla de recàrrega per establir l'estació de ràdio.",
                ["stream"] = "Utilitza /estacio per establir l'estació de ràdio.",
                ["NoBoomBoxFound"] = "No hi ha cap boombox aquí.",
                ["ExitMenu"] = "<color=#FFFF00>MENÚ DE SORTIDA</color>",
                ["AddingFor"] = "<color=#FFFF00>AFEGINT URL PER {0}</color>",
                ["AddUrlName"] = "NOM DE LA URL",
                ["AddUrLink"] = "ENLLAÇ DE LA URL - Ha de contenir http -",
                ["AddUrl"] = "Podeu afegir una URL aquí per reproduir el vostre propi flux personalitzat. Aquí teniu informació important a tenir en compte:\n\n* Els arxius MP3 es poden allotjar mitjançant les URL de compartició de Dropbox.\n* Podeu trobar una varietat d'URLs de flux a https://streamurl.link.",
                ["Save"] = "Desa",
                ["AdminSave"] = "Desa Públicament",
                ["Cancel"] = "Cancelar",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_ca.ContainsKey(key.Key))
                    messages_ca.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_ca, this, "ca");

            var messages_af = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox Deur Razor</color>",
                ["errorName"] = "Jy moet 'n skermnaam invoer.",
                ["MustContain"] = "Die url moet {0} bevat.",
                ["CanNotContain"] = "Die url mag nie {0} bevat nie.",
                ["errorUrl"] = "Jy moet 'n url invoer.",
                ["RelaodKey"] = "Druk die herlaai sleutel om die radiostasie te stel.",
                ["stream"] = "Gebruik /stasie om die radiostasie te stel.",
                ["NoBoomBoxFound"] = "Daar is geen boombox hier nie.",
                ["ExitMenu"] = "<color=#FFFF00>EXIT MENU</color>",
                ["AddingFor"] = "<color=#FFFF00>VOEG URL BY {0}</color>",
                ["AddUrlName"] = "URL SKERMNAAM",
                ["AddUrLink"] = "URL SKAKEL - Moet http bevat -",
                ["AddUrl"] = "Jy kan 'n URL hier byvoeg om jou eie pasgemaakte stroom af te speel. Hier is belangrike inligting om in gedagtes te hou:\n\n* MP3-lêers kan met Dropbox-deel URL's gehuisves word.\n* Jy kan 'n verskeidenheid stroom URL's vind by https://streamurl.link.",
                ["Save"] = "Stoor",
                ["AdminSave"] = "Stoor Publiek",
                ["Cancel"] = "Kanselleer",
                ["Rock"] = "Rots",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_af.ContainsKey(key.Key))
                    messages_af.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_af, this, "af");

            var messages_fr = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox Par Razor</color>",
                ["errorName"] = "Vous devez entrer un nom d'affichage.",
                ["MustContain"] = "L'URL doit contenir {0}",
                ["CanNotContain"] = "L'URL ne peut pas contenir {0}",
                ["errorUrl"] = "Vous devez entrer une URL.",
                ["RelaodKey"] = "Appuyez sur la touche de rechargement pour définir la station de radio",
                ["stream"] = "Utilisez /station pour définir la station de radio",
                ["NoBoomBoxFound"] = "Il n'y a pas de boombox ici.",
                ["ExitMenu"] = "<color=#FFFF00>MENU DE SORTIE</color>",
                ["AddingFor"] = "<color=#FFFF00>AJOUTER L'URL POUR {0}</color>",
                ["AddUrlName"] = "NOM D'AFFICHAGE URL",
                ["AddUrLink"] = "LIEN URL - Doit Contenir http -",
                ["AddUrl"] = "Vous pouvez ajouter une URL ici pour lire votre propre flux personnalisé. Voici quelques informations importantes à garder à l'esprit:\n\n* Les fichiers MP3 peuvent être hébergés à l'aide des URL de partage Dropbox.\n* Vous pouvez trouver une variété d'URL de flux sur https://streamurl.link.",
                ["Save"] = "Sauvegarder",
                ["AdminSave"] = "Sauvegarder Public",
                ["Cancel"] = "Annuler",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_fr.ContainsKey(key.Key))
                    messages_fr.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_fr, this, "fr");

            var messages_it = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox Di Razor</color>",
                ["errorName"] = "Devi inserire un nome visualizzato.",
                ["MustContain"] = "L'URL deve contenere {0}",
                ["CanNotContain"] = "L'URL non può contenere {0}",
                ["errorUrl"] = "Devi inserire un URL.",
                ["RelaodKey"] = "Premi il tasto di ricarica per impostare la stazione radio",
                ["stream"] = "Usa /station per impostare la stazione radio",
                ["NoBoomBoxFound"] = "Non c'è un boombox qui.",
                ["ExitMenu"] = "<color=#FFFF00>MENU DI USCITA</color>",
                ["AddingFor"] = "<color=#FFFF00>AGGIUNGENDO URL PER {0}</color>",
                ["AddUrlName"] = "NOME VISUALIZZATO URL",
                ["AddUrLink"] = "LINK URL - Deve contenere http -",
                ["AddUrl"] = "Puoi aggiungere un URL qui per riprodurre il tuo flusso personalizzato. Ecco alcune informazioni importanti da tenere a mente:\n\n* I file MP3 possono essere ospitati utilizzando gli URL di condivisione Dropbox.\n* Puoi trovare una varietà di URL di flusso su https://streamurl.link.",
                ["Save"] = "Salva",
                ["AdminSave"] = "Salva Pubblico",
                ["Cancel"] = "Annulla",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_it.ContainsKey(key.Key))
                    messages_it.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_it, this, "it");

            var messages_de = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox Von Razor</color>",
                ["errorName"] = "Sie müssen einen Anzeigenamen eingeben.",
                ["MustContain"] = "Die URL muss {0} enthalten",
                ["CanNotContain"] = "Die URL darf {0} nicht enthalten",
                ["errorUrl"] = "Sie müssen eine URL eingeben.",
                ["RelaodKey"] = "Drücken Sie die Neuladen-Taste, um den Radiosender festzulegen",
                ["stream"] = "Verwenden Sie /station, um den Radiosender festzulegen",
                ["NoBoomBoxFound"] = "Es gibt hier keinen Boombox.",
                ["ExitMenu"] = "<color=#FFFF00>EXIT-MENÜ</color>",
                ["AddingFor"] = "<color=#FFFF00>URL FÜR {0} HINZUFÜGEN</color>",
                ["AddUrlName"] = "URL ANZEIGENAME",
                ["AddUrLink"] = "URL LINK - Muss http enthalten -",
                ["AddUrl"] = "Hier können Sie eine URL hinzufügen, um Ihren eigenen benutzerdefinierten Stream abzuspielen. Hier sind einige wichtige Informationen, die Sie beachten sollten:\n\n* MP3-Dateien können mit Dropbox-Share-URLs gehostet werden.\n* Sie können eine Vielzahl von Stream-URLs auf https://streamurl.link finden.",
                ["Save"] = "Speichern",
                ["AdminSave"] = "Speichern Öffentliche",
                ["Cancel"] = "Abbrechen",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_de.ContainsKey(key.Key))
                    messages_de.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_de, this, "de");

            var messages_es = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox De Razor</color>",
                ["errorName"] = "Debes introducir un nombre de visualización.",
                ["MustContain"] = "La URL debe contener {0}",
                ["CanNotContain"] = "La URL no puede contener {0}",
                ["errorUrl"] = "Debes introducir una URL.",
                ["RelaodKey"] = "Presiona la tecla de recarga para establecer la estación de radio",
                ["stream"] = "Usa /station para establecer la estación de radio",
                ["NoBoomBoxFound"] = "No hay un boombox aquí.",
                ["ExitMenu"] = "<color=#FFFF00>MENÚ DE SALIDA</color>",
                ["AddingFor"] = "<color=#FFFF00>AGREGANDO URL PARA {0}</color>",
                ["AddUrlName"] = "NOMBRE DE VISUALIZACIÓN DE URL",
                ["AddUrLink"] = "ENLACE DE URL - Debe contener http -",
                ["AddUrl"] = "Puedes agregar una URL aquí para reproducir tu propio stream personalizado. Aquí hay algunos detalles importantes a tener en cuenta:\n\n* Los archivos MP3 pueden ser hospedados utilizando URLs de compartición de Dropbox.\n* Puedes encontrar una variedad de URLs de stream en https://streamurl.link.",
                ["Save"] = "Guardar",
                ["AdminSave"] = "Guardar Público",
                ["Cancel"] = "Cancelar",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_es.ContainsKey(key.Key))
                    messages_es.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_es, this, "es-ES");

            var messages_ja = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox by Razor</color>",
                ["errorName"] = "表示名を入力する必要があります。",
                ["MustContain"] = "URLには{0}が含まれている必要があります",
                ["CanNotContain"] = "URLには{0}を含めることはできません",
                ["errorUrl"] = "URLを入力する必要があります。",
                ["RelaodKey"] = "ラジオ局を設定するためにリロードキーを押してください",
                ["stream"] = "/stationを使用してラジオ局を設定してください",
                ["NoBoomBoxFound"] = "ここにはブームボックスはありません。",
                ["ExitMenu"] = "<color=#FFFF00>メニューを終了</color>",
                ["AddingFor"] = "<color=#FFFF00>{0}のURLを追加中</color>",
                ["AddUrlName"] = "URL表示名",
                ["AddUrLink"] = "URLリンク - httpを含む必要があります -",
                ["AddUrl"] = "カスタムストリームを再生するためにURLを追加できます。以下の重要な情報を考慮してください：\n\n* MP3ファイルはDropboxの共有URLを使ってホストできます。\n* 様々なストリームURLをhttps://streamurl.linkで見つけることができます。",
                ["Save"] = "保存",
                ["AdminSave"] = "公開保存",
                ["Cancel"] = "キャンセル",
                ["Rock"] = "ロック",
                ["Disco"] = "ディスコ",
                ["Pop"] = "ポップ",
                ["Hip hop"] = "ヒップホップ",
                ["Jazz"] = "ジャズ",
                ["Blues"] = "ブルース",
                ["Rap"] = "ラップ"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_ja.ContainsKey(key.Key))
                    messages_ja.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_ja, this, "ja");

            var messages_ko = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox By Razor</color>",
                ["errorName"] = "표시 이름을 입력해야 합니다.",
                ["MustContain"] = "URL에는 {0}이 포함되어야 합니다.",
                ["CanNotContain"] = "URL에는 {0}을(를) 포함할 수 없습니다.",
                ["errorUrl"] = "URL을 입력해야 합니다.",
                ["RelaodKey"] = "라디오 방송국을 설정하려면 리로드 키를 누르세요.",
                ["stream"] = "/station을 사용하여 라디오 방송국을 설정하세요.",
                ["NoBoomBoxFound"] = "여기에는 붐박스가 없습니다.",
                ["ExitMenu"] = "<color=#FFFF00>종료 메뉴</color>",
                ["AddingFor"] = "<color=#FFFF00>{0}의 URL 추가</color>",
                ["AddUrlName"] = "URL 표시 이름",
                ["AddUrLink"] = "URL 링크 - http를 포함해야 합니다 -",
                ["AddUrl"] = "여기에 URL을 추가하여 사용자 정의 스트림을 재생할 수 있습니다. 다음은 유의해야 할 중요한 정보입니다:\n\n* MP3 파일은 Dropbox 공유 URL을 사용하여 호스팅할 수 있습니다.\n* 다양한 스트림 URL을 https://streamurl.link에서 찾을 수 있습니다.",
                ["Save"] = "저장",
                ["AdminSave"] = "공개 저장",
                ["Cancel"] = "취소",
                ["Rock"] = "록",
                ["Disco"] = "디스코",
                ["Pop"] = "팝",
                ["Hip hop"] = "힙합",
                ["Jazz"] = "재즈",
                ["Blues"] = "블루스",
                ["Rap"] = "랩"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_ko.ContainsKey(key.Key))
                    messages_ko.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_ko, this, "ko");

            var messages_ru = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox от Razor</color>",
                ["errorName"] = "Вы должны ввести отображаемое имя.",
                ["MustContain"] = "URL должен содержать {0}",
                ["CanNotContain"] = "URL не может содержать {0}",
                ["errorUrl"] = "Вы должны ввести URL.",
                ["RelaodKey"] = "Нажмите клавишу перезагрузки, чтобы установить радиостанцию",
                ["stream"] = "Используйте /station, чтобы установить радиостанцию",
                ["NoBoomBoxFound"] = "Здесь нет бумбокса.",
                ["ExitMenu"] = "<color=#FFFF00>ВЫХОД ИЗ МЕНЮ</color>",
                ["AddingFor"] = "<color=#FFFF00>ДОБАВЛЕНИЕ URL ДЛЯ {0}</color>",
                ["AddUrlName"] = "ИМЯ ОТОБРАЖЕНИЯ URL",
                ["AddUrLink"] = "ССЫЛКА URL - Должна содержать http -",
                ["AddUrl"] = "Вы можете добавить URL здесь, чтобы воспроизвести свой собственный пользовательский поток. Вот некоторые важные моменты, которые нужно учитывать:\n\n* Файлы MP3 могут быть размещены с использованием URL-адресов общего доступа Dropbox.\n* Вы можете найти различные URL-адреса потоков на https://streamurl.link.",
                ["Save"] = "Сохранить",
                ["AdminSave"] = "Сохранить для публичного доступа",
                ["Cancel"] = "Отмена",
                ["Rock"] = "Рок",
                ["Disco"] = "Диско",
                ["Pop"] = "Поп",
                ["Hip hop"] = "Хип-хоп",
                ["Jazz"] = "Джаз",
                ["Blues"] = "Блюз",
                ["Rap"] = "Рэп"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_ru.ContainsKey(key.Key))
                    messages_ru.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_ru, this, "ru");

            var messages_zh = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox By Razor</color>",
                ["errorName"] = "您必须输入显示名称。",
                ["MustContain"] = "URL必须包含{0}",
                ["CanNotContain"] = "URL不能包含{0}",
                ["errorUrl"] = "您必须输入一个URL。",
                ["RelaodKey"] = "按下重新加载键以设置广播电台",
                ["stream"] = "使用/ station设置广播电台",
                ["NoBoomBoxFound"] = "这里没有BoomBox。",
                ["ExitMenu"] = "<color=#FFFF00>退出菜单</color>",
                ["AddingFor"] = "<color=#FFFF00>添加{0}的URL</color>",
                ["AddUrlName"] = "URL显示名称",
                ["AddUrLink"] = "URL链接 - 必须包含http -",
                ["AddUrl"] = "您可以在此处添加URL，以播放您自己的自定义流。以下是一些重要的注意事项：\n\n* MP3文件可以使用Dropbox共享URL进行托管。\n* 您可以在https://streamurl.link找到各种流URL。",
                ["Save"] = "保存",
                ["AdminSave"] = "保存为公共",
                ["Cancel"] = "取消",
                ["Rock"] = "摇滚",
                ["Disco"] = "迪斯科",
                ["Pop"] = "流行",
                ["Hip hop"] = "嘻哈",
                ["Jazz"] = "爵士",
                ["Blues"] = "蓝调",
                ["Rap"] = "说唱"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_zh.ContainsKey(key.Key))
                    messages_zh.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_zh, this, "zh-TW");
            lang.RegisterMessages(messages_zh, this, "zh-CN");

            var messages_uk = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox від Razor</color>",
                ["errorName"] = "Ви повинні ввести відображуване ім'я.",
                ["MustContain"] = "URL повинен містити {0}",
                ["CanNotContain"] = "URL не може містити {0}",
                ["errorUrl"] = "Ви повинні ввести URL.",
                ["RelaodKey"] = "Натисніть клавішу перезавантаження, щоб налаштувати радіостанцію",
                ["stream"] = "Використовуйте /station, щоб налаштувати радіостанцію",
                ["NoBoomBoxFound"] = "Тут немає бумбоксу.",
                ["ExitMenu"] = "<color=#FFFF00>ВИХІД З МЕНЮ</color>",
                ["AddingFor"] = "<color=#FFFF00>ДОБАВЛЕННЯ URL ДЛЯ {0}</color>",
                ["AddUrlName"] = "ІМ'Я ВІДОБРАЖЕННЯ URL",
                ["AddUrLink"] = "URL ПОСИЛАННЯ - Повинно містити http -",
                ["AddUrl"] = "Ви можете додати URL, щоб відтворити власний потік. Ось деяка важлива інформація, яку слід враховувати:\n\n* MP3-файли можна хостити за допомогою спільних URL-адрес Dropbox.\n* Ви можете знайти різноманітні URL-адреси потоків на https://streamurl.link.",
                ["Save"] = "Зберегти",
                ["AdminSave"] = "Зберегти для публічного доступу",
                ["Cancel"] = "Скасувати",
                ["Rock"] = "Рок",
                ["Disco"] = "Диско",
                ["Pop"] = "Поп",
                ["Hip hop"] = "Хіп-хоп",
                ["Jazz"] = "Джаз",
                ["Blues"] = "Блюз",
                ["Rap"] = "Реп"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_uk.ContainsKey(key.Key))
                    messages_uk.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_uk, this, "uk");

            var messages_pl = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox By Razor</color>",
                ["errorName"] = "Musisz wprowadzić nazwę wyświetlaną.",
                ["MustContain"] = "URL musi zawierać {0}",
                ["CanNotContain"] = "URL nie może zawierać {0}",
                ["errorUrl"] = "Musisz wprowadzić URL.",
                ["RelaodKey"] = "Naciśnij przycisk przeładowania, aby ustawić stację radiową",
                ["stream"] = "Użyj /station, aby ustawić stację radiową",
                ["NoBoomBoxFound"] = "Nie ma tutaj boom-boxa.",
                ["ExitMenu"] = "<color=#FFFF00>WYJDŹ Z MENU</color>",
                ["AddingFor"] = "<color=#FFFF00>ADRES URL DLA {0}</color>",
                ["AddUrlName"] = "NAZWA WYŚWIETLANIA URL",
                ["AddUrLink"] = "LINK URL - Musi zawierać http -",
                ["AddUrl"] = "Możesz dodać URL, aby odtwarzać swój własny niestandardowy strumień. Oto kilka ważnych informacji, które należy wziąć pod uwagę:\n\n* Pliki MP3 można hostować za pomocą URL Dropbox share.\n* Możesz znaleźć różnorodne URL do strumieni na https://streamurl.link.",
                ["Save"] = "Zapisz",
                ["AdminSave"] = "Zapisz publicznie",
                ["Cancel"] = "Anuluj",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_pl.ContainsKey(key.Key))
                    messages_pl.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_pl, this, "pl");

            var messages_pt = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox By Razor</color>",
                ["errorName"] = "Você deve inserir um nome de exibição.",
                ["MustContain"] = "O URL deve conter {0}",
                ["CanNotContain"] = "O URL não pode conter {0}",
                ["errorUrl"] = "Você deve inserir um URL.",
                ["RelaodKey"] = "Pressione a tecla de recarga para configurar a estação de rádio",
                ["stream"] = "Use /station para configurar a estação de rádio",
                ["NoBoomBoxFound"] = "Não há boombox aqui.",
                ["ExitMenu"] = "<color=#FFFF00>Sair do menu</color>",
                ["AddingFor"] = "<color=#FFFF00>ADICIONANDO URL PARA {0}</color>",
                ["AddUrlName"] = "NOME DE EXIBIÇÃO DA URL",
                ["AddUrLink"] = "LINK DA URL - Deve conter http -",
                ["AddUrl"] = "Você pode adicionar uma URL aqui para tocar seu próprio stream personalizado. Aqui estão algumas informações importantes para considerar:\n\n* Arquivos MP3 podem ser hospedados usando URLs de compartilhamento Dropbox.\n* Você pode encontrar uma variedade de URLs de stream em https://streamurl.link.",
                ["Save"] = "Salvar",
                ["AdminSave"] = "Salvar publicamente",
                ["Cancel"] = "Cancelar",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_pt.ContainsKey(key.Key))
                    messages_pt.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_pt, this, "pt-BR");
            lang.RegisterMessages(messages_pt, this, "pt-PT");
            lang.RegisterMessages(messages_pt, this, "pt-RB");

            var messages_tr = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox By Razor</color>",
                ["errorName"] = "Bir görüntü adı girmelisiniz.",
                ["MustContain"] = "URL {0} içermelidir",
                ["CanNotContain"] = "URL {0} içeremez",
                ["errorUrl"] = "Bir URL girmelisiniz.",
                ["RelaodKey"] = "Radyo istasyonunu ayarlamak için yeniden yükleme tuşuna basın",
                ["stream"] = "/station kullanarak radyo istasyonunu ayarlayın",
                ["NoBoomBoxFound"] = "Burada bir boombox yok.",
                ["ExitMenu"] = "<color=#FFFF00>MENÜDEN ÇIK</color>",
                ["AddingFor"] = "<color=#FFFF00>{0} URL'si EKLENİYOR</color>",
                ["AddUrlName"] = "URL GÖSTERİM ADI",
                ["AddUrLink"] = "URL LİNKİ - HTTP içermelidir -",
                ["AddUrl"] = "Kendi özel akışınızı oynatmak için buraya URL ekleyebilirsiniz. İşte dikkate almanız gereken bazı önemli bilgiler:\n\n* MP3 dosyaları Dropbox paylaşım URL'leriyle barındırılabilir.\n* https://streamurl.link adresinde çeşitli akış URL'leri bulabilirsiniz.",
                ["Save"] = "Kaydet",
                ["AdminSave"] = "Herkese Açık Kaydet",
                ["Cancel"] = "İptal",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Caz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_tr.ContainsKey(key.Key))
                    messages_tr.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_tr, this, "tr");

            var messages_ar = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox By Razor</color>",
                ["errorName"] = "You must enter a display name.",
                ["MustContain"] = "The url must contain {0}",
                ["CanNotContain"] = "The url can not contain {0}",
                ["errorUrl"] = "You must enter a url.",
                ["RelaodKey"] = "Press the reload key to set the radio station",
                ["stream"] = "Use /station to set the radio station",
                ["NoBoomBoxFound"] = "There is no boombox here.",
                ["ExitMenu"] = "<color=#FFFF00>EXIT MENU</color>",
                ["AddingFor"] = "<color=#FFFF00>ADDING URL FOR {0}</color>",
                ["AddUrlName"] = "URL DISPLAY NAME",
                ["AddUrLink"] = "URL LINK - Must Contain http -",
                ["AddUrl"] = "You can add a URL here to play your own custom stream. Here's some important information to keep in mind:\n\n* MP3 files can be hosted using Dropbox share URLs.\n* You can find a variety of stream URLs at https://streamurl.link.",
                ["Save"] = "Save",
                ["AdminSave"] = "Save Public",
                ["Cancel"] = "Cancel",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_ar.ContainsKey(key.Key))
                    messages_ar.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_ar, this, "ar");

            var messages_cs = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox By Razor</color>",
                ["errorName"] = "Musíte zadat zobrazené jméno.",
                ["MustContain"] = "URL musí obsahovat {0}",
                ["CanNotContain"] = "URL nemůže obsahovat {0}",
                ["errorUrl"] = "Musíte zadat URL.",
                ["RelaodKey"] = "Stiskněte klávesu pro znovu načtení a nastavte rádio",
                ["stream"] = "Použijte /station pro nastavení rádia",
                ["NoBoomBoxFound"] = "Tady není žádný boom box.",
                ["ExitMenu"] = "<color=#FFFF00>EXIT MENU</color>",
                ["AddingFor"] = "<color=#FFFF00>PŘIDÁVÁNÍ URL PRO {0}</color>",
                ["AddUrlName"] = "ZOBRAZENÍ URL",
                ["AddUrLink"] = "ODKAZ URL - Musí obsahovat http -",
                ["AddUrl"] = "Sem můžete přidat URL pro přehrávání vlastního streamu. Zde je několik důležitých informací, které byste měli zvážit:\n\n* MP3 soubory lze hostovat pomocí sdílených odkazů Dropbox.\n* Různé streamovací URL najdete na https://streamurl.link.",
                ["Save"] = "Uložit",
                ["AdminSave"] = "Uložit veřejně",
                ["Cancel"] = "Zrušit",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_cs.ContainsKey(key.Key))
                    messages_cs.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_cs, this, "cs");

            var messages_da = new Dictionary<string, string>
            {
                ["Play"] = "Now playing: {0}",
                ["SoundBox"] = "<color=#FFFF00>SoundBox By Razor</color>",
                ["errorName"] = "Du skal indtaste et visningsnavn.",
                ["MustContain"] = "URL'en skal indeholde {0}",
                ["CanNotContain"] = "URL'en må ikke indeholde {0}",
                ["errorUrl"] = "Du skal indtaste en URL.",
                ["RelaodKey"] = "Tryk på genindlæsningsknappen for at indstille radiostationen",
                ["stream"] = "Brug /station for at indstille radiostationen",
                ["NoBoomBoxFound"] = "Der er ingen boombox her.",
                ["ExitMenu"] = "<color=#FFFF00>EXIT MENU</color>",
                ["AddingFor"] = "<color=#FFFF00>ADDERER URL FOR {0}</color>",
                ["AddUrlName"] = "VISNING AF URL",
                ["AddUrLink"] = "URL LINK - Skal indeholde http -",
                ["AddUrl"] = "Du kan tilføje en URL her for at spille din egen tilpassede stream. Her er nogle vigtige oplysninger at tage hensyn til:\n\n* MP3-filer kan hostes ved hjælp af Dropbox-delings-URL'er.\n* Du kan finde forskellige stream-URL'er på https://streamurl.link.",
                ["Save"] = "Gem",
                ["AdminSave"] = "Gem offentlig",
                ["Cancel"] = "Annuller",
                ["Rock"] = "Rock",
                ["Disco"] = "Disco",
                ["Pop"] = "Pop",
                ["Hip hop"] = "Hip hop",
                ["Jazz"] = "Jazz",
                ["Blues"] = "Blues",
                ["Rap"] = "Rap"
            };
            foreach (var key in configData.genres.lists)
            {
                if (!messages_da.ContainsKey(key.Key))
                    messages_da.Add(key.Key, key.Key);
            }
            lang.RegisterMessages(messages_da, this, "da");
        }
        #endregion
    }
}