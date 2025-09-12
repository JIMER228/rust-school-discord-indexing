using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections;
using System.Text;

namespace Oxide.Plugins
{
    public static class Extensions
    {
        public static string ToBase64(this byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }
    }

    [Info("BRulette", "blinchik x .h1pex", "1.0.0")]
    [Description("Плагин для игры в рулетку")]
    public class BRulette : RustPlugin
    {
        #region Fields
        private const string Layer = "BRulette";
        [PluginReference] private Plugin GameStoresRUST;
        private Configuration config;
        private Dictionary<ulong, RouletteState> playerStates = new Dictionary<ulong, RouletteState>();
        private Dictionary<ulong, int> playerSpins = new Dictionary<ulong, int>();
        private Dictionary<ulong, int> playerBalances = new Dictionary<ulong, int>();
        private Dictionary<ulong, Dictionary<string, int>> playerInventories = new Dictionary<ulong, Dictionary<string, int>>();
        private Dictionary<string, string> imageCache = new Dictionary<string, string>();
        private const string IMAGE_FOLDER = "BRulette/images";
        private const float INITIAL_DELAY = 0.05f; // Уменьшаем начальную задержку для более быстрого старта
        private const float MIN_DELAY = 0.2f; // Увеличиваем минимальную задержку для более медленной остановки
        private const float ACCELERATION = 0.98f; // Уменьшаем коэффициент ускорения для более плавного замедления
        private const int MIN_SPINS = 40; // Увеличиваем минимальное количество прокруток
        private const int MAX_SPINS = 60; // Увеличиваем максимальное количество прокруток
        private static BRulette Instance;
        #endregion

        private class ImageUi
        {
            private static Coroutine coroutineImg = null;
            private static Dictionary<String, String> Images = new Dictionary<String, String>();
            private static List<String> KeyImages = new List<String>();

            public static Dictionary<String, String> GetImages() => Images;

            public static void DownloadImages() 
            { 
                coroutineImg = ServerMgr.Instance.StartCoroutine(AddImage()); 
            }

            private static IEnumerator AddImage()
            {
                foreach (var key in KeyImages)
                {
                    if (Images.ContainsKey(key)) continue;

                    string url = key;
                    if (!url.StartsWith("http"))
                    {
                        string fullPath = $"{Interface.Oxide.DataDirectory}/{IMAGE_FOLDER}/{url}";
                        if (!System.IO.File.Exists(fullPath))
                        {
                            Instance.PrintWarning($"Изображение не найдено: {fullPath}");
                            continue;
                        }
                        url = "file://" + fullPath.Replace("\\", "/");
                    }

                    using (WWW www = new WWW(url))
                    {
                        yield return www;
                        if (www.error != null)
                        {
                            Instance.PrintError($"Ошибка загрузки изображения {key}: {www.error}");
                            continue;
                        }
                        Images[key] = www.texture.EncodeToPNG().ToBase64();
                    }
                }
            }

            public static String GetImage(String ImgKey) 
            { 
                return Images.ContainsKey(ImgKey) ? Images[ImgKey] : "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            }

            public static void Initialize()
            {
                Images.Clear();
                KeyImages.Clear();

                // Добавляем все изображения из конфигурации
                foreach (var prize in Instance.config.Prizes)
                {
                    if (!string.IsNullOrEmpty(prize.Image))
                    {
                        KeyImages.Add(prize.Image);
                    }
                }

                DownloadImages();
            }

            public static void Unload()
            {
                if (coroutineImg != null)
                {
                    ServerMgr.Instance.StopCoroutine(coroutineImg);
                    coroutineImg = null;
                }
                Images.Clear();
                KeyImages.Clear();
            }
        }

        #region Configuration
        private class Prize
        {
            [JsonProperty("Название")]
            public string Name { get; set; }

            [JsonProperty("URL изображения")]
            public string Image { get; set; }

            [JsonProperty("Шанс выпадения (%)")]
            public float Chance { get; set; }

            [JsonProperty("Количество")]
            public int Amount { get; set; }

            [JsonProperty("Цена продажи")]
            public int SellPrice { get; set; }

            [JsonProperty("Команда при получении")]
            public string Command { get; set; }
        }

        private class Configuration
        {
            [JsonProperty("ID магазина")]
            public string ShopID { get; set; } = "UNDEFINED";
            
            [JsonProperty("Секретный ключ")]
            public string SecretKey { get; set; } = "UNDEFINED";
            
            [JsonProperty("ID сервера")]
            public string ServerID { get; set; } = "UNDEFINED";

            [JsonProperty("Цена прокрутки")]
            public int SpinPrice { get; set; } = 100;

            [JsonProperty("Заголовок")]
            public string Title { get; set; } = "РУЛЕТКА <color=#fd00ff>BEAST RUST</color>";

            [JsonProperty("Баланс рулетки")]
            public int RouletteBalance { get; set; } = 0;

            [JsonProperty("Призы")]
            public List<Prize> Prizes { get; set; } = new List<Prize>
            {
                new Prize { Name = "АК-47", Image = "ak47.png", Chance = 5f, Amount = 1, SellPrice = 10 },
                new Prize { Name = "Болт", Image = "bolt.png", Chance = 10f, Amount = 100, SellPrice = 8 },
                new Prize { Name = "Револьвер", Image = "revolver.png", Chance = 8f, Amount = 1, SellPrice = 7 },
                new Prize { Name = "Сера", Image = "sulfur.png", Chance = 15f, Amount = 1000, SellPrice = 6 },
                new Prize { Name = "Порох", Image = "gunpowder.png", Chance = 15f, Amount = 1000, SellPrice = 6 },
                new Prize { Name = "Металл", Image = "metal.png", Chance = 15f, Amount = 2000, SellPrice = 5 },
                new Prize { Name = "Камень", Image = "stone.png", Chance = 15f, Amount = 2000, SellPrice = 5 },
                new Prize { Name = "Дерево", Image = "wood.png", Chance = 15f, Amount = 2000, SellPrice = 5 }
            };

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    ShopID = "UNDEFINED",
                    SecretKey = "UNDEFINED",
                    ServerID = "UNDEFINED",
                    SpinPrice = 100,
                    Title = "РУЛЕТКА <color=#fd00ff>BEAST RUST</color>",
                    RouletteBalance = 0,
                    Prizes = new List<Prize>
                    {
                        new Prize { Name = "АК-47", Image = "ak47.png", Chance = 5f, Amount = 1, SellPrice = 10 },
                        new Prize { Name = "Болт", Image = "bolt.png", Chance = 10f, Amount = 100, SellPrice = 8 },
                        new Prize { Name = "Револьвер", Image = "revolver.png", Chance = 8f, Amount = 1, SellPrice = 7 },
                        new Prize { Name = "Сера", Image = "sulfur.png", Chance = 15f, Amount = 1000, SellPrice = 6 },
                        new Prize { Name = "Порох", Image = "gunpowder.png", Chance = 15f, Amount = 1000, SellPrice = 6 },
                        new Prize { Name = "Металл", Image = "metal.png", Chance = 15f, Amount = 2000, SellPrice = 5 },
                        new Prize { Name = "Камень", Image = "stone.png", Chance = 15f, Amount = 2000, SellPrice = 5 },
                        new Prize { Name = "Дерево", Image = "wood.png", Chance = 15f, Amount = 2000, SellPrice = 5 }
                    }
                };
            }
        }

        private class RouletteState
        {
            public bool IsSpinning { get; set; }
            public int LastWinIndex { get; set; }
            public Prize SelectedPrize { get; set; }
            public Timer SpinTimer { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError("Ошибка чтения конфигурации, создаю новую!");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.DefaultConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Commands
        [ChatCommand("roll")]
        private void CmdRulette(BasePlayer player)
        {
            if (player == null) return;

            PrintWarning($"Команда /roll вызвана игроком {player.displayName}");

            try
            {
                // Очищаем все UI элементы
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, Layer + ".Background");
                CuiHelper.DestroyUi(player, Layer + ".Cursor");
                CuiHelper.DestroyUi(player, Layer + ".Roulette");
                CuiHelper.DestroyUi(player, "Overlay");
                CuiHelper.DestroyUi(player, "Hud");
                CuiHelper.DestroyUi(player, "BRulette.Title");
                CuiHelper.DestroyUi(player, "BRulette.Balance");
                CuiHelper.DestroyUi(player, "BRulette.Content");
                CuiHelper.DestroyUi(player, "BRulette.Places");
                CuiHelper.DestroyUi(player, "BRulette.Remaining");
                CuiHelper.DestroyUi(player, "BRulette.Win");

                // Блокируем управление игрока
                player.Command("cursor.lock", "false");
                player.Command("cursor.visible", "true");

                ShowMainMenu(player);
                
                PrintWarning($"UI успешно открыт для игрока {player.displayName}");
            }
            catch (Exception ex)
            {
                PrintError($"Ошибка при открытии UI для {player.displayName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [ConsoleCommand("brulette.givespins")]
        private void CmdGiveSpinsConsole(ConsoleSystem.Arg args)
        {
            // Проверяем, что команда вызвана из консоли сервера
            if (args.Player() != null) 
            {
                SendReply(args.Player(), "Эта команда доступна только через консоль сервера!");
                return;
            }

            if (args.Args == null || args.Args.Length != 2)
            {
                Puts("Использование: brulette.givespins <steamid> <количество>");
                return;
            }

            string targetId = args.Args[0];
            string amountStr = args.Args[1];

            ulong steamId;
            if (!ulong.TryParse(targetId, out steamId))
            {
                Puts("Неверный SteamID!");
                return;
            }

            int amount;
            if (!int.TryParse(amountStr, out amount) || amount <= 0)
            {
                Puts("Неверное количество прокруток!");
                return;
            }

            BasePlayer target = BasePlayer.FindByID(steamId);
            if (target == null)
            {
                // Если игрок оффлайн, всё равно добавляем ему прокрутки
                if (!playerSpins.ContainsKey(steamId))
                    playerSpins[steamId] = 0;
                playerSpins[steamId] += amount;
                Puts($"Выдано {amount} прокруток игроку {steamId} (оффлайн)");
                return;
            }

            if (!playerSpins.ContainsKey(target.userID))
                playerSpins[target.userID] = 0;
            playerSpins[target.userID] += amount;

            Puts($"Выдано {amount} прокруток игроку {target.displayName}");
            SendReply(target, $"Вам выдано {amount} прокруток!");
        }

        [ConsoleCommand("rulette.center")]
        private void CmdStartSpin(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            // Проверяем, не крутится ли уже рулетка
            if (playerStates.ContainsKey(player.userID) && playerStates[player.userID].IsSpinning)
            {
                SendReply(player, "Рулетка уже крутится!");
                return;
            }

            // Проверяем наличие прокруток
            if (!playerSpins.ContainsKey(player.userID) || playerSpins[player.userID] <= 0)
            {
                SendReply(player, "У вас нет доступных прокруток! Купите прокрутку за " + config.SpinPrice + "₽");
                return;
            }

            // Используем одну прокрутку
            playerSpins[player.userID]--;

            // Запускаем прокрутку
            StartSpinning(player);
        }

        [ConsoleCommand("rulette.inventory")]
        private void CmdInventory(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            ShowInventoryMenu(player);
        }

        [ConsoleCommand("rulette.back")]
        private void CmdBack(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            // Удаляем все UI элементы
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer + ".Background");
            CuiHelper.DestroyUi(player, Layer + ".Cursor");
            CuiHelper.DestroyUi(player, Layer + ".Roulette");
            CuiHelper.DestroyUi(player, "Overlay");
            CuiHelper.DestroyUi(player, "Hud");
            CuiHelper.DestroyUi(player, "BRulette.Title");
            CuiHelper.DestroyUi(player, "BRulette.Balance");
            CuiHelper.DestroyUi(player, "BRulette.Content");
            CuiHelper.DestroyUi(player, "BRulette.Places");
            CuiHelper.DestroyUi(player, "BRulette.Remaining");
            CuiHelper.DestroyUi(player, "BRulette.Win");

            ShowMainMenu(player);
        }

        [ConsoleCommand("rulette.buyspin")]
        private void CmdBuySpin(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            // Проверяем баланс игрока
            if (GetPlayerBalance(player) < config.SpinPrice)
            {
                SendReply(player, $"Недостаточно средств! Требуется: {config.SpinPrice}₽");
                return;
            }

            // Списываем стоимость прокрутки
            if (!RemoveFromPlayerBalance(player, config.SpinPrice))
            {
                SendReply(player, "Ошибка при списании средств!");
                return;
            }
            
            // Добавляем одну прокрутку
            if (!playerSpins.ContainsKey(player.userID))
                playerSpins[player.userID] = 0;
            playerSpins[player.userID]++;
            
            SendReply(player, "Вы купили одну прокрутку!");
            ShowMainMenu(player);
        }

        [ConsoleCommand("rulette.sell")]
        private void CmdSellPrize(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            if (args.Args == null || args.Args.Length != 1)
            {
                SendReply(player, "Использование: rulette.sell <индекс приза>");
                return;
            }

            if (!playerInventories.ContainsKey(player.userID))
            {
                SendReply(player, "У вас нет предметов в инвентаре!");
                return;
            }

            int itemIndex;
            if (!int.TryParse(args.Args[0], out itemIndex))
            {
                SendReply(player, "Неверный индекс предмета!");
                return;
            }

            var items = playerInventories[player.userID].Keys.ToList();
            if (itemIndex < 0 || itemIndex >= items.Count)
            {
                SendReply(player, "Неверный индекс предмета!");
                return;
            }

            string itemName = items[itemIndex];
            int amount = playerInventories[player.userID][itemName];
            var prize = config.Prizes.Find(p => p.Name == itemName);
            if (prize == null)
            {
                SendReply(player, "Предмет не найден в конфигурации!");
                return;
            }

            // Теперь используем фиксированную цену за весь приз
            int sellPrice = prize.SellPrice;
            RemoveItem(player, itemName, amount);
            AddToPlayerBalance(player, sellPrice);

            SendReply(player, $"Вы продали {amount} {itemName} за {sellPrice}₽");
            ShowInventoryMenu(player);
        }

        [ConsoleCommand("rulette.claim")]
        private void CmdClaimPrize(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            if (args.Args == null || args.Args.Length != 1)
            {
                SendReply(player, "Использование: rulette.claim <индекс приза>");
                return;
            }

            if (!playerInventories.ContainsKey(player.userID))
            {
                SendReply(player, "У вас нет предметов в инвентаре!");
                return;
            }

            int itemIndex;
            if (!int.TryParse(args.Args[0], out itemIndex))
            {
                SendReply(player, "Неверный индекс предмета!");
                return;
            }

            var items = playerInventories[player.userID].Keys.ToList();
            if (itemIndex < 0 || itemIndex >= items.Count)
            {
                SendReply(player, "Неверный индекс предмета!");
                return;
            }

            string itemName = items[itemIndex];
            int amount = playerInventories[player.userID][itemName];
            var prize = config.Prizes.Find(p => p.Name == itemName);
            if (prize == null)
            {
                SendReply(player, "Предмет не найден в конфигурации!");
                return;
            }

            // Выполняем команду из конфига, если она указана
            if (!string.IsNullOrEmpty(prize.Command))
            {
                string finalCommand = prize.Command
                    .Replace("{steamid}", player.userID.ToString())
                    .Replace("{name}", player.displayName)
                    .Replace("{amount}", amount.ToString());
                
                Server.Command(finalCommand);
            }

            RemoveItem(player, itemName, amount);
            SendReply(player, $"Вы успешно получили {amount}x {itemName}!");
            ShowInventoryMenu(player);
        }
        #endregion

        #region UI
        private void ShowMainMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer + ".Balance");
            CuiHelper.DestroyUi(player, Layer + ".Title");
            CuiHelper.DestroyUi(player, Layer + ".Content");
            CuiHelper.DestroyUi(player, Layer + ".Places");
            CuiHelper.DestroyUi(player, Layer + ".Remaining");
            CuiHelper.DestroyUi(player, Layer + ".Win");
            CuiHelper.DestroyUi(player, "Overlay");

            CuiElementContainer container = new CuiElementContainer();

            // Блокируем курсор
            player.Command("cursor.lock");
            player.Command("cursor.visible", "true");

            // Получаем баланс игрока
            int balance = GetPlayerBalance(player);
            string balanceText = $"Баланс: {balance}₽";

            // Получаем количество прокруток
            int spins = playerSpins.ContainsKey(player.userID) ? playerSpins[player.userID] : 0;

            // Надпись "Баланс:"
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.82", AnchorMax = "0.5 0.82", OffsetMin = "-100 -20", OffsetMax = "100 20" },
                Text = { 
                    Text = balanceText, 
                    Align = TextAnchor.MiddleCenter, 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 16, 
                    Color = "1 1 1 1"
                }
            }, "Overlay", Layer + ".Balance");

            // Заголовок "РУЛЕТКА PHENIX RUST"
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.79", AnchorMax = "0.5 0.79", OffsetMin = "-200 -20", OffsetMax = "200 20" },
                Text = { 
                    Text = config.Title, 
                    Align = TextAnchor.MiddleCenter, 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 28, 
                    Color = "1 1 1 1"
                }
            }, "Overlay", Layer + ".Title");

            // Основная панель с тёмно-серым фоном
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450 -220", OffsetMax = "450 220" },
                Image = { 
                    Color = "0.2 0.2 0.2 0.6",
                    Png = "https://i.postimg.cc/sxxf2SNy/Rectangle-11.png",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                }
            }, "Overlay", Layer);

            // Фоновая панель для блокировки кликов
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer);

            // Панель для управления курсором
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Cursor");

            // Черная полоса сверху
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.70", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.65" }
            }, Layer);

            // Белая полоска сверху
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.83", AnchorMax = "0.5 0.91", OffsetMin = "-1 -39", OffsetMax = "1 39" },
                Image = { Color = "1 1 1 1" }
            }, Layer);

            // Надпись "Осталось прокруток:"
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.63", AnchorMax = "0.5 0.63", OffsetMin = "-100 -20", OffsetMax = "100 20" },
                Text = { 
                    Text = $"Осталось прокруток: {spins}", 
                    Align = TextAnchor.MiddleCenter, 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 14, 
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".Remaining");

            // Создаем начальное состояние, если его нет
            if (!playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID] = new RouletteState
                {
                    IsSpinning = false,
                    LastWinIndex = 0
                };
            }

            // Всегда показываем барабан
            UpdateRouletteUI(player, playerStates[player.userID]);

            // Надпись "НЕТ ПРОКРУТОК" или "КРУТИТСЯ..." под барабаном
            string spinText;
            string spinColor;
            string spinCommand = "";

            // Проверяем состояние рулетки
            bool isSpinning = playerStates.ContainsKey(player.userID) && playerStates[player.userID].IsSpinning;
            bool hasSpins = playerSpins.ContainsKey(player.userID) && playerSpins[player.userID] > 0;

            if (isSpinning)
            {
                spinText = "КРУТИТСЯ...";
                spinColor = "0.5 0.5 0.5 0.8";
                spinCommand = "";
            }
            else if (hasSpins)
            {
                spinText = "КРУТИТЬ";
                spinColor = "0.2 0.8 0.2 0.8";
                spinCommand = "rulette.center";
            }
            else
            {
                spinText = "НЕТ ПРОКРУТОК";
                spinColor = "0.5 0.5 0.5 0.8";
                spinCommand = "";
            }

            // Центральная кнопка
            CuiHelper.DestroyUi(player, Layer + ".SpinButton");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.70", AnchorMax = "0.5 0.70", OffsetMin = "-100 -18", OffsetMax = "100 18" },
                Button = { 
                    Color = spinColor,
                    Command = spinCommand
                },
                Text = { 
                    Text = spinText,
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".SpinButton");

            // Надпись "СОДЕРЖИМОЕ РУЛЕТКИ"
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.52", AnchorMax = "0.5 0.52", OffsetMin = "-150 -20", OffsetMax = "150 20" },
                Text = { 
                    Text = "СОДЕРЖИМОЕ РУЛЕТКИ", 
                    Align = TextAnchor.MiddleCenter, 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 18, 
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".Content");

            // Надпись "22 призовых места"
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.47", AnchorMax = "0.5 0.47", OffsetMin = "-100 -20", OffsetMax = "100 20" },
                Text = { 
                    Text = "22 призовых места", 
                    Align = TextAnchor.MiddleCenter, 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 14, 
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".Places");

            // Сетка из квадратиков (22 штуки в два ряда)
            float squareHeight = 120f;
            float spacing = 10f;
            float totalWidth = 900f;
            float availableWidth = (totalWidth - (spacing * 10)) * 0.96f;
            float squareWidth = availableWidth / 11;
            float startX = 0.52f - (totalWidth / 2f / 900f);
            float squareHeightRelative = squareHeight / 900f;
            float squareWidthRelative = squareWidth / 900f;

            // Верхний ряд (11 квадратов)
            for (int i = 0; i < 11; i++)
            {
                float xPos = startX + (i * (squareWidth + spacing) / 900f);
                string panelName = $"Square_{i}";
                
                // Добавляем панель
                container.Add(new CuiPanel
                {
                    RectTransform = { 
                        AnchorMin = $"{xPos} 0.28",
                        AnchorMax = $"{xPos + squareWidthRelative} {0.28 + squareHeightRelative}"
                    },
                    Image = { Color = "0.5 0.5 0.5 0.6" }
                }, Layer, panelName);

                // Добавляем изображение, если есть приз и изображение валидно
                if (i < config.Prizes.Count && IsImageValid(config.Prizes[i].Image))
                {
                    container.Add(new CuiElement
                    {
                        Parent = panelName,
                        Components =
                        {
                            new CuiRawImageComponent { Url = GetImageUrl(config.Prizes[i].Image) },
                            new CuiRectTransformComponent { 
                                AnchorMin = "0.05 0.2",
                                AnchorMax = "0.95 1"
                            }
                        }
                    });
                }

                // Добавляем цветную полоску
                string color = GetRarityColor(config.Prizes[i].Chance);
                container.Add(new CuiPanel
                {
                    RectTransform = { 
                        AnchorMin = $"{xPos} {0.28 - 0.005f}",
                        AnchorMax = $"{xPos + squareWidthRelative} {0.28}"
                    },
                    Image = { Color = color }
                }, Layer);

                // Добавляем название приза под полоской
                container.Add(new CuiLabel
                {
                    RectTransform = { 
                        AnchorMin = "0 -0.20",
                        AnchorMax = "1 -0.05"
                    },
                    Text = { 
                        Text = config.Prizes[i].Name,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, panelName);
            }

            // Нижний ряд (11 квадратов)
            for (int i = 0; i < 11; i++)
            {
                float xPos = startX + (i * (squareWidth + spacing) / 900f);
                string panelName = $"Square_{i + 11}";
                
                // Добавляем панель
                container.Add(new CuiPanel
                {
                    RectTransform = { 
                        AnchorMin = $"{xPos} 0.08",
                        AnchorMax = $"{xPos + squareWidthRelative} {0.08 + squareHeightRelative}"
                    },
                    Image = { Color = "0.5 0.5 0.5 0.6" }
                }, Layer, panelName);

                // Добавляем изображение, если есть приз и изображение валидно
                if ((i + 11) < config.Prizes.Count && IsImageValid(config.Prizes[i + 11].Image))
                {
                    container.Add(new CuiElement
                    {
                        Parent = panelName,
                        Components =
                        {
                            new CuiRawImageComponent { Url = GetImageUrl(config.Prizes[i + 11].Image) },
                            new CuiRectTransformComponent { 
                                AnchorMin = "0.05 0.2",
                                AnchorMax = "0.95 1"
                            }
                        }
                    });
                }

                // Добавляем цветную полоску
                string color = GetRarityColor(config.Prizes[i + 11].Chance);
                container.Add(new CuiPanel
                {
                    RectTransform = { 
                        AnchorMin = $"{xPos} {0.08 - 0.005f}",
                        AnchorMax = $"{xPos + squareWidthRelative} {0.08}"
                    },
                    Image = { Color = color }
                }, Layer);

                // Добавляем название приза под полоской
                container.Add(new CuiLabel
                {
                    RectTransform = { 
                        AnchorMin = "0 -0.20",
                        AnchorMax = "1 -0.05"
                    },
                    Text = { 
                        Text = config.Prizes[i + 11].Name,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, panelName);
            }

            // Кнопка "КУПИТЬ ПРОКРУТКУ"
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.9 0.58", AnchorMax = "0.9 0.58", OffsetMin = "-60 -18", OffsetMax = "60 18" },
                Button = { Color = "0 0 0 0.7", Command = "rulette.buyspin" },
                Text = { 
                    Text = $"КУПИТЬ ПРОКРУТКУ {config.SpinPrice}₽",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer);

            // Кнопка "ЗАКРЫТЬ"
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.937 1.055", AnchorMax = "0.937 1.055", OffsetMin = "-50 -18", OffsetMax = "50 18" },
                Button = { Color = "0 0 0 0.7", Command = "rulette.close" },
                Text = { 
                    Text = "ЗАКРЫТЬ",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer);

            // Кнопка "ИНВЕНТАРЬ"
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1 0.58", AnchorMax = "0.1 0.58", OffsetMin = "-60 -18", OffsetMax = "60 18" },
                Button = { Color = "0 0 0 0.7", Command = "rulette.inventory" },
                Text = { 
                    Text = "ИНВЕНТАРЬ",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer);

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("rulette.close")]
        private void CmdCloseMenu(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            // Удаляем все UI элементы
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer + ".Background");
            CuiHelper.DestroyUi(player, Layer + ".Cursor");
            CuiHelper.DestroyUi(player, Layer + ".Roulette");
            CuiHelper.DestroyUi(player, "Overlay");
            CuiHelper.DestroyUi(player, "Hud");
            CuiHelper.DestroyUi(player, "BRulette.Title");
            CuiHelper.DestroyUi(player, "BRulette.Balance");
            CuiHelper.DestroyUi(player, "BRulette.Content");
            CuiHelper.DestroyUi(player, "BRulette.Places");
            CuiHelper.DestroyUi(player, "BRulette.Remaining");
            CuiHelper.DestroyUi(player, "BRulette.Win");
            CuiHelper.DestroyUi(player, "RuletteInventory");

            // Сбрасываем состояние курсора
            player.Command("cursor.lock", "false");
            player.Command("cursor.visible", "false");
            player.Command("cursor.unlock");
            player.Command("player.look", "1");
            player.Command("player.move", "1");
            player.Command("player.attack", "1");
            player.Command("player.jump", "1");
            player.Command("player.sprint", "1");
            player.Command("player.duck", "1");
        }

        private void ShowInventoryMenu(BasePlayer player)
        {
            if (player == null) return;

            var ui = new CuiElementContainer();

            // Синий фон на весь экран
            ui.Add(new CuiElement
            {
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent { Color = "0.1 0.1 0.3 0.95" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                },
                Name = "RuletteInventory"
            });

            // Заголовок и баланс
            ui.Add(new CuiElement
            {
                Parent = "RuletteInventory",
                Components =
                {
                    new CuiTextComponent { Text = "ИНВЕНТАРЬ РУЛЕТКИ", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.4 0.9", AnchorMax = "0.6 0.95" }
                }
            });

            ui.Add(new CuiElement
            {
                Parent = "RuletteInventory",
                Components =
                {
                    new CuiTextComponent { Text = $"Баланс: {GetPlayerBalance(player)}₽", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.4 0.85", AnchorMax = "0.6 0.9" }
                }
            });

            // Кнопка "ЗАКРЫТЬ"
            ui.Add(new CuiButton
            {
                Button = { Command = "rulette.close", Color = "0.7 0.3 0.3 0.9" },
                Text = { Text = "ЗАКРЫТЬ", FontSize = 14, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.85 0.9", AnchorMax = "0.95 0.95" }
            }, "RuletteInventory");

            // Отображение предметов
            if (!playerInventories.ContainsKey(player.userID) || playerInventories[player.userID].Count == 0)
            {
                ui.Add(new CuiElement
                {
                    Parent = "RuletteInventory",
                    Components =
                    {
                        new CuiTextComponent { Text = "У вас нет предметов", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.4 0.5", AnchorMax = "0.6 0.55" }
                    }
                });
            }
            else
            {
                var items = playerInventories[player.userID].Keys.ToList();
                float startY = 0.8f;
                float itemHeight = 0.15f;
                float spacing = 0.02f;
                int itemsPerRow = 4;
                float itemWidth = (0.9f - (itemsPerRow + 1) * spacing) / itemsPerRow;

                for (int i = 0; i < items.Count; i++)
                {
                    string itemName = items[i];
                    int amount = playerInventories[player.userID][itemName];
                    var prize = config.Prizes.Find(p => p.Name == itemName);
                    if (prize == null) continue;

                    int row = i / itemsPerRow;
                    int col = i % itemsPerRow;
                    float x = spacing + col * (itemWidth + spacing);
                    float y = startY - row * (itemHeight + spacing);

                    // Панель предмета
                    string panelName = $"ItemPanel_{i}";
                    ui.Add(new CuiElement
                    {
                        Parent = "RuletteInventory",
                        Components =
                        {
                            new CuiImageComponent { Color = "0.2 0.2 0.4 0.9" },
                            new CuiRectTransformComponent { AnchorMin = $"{x} {y - itemHeight}", AnchorMax = $"{x + itemWidth} {y}" }
                        },
                        Name = panelName
                    });

                    // Изображение предмета (если есть)
                    if (!string.IsNullOrEmpty(prize.Image) && IsImageValid(prize.Image))
                    {
                        ui.Add(new CuiElement
                        {
                            Parent = panelName,
                            Components =
                            {
                                new CuiRawImageComponent { Url = GetImageUrl(prize.Image) },
                                new CuiRectTransformComponent { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.9" }
                            }
                        });
                    }

                    // Название и количество
                    ui.Add(new CuiElement
                    {
                        Parent = panelName,
                        Components =
                        {
                            new CuiTextComponent { Text = $"{itemName} x{amount}", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.1 0.25", AnchorMax = "0.9 0.35" }
                        }
                    });

                    // Кнопки "ПОЛУЧИТЬ" и "ПРОДАТЬ"
                    ui.Add(new CuiButton
                    {
                        Button = { Command = $"rulette.claim {i}", Color = "0.2 0.8 0.2 0.9" },
                        Text = { Text = "ПОЛУЧИТЬ", FontSize = 10, Align = TextAnchor.MiddleCenter },
                        RectTransform = { AnchorMin = "0.1 0.1", AnchorMax = "0.45 0.2" }
                    }, panelName);

                    ui.Add(new CuiButton
                    {
                        Button = { Command = $"rulette.sell {i}", Color = "0.8 0.2 0.2 0.9" },
                        Text = { Text = "ПРОДАТЬ", FontSize = 10, Align = TextAnchor.MiddleCenter },
                        RectTransform = { AnchorMin = "0.55 0.1", AnchorMax = "0.9 0.2" }
                    }, panelName);
                }
            }

            CuiHelper.DestroyUi(player, "RuletteInventory");
            CuiHelper.AddUi(player, ui);
        }
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            Instance = this;
            LoadData();
            
            // Создаем директорию для изображений, если её нет
            var imagePath = $"{Interface.Oxide.DataDirectory}/{IMAGE_FOLDER}";
            if (!System.IO.Directory.Exists(imagePath))
            {
                System.IO.Directory.CreateDirectory(imagePath);
                PrintWarning($"Создана директория для изображений: {imagePath}");
                PrintWarning("Пожалуйста, поместите изображения предметов в эту папку:");
                PrintWarning($"{imagePath}");
            }
            
            ImageUi.Initialize();
            Init(); // Добавляем вызов Init()
            PrintWarning("BRulette полностью инициализирован");
        }

        private void Unload()
        {
            // Очищаем UI у всех игроков при выгрузке плагина
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, Layer + ".Background");
                CuiHelper.DestroyUi(player, Layer + ".Cursor");
                CuiHelper.DestroyUi(player, "Overlay");
                CuiHelper.DestroyUi(player, "Hud");
                player.Command("cursor.unlock");
            }
            
            ImageUi.Unload();
        }

        private bool IsImageValid(string imageName)
        {
            if (string.IsNullOrEmpty(imageName)) return false;
            return imageCache.ContainsKey(imageName);
        }

        private string GetImageUrl(string imageName)
        {
            return ImageUi.GetImage(imageName);
        }
        #endregion

        private void StartSpinning(BasePlayer player)
        {
            RouletteState state;

            if (playerStates.ContainsKey(player.userID))
            {
                state = playerStates[player.userID];
                state.LastWinIndex = state.LastWinIndex;
            }
            else
            {
                state = new RouletteState();
                playerStates[player.userID] = state;
            }

            state.IsSpinning = true;
            state.SelectedPrize = SelectPrize();
            state.SpinTimer?.Destroy();

            // Запускаем анимацию прокрутки
            ServerMgr.Instance.StartCoroutine(DrawLine(player, GetRandomLine(), true));

            // Обновляем UI
            UpdateRouletteUI(player, state);
        }

        private Prize SelectPrize()
        {
            float totalChance = config.Prizes.Sum(p => p.Chance);
            float random = UnityEngine.Random.Range(0f, totalChance);
            float currentSum = 0f;

            foreach (var prize in config.Prizes)
            {
                currentSum += prize.Chance;
                if (random <= currentSum)
                    return prize;
            }

            return config.Prizes[0]; // Fallback
        }

        private List<Prize> GetRandomLine()
        {
            List<Prize> prizeLine = new List<Prize>();
            // Добавляем случайные призы в начало линии
            for (int i = 0; i < 50; i++)
            {
                prizeLine.Add(config.Prizes[UnityEngine.Random.Range(0, config.Prizes.Count)]);
            }

            // Добавляем выигрышный приз
            var winningPrize = SelectPrize();
            prizeLine.Add(winningPrize);
            
            // Добавляем несколько призов после выигрышного для плавной остановки
            for (int i = 0; i < 5; i++)
            {
                prizeLine.Add(config.Prizes[UnityEngine.Random.Range(0, config.Prizes.Count)]);
            }

            return prizeLine;
        }

        private IEnumerator<object> DrawLine(BasePlayer player, List<Prize> prizeList, bool really = false)
        {
            List<Prize> localList = new List<Prize>();
            int totalSteps = prizeList.Count - 9; // Изменяем количество шагов для 9 призов

            for (int z = 0; z < totalSteps; z++)
            {
                localList = prizeList.Skip(z).Take(9).ToList(); // Берем 9 призов вместо 5

                CuiElementContainer container = new CuiElementContainer();
                foreach (var check in localList.Select((i, t) => new { A = i, B = t }))
                {
                    try
                    {
                        CuiHelper.DestroyUi(player, Layer + $".Prize.{check.B}.Img");
                        CuiHelper.DestroyUi(player, Layer + $".Prize.{check.B}");

                        var kitMargin = 0.11f; // Уменьшаем расстояние между призами для плотного размещения
                        float startX = 0.06f; // Корректируем начальную позицию
                        float xPos = startX + (check.B * kitMargin);
                        float height = 0.25f;
                        float yCenter = 0.87f;
                        float yMin = yCenter - height/2;
                        float yMax = yCenter + height/2;

                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{xPos} {yMin}",
                                AnchorMax = $"{xPos} {yMax}",
                                OffsetMin = "-50 0",
                                OffsetMax = "50 0"
                            },
                            Button = { Color = "0 0 0 0" },
                            Text = { Text = "" }
                        }, Layer, Layer + $".Prize.{check.B}");

                        container.Add(new CuiElement
                        {
                            Parent = Layer + $".Prize.{check.B}",
                            Name = Layer + $".Prize.{check.B}.Img",
                            Components =
                            {
                                new CuiRawImageComponent { Url = check.A.Image },
                                new CuiRectTransformComponent { 
                                    AnchorMin = "0 0.15",
                                    AnchorMax = "1 0.95"
                                }
                            }
                        });

                        container.Add(new CuiLabel
                        {
                            RectTransform = { 
                                AnchorMin = "0 0",
                                AnchorMax = "1 0.15"
                            },
                            Text = { 
                                Text = check.A.Name,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 8,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                        }, Layer + $".Prize.{check.B}");
                    }
                    catch (NullReferenceException e)
                    {
                        PrintWarning(e.StackTrace);
                    }
                }
                CuiHelper.AddUi(player, container);
                container.Clear();

                Effect x = new Effect("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", player, 0, new Vector3(), new Vector3());
                EffectNetwork.Send(x, player.Connection);

                float delay = (float)(((float)Math.Pow(1.9, z - 120) / 40000) + (float)z * 0.006);
                yield return new WaitForSeconds(delay);
            }

            if (really)
            {
                CuiHelper.DestroyUi(player, Layer + ".Overlay");
                PlayWinEffect(player);

                var winningPrize = localList[4]; // Центральный приз (5-й по счету)
                GivePrize(player, winningPrize);
                
                if (playerStates.ContainsKey(player.userID))
                {
                    playerStates[player.userID].IsSpinning = false;
                    UpdateRouletteUI(player, playerStates[player.userID]);
                }
            }
        }

        private void UpdateRouletteUI(BasePlayer player, RouletteState state)
        {
            if (player == null) return;

            // Проверяем и перезагружаем изображения, если нужно
            if (ImageUi.GetImages().Count == 0)
            {
                ImageUi.Initialize();
            }

            CuiHelper.DestroyUi(player, Layer + ".Roulette");
            CuiHelper.DestroyUi(player, Layer + ".SpinButton");
            CuiHelper.DestroyUi(player, Layer + ".Remaining");

            CuiElementContainer container = new CuiElementContainer();
            
            // Создаем панель-контейнер для барабана
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.78", AnchorMax = "1 0.96" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Roulette");

            // Добавляем белую вертикальную линию-указатель в центре
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.78", AnchorMax = "0.5 0.96", OffsetMin = "-1 0", OffsetMax = "1 0" },
                Image = { Color = "1 1 1 1" }
            }, Layer);

            // Обновляем кнопку КРУТИТЬ
            string spinText = state.IsSpinning ? "КРУТИТСЯ..." : (playerSpins.ContainsKey(player.userID) && playerSpins[player.userID] > 0 ? "КРУТИТЬ" : "НЕТ ПРОКРУТОК");
            string spinColor = state.IsSpinning ? "0.5 0.5 0.5 0.8" : (playerSpins.ContainsKey(player.userID) && playerSpins[player.userID] > 0 ? "0.2 0.8 0.2 0.8" : "0.5 0.5 0.5 0.8");
            string spinCommand = state.IsSpinning ? "" : (playerSpins.ContainsKey(player.userID) && playerSpins[player.userID] > 0 ? "rulette.center" : "");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.70", AnchorMax = "0.5 0.70", OffsetMin = "-100 -18", OffsetMax = "100 18" },
                Button = { 
                    Color = spinColor,
                    Command = spinCommand
                },
                Text = { 
                    Text = spinText,
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".SpinButton");

            // Обновляем количество прокруток
            int spins = playerSpins.ContainsKey(player.userID) ? playerSpins[player.userID] : 0;
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.63", AnchorMax = "0.5 0.63", OffsetMin = "-100 -20", OffsetMax = "100 20" },
                Text = { 
                    Text = $"Осталось прокруток: {spins}", 
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14, 
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".Remaining");

            CuiHelper.AddUi(player, container);
        }

        private void GivePrize(BasePlayer player, Prize prize)
        {
            if (!playerInventories.ContainsKey(player.userID))
                playerInventories[player.userID] = new Dictionary<string, int>();

            if (!playerInventories[player.userID].ContainsKey(prize.Name))
                playerInventories[player.userID][prize.Name] = 0;

            playerInventories[player.userID][prize.Name] += prize.Amount;
            SaveData();
        }

        private string GetRarityColor(float chance)
        {
            if (chance >= 86f) return "0.5 0.5 0.5 1"; // Серый (обычный)
            if (chance >= 65f) return "0.2 0.6 1 1"; // Голубой (редкий)
            if (chance >= 35f) return "0.6 0.2 1 1"; // Фиолетовый (эпический)
            return "1 0.2 0.2 1"; // Красный (легендарный)
        }

        private int GetPlayerBalance(BasePlayer player)
        {
            if (!playerBalances.ContainsKey(player.userID))
                playerBalances[player.userID] = 0;
            return playerBalances[player.userID];
        }

        private void AddToPlayerBalance(BasePlayer player, int amount)
        {
            if (!playerBalances.ContainsKey(player.userID))
                playerBalances[player.userID] = 0;
            playerBalances[player.userID] += amount;
            SaveData();
        }

        private bool RemoveFromPlayerBalance(BasePlayer player, int amount)
        {
            if (!playerBalances.ContainsKey(player.userID) || playerBalances[player.userID] < amount)
                return false;
            playerBalances[player.userID] -= amount;
            SaveData();
            return true;
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/balances", playerBalances);
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/inventories", playerInventories);
        }

        private void LoadData()
        {
            playerBalances = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>($"{Name}/balances") 
                            ?? new Dictionary<ulong, int>();
            playerInventories = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, int>>>($"{Name}/inventories") 
                            ?? new Dictionary<ulong, Dictionary<string, int>>();
        }

        private bool HasItem(BasePlayer player, string itemName, int amount)
        {
            if (!playerInventories.ContainsKey(player.userID) || 
                !playerInventories[player.userID].ContainsKey(itemName) || 
                playerInventories[player.userID][itemName] < amount)
                return false;
            return true;
        }

        private void RemoveItem(BasePlayer player, string itemName, int amount)
        {
            if (!HasItem(player, itemName, amount)) return;
            
            playerInventories[player.userID][itemName] -= amount;
            if (playerInventories[player.userID][itemName] <= 0)
                playerInventories[player.userID].Remove(itemName);
            
            SaveData();
        }

        private void PlayWinEffect(BasePlayer player)
        {
            Effect effect = new Effect("assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab", player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(effect, player.Connection);

            Effect stackEffect = new Effect("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(stackEffect, player.Connection);
        }

        private void Init()
        {
            // Регистрируем команду
            cmd.AddChatCommand("roll", this, nameof(CmdRulette));
            PrintWarning("Плагин BRulette инициализирован, команда /roll зарегистрирована");
        }
    }
} 