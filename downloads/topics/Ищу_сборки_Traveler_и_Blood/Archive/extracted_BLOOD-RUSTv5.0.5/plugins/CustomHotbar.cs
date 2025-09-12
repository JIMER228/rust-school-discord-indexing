using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Configuration;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System;

///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("CustomHotbar", "discord.gg/9vyTXsJyKR", "1.0.4", ResourceId = 31)]

    class CustomHotbar : RustPlugin
    {
        CustomHotbarData cbdata;
        private DynamicConfigFile CBData;

        class CustomHotbarData
        {
            public Dictionary<ulong, Dictionary<int, Dictionary<string, int>>> BeltPreference = new Dictionary<ulong, Dictionary<int, Dictionary<string, int>>>();
            public Dictionary<ulong, Dictionary<int, Dictionary<string, int>>> WearPreference = new Dictionary<ulong, Dictionary<int, Dictionary<string, int>>>();
        }
        public Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();

        public List<ulong> Sorting = new List<ulong>();
        public List<ulong> Saving = new List<ulong>();
        public Dictionary<ulong, int> Enabled = new Dictionary<ulong, int>();
        public bool Debug = false;

        private void GetSendMSG(BasePlayer player, string message, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this, player.UserIDString), arg1, arg2, arg3);
            SendReply(player, "<color=orange>" + lang.GetMessage("title", this, player.UserIDString) + "</color>" + "<color=#A9A9A9>" + msg + "</color>");
        }

        private string GetMSG(string message, BasePlayer player = null, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string p = null;
            if (player != null)
                p = player.UserIDString;
            if (messages.ContainsKey(message))
                return string.Format(lang.GetMessage(message, this, p), arg1, arg2, arg3);
            else return message;
        }

        void Loaded()
        {
            CBData = Interface.Oxide.DataFileSystem.GetFile("CustomHotbar_Data");
            lang.RegisterMessages(messages, this);
        }

        void Unloaded()
        {
            foreach (var entry in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(entry, HotbarPanel);
                if (Enabled.ContainsKey(entry.userID))
                    Enabled.Remove(entry.userID);
            }
            foreach (var entry in timers)
                entry.Value.Destroy();
            timers.Clear();
            Sorting.Clear();
            Enabled.Clear();
            CBData.WriteObject(cbdata);
        }

        void OnPlayerInit(BasePlayer player)
        {
            BindKeys(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            BindKeys(player, true);
            CuiHelper.DestroyUi(player, HotbarPanel);
            if (Enabled.ContainsKey(player.userID))
                Enabled.Remove(player.userID);
            if (timers.ContainsKey(player.userID))
            {
                timers[player.userID].Destroy();
                timers.Remove(player.userID);
            }
        }

        void OnServerInitialized()
        {
            LoadVariables();
            LoadData();
            if (!permission.PermissionExists(this.Name + ".allow"))
                permission.RegisterPermission(this.Name + ".allow", this);
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }

        void LoadData()
        {
            try
            {
                cbdata = CBData.ReadObject<CustomHotbarData>();
                if (cbdata == null) cbdata = new CustomHotbarData();
            }
            catch
            {
                cbdata = new CustomHotbarData();
            }
            if (cbdata.BeltPreference == null) cbdata.BeltPreference = new Dictionary<ulong, Dictionary<int, Dictionary<string, int>>>();
            if (cbdata.WearPreference == null) cbdata.WearPreference = new Dictionary<ulong, Dictionary<int, Dictionary<string, int>>>();
        }

        void BindKeys(BasePlayer player, bool unbind = false)
        {
            if (unbind)
            {
                if (!string.IsNullOrEmpty(configData.Preference1_HotKey))
                    player.Command($"bind {configData.Preference1_HotKey} \"\"");
                if (!string.IsNullOrEmpty(configData.Preference2_HotKey))
                    player.Command($"bind {configData.Preference2_HotKey} \"\"");
                if (!string.IsNullOrEmpty(configData.Preference3_HotKey))
                    player.Command($"bind {configData.Preference3_HotKey} \"\"");
                if (!string.IsNullOrEmpty(configData.Cycle_HotKey))
                    player.Command($"bind {configData.Cycle_HotKey} \"\"");
                return;
            }
            if (!string.IsNullOrEmpty(configData.Preference1_HotKey))
                player.Command($"bind {configData.Preference1_HotKey} \"UI_ToggleHotbar {1}\"");
            if (!string.IsNullOrEmpty(configData.Preference2_HotKey))
                player.Command($"bind {configData.Preference2_HotKey} \"UI_ToggleHotbar {2}\"");
            if (!string.IsNullOrEmpty(configData.Preference3_HotKey))
                player.Command($"bind {configData.Preference3_HotKey} \"UI_ToggleHotbar {3}\"");
            if (!string.IsNullOrEmpty(configData.Cycle_HotKey))
                player.Command($"bind {configData.Cycle_HotKey} \"UI_ToggleHotbar {99}\"");
        }


        private string HotbarPanel = "HotbarPanel";
        private string PanelOnScreen = "PanelOnScreen";


        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent,
                    panelName
                }
            };
                return NewElement;
            }
            static public CuiElementContainer CreateOverlayContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent = "Overlay",
                    panelName
                }
            };
                return NewElement;
            }

            static public CuiElementContainer CreateHudContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent = "HUD",
                    panelName
                }
            };
                return NewElement;
            }

            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            static public void LoadImage(ref CuiElementContainer container, string panel, string img, string aMin, string aMax)
            {
                if (img.Contains("http"))
                {
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Url = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
                }
                else
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Png = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
            }

            static public void CreateTextOverlay(ref CuiElementContainer container, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            static public void CreateTextOutline(ref CuiElementContainer element, string panel, string colorText, string colorOutline, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent{Color = colorText, FontSize = size, Align = align, Text = text },
                        new CuiOutlineComponent {Distance = "1 1", Color = colorOutline},
                        new CuiRectTransformComponent {AnchorMax = aMax, AnchorMin = aMin }
                    }
                });
            }
        }



        void ToggleBeltUI(BasePlayer player)
        {
            if (Enabled.ContainsKey(player.userID))
            {
                Enabled.Remove(player.userID);
                CuiHelper.DestroyUi(player, HotbarPanel);
            }
            else
            {
                Enabled.Add(player.userID, 0);
                HotbarUI(player);
            }
        }


        [ChatCommand("setayar")]
        void cmdhotbar(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, this.Name + ".allow"))
            {
                GetSendMSG(player, "NoPerm");
                return;
            }
            if (args != null && args.Length > 0 && args[0] == "debug")
            {
                if (player.net.connection.authLevel == 2)
                    if (Debug)
                        Debug = false;
                    else
                        Debug = true;
                return;
            }
            ToggleBeltUI(player);
        }


        void HotbarUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, HotbarPanel);
            var element = UI.CreateOverlayContainer(HotbarPanel, "0 0 0 0", $"{configData.minx} {configData.miny}", $"{configData.maxx} {configData.maxy}", false);
            if (!Enabled.ContainsKey(player.userID)) return;
            int index = Enabled[player.userID];
            if(index == 1) UI.CreateButton(ref element, HotbarPanel, "0.4 0.69 1 .15", GetMSG("1", player), 12, "0 0.52", ".32 1", $"UI_ToggleHotbar {1}");
            else UI.CreateButton(ref element, HotbarPanel, "0.7 0.7 0.7 0.15", GetMSG("1",player), 12, "0 0.52", ".32 1", $"UI_ToggleHotbar {1}");
            if (index == 2) UI.CreateButton(ref element, HotbarPanel, "0.4 0.69 1 .15", GetMSG("2", player), 12, ".34 0.52", ".66 1", $"UI_ToggleHotbar {2}");
            else UI.CreateButton(ref element, HotbarPanel, "0.7 0.7 0.7 0.15", GetMSG("2", player), 12, ".34 0.52", ".66 1", $"UI_ToggleHotbar {2}");
            if (index == 3) UI.CreateButton(ref element, HotbarPanel, "0.4 0.69 1 .15", GetMSG("3", player), 12, ".68 0.52", "1 1", $"UI_ToggleHotbar {3}");
            else UI.CreateButton(ref element, HotbarPanel, "0.7 0.7 0.7 0.15", GetMSG("3", player), 12, ".68 0.52", "1 1", $"UI_ToggleHotbar {3}");
            UI.CreateButton(ref element, HotbarPanel, "0.7 0.7 0.7 0.15", GetMSG("Save", player), 12, "0 0", $"1 .48", $"UI_SaveHotbar");
            CuiHelper.AddUi(player, element);
        }


        void OnScreen(BasePlayer player, string msg)
        {
            if(timers.ContainsKey(player.userID))
            {
                timers[player.userID].Destroy();
                timers.Remove(player.userID);
            }
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var element = UI.CreateOverlayContainer(PanelOnScreen, "0.0 0.0 0.0 0.0", "0.3 0.45", "0.7 0.75", false);
            UI.CreateTextOutline(ref element, PanelOnScreen,string.Empty, "0 0 0 1", GetMSG(msg, player), 32, "0.0 0.0", "1.0 1.0");
            CuiHelper.AddUi(player, element);
            timers.Add(player.userID, timer.Once(4, () => CuiHelper.DestroyUi(player, PanelOnScreen)));
        }

        [ConsoleCommand("UI_ToggleHotbar")]
        void cmdUI_ToggleHotbar(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !Enabled.ContainsKey(player.userID)) return;
            if (!permission.UserHasPermission(player.UserIDString, this.Name + ".allow")) { CuiHelper.DestroyUi(player, HotbarPanel); return; }
            int index;
            if (!int.TryParse(arg.Args[0], out index)) return;
            if (Saving.Contains(player.userID))
            {
                if (configData.IncludeWearSlots)
                {
                    if (!cbdata.WearPreference.ContainsKey(player.userID))
                        cbdata.WearPreference.Add(player.userID, new Dictionary<int, Dictionary<string, int>>());
                    if (!cbdata.WearPreference[player.userID].ContainsKey(index))
                        cbdata.WearPreference[player.userID].Add(index, new Dictionary<string, int>());
                    else cbdata.WearPreference[player.userID][index].Clear();
                    foreach (var entry in player.inventory.containerWear.itemList)
                    {
                        if (!cbdata.WearPreference[player.userID][index].ContainsKey(entry.info.shortname))
                            cbdata.WearPreference[player.userID][index].Add(entry.info.shortname, entry.position);
                        else
                        {
                            OnScreen(player, "DuplicateItemsAbort");
                            cbdata.WearPreference[player.userID].Remove(index);
                            Saving.Remove(player.userID);
                            return;
                        }
                    }
                }
                if (!cbdata.BeltPreference.ContainsKey(player.userID))
                    cbdata.BeltPreference.Add(player.userID, new Dictionary<int, Dictionary<string, int>>());
                if (!cbdata.BeltPreference[player.userID].ContainsKey(index))
                    cbdata.BeltPreference[player.userID].Add(index, new Dictionary<string, int>());
                else cbdata.BeltPreference[player.userID][index].Clear();
                foreach (var entry in player.inventory.containerBelt.itemList)
                {
                    if (!cbdata.BeltPreference[player.userID][index].ContainsKey(entry.info.shortname))
                        cbdata.BeltPreference[player.userID][index].Add(entry.info.shortname, entry.position);
                    else
                    {
                        OnScreen(player, "DuplicateItemsAbort");
                        cbdata.BeltPreference[player.userID].Remove(index);
                        Saving.Remove(player.userID);
                        return;
                    }
                }
                OnScreen(player, GetMSG("SavedSuccess", player, index.ToString()));
                CBData.WriteObject(cbdata);
                Saving.Remove(player.userID);
                return;
            }
            if (index == 99)
            {
                index = Enabled[player.userID];
                index++;
            }
            if (index == 4) index = 1;
            if (!cbdata.BeltPreference.ContainsKey(player.userID) && !cbdata.BeltPreference[player.userID].ContainsKey(index))
                if (!configData.IncludeWearSlots || cbdata.BeltPreference.ContainsKey(player.userID) || !cbdata.BeltPreference[player.userID].ContainsKey(index))
                {
                    OnScreen(player, GetMSG("NoProfileError", player, index.ToString()));
                    return;
                }
            if (Enabled[player.userID] == index)
            {
                Enabled[player.userID] = 0;
                OnScreen(player, GetMSG("HotbarDisabled", player, index.ToString()));
            }
            else
            {
                Enabled[player.userID] = index;
                OnScreen(player, GetMSG("HotbarEnabled", player, index.ToString()));
                ReOrg(player);
            }
            HotbarUI(player);
        }

        [ConsoleCommand("UI_SaveHotbar")]
        void cmdUI_SaveHotbar(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, this.Name + ".allow")) { CuiHelper.DestroyUi(player, HotbarPanel); return; }
            if (player.inventory.containerBelt.itemList.Count() == 0)
                if (!configData.IncludeWearSlots || player.inventory.containerWear.itemList.Count() == 0)
                {
                    OnScreen(player, "NoItemsError");
                    return;
                }
            if (!Saving.Contains(player.userID))
                Saving.Add(player.userID);
            OnScreen(player, GetMSG("SelectProfileToSave", player));
        }


        object CanAcceptItem(ItemContainer container, Item item)
        {
           if (container == null || item == null || container.playerOwner == null || container.playerOwner.GetComponent<PlayerInventory>() == null || item.parent == null || item.parent.playerOwner == null) return null;
            BasePlayer player = container.playerOwner.GetComponent<PlayerInventory>().containerBelt.GetOwnerPlayer();
            if (Sorting.Contains(player.userID) || !Enabled.ContainsKey(player.userID)) return null;
            if (item.parent.playerOwner.inventory.containerBelt.itemList.Contains(item) && cbdata.BeltPreference[player.userID][Enabled[player.userID]].ContainsKey(item.info.shortname))
                return ItemContainer.CanAcceptResult.CannotAccept;
            if (configData.IncludeWearSlots && item.parent.playerOwner.inventory.containerWear.itemList.Contains(item) && cbdata.WearPreference[player.userID][Enabled[player.userID]].ContainsKey(item.info.shortname))
                return ItemContainer.CanAcceptResult.CannotAccept;
            return null;
        }
        void ReOrg(BasePlayer player)
        {
            List<Item> Items = new List<Item>();
            foreach (var entry in player.inventory.containerBelt.itemList) Items.Add(entry);
            foreach (var entry in player.inventory.containerMain.itemList) Items.Add(entry);
            foreach (var entry in Items)
            {
                entry.RemoveFromContainer();
               entry.MoveToContainer(player.inventory.containerMain);
            }
            if(configData.IncludeWearSlots)
            {
                Items.Clear();
                foreach (var entry in player.inventory.containerWear.itemList) Items.Add(entry);
                foreach (var entry in player.inventory.containerMain.itemList) Items.Add(entry);
                foreach (var entry in Items)
                {
                    entry.RemoveFromContainer();
                    entry.MoveToContainer(player.inventory.containerMain);
                }
            }
        }


        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null || container.playerOwner == null || container.playerOwner.GetComponent<PlayerInventory>() == null) return;
            if (Debug) Puts("Pass 1");
            BasePlayer player = container.playerOwner.GetComponent<PlayerInventory>().containerBelt.GetOwnerPlayer();
            if (player == null)
            {
                if (Debug) Puts("Not Player");
                return;
            }
            if (Sorting.Contains(player.userID))
            {
                if (Debug) Puts("Sorting");
                Sorting.Remove(player.userID);
                return;
            }
            if (!Enabled.ContainsKey(player.userID)) return;
            if (!cbdata.BeltPreference.ContainsKey(player.userID) || !cbdata.BeltPreference[player.userID].ContainsKey(Enabled[player.userID]) || !cbdata.BeltPreference[player.userID][Enabled[player.userID]].ContainsKey(item.info.shortname))
                if (!configData.IncludeWearSlots) return;
                else if (!cbdata.WearPreference.ContainsKey(player.userID) || !cbdata.WearPreference[player.userID].ContainsKey(Enabled[player.userID]) || !cbdata.WearPreference[player.userID][Enabled[player.userID]].ContainsKey(item.info.shortname)) return;
            if (Debug) Puts("Passed Sorting");
            ItemContainer cont;
            int SavedPosition;
            if (configData.IncludeWearSlots && cbdata.WearPreference[player.userID][Enabled[player.userID]].ContainsKey(item.info.shortname))
            {
                cont = container.playerOwner.GetComponent<BasePlayer>().inventory.containerWear;
                SavedPosition = cbdata.WearPreference[player.userID][Enabled[player.userID]][item.info.shortname];
            }
            else
            {
                cont = container.playerOwner.GetComponent<BasePlayer>().inventory.containerBelt;
                SavedPosition = cbdata.BeltPreference[player.userID][Enabled[player.userID]][item.info.shortname];
            }
            if (Debug) Puts($"SavedPostion: {SavedPosition}");
            int index = -1;
            index = cont.itemList.FindIndex(k=>k.position == SavedPosition);
            if (index != -1)
            {
                Item ExistingItem = cont.itemList[index];
                if (Debug) Puts("Contains an Item");
                if (ExistingItem.info.shortname == item.info.shortname)
                {
                    if (Debug) Puts("Item is correct item");
                    if (ExistingItem.amount != ExistingItem.MaxStackable() && ExistingItem != item)
                    {
                        if (Debug) Puts("Item stack has room - Moving item");
                        item.RemoveFromContainer();
                        if (!Sorting.Contains(player.userID)) Sorting.Add(player.userID);
                        item.MoveToContainer(cont, SavedPosition);
                    }
                }
                else
                {
                    if (Debug) Puts("Not same Item - Moving bad..");
                    ExistingItem.RemoveFromContainer();
                    ExistingItem.MoveToContainer(player.inventory.containerMain);
                    if (Debug) Puts("Moving good..");
                    item.RemoveFromContainer();
                    if (!Sorting.Contains(player.userID)) Sorting.Add(player.userID);
                    item.MoveToContainer(cont, SavedPosition);
                }
            }
            else
            {
                if (Debug) Puts("No item in spot. Moving Item..");
                item.RemoveFromContainer();
                if (!Sorting.Contains(player.userID)) Sorting.Add(player.userID);
                item.MoveToContainer(cont, SavedPosition);
            }
        }

        float Default_minx = 0.65f;
        float Default_miny = 0.026f;
        float Default_maxx = 0.72f;
        float Default_maxy = 0.095f;

        private ConfigData configData;
        class ConfigData
        {
            public string Preference1_HotKey { get; set; }
            public string Preference2_HotKey { get; set; }
            public string Preference3_HotKey { get; set; }
            public string Cycle_HotKey { get; set; }
            public bool IncludeWearSlots { get; set; }
            public float minx { get; set; }
            public float miny { get; set; }
            public float maxx { get; set; }
            public float maxy { get; set; }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
            if (configData.maxx == new float() && configData.maxy == new float() && configData.minx == new float() && configData.miny == new float())
            {
                configData.minx = Default_minx;
                configData.miny = Default_miny;
                configData.maxx = Default_maxx;
                configData.maxy = Default_maxy;
                SaveConfig(configData);
            }
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Preference1_HotKey = string.Empty,
                Preference2_HotKey = string.Empty,
                Preference3_HotKey = string.Empty,
                Cycle_HotKey = string.Empty,
                IncludeWearSlots = false,
                minx = Default_minx,
                miny = Default_miny,
                maxx = Default_maxx,
                maxy = Default_maxy,
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);


        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "CustomHotbar: " },
            {"NoPerm", "You do not have permission to use this command" },
            {"NoItemsError", "You must place items on your hotbar before saving." },
            {"SavedSuccess", "You have saved a new Hotbar Profile {0}" },
            {"NoProfileError","You do not have a {0} saved profile. Arrange your hotbar and click 'Save'." },
            {"HotbarEnabled", "You have enabled Hotbar Profile {0}. Items will be autoplaced in your hotbar!" },
            {"HotbarDisabled", "You have disabled hotbar Profile {0}. Items will no longer autoplace in your hotbar!" },
            {"SelectProfileToSave", "Please press button 1, 2 , or 3 to SAVE this profile." },
            {"DuplicateItemsAbort", "Duplicate Items found on Hotbar. Please remove duplicates and try again." },
            {"Toggle", "Toggle" },
            {"Save", "Save" },
        };
    }
}