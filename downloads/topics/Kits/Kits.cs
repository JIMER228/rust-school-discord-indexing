using System.Data.SqlTypes;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using System.Collections;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Kits", "wyir", "1.2.4")]
    class Kits : RustPlugin
    {
        private PluginConfig config;
        private List<Kit> kitsList;
        private Dictionary<ulong, Dictionary<string, KitData>> PlayersData;
        private Dictionary<BasePlayer, List<Kit>> OpenGUI = new Dictionary<BasePlayer, List<Kit>>();
        
        class PluginConfig
        {
            [JsonProperty("Кастомные автокиты по привилегии (Привилегию устанавливаете в настройке кита) | Custom autokit, install privilege in the configuration of the kit")] public List<string> CustomAutoKits;
            [JsonProperty("Префикс чата | Chat Prefix")]
            public string DefaultPrefix
            {
                get;
                set;
            }
            [JsonProperty("Версия конфигурации | Configuration Version")] public VersionNumber PluginVersion = new VersionNumber();
            public static PluginConfig CreateDefault()
            {
                return new PluginConfig
                {
                    CustomAutoKits = new List<string>() {
                        "autokit1", "autokit2"
                    },
                    DefaultPrefix = "[Kit]",
                    PluginVersion = new VersionNumber()
                };
            }
        }
        
        public class Kit
        {
            public string Name
            {
                get;
                set;
            }
            public string DisplayName
            {
                get;
                set;
            }
            public int Amount
            {
                get;
                set;
            }
            public double Cooldown
            {
                get;
                set;
            }
            public bool Hide
            {
                get;
                set;
            }
            public string Permission
            {
                get;
                set;
            }
            public string Color
            {
                get;
                set;
            }
            public List<KitItem> Items
            {
                get;
                set;
            }
        }
        public class KitItem
        {
            public string ShortName
            {
                get;
                set;
            }
            public int Amount
            {
                get;
                set;
            }
            public int Blueprint
            {
                get;
                set;
            }
            public ulong SkinID
            {
                get;
                set;
            }
            public string Container
            {
                get;
                set;
            }
            public float Condition
            {
                get;
                set;
            }
            public int Change
            {
                get;
                set;
            }
          
            public bool EnableCommand { get; set; }
            [JsonProperty("Command (Player identifier %STEAMID%)")]
            public string Command { get; set; }
            public string CustomImage { get; set; }
            public Weapon Weapon
            {
                get;
                set;
            }
            public List<ItemContent> Content
            {
                get;
                set;
            }
        }
        public class Weapon
        {
            public string ammoType
            {
                get;
                set;
            }
            public int ammoAmount
            {
                get;
                set;
            }
        }
        public class ItemContent
        {
            public string ShortName
            {
                get;
                set;
            }
            public float Condition
            {
                get;
                set;
            }
            public int Amount
            {
                get;
                set;
            }
        }
        public class KitData
        {
            public int Amount
            {
                get;
                set;
            }
            public double Cooldown
            {
                get;
                set;
            }
        }
        public class Position
        {
            public string AnchorMin
            {
                get;
                set;
            }
            public string AnchorMax
            {
                get;
                set;
            }
        }
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(PluginConfig.CreateDefault(), true);
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            if (config.PluginVersion < Version) UpdateConfigValues();
            Config.WriteObject(config, true);
        }
        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.CreateDefault();
            if (config.PluginVersion < Version)
            {
                //
            }
            config.PluginVersion = Version;
        }
        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            if (kitsList.Exists(x => x.Name.ToLower() == "autokit"))
            {
                player.inventory.Strip();
                var kit = kitsList.First(x => x.Name.ToLower() == "autokit");
                GiveItems(player, kit);
            }
        }
        private void SaveKits()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Kits/KitsList", kitsList);
        }
        private void SaveData()
        {
            if (PlayersData != null) Interface.Oxide.DataFileSystem.WriteObject("Kits/PlayersData", PlayersData);
        }
        void OnServerSave()
        {
            SaveData();
            SaveKits();
        }
        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Kit Was Removed"] = "{Prefix}: Kit {kitname} was removed",
                ["Kit Doesn't Exist"] = "{Prefix}: This kit doesn't exist",
                ["Not Found Player"] = "{Prefix}: Player not found",
                ["To Many Player"] = "{Prefix}: Found multipy players",
                ["Permission Denied"] = "{Prefix}: Access denied",
                ["Limite Denied"] = "{Prefix}: Useage limite reached",
                ["Cooldown Denied"] = "{Prefix}: You will be able to use this kit after {time}",
                ["Reset"] = "{Prefix}Kits data wiped",
                ["Kit Already Exist"] = "{Prefix}Kit with the same name already exist",
                ["Kit Created"] = "{Prefix}You have created a new kit - {name}",
                ["Kit Extradited"] = "{Prefix}You have claimed kit - {kitname}",
                ["Kit Cloned"] = "{Prefix}You inventory was copyed to the kit",
                ["UI Amount"] = "<b>Timeleft: {amount}</b>",
                ["UI COOLDOWN"] = "Cooldown: {cooldown}",
                ["UI EXIT"] = "<b>EXIT</b>",
                ["UI READ"] = "<b>READ MORE</b>",
                ["UI NOLIMIT"] = "<b>no limit</b>",
                ["UI LIMIT"] = "<b>Limit: {limit}</b>",
                ["UI NOGIVE"] = "<b>NOT AVAILABLE</b>",
                ["UI GIVE"] = "YOU CAN TAKE",
                ["Help"] = "/kit name|add|clone|remove|list|reset",
                ["Help Add"] = "/kit add <kitname>",
                ["Help Clone"] = "/kit clone <kitname>",
                ["Help Remove"] = "/kit remove <kitname>",
                ["Help Give"] = "/kit give <kitname> <playerName|steamID>",
                ["Give Succes"] = "You have successfully given the player {0} a set {1}",
                ["No Space"] = "Can't redeem kit. Not enought space",
                ["UI Item Info"] = "If you see a percentage on an item, it means that with the indicated probability you can get this one.",
                ["UI Admin ON"] = "DISPLAY ALL KITS",
                ["UI Admin OFF"] = "HIDE ALL KITS",
                ["UI No Available"] = "There are no available ktis for you.",
            }
            , this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Kit Was Removed"] = "{Prefix}{kitname} был удалён",
                ["Kit Doesn't Exist"] = "{Prefix}Этого комплекта не существует",
                ["Not Found Player"] = "{Prefix}Игрок не найден",
                ["To Many Player"] = "{Prefix}Найдено несколько игроков",
                ["Permission Denied"] = "{Prefix}У вас нет полномочий использовать этот комплект",
                ["Limite Denied"] = "{Prefix}Вы уже использовали этот комплект максимальное количество раз",
                ["Cooldown Denied"] = "{Prefix}Вы сможете использовать этот комплект через {time}",
                ["Reset"] = "{Prefix}Вы обнулили все данные о использовании комплектов игроков",
                ["Kit Already Exist"] = "{Prefix}Этот набор уже существует",
                ["Kit Created"] = "{Prefix}Вы создали новый набор - {name}",
                ["Kit Extradited"] = "{Prefix}Вы получили комплект {kitname}",
                ["Kit Cloned"] = "{Prefix}Предметы были скопированы из инвентаря в набор",
                ["UI Amount"] = "<b>Осталось: {amount}</b>",
                ["UI READ"] = "ПОДРОБНЕЕ",
                ["UI EXIT"] = "<b>ВЫХОД</b>",
                ["UI NOLIMIT"] = "<b>неогр.</b>",
                ["UI LIMIT"] = "<b>Лимит: {limit}</b>",
                ["UI COOLDOWN"] = "ЗАДЕРЖКА: {cooldown}",
                ["UI NOGIVE"] = "<b>НЕ ДОСТУПНО</b>",
                ["UI GIVE"] = "МОЖНО БРАТЬ",
                ["Help"] = "/kit name|add|clone|remove|list|reset",
                ["Help Add"] = "/kit add <kitname>",
                ["Help Clone"] = "/kit clone <kitname>",
                ["Help Remove"] = "/kit remove <kitname>",
                ["Help Give"] = "/kit give <kitname> <playerName|steamID>",
                ["Give Succes"] = "Вы успешно выдали игрок {0} набор {1}",
                ["No Space"] = "Невозможно получить набор - недостаточно места в инвентаре",
                ["UI Item Info"] = "Если вы видете на предмете процент, это значит что с указанной вероятностью вы сможете получить его.",
                ["UI Admin ON"] = "ОТОБРАЗИТЬ ВСЕ НАБОРЫ",
                ["UI Admin OFF"] = "СПРЯТАТЬ ВСЕ НАБОРЫ",
                ["UI No Available"] = "Для Вас нету доступных наборов.",
            }
            , this, "ru");
        }
        private void Loaded()
        {
            config = Config.ReadObject<PluginConfig>();
            LoadData();
            LoadMessages();
        }
        void LoadData()
        {
            try
            {
                kitsList = Interface.Oxide.DataFileSystem.ReadObject<List<Kit>>("Kits/KitsList");
                PlayersData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, KitData>>>("Kits/PlayersData");
            }
            catch
            {
                kitsList = new List<Kit>();
                PlayersData = new Dictionary<ulong, Dictionary<string, KitData>>();
            }
        }
        private void Unload()
        {
            SaveData();
            foreach (var plobj in BasePlayer.activePlayerList)
            {
                DestroyUI(plobj);
            }
        }
        
        private void OnServerInitialized()
        {
            timer.Every(1, UpdateController);

            foreach (var kit in kitsList)
            {
                if (!permission.PermissionExists(kit.Permission)) 
                    permission.RegisterPermission(kit.Permission, this);
            }
        }
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            OpenGUI.Remove(player);
        }
        [ConsoleCommand("kits")]
        private void CommandConsoleKit(ConsoleSystem.Arg args)
        {
			BasePlayer player = args?.Player();
			if (player == null || !OpenGUI.ContainsKey(player) || !args.HasArgs()) return;

            String value = args.Args[0].ToLower();

            Int32 page;
            if (!Int32.TryParse(args.Args[1], out page) || !GiveKit(player, value, page)) return;

            Kit kit = kitsList.First(x => x.Name.ToLower() == value.ToLower());
            if (kit == null) return;

            CuiElementContainer container = new CuiElementContainer();
            KitUi(player, ref container, kit);
            CuiHelper.AddUi(player, container);

            OpenGUI[player].Add(kit);
        }
        [ChatCommand("kit")]
        private void CommandChatKit(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args.Length == 0)
            {
                TriggerUI(player, 0);
                return;
            }
            if (!player.IsAdmin)
            {
                GiveKit(player, args[0].ToLower(), 0);
                return;
            }
            switch (args[0].ToLower())
            {
                case "help":
                    SendReply(player, GetMsg("Help", player));
                    return;
                case "add":
                    if (args.Length < 2) SendReply(player, GetMsg("Help Add", player));
                    else KitCommandAdd(player, args[1].ToLower());
                    return;
                case "clone":
                    if (args.Length < 2) SendReply(player, GetMsg("Help Clone", player));
                    else KitCommandClone(player, args[1].ToLower());
                    return;
                case "remove":
                    if (args.Length < 2) SendReply(player, GetMsg("Help Remove", player));
                    else KitCommandRemove(player, args[1].ToLower());
                    return;
                case "list":
                    KitCommandList(player);
                    return;
                case "reset":
                    KitCommandReset(player);
                    return;
                case "give":
                    if (args.Length < 3)
                    {
                        SendReply(player, GetMsg("Help Give", player));
                    }
                    else
                    {
                        var foundPlayer = FindPlayer(player, args[1].ToLower());
                        if (foundPlayer == null) return;
                        SendReply(player, GetMsg("Give Succes", player), foundPlayer.displayName, args[2]);
                        KitCommandGive(player, foundPlayer, args[2].ToLower());
                    }
                    return;
                default:
                    GiveKit(player, args[0].ToLower(), 0);
                    return;
            }
        }
        
        string GetMsg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player == null ? null : player.UserIDString).Replace("{Prefix}", config.DefaultPrefix);

        private bool GiveKit(BasePlayer player, string kitname, int page = -1, bool admin = false)
        {
            if (string.IsNullOrEmpty(kitname)) return false;
            if (Interface.Oxide.CallHook("canRedeemKit", player) != null && page > -1) return false;
            if (!kitsList.Exists(x => x.Name.ToLower() == kitname.ToLower()))
            {
                SendReply(player, GetMsg("Kit Doesn't Exist", player));
                return false;
            }
            var kit = kitsList.First(x => x.Name.ToLower() == kitname.ToLower());
            if (!string.IsNullOrEmpty(kit.Permission) && !permission.UserHasPermission(player.UserIDString, kit.Permission) && !admin && page > -1)
            {
                SendReply(player, GetMsg("Permission Denied", player));
                return false;
            }
            var playerData = GetPlayerData(kitname, player.userID);
            if (kit.Amount > 0 && playerData.Amount >= kit.Amount && !admin && page > -1)
            {
                SendReply(player, GetMsg("Limite Denied", player));
                return false;
            }
            if (kit.Cooldown > 0 && !admin && page > -1)
            {
                var currentTime = GetCurrentTime();
                if (playerData.Cooldown > currentTime)
                {
                    SendReply(player, GetMsg("Cooldown Denied", player).Replace("{time}", TimeExtensions.FormatTime(TimeSpan.FromSeconds(playerData.Cooldown - currentTime))));
                    return false;
                }
            }

            int beltcount = kit.Items.Where(i => i.Container == "belt").Count();
            int wearcount = kit.Items.Where(i => i.Container == "wear").Count();
            int maincount = kit.Items.Where(i => i.Container == "main").Count();
            int totalcount = beltcount + wearcount + maincount;
            if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount) if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
            {
                player.ChatMessage(GetMsg("No Space", player));
                return false;
            }

            GiveItems(player, kit);
            if (page > -1)
            {
                if (kit.Amount > 0)
                {
                    playerData.Amount += 1;
                }
                if (kit.Cooldown > 0) playerData.Cooldown = GetCurrentTime() + kit.Cooldown;
            }
            return true;
        }

        private void KitCommandAdd(BasePlayer player, string kitname)
        {
            if (kitsList.Exists(x => x.Name == kitname))
            {
                SendReply(player, GetMsg("Kit Already Exist", player));
                return;
            }
            
            kitsList.Add(new Kit
            {
                Name = kitname,
                DisplayName = kitname,
                Cooldown = 600,
                Hide = true,
                Permission = "kits.default",
                Amount = 0,
                Color = "1.00 0.65 0.00 1.00",
                Items = GetPlayerItems(player)
            });

            if (permission.PermissionExists("kits.default")) permission.RegisterPermission($"kits.default", this);
            SendReply(player, GetMsg("Kit Created", player).Replace("{name}", kitname));
            SaveKits();
            SaveData();
        }

        private void KitCommandClone(BasePlayer player, string kitname)
        {
            if (!kitsList.Exists(x => x.Name == kitname))
            {
                SendReply(player, GetMsg("Kit Doesn't Exist", player));
                return;
            }

            kitsList.First(x => x.Name.ToLower() == kitname.ToLower()).Items = GetPlayerItems(player);
            SendReply(player, GetMsg("Kit Cloned", player).Replace("{name}", kitname));
            SaveKits();
        }

        private void KitCommandRemove(BasePlayer player, string kitname)
        {
            if (kitsList.RemoveAll(x => x.Name == kitname) <= 0)
            {
                SendReply(player, GetMsg("Kit Doesn't Exist", player));
                return;
            }

            SendReply(player, GetMsg("Kit Was Removed", player).Replace("{kitname}", kitname));
            SaveKits();
        }

        private void KitCommandList(BasePlayer player)
        {
            foreach (var kit in kitsList) SendReply(player, $"{kit.Name} - {kit.DisplayName}");
        }

        private void KitCommandReset(BasePlayer player)
        {
            PlayersData.Clear();
            SendReply(player, GetMsg("Reset", player));
        }

        private void KitCommandGive(BasePlayer player, BasePlayer foundPlayer, string kitname)
        {
            var reply = 1;
            if (reply == 0) { }
            if (!kitsList.Exists(x => x.Name == reply.ToString())) { }
            if (!kitsList.Exists(x => x.Name == kitname))
            {
                SendReply(player, GetMsg("Kit Doesn't Exist", player));
                return;
            }
            GiveItems(foundPlayer, kitsList.First(x => x.Name.ToLower() == kitname.ToLower()));
        }

        private void GiveItems(BasePlayer player, Kit kit)
        {
            foreach (var kitem in kit.Items)
            {
                if (kitem.EnableCommand && !string.IsNullOrEmpty(kitem.Command))
                {
                    Server.Command(kitem.Command.Replace("%STEAMID%", player.UserIDString));
                    continue;
                }
                GiveItem(player, BuildItem(kitem.ShortName, kitem.Amount, kitem.SkinID, kitem.Condition, kitem.Blueprint, kitem.Weapon, kitem.Content), kitem.Change, kitem.Container == "belt" ? player.inventory.containerBelt : kitem.Container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
            }
        }

        private void GiveItem(BasePlayer player, Item item, int percent, ItemContainer cont = null)
        {
            if (item == null) return;
            var inv = player.inventory;
            if (UnityEngine.Random.Range(1, 100) < percent)
            {
                var moved = item.MoveToContainer(cont) || item.MoveToContainer(inv.containerMain);
                if (!moved)
                {
                    if (cont == inv.containerBelt) moved = item.MoveToContainer(inv.containerWear);
                    if (cont == inv.containerWear) moved = item.MoveToContainer(inv.containerBelt);
                }
                if (!moved) item.Drop(player.GetCenter(), player.GetDropVelocity());
            }
        }

        private Item BuildItem(string ShortName, int Amount, ulong SkinID, float Condition, int blueprintTarget, Weapon weapon, List<ItemContent> Content)
        {
            Item item = ItemManager.CreateByName(ShortName, Amount > 1 ? Amount : 1, SkinID);
            item.condition = Condition;
            if (blueprintTarget != 0) item.blueprintTarget = blueprintTarget;
            if (weapon != null)
            {
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = weapon.ammoAmount;
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType = ItemManager.FindItemDefinition(weapon.ammoType);
            }
            if (Content != null)
            {
                foreach (var cont in Content)
                {
                    Item new_cont = ItemManager.CreateByName(cont.ShortName, cont.Amount);
                    new_cont.condition = cont.Condition;
                    new_cont.MoveToContainer(item.contents);
                }
            }
            return item;
        }

        private void TriggerUI(BasePlayer player, int page, bool take = false)
        {
            #region [Vars]
            if (OpenGUI.ContainsKey(player)) return;
            OpenGUI[player] = new List<Kit>();

            CuiElementContainer container = new CuiElementContainer();
            List<Kit> kits = GetKitsForPlayer(player);

            if (kits.Count == 0)
            {
                OpenGUI.Remove(player);
                player.ChatMessage("Для вас нет доступных китов.");
                return;
            }

            Single width = 184.25f, margin = 15f;
            Int32 kitLine = kits.Count > 6 ? 6 : kits.Count, i = 0, y = 0;
            Single switchs = -(width * kitLine + (kitLine - 1) * margin) / 2f;
            Int32 howLine = (Int32)Math.Ceiling(kits.Count / 6f);
            Single yMax = 28f + (howLine * 15.35f), yMin = -27.5f + (howLine * 15.35f);
            #endregion

            #region [Main]
            container.Add(new CuiPanel
            {
                Image = { Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", Color = "0 0 0 0" },
                RectTransform = { AnchorMin= "0 0", AnchorMax= "1 1" },
                CursorEnabled = true
            }, "Overlay", "ui.kits");

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = "CMD_REMOVE_UI" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, $"ui.kits", $"close_btn");
            #endregion

            #region [Panel]
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0.6" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-1300 {yMin + (-72 * (howLine - 1)) - 6}", OffsetMax = $"1300 {yMax + 6}" }
            }, "ui.kits", "panel");
            #endregion

            #region [Kits]
            foreach (var kit in kits.Select((u, t) => new { A = u, B = t }))
            {
                OpenGUI[player].Add(kit.A);

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{switchs} {yMin}", OffsetMax = $"{switchs + width} {yMax}" },
                    Image = { Color = "0.4056604 0.4056604 0.4056604 0.5372549", Material = "assets/icons/greyout.mat" }
                }, $"ui.kits", $"ui.kits" + $"Kit({kit.A.Name})");

                KitUi(player, ref container, kit.A);

                switchs += width + margin;
                i++;
                if (i == 6)
                {
                    y++;
                    i = 0;

					kitLine = kits.Count - (6 * y);
					if (kitLine > 6)
					    kitLine = 6;

                    switchs = -(width * kitLine + (kitLine - 1) * margin) / 2f;
                    yMax += -72f;
                    yMin += -72f;
                }
            }
            #endregion

            CuiHelper.DestroyUi(player, $"ui.kits");
            CuiHelper.AddUi(player, container);
        }

		private void KitUi(BasePlayer player, ref CuiElementContainer container, Kit kit)
		{
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0" }
            }, $"ui.kits" + $"Kit({kit.Name})", "Layer" + $"Kit({kit.Name})");

            var playerData = GetPlayerData(kit.Name, player.userID);
            var time = TimeSpan.FromSeconds(playerData.Cooldown - GetCurrentTime());
            var max = 1 - ((time.TotalSeconds + (float)kit.Cooldown / 60) / kit.Cooldown);

            if (playerData.Cooldown - 1 > GetCurrentTime())
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "1.00 0.65 0.00 1.00", Material = "assets/icons/greyout.mat" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"{max} 0.97" }
                }, "Layer" + $"Kit({kit.Name})", $"CoolDownBar_{kit.Name}");

                container.Add(new CuiElement
                {
                    Parent = "Layer" + $"Kit({kit.Name})",
                    Name = $"name_{kit.Name}",
                    Components = {
                        new CuiTextComponent { Color="1 1 1 1", Text= $"<b>{kit.DisplayName.ToUpper()}</b>", FontSize=22, Font="robotocondensed-regular.ttf", Align=TextAnchor.MiddleCenter, },  
                        new CuiRectTransformComponent { AnchorMin=$"0 0.2", AnchorMax=$"1 1" }, 
                        new CuiOutlineComponent { Color="0 0 0 1", Distance="0.15 0.15" }
                    },
                });

                container.Add(new CuiElement
                {
                    Parent = "Layer" + $"Kit({kit.Name})",
                    Name = $"text_{kit.Name}",
                    Components = {
                        new CuiTextComponent { Color="1 1 1 0.5", Text= $"<b>{TimeExtensions.FormatShortTime(time)}</b>", FontSize=12, Font="robotocondensed-regular.ttf", Align=TextAnchor.MiddleCenter, }, 
                        new CuiRectTransformComponent { AnchorMin=$"0 0", AnchorMax=$"1 0.45" }, 
                        new CuiOutlineComponent { Color="0 0 0 1", Distance="0.15 0.15" }
                    },
                });
            }
            else
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "1.00 0.65 0.00 1.00", Material = "assets/icons/greyout.mat" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"0.99 0.97" }
                }, "Layer" + $"Kit({kit.Name})", $"CoolDownBar_{kit.Name}");

                container.Add(new CuiElement
                {
                    Parent = "Layer" + $"Kit({kit.Name})",
                    Name = $"name_{kit.Name}",
                    Components = { 
                        new CuiTextComponent { Color="1 1 1 1", Text = $"<b>{kit.DisplayName.ToUpper()}</b>", FontSize=22, Font="robotocondensed-regular.ttf", Align=TextAnchor.MiddleCenter, }, 
                        new CuiRectTransformComponent { AnchorMin=$"0 0", AnchorMax=$"1 1" },
                        new CuiOutlineComponent { Color="0 0 0 1", Distance="0.15 0.15" }
                    },
                });
            }

            if (playerData.Cooldown - 1 < GetCurrentTime())
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"kit {kit.Name} 1" },
                    RectTransform = { AnchorMin= "0 0", AnchorMax= "1 1" },
                    Text = { Text = "", Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, FontSize = 18}
                }, "Layer" + $"Kit({kit.Name})", $"btn_{kit.Name}");
            }

			CuiHelper.DestroyUi(player, "Layer" + $"Kit({kit.Name})");
		}

		private void UpdateController()
		{
            if (OpenGUI.Count == 0) return;

			var keysToRemove = Pool.GetList<BasePlayer>();

			foreach (var check in OpenGUI)
			{
				var toRemove = Pool.GetList<Kit>();

				var container = new CuiElementContainer();

				check.Value.ForEach(kit =>
				{
					KitUi(check.Key, ref container, kit);

                    var playerData = GetPlayerData(kit.Name, check.Key.userID);

					if (playerData.Cooldown - 1 < GetCurrentTime())
					    toRemove.Add(kit);
				});

				CuiHelper.AddUi(check.Key, container);

				toRemove.ForEach(kit => check.Value.Remove(kit));
				Pool.FreeList(ref toRemove);
			}

			keysToRemove.ForEach(x => OpenGUI.Remove(x));
			Pool.FreeList(ref keysToRemove);
		}

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, $"ui.kits.info");
            if (!OpenGUI.ContainsKey(player)) return;

            foreach (var kitname in OpenGUI[player])
            {
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}.time");
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}.mask");
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}.button");
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}.amount");
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}");
            }
            
            CuiHelper.DestroyUi(player, "ui.kits");
            OpenGUI.Remove(player);
        }
        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds(time);
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            var mins = dateDifference.Minutes;
            var secs = dateDifference.Seconds;
            if (hours > 0) return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
            else return string.Format("{0:00}:{1:00}", mins, secs);
        }

        [ConsoleCommand("CMD_REMOVE_UI")]
        void cnsCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            CuiHelper.DestroyUi(player, $"ui.kits");
            OpenGUI.Remove(player);
        }

        private KitData GetPlayerData(string name, ulong playerid = 1)
        {
            if (!PlayersData.ContainsKey(playerid)) PlayersData[playerid] = new Dictionary<string, KitData>();
            if (!PlayersData[playerid].ContainsKey(name)) PlayersData[playerid][name] = new KitData();
            return PlayersData[playerid][name];
        }
        private List<KitItem> GetPlayerItems(BasePlayer player)
        {
            List<KitItem> kititems = new List<KitItem>();

            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemToKit(item, "wear");
                    kititems.Add(iteminfo);
                }
            }

            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemToKit(item, "main");
                    kititems.Add(iteminfo);
                }
            }

            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemToKit(item, "belt");
                    kititems.Add(iteminfo);
                }
            }

            return kititems;
        }

        private KitItem ItemToKit(Item item, string container)
        {
            KitItem kitem = new KitItem();
            kitem.Amount = item.amount;
            kitem.Container = container;
            kitem.SkinID = item.skin;
            kitem.Blueprint = item.blueprintTarget;
            kitem.ShortName = item.info.shortname;
            kitem.Condition = item.condition;
            kitem.Change = 100;
            kitem.Weapon = null;
            kitem.Content = null;

            if (item.info.category == ItemCategory.Weapon)
            {
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    kitem.Weapon = new Weapon();
                    kitem.Weapon.ammoType = weapon.primaryMagazine.ammoType.shortname;
                    kitem.Weapon.ammoAmount = weapon.primaryMagazine.contents;
                }
            }

            if (item.contents != null)
            {
                kitem.Content = new List<ItemContent>();
                foreach (var cont in item.contents.itemList)
                {
                    kitem.Content.Add(new ItemContent()
                    {
                        Amount = cont.amount,
                        Condition = cont.condition,
                        ShortName = cont.info.shortname
                    }
                    );
                }
            }

            return kitem;
        }

        private List<Kit> GetKitsForPlayer(BasePlayer player)
        {
            return kitsList.Where(kit => !kit.Hide && (string.IsNullOrEmpty(kit.Permission) || permission.UserHasPermission(player.UserIDString, kit.Permission)) && (kit.Amount == 0 || (kit.Amount > 0 && GetPlayerData(kit.Name, player.userID).Amount < kit.Amount))).ToList();
        }

        private BasePlayer FindPlayer(BasePlayer player, string nameOrID)
        {
            ulong id;
            if (ulong.TryParse(nameOrID, out id) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                var findedPlayer = BasePlayer.FindByID(id);
                if (findedPlayer == null || !findedPlayer.IsConnected)
                {
                    SendReply(player, GetMsg("Not Found Player", player));
                    return null;
                }
                return findedPlayer;
            }
            var foundPlayers = BasePlayer.activePlayerList.Where(x => x.displayName.ToLower().Contains(nameOrID.ToLower()));
            if (foundPlayers.Count() == 0)
            {
                SendReply(player, GetMsg("Not Found Player", player));
                return null;
            }
            if (foundPlayers.Count() > 1)
            {
                SendReply(player, GetMsg("To Many Player", player));
                return null;
            }
            return foundPlayers.First();
        }

        private double GetCurrentTime() => new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;

        private static class TimeExtensions
        {
            public static string FormatShortTime(TimeSpan time)
            {
                string result = string.Empty;
                if (time.Days != 0) result += $"{time.Days} д. ";
                if (time.Hours != 0) result += $"{time.Hours} ч. ";
                if (time.Minutes != 0) result += $"{time.Minutes} м. ";
                if (time.Seconds != 0) result += $"{time.Seconds} с. ";
                return result;
            }
            public static string FormatTime(TimeSpan time)
            {
                string result = string.Empty;
                if (time.Days != 0) result += $"{Format(time.Days, "дней", "дня", "день")} ";
                if (time.Hours != 0) result += $"{Format(time.Hours, "часов", "часа", "час")} ";
                if (time.Minutes != 0) result += $"{Format(time.Minutes, "минут", "минуты", "минута")} ";
                if (time.Seconds != 0) result += $"{Format(time.Seconds, "секунд", "секунды", "секунда")} ";
                return result;
            }
            private static string Format(int units, string form1, string form2, string form3)
            {
                var tmp = units % 15;
                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9) return $"{units} {form1}";
                if (tmp >= 2 && tmp <= 4) return $"{units} {form2}";
                return $"{units} {form3}";
            }
        }
        
        [HookMethod("isKit")]
        public bool isKit(string kitName)
        {
            if (kitsList.Select(p => p.Name == kitName) != null) return true;
            return false;
        }
        
        [HookMethod("GetAllKits")] public string[] GetAllKits() => kitsList.Select(p => p.Name).ToArray();
    }
}                                                                                                                      