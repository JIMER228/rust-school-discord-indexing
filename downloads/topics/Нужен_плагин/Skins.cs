using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Oxide.Core.Libraries;
using Cui = UnityEngine.UI;

namespace Oxide.Plugins
{
    [Info("Skins", "jazPeer", "0.0.1")]
    [Description("Advanced skin changer with CUI, icons, permissions and API.")]
    public class Skins : RustPlugin
    {
        #region Configuration
        private Configuration config;

        public class Configuration
        {
            public List<ConfigSkinItem> Skins { get; set; } = new List<ConfigSkinItem>
            {
                new ConfigSkinItem
                {
                    ItemShortname = "pickaxe",
                    Permission = "",
                    Skins = new List<SkinData>
                    {
                        new SkinData { Id = 0 },
                        new SkinData { Id = 1234567890, Icon = "https://i.imgur.com/abc123.png" }
                    }
                }
            };

            public List<string> Commands = new List<string> { "skin", "skins" };
            public int ContainerCapacity = 36;
            public float CooldownSeconds = 2f;

            public CuiConfig UI = new CuiConfig();
        }

        public class ConfigSkinItem
        {
            public string ItemShortname { get; set; }
            public string Permission { get; set; } = "";
            public List<SkinData> Skins { get; set; } = new List<SkinData>();
        }

        public class SkinData
        {
            public ulong Id { get; set; }
            public string Icon { get; set; } // URL или workshop/123456789
        }

        public class CuiConfig
        {
            public string BackgroundColor = "0.18 0.28 0.36 0.95";
            public string PanelName = "skins_panel";
            public Vector2 Size = new Vector2(400, 500);
            public Vector2 Offset = new Vector2(-200, -250);

            public ButtonConfig LeftButton = new ButtonConfig
            {
                Text = "<size=36><</size>",
                Color = "0.11 0.51 0.83 1",
                AnchorMin = "0.05 0.02",
                AnchorMax = "0.3 0.12"
            };

            public ButtonConfig CenterButton = new ButtonConfig
            {
                Text = "<size=30>Page: {page}/{max}</size>",
                Color = "0.11 0.51 0.83 1",
                AnchorMin = "0.35 0.02",
                AnchorMax = "0.65 0.12"
            };

            public ButtonConfig RightButton = new ButtonConfig
            {
                Text = "<size=36>></size>",
                Color = "0.11 0.51 0.83 1",
                AnchorMin = "0.7 0.02",
                AnchorMax = "0.95 0.12"
            };

            public GridConfig SkinGrid = new GridConfig
            {
                AnchorMin = "0.05 0.15",
                AnchorMax = "0.95 0.95",
                CellWidth = 80,
                CellHeight = 80,
                ChildAlignment = 4
            };
        }

        public class ButtonConfig
        {
            public string Text;
            public string Color;
            public string AnchorMin;
            public string AnchorMax;
        }

        public class GridConfig
        {
            public string AnchorMin;
            public string AnchorMax;
            public float CellWidth = 80;
            public float CellHeight = 80;
            public int ChildAlignment = 4;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintError("Config corrupted. Resetting...");
                config = new Configuration();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Data & Caching
        private readonly Dictionary<ulong, float> lastUse = new();
        private readonly Dictionary<string, List<SkinData>> skinCache = new();
        private readonly HashSet<ulong> applyingSkins = new();

        private List<SkinData> GetCachedSkins(ItemDefinition itemDef, BasePlayer player)
        {
            var key = $"{itemDef.shortname}_{player.UserIDString}";
            if (skinCache.TryGetValue(key, out var list))
                return list;

            list = new List<SkinData>();

            // Вызываем хук для кастомных скинов
            var hookResult = Interface.Oxide.CallHook("OnSkinsFetch", player, itemDef, list);
            if (hookResult is List<SkinData> custom) list = custom;

            var configItem = config.Skins.FirstOrDefault(x => x.ItemShortname == itemDef.shortname);
            if (configItem != null)
            {
                if (string.IsNullOrEmpty(configItem.Permission) || player.HasPermission(configItem.Permission))
                    list.AddRange(configItem.Skins);
            }

            // Всегда добавляем стандартный скин
            if (!list.Any(x => x.Id == 0))
                list.Insert(0, new SkinData { Id = 0 });

            list = list.DistinctBy(x => x.Id).ToList();

            Interface.Oxide.CallHook("OnSkinsFetched", player, itemDef, list);
            skinCache[key] = list;

            return list;
        }

        private void PurgeCache(string shortname = null)
        {
            if (string.IsNullOrEmpty(shortname))
                skinCache.Clear();
            else
                skinCache.Keys.Where(k => k.StartsWith(shortname + "_")).ToList().ForEach(k => skinCache.Remove(k));
        }
        #endregion

        #region API (for other plugins)
        public bool AddSkin(string shortname, ulong skinId, string icon = null, string permission = null)
        {
            var item = config.Skins.FirstOrDefault(x => x.ItemShortname == shortname);
            if (item == null)
            {
                item = new ConfigSkinItem { ItemShortname = shortname, Permission = permission, Skins = new List<SkinData>() };
                config.Skins.Add(item);
            }

            if (item.Skins.Any(x => x.Id == skinId)) return false;

            item.Skins.Add(new SkinData { Id = skinId, Icon = icon });
            SaveConfig();

            skinCache.Keys.Where(k => k.StartsWith(shortname + "_")).ToList().ForEach(k => skinCache.Remove(k));
            return true;
        }

        public bool RemoveSkin(string shortname, ulong skinId)
        {
            var item = config.Skins.FirstOrDefault(x => x.ItemShortname == shortname);
            if (item == null) return false;

            var skin = item.Skins.FirstOrDefault(x => x.Id == skinId);
            if (skin == null) return false;

            item.Skins.Remove(skin);
            SaveConfig();

            skinCache.Keys.Where(k => k.StartsWith(shortname + "_")).ToList().ForEach(k => skinCache.Remove(k));
            return true;
        }

        public void OpenSkinUI(BasePlayer player) => ShowSkinUI(player);

        public void CloseSkinUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, config.UI.PanelName);
        }
        #endregion

        #region Hooks
        object CanUseSkins(string playerId)
        {
            var player = BasePlayer.FindByID(Convert.ToUInt64(playerId));
            return player != null && player.HasPermission("skins.use") ? null : (object)false;
        }

        void OnItemSkinChanged(BasePlayer player, Item item) => Puts($"{player.displayName} changed {item.info.shortname} to skin {item.skin}");

        void OnSkinsFetch(BasePlayer player, ItemDefinition info, List<SkinData> skins) { }
        void OnSkinsFetched(BasePlayer player, ItemDefinition info, List<SkinData> skins) { }
        void OnSkinsPage(BasePlayer player, ItemDefinition info, List<SkinData> skins, int page, int maxPage) { }
        #endregion

        #region Commands
        private void Init()
        {
            foreach (var cmd in config.Commands)
                AddCovalenceCommand(cmd, nameof(SkinCommand));
        }

        private void SkinCommand(IPlayer iPlayer, string cmd, string[] args)
        {
            var player = iPlayer.Object as BasePlayer;
            if (player == null) return;

            if (!player.HasPermission("skins.use"))
            {
                SendReply(player, Lang("Not Allowed", player.UserIDString));
                return;
            }

            if (Time.realtimeSinceStartup - lastUse.GetValueOrDefault(player.userID, 0) < config.CooldownSeconds)
            {
                SendReply(player, "Please wait before opening again.");
                return;
            }

            if (args.Length == 0)
            {
                ShowHelp(player);
                return;
            }

            var arg = args[0].ToLower();

            switch (arg)
            {
                case "show":
                    ShowSkinUI(player);
                    break;
                case "get":
                    GetSkinId(player);
                    break;
                case "purgecache":
                    if (player.HasPermission("skins.admin"))
                    {
                        var shortname = args.Length > 1 ? args[1] : null;
                        PurgeCache(shortname);
                        SendReply(player, "Cache purged.");
                    }
                    break;
                case "add":
                    if (player.HasPermission("skins.admin") && args.Length >= 3)
                    {
                        var shortname = args[1];
                        if (ulong.TryParse(args[2], out var skinId))
                        {
                            var icon = args.Length > 3 ? string.Join(" ", args.Skip(3)) : null;
                            var result = AddSkin(shortname, skinId, icon);
                            SendReply(player, result ? Lang("Skin Added") : Lang("Skin Already Exists"));
                        }
                    }
                    break;
                case "remove":
                    if (player.HasPermission("skins.admin") && args.Length >= 3)
                    {
                        var shortname = args[1];
                        if (ulong.TryParse(args[2], out var skinId))
                        {
                            var result = RemoveSkin(shortname, skinId);
                            SendReply(player, result ? Lang("Skin Removed") : Lang("Skin Does Not Exist"));
                        }
                    }
                    break;
                default:
                    ShowHelp(player);
                    break;
            }

            lastUse[player.userID] = Time.realtimeSinceStartup;
        }

        private void ShowHelp(BasePlayer player)
        {
            SendReply(player, Lang("Help"));
            if (player.HasPermission("skins.admin"))
                SendReply(player, Lang("Admin Help"));
        }

        private void GetSkinId(BasePlayer player)
        {
            var held = player.GetHeldItem();
            if (held == null)
            {
                SendReply(player, Lang("Skin Get No Item"));
                return;
            }
            SendReply(player, string.Format(Lang("Skin Get Format"), held.info.shortname, held.skin));
        }
        #endregion

        #region UI System
        private void ShowSkinUI(BasePlayer player)
        {
            var held = player.GetHeldItem();
            if (held == null)
            {
                SendReply(player, Lang("Skin Get No Item"));
                return;
            }

            if (!held.info.HasFlag(ItemFlags.CanChangeSkin))
            {
                SendReply(player, "This item doesn't support skins.");
                return;
            }

            var skins = GetCachedSkins(held.info, player);
            if (skins.Count == 0)
            {
                SendReply(player, "No skins available.");
                return;
            }

            var page = player.Connection.lastFrame.Ticks % 1000; // временно, можно хранить в словаре
            var perPage = (int)((config.UI.SkinGrid.AnchorMax.Y - config.UI.SkinGrid.AnchorMin.Y) * Screen.height / config.UI.SkinGrid.CellHeight) * 4;
            var maxPage = Math.Max(0, (skins.Count - 1) / perPage);

            page = Mathf.Clamp(page, 0, maxPage);

            var container = new CuiElementContainer();
            var panel = config.UI.PanelName;

            // Background
            container.Add(new CuiPanel
            {
                Image = { Color = config.UI.BackgroundColor },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = config.UI.Offset, OffsetMax = config.UI.Offset + config.UI.Size }
            }, "Hud", panel);

            // Grid
            container.Add(new CuiElement
            {
                Name = $"{panel}.grid",
                Parent = panel,
                Components =
                {
                    new CuiRawImageComponent(),
                    new CuiRectTransformComponent { AnchorMin = config.UI.SkinGrid.AnchorMin, AnchorMax = config.UI.SkinGrid.AnchorMax }
                }
            });

            // Add skins
            var start = page * perPage;
            var end = Math.Min(start + perPage, skins.Count);
            for (int i = start; i < end; i++)
            {
                var skin = skins[i];
                var btnName = $"{panel}.skin.{i}";

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2 0.2 0.2 1", Command = $"skin.apply {held.skin} {skin.Id}" },
                    RectTransform = { AnchorMin = $"{(i - start) % 4 * 0.24 + 0.02} {1f - (((i - start) / 4 + 1) * 0.24)}", AnchorMax = $"{(i - start) % 4 * 0.24 + 0.22} {1f - ((i - start) / 4 * 0.24)}" },
                    Text = { Text = "", FontSize = 12 }
                }, $"{panel}.grid", btnName);

                if (!string.IsNullOrEmpty(skin.Icon))
                {
                    container.Add(new CuiElement
                    {
                        Parent = btnName,
                        Components =
                        {
                            new CuiRawImageComponent { Png = skin.Icon.StartsWith("workshop/") ? null : skin.Icon, Url = skin.Icon },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });
                }
            }

            // Buttons
            AddButton(container, panel, config.UI.LeftButton, $"skin.page {held.skin} {page - 1}");
            AddButton(container, panel, config.UI.CenterButton, $"skin.page {held.skin} {page}", config.UI.CenterButton.Text.Replace("{page}", (page + 1).ToString()).Replace("{max}", (maxPage + 1).ToString()));
            AddButton(container, panel, config.UI.RightButton, $"skin.page {held.skin} {page + 1}");

            CuiHelper.DestroyUi(player, panel);
            CuiHelper.AddUi(player, container);

            Interface.Oxide.CallHook("OnSkinsPage", player, held.info, skins, page, maxPage);
        }

        private void AddButton(CuiElementContainer container, string panel, ButtonConfig btn, string command, string text = null)
        {
            container.Add(new CuiButton
            {
                Button = { Color = btn.Color, Command = command },
                RectTransform = { AnchorMin = btn.AnchorMin, AnchorMax = btn.AnchorMax },
                Text = { Text = text ?? btn.Text, FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel, $"{panel}.{btn.Text}");
        }

        [ConsoleCommand("skin.apply")]
        private void CmdApply(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || applyingSkins.Contains(player.userID)) return;

            if (!arg.HasArgs(2) || !ulong.TryParse(arg.Args[1], out var skinId)) return;

            var held = player.GetHeldItem();
            if (held == null || held.skin == skinId) return;

            held.skin = skinId;
            held.MarkDirty();
            player.Command("inventory.update");

            applyingSkins.Add(player.userID);
            timer.Once(0.5f, () => applyingSkins.Remove(player.userID));

            Interface.Oxide.CallHook("OnItemSkinChanged", player, held);
            CuiHelper.DestroyUi(player, config.UI.PanelName);
        }

        [ConsoleCommand("skin.page")]
        private void CmdPage(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var page)) return;
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            ShowSkinUI(player);
        }
        #endregion

        #region Language
        private string Lang(string key, string id = null, params object[] args) =>
            string.Format(lang.GetMessage(key, this, id), args);

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Not Allowed"] = "You don't have permission to use this command.",
                ["Cannot Use"] = "I'm sorry, you cannot use that right now.",
                ["Help"] = "Command usage:\nskin show - Open skin selector.\nskin get - Get current skin ID.",
                ["Admin Help"] = "Admin:\nskin add <item> <id> [icon]\nskin remove <item> <id>",
                ["Skin Get Format"] = "{0}'s skin: {1}.",
                ["Skin Get No Item"] = "Hold an item first.",
                ["Skin Already Exists"] = "Skin already exists.",
                ["Skin Does Not Exist"] = "Skin not found.",
                ["Skin Added"] = "Skin added.",
                ["Skin Removed"] = "Skin removed."
            }, this);
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            LoadDefaultMessages();
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, config.UI.PanelName);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            skinCache.Keys.Where(k => k.Contains(player.UserIDString.ToString())).ToList().ForEach(k => skinCache.Remove(k));
        }
        #endregion
    }
}