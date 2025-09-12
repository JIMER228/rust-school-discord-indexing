using ConVar;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HeliCallCraft", "https://topplugin.ru/ / https://discord.com/invite/5DPTsRmd3G", "2.1.3")]
    public class HeliCallCraft : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;

        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);

        #region Reference
        [PluginReference] Plugin IQChat;
        #endregion

        #region CFG
        private class Configuration
        {
            public Dictionary<int, CustomItem> CustomItems = new Dictionary<int, CustomItem>
            {
                [0] = new CustomItem
                {
                    DisplayName = "Рация",
                    Descripteon = "Рация -  с помощью нее вы сможете вызвать вертолет! Только будте готовы его сбить. Иначе он улетит (",
                    ReplaceShortName = "battery.small",
                    UiGOOD = "✔",
                    Cooldown = 360,
                    ReplaceID = 1700982057,
                    CraftUiOpen = "Heli",
                    itemsrec = new Dictionary<string, int>
                    {
                        ["metal.fragments"]  = 1000,
                        ["metal.refined"]  = 130,
                        ["wiretool"]  = 1,
                        ["techparts"]  = 5,
                        ["targeting.computer"]  = 1,
                    }, 
                }
            };

        }

        private static Configuration Settings = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
                if (Settings?.CustomItems == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("Ошибка чтения конфигурации 'oxide/config/', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => Settings = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(Settings);

        private class CustomItem
        {
            [JsonProperty("Отображаемое имя", Order = 0)]
            public string DisplayName;
            [JsonProperty("Описания в меню крафта", Order = 1)]
            public string Descripteon;
            [JsonProperty("Этот парамтр менять не нужно", Order = 2)]
            public string ReplaceShortName;
            [JsonProperty("Кд на вызов вертолета", Order = 3)]
            public int Cooldown;

            [JsonProperty("Скин ID рации", Order = 4)]
            public ulong ReplaceID;
            [JsonProperty("Команда для открытия меню", Order = 5)]
            public string CraftUiOpen;
            [JsonProperty("Символ показывающий,что у игрока достаточно предметов на крафт / Symbol showing that the player has enough items to craft", Order = 6)]
            public string UiGOOD;

            [JsonProperty("Что нужно для крафта (Макс 5 предметов)", Order = 7)]
            public Dictionary<string, int> itemsrec = new Dictionary<string, int>();

            public Item CreateItem(int amount)
            {
                Item item = ItemManager.CreateByPartialName(ReplaceShortName, amount);
                item.name = DisplayName;
                item.skin = ReplaceID;

                return item;
            }
        }

        #endregion

        #region command
        

        private void CallHeliForPlayer(BasePlayer player)
        {
            #region RandomSpawnPosition
            float x = TerrainMeta.Size.x;
            float y = 70f;
            Vector3 val = Vector3Ex.Range(-1f, 1f);
            val.y = 0f;
            val.Normalize();
            val *= x * 1f;
            val.y = y;
            #endregion

            BaseHelicopter heli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", val, new Quaternion(), true) as BaseHelicopter;
            if (!heli) return;
            heli.Spawn();  
            heli.GetComponent<PatrolHelicopterAI>().State_Move_Enter(player.transform.position + new Vector3(0.0f, 30f, 0.0f));     
        }

        private void GiveRacia(BasePlayer player, int count = 1)
        {
            Item item = Settings.CustomItems[0].CreateItem(count);
            player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }



        #endregion

        #region Command

        [ConsoleCommand("Craft_racif")]
        void CraftRacia(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!CraftCheck(player))
            {
                SendChat(player, "Недостаточно ресурсов");
                CuiHelper.DestroyUi(player, CraftMenu);
                return;
            }
            foreach (var item in Settings.CustomItems[0].itemsrec)
            {
                player.inventory.Take(null, ItemManager.FindItemDefinition(item.Key).itemid, item.Value);
            }
            GiveRacia(player);
            SendChat(player, "Рация создана успешно");
            CuiHelper.DestroyUi(player, CraftMenu);

        }

        [ConsoleCommand("heli")]
        void HeliCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = BasePlayer.FindByID(ulong.Parse(arg.Args[0]));
            if (player == null || !player.IsConnected)
            {
                Puts("Игрок не найден");
                return;
            }
            int count = int.Parse(arg.Args[1]);
            GiveRacia(player, count);
            SendChat(player, $"Вы успешно получили {Settings.CustomItems[0].DisplayName}");
            Puts($"Игроку выдана {Settings.CustomItems[0].DisplayName}");
        }

        [ChatCommand("heli.give")]
        private void CmdChatDebugHeliSpawn(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            foreach (var check in Settings.CustomItems)
            {
                var item = check.Value.CreateItem(5);
                item.MoveToContainer(player.inventory.containerMain);
            }
        }

        #endregion

        #region Hook

        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("Не найден ImageLibrary, плагин не будет работать!");
                return;
            }

            #region Permission
            permission.RegisterPermission("helicallcraft.craft", this);
            #endregion

            AddImage("https://i.imgur.com/lqdLqOW.png", "HeliCall");

            foreach(var Set in Settings.CustomItems[0].itemsrec)
            {
                if (!(bool)ImageLibrary?.Call("HasImage", Set.Key + 36)) ImageLibrary.Call("AddImage", $"https://api.skyplugins.ru/api/getimage/{Set.Key}/128", Set.Key + 36);
            }

            BasePlayer.activePlayerList.ToList().ForEach(p => OnPlayerConnected(p));

            cmd.AddChatCommand(Settings.CustomItems[0].CraftUiOpen, this, nameof(craftuiheli));
        }

        private void OnPlayerConnected(BasePlayer player, bool first = true)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player, first));
                return;
            }

            #region CashImage
            if (first)
            {
                ImageLibrary.Call("SendImage", player, "HeliCall");
                foreach (var Set in Settings.CustomItems[0].itemsrec)
                {
                    ImageLibrary.Call("SendImage", player, Set.Key + 36);
                }
            }
            #endregion
        }

        private List<ulong> cooldownPlayers = new List<ulong>();

        void OnPlayerInput(BasePlayer player, InputState input)
        {          
            if (input.WasJustPressed(BUTTON.USE) && player.GetActiveItem() != null && player.GetActiveItem().info.shortname == Settings.CustomItems[0].ReplaceShortName)
            {
                Item activeItem = player.GetActiveItem();
                if (activeItem.skin != Settings.CustomItems[0].ReplaceID) return;

                if (!cooldownPlayers.Contains(player.userID))
                {
                    activeItem.amount -= 1;
                    if (activeItem.amount <= 0)
                    {
                        activeItem.amount = 0;
                        activeItem.Remove(0f);
                    }
                    activeItem.MarkDirty();
                    CallHeliForPlayer(player);
                    SendChat(player, "Ваш вертолет уже вылетел к вам");
                    cooldownPlayers.Add(player.userID);
                    timer.Once(Settings.CustomItems[0].Cooldown, () => cooldownPlayers.Remove(player.userID));
                }
                else
                {
                    SendChat(player, "Вы не можете так быстро вызывать вертолет! Подождите не много");
                }
            }
        }

        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.GetItem().skin != targetItem.GetItem().skin) return false;
            return null;
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item.skin != targetItem.skin) return false;
            return null;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            if (plugins.Find("Stacks") || plugins.Find("CustomSkinsStacksFix") || plugins.Find("SkinBox")) return null;

            var customItem = Settings.CustomItems.FirstOrDefault(p => p.Value.ReplaceShortName == item.info.shortname);
            if (customItem.Value != null && customItem.Value.ReplaceID == item.skin)
            {
                Item x = ItemManager.CreateByPartialName(customItem.Value.ReplaceShortName, amount);
                x.name = customItem.Value.DisplayName;
                x.skin = customItem.Value.ReplaceID;
                x.amount = amount;

                item.amount -= amount;
                return x;
            }

            return null;
        }
        #endregion

        public static string CraftMenu = "MENU_CRAFT";

        #region CUI
        private void craftuiheli(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "helicallcraft.craft"))
            {
                SendChat(player, "У вас нет разрешения для использования данной команды!");
                return;
            }

            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, CraftMenu);

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = { Material = "assets/content/ui/uibackgroundblur.mat", Color = "0 0 0 0.8", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, "Overlay", CraftMenu);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100" },
                Button = { Close = CraftMenu, Color = "0 0 0 0" },
                Text = { Text = "" }
            }, CraftMenu);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.001041676 0.8453709", AnchorMax = "0.9989583 0.9491506", OffsetMax = "0 0" },
                Text = { Text = Settings.CustomItems[0].Descripteon, FontSize = 26, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, CraftMenu);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.3468742 0.6499981", AnchorMax = "0.609375 0.7027759"},
                Text = { Text = "Для крафта вам понадабиться:", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, CraftMenu);

            container.Add(new CuiElement
            {
                Parent = CraftMenu,
                Components = {
                    new CuiRawImageComponent {
                        Png = GetImage("HeliCall"),
                        Url = null ,
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.08281201 0.4259242",
                        AnchorMax = "0.2968751 0.7601834"
                    },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1156252 0.3388898", AnchorMax = "0.2614583 0.4027806" },
                Button = { Color = HexToCuiColor("#6AD78AE8"), Command = $"Craft_racif" },
                Text = { Text = "Скрафтить рацию", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, CraftMenu);

            int i = 0;
            foreach(var items in Settings.CustomItems[0].itemsrec)
            {
                string color = UseCraft(player, items.Key) ? "#A60D0D6D" : "#1FB9196B";
                var has = player.inventory.GetAmount(ItemManager.FindItemDefinition(items.Key).itemid);
                var result = items.Value - has <= 0 ? $"{Settings.CustomItems[0].UiGOOD}" : $"{Convert.ToInt32(items.Value - has).ToString()}";

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{0.2984375 + (i * 0.1)} 0.4425907", AnchorMax = $"{0.3921875 + (i * 0.1)} 0.6092573" },
                    Image = {Color = HexToCuiColor(color),}
                }, CraftMenu, "countitem");

                container.Add(new CuiElement
                {
                    Parent = "countitem",
                    Name = "count",
                    Components = {
                    new CuiRawImageComponent {
                        Png = GetImage(items.Key + 36),
                        Url = null ,
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.0277777 0.8000112", AnchorMax = "0.9666665 0.9722338" },
                    Text = { Text = result, FontSize = 18, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, "count");
                i++;

            }

            CuiHelper.AddUi(player, container);
        }

        public void SendChat(BasePlayer player, string Message, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message, "");
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }

        private bool UseCraft(BasePlayer player, string Short)
        {
            var craft = Settings.CustomItems[0].itemsrec;
            var more = new Dictionary<string, int>();

            foreach (var component in craft)
            {
                var name = component.Key;
                var has = player.inventory.GetAmount(ItemManager.FindItemDefinition(component.Key).itemid);
                var need = component.Value;
                if (has < component.Value)
                {
                    if (!more.ContainsKey(name))
                    {
                        more.Add(name, 0);
                    }

                    more[name] += need - has;
                }
            }

            if (more.ContainsKey(Short))
                return true;
            else
                return false;
        }

        private bool CraftCheck(BasePlayer player)
        {
            var craft = Settings.CustomItems[0].itemsrec;
            var more = new Dictionary<string, int>();

            foreach (var component in craft)
            {
                var name = component.Key;
                var has = player.inventory.GetAmount(ItemManager.FindItemDefinition(component.Key).itemid);
                var need = component.Value;
                if (has < component.Value)
                {
                    if (!more.ContainsKey(name))
                    {
                        more.Add(name, 0);
                    }

                    more[name] += need - has;
                }
            }

            if (more.Count == 0)
                return true;
            else
                return false;
        }
        #endregion

        #region Hex
        private static string HexToCuiColor(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
        #endregion
    }
}
