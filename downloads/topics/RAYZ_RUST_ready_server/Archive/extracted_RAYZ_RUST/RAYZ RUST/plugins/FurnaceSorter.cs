using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FurnaceSorter", "PsychoTea", "1.3.4"), Description("An easy to use Furnace Sorting Tool, automatically evening out stacks")]
    class FurnaceSorter : RustPlugin
    {
        #region Fields
        private readonly Dictionary<string, Timer> _timers = new Dictionary<string, Timer>();

        private readonly Dictionary<ulong, BaseOven> _uiInfo = new Dictionary<ulong, BaseOven>();

        private readonly Hash<BaseOven, BasePlayer> entityLookup = new Hash<BaseOven, BasePlayer>();

        private UpdateInvoker _updateInvoker;

        private const string PANEL_SORTER = "PanelSorter";
        private const string PANEL_SCREEN = "PanelOnScreen";        
        private const string PERMISSION_ALLOW = "furnacesorter.allow";
        #endregion
                
        #region Oxide Hooks

        private void Loaded()
        {
            permission.RegisterPermission(PERMISSION_ALLOW, this);
            _updateInvoker = new GameObject().AddComponent<UpdateInvoker>();
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(Messages, this);

        private void Unload()
        {
            if (_updateInvoker)
                UnityEngine.Object.Destroy(_updateInvoker);

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CloseUI(player);

            foreach (KeyValuePair<string, Timer> entry in _timers)            
                entry.Value.Destroy();            

            _timers.Clear();

            PlayerInfo.Reset();
            FurnaceInfo.Reset();

            Configuration = null;
        }

        private void OnPlayerDisconnected(BasePlayer player) => CloseUI(player);
        
        private void OnPlayerRespawned(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PANEL_SCREEN);
            CuiHelper.DestroyUi(player, PANEL_SORTER);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!player || !entity) 
                return;

            if (!HasPerm(player)) 
                return;

            BaseOven oven = entity as BaseOven;
            if (oven == null || (int)oven.temperature < 2) 
                return;

            if (!_uiInfo.ContainsKey(player.userID))            
                _uiInfo.Add(player.userID, oven);            

            PlayerInfo playerInfo = PlayerInfo.Get(player);
            FurnaceInfo ovenInfo = FurnaceInfo.Get(oven);

            playerInfo.CurrentFurnaceID = ovenInfo.FurnaceID;

            ovenInfo.TimeRemaining = SmeltTimeRemaining(oven);
            ovenInfo.FuelRequired = GetFuelRequired(oven, ovenInfo.TimeRemaining);

            _uiInfo[player.userID] = oven;

            entityLookup[oven] = player;

            _updateInvoker.Invoke(oven, ()=> UpdateUICycle(oven, player));

            SorterUI(player);
        }

        private void OnPlayerLootEnd(PlayerLoot looter)
        {
            if (looter == null || looter.entitySource == null)            
                return;            

            if (!(looter.entitySource is BaseOven))            
                return;

            BaseOven oven = (looter.entitySource as BaseOven);

            if ((int)oven.temperature < 2)
                return;
            
            entityLookup.Remove(oven);

            BasePlayer player = looter._baseEntity as BasePlayer;
            if (player == null)            
                return;
            
            _updateInvoker.CancelInvoke(oven, ()=> UpdateUICycle(oven, player));

            PlayerInfo playerInfo = PlayerInfo.Get(player);
            playerInfo.CurrentFurnaceID = 0;

            if (_uiInfo.ContainsKey(player.userID))            
                _uiInfo[player.userID] = null;            

            CuiHelper.DestroyUi(player, PANEL_SORTER);
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item) =>
            UpdateFurnace(container, item, true, true);

        private void OnItemRemovedFromContainer(ItemContainer container, Item item) => 
            UpdateFurnace(container, item);

        private void OnItemStacked(Item item, Item otherItem, ItemContainer container)
        {
            if (item == null || otherItem == null)
                return;
            
            if (item.parent != null && item.parent == otherItem.parent)
                return;
            
            UpdateFurnace(container, item, true, true);
        }

        private void OnItemSplit(Item item, int splitAmount)
        {
             NextTick(() =>
             {
                 if (item == null)
                     return;
                 
                 UpdateFurnace(item.parent, item);
             });
        }
        #endregion
       
        #region Functions
        private void UpdateFurnace(ItemContainer container, Item item, bool adjustFuel = false, bool sortInputSlots = false)
        {
            if (container == null)
                return;
            
            BaseOven targetOven = container.entityOwner as BaseOven;
            if (targetOven == null || targetOven.IsBurnableItem(item) || targetOven.IsOutputItem(item) || (int)targetOven.temperature < 2)            
                return;

            BasePlayer player;
            if (!entityLookup.TryGetValue(targetOven, out player))
                return;
            
            if (player == null || !HasPerm(player))
                return;
            
            PlayerInfo playerInfo = PlayerInfo.Get(player);

            if (!playerInfo.SorterEnabled)
                return;
            
            playerInfo.CurrentFurnaceID = targetOven.net.ID;

            FurnaceInfo furnaceInfo = FurnaceInfo.Get(targetOven);
            
            if (sortInputSlots)
                SortInputSlots(targetOven, item);

            furnaceInfo.TimeRemaining = SmeltTimeRemaining(targetOven);

            int fuelRequired = GetFuelRequired(targetOven, furnaceInfo.TimeRemaining);
            furnaceInfo.FuelRequired = fuelRequired;

            if (adjustFuel && playerInfo.AutoFuelEnabled)
                AdjustAutoFuel(player, targetOven, fuelRequired);

            SorterUI(player);
        }

        private void AdjustAutoFuel(BasePlayer player, BaseOven targetOven, int fuelRequired)
        {
            int fuelInOven = 0;

            for (int i = 0; i < targetOven.fuelSlots; i++)
            {
                Item slot = targetOven.inventory.GetSlot(i);
                if (slot != null && slot.info == targetOven.fuelType)
                    fuelInOven += slot.amount;
            }

            int fuelCapacity = targetOven.fuelType.stackable * targetOven.fuelSlots;

            if (fuelInOven < fuelRequired && fuelInOven < fuelCapacity)
            {
                int playerFuelAmount = player.inventory.GetAmount(targetOven.fuelType.itemid);
                int amountToMove = Mathf.Min(playerFuelAmount, fuelRequired - fuelInOven, fuelCapacity - fuelInOven);
                
                if (playerFuelAmount > 0 && amountToMove > 0)
                {
                    player.inventory.Take(null, targetOven.fuelType.itemid, amountToMove);
                    targetOven.inventory.AddItem(targetOven.fuelType, amountToMove, 0UL, ItemContainer.LimitStack.All);
                }
            }
        }

        private void SortInputSlots(BaseOven oven, Item inputItem)
        {
            List<Item> list = Facepunch.Pool.GetList<Item>();
            int totalInContainer = 0;
            int freeSlots = 0;
            
            for (int i = oven._inputSlotIndex; i < oven._inputSlotIndex + oven.inputSlots; i++)
            {
                Item item = oven.inventory.GetSlot(i);
                if (item != null)
                {
                    if (item.info == inputItem.info)
                    {
                        list.Add(item);
                        totalInContainer += item.amount;
                    }
                }
                else freeSlots++;
            }

            if (totalInContainer < oven.inputSlots)
                return;
            
            if (freeSlots > 0)
            {
                for (int i = 0; i < freeSlots; i++)
                {
                    for (int y = oven._inputSlotIndex; y < oven._inputSlotIndex + oven.inputSlots; y++)
                    {
                        Item slot = oven.inventory.GetSlot(y);
                        if (slot == null)
                        {
                            Item item = ItemManager.Create(inputItem.info, 1, 0UL);
                            
                            item.RemoveFromContainer();
                            item.RemoveFromWorld();
                            
                            item.position = y;
                            item.parent = oven.inventory;

                            oven.inventory.itemList.Add(item);
                            oven.inventory.MarkDirty();
                            
                            if (oven.inventory.onItemAddedRemoved != null)
                                oven.inventory.onItemAddedRemoved(item, true);

                            item.MarkDirty();
                            list.Add(item);
                        }
                    }
                }
            }
            
            int amountPerSlot = Mathf.CeilToInt((float)totalInContainer / (float)list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                Item item = list[i];
                item.amount = Mathf.Min(amountPerSlot, totalInContainer);
                item.MarkDirty();

                totalInContainer -= item.amount;
            }
        }

        private float SmeltTimeRemaining(BaseOven oven)
        {
            float totalTime = 0;

            for (int i = oven._inputSlotIndex; i < oven._inputSlotIndex + oven.inputSlots; i++)
            {
                Item slot = oven.inventory.GetSlot(i);
                if (slot != null)
                {
                    ItemModCookable itemModCookable = slot.info.GetComponent<ItemModCookable>();
                    if (itemModCookable)
                    {
                        if (slot.cookTimeLeft < itemModCookable.cookTime)
                        {
                            totalTime += itemModCookable.cookTime * (float) (slot.amount - 1);
                            totalTime += slot.cookTimeLeft;
                        }
                        else totalTime += itemModCookable.cookTime * (float) slot.amount;
                    }
                
                }
            }

            return totalTime / (float) oven.smeltSpeed;
        }

        private int GetFuelRequired(BaseOven oven, float totalCookTime)
        {
            ItemModBurnable itemModBurnable = oven.fuelType.GetComponent<ItemModBurnable>();
            if (itemModBurnable)
            {
                int fuelAmount = Mathf.CeilToInt(totalCookTime / (itemModBurnable.fuelAmount / (oven.cookingTemperature / 200f)));
                return fuelAmount;
            }

            return Mathf.CeilToInt(totalCookTime);
        }

        private void UpdateUICycle(BaseOven targetOven, BasePlayer player)
        {
            if (!targetOven || !player)
                return;

            if (_updateInvoker.IsOpen(targetOven))
            {
                if (targetOven.IsOn())
                {
                    PlayerInfo playerInfo = PlayerInfo.Get(player);

                    if (playerInfo.SorterEnabled)
                    {
                        FurnaceInfo furnaceInfo = FurnaceInfo.Get(targetOven);

                        furnaceInfo.TimeRemaining = SmeltTimeRemaining(targetOven);
                        furnaceInfo.FuelRequired = GetFuelRequired(targetOven, furnaceInfo.TimeRemaining);

                        SorterUI(player);
                    }
                }

                _updateInvoker.Invoke(targetOven, () => UpdateUICycle(targetOven, player));
            }
        }
        
        private class UpdateInvoker : MonoBehaviour
        {
            private readonly HashSet<BaseOven> openOvens = new HashSet<BaseOven>();

            public void Invoke(BaseOven baseOven, Action action)
            {
                openOvens.Add(baseOven);
                InvokeHandler.Invoke(this, action, 1f);
            }

            public void CancelInvoke(BaseOven baseOven, Action action)
            {
                openOvens.Remove(baseOven);
                InvokeHandler.CancelInvoke(this, action);
            }

            public bool IsOpen(BaseOven baseOven) => openOvens.Contains(baseOven);
        }
        #endregion

        #region Helpers
        private void SendMessage(BasePlayer player, string key, params object[] args)
        {
            string message = GetMessage(key, player, args);
            SendReply(player, $"<color=orange>{GetMessage("Title", player)}</color><color=#A9A9A9>{message}</color>");
        }

        private bool HasPerm(BasePlayer player, string perm = PERMISSION_ALLOW) => (player.IsAdmin || permission.UserHasPermission(player.UserIDString, perm));
        #endregion

        #region UI Commands
        [ConsoleCommand("UI_ToggleSorter")]
        private void UIToggleSorterConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPerm(player) || arg.Args.Length < 1)
                return;

            PlayerInfo playerInfo = PlayerInfo.Get(player);

            string option = arg.Args[0].ToLower();

            if (option == "sorter")
            {
                if (!playerInfo.SorterEnabled)
                {
                    playerInfo.SorterEnabled = true;
                    playerInfo.AutoFuelEnabled = true;

                    //OnScreen(player, "FurnaceSorterEnabled");
                    SorterUI(player);
                    return;
                }

                playerInfo.SorterEnabled = false;

                //OnScreen(player, "FurnaceSorterDisabled");
                SorterUI(player);
                return;
            }

            if (option == "autofuel")
            {
                playerInfo.AutoFuelEnabled = !playerInfo.AutoFuelEnabled;

                SorterUI(player);
                return;
            }

            //if (option == "stack")
            //{
            //    if (arg.Args.Length < 2)                
            //        return;                

            //    string stackOption = arg.Args[1].ToLower();

            //    if (stackOption == "increase")
            //    {
            //        if (playerInfo.ReservedCount >= 8)
            //            return;                    

            //        playerInfo.ReservedCount++;

            //        SorterUI(player);
            //        return;
            //    }

            //    if (stackOption == "decrease")
            //    {
            //        if (playerInfo.ReservedCount == 0)                    
            //            return;                    

            //        playerInfo.ReservedCount--;

            //        SorterUI(player);
            //        return;
            //    }
            //}
        }

        #endregion

        #region UI Helper
        internal class UI
        {
            internal static CuiElementContainer Container(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                return new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent = "Overlay",
                        panelName
                    }
                };
            }

            internal static void Button(CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, float fadeIn = 0f, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = fadeIn },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                }, panel);
            }

            internal static void Text(CuiElementContainer element, string panel, string colorText, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = colorText,
                            FontSize = size,
                            Align = align,
                            Text = text
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = aMin,
                            AnchorMax = aMax
                        }
                    }
                });
            }

            internal static void Outline(ref CuiElementContainer element, string panel, string colorText, string colorOutline, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = colorText,
                            FontSize = size,
                            Align = align,
                            Text = text
                        },
                        new CuiOutlineComponent
                        {
                            Distance = "1 1",
                            Color = colorOutline
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = aMin,
                            AnchorMax = aMax
                        }
                    }
                });
            }
        }
        #endregion

        #region UI Creation
        private void SorterUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PANEL_SORTER);

            CuiElementContainer element = UI.Container(PANEL_SORTER, "1 0 0 0.0", "0.651 0.020", "0.8141 0.140");

            PlayerInfo playerInfo = PlayerInfo.Get(player);

            string enabledColour = playerInfo.SorterEnabled ? "0.439 0.509 0.294 1.0" : "0.584 0.29 0.211 1.0";
            string enabledText = playerInfo.SorterEnabled ? "SorterEnabled" : "SorterDisabled";

            UI.Button(element, PANEL_SORTER, enabledColour, GetMessage(enabledText, player), 12, "0 0.75", "0.495 1", "UI_ToggleSorter sorter");

            if (playerInfo.SorterEnabled)
            {
                bool autoFuelEnabled = playerInfo.AutoFuelEnabled;

                string autoFuelEnabledText = autoFuelEnabled ? "AutoFuelEnabled" : "AutoFuelDisabled";

                UI.Button(element, PANEL_SORTER, enabledColour, GetMessage(autoFuelEnabledText, player), 12, "0.505 0.75", "1 1", "UI_ToggleSorter autofuel", 0.0f);
                
                FurnaceInfo furnaceInfo = FurnaceInfo.FindByID(playerInfo.CurrentFurnaceID);
                if (furnaceInfo != null)
                {
                    string rateKey = furnaceInfo.Temperature == BaseOven.TemperatureType.Smelting ? "SmeltRate" :
                                     furnaceInfo.Temperature == BaseOven.TemperatureType.Cooking ? "CookRate" : 
                                     furnaceInfo.Temperature == BaseOven.TemperatureType.Fractioning ? "RefineRate" :  string.Empty;

                    if (string.IsNullOrEmpty(rateKey))
                        return;
                    
                    UI.Text(element, PANEL_SORTER, "1 1 1 1", GetMessage(rateKey, player, furnaceInfo.SmeltRate), 12, "0 0", "1.0 0.25", TextAnchor.MiddleLeft);
                   
                    if (furnaceInfo.TimeRemaining > 0)
                    {
                        string timeMessage = string.Empty;
                        TimeSpan timeSpan = TimeSpan.FromSeconds(furnaceInfo.TimeRemaining);
                        if (timeSpan.Hours > 0)
                            timeMessage = string.Format("~ {0:D2}h:{1:D2}m:{2:D2}s", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
                        else timeMessage = string.Format("~ {0:D2}m:{1:D2}s", timeSpan.Minutes, timeSpan.Seconds);

                        UI.Text(element, PANEL_SORTER, "1 1 1 1", GetMessage("TimeRemaining", player, timeMessage), 12, "0 0.5", "1.0 0.75", TextAnchor.MiddleLeft);
                    }

                    if (furnaceInfo.FuelRequired > 0)
                        UI.Text(element, PANEL_SORTER, "1 1 1 1", GetMessage("FuelRequired", player, string.Format("~ {0:n0}", furnaceInfo.FuelRequired)), 12, "0 0.25", "1.0 0.5", TextAnchor.MiddleLeft);
                }

                //UI.Button(element, PANEL_SORTER, "1 0 0 0.3", "-", 14, "0 0.025", "0.15 0.225", "UI_ToggleSorter stack decrease", 0f);

                //UI.Text(element, PANEL_SORTER, "1 1 1 1", $"Reserved Slots : {playerInfo.ReservedCount}", 12, "0.15 0", "0.85 0.25", TextAnchor.MiddleCenter);

                //UI.Button(element, PANEL_SORTER, "0 1 0 0.3", "+", 14, "0.85 0.025", "1 0.225", "UI_ToggleSorter stack increase", 0f);
            }

            CuiHelper.AddUi(player, element);
        }

        private void OnScreen(BasePlayer player, string msg)
        {
            if (_timers.ContainsKey(player.userID.ToString()))
            {
                _timers[player.userID.ToString()].Destroy();
                _timers.Remove(player.userID.ToString());
            }

            CuiElementContainer element = UI.Container(PANEL_SCREEN, "0.0 0.0 0.0 0.0", "0.3 0.5", "0.7 0.8");
            UI.Outline(ref element, PANEL_SCREEN, string.Empty, "0 0 0 1", GetMessage(msg, player), 32, "0.0 0.0", "1.0 1.0");

            CuiHelper.DestroyUi(player, PANEL_SCREEN);
            CuiHelper.AddUi(player, element);

            _timers.Add(player.userID.ToString(), timer.Once(4, () => CuiHelper.DestroyUi(player, PANEL_SCREEN)));
        }

        private void CloseUI(BasePlayer player)
        {
            if (player == null)
                return;

            if (_uiInfo.ContainsKey(player.userID))
                _uiInfo.Remove(player.userID);

            if (_timers.ContainsKey(player.userID.ToString()))
            {
                _timers[player.userID.ToString()].Destroy();
                _timers.Remove(player.userID.ToString());
            }

            PlayerInfo playerInfo = PlayerInfo.Get(player);

            //playerInfo.SorterEnabled = false;

            CuiHelper.DestroyUi(player, PANEL_SCREEN);

            CuiHelper.DestroyUi(player, PANEL_SORTER);
        }
        #endregion

        #region Config
        private static ConfigData Configuration;

        private class ConfigData
        {
            public bool EnabledByDefault = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigData>();
            Config.WriteObject(Configuration, true);
        }

        protected override void LoadDefaultConfig() => Configuration = new ConfigData();

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);
        #endregion

        #region Data
        private class PlayerInfo
        {
            public BasePlayer Player { get; set; }

            public bool SorterEnabled { get; set; }

            public bool AutoFuelEnabled { get; set; }

            //public int ReservedCount { get; set; }

            public uint CurrentFurnaceID { get; set; }


            private static Hash<ulong, PlayerInfo> _playerInfos = new Hash<ulong, PlayerInfo>();

            public PlayerInfo(BasePlayer player)
            {
                this.Player = player;
                this.SorterEnabled = Configuration.EnabledByDefault;
                this.AutoFuelEnabled = true;
                //this.ReservedCount = 3;
            }

            public static PlayerInfo Get(BasePlayer player)
            {
                if (player == null)
                    return null;

                PlayerInfo playerInfo;
                if (!_playerInfos.TryGetValue(player.userID, out playerInfo))
                    playerInfo = _playerInfos[player.userID] = new PlayerInfo(player);

                return playerInfo;
            }

            public static void Reset() => _playerInfos.Clear();
        }

        private class FurnaceInfo
        {
            public uint FurnaceID { get; set; }

            public float TimeRemaining { get; set; }

            public int FuelRequired { get; set; }
            
            public float SmeltRate { get; set; }
            
            public BaseOven.TemperatureType Temperature { get; set; }


            private static Hash<uint, FurnaceInfo> _furnaceInfos = new Hash<uint, FurnaceInfo>();

            public FurnaceInfo(BaseOven oven)
            {
                this.FurnaceID = oven.net.ID;
                this.TimeRemaining = 0f;
                this.FuelRequired = 0;
                this.SmeltRate = oven.smeltSpeed;
                this.Temperature = oven.temperature;
            }

            public static FurnaceInfo Get(BaseOven oven)
            {
                if (oven == null)                
                    return null;

                FurnaceInfo furnaceInfo;
                if (!_furnaceInfos.TryGetValue(oven.net.ID, out furnaceInfo))
                    furnaceInfo = _furnaceInfos[oven.net.ID] = new FurnaceInfo(oven);

                return furnaceInfo;
            }

            public static FurnaceInfo FindByID(uint id)
            {
                FurnaceInfo furnaceInfo;
                if (!_furnaceInfos.TryGetValue(id, out furnaceInfo))
                    return null;

                return furnaceInfo;
            }

            public static void Reset() => _furnaceInfos.Clear();
        }
        #endregion


        #region Localization
        private string GetMessage(string key, BasePlayer player = null, params object[] args) => string.Format(lang.GetMessage(key, this, player?.UserIDString), args);

        private Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "Title", "FurnaceSorter: " },
            { "NoPerm","You do not have permission to use this command" },

            { "FurnaceSorterDisabled", "FurnaceSorter is now disabled." },
            { "FurnaceSorterEnabled", "FurnaceSorter is now enabled." },

            { "SorterEnabled", "Sorter Enabled" },
            { "SorterDisabled", "Sorter Disabled" },
            { "AutoFuelEnabled", "Auto-fuel Enabled" },
            { "AutoFuelDisabled", "Auto-fuel Disabled" },
            
            { "OptimizationUnavailable", "You can not use the Optimizer while the furnace is on!" },
            { "NothingToOptimize", "There is nothing in the furnace to optimize. Optimization Failed!" },
            { "NoWood", "The furnace does not appear to have wood. Optimization Failed!" },
            { "NoAcceptableItems", "The furnace does not appear to have any valid items to optimize wood against. Optimization Failed!" },
            { "WoodRatioGood", "The wood to acceptable item ratio is correct. No Optimization Required!" },
            { "WoodNeeded", "Optimizing found you are short {0} wood" },
            { "ExtraWoodGiven", "Optimizing found you have {0} extra wood. This wood has been placed in your inventory!" },
            { "InventoryFull", "Your furnace has {0} extra wood but it will not fit in your inventory. Optimization Failed!" },
            { "FurnaceOptimized", "Your furnace has been optimized!" },
            { "TimeRemaining", "Time Remaining: {0}" },
            { "FuelRequired", "Total Fuel Required: {0}" },
            { "SmeltRate", "Smelt Rate: {0}" },
            { "CookRate", "Cook Rate: {0}" },
            { "RefineRate", "Refine Rate: {0}"}
        };
        #endregion
    }
}
