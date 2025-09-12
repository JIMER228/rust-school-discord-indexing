using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("InventorySystem", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    class InventorySystem : RustPlugin
    {
        #region Вар
        string Layer = "Inventory_UI";

        [PluginReference] Plugin ImageLibrary;

        static InventorySystem ins;

        Dictionary<ulong, PlayerInventory> DB = new Dictionary<ulong, PlayerInventory>();
        #endregion

        #region Класс
        public class InventorySettings
        {
            public string DisplayName;
            public string ShortName;
            public string Command;
            public string Url;
            public string Color;
            public int Amount;
        }

        public class PlayerInventory
        {
            [JsonProperty("Список предметов")] public List<InventoryItem> Inventory = new List<InventoryItem>();
        }

        public class InventoryItem
        {
            [JsonProperty("Название предмета")] public string DisplayName;
            [JsonProperty("Короткое название предмета")] public string ShortName;
            [JsonProperty("Команда")] public string Command;
            [JsonProperty("Изображение")] public string Url;
            [JsonProperty("Цвет")] public string Color;
            [JsonProperty("Количество предмета")] public int Amount;

            public Item CreateItem(BasePlayer player)
            {
                if (!string.IsNullOrEmpty(Command)) ins.Server.Command(Command.Replace("%STEAMID%", player.UserIDString));
                if (!string.IsNullOrEmpty(ShortName))
                {
                    Item item = ItemManager.CreateByPartialName(ShortName, Amount);
                    return item;
                }

                return null;
            }

            public static InventoryItem Generate(string displayName, string shortName, string command, string url, string color, int amount)
            {
                return new InventoryItem
                {
                    DisplayName = displayName,
                    ShortName = shortName,
                    Command = command,
                    Url = url,
                    Color = color,
                    Amount = amount
                };
            }
        }
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            ins = this;
            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
        }

        void OnPlayerConnected(BasePlayer player) => CreateDataBase(player);

        void OnPlayerDisconnected(BasePlayer player) => SaveDataBase(player.userID);

        void Unload()
        {
            foreach (var check in DB)
               SaveDataBase(check.Key);
        }
        #endregion

        #region Дата
        void CreateDataBase(BasePlayer player)
        {
            var Database = Interface.Oxide.DataFileSystem.ReadObject<PlayerInventory>($"InventorySystem/{player.userID}");
            
            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, new PlayerInventory());
             
            DB[player.userID] = Database ?? new PlayerInventory();
        }

        void SaveDataBase(ulong userId) => Interface.Oxide.DataFileSystem.WriteObject($"InventorySystem/{userId}", DB[userId]);
        #endregion

        #region Метод
        InventoryItem AddItem(BasePlayer player, string displayName, string shortName, string command, string url, string color, int amount)
        {
            var check = InventoryItem.Generate(displayName, shortName, command, url, color, amount);
            DB[player.userID].Inventory.Add(check);
            return check;
        }
        #endregion

        #region Команда
        [ConsoleCommand("inventory")]
        void ConsoleInventory(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "take")
                {
                    var item = DB[player.userID].Inventory.ElementAt(int.Parse(args.Args[1]));
                    if (item.ShortName != null)
                    {
                        if (player.inventory.containerMain.itemList.Count >= 24)
                        {
                            player.ChatMessage($"<size=12>У вас <color=#4286f4>недостаточно</color> места в основном инвентаре!</size>");
                            return;
                        }
                    }
                        
                    if (item.ShortName != null)
                        player.SendConsoleCommand($"note.inv 605467368 {item.Amount} \"Предмет получен\"");
                    else
                        player.SendConsoleCommand($"note.inv 605467368 {item.Amount} \"Услуга получена\"");
                        
                    item.CreateItem(player)?.MoveToContainer(player.inventory.containerMain);
                    DB[player.userID].Inventory.Remove(item);

                    InventoryUI(player);
                    Effect x = new Effect("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", player, 0, new Vector3(), new Vector3());
                    EffectNetwork.Send(x, player.Connection);
                }
                if (args.Args[0] == "skip")
                {
                    InventoryUI(player, int.Parse(args.Args[1]));
                }
            }
        }
        #endregion

        #region Интерфейс
        void InventoryUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.09 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu", Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.855", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=25><b>Инвентарь</b></size>\nЗдесь будут храниться ваши предметы (батл пасс, лотерея).", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.407 0.05", AnchorMax = $"0.467 0.11", OffsetMax = "0 0" },
                Button = { Color = $"0.10 0.13 0.19 1", Command = page != 0 ? $"inventory skip {page - 1}" : "" },
                Text = { Text = "<", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.47 0.05", AnchorMax = $"0.53 0.11", OffsetMax = "0 0" },
                Image = { Color = $"0.10 0.13 0.19 1" }
            }, Layer, "Page");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = $"{page + 1}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Page");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.535 0.05", AnchorMax = $"0.595 0.11", OffsetMax = "0 0" },
                Button = { Color = $"0.10 0.13 0.19 1", Command = DB[player.userID].Inventory.Count() > (page + 1) * 12 ? $"inventory skip {page + 1}" : "" },
                Text = { Text = ">", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            float width = 0.23f, height = 0.23f, startxBox = 0.04f, startyBox = 0.85f - height, xmin = startxBox, ymin = startyBox;
            for (int z = 0; z < 12; z++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                    Image = { Color = "0.10 0.13 0.19 1" }
                }, Layer);

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            if (DB[player.userID].Inventory.Count() == 0)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.04 0.16", AnchorMax = $"0.96 0.85", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                    Image = { Color = "0.10 0.13 0.19 0.9" }
                }, Layer, "Empty");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.02", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"ИНВЕНТАРЬ ПУСТ", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 40, Font = "robotocondensed-bold.ttf" }
                }, "Empty");
            }
            else
            {
                var inv = DB[player.userID].Inventory.Skip(page * 12).Take(12);
                float width1 = 0.23f, height1 = 0.23f, startxBox1 = 0.04f, startyBox1 = 0.85f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
                foreach (var check in inv.Select((i,t) => new { A = i, B = t }))
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                        Button = { Color = $"{check.A.Color} 0.1", Command = $"inventory take {check.B + page * 12}" },
                        Text = { Text = "" }
                    }, Layer, "Inventory");

                    var image = check.A.Command != null ? check.A.Url : check.A.ShortName;
                    container.Add(new CuiElement
                    {
                        Parent = "Inventory",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image), Color = "1 1 1 0.9", FadeIn = 1f },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 15", OffsetMax = "-15 -15" }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0.02", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = $"{check.A.Amount} шт.", Color = "1 1 1 0.5", Align = TextAnchor.LowerCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                    }, "Inventory");

                    xmin1 += width1;
                    if (xmin1 + width1 >= 1)
                    {
                        xmin1 = startxBox1;
                        ymin1 -= height1;
                    }
                }
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}