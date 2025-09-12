using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Facepunch.Math;
using UnityEngine.UI;

namespace Oxide.Plugins
{
    [Info("PlayerKits", "Ifte", "1.0.0")]
    [Description("Create kits for players.")]
    public class PlayerKits : RustPlugin
    {
        /*------------------------------------
         *
         *        PlayerKits by Ifte
         *     Support: https://discord.gg/cCWadjcapr
         *     Fiverr: https://www.fiverr.com/s/e2pkYY
         *         CODECCREATION
         *
         ------------------------------------*/

        #region Variables

        [PluginReference]
        Plugin ImageLibrary, ZoneManager;

        private Configuration config;
        private PlayerData playerData;
        private KitsUIData _data;

        private readonly string AdminPerm = "PlayerKits.admin";
        private readonly string AutoKitPerm = "PlayerKits.changeautokit";

        private static double CurrentTime => DateTime.UtcNow.Subtract(Epoch).TotalSeconds;
        private static DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        private static double LastWipeTime;

        private enum NotifyType
        {
            SUCCESS,
            ERROR
        }

        private Dictionary<string, string> Images = new Dictionary<string, string>();

        #endregion

        #region Config & Lang

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "Kit Chat Commands")]
            public string[] Commands = new[] { "kitmenu", "kitlist", "kits" };
            
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string Prefix = "<color=#FFD700>[PlayerKits]</color> »";
            [JsonProperty(PropertyName = "Chat Avator")]
            public ulong Avatar = 0;

            [JsonProperty(PropertyName = "Setting")]
            public SettingsKits Settings = new SettingsKits();

            [JsonProperty(PropertyName = "UI Settings")]
            public KitsUISetting UI = new KitsUISetting();

        }
        
        private class SettingsKits
        {
            [JsonProperty(PropertyName = "Wipe player data when the server is wiped")]
            public bool WipePlayerDataOnWipe = true;
            [JsonProperty(PropertyName = "Give spawn kits to players")]
            public bool SpawnKits = true;
            [JsonProperty(PropertyName = "Allow players to toggle spawn kits on spawn")]
            public bool AllowToggleSpawn = true;
        }

        private class KitsUISetting
        {
            [JsonProperty(PropertyName = "Background UI")]
            public HexColor bacground = new HexColor("#000000", 90);

            [JsonProperty(PropertyName = "Background Image Full")]
            public string bacgroundImgFull = "";

            [JsonProperty(PropertyName = "Topbar UI")]
            public TopBarUISetting topbar = new TopBarUISetting();

            [JsonProperty(PropertyName = "Category UI")]
            public CategoryUISetting category = new CategoryUISetting();

            [JsonProperty(PropertyName = "Kit List UI")]
            public KitListUISetting kitList = new KitListUISetting();

        }
        
        private class TopBarUISetting 
        {
            [JsonProperty(PropertyName = "Background Color")]
            public HexColor bacground = new HexColor("#000000", 0);

            [JsonProperty(PropertyName = "Background Image")]
            public string backgroundImage = "";

            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName = "PLAYER KITS";

            [JsonProperty(PropertyName = "Logo URL")]
            public string logourl = "https://rustlabs.com/img/skins/324/10197.png";

            [JsonProperty(PropertyName = "Bar Color")]
            public HexColor barColor = new HexColor("#BFBFAC", 100);
        }

        private class CategoryUISetting
        {
            [JsonProperty(PropertyName = "Background Color")]
            public HexColor bacground = new HexColor("#000000", 0);

            [JsonProperty(PropertyName = "Background Image")]
            public string backgroundImage = "";

            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName = "CATEGORY";

            [JsonProperty(PropertyName = "Category List Active Color")]
            public HexColor cateBoxColorActive = new HexColor("#FFC700", 60);

            [JsonProperty(PropertyName = "Category List Inactive Color")]
            public HexColor cateBoxColorInactive = new HexColor("#3C4B24", 60);
        }

        private class KitListUISetting
        {
            [JsonProperty(PropertyName = "Background Color")]
            public HexColor bacground = new HexColor("#000000", 0);

            [JsonProperty(PropertyName = "Background Image")]
            public string backgroundImage = "";

            [JsonProperty(PropertyName = "Kit Background Color")]
            public HexColor kitBack = new HexColor("#1B201B", 90);

            [JsonProperty(PropertyName = "Kit Description Color")]
            public HexColor kitDesc = new HexColor("#BFBFAC", 100);

            [JsonProperty(PropertyName = "Kit Bookmark Icon")]
            public string kitBookmarkIcon = "https://i.postimg.cc/VkyrF9vf/star.png";

            [JsonProperty(PropertyName = "Kit Description Font")]
            public string kitDescFont = "robotocondensed-bold.ttf";

            [JsonProperty(PropertyName = "Kit Description Font Size")]
            public int kitDescSize = 12;

            [JsonProperty(PropertyName = "Kit Infos Name Color")]
            public HexColor kitInfo = new HexColor("#9CD293", 100);

            [JsonProperty(PropertyName = "Kit Infos Value Color")]
            public HexColor kitInfoValue = new HexColor("#E7E277", 100);

            [JsonProperty(PropertyName = "Kit Icons Color")]
            public HexColor kitIconsColor = new HexColor("#9CD293", 100);

            [JsonProperty(PropertyName = "Kit Limit Icon")]
            public string kitLimitIcon = "https://i.postimg.cc/Rhsdz9t2/warning.png";

            [JsonProperty(PropertyName = "Kit Cooldown Icon")]
            public string kitCoolIcon = "https://i.postimg.cc/hj0MpVtD/timer-1.png";

            [JsonProperty(PropertyName = "Kit WipeCooldown Icon")]
            public string kitWipeCoolIcon = "https://i.postimg.cc/0j2n3rzp/time.png";

        }

        #endregion

        #region Init

        private void OnServerInitialized()
        {
            permission.RegisterPermission(AdminPerm, this);
            permission.RegisterPermission(AutoKitPerm, this);            
            LoadData();
            LoadImages();
            CheckKits();
            RegisterAllKitsPerms();
            RegisterKitCommands(cmd, this);
            LoadPlayerData();
            LastWipeTime = SaveRestore.SaveCreatedTime.Subtract(Epoch).TotalSeconds;
        }

        private void Unload()
        {
            SaveData();
            SavePlayerData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "PLAYER_KITS");
            }           
        }

        private void OnNewSave(string FileName)
        {
            if (config.Settings.WipePlayerDataOnWipe)
            {
                playerData.Wipe();
                SavePlayerData();
                Puts($"New save detected, wiping PlayerData.");
            }
        }

        private void OnServerSave()
        {
            SaveData();
            SavePlayerData();
        }

        #endregion

        #region Commands
        private void KitMenuCommand(BasePlayer player, string cmd, string[] args)
        {
            if (player == null) return;
            if (args.Length == 0)
            {
                KitsShow_UI(player);
                return;
            }

            string command = args[0].ToLower();
            string identifier = string.Join(" ", args.Skip(1)); // Extract the kit identifier (ID or Name)

            switch (command)
            {
                case "remove":
                case "delete":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    DeleteKit(player, identifier);
                    return;
                case "list":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    //show list
                    ShowKits(player);
                    return;
                case "edit":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    if (args.Length != 2)
                    {
                        CM(player, "You must specify a kit name.");
                        return;
                    }
                    Kits editKit = FindKit(identifier);
                    if (editKit != null)
                    {
                        KitsShow_UI(player);
                        CreateOrEditKit(player, editKit);
                    }
                    return;
                case "give":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    if (args.Length != 3)
                    {
                        CM(player, "You must specify target player and a kit name.");
                        return;
                    }
                    BasePlayer target = FindPlayer(args[1]);
                    if (target == null)
                    {
                        CM(player, "Failed to find a player with the specified name or ID.");
                        return;
                    }
                    string FullName = string.Join("", args.Skip(2));
                    Kits giveKit = FindKit(FullName);

                    if (giveKit == null)
                    {
                        CM(player, "The kit <color=#ce422b>{0}</color> does not exist.".Replace("{0}", giveKit.Name));
                        return;
                    }
                    GiveKit(target, giveKit);
                    CM(player, "You have given <color=#ce422b>{0}</color> the kit <color=#ce422b>{1}</color>.".Replace("{0}", target.displayName).Replace("{1}", giveKit.Name));
                    return;
                case "reset":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    playerData.Wipe();
                    SavePlayerData();
                    CM(player, "You have wiped player usage data.");
                    return;
                case "help":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    ShowHelpMessageAdmin(player);
                    return;
                case "create":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    if (args.Length != 2)
                    {
                        CM(player, "You must specify a kit name.");
                        return;
                    }
                    string kitName = identifier;

                    int randomNumber = GetRandom();
                    Kits createKit = new Kits
                    {
                        ID = randomNumber,
                        Name = kitName,
                        Description = "This is a kit description.\nPlease use as you fit.",
                        Cooldown = 0,
                        WipeCooldown = 0,
                        AutoKit = false,
                        AutoKitWeight = 0,
                        UsesLimit = 0,
                        Permission = string.Empty,
                    };
                    _data.KitsUI_Data.Add(randomNumber, createKit);
                    SaveData();

                    if (createKit != null)
                    {
                        KitsShow_UI(player);
                        CreateOrEditKit(player, createKit, true);
                    }

                    return;
                case "autokit":
                    if (!permission.UserHasPermission(player.UserIDString, AutoKitPerm))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    if (!config.Settings.AllowToggleSpawn)
                    {
                        CM(player, "You are not allowed to change Auto/Spawn Kits.");
                        return;
                    }
                    playerData[player.userID].ClaimAutoKits = !playerData[player.userID].ClaimAutoKits;
                    SavePlayerData();
                    if (playerData[player.userID].ClaimAutoKits)
                    {
                        CM(player, $"You have enabled your Auto/Spawn kits.");
                    }
                    else
                    {
                        CM(player, $"You have disabled your Auto/Spawn kits.");
                    }                   
                    return;
                case "convert":
                    //
                    return;

            }
        }

        private void ConsoleKitCommand(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();

            string command = args.Args[0].ToLower();
            string identifier = string.Join(" ", args.Args.Skip(1)); // Extract the kit identifier (ID or Name)

            switch (command)
            {
                case "give":
                    if (args.Args.Length != 3)
                    {
                        Puts("You must specify target player and a kit name.");
                        return;
                    }
                    BasePlayer target = FindPlayer(args.Args[1]);
                    if (target == null)
                    {
                        Puts("Failed to find a player with the specified name or ID.");
                        return;
                    }
                    string FullName = string.Join("", args.Args.Skip(2));
                    Kits giveKit = FindKit(FullName);

                    if (giveKit == null)
                    {
                        Puts("The kit {0} does not exist.".Replace("{0}", giveKit.Name));
                        return;
                    }
                    GiveItemsTo(target, giveKit);
                    Puts("You have given {0}the kit {1}.".Replace("{0}", target.displayName).Replace("{1}", giveKit.Name));
                    return;
                case "list":
                    ShowKits();                    
                    return;
                case "remove":
                case "delete":
                    DeleteKit(player, identifier);
                    return;
                case "reset":
                    playerData.Wipe();
                    SavePlayerData();
                    Puts("You have wiped player usage data.");
                    return;

            }

        }

        private void CMDKitsUI(IPlayer p, string cmd, string[] args)
        {
            var player = p.Object as BasePlayer;
            if (player == null) return;

            string command = args[0].ToLower();
            string identifier = string.Join(" ", args.Skip(1)); // Extract the kit identifier (ID or Name)

            switch (command)
            {
                case "remove":
                case "delete":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    DeleteKit(player, identifier);                   
                    return;
                case "list":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    //show list
                    return;
                case "edit":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    Kits editKit = FindKit(identifier);
                    if (editKit != null)
                    {
                        CreateOrEditKit(player, editKit);
                    }
                    return;
                case "give":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    if (args.Length != 3)
                    {
                        CM(player, "You must specify target player and a kit name.");
                        return;
                    }
                    BasePlayer target = FindPlayer(args[0]);
                    if (target == null)
                    {
                        CM(player, "Failed to find a player with the specified name or ID.");
                        return;
                    }
                    string FullName = string.Join("", args.Skip(2));
                    Kits giveKit = FindKit(FullName);

                    if (giveKit == null)
                    {
                        CM(player, "The kit <color=#ce422b>{0}</color> does not exist.".Replace("{0}", giveKit.Name));
                        return;
                    }
                    GiveKit(target, giveKit);
                    CM(player, "You have given <color=#ce422b>{0}</color> the kit <color=#ce422b>{1}</color>.".Replace("{0}", target.displayName).Replace("{1}", giveKit.Name));
                    return;
                case "reset":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    playerData.Wipe();
                    SavePlayerData();
                    CM(player, "You have wiped player usage data.");
                    return;
                case "help":
                    if (!IsAdmin(player))
                    {
                        CM(player, "You don't have permission to use this command.");
                        return;
                    }
                    //message
                    return;
            }
        }

        [ConsoleCommand("pkitcon")]
        private void NewKitConsole(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            var command = args.Args[0];
            switch (command)
            {
                case "close":
                    CuiHelper.DestroyUi(player, "PLAYER_KITS");
                    return;
                case "closepreview":
                    CuiHelper.DestroyUi(player, "KitPreviews");
                    return;
                case "closeediting":
                    CuiHelper.DestroyUi(player, "CreatingKit");
                    return;
                case "select":
                    Kits kit = FindKitByID(args.GetInt(1));
                    //KitsShow_UI(player, kit);
                    KitPreviews(player, kit);
                    return;
                case "page":
                    int page = args.GetInt(1);
                    string category = args.GetString(2);
                    KitsShow_UI(player, page, category);
                    return;
                case "editkit":
                    Kits kitedit = FindKitByID(args.GetInt(1));
                    CuiHelper.DestroyUi(player, "KitPreviews");
                    CreateOrEditKit(player, kitedit);
                    return;
                case "createkit":
                    int randomNumber = GetRandom();
                    Kits createKit = new Kits
                    {
                        ID = randomNumber,
                        Name = "Kit Name",
                        Description = "This is a kit description.Please use as you fit. Get this kit from www.kitstore.com/kits. More kits available Soon",
                        Cooldown = 0,
                        WipeCooldown = 0,
                        AutoKit = false,
                        AutoKitWeight = 0,
                        UsesLimit = 0,
                        Permission = string.Empty,
                        EnableKit = true,
                        Color = "#FFFF61",
                    };
                    _data.KitsUI_Data.Add(randomNumber, createKit);
                    createKit.WearItems = GetKitItems(player.inventory.containerWear);
                    createKit.BeltItems = GetKitItems(player.inventory.containerBelt);
                    createKit.MainItems = GetKitItems(player.inventory.containerMain);
                    SaveData();
                    CreateOrEditKit(player, createKit, true);
                    return;
                case "delete":
                    Kits DelKit = FindKitByID(args.GetInt(1));
                    RemoveKitByID(DelKit.ID);
                    SaveData();
                    KitsShow_UI(player);
                    CM(player, $"<color=#FFD700>{DelKit.Name}</color> deleted from kits.");
                    return;
                case "savekit":
                    Kits SaveKit = FindKitByID(args.GetInt(1));
                    if (!HasKitItems(SaveKit))
                    {
                        //ShowNotify(player, "Please save at least 1 item for your Kit to be saved.");
                        NotifyMessage(player, NotifyType.ERROR, "Please save at least 1 item for your kit to be saved.");
                        PlaySound(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                        return;
                    }
                    KitsShow_UI(player);
                    return;
                case "copyinv":
                    Kits CopyInvKit = FindKitByID(args.GetInt(1));
                    CopyInvKit.WearItems = GetKitItems(player.inventory.containerWear);
                    CopyInvKit.BeltItems = GetKitItems(player.inventory.containerBelt);
                    CopyInvKit.MainItems = GetKitItems(player.inventory.containerMain);
                    SaveData();
                    //ShowNotify(player, "You have copied your inventory to this kit.");
                    NotifyMessage(player, NotifyType.SUCCESS, $"You have copied your inventory to <color=#FFD700>{CopyInvKit.Name}</color>");
                    PlaySound(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                    return;
                case "changeName":
                    Kits nameKit = FindKitByID(args.GetInt(1));
                    nameKit.Name = string.Join(" ", args.Args.Skip(2));
                    SaveData();
                    CreateOrEditKit(player, nameKit);
                    return;
                case "changeDes":
                    Kits desKit = FindKitByID(args.GetInt(1));
                    desKit.Description = string.Join(" ", args.Args.Skip(2));
                    SaveData();
                    CreateOrEditKit(player, desKit);
                    return;
                case "changePerm":
                    Kits permKit = FindKitByID(args.GetInt(1));
                    permKit.Permission = args.GetString(2);
                    RegisterKitPerm(args.GetString(2));
                    SaveData();
                    CreateOrEditKit(player, permKit);
                    return;
                case "changeColor":
                    Kits col = FindKitByID(args.GetInt(1));
                    col.Color = args.GetString(2);
                    SaveData();
                    CreateOrEditKit(player, col);
                    return;
                case "changeCategory":
                    Kits chType = FindKitByID(args.GetInt(1));
                    chType.Category = string.Join(" ", args.Args.Skip(2));
                    SaveData();
                    CreateOrEditKit(player, chType);
                    return;
                case "changeCool":
                    Kits coolKit = FindKitByID(args.GetInt(1));
                    coolKit.Cooldown = args.GetInt(2);
                    SaveData();
                    CreateOrEditKit(player, coolKit);
                    return;
                case "changeUses":
                    Kits usesKit = FindKitByID(args.GetInt(1));
                    usesKit.UsesLimit = args.GetInt(2);
                    SaveData();
                    CreateOrEditKit(player, usesKit);
                    return;
                case "changeWipe":
                    Kits wipeKit = FindKitByID(args.GetInt(1));
                    wipeKit.WipeCooldown = args.GetInt(2);
                    SaveData();
                    CreateOrEditKit(player, wipeKit);
                    return;
                case "changeIMG":
                    Kits imgKit = FindKitByID(args.GetInt(1));
                    //imgKit.ImageURL = args.GetString(2);
                    SaveData();
                    //AddImage(imgKit.ImageURL);
                    LoadImages();
                    CreateOrEditKit(player, imgKit);
                    return;
                case "changeShowKit":
                    Kits showKitChange = FindKitByID(args.GetInt(1));
                    //showKitChange.ShowWithoutPerm = !showKitChange.ShowWithoutPerm;
                    SaveData();
                    CreateOrEditKit(player, showKitChange);
                    return;
                case "changeAuto":
                    Kits autoKit = FindKitByID(args.GetInt(1));
                    autoKit.AutoKit = !autoKit.AutoKit;
                    SaveData();
                    CreateOrEditKit(player, autoKit);
                    return;
                case "changeKitEnable": //$"pkitcon changeKitEnable {kit.ID}"
                    Kits colorBarKit = FindKitByID(args.GetInt(1));
                    colorBarKit.EnableKit = !colorBarKit.EnableKit;
                    SaveData();
                    CreateOrEditKit(player, colorBarKit);
                    return;
                case "changeTypeColor":
                    Kits typeColorKit = FindKitByID(args.GetInt(1));
                    //typeColorKit.TypeColor = args.GetString(2);
                    SaveData();
                    CreateOrEditKit(player, typeColorKit);
                    return;
                case "changePri":
                    Kits priKit = FindKitByID(args.GetInt(1));
                    int num = args.GetInt(2);
                    if (num > 100)
                    {
                        num = 100;
                    }
                    if (num <= 0)
                    {
                        num = 1;
                    }
                    priKit.AutoKitWeight = num;
                    SaveData();
                    CreateOrEditKit(player, priKit);
                    return;
                case "redeemkit":
                    Kits rKit = FindKitByID(args.GetInt(1));
                    GiveKit(player, rKit);
                    return;
                case "cautoadmin":
                    playerData[player.userID].ShowAutoKitList = !playerData[player.userID].ShowAutoKitList;
                    SavePlayerData();
                    KitsShow_UI(player);
                    return;
                case "selectCate":
                    string cate = args.GetString(1).Replace("_", " ");
                    KitsShow_UI(player, 1, cate);
                    return;
                case "bookmark":
                    Kits bmKit = FindKitByID(args.GetInt(1));
                    if (playerData.Find(player.userID, out PlayerData.PlayerUsageData playerUsageData))
                    {
                        if (playerUsageData.FavKits.Contains(bmKit.ID))
                        {
                            playerUsageData.FavKits.Remove(bmKit.ID);
                            KitsShow_UI(player);
                        }
                        else
                        {
                            playerUsageData.FavKits.Add(bmKit.ID);
                            KitsShow_UI(player);
                        }
                    }
                    return;
            }
        }

        #endregion

        #region Hooks

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !config.Settings.SpawnKits || !playerData[player.userID].ClaimAutoKits)
                return;
            List<Kits> AutoKits = _data.KitsUI_Data.Values.Where(kit => kit.AutoKit && (string.IsNullOrEmpty(kit.Permission) || permission.UserHasPermission(player.UserIDString, kit.Permission))).ToList();

            float highestWeight = 0.0f;
            Kits highestWeightKit = null;

            foreach (Kits kit in AutoKits)
            {
                // Check if the weight of the current kit is higher than the highest weight
                if (kit.AutoKitWeight > highestWeight)
                {
                    highestWeight = kit.AutoKitWeight;
                    highestWeightKit = kit;
                }
            }

            if (highestWeightKit != null)
            {
                player.inventory.Strip();
                GiveItemsTo(player, highestWeightKit);
            }
        }

        #endregion

        #region Methods

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds(time);
            int days = dateDifference.Days;
            int hours = dateDifference.Hours;
            int mins = dateDifference.Minutes;
            int secs = dateDifference.Seconds;

            if (days > 0)
                return $"{days:00}d:{hours:00}h";
            if (hours > 0)
                return $"{hours:00}h:{mins:00}m";
            return mins > 0 ? $"{mins:00}m:{secs:00}s" : $"{secs}s";
        }

        private int GetRandom()
        {
            System.Random random = new System.Random();
            int randomNumber;
            do
            {
                randomNumber = random.Next(1000, 99999999);
            } while (GetKitIDS().Contains(randomNumber));

            return randomNumber;
        }
        
        private HashSet<int> GetKitIDS()
        {
            HashSet<int> kitIDS = new HashSet<int>();
            foreach(var id in _data.KitsUI_Data.Values)
            {
                if (id != null)
                {
                    kitIDS.Add(id.ID);
                }
            }
            return kitIDS;
        }

        private bool HasBookMarkKit(BasePlayer player, int ID)
        {
            if (playerData.Find(player.userID, out PlayerData.PlayerUsageData playerUsageData))
            {
                if (playerUsageData.FavKits.Count > 0)
                {
                    foreach (int num in playerUsageData.FavKits)
                    {
                        if (num == ID) 
                            return true;
                    }
                }
            }
            return false;
        }

        private HashSet<string> GetCategorys()
        {
            HashSet<string> categories = new HashSet<string>();

            categories.Add("ALL KITS");
            categories.Add("FAVORITES");

            foreach (var kit in _data.KitsUI_Data.Values)
            {
                if (kit.Category != null && !string.IsNullOrEmpty(kit.Category))
                {
                    categories.Add(kit.Category);
                }
            }

            return categories;
        }

        private void ShowHelpMessageAdmin(BasePlayer player)
        {
            string message = "<color=#FFD700>PlayerKits Help</color>\n» /kits - Shows Kits UI\n» /kits <color=#FFD700>list</color> - Shows Kits list\n» /kits <color=#FFD700>create</color> <Name> - Shows Kits create UI\n» /kits <color=#FFD700>edit</color> <Name/ID>< - Shows Kits edit UI\n» /kits <color=#FFD700>give</color> <PlayerName/SteamID> <Name/ID> - Give kit to specific player\n» /kits <color=#FFD700>reset</color> - Reset all player data";
            Player.Message(player, message, config.Avatar);
        }
        private void CM(BasePlayer player, string message)
        {
            if (player == null) return;
            if (message == null) return;
            Player.Message(player, config.Prefix + " " + message, config.Avatar);
        }

        private bool IsAdmin(BasePlayer player) => permission.UserHasPermission(player.UserIDString, AdminPerm);

        private BasePlayer FindPlayer(string partialNameOrID) => BasePlayer.allPlayerList.FirstOrDefault<BasePlayer>((BasePlayer x) => x.UserIDString.Equals(partialNameOrID) ||
                                                                                                    x.displayName.Equals(partialNameOrID, StringComparison.OrdinalIgnoreCase) ||
                                                                                                    x.displayName.Contains(partialNameOrID, CompareOptions.OrdinalIgnoreCase));

        private void PlaySound(BasePlayer player, string path) => Effect.server.Run(path, player, 2, Vector3.zero, new Vector3(0f, 2f, 0f));        

        private void AddImage(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!ImageLibrary.Call<bool>("HasImage", url)) ImageLibrary.Call("AddImage", url, url);
            timer.In(1f, () => 
            { 
                if (Images.ContainsKey(url)) Images.Remove(url);
                Images.Add(url, ImageLibrary.Call<string>("GetImage", url));
            });
        }

        private void LoadImages()
        {
            if (ImageLibrary == null)
            {
                PrintError("[ImageLibrary] not found!");
                return;
            }

            //
            AddImage("https://i.postimg.cc/Rhh5bMFt/check.png");
            AddImage("https://i.postimg.cc/j5sgvcZG/Error-2.png");
            ///
            AddImage(config.UI.topbar.backgroundImage);
            AddImage(config.UI.topbar.logourl);
            AddImage(config.UI.kitList.backgroundImage);
            AddImage(config.UI.kitList.kitLimitIcon);
            AddImage(config.UI.kitList.kitCoolIcon);
            AddImage(config.UI.kitList.kitWipeCoolIcon);
            AddImage(config.UI.kitList.kitBookmarkIcon);

            foreach (var kit in _data.KitsUI_Data.Values)
            {
                var allItems = kit.MainItems.Concat(kit.WearItems).Concat(kit.BeltItems);
                foreach (var item in allItems)
                {
                    if (!string.IsNullOrEmpty(item.ImageURL))
                    {
                        AddImage(item.ImageURL);
                    }
                }                
            }           
        }

        private string GetImage(string url)
        {
            return Images[url];
        }

        private class HexColorCopy
        {
            [JsonProperty(PropertyName = "Hexa Color")] public string Hex;

            [JsonProperty(PropertyName = "Opacity(0 - 100)")]
            public float Alpha;

            public string Color()
            {
                if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

                var str = Hex.Trim('#');
                if (str.Length != 6) throw new Exception(Hex);
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
            }

            public HexColorCopy(string hex, float alpha)
            {
                Hex = hex;
                Alpha = alpha;
            }
        }
        private string HexConvert(string Color, float Alpha)
        {
            var str = Color.Trim('#');
            if (str.Length != 6)
                throw new Exception("Invalid hexadecimal color");

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }
        private class HexColor
        {
            [JsonProperty(PropertyName = "Hexa Color(#FFFFFF,100)")]
            public string Hex;

            public string Color()
            {
                if (string.IsNullOrEmpty(Hex))
                    Hex = "#FFFFFF,100"; // Set default value if not provided

                var parts = Hex.Split(',');
                if (parts.Length != 2)
                    throw new Exception("Invalid format for Hexa Color");

                var str = parts[0].Trim('#');
                if (str.Length != 6)
                    throw new Exception("Invalid hexadecimal color");

                if (!float.TryParse(parts[1], out var alpha))
                    throw new Exception("Invalid alpha value");

                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {alpha / 100}";
            }

            public HexColor(string hex, float alpha)
            {
                Hex = $"{hex},{alpha}";
            }
        }

        private void RegisterKitCommands(Game.Rust.Libraries.Command cmd, Plugin plugin)
        {
            for(int i = 0; i < config.Commands.Length; i++)
            {
                cmd.AddChatCommand(config.Commands[i], plugin, "KitMenuCommand");
                cmd.AddConsoleCommand(config.Commands[i], plugin, "ConsoleKitCommand");
            }
        }


        #endregion

        #region UIs

        private void NotifyMessage(BasePlayer player, NotifyType type, string Message)
        {
            var container = new CuiElementContainer();

            if (type == NotifyType.SUCCESS)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.3568628 0.4392157 0.2196078 1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "371.86 184.22", OffsetMax = "631.86 244.22" }
                }, "Overlay", "NotifyUI");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0 0 0 0.4" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-130 -30", OffsetMax = "-70 30" }
                }, "NotifyUI", "Panel_4656");

                container.Add(new CuiElement
                {
                    Name = "Image_464",
                    Parent = "NotifyUI",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("https://i.postimg.cc/Rhh5bMFt/check.png") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-116.4 -14", OffsetMax = "-88.4 14" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Title",
                    Parent = "NotifyUI",
                    Components = {
                    new CuiTextComponent { Text = "SUCCESS!", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.3 0", OffsetMax = "113.7 20.668" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Desc",
                    Parent = "NotifyUI",
                    Components = {
                    new CuiTextComponent { Text = Message, Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.3 -28", OffsetMax = "113.7 0" }
                }
                });

                CuiHelper.DestroyUi(player, "NotifyUI");
                CuiHelper.AddUi(player, container);
            }
            else if (type == NotifyType.ERROR)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.8039216 0.254902 0.1686275 1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "371.86 184.22", OffsetMax = "631.86 244.22" }
                }, "Overlay", "NotifyUI");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0 0 0 0.4" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-130 -30", OffsetMax = "-70 30" }
                }, "NotifyUI", "Panel_4656");

                container.Add(new CuiElement
                {
                    Name = "Image_464",
                    Parent = "NotifyUI",
                    Components = {
                    new CuiRawImageComponent { Color = "0.7686275 1 0.3803922 1", Png = GetImage("https://i.postimg.cc/j5sgvcZG/Error-2.png") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-116.4 -14", OffsetMax = "-88.4 14" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Title",
                    Parent = "NotifyUI",
                    Components = {
                    new CuiTextComponent { Text = "ERROR!", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.3 0", OffsetMax = "113.7 20.668" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Desc",
                    Parent = "NotifyUI",
                    Components = {
                    new CuiTextComponent { Text = Message, Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.3 -28", OffsetMax = "113.7 0" }
                }
                });

                CuiHelper.DestroyUi(player, "NotifyUI");
                CuiHelper.AddUi(player, container);
            }
            

            timer.In(5, () =>
            {
                CuiHelper.DestroyUi(player, "NotifyUI");
            });
        }

        private void ShowNotify(BasePlayer player, string Message)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "371.858 190.925", OffsetMax = "604.342 257.875" }
            }, "Overlay", "Notify");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1411765 0.1372549 0.1372549 1", Sprite = "assets/content/ui/ui.background.transparent.linearltr.tga" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-116.241 -33.984", OffsetMax = "116.239 33.984" }
            }, "Notify", "Gradient");

            container.Add(new CuiElement
            {
                Name = "Image_4415",
                Parent = "Notify",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Sprite = "assets/icons/info.png" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-107.3 -12.5", OffsetMax = "-82.3 12.5" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Type",
                Parent = "Notify",
                Components = {
                    new CuiTextComponent { Text = "WARNING!", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 0.3411765 0.3411765 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-78.737 5.061", OffsetMax = "52.937 28.339" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Msg",
                Parent = "Notify",
                Components = {
                    new CuiTextComponent { Text = Message, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-78.738 -37.786", OffsetMax = "112.476 0" }
                }
            });

            CuiHelper.DestroyUi(player, "Notify");
            CuiHelper.AddUi(player, container);

            timer.In(5, () =>
            {
                CuiHelper.DestroyUi(player, "Notify");
            });
        }

        private string CheckRedeemable(BasePlayer player, Kits kit)
        {
            string ClaimBTN = "REDEEM";

            if (!string.IsNullOrEmpty(kit.Permission) && !permission.UserHasPermission(player.UserIDString, kit.Permission))
            {
                return ClaimBTN = "LOCKED";
            }
            else if (kit.WipeCooldown > 0 && IsOnWipeCooldown(kit))
            {
                return ClaimBTN = "WAIT " + FormatTime(TimeLeftBeforeKitCanBeUsed(kit));
            }
            else if (playerData.Find(player.userID, out PlayerData.PlayerUsageData playerUsageData))
            {
                if (kit.Cooldown > 0)
                {
                    double cooldownRem = playerUsageData.GetCooldownRemaining(kit.ID);
                    if (cooldownRem > 0)
                    {                       
                        return ClaimBTN = "Wait " + FormatTime(playerData[player.userID].GetCooldownRemaining(kit.ID));
                    }
                }

                if (kit.UsesLimit > 0)
                {
                    int currentUses = playerUsageData.GetKitUses(kit.ID);
                    if (currentUses >= kit.UsesLimit)
                    {                       
                        return ClaimBTN = "Limit Reached";
                    }
                }
            }

            return ClaimBTN;
        }

        private void KitsShow_UI(BasePlayer player, int currentPage = 1, string Category = "")
        {
            var container = new CuiElementContainer();

            if (string.IsNullOrEmpty(config.UI.bacgroundImgFull))
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = config.UI.bacground.Color(), Material = "assets/content/ui/uibackgroundblur.mat" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640.002 -360.001", OffsetMax = "639.998 360.009" }
                }, "Overlay", "PLAYER_KITS");
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "PLAYER_KITS",
                    Parent = "Overlay",                  
                    Components = {
                    new CuiNeedsCursorComponent(),
                    new CuiRawImageComponent { Color = config.UI.bacground.Color(), Png = GetImage(config.UI.bacgroundImgFull) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640.002 -360.001", OffsetMax = "639.998 360.009" }
                }
                });
            }

            #region TOP BAR

            if (string.IsNullOrEmpty(config.UI.topbar.backgroundImage))
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = config.UI.topbar.bacground.Color() },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-639.998 287.25", OffsetMax = "640.002 360.01" }
                }, "PLAYER_KITS", "TOPBAR");
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "TOPBAR",
                    Parent = "PLAYER_KITS",
                    Components = {
                    new CuiRawImageComponent { Color = config.UI.topbar.bacground.Color(), Png = GetImage(config.UI.topbar.backgroundImage) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-639.998 287.25", OffsetMax = "640.002 360.01" }
                }
                });
            }

            container.Add(new CuiElement
            {
                Name = "Label_5923",
                Parent = "TOPBAR",
                Components = {
                    new CuiTextComponent { Text = config.UI.topbar.DisplayName, Font = "robotocondensed-bold.ttf", FontSize = 30, Align = TextAnchor.MiddleLeft, Color = "1 0.7803922 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-565.349 -28.194", OffsetMax = "-320.491 34.768" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5960785 0.1764706 0.1294118 1", Command = "pkitcon close" },
                Text = { Text = "✕", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.8235294 0.5882353 0.5764706 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "605.165 3.733", OffsetMax = "631.835 30.403" }
            }, "TOPBAR", "CloseBTN");

            if (IsAdmin(player))
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.2352941 0.2941177 0.1411765 1", Command = $"pkitcon createkit" },
                    Text = { Text = "CREATE", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 0.3803922 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "493.68 3.733", OffsetMax = "593.335 30.403" }
                }, "TOPBAR", "CreateBTN");
            }

            container.Add(new CuiPanel
            {
                Image = { Color = config.UI.topbar.barColor.Color() },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-569.717 -29.365", OffsetMax = "639.983 -28.195" }
            }, "TOPBAR", "Bar");

            container.Add(new CuiElement
            {
                Name = "Image_1302",
                Parent = "TOPBAR",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(config.UI.topbar.logourl) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-633.866 -29.365", OffsetMax = "-569.734 34.768" }
                }
            });

            #endregion

            #region Categorys

            if (string.IsNullOrEmpty(config.UI.category.backgroundImage))
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = config.UI.category.bacground.Color() },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360.003", OffsetMax = "-418.88 287.247" }
                }, "PLAYER_KITS", "CatagoryPanel");
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "CatagoryPanel",
                    Parent = "PLAYER_KITS",
                    Components = {
                        new CuiRawImageComponent { Color = config.UI.category.bacground.Color(), Png = GetImage(config.UI.category.backgroundImage) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360.003", OffsetMax = "-418.88 287.247" }
                    }
                });
            }
            

            container.Add(new CuiElement
            {
                Name = "CategoryNames",
                Parent = "CatagoryPanel",
                Components = {
                    new CuiTextComponent { Text = config.UI.category.DisplayName, Font = "robotocondensed-bold.ttf", FontSize = 30, Align = TextAnchor.MiddleLeft, Color = "0.7450981 0.7529412 0.7607843 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-101.215 273.76", OffsetMax = "110.595 320.717" },
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-101.215 -304.242", OffsetMax = "110.59 268.468" },
            }, "CatagoryPanel", "Category_List");

            // Add the scroll viewer to the container
            container.Add(new CuiElement
            {
                Parent = "Category_List",
                Name = "ScrollView",
                Components = { 
                    new CuiImageComponent() { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiScrollViewComponent
                    {
                        Vertical = true, 
                        MovementType = ScrollRect.MovementType.Elastic,
                        Elasticity = 0.3f,
                        Inertia= true,
                        DecelerationRate = 0.25f,
                        ScrollSensitivity = 0.25f,
                        ContentTransform = new CuiRectTransformComponent {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "0 -1000", // Adjust based on content height
                            OffsetMax = "0 0"
                        },
                        VerticalScrollbar = new CuiScrollbar 
                        {
                            Invert = false,
                            Size = 10f,
                            TrackColor = "0.8 0.8 0.8 0",
                            HandleColor = "0.2 0.2 0.2 0",
                            HighlightColor = "0.5 0.5 0.5 0"
                        }
                    }
                }
            });

            HashSet<string> categories = GetCategorys();
            
            int index = 0;

            foreach(var category in categories)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = category != Category ? config.UI.category.cateBoxColorInactive.Color(): config.UI.category.cateBoxColorActive.Color(), Command = $"pkitcon selectCate {category.Replace(" ", "_")}", Material = "assets/icons/iconmaterial.mat" },
                    Text = { Text = category, Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0.7450981 0.7529412 0.7607843 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"0 {-31.932 - (index * 33.933)}", OffsetMax = $"190 {0 - (index * 33.933)}" }
                }, "ScrollView", $"Cata{index}");

                index++;
            }

            #endregion

            #region Kits List

            if (string.IsNullOrEmpty(config.UI.kitList.backgroundImage))
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = config.UI.kitList.bacground.Color() }, //"0 0 0 0.6078432"
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-418.87 -360.003", OffsetMax = "640.03 287.247" }
                }, "PLAYER_KITS", "KITS_LIST");
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "KITS_LIST",
                    Parent = "PLAYER_KITS",
                    Components = {
                        new CuiRawImageComponent { Color = config.UI.kitList.bacground.Color(), Png = GetImage(config.UI.kitList.backgroundImage) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-418.87 -360.003", OffsetMax = "640.03 287.247" }
                    }
                });
            }

            //Kits 
            List<Kits> KitList = new List<Kits>();

            if (string.IsNullOrEmpty(Category) || Category == "ALL KITS")
            {
                if (IsAdmin(player))
                {
                    KitList = _data.KitsUI_Data.Values.ToList();
                }
                else
                {
                    KitList = _data.KitsUI_Data.Values.Where(x => x.EnableKit && !x.AutoKit).ToList();
                }
            }
            else if (Category == "FAVORITES") 
            {
                if (IsAdmin(player))
                {
                    if (playerData.Find(player.userID, out PlayerData.PlayerUsageData playerUsageData))
                    {
                        if (playerUsageData.FavKits.Count > 0)
                        {
                            foreach (int num in playerUsageData.FavKits)
                            {
                                var kit = _data.KitsUI_Data.Values.FirstOrDefault(x => x.ID == num);
                                if (kit != null)
                                {
                                    KitList.Add(kit);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (playerData.Find(player.userID, out PlayerData.PlayerUsageData playerUsageData))
                    {
                        if (playerUsageData.FavKits.Count > 0)
                        {
                            foreach (int num in playerUsageData.FavKits)
                            {
                                var kit = _data.KitsUI_Data.Values.FirstOrDefault(x => x.ID == num);
                                if (kit != null && kit.EnableKit && !kit.AutoKit)
                                {
                                    KitList.Add(kit);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (IsAdmin(player))
                {
                    KitList = _data.KitsUI_Data.Values.Where(x => x.Category.Contains(Category)).ToList();
                }
                else
                {
                    KitList = _data.KitsUI_Data.Values.Where(x => x.Category.Contains(Category) && x.EnableKit && !x.AutoKit).ToList();
                }
            }

            if (KitList.Count < 1)
            {
                CuiHelper.DestroyUi(player, "PLAYER_KITS");
                CuiHelper.AddUi(player, container);
                return;
            }

            // Pagination
            int pageSize = 12;
            int totalKits = KitList.Count;
            int totalPages = (int)Math.Ceiling((double)totalKits / pageSize);
            if (currentPage < 1) currentPage = 1;
            if (currentPage > totalPages) currentPage = totalPages;
            int startIndexSecondPage = (currentPage - 1) * pageSize;

            int kitIndex = 0, Row = 0, Col = 0;

            for (int i = (currentPage - 1) * pageSize; i < Math.Min(currentPage * pageSize, totalKits); i++)
            {
                var kit = KitList[i];

                container.Add(new CuiPanel
                {
                    Image = { Color = config.UI.kitList.kitBack.Color(), Material = "assets/content/ui/uibackgroundblur.mat" }, //"0.1607843 0.1607843 0.1294118 0.6""0.1607843 0.1607843 0.1294118 0.9" - "0.2352941 0.2941177 0.1411765 0.6"
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{10 + (Row * 347.07)} {-141.717 - (Col * 141.717)}", OffsetMax = $"{347.07 + (Row * 347.07)} {-10 - (Col * 141.717)}" }
                }, "KITS_LIST", $"Kits_{kitIndex}_{Row}_{Col}");

                container.Add(new CuiElement
                {
                    Name = "KitName",
                    Parent = $"Kits_{kitIndex}_{Row}_{Col}",
                    Components = {
                    new CuiTextComponent { Text = kit.Name, Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = HexConvert(kit.Color, 100) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-162.703 36.834", OffsetMax = "165.575 62.47" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Description",
                    Parent = $"Kits_{kitIndex}_{Row}_{Col}",
                    Components = {
                    new CuiTextComponent { Text = kit.Description, Font = config.UI.kitList.kitDescFont, FontSize = config.UI.kitList.kitDescSize, Align = TextAnchor.UpperLeft, Color = config.UI.kitList.kitDesc.Color() },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-162.703 -8.7", OffsetMax = "165.574 36.834" }
                }
                });

                container.Add(new CuiPanel
                {
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-162.703 -26.306", OffsetMax = "0.004 -8.7" }
                }, $"Kits_{kitIndex}_{Row}_{Col}", "Limit");

                container.Add(new CuiElement
                {
                    Name = "Image_2414",
                    Parent = "Limit",
                    Components = {
                    new CuiRawImageComponent { Color = config.UI.kitList.kitIconsColor.Color(), Png = GetImage(config.UI.kitList.kitLimitIcon) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-81.355 -6", OffsetMax = "-69.355 6" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Label_2113",
                    Parent = "Limit",
                    Components = {
                    new CuiTextComponent { Text = "Uses Limit", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = config.UI.kitList.kitInfo.Color() },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-65.975 -8.803", OffsetMax = "13.301 8.803" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Label_8775",
                    Parent = "Limit",
                    Components = {
                    new CuiTextComponent { Text = kit.UsesLimit == 0? "∞":$"{kit.UsesLimit}", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = config.UI.kitList.kitInfoValue.Color() },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "13.301 -8.803", OffsetMax = "79.845 8.803" }
                    }
                });

                container.Add(new CuiPanel
                {
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-162.703 -43.912", OffsetMax = "0.004 -26.306" }
                }, $"Kits_{kitIndex}_{Row}_{Col}", "Cooldown");

                container.Add(new CuiElement
                {
                    Name = "Image_2414",
                    Parent = "Cooldown",
                    Components = {
                    new CuiRawImageComponent { Color = config.UI.kitList.kitIconsColor.Color(), Png = GetImage(config.UI.kitList.kitCoolIcon) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-81.355 -6", OffsetMax = "-69.355 6" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Label_2113",
                    Parent = "Cooldown",
                    Components = {
                    new CuiTextComponent { Text = "Cooldown", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = config.UI.kitList.kitInfo.Color() },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-65.984 -8.803", OffsetMax = "13.3 8.803" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Label_8775",
                    Parent = "Cooldown",
                    Components = {
                    new CuiTextComponent { Text = kit.Cooldown == 0 ? "None": FormatTime(kit.Cooldown), Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = config.UI.kitList.kitInfoValue.Color() },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "13.301 -8.803", OffsetMax = "79.845 8.803" }
                }
                });

                container.Add(new CuiPanel
                {
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-162.703 -61.518", OffsetMax = "0.004 -43.912" }
                }, $"Kits_{kitIndex}_{Row}_{Col}", "WipeCooldown");

                container.Add(new CuiElement
                {
                    Name = "Image_2414",
                    Parent = "WipeCooldown",
                    Components = {
                    new CuiRawImageComponent { Color = config.UI.kitList.kitIconsColor.Color(), Png = GetImage(config.UI.kitList.kitWipeCoolIcon) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-81.355 -6", OffsetMax = "-69.355 6" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Label_2113",
                    Parent = "WipeCooldown",
                    Components = {
                    new CuiTextComponent { Text = " Wipe Cooldown", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = config.UI.kitList.kitInfo.Color() },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-69.49 -8.803", OffsetMax = "0 8.803" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Label_8775",
                    Parent = "WipeCooldown",
                    Components = {
                    new CuiTextComponent { Text = kit.WipeCooldown == 0 ? "None" : FormatTime(kit.WipeCooldown), Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = config.UI.kitList.kitInfoValue.Color() },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "13.301 -8.803", OffsetMax = "79.845 8.803" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = CanClaimKit(player, kit, false) ? "0.2352941 0.2941177 0.1411765 1" : "1 0.04095837 0 1", Command = $"pkitcon redeemkit {kit.ID}" },
                    Text = { Text = CheckRedeemable(player, kit), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = CanClaimKit(player, kit, false) ? "0.6117647 0.8235294 0.5764706 1" :"0.8235294 0.5882353 0.5764706 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "85.003 -59.1", OffsetMax = "165.577 -39.1" }
                }, $"Kits_{kitIndex}_{Row}_{Col}", "RedeemBTN");

                if (CanClaimKit(player, kit, false))
                {
                    container.Add(new CuiPanel
                    {
                        Image = { Color = CanClaimKit(player, kit, false) ? "0.5058824 0.7411765 0.1254902 1" : "0.8235294 0.5882353 0.5764706 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40.287 -10", OffsetMax = "40.287 -8.67" }
                    }, "RedeemBTN", "Panel_2139");
                }

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2352941 0.2941177 0.1411765 1", Command = $"pkitcon select {kit.ID}" },
                    Text = { Text = "PREVIEW", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.6117647 0.8235294 0.5764706 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "85.003 -36.1", OffsetMax = "165.577 -16.1" }
                }, $"Kits_{kitIndex}_{Row}_{Col}", "PreviewBTN");

                container.Add(new CuiPanel
                {
                    Image = { Color = "0.5058824 0.7411765 0.1254902 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40.287 -10", OffsetMax = "40.287 -8.67" }
                }, "PreviewBTN", "Panel_2139");

                container.Add(new CuiPanel
                {
                    Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.535 -65.858", OffsetMax = "168.535 -64.409" }
                }, $"Kits_{kitIndex}_{Row}_{Col}", "Bar");

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"pkitcon bookmark {kit.ID}" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "144.7 42.2", OffsetMax = "164.7 62.2" }
                }, $"Kits_{kitIndex}_{Row}_{Col}", "BookMarkBTN");

                container.Add(new CuiElement
                {
                    Name = "Image_8276",
                    Parent = "BookMarkBTN",
                    Components = {
                    new CuiRawImageComponent { Color = HasBookMarkKit(player, kit.ID) ? "1 0.7803922 0 1": "0.7450981 0.7529412 0.7607843 1", Png = GetImage(config.UI.kitList.kitBookmarkIcon) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10 -10", OffsetMax = "10 10" }
                }
                });

                //Indexing
                kitIndex++;
                Row++;
                if (Row >= 3)
                {
                    Row = 0;
                    Col++;
                }
            }

            #endregion

            #region Pages

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "418.095 -340.62", OffsetMax = "629.905 -302.18" }
            }, "PLAYER_KITS", "Page");

            container.Add(new CuiButton
            {
                Button = { Color = "0.2352941 0.2941177 0.1411765 1", Command = $"pkitcon page {currentPage + 1} {Category}" },
                Text = { Text = "NEXT", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.6117647 0.8235294 0.5764706 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "25.905 -19.22", OffsetMax = "105.905 0.78" }
            }, "Page", "NextBTN");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.5058824 0.7411765 0.1254902 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40 8.67", OffsetMax = "40 10" }
            }, "NextBTN", "Panel_2139");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5960785 0.1764706 0.1294118 1", Command = $"pkitcon page {currentPage - 1} {Category}" },
                Text = { Text = "PREV", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.8235294 0.5882353 0.5764706 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-105.905 -19.22", OffsetMax = "-25.905 0.78" }
            }, "Page", "PrevBTN");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.8235294 0.5882353 0.5764706 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40 8.67", OffsetMax = "40 10" }
            }, "PrevBTN", "Panel_2139");

            container.Add(new CuiElement
            {
                Name = "Label_4631",
                Parent = "Page",
                Components = {
                    new CuiTextComponent { Text = $"{currentPage}/{totalPages}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-18.65 -19.22", OffsetMax = "18.65 0.78" }
                }
            });

            #endregion

            CuiHelper.DestroyUi(player, "PLAYER_KITS");
            CuiHelper.AddUi(player, container);

        }

        private void KitPreviews(BasePlayer player, Kits kit)
        {
            var container = new CuiElementContainer();

            #region TopBar

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-639.979 -360.003", OffsetMax = "640.021 360.007" }
            }, "PLAYER_KITS", "KitPreviews");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.05098039 0.05098039 0.05098039 1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },//0.1058824 0.1254902 0.1058824 0.9
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-56.126 -307.715", OffsetMax = "474.126 281.715" }
            }, "KitPreviews", "KitSelected");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1647059 0.1647059 0.1333333 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-265.125 255.099", OffsetMax = "265.125 294.72" }
            }, "KitSelected", "TopBar");

            container.Add(new CuiElement
            {
                Name = "Label_2152",
                Parent = "TopBar",
                Components = {
                    new CuiTextComponent { Text = kit.Name, Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7921569 0.7921569 0.7921569 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-251.301 -19.81", OffsetMax = "0.005 19.81" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5960785 0.1764706 0.1294118 1", Command = "pkitcon closepreview" },
                Text = { Text = "✕", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.8235294 0.5882353 0.5764706 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "234.6 -11.5", OffsetMax = "257.6 11.5" }
            }, "TopBar", "CloseBTN");

            if (IsAdmin(player))
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.5960785 0.1764706 0.1294118 1", Command = $"pkitcon editkit {kit.ID}" },
                    Text = { Text = "EDIT KIT", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.8235294 0.5882353 0.5764706 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "155.861 -11.5", OffsetMax = "231.3 11.5" }
                }, "TopBar", "EditBTN");
            }

            #endregion

            #region Inventory

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1647059 0.1647059 0.1333333 0" },//0.1647059 0.1647059 0.1333333 0.8
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-258.247 -98.051", OffsetMax = "259.541 249" }
            }, "KitSelected", "Inventory");

            int Row = 0, Col = 0, Index = 0;

            for (int i = 0; i < 24; i++)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "1 1 1 0.15" },//0.2352941 0.2313726 0.2235294 0 - 0.2352941 0.2313726 0.2235294 0.7
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{6.394 + (Row * 85)} {-96.026 - (Col * 80)}", OffsetMax = $"{86.394 + (Row * 85)} {-21.026 - (Col * 80)}" }
                }, "Inventory", $"Items{Index}_{Row}");

                foreach(var item in kit.MainItems)
                {
                    int position = item.Position;
                    if (position == Index)
                    {
                        if (!string.IsNullOrEmpty(item.ImageURL))
                        {
                            container.Add(new CuiElement
                            {
                                Name = "ItemImage",
                                Parent = $"Items{Index}_{Row}",
                                Components = {
                                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.ImageURL) },
                                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-37.5 -35", OffsetMax = "37.5 35" }
                                }
                            });
                        }
                        else
                        {
                            container.Add(new CuiElement
                            {
                                Name = "ItemImage",
                                Parent = $"Items{Index}_{Row}",
                                Components =
                                {
                                    new CuiImageComponent { Color = "1 1 1 1", ItemId = item.ItemID, SkinId = item.SkinID },
                                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-37.5 -35", OffsetMax = "37.5 35" } 
                                }
                            });
                        }

                        if (item.Amount > 1)
                        {
                            container.Add(new CuiElement
                            {
                                Name = "ItemCount",
                                Parent = $"Items{Index}_{Row}",
                                Components = {
                                    new CuiTextComponent { Text = "x" + item.Amount, Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" },
                                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.721 -37.5", OffsetMax = "37.5 -18.43" }
                                }
                            });
                        }
                    }
                }

                Row++;
                Index++;
                if (Row >= 6)
                {
                    Row = 0;
                    Col++;
                }
            }

            #endregion

            #region Wear

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1647059 0.1647059 0.1333333 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-258.895 -193.593", OffsetMax = "258.894 -106.17" }
            }, "KitSelected", "Wear");

            int WearRow = 0, WearCol = 0, WearIndex = 0;

            for (int i = 0; i < 7; i++)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "1 1 1 0.15" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{4.894 + (WearRow * 73)} -81.211", OffsetMax = $"{74.894 + (WearRow * 73)} -6.211" }
                }, "Wear", $"Items{WearIndex}_{WearRow}");

                foreach (var item in kit.WearItems)
                {
                    int position = item.Position;
                    if (position == WearIndex)
                    {
                        if (!string.IsNullOrEmpty(item.ImageURL))
                        {
                            container.Add(new CuiElement
                            {
                                Name = "ItemImage",
                                Parent = $"Items{WearIndex}_{WearRow}",
                                Components = {
                                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.ImageURL) },
                                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.5 -35", OffsetMax = "32.5 35" }
                                }
                            });
                        }
                        else
                        {
                            container.Add(new CuiElement
                            {
                                Name = "ItemImage",
                                Parent = $"Items{WearIndex}_{WearRow}",
                                Components =
                                {
                                    new CuiImageComponent { Color = "1 1 1 1", ItemId = item.ItemID, SkinId = item.SkinID },
                                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.5 -35", OffsetMax = "32.5 35" }
                                }
                            });
                        }

                        if (item.Amount > 1)
                        {
                            container.Add(new CuiElement
                            {
                                Name = "ItemCount",
                                Parent = $"Items{WearIndex}_{WearRow}",
                                Components = {
                                    new CuiTextComponent { Text = "x" + item.Amount, Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" },
                                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-34.11 -37.5", OffsetMax = "32.5 -18.43" }
                                }
                            });
                        }
                    }
                }

                WearRow++;
                WearIndex++;
                if (WearRow >= 7)
                {
                    WearRow = 0;
                    WearCol++;
                }
            }

            #endregion

            #region Belt

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1647059 0.1647059 0.1333333 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-258.895 -286.711", OffsetMax = "258.894 -199.289" }
            }, "KitSelected", "Belt");

            int BeltRow = 0, BeltCol = 0, BeltIndex = 0;

            for (int i = 0; i < 6; i++)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "1 1 1 0.15" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{11.394 + (BeltRow * 83)} -81.211", OffsetMax = $"{91.394 + (BeltRow * 83)} -6.211" }
                }, "Belt", $"Items{BeltIndex}_{BeltRow}");

                foreach (var item in kit.BeltItems)
                {
                    int position = item.Position;
                    if (position == BeltIndex)
                    {
                        if (!string.IsNullOrEmpty(item.ImageURL))
                        {
                            container.Add(new CuiElement
                            {
                                Name = "ItemImage",
                                Parent = $"Items{BeltIndex}_{BeltRow}",
                                Components = {
                                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.ImageURL) },
                                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-37.5 -35", OffsetMax = "37.5 35" }
                                }
                            });
                        }
                        else
                        {
                            container.Add(new CuiElement
                            {
                                Name = "ItemImage",
                                Parent = $"Items{BeltIndex}_{BeltRow}",
                                Components =
                                {
                                    new CuiImageComponent { Color = "1 1 1 1", ItemId = item.ItemID, SkinId = item.SkinID },
                                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-37.5 -35", OffsetMax = "37.5 35" }
                                }
                            });
                        }

                        if (item.Amount > 1)
                        {
                            container.Add(new CuiElement
                            {
                                Name = "ItemCount",
                                Parent = $"Items{BeltIndex}_{BeltRow}",
                                Components = {
                                    new CuiTextComponent { Text = "x" + item.Amount, Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" },
                                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29.11 -37.5", OffsetMax = "37.5 -18.43" }
                                }
                            });
                        }
                    }
                }

                BeltRow++;
                BeltIndex++;
                if (BeltRow >= 6)
                {
                    BeltRow = 0;
                    BeltCol++;
                }
            }

            #endregion

            #region Information
            
            container.Add(new CuiPanel
            {
                Image = { Color = "0.05098039 0.05098039 0.05098039 1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }, //0.1058824 0.1254902 0.1058824 0.9
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-418.977 -154.503", OffsetMax = "-63.874 281.715" }
            }, "KitPreviews", "Information");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1647059 0.1647059 0.1333333 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-177.629 178.49", OffsetMax = "177.471 218.11" }
            }, "Information", "TopBar");

            container.Add(new CuiElement
            {
                Name = "Label_2152",
                Parent = "TopBar",
                Components = {
                    new CuiTextComponent { Text = "KIT INFORMATION", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7921569 0.7921569 0.7921569 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-167.964 -19.81", OffsetMax = "0.005 19.81" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "DesName",
                Parent = "Information",
                Components = {
                    new CuiTextComponent { Text = "DESCRIPTION", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleLeft, Color = "0.7921569 0.7921569 0.7921569 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.914 136.26", OffsetMax = "-0.944 175.88" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "KitDescription",
                Parent = "Information",
                Components = {
                    new CuiTextComponent { Text = kit.Description, Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.UpperLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.913 30.265", OffsetMax = "168.913 136.255" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "UsesName",
                Parent = "Information",
                Components = {
                    new CuiTextComponent { Text = "USES LIMIT", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.LowerLeft, Color = "0.7921569 0.7921569 0.7921569 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.914 -9.355", OffsetMax = "-0.944 30.265" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "KitLimit",
                Parent = "Information",
                Components = {
                    new CuiTextComponent { Text = kit.UsesLimit == 0 ? "∞":$"{kit.UsesLimit}", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.UpperLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.985 -33.281", OffsetMax = "168.842 -9.355" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "CoolName",
                Parent = "Information",
                Components = {
                    new CuiTextComponent { Text = "COOLDOWN", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.LowerLeft, Color = "0.7921569 0.7921569 0.7921569 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.994 -72.994", OffsetMax = "167.816 -33.374" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "KitCooldown",
                Parent = "Information",
                Components = {
                    new CuiTextComponent { Text = kit.Cooldown == 0 ? "None": FormatTime(kit.Cooldown), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.UpperLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.913 -96.92", OffsetMax = "168.913 -72.994" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "WipeCoolName",
                Parent = "Information",
                Components = {
                    new CuiTextComponent { Text = "WIPE COOLDOWN", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.LowerLeft, Color = "0.7921569 0.7921569 0.7921569 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.994 -136.54", OffsetMax = "168.836 -96.92" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "WipeCooldown",
                Parent = "Information",
                Components = {
                    new CuiTextComponent { Text = kit.WipeCooldown == 0 ? "None" : FormatTime(kit.WipeCooldown), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.UpperLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.99 -160.463", OffsetMax = "168.836 -136.537" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = CanClaimKit(player, kit, false) ? "0.2352941 0.2941177 0.1411765 1" : "0.1372549 0.4862745 0.08235294 1", Command = $"pkitcon redeemkit {kit.ID}" },
                Text = { Text = "REDEEM", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.6117647 0.8235294 0.5764706 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-166.953 -214.986", OffsetMax = "169.857 -180.614" }
            }, "Information", "RedeemBTN");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.5058824 0.7411765 0.1254902 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-168.405 -17.186", OffsetMax = "168.405 -15.856" }
            }, "RedeemBTN", "Panel_2139");

            #endregion

            CuiHelper.DestroyUi(player, "KitPreviews");
            CuiHelper.AddUi(player, container);
        }

        private void CreateOrEditKit(BasePlayer player, Kits kit, bool Creating = false)
        {
            var container = new CuiElementContainer();

            #region TopBar

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                KeyboardEnabled = true,
                Image = { Color = "0 0 0 0.7", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-639.981 -360.005", OffsetMax = "640.019 360.005" }
            }, "PLAYER_KITS", "CreatingKit");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1058824 0.1254902 0.1058824 0.9019608" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.897 -321.462", OffsetMax = "610.897 321.462" }
            }, "CreatingKit", "MainPanel");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.898 274.501", OffsetMax = "610.902 321.459" }
            }, "MainPanel", "TopBar");

            container.Add(new CuiElement
            {
                Name = "Creating name",
                Parent = "TopBar",
                Components = {
                    new CuiTextComponent { Text = Creating ? "CREATING A KIT":"EDITING A KIT", Font = "robotocondensed-bold.ttf", FontSize = 25, Align = TextAnchor.MiddleLeft, Color = "0.7450981 0.7529412 0.7607843 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-598.907 -23.479", OffsetMax = "-401.997 23.478" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-402.002 -23.477", OffsetMax = "610.898 -21.629" }
            }, "TopBar", "Bar");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5960785 0.1764706 0.1294118 1", Command = "pkitcon closeediting" },
                Text = { Text = "✕", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.8235294 0.5882353 0.5764706 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "582.798 -8.68", OffsetMax = "605.798 14.32" }
            }, "TopBar", "CloseBTN");

            #endregion

            #region Editing Options

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.898 -321.461", OffsetMax = "610.902 274.499" }
            }, "MainPanel", "Main");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 231.263", OffsetMax = "0 272.737" }
            }, "Main", "KitName");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "KitName",
                Components = {
                    new CuiTextComponent { Text = "Kit Name", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "InputField_6683",
                Parent = "KitName",
                Components = {
                    new CuiInputFieldComponent { Command = $"pkitcon changeName {kit.ID}", Text = kit.Name, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -20.737", OffsetMax = "301 20.738" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "KitName", "Bar");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 189.793", OffsetMax = "0 231.267" }
            }, "Main", "KitDes");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "KitDes",
                Components = {
                    new CuiTextComponent { Text = "Kit Description", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "InputField_6683",
                Parent = "KitDes",
                Components = {
                    new CuiInputFieldComponent { Command = $"pkitcon changeDes {kit.ID}", Text = kit.Description, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -20.737", OffsetMax = "301 20.738" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "KitDes", "Bar");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 148.323", OffsetMax = "0 189.797" }
            }, "Main", "KitPerm");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "KitPerm",
                Components = {
                    new CuiTextComponent { Text = "Kit Permission (playerkits.example)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "InputField_6683",
                Parent = "KitPerm",
                Components = {
                    new CuiInputFieldComponent { Command = $"pkitcon changePerm {kit.ID}", Text = kit.Permission, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -20.737", OffsetMax = "301 20.738" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "KitPerm", "Bar");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 106.853", OffsetMax = "0 148.327" }
            }, "Main", "KitColor");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "KitColor",
                Components = {
                    new CuiTextComponent { Text = "Kit Color(#Hexa)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "InputField_6683",
                Parent = "KitColor",
                Components = {
                    new CuiInputFieldComponent { Command = $"pkitcon changeColor {kit.ID}", Text = kit.Color, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -20.737", OffsetMax = "301 20.738" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "KitColor", "Bar");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 65.379", OffsetMax = "0 106.853" }
            }, "Main", "KitCategory");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "KitCategory",
                Components = {
                    new CuiTextComponent { Text = "Kit Category", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "InputField_6683",
                Parent = "KitCategory",
                Components = {
                    new CuiInputFieldComponent { Command = $"pkitcon changeCategory {kit.ID}", Text = kit.Category, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -20.737", OffsetMax = "301 20.738" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "KitCategory", "Bar");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 23.905", OffsetMax = "0 65.379" }
            }, "Main", "kitUses");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "kitUses",
                Components = {
                    new CuiTextComponent { Text = "Kit Uses Limit", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "InputField_6683",
                Parent = "kitUses",
                Components = {
                    new CuiInputFieldComponent { Command = $"pkitcon changeUses {kit.ID}", Text = kit.UsesLimit.ToString(), Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -20.737", OffsetMax = "301 20.738" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "kitUses", "Bar");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 -17.57", OffsetMax = "0 23.905" }
            }, "Main", "KitCooldown");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "KitCooldown",
                Components = {
                    new CuiTextComponent { Text = "Kit Cooldown", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "InputField_6683",
                Parent = "KitCooldown",
                Components = {
                    new CuiInputFieldComponent { Command = $"pkitcon changeCool {kit.ID}", Text = kit.Cooldown.ToString(), Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -20.737", OffsetMax = "301 20.738" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "KitCooldown", "Bar");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 -59.044", OffsetMax = "0 -17.57" }
            }, "Main", "KitWipeCooldown");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "KitWipeCooldown",
                Components = {
                    new CuiTextComponent { Text = "Kit Wipe Cooldown", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "InputField_6683",
                Parent = "KitWipeCooldown",
                Components = {
                    new CuiInputFieldComponent { Command = $"pkitcon changeWipe {kit.ID}", Text = kit.WipeCooldown.ToString(), Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -20.737", OffsetMax = "301 20.738" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "KitWipeCooldown", "Bar");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 -100.518", OffsetMax = "0 -59.044" }
            }, "Main", "KitAutoKit");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "KitAutoKit",
                Components = {
                    new CuiTextComponent { Text = "Kit AutoKit Enable?", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "KitAutoKit", "Bar");

            container.Add(new CuiButton
            {
                Button = { Color = "0.2352941 0.2941177 0.1411765 1", Command = $"pkitcon changeAuto {kit.ID}" },
                Text = { Text = kit.AutoKit ? "TRUE": "FALSE", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = kit.AutoKit ? "0.1375685 1 0 1": "1 0.2127943 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -19.229", OffsetMax = "301 20.737" }
            }, "KitAutoKit", "Button_9150");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 -141.997", OffsetMax = "0 -100.523" }
            }, "Main", "KitAutoWeight");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "KitAutoWeight",
                Components = {
                    new CuiTextComponent { Text = "Kit Auto priority", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "InputField_6683",
                Parent = "KitAutoWeight",
                Components = {
                    new CuiInputFieldComponent { Command = $"pkitcon changePri {kit.ID}", Text = kit.AutoKitWeight.ToString(), Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -20.737", OffsetMax = "301 20.738" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "KitAutoWeight", "Bar");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-610.9 -183.467", OffsetMax = "0 -141.993" }
            }, "Main", "kit Enable");

            container.Add(new CuiElement
            {
                Name = "Label_6415",
                Parent = "kit Enable",
                Components = {
                    new CuiTextComponent { Text = "Kit Enable?", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.7490196 0.7490196 0.6745098 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291 -20.737", OffsetMax = "0 20.737" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.7490196 0.7490196 0.6745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -20.737", OffsetMax = "301 -19.229" }
            }, "kit Enable", "Bar");

            container.Add(new CuiButton
            {
                Button = { Color = "0.2352941 0.2941177 0.1411765 1", Command = $"pkitcon changeKitEnable {kit.ID}" },
                Text = { Text = kit.EnableKit ? "TRUE":"FALSE", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = kit.EnableKit ? "0.1375685 1 0 1": "1 0.2127943 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -19.229", OffsetMax = "301 20.737" }
            }, "kit Enable", "Button_9150");

            #endregion

            #region Buttons

            container.Add(new CuiButton
            {
                Button = { Color = "0.2352941 0.2941177 0.1411765 1", Command = $"pkitcon savekit {kit.ID}" },
                Text = { Text = "SAVE KIT", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "472.099 -317.183", OffsetMax = "607.4 -294.813" }
            }, "MainPanel", "SaveKit");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.5058824 0.7411765 0.1254902 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-67.649 -11.185", OffsetMax = "67.651 -9.835" }
            }, "SaveKit", "Panel_2139");

            container.Add(new CuiButton
            {
                Button = { Color = "0.2352941 0.2941177 0.1411765 1", Command = $"pkitcon copyinv {kit.ID}" },
                Text = { Text = "COPY INVENTORY", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "333.25 -317.183", OffsetMax = "468.55 -294.813" }
            }, "MainPanel", "CopyInv");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.5058824 0.7411765 0.1254902 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-67.649 -11.185", OffsetMax = "67.651 -9.835" }
            }, "CopyInv", "Panel_2139");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5960785 0.1764706 0.1294118 1", Command = $"pkitcon delete {kit.ID}" },
                Text = { Text = "DELETE KIT", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "194.85 -317.183", OffsetMax = "330.15 -294.813" }
            }, "MainPanel", "DeleteKit");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 0.2127943 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-67.649 -11.185", OffsetMax = "67.651 -9.835" }
            }, "DeleteKit", "Panel_2139");

            #endregion

            CuiHelper.DestroyUi(player, "CreatingKit");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Kits Data

        private class KitsUIData
        {
            public Dictionary<int, Kits> KitsUI_Data = new Dictionary<int, Kits>();
        }

        private void LoadData()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<KitsUIData>($"{Name}/KitsUI_Data");
        }
        
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/KitsUI_Data", _data);
        }

        private class Kits
        {
            public int ID;
            public string Name;
            public string Description;
            public string Permission;
            public string Color = "#237C15";
            public string Category = "";
            public int Cooldown;
            public int WipeCooldown;
            public int UsesLimit;
            public bool EnableKit = true;
            public bool AutoKit = false;
            public float AutoKitWeight = 0.0f;
            public List<string> CommandsOnReceving = new List<string>();
            public List<KitItems> WearItems = new List<KitItems>();
            public List<KitItems> BeltItems = new List<KitItems>();
            public List<KitItems> MainItems = new List<KitItems>();           
        }

        private class KitItems
        {
            public string ShortName;
            public string DisplayName;
            public int Amount;
            public int ItemID;
            public ulong SkinID;
            public int Position;
            public float Condition;
            public float MaxCondition;
            public string ImageURL;
            public int WeaponAmmo;
            public string AmmoType;
            public bool GiveItem = true;
            public List<string> GiveCommand;
            public List<KitItems> Content;
        }

        private Kits FindKitByName(string name)
        {
            return _data.KitsUI_Data.Values.FirstOrDefault(kit => kit.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private Kits FindKitByID(int id)
        {
            return _data.KitsUI_Data.TryGetValue(id, out var kit) ? kit : null;
        }

        private void RemoveKitByName(string name)
        {
            var kitToRemove = FindKitByName(name);
            if (kitToRemove != null)
            {
                _data.KitsUI_Data.Remove(kitToRemove.ID);
                SaveData();
            }
        }

        private void RemoveKitByID(int id)
        {
            if (_data.KitsUI_Data.TryGetValue(id, out var kitToRemove))
            {
                _data.KitsUI_Data.Remove(id);
                SaveData();
            }
        }

        private List<KitItems> GetKitItems(ItemContainer container)
        {
            // Retrieve items from the container and create KitItems objects
            return container.itemList.Select(item =>
                new KitItems
                {
                    ShortName = item.info.shortname,
                    DisplayName = item.name,
                    ItemID = item.info.itemid,
                    Amount = item.amount,
                    SkinID = item.skin,
                    Position = item.position,
                    Condition = item.condition,
                    MaxCondition = item.maxCondition,
                    ImageURL = "", 
                    WeaponAmmo = WeaponAmmoCheck(item), //(item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0
                    AmmoType = WeaponTypeCheck(item),  //(item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname
                    Content = item.contents != null ? GetKitItems(item.contents) : null
                }).ToList();
        }

        private int WeaponAmmoCheck(Item item)
        {
            FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
            if (flameThrower != null)
                return flameThrower.ammo;

            Chainsaw chainsaw = item.GetHeldEntity() as Chainsaw;
            if (chainsaw != null)
                return chainsaw.ammo;

            BaseProjectile projectile = item.GetHeldEntity() as BaseProjectile;
            if (projectile != null)
                return projectile.primaryMagazine.contents;

            return 0;
        }

        private string WeaponTypeCheck(Item item)
        {
            if (item == null || item.GetHeldEntity() == null)
            {
                return string.Empty;
            }

            FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
            if (flameThrower != null)
            {
                return flameThrower.GetAmmo()?.info.shortname ?? string.Empty;
            }

            Chainsaw chainsaw = item.GetHeldEntity() as Chainsaw;
            if (chainsaw != null)
            {
                return chainsaw.GetAmmo()?.info.shortname ?? string.Empty;
            }

            BaseProjectile projectile = item.GetHeldEntity() as BaseProjectile;
            if (projectile != null)
            {
                return projectile.primaryMagazine?.ammoType?.shortname ?? string.Empty;
            }

            return string.Empty;
        }

        private void DeleteKit(BasePlayer player, string identifier)
        {
            // Check whether the identifier is numeric (ID) or alphanumeric (Name)
            if (int.TryParse(identifier, out int kitId))
            {
                // Delete kit by ID
                Kits kit = FindKitByID(kitId);
                if (kit != null)
                {
                    RemoveKitByID(kitId);
                    CM(player, $"You have deleted the kit <color=#ce422b>{kit.Name}</color>.");
                    Puts($"{kit.Name} deleted from kits.");
                }               
            }
            else
            {
                // Delete kit by Name
                Kits kit = FindKitByName(identifier);
                if (kit != null)
                {
                    RemoveKitByName(identifier);
                    CM(player, $"You have deleted the kit <color=#ce422b>{kit.Name}</color>.");
                    Puts($"{kit.Name} deleted from kits.");
                }                
            }
        }

        private Kits FindKit(string identifier)
        {
            if (int.TryParse(identifier, out int kitId))
            {
                return FindKitByID(kitId);
            }
            else
            {
                return FindKitByName(identifier);
            }
            //show message
        }

        private void CheckKits()
        {
            HashSet<int> deleteList = new HashSet<int>();

            if (_data.KitsUI_Data != null && _data.KitsUI_Data.Values != null)
            {
                foreach (var kit in _data.KitsUI_Data.Values)
                {
                    if (kit.MainItems != null && kit.WearItems != null && kit.BeltItems != null)
                    {
                        if (kit.MainItems.Count == 0 && kit.WearItems.Count == 0 && kit.BeltItems.Count == 0)
                        {
                            deleteList.Add(kit.ID);
                            RemoveKitByID(kit.ID);
                        }
                    }
                }

                for(int i = 0; i < deleteList.Count; i++)
                {
                    RemoveKitByID(deleteList.ElementAt(i));
                }

                Puts("Loaded Kits");
                Puts($"Redeemable Kits - ({_data.KitsUI_Data.Values.Where(kit => !kit.AutoKit).Count()})");
                Puts($"Spawn/Auto Kits - ({_data.KitsUI_Data.Values.Where(kit => kit.AutoKit).Count()})");
            }
            else
            {
                // Handle the case where KitsUI_Data or its Values property is null
                Puts("Kits data is not loaded or is empty.");
            }

        }

        private void ShowKits(BasePlayer player = null)
        {
            // Get non-auto kits
            var nonAutoKits = _data.KitsUI_Data.Values.Where(kit => !kit.AutoKit);

            // Get auto kits
            var autoKits = _data.KitsUI_Data.Values.Where(kit => kit.AutoKit);

            // Show non-auto kits
            string nonAutoKitMessage = "<color=#FFD700>Redeemable Kits:</color>\n";
            foreach (var kit in nonAutoKits)
            {
                nonAutoKitMessage += $"ID: <color=#FFD700>{kit.ID}</color>, Name: <color=#FFD700>{kit.Name}</color>\n";
            }

            // Show auto kits
            string autoKitMessage = "<color=#FFD700>Spawn/Auto Kits:</color>\n";
            foreach (var kit in autoKits)
            {
                autoKitMessage += $"ID: <color=#FFD700>{kit.ID}</color>, Name: <color=#FFD700>{kit.Name}</color>, AutoKitWeight: <color=#FFD700>{kit.AutoKitWeight}</color>\n";
            }

            // Output messages
            if (player != null)
            {
                player.ChatMessage(nonAutoKitMessage);
                player.ChatMessage(autoKitMessage);
            }
            else
            {
                Puts(nonAutoKitMessage);
                Puts(autoKitMessage);
            }
        }

        private IEnumerable<Item> GetKitItems(string IDorName)
        {
            Kits kit = FindKit(IDorName);
            List<Item> items = new List<Item>();

            if ( kit != null)
            {
                foreach (var item in kit.MainItems)
                {
                    Item i = CreateItem(item);
                    items.Add(i);
                }
                foreach (var item in kit.WearItems)
                {
                    Item i = CreateItem(item);
                    items.Add(i);
                }
                foreach (var item in kit.BeltItems)
                {
                    Item i = CreateItem(item);
                    items.Add(i);
                }
            }           

            return items;
        }

        private void RegisterAllKitsPerms()
        {
            foreach (var kit in _data.KitsUI_Data.Values)
            {
                if (!string.IsNullOrEmpty(kit.Permission))
                    RegisterKitPerm(kit.Permission);
            }
        }

        private void RegisterKitPerm(string PermName)
        {
            if (!permission.PermissionExists(PermName))
                permission.RegisterPermission(PermName, this);
        }

        private void GiveKit(BasePlayer player, Kits kit)
        {
            if (CanClaimKit(player, kit))
            {
                CM(player, $"You received <color=#FFD700>{kit.Name}</color> Kit.");
                GiveItemsTo(player, kit);
                OnKitReceived(player, kit);
                if (kit.CommandsOnReceving != null)
                {
                    foreach (var cmd in kit.CommandsOnReceving)
                    {
                        Server.Command(cmd.Replace("{ID}", player.UserIDString));
                    }
                }
                CuiHelper.DestroyUi(player, "PLAYER_KITS");
                NotifyMessage(player, NotifyType.SUCCESS, $"You successfully redeemed <color=#FFD700>{kit.Name}</color>");
                return;
            }

        }

        private bool CanClaimKit(BasePlayer player, Kits kit, bool notify = true)
        {
            if (!string.IsNullOrEmpty(kit.Permission) && !permission.UserHasPermission(player.UserIDString, kit.Permission))
            {
                if (notify)
                {
                    //ShowNotify(player, "You don't have permission to use this command.");
                    NotifyMessage(player, NotifyType.ERROR, "You don't have permission to use this command.");
                    PlaySound(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                }
                return false;
            }

            if (kit.WipeCooldown > 0 && IsOnWipeCooldown(kit))
            {
                if (notify)
                {
                    //ShowNotify(player, "Kit's on Wipe Cooldown. {TimeFormat} left before you can use it.".Replace("{TimeFormat}", FormatTime(TimeLeftBeforeKitCanBeUsed(kit))));
                    NotifyMessage(player, NotifyType.ERROR, $"<color=#FFD700>{kit.Name}</color> on Wipe Cooldown. (TimeFormat) left before you can use it.".Replace("(TimeFormat)", FormatTime(TimeLeftBeforeKitCanBeUsed(kit))));
                    PlaySound(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                }
                return false;
            }

            if (playerData.Find(player.userID, out PlayerData.PlayerUsageData playerUsageData))
            {
                if (kit.Cooldown > 0)
                {
                    double cooldownRem = playerUsageData.GetCooldownRemaining(kit.ID);
                    if (cooldownRem > 0)
                    {
                        if (notify)
                        {
                            //ShowNotify(player, "Kits on cooldown. {TimeFormat} left before you can use it.".Replace("{TimeFormat}", FormatTime(playerData[player.userID].GetCooldownRemaining(kit.ID))));
                            NotifyMessage(player, NotifyType.ERROR, $"<color=#FFD700>{kit.Name}</color> on cooldown. (TimeFormat) left before you can use it.".Replace("(TimeFormat)", FormatTime(playerData[player.userID].GetCooldownRemaining(kit.ID))));
                            PlaySound(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                        }
                        return false;
                    }
                }

                if (kit.UsesLimit > 0)
                {
                    int currentUses = playerUsageData.GetKitUses(kit.ID);
                    if (currentUses >= kit.UsesLimit)
                    {
                        if (notify)
                        {
                            //ShowNotify(player, "Kits reached it max use limit.");
                            NotifyMessage(player, NotifyType.ERROR, $"<color=#FFD700>{kit.Name}</color> reached its max use limit.");
                            PlaySound(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                        }
                        return false;
                    }
                }
            }

            if (!HasSpaceForItems(player, kit))
            {
                if (notify)
                {
                    //ShowNotify(player, "You don't have enough space to claim this kit.");
                    NotifyMessage(player, NotifyType.ERROR, "You don't have enough space to claim this kit.");
                    PlaySound(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                }
                return false;
            }
            //Costs

            return true;
        }


        private bool HasSpaceForItems(BasePlayer player, Kits kit)
        {
            int wearSpacesFree = 8 - player.inventory.containerWear.itemList.Count;
            int mainSpacesFree = 24 - player.inventory.containerMain.itemList.Count;
            int beltSpacesFree = 6 - player.inventory.containerBelt.itemList.Count;

            int ItemCount = kit.WearItems.Count + kit.MainItems.Count + kit.BeltItems.Count;

            return (wearSpacesFree >= kit.WearItems.Count &&
                    beltSpacesFree >= kit.BeltItems.Count &&
                    mainSpacesFree >= kit.MainItems.Count) || ItemCount <= mainSpacesFree + beltSpacesFree;
        }


        private bool HasKitItems(Kits kit)
        {
            // Check if kit is not null
            if (kit != null)
            {
                // Check if any of the item lists in the kit object contain items
                return kit.WearItems != null && kit.WearItems.Count > 0
                    || kit.BeltItems != null && kit.BeltItems.Count > 0
                    || kit.MainItems != null && kit.MainItems.Count > 0;
            }
            // If kit is null, return false
            return false;
        }

        private void GiveItemsTo(BasePlayer player, Kits kit)
        {
            // Create a list to track leftover items
            List<KitItems> leftOverItems = new List<KitItems>();

            // Give items to the player's inventory based on the kit
            GiveItems(kit.MainItems, player.inventory.containerMain, ref leftOverItems);
            GiveItems(kit.WearItems, player.inventory.containerWear, ref leftOverItems, true);
            GiveItems(kit.BeltItems, player.inventory.containerBelt, ref leftOverItems);

            // Process leftover items if any
            ProcessLeftOverItems(player, player.inventory, leftOverItems);
        }

        private void ProcessLeftOverItems(BasePlayer player, PlayerInventory playerInventory, List<KitItems> leftOverItems)
        {
            foreach (var itemData in leftOverItems)
            {
                // Create an item from KitItems
                Item item = CreateItem(itemData);

                // Try to move the item to the ideal container
                if (!MoveToIdealContainer(playerInventory, item))
                {
                    // If failed, attempt to move it to the main or belt container
                    if (!item.MoveToContainer(playerInventory.containerMain, -1, true) && !item.MoveToContainer(playerInventory.containerBelt, -1, true))
                    {
                        // If all attempts fail, drop the item
                        item.Drop(player.GetDropPosition(), player.GetDropVelocity());
                    }
                }
            }
        }

        private void GiveItems(List<KitItems> kitItems, ItemContainer container, ref List<KitItems> leftOverItems, bool isWearContainer = false)
        {
            for (int i = 0; i < kitItems.Count; i++)
            {
                KitItems itemData = kitItems[i];
                if (itemData.Amount < 1)
                    continue;
                if (container.GetSlot(itemData.Position) != null)
                    leftOverItems.Add(itemData);
                else
                {
                    Item item = CreateItem(itemData);
                    if (!isWearContainer || (isWearContainer && item.info.isWearable && CanWearItem(container, item)))
                    {
                        item.position = itemData.Position;
                        item.SetParent(container);
                    }
                    else
                    {
                        leftOverItems.Add(itemData);
                        item.Remove(0f);
                    }
                }
            }
        }

        private bool CanWearItem(ItemContainer containerWear, Item item)
        {
            ItemModWearable itemModWearable = item.info.GetComponent<ItemModWearable>();
            if (itemModWearable == null)
                return false;

            for (int i = 0; i < containerWear.itemList.Count; i++)
            {
                Item otherItem = containerWear.itemList[i];
                if (otherItem != null)
                {
                    ItemModWearable otherModWearable = otherItem.info.GetComponent<ItemModWearable>();
                    if (otherModWearable != null && !itemModWearable.CanExistWith(otherModWearable))
                        return false;
                }
            }

            return true;
        }

        private Item CreateItem(KitItems itemData)
        {
            Item item = ItemManager.CreateByItemID(itemData.ItemID, itemData.Amount, itemData.SkinID);
            item.condition = itemData.Condition;
            item.maxCondition = itemData.MaxCondition;

            if (!string.IsNullOrEmpty(itemData.DisplayName))
            {
                item.name = itemData.DisplayName;
            }

            FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
            if (flameThrower != null)
                flameThrower.ammo = itemData.WeaponAmmo;

            Chainsaw chainsaw = item.GetHeldEntity() as Chainsaw;
            if (chainsaw != null)
                chainsaw.ammo = itemData.WeaponAmmo;

            if (itemData.Content != null)
            {
                foreach (KitItems contentData in itemData.Content)
                {
                    Item newContent = CreateItem(contentData);
                    if (newContent != null)
                    {
                        if (!newContent.MoveToContainer(item.contents))
                            newContent.Remove(0f);
                    }
                }
            }

            BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                weapon.DelayedModsChanged();

                if (!string.IsNullOrEmpty(itemData.AmmoType))
                    weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.AmmoType);
                weapon.primaryMagazine.contents = itemData.WeaponAmmo;
            }

            item.MarkDirty();

            return item;
        }

        private bool MoveToIdealContainer(PlayerInventory playerInventory, Item item)
        {
            if (item.info.isWearable && CanWearItem(playerInventory.containerWear, item))
                return item.MoveToContainer(playerInventory.containerWear, -1, false);

            if (item.info.stackable > 1)
            {
                if (playerInventory.containerBelt != null && playerInventory.containerBelt.FindItemByItemID(item.info.itemid) != null)
                    return item.MoveToContainer(playerInventory.containerBelt, -1, true);


                if (playerInventory.containerMain != null && playerInventory.containerMain.FindItemByItemID(item.info.itemid) != null)
                    return item.MoveToContainer(playerInventory.containerMain, -1, true);

            }
            if (item.info.HasFlag(ItemDefinition.Flag.NotStraightToBelt) || !item.info.isUsable)
                return item.MoveToContainer(playerInventory.containerMain, -1, true);

            return item.MoveToContainer(playerInventory.containerBelt, -1, false);
        }

        private bool IsOnWipeCooldown(Kits kit)
        {
            double currentTime = CurrentTime;
            double nextUseTime = LastWipeTime + kit.WipeCooldown;

            if (currentTime < nextUseTime)
            {
                return true;
            }
            return false;
        }

        private double TimeLeftBeforeKitCanBeUsed(Kits kit)
        {
            double currentTime = CurrentTime;
            double nextUseTime = LastWipeTime + kit.WipeCooldown;

            if (currentTime < nextUseTime)
            {
                return nextUseTime - currentTime;
            }
            else
            {
                return 0;
            }
        }

        #endregion

        #region Player Data

        /// <summary>
        /// //////////////////////////////////////////////// Codes From Rust Kits & modified - Credits To k1lly0u(https://umod.org/plugins/rust-kits)
        /// </summary>

        private class PlayerData
        {
            [JsonProperty(PropertyName = "PlayerUsageData")]
            private Dictionary<ulong, PlayerUsageData> _players = new Dictionary<ulong, PlayerUsageData>();

            internal bool Find(ulong playerId, out PlayerUsageData playerUsageData) => _players.TryGetValue(playerId, out playerUsageData);

            internal bool Exists(ulong playerId) => _players.ContainsKey(playerId);

            internal void Wipe() => _players.Clear();

            internal bool IsValid => _players != null;

            internal PlayerUsageData this[ulong key]
            {
                get
                {
                    if (_players.TryGetValue(key, out PlayerUsageData tValue))
                        return tValue;

                    tValue = (PlayerUsageData)Activator.CreateInstance(typeof(PlayerUsageData));
                    _players.Add(key, tValue);
                    return tValue;
                }
                set
                {
                    if (value == null)
                    {
                        _players.Remove(key);
                        return;
                    }
                    _players[key] = value;
                }
            }

            internal void OnKitClaimed(BasePlayer player, Kits kit)
            {
                if (kit.UsesLimit == 0 && kit.Cooldown == 0)
                    return;

                if (!_players.TryGetValue(player.userID, out PlayerUsageData playerUsageData))
                    playerUsageData = _players[player.userID] = new PlayerUsageData();

                playerUsageData.OnKitClaimed(kit);
            }

            

            public class PlayerUsageData
            {
                [JsonProperty(PropertyName = "Usages")]
                private Hash<int, KitUsageData> _usageData = new Hash<int, KitUsageData>();

                public bool ClaimAutoKits { get; set; } = true;
                public bool ShowAutoKitList { get; set; } = false;
                public List<int> FavKits { get; set; } = new List<int>();


                internal double GetCooldownRemaining(int id)
                {
                    if (!_usageData.TryGetValue(id, out KitUsageData kitUsageData))
                        return 0;

                    double currentTime = CurrentTime;

                    return currentTime > kitUsageData.NextUseTime ? 0 : kitUsageData.NextUseTime - CurrentTime;
                }

                internal void SetCooldownRemaining(int id, double seconds)
                {
                    if (!_usageData.TryGetValue(id, out KitUsageData kitUsageData))
                        return;

                    kitUsageData.NextUseTime = CurrentTime + seconds;
                }

                internal int GetKitUses(int id)
                {
                    if (!_usageData.TryGetValue(id, out KitUsageData kitUsageData))
                        return 0;

                    return kitUsageData.TotalUses;
                }

                internal void SetKitUses(int id, int amount)
                {
                    if (!_usageData.TryGetValue(id, out KitUsageData kitUsageData))
                        return;

                    kitUsageData.TotalUses = amount;
                }

                internal void OnKitClaimed(Kits kit)
                {
                    if (!_usageData.TryGetValue(kit.ID, out KitUsageData kitUsageData))
                        kitUsageData = _usageData[kit.ID] = new KitUsageData();

                    kitUsageData.OnKitClaimed(kit.Cooldown);
                }

                public class KitUsageData
                {
                    public int TotalUses { get; set; }

                    public double NextUseTime { get; set; }

                    internal void OnKitClaimed(int cooldownSeconds)
                    {
                        TotalUses += 1;
                        NextUseTime = CurrentTime + cooldownSeconds;
                    }
                }
            }
        }

        private void LoadPlayerData() => playerData = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"{Name}/PlayerUsageData");

        private void SavePlayerData() => Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerUsageData", playerData);

        private void OnKitReceived(BasePlayer player, Kits kit)
        {
            playerData[player.userID].OnKitClaimed(kit);

            Interface.CallHook("OnKitRedeemed", player, kit.Name);

        }

        /// <summary>
        /// ---------------------------------------------------------------------------------
        /// </summary>

        #endregion

        #region API

        public object GiveKit(BasePlayer player, string NameOrID)
        {
            if (!player) return null;

            if (string.IsNullOrEmpty(NameOrID))
            {
                Puts("Empty Kit ID.");
                return null;
            }
            Kits kit = FindKit(NameOrID);
            if (kit != null)
            {
                GiveItemsTo(player, kit);               
                return true;
            }
            Puts($"Kit {NameOrID} not found.");
            return null;
        }

        public string GetKitName(string IDorName)
        {
            Kits kit = FindKit(IDorName);

            if (kit != null)
            {
                return kit.Name;
            }
            return string.Empty;
        }

        public string GetKitDescription(string IDorName)
        {
            Kits kit = FindKit(IDorName);

            if (kit != null)
            {
                return kit.Description;
            }
            return string.Empty;
        }

        public int GetKitMaxUses(string IDorName)
        {
            Kits kit = FindKit(IDorName);

            if (kit != null)
            {
                return kit.UsesLimit;
            }
            return 0;
        }

        public int GetKitCooldown(string IDorName)
        {
            Kits kit = FindKit(IDorName);

            if (kit != null)
            {
                return kit.Cooldown;
            }
            return 0;
        }

        public int GetPlayerKitUses(ulong userID, string IDorName)
        {
            Kits kit = FindKit(IDorName);

            if (kit != null && playerData.Exists(userID))
            {
                return playerData[userID].GetKitUses(kit.ID);
            }
            return 0;
        }

        public double GetPlayerKitCooldown(ulong userID, string IDorName)
        {
            Kits kit = FindKit(IDorName);

            if (kit != null && playerData.Exists(userID))
            {
                return playerData[userID].GetCooldownRemaining(kit.ID);
            }
            return 0;
        }

        public IEnumerable<Item> CreateKitItems(string IDorName)
        {
            Kits kit = FindKit(IDorName);
            if (kit != null)
            {
                return GetKitItems(kit.ID.ToString());
            }
            return null;
        }

        #endregion

    }
}
