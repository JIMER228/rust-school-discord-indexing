using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Newtonsoft.Json;
using VLB;
using HarmonyLib;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Wallpaper Planner", "RobJ/Razor", "1.0.7")]
    [Description("Intercepts third-click on the Wallpaper Tool and shows a custom skin selector.")]
    public class WallpaperPlanner : RustPlugin
    {
        public static WallpaperPlanner Instance;
        private ItemDefinition WallItem;
        private ItemDefinition FloorItem;
        private ItemDefinition CeilingItem;
        private const string effect = "assets/prefabs/wallpaper/effects/place.prefab";
        private static string usePerm = "wallpaperplanner.use";
        private static string useOutPerm = "wallpaperplanner.outside";
        private static string adminPerm = "wallpaperplanner.admin";

        public static BUTTON button;

        #region Update Skin Add Location
        /*
       *  DoNot Forget to change version to new version if want to force new added skins below.
       * 
       *  if (configData.Version < new VersionNumber(1, 0, 1))
       *     AddNewSkins();
       *     
       *  Example of list add.
       *  
       *  new SkinInfo() { skinid = (ulong)3492705108, name = "Green Bamboo" },
       *  new SkinInfo() { skinid = (ulong)123456789, name = "Underground AQH1AN==" }
       *     
       */
        private List<SkinInfo> NewSkinsWall = new List<SkinInfo>()
        {
          new SkinInfo() { skinid = (ulong)3502949294, name = "Underwater Abyss Wallpaper" },
          new SkinInfo() { skinid = (ulong)3495643608, name = "Hemp Farmers Wallpaper" }, 
        };

        private List<SkinInfo> NewSkinsFloor = new List<SkinInfo>()
        {
          new SkinInfo() { skinid = (ulong)3498348852, name = "Killers Lair Floor 1x2" },
          new SkinInfo() { skinid = (ulong)3498352589, name = "Killers Lair Floor 2x2" },
          new SkinInfo() { skinid = (ulong)3493617039, name = "Grip Metal Plate Floor" },
        };

        private List<SkinInfo> NewSkinsCeling = new List<SkinInfo>()
        {
          new SkinInfo() { skinid = (ulong)3495987692, name = "Welded Steel Ceiling" },
          new SkinInfo() { skinid = (ulong)3494116957, name = "Painted Plate Ceiling" },
        };

        private void AddNewSkins()
        {
            PrintWarning("Auto adding in some new skins!");

            foreach (var update in NewSkinsWall)
            {
                bool skip = false;

                foreach (var wall in configData.settingsW.info)
                {
                    if (wall.skinid == update.skinid)
                    {
                        skip = true;
                        break;
                    }
                }

                if (!skip)
                    configData.settingsW.info.Add(update);
            }

            foreach (var updateF in NewSkinsFloor)
            {
                bool skip = false;

                foreach (var floor in configData.settingsF.info)
                {
                    if (floor.skinid == updateF.skinid)
                    {
                        skip = true;
                        break;
                    }
                }
                if (!skip)
                    configData.settingsF.info.Add(updateF);
            }

            foreach (var updateC in NewSkinsCeling)
            {
                bool skip = false;

                foreach (var celing in configData.settingsC.info)
                {
                    if (celing.skinid == updateC.skinid)
                    {
                        skip = true;
                        break;
                    }
                }
                if (!skip)
                    configData.settingsC.info.Add(updateC);
            }

            SaveConfig();
        }
        #endregion

        #region Init/Unload
        private void UpdateAllSkins()
        {
            configData.settingsW.info = new List<SkinInfo>();
            configData.settingsC.info = new List<SkinInfo>();
            configData.settingsF.info = new List<SkinInfo>();

            configData.settingsW.info.Add(new SkinInfo() { skinid = (ulong)3502949294, name = "Underwater Abyss Wallpaper" });
            configData.settingsW.info.Add(new SkinInfo() { skinid = (ulong)3495643608, name = "Hemp Farmers Wallpaper" });
            configData.settingsW.info.Add(new SkinInfo() { skinid = (ulong)3492705108, name = "Green Bamboo" });
            configData.settingsW.info.Add(new SkinInfo() { skinid = (ulong)3489063744, name = "Underground Tiles" });
            configData.settingsW.info.Add(new SkinInfo() { skinid = (ulong)3489094384, name = "Underground Tiles Logo" });
            configData.settingsW.info.Add(new SkinInfo() { skinid = (ulong)3475326027, name = "Vintage Jungle Wallpaper" });
            foreach (ItemSkinDirectory.Skin skin in WallpaperSettings.WallpaperItemDef.skins)
            {
                configData.settingsW.info.Add(new SkinInfo() { skinid = (ulong)skin.id, name = skin.invItem.displayName.english.Trim() });
            }
            
            configData.settingsF.info.Add(new SkinInfo() { skinid = (ulong)3498348852, name = "Killers Lair Floor 1x2" });
            configData.settingsF.info.Add(new SkinInfo() { skinid = (ulong)3498352589, name = "Killers Lair Floor 2x2" });
            configData.settingsF.info.Add(new SkinInfo() { skinid = (ulong)3493617039, name = "Grip Metal Plate Floor" });
            configData.settingsF.info.Add(new SkinInfo() { skinid = (ulong)3492777466, name = "Green Bamboo Floor" });
            configData.settingsF.info.Add(new SkinInfo() { skinid = (ulong)3491102082, name = "Underground Floor" });
            foreach (ItemSkinDirectory.Skin skin in WallpaperSettings.FlooringItemDef.skins)
            {
                configData.settingsF.info.Add(new SkinInfo() { skinid = (ulong)skin.id, name = skin.invItem.displayName.english.Trim() });
            }

            configData.settingsC.info.Add(new SkinInfo() { skinid = (ulong)3495987692, name = "Welded Steel Ceiling" });
            configData.settingsC.info.Add(new SkinInfo() { skinid = (ulong)3494116957, name = "Painted Plate Ceiling" });
            configData.settingsC.info.Add(new SkinInfo() { skinid = (ulong)3492711354, name = "Green Bamboo Ceiling" });
            configData.settingsC.info.Add(new SkinInfo() { skinid = (ulong)3491003267, name = "Underground Ceiling" });
            foreach (ItemSkinDirectory.Skin skin in WallpaperSettings.CeilingItemDef.skins)
            {
                configData.settingsC.info.Add(new SkinInfo() { skinid = (ulong)skin.id, name = skin.invItem.displayName.english.Trim() });
            }
            SaveConfig();
        }

        private void OnServerInitialized()
        {
            Instance = this;
            if (configData.settings == null)
            {
                configData.settings = new ConfigData.Settings();
                configData.settings.inputkey = "FIRE_THIRD";
                SaveConfig();
            }

            if (!Enum.TryParse(configData.settings.inputkey, true, out button))
            {
                Puts($"ERROR BUTTON NOT CORRECT: {configData.settings.inputkey} resetting to FIRE_THIRD");
                Enum.TryParse("FIRE_THIRD", true, out button);
            }
            WallItem = ItemManager.FindItemDefinition("wallpaper.wall");
            FloorItem = ItemManager.FindItemDefinition("wallpaper.flooring");
            CeilingItem = ItemManager.FindItemDefinition("wallpaper.ceiling");
            permission.RegisterPermission(usePerm, this);
            permission.RegisterPermission(useOutPerm, this);
            permission.RegisterPermission(adminPerm, this);

            if (configData.settingsW.info.Count <= 0 && configData.settingsF.info.Count <= 0 && configData.settingsC.info.Count <= 0)
                UpdateAllSkins();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player == null || !permission.UserHasPermission(player.UserIDString, usePerm) || !player.svActiveItemID.IsValid)
                    continue;

                Item item = player.GetActiveItem();
                if (item != null && item.info.shortname == "wallpaper.tool")
                    InputHelper.EnableHelper(player);
            }
        }

        private void Unload()
        {
            foreach (var helper in InputHelper._AllHelpers)
                UnityEngine.Object.DestroyImmediate(helper);

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                DestroyUI(player);

            Instance = null;
        }
        #endregion

        #region Config
        public class SkinInfo
        {
            [JsonProperty(PropertyName = "SkinID")]
            public ulong skinid { get; set; }

            [JsonProperty(PropertyName = "Wallpaper Name")]
            public string name { get; set; }
        }

        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "General settings")]
            public Settings settings { get; set; }

            [JsonProperty(PropertyName = "Wall")]
            public SettingsW settingsW { get; set; }

            [JsonProperty(PropertyName = "Floor")]
            public SettingsF settingsF { get; set; }

            [JsonProperty(PropertyName = "Ceiling")]
            public SettingsC settingsC { get; set; }

            public class Settings
            {
                [JsonProperty(PropertyName = "UI input key")]
                public string inputkey;
            }

            public class SettingsW
            {
                [JsonProperty(PropertyName = "SkinID's")]
                public List<SkinInfo> info;
            }

            public class SettingsF
            {
                [JsonProperty(PropertyName = "SkinID's")]
                public List<SkinInfo> info;
            }

            public class SettingsC
            {
                [JsonProperty(PropertyName = "SkinID's")]
                public List<SkinInfo> info;
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
                    inputkey = "FIRE_THIRD"
                },

                settingsW = new ConfigData.SettingsW
                {
                    info = new List<SkinInfo>()
                },

                settingsF = new ConfigData.SettingsF
                {
                    info = new List<SkinInfo>()
                },

                settingsC = new ConfigData.SettingsC
                {
                    info = new List<SkinInfo>()
                },

                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 0, 7))
                configData = baseConfig;

            if (configData.Version < new VersionNumber(1, 0, 7))
                AddNewSkins();

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion Config

        #region Input Hook
        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, usePerm))
                return;

            if (newItem != null && newItem.info.shortname == "wallpaper.tool")
            {
                InputHelper.EnableHelper(player);
            }
            else if (oldItem != null && oldItem.info.shortname == "wallpaper.tool")
            {
                InputHelper.DisableHelper(player);
            }

        }
        #endregion

        #region Placement Helper
        public class InputHelper : FacepunchBehaviour
        {
            public static HashSet<InputHelper> _AllHelpers = new HashSet<InputHelper>();
            public BasePlayer player { get; private set; }
            private float NextInputTime { get; set; }
            public bool OutSide { get; set; }

            public static void EnableHelper(BasePlayer target)
            {
                InputHelper helper = target.GetOrAddComponent<InputHelper>();
                helper.player = target;
                SendGameTip(target, String.Format(Instance.lang.GetMessage("helptext", Instance, target.UserIDString), Instance.configData.settings.inputkey));
                helper.OutSide = Instance.permission.UserHasPermission(target.UserIDString, useOutPerm);
                target.SendNetworkUpdate();
            }

            public static void DisableHelper(BasePlayer target)
            {
                InputHelper helper = target.GetComponent<InputHelper>();
                if (helper != null)
                {
                    UnityEngine.Object.Destroy(helper);
                    _AllHelpers.Remove(helper);
                }

                target.SendNetworkUpdate();
            }

            private void Update()
            {
                if (player == null || Instance == null || button == null)
                {
                    _AllHelpers.Remove(this);
                    Destroy(this);
                    return;
                }

                if (player.serverInput.WasJustPressed(button) && NextInputTime < UnityEngine.Time.realtimeSinceStartup)
                {
                    NextInputTime = UnityEngine.Time.realtimeSinceStartup + 0.05f;
                    CheckInput(false);
                }
                else if (OutSide && player.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY) && NextInputTime < UnityEngine.Time.realtimeSinceStartup)
                {
                    NextInputTime = UnityEngine.Time.realtimeSinceStartup + 0.05f;
                    CheckInput(true);
                }
            }

            private void CheckInput(bool state)
            {
                var active = player.GetActiveItem();

                if (active == null || active.info.shortname != "wallpaper.tool")
                {
                    Destroy(this);
                    return;
                }

                if (!state)
                {
                    Instance.ShowCustomUI(player);
                }
                else if (player.CanBuild())
                {

                    global::WallpaperPlanner entity = active.GetHeldEntity() as global::WallpaperPlanner;
                    if (entity == null)
                        return;

                    Vector3 origin = player.eyes.position;
                    Vector3 direction = player.eyes.HeadForward();
                    float sphereRadius = 0.2f;
                    float maxDistance = 13f;

                    RaycastHit hit;
                    if (UnityEngine.Physics.SphereCast(origin, sphereRadius, direction, out hit, maxDistance))
                    {
                        var block = hit.collider?.GetComponentInParent<BuildingBlock>();

                        if (block != null)
                        {
                            if (block.CanSeeWallpaperSocket(player, 1) && (block.ShortPrefabName.Contains("wall") || block.ShortPrefabName.Contains("floor")))
                            {
                                if (!entity.CanAffordToPlace(null))
                                {
                                    ItemDefinition placementPrice = ItemManager.FindItemDefinition(entity.placementPrice.itemid);
                                    SendGameTip(player, string.Format(Instance.lang.GetMessage("broke", Instance, player.UserIDString), entity.placementPrice.amount, placementPrice.displayName.english));
                                    return;
                                }

                                player.inventory.Take((List<Item>)null, entity.placementPrice.itemid, (int)entity.placementPrice.amount);
                                player.Command("note.inv", (object)entity.placementPrice.itemid, (object)((int)entity.placementPrice.amount * -1));
                                ulong skinID = block.ShortPrefabName.Contains("wall") ? entity.wallSkinID : entity.flooringSkinID;
                                Effect.server.Run(effect, block.transform.position);
                                block.RemoveWallpaper(1);
                                block.SetWallpaper(skinID, 1);
                                block.SetConditionalModel(block.currentSkin.DetermineConditionalModelState(block));
                                block.ClientRPC(RpcTarget.NetworkGroup("RefreshSkin"));
                                block.SendNetworkUpdateImmediate();
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region UI
        public static ulong SecretKey = GenerateKey();
        public static ulong GenerateKey() => (ulong)UnityEngine.Random.Range(1, 9999999999999999);

        private const string command = "customwallpaper.command";
        public const string MainPanel = "customwallpaper.MainPanel";
        public const string MainPanelSub = "customwallpaper.MainPanelSub";
        public const string MainBodyPanel = "customwallpaper.MainBodyPanel";

        public const string MainPanelPlayersScroll = "customwallpaper.MainPanelPlayersScroll";
        public const string MainPanelPlayersScroll2 = "customwallpaper.MainPanelPlayersScroll2";
        public const string MainPanelPlayersScroll1 = "customwallpaper.MainPanelPlayersScroll1";

        private void DestroyUI(BasePlayer player)
        {
            if (player != null)
                CuiHelper.DestroyUi(player, MainPanel);
        }

        private void ShowCustomUI(BasePlayer player, int page = 0)
        {
            var item = player.GetActiveItem();
            if (item == null)
            {
                player.ChatMessage(lang.GetMessage("errorhold", this, player.UserIDString));
                return;
            }

            var heldEnt = item.GetHeldEntity() as global::WallpaperPlanner;

            if (heldEnt == null)
            {
                player.ChatMessage(lang.GetMessage("errorhold", this, player.UserIDString));
                return;
            }

            List<SkinInfo> skinIDs = new List<SkinInfo>();
            string type = "";
            int itemID = 0;

            switch ((int)heldEnt.currentMode)
            {
                case 1:
                    skinIDs = configData.settingsW.info;
                    type = "WALL";
                    itemID = WallItem.itemid;
                    break;
                case 2:
                    skinIDs = configData.settingsF.info;
                    type = "FLOOR";
                    itemID = FloorItem.itemid;
                    break;
                case 3:
                    skinIDs = configData.settingsC.info;
                    type = "CEILING";
                    itemID = CeilingItem.itemid;
                    break;
            }

            bool isAdmin = permission.UserHasPermission(player.UserIDString, adminPerm);

            CuiElementContainer mainPanel = CreatePanel(MainPanel, "Hud", "0 0", "1 1", "0 0", "0 0");
            AddPanel(mainPanel, MainPanel, MainPanelSub, "0.2 0.2 0.2 0.9", "0 0", "1 1", "0 0", "0 0", true);
            AddButton(mainPanel, MainPanelSub, $"customwallpaper.ButtonsCancel", "", 0, "0 0 0 0", "0 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} close", "0 0", "1 1", "0 0", "0 0");

            AddPanel(mainPanel, MainPanelSub, MainBodyPanel, "0.7 0.7 0.7 0.3", "0.2 0.2", "0.8 0.8", "0 0", "0 0", true);
            CreateLable(mainPanel, MainBodyPanel, "", $"SKINS FOR {type}", 20, "1 1 1 0.95", TextAnchor.MiddleLeft, "0.02 0.92", "0.3 1", "0 0", "0 0");


            int position = 0;
            int positionStop = 0;

            double b = 0.65, d = 0.85, a = 0.07, c = 0.185;

            for (int i = page; i < skinIDs.Count; i++)
            {
                position++;
                positionStop++;
                AddButton(mainPanel, MainBodyPanel, $"customwallpaper.Buttons{i}", "", 0, "0 0 0 0", "1 1 1 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} setskin {skinIDs[i].skinid}", $"{a} {b}", $"{c} {d}", "0 0", "0 0");
                AddImage(mainPanel, $"customwallpaper.Buttons{i}", $"customwallpaper.Image{i}", itemID, skinIDs[i].skinid, "1 1 1 1");
                CreateLable(mainPanel, $"customwallpaper.Buttons{i}", $"customwallpaper.text{i}", $"{skinIDs[i].name}", 13, "1 1 1 0.95", TextAnchor.LowerCenter, "0 0", "1 1", "0 0", "0 0");

                if (isAdmin)
                {
                    AddButton(mainPanel, $"customwallpaper.Buttons{i}", $"customwallpaper.GearButton{i}", "", 0, "1 1 1 1", "0.8 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} editskin {i} {type.ToLower()}", $"0.01 0.91", "0.11 0.99", "0 0", "0 0");
                    CreateElement(mainPanel, $"customwallpaper.GearButton{i}", $"customwallpaper.Gear{i}", "assets/icons/gear.png", "0 0", "1 1", "1 1 1 1", "");
                }

                a += 0.15; c += 0.15;

                if (position == 6)
                {
                    b -= 0.25; d -= 0.25;
                    a = 0.07; c = 0.185;
                    position = 0;
                }

                if (positionStop == 18)
                    break;
            }

            if (positionStop == 18 && skinIDs.Count > 18 + page)
                AddButton(mainPanel, MainBodyPanel, "customwallpaper.nextpage", ">>>", 26, "1 1 1 1", "1 1 1 0.2", TextAnchor.MiddleCenter, $"{command} {SecretKey} nextpage {page}", "0.7 0.02", "0.8 0.09", "0 0", "0 0");

            if (page > 17)
                AddButton(mainPanel, MainBodyPanel, "customwallpaper.backpage", "<<<", 26, "1 1 1 1", "1 1 1 0.2", TextAnchor.MiddleCenter, $"{command} {SecretKey} backpage {page}", "0.2 0.02", "0.3 0.09", "0 0", "0 0");

            if (isAdmin)
                AddButton(mainPanel, MainPanel, "customwallpaper.adminadd", lang.GetMessage("adminadd", this, player.UserIDString), 14, "1 1 1 1", "0.3 0.6 0.3 1", TextAnchor.MiddleCenter, $"{command} {SecretKey} admin_add_open {page}", "0.80 0.92", "0.98 0.98", "0 0", "0 0");

            CuiHelper.AddUi(player, mainPanel);
        }

        private void BuildAdminUI(BasePlayer player, string backPage)
        {
            if (!SaveTrack.TryGetValue(player.UserIDString, out var trackInfo))
                return;

            CuiElementContainer mainPanel = CreatePanel(MainPanel, "Hud", "0 0", "1 1", "0 0", "0 0");
            AddPanel(mainPanel, MainPanel, MainPanelSub, "0.2 0.2 0.2 0.9", "0 0", "1 1", "0 0", "0 0", true);
            AddButton(mainPanel, MainPanelSub, $"customwallpaper.ButtonsCancel", "", 0, "0 0 0 0", "0 0 0 0", TextAnchor.MiddleCenter, $"{command} {SecretKey} goback {backPage}", "0 0", "1 1", "0 0", "0 0");

            AddPanel(mainPanel, MainPanelSub, MainBodyPanel, "0.7 0.7 0.7 0.3", "0.2 0.2", "0.8 0.8", "0 0", "0 0", true);
            CreateLable(mainPanel, MainBodyPanel, "", trackInfo.isEdit ? lang.GetMessage("editskin", this, player.UserIDString) : lang.GetMessage("addskin", this, player.UserIDString), 20, "1 1 1 0.95", TextAnchor.MiddleLeft, "0.02 0.92", "0.3 1", "0 0", "0 0");

            CreateLable(mainPanel, MainBodyPanel, "wallpaperplanner.lable5", lang.GetMessage("name", this, player.UserIDString), 22, "1 1 1 0.7", TextAnchor.MiddleLeft, "0.2 0.68", "0.32 0.75", "0 0", "0 0");
            AddPanel(mainPanel, MainBodyPanel, "wallpaperplanner.name", "1 1 1 0.2", "0.33 0.68", "0.63 0.75", "0 0", "0 0");
            AddTextBox(mainPanel, "wallpaperplanner.name", "wallpaperplanner.name1", "1 1 1 1", $"{trackInfo.name}", 13, TextAnchor.MiddleLeft, $"{command} {SecretKey} savename", "0.03 0", "1 1");

            if (!trackInfo.isEdit)
            {
                CreateLable(mainPanel, MainBodyPanel, "wallpaperplanner.lable6", lang.GetMessage("skin", this, player.UserIDString), 22, "1 1 1 0.7", TextAnchor.MiddleLeft, "0.2 0.59", "0.32 0.66", "0 0", "0 0");
                AddPanel(mainPanel, MainBodyPanel, "wallpaperplanner.skin", "1 1 1 0.2", "0.33 0.59", "0.63 0.66", "0 0", "0 0");
                AddTextBox(mainPanel, "wallpaperplanner.skin", "wallpaperplanner.skin1", "1 1 1 1", $"{trackInfo.skin} ", 13, TextAnchor.MiddleLeft, $"{command} {SecretKey} saveskin", "0.03 0", "1 1"); // Remoced to force you to go to config to change skin.

                AddButton(mainPanel, MainBodyPanel, $"customwallpaper.ButtonsSaveWall", lang.GetMessage("savewall", this, player.UserIDString), 14, "1 1 1 0.9", "0.549 0.725 0.239 0.6", TextAnchor.MiddleCenter, $"{command} {SecretKey} savenew wall {backPage}", "0.2 0.05", "0.34 0.12", "0 0", "0 0");
                AddButton(mainPanel, MainBodyPanel, $"customwallpaper.ButtonsSaveFloor", lang.GetMessage("savefloor", this, player.UserIDString), 14, "1 1 1 0.9", "0.549 0.725 0.239 0.6", TextAnchor.MiddleCenter, $"{command} {SecretKey} savenew floor {backPage}", "0.43 0.05", "0.57 0.12", "0 0", "0 0");
                AddButton(mainPanel, MainBodyPanel, $"customwallpaper.ButtonsSaveCeiling", lang.GetMessage("saveceiling", this, player.UserIDString), 14, "1 1 1 0.9", "0.549 0.725 0.239 0.6", TextAnchor.MiddleCenter, $"{command} {SecretKey} savenew ceiling {backPage}", "0.66 0.05", "0.8 0.12", "0 0", "0 0");
            }
            else
            {
                AddButton(mainPanel, MainBodyPanel, $"customwallpaper.ButtonsSaveWall", lang.GetMessage("savechanges", this, player.UserIDString), 14, "1 1 1 0.9", "0.549 0.725 0.239 0.6", TextAnchor.MiddleCenter, $"{command} {SecretKey} saveedit {backPage}", "0.2 0.05", "0.34 0.12", "0 0", "0 0");
                AddButton(mainPanel, MainBodyPanel, $"customwallpaper.ButtonsSaveCeiling", lang.GetMessage("delete", this, player.UserIDString), 14, "1 1 1 0.9", "0.8 0 0 0.6", TextAnchor.MiddleCenter, $"{command} {SecretKey} deleteedit {backPage}", "0.66 0.05", "0.8 0.12", "0 0", "0 0");
            }
            CuiHelper.AddUi(player, mainPanel);
        }
        #endregion

        #region UI Command
        public static Dictionary<string, configTrack> SaveTrack = new Dictionary<string, configTrack>();
        public class configTrack
        {
            public string name = "";
            public ulong skin;
            public string lastSkin = "";
            public bool isEdit;
            public int elementat;
        }

        [ChatCommand("wallpaperplanner")]
        private void OpenWallpaperPlannerCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, usePerm))
            {
                SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
            ShowCustomUI(player);
        }

        [ConsoleCommand(command)]
        private void CmdApplySkin(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (arg == null || arg.Args == null || arg.Args.Length < 2)
                return;

            if (player == null || !ulong.TryParse(arg.Args[0], out var key))
                return;

            if (key != SecretKey)
                return;

            switch (arg.Args[1])
            {
                case "close":
                    {
                        DestroyUI(player);
                        if (SaveTrack.ContainsKey(player.UserIDString))
                            SaveTrack.Remove(player.UserIDString);
                        break;
                    }

                case "admin_add_open":
                    {
                        if (!SaveTrack.ContainsKey(player.UserIDString))
                            SaveTrack.Add(player.UserIDString, new configTrack());
                        else
                            SaveTrack[player.UserIDString] = new configTrack();

                        BuildAdminUI(player, arg.Args[2]);
                        break;
                    }

                case "editskin":
                    {
                        if (!int.TryParse(arg.Args[2], out var position))
                            return;

                        if (!SaveTrack.ContainsKey(player.UserIDString))
                            SaveTrack.Add(player.UserIDString, new configTrack());

                        SkinInfo info = null;

                        switch (arg.Args[3])
                        {
                            case "wall":
                                info = configData.settingsW.info.ElementAt(position);
                                SaveTrack[player.UserIDString] = new configTrack() { elementat = position, isEdit = true, name = info.name, skin = info.skinid, lastSkin = info.skinid.ToString() };
                                BuildAdminUI(player, "wall");
                                break;

                            case "floor":
                                info = configData.settingsF.info.ElementAt(position);
                                SaveTrack[player.UserIDString] = new configTrack() { elementat = position, isEdit = true, name = info.name, skin = info.skinid, lastSkin = info.skinid.ToString() };
                                BuildAdminUI(player, "floor");
                                break;

                            case "ceiling":
                                info = configData.settingsC.info.ElementAt(position);
                                SaveTrack[player.UserIDString] = new configTrack() { elementat = position, isEdit = true, name = info.name, skin = info.skinid, lastSkin = info.skinid.ToString() };
                                BuildAdminUI(player, "ceiling");
                                break;
                        }

                        break;
                    }

                case "saveedit":
                    {
                        if (!SaveTrack.TryGetValue(player.UserIDString, out var trackInfo))
                            return;

                        if (string.IsNullOrEmpty(trackInfo.name))
                        {
                            SendGameTip(player, lang.GetMessage("errorname", this, player.UserIDString));
                            return;
                        }

                        if (!ulong.TryParse(trackInfo.lastSkin, out var values))
                        {
                            SendGameTip(player, lang.GetMessage("errorid", this, player.UserIDString));
                            return;
                        }

                        SkinInfo info = null;

                        switch (arg.Args[2])
                        {
                            case "wall":
                                info = configData.settingsW.info.ElementAt(trackInfo.elementat);
                                if (info != null)
                                {
                                    info.name = trackInfo.name; info.skinid = trackInfo.skin;
                                    ShowCustomUI(player, 0);
                                }
                                break;

                            case "floor":
                                info = configData.settingsF.info.ElementAt(trackInfo.elementat);
                                if (info != null)
                                {
                                    info.name = trackInfo.name; info.skinid = trackInfo.skin;
                                    ShowCustomUI(player, 0);
                                }
                                break;

                            case "ceiling":
                                info = configData.settingsC.info.ElementAt(trackInfo.elementat);
                                if (info != null)
                                {
                                    info.name = trackInfo.name; info.skinid = trackInfo.skin;
                                    ShowCustomUI(player, 0);
                                }
                                break;
                        }
                        SaveConfig();
                        break;
                    }

                case "deleteedit":
                    {
                        if (!SaveTrack.TryGetValue(player.UserIDString, out var trackInfo))
                            return;

                        switch (arg.Args[2])
                        {
                            case "wall":
                                configData.settingsW.info.RemoveAt(trackInfo.elementat);
                                ShowCustomUI(player, 0);
                                break;

                            case "floor":
                                configData.settingsF.info.RemoveAt(trackInfo.elementat);
                                ShowCustomUI(player, 0);
                                break;

                            case "ceiling":
                                configData.settingsC.info.RemoveAt(trackInfo.elementat);
                                ShowCustomUI(player, 0);
                                break;
                        }
                        SaveConfig();
                        break;
                    }

                case "goback":
                    {
                        if (SaveTrack.ContainsKey(player.UserIDString))
                            SaveTrack.Remove(player.UserIDString);
                        if (int.TryParse(arg.Args[2], out var page))
                            ShowCustomUI(player, page);
                        else
                            ShowCustomUI(player, 0);
                        break;
                    }

                case "savenew":
                    {
                        if (!SaveTrack.TryGetValue(player.UserIDString, out var trackInfo))
                            return;

                        if (string.IsNullOrEmpty(trackInfo.name))
                        {
                            SendGameTip(player, lang.GetMessage("errorname", this, player.UserIDString));
                            return;
                        }

                        if (!ulong.TryParse(trackInfo.lastSkin, out var values))
                        {
                            SendGameTip(player, lang.GetMessage("errorid", this, player.UserIDString));
                            return;
                        }

                        switch (arg.Args[2])
                        {
                            case "wall":
                                foreach (var skins in configData.settingsW.info)
                                {
                                    if (skins.skinid == trackInfo.skin)
                                    {
                                        SendGameTip(player, string.Format(lang.GetMessage("exists", this, player.UserIDString), skins.name));
                                        return;
                                    }
                                }
                                configData.settingsW.info.Add(new SkinInfo() { name = trackInfo.name, skinid = trackInfo.skin });
                                break;

                            case "floor":
                                foreach (var skins in configData.settingsF.info)
                                {
                                    if (skins.skinid == trackInfo.skin)
                                    {
                                        SendGameTip(player, string.Format(lang.GetMessage("exists", this, player.UserIDString), skins.name));
                                        return;
                                    }
                                }
                                configData.settingsF.info.Add(new SkinInfo() { name = trackInfo.name, skinid = trackInfo.skin });
                                break;

                            case "ceiling":
                                foreach (var skins in configData.settingsC.info)
                                {
                                    if (skins.skinid == trackInfo.skin)
                                    {
                                        SendGameTip(player, string.Format(lang.GetMessage("exists", this, player.UserIDString), skins.name));
                                        return;
                                    }
                                }
                                configData.settingsC.info.Add(new SkinInfo() { name = trackInfo.name, skinid = trackInfo.skin });
                                break;
                        }
                        SaveConfig();

                        if (int.TryParse(arg.Args[3], out var page))
                            ShowCustomUI(player, page);

                        SendGameTip(player, string.Format(lang.GetMessage("saved", this, player.UserIDString), arg.Args[2]));
                        break;
                    }

                case "savename":
                    {
                        if (!SaveTrack.TryGetValue(player.UserIDString, out var trackInfo))
                            return;

                        string values = string.Join(" ", arg.Args.Skip(2)).Trim();

                        if (string.IsNullOrEmpty(values))
                            trackInfo.name = "";
                        else
                            trackInfo.name = values;

                        break;
                    }

                case "saveskin":
                    {
                        if (!SaveTrack.TryGetValue(player.UserIDString, out var trackInfo))
                            return;

                        if (!ulong.TryParse(arg.Args[2], out var values))
                        {
                            SendGameTip(player, lang.GetMessage("errorid", this, player.UserIDString));
                        }
                        else
                        {
                            trackInfo.skin = values;
                        }

                        trackInfo.lastSkin = arg.Args[2];

                        break;
                    }

                case "nextpage":
                    {
                        if (int.TryParse(arg.Args[2], out var page))
                        {
                            ShowCustomUI(player, page + 18);
                        }
                        break;
                    }

                case "backpage":
                    {
                        if (int.TryParse(arg.Args[2], out var page))
                        {
                            ShowCustomUI(player, page - 18);
                        }
                        break;
                    }

                case "setskin":
                    {
                        DestroyUI(player);

                        if (ulong.TryParse(arg.Args[2], out var skinID))
                        {
                            var item = player.GetActiveItem();

                            if (item == null)
                            {
                                player.ChatMessage(lang.GetMessage("errorhold", this, player.UserIDString));
                                return;
                            }

                            var heldEnt = item.GetHeldEntity() as global::WallpaperPlanner;

                            if (heldEnt == null)
                            {
                                player.ChatMessage(lang.GetMessage("errorhold", this, player.UserIDString));
                            }
                            else if (heldEnt.GetOwnerPlayer() == player && (int)heldEnt.currentMode > 0 && (int)heldEnt.currentMode <= 3)
                            {
                                switch ((int)heldEnt.currentMode)
                                {
                                    case 1:
                                        heldEnt.wallSkinID = skinID;
                                        break;
                                    case 2:
                                        heldEnt.flooringSkinID = skinID;
                                        break;
                                    case 3:
                                        heldEnt.ceilingSkinID = skinID;
                                        break;
                                }

                                heldEnt.skinID = (ulong)skinID;
                                heldEnt.SendNetworkUpdate();
                                heldEnt.ClientRPC<ulong, int>(RpcTarget.NetworkGroup("CLIENT_ChangeSkin"), (ulong)skinID, (int)heldEnt.currentMode);
                            }
                        }
                        break;
                    }

                default:
                    break;
            }
        }
        #endregion

        #region UI Helpers
        private static CuiElementContainer CreatePanel(string panelName, string parent, string AnchorMin = "0.5 0", string AnchorMax = "0.5 0", string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            return new CuiElementContainer
            {
                new CuiElement
                {
                    Parent = parent, Name = panelName, DestroyUi = panelName,
                    Components = { new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax, } }
                }
            };
        }

        private static void AddPanel(CuiElementContainer container, string panelName, string panelButton, string color = "0.33 0.33 0.33 0.90", string AnchorMin = "0 0", string AnchorMax = "0 0", string OffsetMin = "-400 -200", string OffsetMax = "400 200", bool keyboard = false)
        {
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                KeyboardEnabled = keyboard,
                Image = { Color = color },
                RectTransform = { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax, }
            }, panelName, panelButton, panelButton);
        }

        private static void AddTextBox(CuiElementContainer container, string parent, string panelName, string Color, string text, int size, TextAnchor Align, string command, string AnchorMin = "0 0", string AnchorMax = "0 0", string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parent,
                Components =
                {
                    new CuiInputFieldComponent { NeedsKeyboard = true, Text = text, CharsLimit = 2000, Color = Color, IsPassword = false, Command = command, Font = "robotocondensed-regular.ttf", FontSize = size, Align = Align },
                    new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax }
                }
            });
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

        private static void CreateLable(CuiElementContainer container, string parent, string panelN, string message, int size, string color, TextAnchor anchor, string ancorMin, string ancorMax, string OffsetMin = "0 0", string OffsetMax = "0 0")
        {
            container.Add(new CuiLabel
            {
                Text = { Text = message, FontSize = size, Align = anchor, Color = color, Font = "RobotoCondensed-Bold.ttf" },
                RectTransform = { AnchorMin = ancorMin, AnchorMax = ancorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax }
            }, parent, panelN);
        }

        private static void AddButton(CuiElementContainer container, string panelName, string panelButton, string text, int testSize, string colorT, string colorB, TextAnchor anchor = TextAnchor.MiddleCenter, string usaageCommand = "", string AnchorMin = "0 0", string AnchorMax = "0 0", string OffsetMin = "236.5 30.0", string OffsetMax = "378 55.0")
        {
            container.Add(new CuiButton
            {
                Text = { Text = text, FontSize = testSize, Align = anchor, Color = colorT },
                Button = { Command = usaageCommand, Color = colorB },
                RectTransform = { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax, }
            }, panelName, panelButton, panelButton);
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
        #endregion

        #region Patch
        [AutoPatch]
        [HarmonyPatch(typeof(BuildingBlock), nameof(BuildingBlock.CheckWallpaper))]
        internal class PatchBuildingBlock
        {
            [HarmonyPrefix]
            internal static bool Prefix(BuildingBlock __instance)
            {
                if ((object)__instance == null || __instance.IsDestroyed)
                    return true;
                return false;
            }
        }
        #endregion

        #region Localization
        public static void SendGameTip(BasePlayer player, string message)
        {
            if (player != null)
                player.ShowToast(GameTip.Styles.Blue_Normal, message, true);
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["helptext"] = "PRESS THE {0} KEY TO OPEN CUSTOM WALLPAPER MENU",
                ["broke"] = "YOU NEED {0} {1} TO PLACE THIS",
                ["adminadd"] = "[ + ] ADD NEW",
                ["errorhold"] = "<color=orange>You need to be holding a Wallpaper Tool!</color>",
                ["savewall"] = "SAVE AS WALL",
                ["savefloor"] = "SAVE AS FLOOR",
                ["saveceiling"] = "SAVE AS CEILING",
                ["errorname"] = "Skin name can not be empty!",
                ["errorid"] = "Skin id is not the correct format!",
                ["name"] = "SKIN NAME:",
                ["skin"] = "SKIN ID:",
                ["saved"] = "NEW SKIN ADDED TO {0}",
                ["exists"] = "THIS SKIN EXISTS ALREADY AS NAME {0}",
                ["savechanges"] = "SAVE CHANGES",
                ["delete"] = "DELETE",
                ["editskin"] = "EDITING SKIN",
                ["addskin"] = "ADDING NEW SKIN",
                ["NoPermission"] = "<color=red>You do not have permission to use this command.</color>",
            }, this);
        }
        #endregion
    }
}
  