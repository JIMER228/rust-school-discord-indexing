using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Core;
using System;
using VLB;
using Network;
using ProtoBuf;
using Oxide.Game.Rust.Cui;
using Color = UnityEngine.Color;
using System.Globalization;
using UnityEngine;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Oxide.Core.Configuration;
using System.Collections;
using Oxide.Core.Libraries.Covalence;


namespace Oxide.Plugins
{

    [Info("SkinSpray", "Razor", "1.3.4")]
    [Description("SkinBox On SprayCan")]
    public class SkinSpray : RustPlugin
    {
        [PluginReference]
        private Plugin PlayerSkins;

        private static SkinSpray Instance;
        private static bool debug = false;
        private static bool _ImagesReady;
        public static List<BasePlayer> PlayerItem = new List<BasePlayer>();
        private readonly Dictionary<string, int> shortPrefabNameToDeployableName = new Dictionary<string, int>();
        private static readonly Hash<string, string> nameToshortname = new Hash<string, string>();
        private Coroutine QueuedRoutineLoadImages;
        public static List<string> knownBadWords = new List<string>() { "sex", "ass", "toon", "Tiffy", "Girl", "fbk", "aratya", "anya", "Nun", "Rina Bunji", "Blond", "Amino", "maid", "Anime" };
        public static List<ulong> knownBadIDS = new List<ulong>();
        private BasePlayer commandPlayer;

        public static class ConsoleCommands
        {
            public static class UI
            {
                public const string Command = "skinspray.ui";

                public static class Commands
                {
                    public const string Close = "close";
                    public const string Select = "select";
                    public const string IncrementPage = "nextpage";
                    public const string DecrementPage = "previouspage";
                }
            }
        }

        public static class Commands
        {
            public const string skinSpray = "skinspray";

            public static class ManageArguments
            {
                public const string Add = "add";
                public const string Remove = "remove";
                public const string Clear = "clear";
                public const string Workshop = "workshop";
            }

            public static class ManageWorkshop
            {
                public const string Get = "workshop";
                public const string New = "new";
                public const string Clear = "clear";
                public const string Workshop = "workshop";
            }

            public static class ManageArgumentsHelp
            {
                public const string Add = "/skinspray add <item displayName> <skinID>";
                public const string Remove = "/skinspray remove <item displayName> <skinID or all = all skins>";
                public const string Clear = "/skinbox clear <nothing at the moment>";
            }
        }

        private void Unload()
        {
            if (QueuedRoutineLoadImages != null)
                InvokeHandler.Instance.StopCoroutine(QueuedRoutineLoadImages);
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UI.Overlay.Select.Panel);
                if (player.GetComponent<SprayCanPlayer>() != null)
                    UnityEngine.GameObject.Destroy(player.GetComponent<SprayCanPlayer>());
            }
            Instance = null;
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(Permissions.permUse, this);
            permission.RegisterPermission(Permissions.permAdmin, this);
            if (configData.settings.blackListWords.Count > 0)
                foreach (string word in configData.settings.blackListWords)
                {
                    if (!knownBadWords.Contains(word.ToLower()))
                        knownBadWords.Add(word);
                }
            if (configData.settings.Wname.Count == null || configData.settings.Wname.Count <= 0)
            {
                configData.settings.Wname = disPlayNameToworkshopName;//new Dictionary<string, string>() { { "Wood Double Door", "Wooden Double Door" }, { "Rug Bear Skin", "Bearskin Rug" }, { "Large Wood Box", "Large Wood Box" }, { "Armored Door", "Armored Door" }, { "Armored Double Door", "Armored Double Door" }, { "Chair", "Chair" }, { "Concrete Barricade", "Concrete Barricade" }, { "Fridge", "Fridge" }, { "Furnace", "Furnace" }, { "Garage Door", "Garage Door" }, { "Locker", "Locker" }, { "Sandbag Barricade", "Sandbag Barricade" }, { "Sheet Metal Door", "Sheet Metal Door" }, { "Sheet Metal Double Door", "Sheet Metal Double Door" }, { "Sleeping Bag", "Sleeping Bag" }, { "Vending Machine", "Vending Machine" }, { "Wooden Double Door", "Wooden Double Door" }, { "Wooden Door", "Wooden Door" }, { "Wood Storage Box", "Wood Storage Box" }, { "Assault Rifle", "AK47" }, { "Rug", "Rug" } };
                SaveConfig();
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.GetComponent<SprayCanPlayer>() != null)
                    UnityEngine.GameObject.Destroy(player.GetComponent<SprayCanPlayer>());

                SprayCan can = player?.GetActiveItem()?.GetHeldEntity() as SprayCan;
                if (can != null)
                    player.GetOrAddComponent<SprayCanPlayer>();
            }

            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;

            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                nameToshortname[itemdef.displayName.english] = itemdef.shortname;
                var deployablePrefab = itemdef.GetComponent<ItemModDeployable>()?.entityPrefab?.resourcePath;

                if (string.IsNullOrEmpty(deployablePrefab))
                    continue;

                var shortPrefabName = GameManager.server.FindPrefab(deployablePrefab)?.GetComponent<BaseEntity>()?.ShortPrefabName;
                if (!string.IsNullOrEmpty(shortPrefabName) && !shortPrefabNameToDeployableName.ContainsKey(shortPrefabName))
                {
                    string newName = itemdef.displayName.english;
                    if (configData.settings.Wname.ContainsKey(newName))
                        shortPrefabNameToDeployableName.Add(shortPrefabName, itemdef.itemid);
                }
            }
            QueuedRoutineLoadImages = InvokeHandler.Instance.StartCoroutine(LoadWorkshopImages());
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.GetComponent<SprayCanPlayer>() != null)
                UnityEngine.GameObject.Destroy(player.GetComponent<SprayCanPlayer>());
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player.GetComponent<SprayCanPlayer>() != null)
                UnityEngine.GameObject.Destroy(player.GetComponent<SprayCanPlayer>());
        }

        static class Permissions
        {
            public const string permUse = "skinspray.use";
            public const string permAdmin = "skinspray.admin";
        }

        #region Configuration
        [JsonObject(MemberSerialization.OptIn)]
        class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; } = new Settings();

            [JsonProperty(PropertyName = "SkinBox Plugin Support")]
            public SkinBoxSupport skinboxSupport { get; set; } = new SkinBoxSupport();

            public class Settings
            {
                [JsonProperty("Use Permission")]
                public string PermissionUse { get; set; } = Permissions.permUse;

                [JsonProperty("Total Skins Per Item To Download")]
                public int TotalSkinsPerItem { get; set; } = 24;

                [JsonProperty("Allow non-approved skins to download")]
                public bool nonApproved { get; set; }

                [JsonProperty("Item Display Name / Steam Item WorkShop Name")]
                public Dictionary<string, string> Wname { get; set; } = new Dictionary<string, string>();

                [JsonProperty("Ulong BlackList")]
                public List<ulong> blackList { get; set; } = new List<ulong>();

                [JsonProperty("string BlackList Title Words")]
                public List<string> blackListWords { get; set; } = new List<string>();
            }

            public class SkinBoxSupport
            {
                //  [JsonProperty("Use SkinBox UI")]
                //  public bool useSkinBox { get; set; } = false;

                //  [JsonProperty("Downloaded Skin Lists from SkinBox for item")]
                // public float d = 0;
            }

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber Version { get; set; } = Instance.Version;

            public VersionNumber LastBreakingChange { get; private set; } = new VersionNumber(1, 1, 2);
        }
        #endregion

        #region Configuration Handling
        private ConfigData configData;

        protected override void LoadConfig()
        {
            Instance = this;

            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                    LoadDefaultConfig();
                UpdateConfigVersion();
            }
            catch
            {
                PrintError("Your configuration file is invalid");
                UpdateConfig();
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => configData = new ConfigData();

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfig()
        {
            PrintWarning("Invalid config file detected! Backing up current and creating new config...");
            var outdatedConfig = Config.ReadObject<object>();
            Config.WriteObject(outdatedConfig, filename: $"{Name}.Backup");
            LoadDefaultConfig();
            PrintWarning("Config update completed!");
        }

        void UpdateConfigVersion() => configData.Version = Version;
        #endregion

        #region Data
        public class SkinInfo
        {
            public BasePlayer player;
            public Item item;
            public BaseEntity entity;
            public SprayCan can;
        }

        bool DataFileExists(string path, string name = "")
        {
            if (String.IsNullOrEmpty(name))
                name = Name;
            return Interface.Oxide.DataFileSystem.ExistsDatafile($"{name}/{path}");
        }

        DynamicConfigFile GetDataFile(string path, string name = "")
        {
            if (String.IsNullOrEmpty(name))
                name = Name;
            return Interface.Oxide.DataFileSystem.GetFile($"{name}/{path}");
        }

        void SaveData(string filename, savedSkins dataType)
        {
            GetDataFile(filename).WriteObject(dataType);
        }

        class savedSkins
        {
            [JsonProperty("Optional list of skinid's")]
            public List<ulong> AdminSkinIds = new List<ulong>();
            [JsonProperty("Workshop and plugin shop skins")]
            public List<savedSkinsInfo> skinInfo = new List<savedSkinsInfo>();
        }

        class savedSkinsInfo
        {
            public string title;
            public ulong skinID;
        }

        bool TryLoadData(string filename, out savedSkins data)
        {
            if (DataFileExists(filename))
            {
                var file = GetDataFile(filename);
                try
                {
                    data = file.ReadObject<savedSkins>();
                    return true;
                }
                catch (Exception ex)
                {
                    PrintError($"Error reading data from {file.Filename}: ${ex.Message}");
                }
            }
            data = new savedSkins();
            return false;
        }
        #endregion

        #region SprayCan Conroler
        public class SprayCanPlayer : MonoBehaviour
        {
            private BasePlayer player { get; set; }
            private float nextPressTime { get; set; }
            private SprayCan theCan { get; set; }
            private int errors { get; set; }
            private ItemDefinition itemDefinition { get; set; }
            private Vector3 eyeOffset = new Vector3(4.8654f, 2.6241f, 1.3869f);

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("ItemCheck", 10, 11);
            }

            private bool ItemCheck()
            {
                if (this == null | Instance == null || player == null || player?.GetActiveItem()?.GetHeldEntity() as SprayCan == null)
                {
                    Destroy(this);
                    return true;
                }
                return false;
            }

            private void Update()
            {
                if (ItemCheck()) return;

                if (player.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY))
                {
                    float time = Time.realtimeSinceStartup;
                    if (nextPressTime < time)
                    {
                        theCan = player?.GetActiveItem()?.GetHeldEntity() as SprayCan;
                        if (theCan == null)
                        {
                            if (errors > 3)
                                Destroy(this);
                            errors++;
                            return;
                        }
                        nextPressTime = time + 0.1f;

                        if (!_ImagesReady)
                        {
                            Instance.SendReply(player, "Images not loaded yet try later.");
                            return;
                        }

                        object entityFind = FindItemLookingAt();

                        if (entityFind == null || theCan.IsBusy())
                        {
                            //Instance.SendReply(player, "No skinable entity found!");
                        }
                        else if (entityFind is BaseCombatEntity)
                        {
                            if (player.IsBuildingBlocked() && !Instance.permission.UserHasPermission(player.UserIDString, Permissions.permAdmin))
                                return;

                            BaseCombatEntity entity = entityFind as BaseCombatEntity;
                            if (entity is Door)
                            {
                                if (entity.name.Contains("door.hinged.industrial.a") || entity.name.Contains("door.hinged.industrial.d"))
                                {
                                    player.ChatMessage("This door can not be skined");
                                    return;
                                }

                                Door door4 = entity as Door;
                                if (!door4.GetPlayerLockPermission(player))
                                {
                                    player.ChatMessage("Door must be openable");
                                    return;
                                }
                                if (door4.IsOpen())
                                {
                                    player.ChatMessage("Door must be closed");
                                    return;
                                }
                            }
                            OpenSkinUI(entity);
                        }
                        else if (entityFind is DroppedItem)
                        {
                            Item dropped = (entityFind as DroppedItem).item;
                            if (Instance.configData.settings.Wname.ContainsKey(dropped.info.displayName.english))
                                Instance.OpenUI(player, dropped.info.displayName.english, null, theCan, dropped.info.itemid, dropped);
                            else
                                ShowGameTip(player, $"You can not skin the {dropped.info.displayName.english}!");
                        }
                    }
                }
            }

            private object FindItemLookingAt()
            {
                if (player == null || theCan == null) return null;

                var layers = Rust.Layers.Mask.Deployed | Rust.Layers.Mask.Construction | Rust.Layers.Mask.Physics_Debris;

                RaycastHit hit = new RaycastHit();

                if (eyeOffset != null && eyeOffset == Vector3.zero)
                    return null;

                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 2.3f, layers))
                {
                    return hit.GetEntity();
                }
                return null;
            }

            public void OpenSkinUI(BaseCombatEntity entity)
            {
                if (entity == null)
                {
                    return;
                }

                if (entity.pickup.itemTarget == null)
                {
                    int itemID;
                    if (Instance.shortPrefabNameToDeployableName.TryGetValue(entity.ShortPrefabName, out itemID))
                    {
                        var itemInfo = ItemManager.FindItemDefinition(itemID);
                        if (itemInfo != null)
                        {
                            Instance.OpenUI(player, itemInfo.displayName.english, (entity as BaseEntity), theCan, itemID);
                        }
                    }
                    return;
                }

                string itemName = entity?.pickup.itemTarget.displayName.english;
                if (string.IsNullOrEmpty(itemName))
                    return;

                if (Instance.configData.settings.Wname.ContainsKey(itemName))
                {
                    if (debug)Instance.SendReply(player, $"You can skin this item {itemName}");
                    Instance.OpenUI(player, itemName, (entity as BaseEntity), theCan, entity.pickup.itemTarget.itemid);
                }
                else
                {
                    if (debug)Instance.SendReply(player, $"You can not skin this item {itemName}");
                    ShowGameTip(player, $"No extra skins for the {itemName}!");
                }
            }
        }

        #endregion SprayCan Conroler

        #region Hooks
        private static void ShowGameTip(BasePlayer player, string text)
        {
            text = Formatter.ToPlaintext(text);

            player?.Command("gametip.hidegametip");
            player?.Command("gametip.showgametip", text);
            Instance.timer.In(4, () => player?.Command("gametip.hidegametip"));
        }

        [ChatCommand(Commands.skinSpray)]
        void CommandSkinSpray(BasePlayer player, string command, string[] args)
        {
            var c = 0;
            if (player != null && !permission.UserHasPermission(player.UserIDString, Permissions.permAdmin))
            {
                SendReply(player, $"You need the admin permission {Permissions.permAdmin}");
                return;
            }
            var operation = args.Length > 0 ? args[0].ToLower() : string.Empty;
            var secondArg = args.Length > 1 ? args[1] : string.Empty;
            var thirdArg = args.Length > c ? args[1] : string.Empty;
            savedSkins datafile = null;
            string name = "";
            string workShopName = "";

            switch (operation)
            {
                case Commands.ManageWorkshop.Get:
                    {
                        Item item = player?.GetActiveItem();
                        if (item == null)
                        {
                            SendReply(player, "You must be holding the item you want to work with");
                        }

                        if (secondArg == Commands.ManageWorkshop.New)
                        {
                            commandPlayer = player;
                            name = item.info.displayName.english;
                            if (!configData.settings.Wname.ContainsKey(name))
                            {
                                SendReply(player, $"{name} is not in your config as a valid workshop item.");
                            }
                            else
                            {
                                datafile = new savedSkins();
                                if (datafile != null)
                                {
                                    SendReply(player, $"{name} is geting a new list from the workshop.");
                                    paused = true;
                                    DownloadWorkshop(configData.settings.Wname[name], datafile, () => { finishDownload(name, datafile); }, "", 0);
                                }
                            }

                        }
                        break;
                    }
                case Commands.ManageArguments.Add:
                    {
                        Item item = player?.GetActiveItem();
                        if (item == null)
                        {
                            SendReply(player, "You must be holding the item you want to add");
                        }
                        else
                        {
                            name = item.info.displayName.english;
                            if (!configData.settings.Wname.ContainsKey(name))
                            {
                                if (QueuedRoutineLoadImages != null)
                                {
                                    commandPlayer = player;

                                    if (disPlayNameToworkshopName.ContainsKey(name))
                                        workShopName = disPlayNameToworkshopName[name];
                                    else workShopName = name;
                                    configData.settings.Wname.Add(name, workShopName);
                                    SaveConfig();
                                    QueuedRoutineLoadImages = InvokeHandler.Instance.StartCoroutine(LoadWorkshopImages());
                                    SendReply(player, $"{name} has been added to the config with workshop search text of {workShopName}, If no skins found try a difrent workshop search text");
                                }
                                else
                                {
                                    SendReply(player, $"You are currently adding a new skin item list please wait for it to finish downloading!");
                                }

                            }
                            else
                            {
                                SendReply(player, $"{name} already exists in the config.");
                            }
                        }
                        break;
                    }

                case Commands.ManageArguments.Remove:
                    {
                        Item item = player?.GetActiveItem();
                        if (item == null)
                        {
                            SendReply(player, "You must be holding the item you want to remove");
                        }
                        else if (configData.settings.Wname.ContainsKey(item.info.displayName.english))
                        {
                            configData.settings.Wname.Remove(item.info.displayName.english);
                            SendReply(player, $"{item.info.displayName.english} removed from config.");
                            SaveConfig();
                        }
                        else
                            SendReply(player, $"{item.info.displayName.english} is not in the config.");

                        break;
                    }
                default:
                    SendReply(player, "Usage:\n\n"
                        + $"{Commands.ManageArgumentsHelp.Add}"
                        + $"{Commands.ManageArgumentsHelp.Clear}");
                    break;
            }
        }

        private object OnSprayCreate(SprayCan theCan, Vector3 loc, Quaternion quaternion)
        {
            BasePlayer player = theCan?.GetOwnerPlayer();

            if (player != null && !permission.UserHasPermission(player.UserIDString, Permissions.permUse))
                return null;

            OpenUI(player, "Spray_Decals", null, theCan, -1366326648, null, loc, quaternion);
            return false;
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItemId)
        {
            if (player != null && !permission.UserHasPermission(player.UserIDString, Permissions.permUse))
                return;

            NextTick(() =>
            {
                SprayCan can = player?.GetActiveItem()?.GetHeldEntity() as SprayCan;
                if (can != null)
                {
                    player.GetOrAddComponent<SprayCanPlayer>();
                }
            });
        }

        private object IsSpraycanUser(BasePlayer userId)
        {
            if (userId == null || PlayerItem == null || !PlayerItem.Contains(userId)) return null;
            if (PlayerItem.Contains(userId)) return true;
            return null;
        }

        private void CreateSpray(SprayCan theCan, Vector3 vector, Quaternion rot, ulong skin)
        {
            if (theCan.IsBusy())
                return;
            theCan.ClientRPC<int>((Connection)null, "Client_ChangeSprayColour", -1);
            theCan.SetFlag(BaseEntity.Flags.Busy, true);
            theCan.Invoke(new Action(theCan.ClearBusy), theCan.SprayCooldown);

            //  if (Interface.CallHook("OnSprayCreate", (object)theCan, (float) a=, (object)rot) != null)
            //     return;
            BaseEntity entity = GameManager.server.CreateEntity(theCan.SprayDecalEntityRef.resourcePath, vector, rot);
            entity.skinID = skin;
            entity.OnDeployed((BaseEntity)null, theCan.GetOwnerPlayer(), theCan.GetItem());
            entity.Spawn();
            //theCan.CheckAchievementPosition(vector);
            theCan?.GetItem()?.LoseCondition(theCan.ConditionLossPerSpray);
        }

        private void reskinEntity(BasePlayer player, ulong skinID)
        {
            if (player == null || !playerUiData.ContainsKey(player.userID))
                return;

            BaseEntity wItem = null;
            BaseEntity heldEntity = null;
            NetworkableId newID = player.net.ID;
            var bFloat = 0;
            BaseEntity sprayEntity = playerUiData[player.userID].sprayEntity;
            SprayCan theCan = playerUiData[player.userID].sprayCan;
            Item dropped = playerUiData[player.userID].droppItem;
            string itemName = playerUiData[player.userID].item;
            Quaternion quaternion = playerUiData[player.userID].quaternion;
            Vector3 loc = playerUiData[player.userID].loc;
            Vector3 dPos = player.transform.position;
            

            if (itemName == "Spray_Decals")
            {
                CreateSpray(theCan, loc, quaternion, skinID);
            }
            else if (dropped != null)
            {
                dropped.skin = skinID;

                heldEntity = dropped.GetHeldEntity();
                if (heldEntity != null)
                {
                    heldEntity.skinID = skinID;
                    heldEntity.SendNetworkUpdateImmediate();
                    newID = heldEntity.net.ID;
                    dPos = heldEntity.transform.position;
                }

                wItem = dropped.GetWorldEntity();
                if (wItem != null)
                {
                    WorldItem worldItem = wItem as WorldItem;
                    if (worldItem != null)
                    {
                        newID = worldItem.net.ID;
                        dPos = worldItem.transform.position;
                        worldItem.skinID = skinID;
                        worldItem?.SendNetworkUpdateImmediate();
                        player?.SendNetworkUpdateImmediate();
                    }
                }
                CloseUI(player);
                if (theCan != null)
                {
                    theCan.ClientRPC<int, NetworkableId>((Connection)null, "Client_ReskinResult", 1, newID);
                    theCan.GetItem()?.LoseCondition(theCan.ConditionLossPerReskin);
                    theCan.SetFlag(BaseEntity.Flags.Busy, true);
                    theCan.Invoke(new Action(theCan.ClearBusy), theCan.SprayCooldown);
                }
                dropped.MarkDirty();
                dropped.Drop(dPos, Vector3.zero);
            }

            if (sprayEntity != null)
            {
                sprayEntity.skinID = skinID;
                sprayEntity?.SendNetworkUpdateImmediate();
                if (theCan != null)
                {
                    theCan.ClientRPC<int, NetworkableId>((Connection)null, "Client_ReskinResult", 1, sprayEntity.net.ID);
                    theCan.GetItem()?.LoseCondition(theCan.ConditionLossPerReskin);
                    theCan.SetFlag(BaseEntity.Flags.Busy, true);
                    theCan.Invoke(new Action(theCan.ClearBusy), theCan.SprayCooldown);
                    sprayEntity?.SendNetworkUpdateImmediate();

                }
                CloseDefaultUI(player, sprayEntity);
            }
        }

        private void CloseDefaultUI(BasePlayer player, BaseEntity sprayEntity)
        {
            if (player == null) return;

            if (sprayEntity == null)
                sprayEntity = playerUiData[player.userID].sprayEntity;

            if (sprayEntity == null) return;

            sprayEntity.limitNetworking = true;
            timer.Once(0.15f, () =>
            {
                if (sprayEntity != null)
                {
                    sprayEntity.limitNetworking = false;
                }
                NextTick(() => { sprayEntity?.SendNetworkUpdateImmediate(); player?.SendNetworkUpdateImmediate(); });
            });
        }

        private void OnEntityReskin(BaseEntity entity, ItemSkinDirectory.Skin skin, BasePlayer player)
        {
            if (player != null)
                CloseUI(player);
        }
        #endregion Hooks

        #region UI Elements
        public static class UI
        {
            public const string TransparentTexture = "assets/content/textures/generic/fulltransparent.tga";

            public static class Anchors
            {
#pragma warning disable IDE0051 // Remove unused private members
                public const string LowerLeft = "0 0";
                public const string LowerCenter = "0.5 0";
                public const string LowerRight = "1 0";
                public const string CenterLeft = "0 0.5";
                public const string Center = "0.5 0.5";
                public const string CenterRight = "1 0.5";
                public const string UpperLeft = "0 1";
                public const string UpperCenter = "0.5 1";
                public const string UpperRight = "1 1";
#pragma warning restore IDE0051 // Remove unused private members
            }

            public static class Colors
            {
                public const string Black = "0 0 0 1";
                public const string DarkGray = "0.12 0.12 0.12 1";
                public const string DarkGreen = "0.145 0.255 0.09 1";
                public const string DarkRed = "0.8 0 0 1";
                public const string DimGray = "0.33 0.33 0.33 1";
                public const string White = "1 1 1 1";

                public static class Transparent
                {
                    public const string Black75 = "0 0 0 0.75";
                    public const string Clear = "0 0 0 0";
                }
            }

            public static class Overlay
            {
                public const string Panel = "Overlay";

                public static class Select
                {
                    public static string Panel = "SkinSpray.Select";

                    public static class Header
                    {
                        public static string Panel = "SkinSpray.Select.Header";
                        public static string Title = "SkinSpray.Select.Header.Title";
                        public static string CloseButton = "SkinSpray.Select.Header.Close";
                    }

                    public static class Grid
                    {
                        public static string Panel = "SkinSpray.Select.Grid";
                        public static string Skin = "SkinSpray.Select.Grid.Skin";
                        public static string SkinButton = "SkinSpray.Select.Grid.Skin.Button";
                    }

                    public static class Navigation
                    {
                        public static string Panel = "SkinSpray.Select.Navigation";
                        public static string LeftButton = "SkinSpray.Select.Navigation.Left";
                        public static string PageNumber = "SkinSpray.Select.Navigation.Page";
                        public static string RightButton = "SkinSpray.Select.Navigation.Right";
                    }
                }
            }
        }

        private class PlayerUiData
        {
            public int page = 0;
            public string item = "door.hinged.metal";
            public BaseEntity sprayEntity;
            public SprayCan sprayCan;
            public int itemID;
            public Item droppItem;
            public Vector3 loc;
            public Quaternion quaternion;
        }

        Dictionary<ulong, PlayerUiData> playerUiData = new Dictionary<ulong, PlayerUiData>();

        private void OpenUI(BasePlayer player, string itemName, BaseEntity entity, SprayCan theCan, int itemID = 0, Item dropped = null, Vector3 loc = default(Vector3), Quaternion quaternion = new Quaternion())
        {
            CuiHelper.DestroyUi(player, UI.Overlay.Select.Panel);

            playerUiData[player.userID] = new PlayerUiData { item = itemName, sprayEntity = entity, sprayCan = theCan, itemID = itemID, droppItem = dropped, loc = loc, quaternion = quaternion };

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = UI.Colors.Transparent.Black75 },
                RectTransform = { AnchorMin = UI.Anchors.Center, AnchorMax = UI.Anchors.Center, OffsetMin = "390 -186", OffsetMax = "640 187" },
                CursorEnabled = true
            }, UI.Overlay.Panel, UI.Overlay.Select.Panel);

            // Header

            container.Add(new CuiPanel
            {
                Image = { Color = UI.Colors.Black },
                RectTransform = { AnchorMin = UI.Anchors.UpperLeft, AnchorMax = UI.Anchors.UpperRight, OffsetMin = "0 -20", OffsetMax = "0 0" }
            }, UI.Overlay.Select.Panel, UI.Overlay.Select.Header.Panel);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = UI.Anchors.LowerLeft, AnchorMax = UI.Anchors.UpperRight },
                Text = { Text = "Select Skin", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = UI.Colors.White }
            }, UI.Overlay.Select.Header.Panel, UI.Overlay.Select.Header.Title);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = UI.Anchors.LowerRight, AnchorMax = UI.Anchors.UpperRight, OffsetMin = "-27 0", OffsetMax = "0 0" },
                Text = { Text = "X", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = UI.Colors.White },
                Button = { Color = UI.Colors.DarkRed, Command = $"{ConsoleCommands.UI.Command} {ConsoleCommands.UI.Commands.Close}" }
            }, UI.Overlay.Select.Header.Panel, UI.Overlay.Select.Navigation.RightButton);

            CuiHelper.AddUi(player, container);

            BuildSkinGrid(player);
            BuildNavigationBar(player);
        }

        private void CloseUI(BasePlayer player)
        {
            playerUiData.Remove(player.userID);
            CuiHelper.DestroyUi(player, UI.Overlay.Select.Panel);
        }

        private void BuildSkinGrid(BasePlayer player)
        {
            var page = playerUiData[player.userID].page;
            var itemName = playerUiData[player.userID].item;
            var itemID = playerUiData[player.userID].itemID;

            List<savedSkinsInfo> SkinInfoOriginal = new List<savedSkinsInfo>();

            savedSkins datafile = null;


            if (!TryLoadData(itemName, out datafile))
                return;


            /*if (PlayerSkins != null)
			{
				if (nameToshortname.ContainsKey(itemName))
				{
					string itemNameConvert = nameToshortname[itemName];
					
					List<ulong> playerSkinList = PlayerSkins?.CallHook("hookGetPlayerSkins", player.userID, itemNameConvert, true) as List<ulong>;

					if (playerSkinList != null && playerSkinList.Count > 0)
					{
						foreach (ulong skinGot in playerSkinList)
							if (!skinImages.Contains(skinGot))
								skinImages.Add(skinGot);
					}
				}
			}*/
            foreach (var skinUlong in datafile.AdminSkinIds)
            {
                SkinInfoOriginal.Add(new savedSkinsInfo() { skinID = skinUlong, title = "" });
            }

            SkinInfoOriginal.AddRange(datafile.skinInfo);


            CuiHelper.DestroyUi(player, UI.Overlay.Select.Grid.Panel);

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = UI.Colors.Transparent.Clear },
                RectTransform = { AnchorMin = UI.Anchors.LowerLeft, AnchorMax = UI.Anchors.UpperRight, OffsetMin = "5 25", OffsetMax = "-5 -25" }
            }, UI.Overlay.Select.Panel, UI.Overlay.Select.Grid.Panel);

            var currentSkin = 0;
            var startSkin = 12 * page;
            var lastSkin = 11 + 12 * page;
            var skinColumn = 0;
            var skinRow = 0;
            var aFloat = 0;

            foreach (var skinID in SkinInfoOriginal)
            {
                if (currentSkin >= startSkin)
                {
                    var guid = Guid.NewGuid().ToString();
                    string guID1 = "";
                    container.Add(new CuiElement
                    {
                        Name = UI.Overlay.Select.Grid.Skin + guid,
                        Parent = UI.Overlay.Select.Grid.Panel,
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = UI.Anchors.UpperLeft,
                                AnchorMax = UI.Anchors.UpperLeft,
                                OffsetMin = $"{77 * skinColumn + 5 * skinColumn} {-77 - (77 * skinRow) - 5 * skinRow}",
                                OffsetMax = $"{77 + (77 * skinColumn) + 5 * skinColumn} {-(77 * skinRow) - 5 * skinRow}"
                            },
                            new CuiImageComponent { ItemId = itemID, SkinId = skinID.skinID }
                        }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Color = UI.Colors.Transparent.Clear, Command = $"{ConsoleCommands.UI.Command} {ConsoleCommands.UI.Commands.Select} {skinID.skinID}" },
                        Text = { Text = "" },
                        RectTransform = { AnchorMin = UI.Anchors.LowerLeft, AnchorMax = UI.Anchors.UpperRight },
                    }, UI.Overlay.Select.Grid.Skin + guid, UI.Overlay.Select.Grid.SkinButton + guid);


                    skinColumn++;

                    if (skinColumn == 3)
                    {
                        skinRow++;
                        skinColumn = 0;
                    }
                }

                if (currentSkin++ >= lastSkin)
                    break;
            }

            CuiHelper.AddUi(player, container);
        }

        private void BuildNavigationBar(BasePlayer player)
        {
            var page = playerUiData[player.userID].page;
            var itemName = playerUiData[player.userID].item;

            savedSkins datafile;
            if (!TryLoadData(itemName, out datafile))
                return;

            var totalSkins = datafile.skinInfo.Count + datafile.AdminSkinIds.Count;

            var totalPages = (int)Math.Ceiling(totalSkins / 12f);

            CuiHelper.DestroyUi(player, UI.Overlay.Select.Navigation.Panel);

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = UI.Colors.Transparent.Clear },
                RectTransform = { AnchorMin = UI.Anchors.LowerLeft, AnchorMax = UI.Anchors.LowerRight, OffsetMin = "0 0", OffsetMax = "0 20" }
            }, UI.Overlay.Select.Panel, UI.Overlay.Select.Navigation.Panel);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = UI.Anchors.LowerLeft, AnchorMax = UI.Anchors.UpperRight, OffsetMin = "75 0", OffsetMax = "-75 0" },
                Text = { Text = $"{page + 1}/{totalPages}", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = UI.Colors.White }
            }, UI.Overlay.Select.Navigation.Panel, UI.Overlay.Select.Navigation.PageNumber);

            if (totalPages > 1)
            {
                if (page > 0)
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = UI.Anchors.LowerLeft, AnchorMax = UI.Anchors.UpperLeft, OffsetMin = "0 0", OffsetMax = "75 0" },
                        Text = { Text = "<<<", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = UI.Colors.White },
                        Button = { Color = UI.Colors.DarkGray, Command = $"{ConsoleCommands.UI.Command} {ConsoleCommands.UI.Commands.DecrementPage}" }
                    }, UI.Overlay.Select.Navigation.Panel, UI.Overlay.Select.Navigation.LeftButton);

                if (page < totalPages - 1)
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = UI.Anchors.LowerRight, AnchorMax = UI.Anchors.UpperRight, OffsetMin = "-75 0", OffsetMax = "0 0" },
                        Text = { Text = ">>>", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = UI.Colors.White },
                        Button = { Color = UI.Colors.DarkGray, Command = $"{ConsoleCommands.UI.Command} {ConsoleCommands.UI.Commands.IncrementPage}" }
                    }, UI.Overlay.Select.Navigation.Panel, UI.Overlay.Select.Navigation.RightButton);
            }

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand(ConsoleCommands.UI.Command)]
        private void OnUiCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, Permissions.permUse)))
            {
                var command = arg.GetString(0);

                switch (command)
                {
                    case ConsoleCommands.UI.Commands.Close:
                        CloseDefaultUI(player, null);
                        CloseUI(player);
                        break;
                    case ConsoleCommands.UI.Commands.DecrementPage:
                        playerUiData[player.userID].page--;
                        BuildSkinGrid(player);
                        BuildNavigationBar(player);
                        break;
                    case ConsoleCommands.UI.Commands.IncrementPage:
                        playerUiData[player.userID].page++;
                        BuildSkinGrid(player);
                        BuildNavigationBar(player);
                        break;
                    case ConsoleCommands.UI.Commands.Select:
                        {
                            var skinId = arg.GetULong(1);
                            // Add functionality here to set the skin of the item
                            reskinEntity(player, skinId);
                            CloseUI(player);
                            break;
                        }
                }
            }
            else
            {
                player?.ChatMessage("You do NOT have permission to use this command!");
            }
        }
        #endregion

        #region SkinWorkshop stuff
        private Dictionary<string, string> disPlayNameToworkshopName = new Dictionary<string, string>
        {
            { "Longsleeve T-Shirt", "Long TShirt" },
            { "Baseball Cap", "cap" },
            { "Beenie Hat", "Beenie Hat" },
            { "Boonie Hat", "Boonie Hat" },
            { "Improvised Balaclava", "balaclava" },
            { "Waterpipe Shotgun", "Waterpipe Shotgun" },
            { "Rug Bear Skin", "Bearskin Rug" },
            { "Bolt Action Rifle", "Bolt Rifle" },
            { "Bandana Mask", "bandana" },
            { "Hide Vest", "Hide Shirt" },
            { "Snow Jacket", "Snow Jacket" },
            { "Bucket Helmet", "Bucket Helmet" },
            { "Semi-Automatic Pistol", "Semi-Automatic Pistol" },
            { "Road Sign Jacket", "Roadsign Vest" },
            { "Burlap Trousers", "Burlap Pants" },
            { "Shirt", "Collared Shirt" },
            { "MP5A4", "mp5" },
            { "Salvaged Sword", "sword" },
            { "Boots", "Work Boots" },
            { "Jacket", "Vagabond Jacket" },
            { "Hide Boots", "Hide Shoes" },
            { "Bone Helmet", "Deer Skull Mask" },
            { "Miners Hat", "Miner Hat" },
            { "LR-300 Assault Rifle", "lr300" },
            { "Leather Gloves", "Leather Gloves" },
            { "Python Revolver", "python" },
            { "M39 Rifle", "m39" },
            { "L96 Rifle", "l96" },
            { "Wood Double Door", "Wooden Double Door" },
            { "Large Wood Box", "Large Wood Box" },
            { "Armored Door", "Armored Door" },
            { "Armored Double Door", "Armored Double Door" },
            { "Chair", "Chair" },
            { "Concrete Barricade", "Concrete Barricade" },
            { "Fridge", "Fridge" },
            { "Furnace", "Furnace" },
            { "Garage Door", "Garage Door" },
            { "Locker", "Locker" },
            { "Sandbag Barricade", "Sandbag Barricade" },
            { "Sheet Metal Door", "Sheet Metal Door" },
            { "Sheet Metal Double Door", "Sheet Metal Double Door" },
            { "Sleeping Bag", "Sleeping Bag" },
            { "Vending Machine", "Vending Machine" },
            { "Wooden Double Door", "Wooden Double Door" },
            { "Wooden Door", "Wooden Door" },
            { "Wood Storage Box", "Wood Storage Box" },
            { "Assault Rifle", "AK47" },
            { "Rug", "Rug" },
            { "Hunting Bow", "Hunting Bow" },
            { "Burlap Gloves", "Leather Gloves" },
            { "Burlap Headwrap", "Burlap Headwrap" },
            { "Burlap Shirt", "Burlap Shirt" },
            { "Satchel Charge", "Satchel Charge" },
            { "Semi-Automatic Rifle", "Semi-Automatic Rifle" },
            { "Spinning wheel", "Spinning wheel" },
            { "Burlap Shoes", "Burlap Shoes" },
            { "Hide Pants", "Hide Pants" },
            { "Hide Poncho", "Hide Poncho" },
            { "Hide Skirt", "Hide Skirt" },
            { "Table", "Table" },
            { "Hammer", "Hammer" },
            { "Pickaxe", "Pick axe" },
            { "Hatchet", "Hatchet" },
            { "Pump Shotgun", "Pump Shotgun" },
            { "Custom SMG", "Custom SMG" },
            { "Metal Facemask", "Metal Facemask" },
            { "Metal Chest Plate", "Metal Chest Plate" },
            { "Pants", "Pants" },
            { "T-Shirt", "TShirt" },
            { "Road Sign Kilt", "Roadsign Pants" },
            { "Acoustic Guitar", "Acoustic Guitar" },
            { "Hoodie", "Hoodie" },
            { "Bone Knife", "Bone Knife" },
            { "M249", "M249" },
            { "Coffee Can Helmet", "Coffee Can Helmet" },
            { "Eoka Pistol", "Eoka Pistol" },
            { "Double Barrel Shotgun", "Double Barrel Shotgun" },
            { "Thompson", "Thompson" },
            { "Bone Club", "Bone Club" },
            { "Jackhammer", "Jackhammer" },
            { "Roadsign Gloves", "Roadsign Gloves" },
            { "Reactive Target", "Reactive Target" },
            { "Revolver", "Revolver" },
            { "Tank Top", "Tank Top" },
            { "Salvaged Hammer", "Salvaged Hammer" },
            { "Salvaged Icepick", "Salvaged Icepick" },
            { "Stone Hatchet", "Stone Hatchet" },
            { "Stone Pickaxe", "Stone Pickaxe" },
            { "Shorts", "Shorts" },
            { "Riot Helmet", "Riot Helmet" }
        };

        private bool paused = false;
        public int Page = 1;
        public int PerPage = 18;
        public int TotalResults;
        public int TotalPages => (int)Math.Ceiling(TotalResults / Convert.ToDouble(PerPage));
        public string Search = "";

        private IEnumerator LoadWorkshopImages()
        {
            _imageList = new List<KeyValuePair<string, ulong>>();
            PrintWarning("Validating all config item skins");
            foreach (var skinName in configData.settings.Wname.ToList())
            {
                while (paused)
                {
                    yield return new WaitForSeconds(0.2f);
                }
                if (!DataFileExists(skinName.Key))
                {
                    savedSkins theData = new savedSkins();
                    paused = true;
                    DownloadWorkshop(skinName.Value, theData, () => { finishDownload(skinName.Key, theData); }, "");
                }
                else
                {
                    savedSkins datafile = null;
                    if (TryLoadData(skinName.Key, out datafile))
                    {
                        if (datafile != null)
                        {
                            if (Instance.configData.settings.nonApproved && datafile.skinInfo.Count < Instance.configData.settings.TotalSkinsPerItem)
                            {
                                paused = true;
                                DownloadWorkshop(skinName.Value, datafile, () => { finishDownload(skinName.Key, datafile); }, "", datafile.skinInfo.Count);
                            }
                            else if (datafile.skinInfo.Count <= 0)
                            {
                                //  paused = true;
                                // DownloadWorkshop(skinName.Value, datafile, () => { finishDownload(skinName.Key, datafile); }, "", datafile.skinInfo.Count);
                            }

                        }
                    }
                }
            }

            if (!DataFileExists("Spray_Decals"))  //spray decal
            {
                savedSkins theData = new savedSkins();
                theData.skinInfo.Add(new savedSkinsInfo() { skinID = 0, title = "Default Skin" });
                theData.skinInfo.Add(new savedSkinsInfo() { skinID = 2828483382, title = "TOXIC Spray2" });
                SaveData("Spray_Decals", theData);
            }
            while (paused)
            {
                yield return new WaitForSeconds(0.2f);
            }

            ImagesReady();
            if (QueuedRoutineLoadImages != null)
                InvokeHandler.Instance.StopCoroutine(QueuedRoutineLoadImages);
        }

        #region Image Handling
        List<KeyValuePair<string, ulong>> _imageList = new List<KeyValuePair<string, ulong>>();

        private void ImagesReady()
        {
            _ImagesReady = true;
            paused = false;
            _imageList.Clear();
            PrintWarning("Skins loaded from data and ready.");
        }
        #endregion

        public string GetUrl(string name, int page = 0)
        {
            PerPage += 30;
            var url = "https://steamcommunity.com/workshop/browse/?appid=252490";
            url += "&requiredtags[]=" + (Uri.EscapeDataString(name));

            if (!string.IsNullOrEmpty(Search))
            {
                url += "&searchtext=" + (Uri.EscapeDataString(Search));
            }
            if (configData.settings.nonApproved && page > 3)
            {
                url += "&p=" + Convert.ToString(page) + "&numperpage=" + Convert.ToString(PerPage);
            }
            else
                url += "&childpublishedfileid=0&browsesort=accepted&section=mtxitems&created_date_range_filter_start=0&created_date_range_filter_end=0&updated_date_range_filter_start=0&updated_date_range_filter_end=0&browsefilter=accepted&p=" + Convert.ToString(Page) + "&numperpage=" + Convert.ToString(PerPage);


            return url;
        }

        private void finishDownload(string itemName, savedSkins theData)
        {
            if (theData.skinInfo.Count <= 0)
            {
                if (itemName == "Spinning wheel")
                    theData.skinInfo.Add(new savedSkinsInfo() { title = "Wheel of Fortune Spinning Wheel", skinID = 922866951 });
                else if (itemName == "Salvaged Hammer")
                    theData.skinInfo.Add(new savedSkinsInfo() { title = "Fire", skinID = 2596993628 });
            }
            PrintWarning($"Downloaded skinlist for {itemName} total skins {theData.skinInfo.Count}");
            SaveData(itemName, theData);
            _imageList = new List<KeyValuePair<string, ulong>>();

            paused = false;
            if (Instance.commandPlayer)
            {
                Instance.SendReply(Instance.commandPlayer, $"Skins download for {itemName}!");
                Instance.commandPlayer = null;
            }
        }

        private void DownloadWorkshop(string itemName, savedSkins theData, Action callback, string cacheKey = "", int totalHave = 0, int trys = 0)
        {
            webrequest.Enqueue(GetUrl(itemName, trys), "", (code, downloadString) =>
            {
                Match totalReg = Regex.Match(downloadString, @"of ([\d,]+) entries");
                if (!totalReg.Success) { callback(); return; }

                var total = Convert.ToInt32(totalReg.Groups[1].Value.Replace(",", ""));
                if (total <= 0) { callback(); return; }
                TotalResults = total;

                Regex regex = new Regex("id\":\"(.*?)\"description\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                var matches = regex.Matches(
                    downloadString);
                foreach (Match match in matches)
                {
                    bool shouldNotAdd = false;
                    if (totalHave >= configData.settings.TotalSkinsPerItem)
                        continue;

                    string newMatch = match.Value;
                    string id = "";
                    string titleNew = newMatch.Split(',')[0];
                    string newID = titleNew.Replace("id\":", "").Replace("\"", "");
                    string newTitle = newMatch.Replace("id\":", "").Replace(newID, "").Replace("\"description\"", "").Replace("\"", "").Replace("title:", "").Replace(",", "");
                    ulong skinIDConvert = Convert.ToUInt64(newID);

                    if (configData.settings.blackList.Contains(skinIDConvert) || knownBadIDS.Contains(skinIDConvert))
                        continue;

                    foreach (string BadWords in knownBadWords.ToList())
                    {
                        if (newTitle.Contains(BadWords, CompareOptions.IgnoreCase))
                        {
                            shouldNotAdd = true;
                            break;
                        }
                    }

                    foreach (savedSkinsInfo hasAlready in theData.skinInfo)
                    {
                        if (hasAlready.skinID == skinIDConvert)
                            shouldNotAdd = true;
                    }

                    if (!shouldNotAdd)
                    {
                        theData.skinInfo.Add(new savedSkinsInfo() { title = newTitle, skinID = skinIDConvert });
                        totalHave++;
                    }
                }
                if (trys <= 10 && totalHave < configData.settings.TotalSkinsPerItem)
                {
                    DownloadWorkshop(itemName, theData, callback, cacheKey, totalHave, trys += 1);
                    return;
                }

                callback();
            }, this);
        }
        #endregion
    }
}
