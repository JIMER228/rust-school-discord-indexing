using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence; // Для IPlayer
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;
using Network;         // Для NetworkableId
using Newtonsoft.Json; // Для атрибутов конфигурации
using Oxide.Game.Rust.Cui;
using System.IO;
using System.Text;
using UnityEngine.UI;

namespace Oxide.Plugins
{
    [Info("CoinFinder", "YourName/ChatGPT", "1.4.4")]
    [Description("Find coins and exchange them directly via GameStores Web API.")]
    partial class CoinFinder : RustPlugin
    {
        #region Fields

        private static Configuration _config;
        private const string PermissionUse = "coinfinder.use";
        private const string PermissionExchange = "coinfinder.exchange";

        // HashSet для быстрой проверки префабов контейнеров
        private HashSet<string> _lootableContainerPrefabs;
        // HashSet для хранения ID уже залутанных контейнеров (используем ulong)
        private readonly HashSet<ulong> _handledContainerInstanceIds = new HashSet<ulong>();
        // Базовый URL API GameStores
        private const string GameStoresApiBaseUrl = "https://gamestores.ru/api";

        #endregion

        #region Configuration

        private class Configuration
        {
            // --- Настройки поиска монет ---
            [JsonProperty(PropertyName = "1. Шанс найти монету (0.0 - 1.0)", Order = 1)]
            public float DropChance { get; set; } = 0.1f; // 10%

            [JsonProperty(PropertyName = "2. Базовый предмет для монеты (Shortname)", Order = 2)]
            public string CoinItemShortname { get; set; } = "branch"; // Ветка

            [JsonProperty(PropertyName = "3. ID скина монеты (0 = нет скина)", Order = 3)]
            public ulong CoinSkinID { get; set; } = 0; // Установите реальный Skin ID

            [JsonProperty(PropertyName = "4. Отображаемое имя монеты в чате", Order = 4)]
            public string CustomCoinName { get; set; } = "Монета";

            [JsonProperty(PropertyName = "5. Минимальное количество монет за раз", Order = 5)]
            public int MinCoinAmount { get; set; } = 1;

            [JsonProperty(PropertyName = "6. Максимальное количество монет за раз", Order = 6)]
            public int MaxCoinAmount { get; set; } = 5;

            [JsonProperty(PropertyName = "7. Показывать сообщение о находке монет", Order = 7)]
            public bool ShowFoundMessage { get; set; } = true;

            [JsonProperty(PropertyName = "8. Список контейнеров для поиска (Shortnames)", Order = 8)]
            public List<string> LootableContainers { get; set; } = new List<string>
             {
                "crate_normal", "crate_normal_2", "crate_tools", "crate_mine", "crate_basic",
                "crate_elite", "loot_barrel_1", "loot_barrel_2", "oil_barrel", "foodbox",
                "medical_storage", "codelockedhackablecrate", "supply_drop", "heli_crate",
                "bradley_crate"
                // Добавляйте/удаляйте по необходимости
             };

            // --- Настройки обмена через GameStores API ---
            [JsonProperty(PropertyName = "9. Включить команду обмена", Order = 9)]
            public bool EnableExchangeCommand { get; set; } = true;

            [JsonProperty(PropertyName = "10. Название команды обмена в чате", Order = 10)]
            public string ExchangeCommandName { get; set; } = "exchange";

            [JsonProperty(PropertyName = "11. ID вашего магазина GameStores", Order = 11)]
            public ulong GameStoresShopId { get; set; } = 0; // <-- ВАЖНО: Установите ваш ID!

            [JsonProperty(PropertyName = "12. Секретный ключ вашего магазина GameStores", Order = 12)]
            public string GameStoresSecretKey { get; set; } = string.Empty; // <-- ВАЖНО: Установите ваш ключ!

            [JsonProperty(PropertyName = "13. Название операции для API GameStores (параметр mess)", Order = 13)]
            public string GameStoresActionName { get; set; } = "Обмен монет CoinFinder"; // Название операции для API
        }

        // Загрузка конфигурации
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception("Config file is empty or invalid.");
                _config.LootableContainers = _config.LootableContainers ?? new List<string>(); // Убедимся, что список не null
            }
            catch (Exception ex)
            {
                PrintError($"Failed to load config file: {ex.Message}. Loading default configuration.");
                LoadDefaultConfig(); // Загружаем дефолт, если чтение не удалось
            }

            // Проверяем обязательные поля API после загрузки (или создания дефолтного конфига)
            if (_config.EnableExchangeCommand && (_config.GameStoresShopId == 0 || string.IsNullOrEmpty(_config.GameStoresSecretKey)))
            {
                PrintWarning("Exchange command is enabled, but GameStores Shop ID or Secret Key is not set! Exchange will not work.");
            }

            // Сохраняем конфиг (для применения JsonProperty или добавления новых полей)
            SaveConfig();
            // Инициализируем HashSet для быстрой проверки контейнеров
            _lootableContainerPrefabs = new HashSet<string>(_config.LootableContainers);
        }

        // Загрузка конфигурации по умолчанию
        protected override void LoadDefaultConfig()
        {
            _config = new Configuration(); // Создаем новый объект с дефолтными значениями
            Puts("Creating default configuration file...");
        }

        // Сохранение конфигурации
        protected override void SaveConfig() => Config.WriteObject(_config, true); // true для красивого JSON

        #endregion

        #region Language API

        // Загрузка стандартных сообщений
        protected override void LoadDefaultMessages()
        {
            // Русский язык
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CoinFound"] = "Вы нашли <color=#ffd700>{0}</color> x <color=#add8e6>{1}</color>!",
                ["ExchangeUsage"] = "Используйте: <color=#ffa500>/{0}</color>",
                ["NoPermission"] = "<color=red>Ошибка:</color> У вас нет разрешения на использование этой команды.",
                ["ExchangeDisabled"] = "<color=orange>Информация:</color> Команда обмена отключена в конфигурации.",
                ["APINotConfigured"] = "<color=red>Ошибка:</color> Обмен временно недоступен. Администратор не настроил API магазина.",
                ["NoCoinsToExchange"] = "<color=orange>Информация:</color> У вас нет <color=#add8e6>{0}</color> для обмена.",
                ["ExchangeSuccess"] = "<color=lime>Успех:</color> Вы обменяли <color=#ffd700>{0}</color> x <color=#add8e6>{1}</color> на <color=#90ee90>{0}</color> кредитов! Баланс на сайте обновлен.",
                ["ExchangeWebApiError"] = "<color=red>Ошибка:</color> Не удалось связаться с API магазина или получен неверный ответ ({0}). Монеты не были списаны.",
                ["ExchangeApiRejected"] = "<color=red>Ошибка:</color> Магазин отклонил операцию (возможно, вы не авторизованы?). Монеты не были списаны.",
                ["InvalidCoinItem"] = "<color=red>Ошибка:</color> Предмет-монета ({0}) не найден в игре. Проверьте конфигурацию плагина."
            }, this, "ru");

            // Английский язык
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CoinFound"] = "You found <color=#ffd700>{0}</color> x <color=#add8e6>{1}</color>!",
                ["ExchangeUsage"] = "Usage: <color=#ffa500>/{0}</color>",
                ["NoPermission"] = "<color=red>Error:</color> You do not have permission to use this command.",
                ["ExchangeDisabled"] = "<color=orange>Info:</color> The exchange command is disabled in the configuration.",
                ["APINotConfigured"] = "<color=red>Error:</color> Exchange is temporarily unavailable. The store API is not configured.",
                ["NoCoinsToExchange"] = "<color=orange>Info:</color> You do not have any <color=#add8e6>{0}</color> to exchange.",
                ["ExchangeSuccess"] = "<color=lime>Success:</color> You exchanged <color=#ffd700>{0}</color> x <color=#add8e6>{1}</color> for <color=#90ee90>{0}</color> credits! The website balance has been updated.",
                ["ExchangeWebApiError"] = "<color=red>Error:</color> Could not contact the store API or received an invalid response ({0}). Your coins were not removed.",
                ["ExchangeApiRejected"] = "<color=red>Error:</color> The store rejected the transaction (maybe you are not logged in?). Your coins were not removed.",
                ["InvalidCoinItem"] = "<color=red>Error:</color> The coin item ({0}) was not found in the game. Please check the plugin configuration."
            }, this, "en");
        }

        // Получение локализованного сообщения
        private string GetLang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);

        #endregion

        #region Hooks & Init

        // Инициализация плагина
        private void Init()
        {
            // Регистрация разрешений
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionExchange, this);

            // Проверка базового предмета монеты при запуске
            ItemDefinition coinDef = ItemManager.FindItemDefinition(_config.CoinItemShortname);
            if (coinDef == null) PrintError($"Config Error: Coin base item shortname '{_config.CoinItemShortname}' is invalid!");
            else Puts($"Using base item '{coinDef.displayName.english}' ({_config.CoinItemShortname}) for coins.");

            // Предупреждение о нулевом Skin ID
            if (_config.CoinSkinID == 0) PrintWarning($"Config Warning: CoinSkinID is 0. Coins will be given without a custom skin.");
            else Puts($"Using Skin ID {_config.CoinSkinID} for coin items.");

            // Предупреждение о пустом списке контейнеров
            if (_lootableContainerPrefabs == null || _lootableContainerPrefabs.Count == 0) PrintWarning("Config Warning: The list of LootableContainers is empty. Coins will not drop from any containers.");

            // Регистрация команды чата, если включено
            if (_config.EnableExchangeCommand)
            {
                // Проверка настроек API при включенной команде
                if (_config.GameStoresShopId == 0 || string.IsNullOrEmpty(_config.GameStoresSecretKey))
                {
                    PrintWarning("Exchange command is enabled, but GameStores Shop ID or Secret Key is not configured in CoinFinder.json! The command '/" + _config.ExchangeCommandName + "' will not function correctly.");
                }
                AddCovalenceCommand(_config.ExchangeCommandName, nameof(CmdExchange));
                Puts($"Exchange command '/{_config.ExchangeCommandName}' enabled.");
            }
            else
            {
                Puts("Exchange command disabled.");
            }

            // Подписка на хуки для сброса статуса залутанных контейнеров
            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(OnContainerPopulate));
        }

        private void GUIARCADIA(Connection connection)
        {
            CUI.Root root = new CUI.Root("Overlay");
            {
                CUI.Element KfUMeF = root.AddPanel(
                    material: "assets/content/ui/uibackgroundblur.mat",
                    color: "0.2830189 0.2830189 0.2830189 0.3529412",
                    imageType: UnityEngine.UI.Image.Type.Simple,
                    anchorMin: "0.5 0.5",
                    anchorMax: "0.5 0.5",
                    offsetMin: "-640 -360",
                    offsetMax: "640 360",
                    name: "GUIARCADIA");
                KfUMeF.AddButton(
                    command: null,
                    close: "GUIARCADIA",
                    color: "1 1 1 0.5686275",
                    sprite: "assets/icons/exit.png",
                    material: "assets/content/ui/binocular_overlay.mat",
                    imageType: UnityEngine.UI.Image.Type.Simple,
                    anchorMin: "0.5 0.5",
                    anchorMax: "0.5 0.5",
                    offsetMin: "580 300",
                    offsetMax: "620 340"
                    /* name: "Button" */);
                {
                    CUI.Element UBcOal = KfUMeF.AddPanel(
                        material: "assets/content/ui/uibackgroundblur-notice.mat",
                        imageType: UnityEngine.UI.Image.Type.Simple,
                        anchorMin: "0.5 0.5",
                        anchorMax: "0.5 0.5",
                        offsetMin: "-500 -275",
                        offsetMax: "-300 335"
                        /* name: "Panel" */);
                    {
                        CUI.Element cJPiUB = UBcOal.AddButton(
                            command: null,
                            color: "1 1 1 0.3176471",
                            material: "assets/content/ui/binocular_overlay.mat",
                            imageType: UnityEngine.UI.Image.Type.Simple,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-75 225",
                            offsetMax: "75 275"
                            /* name: "Button" */);
                        cJPiUB.AddText(
                            text: "О СЕРВЕРЕ",
                            color: "0 0 0 1",
                            font: CUI.Font.PressStart2PRegular,
                            fontSize: 13,
                            align: TextAnchor.UpperLeft,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: "15 -70",
                            offsetMax: "145 30"
                            /* name: "Text" */);
                    }
                    {
                        CUI.Element OZEedb = UBcOal.AddButton(
                            command: null,
                            imageType: UnityEngine.UI.Image.Type.Simple,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-75 165",
                            offsetMax: "75 215"
                            /* name: "Button (2)" */);
                        OZEedb.AddText(
                            text: "Бинды",
                            color: "0 0 0 1",
                            font: CUI.Font.RobotoCondensedRegular,
                            align: TextAnchor.UpperLeft,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: "50 -65",
                            offsetMax: "150 35"
                            /* name: "Text" */);
                    }
                    {
                        CUI.Element KIPgNf = UBcOal.AddButton(
                            command: null,
                            imageType: UnityEngine.UI.Image.Type.Simple,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-75 105",
                            offsetMax: "75 155"
                            /* name: "Button (3)" */);
                        KIPgNf.AddText(
                            text: "Text",
                            font: CUI.Font.RobotoCondensedRegular,
                            align: TextAnchor.UpperLeft,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: "-50 -50",
                            offsetMax: "50 50"
                            /* name: "Text" */);
                    }
                    {
                        CUI.Element LPnPWo = UBcOal.AddButton(
                            command: null,
                            imageType: UnityEngine.UI.Image.Type.Simple,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-75 45",
                            offsetMax: "75 95"
                            /* name: "Button (4)" */);
                        LPnPWo.AddText(
                            text: "Text",
                            font: CUI.Font.RobotoCondensedRegular,
                            align: TextAnchor.UpperLeft,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: "-50 -50",
                            offsetMax: "50 50"
                            /* name: "Text" */);
                    }
                    {
                        CUI.Element zOhwte = UBcOal.AddButton(
                            command: null,
                            imageType: UnityEngine.UI.Image.Type.Simple,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-75 -15",
                            offsetMax: "75 35"
                            /* name: "Button (5)" */);
                        zOhwte.AddText(
                            text: "Text",
                            font: CUI.Font.RobotoCondensedRegular,
                            align: TextAnchor.UpperLeft,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: "-50 -50",
                            offsetMax: "50 50"
                            /* name: "Text" */);
                    }
                    {
                        CUI.Element VPOvds = UBcOal.AddButton(
                            command: null,
                            imageType: UnityEngine.UI.Image.Type.Simple,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-75 -75",
                            offsetMax: "75 -25"
                            /* name: "Button (6)" */);
                        VPOvds.AddText(
                            text: "Text",
                            font: CUI.Font.RobotoCondensedRegular,
                            align: TextAnchor.UpperLeft,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: "-50 -50",
                            offsetMax: "50 50"
                            /* name: "Text" */);
                    }
                    {
                        CUI.Element nojFnG = UBcOal.AddButton(
                            command: null,
                            imageType: UnityEngine.UI.Image.Type.Simple,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-75 -135",
                            offsetMax: "75 -85"
                            /* name: "Button (7)" */);
                        nojFnG.AddText(
                            text: "Text",
                            font: CUI.Font.RobotoCondensedRegular,
                            align: TextAnchor.UpperLeft,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: "-50 -50",
                            offsetMax: "50 50"
                            /* name: "Text" */);
                    }
                    {
                        CUI.Element yduuuu = UBcOal.AddButton(
                            command: null,
                            imageType: UnityEngine.UI.Image.Type.Simple,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-75 -195",
                            offsetMax: "75 -145"
                            /* name: "Button (8)" */);
                        yduuuu.AddText(
                            text: "Text",
                            font: CUI.Font.RobotoCondensedRegular,
                            align: TextAnchor.UpperLeft,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: "-50 -50",
                            offsetMax: "50 50"
                            /* name: "Text" */);
                    }
                    {
                        CUI.Element zdohTW = UBcOal.AddButton(
                            command: null,
                            imageType: UnityEngine.UI.Image.Type.Simple,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-75 -255",
                            offsetMax: "75 -205"
                            /* name: "Button (9)" */);
                        zdohTW.AddText(
                            text: "Text",
                            font: CUI.Font.RobotoCondensedRegular,
                            align: TextAnchor.UpperLeft,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: "-50 -50",
                            offsetMax: "50 50"
                            /* name: "Text" */);
                    }
                }
                KfUMeF.AddPanel(
                    color: "0.2196079 0.2196079 0.2196079 1",
                    imageType: UnityEngine.UI.Image.Type.Simple,
                    anchorMin: "0.5 0.5",
                    anchorMax: "0.5 0.5",
                    offsetMin: "-300 -275",
                    offsetMax: "420 335"
                    /* name: "Panel" */);
            }
            root.Render(connection);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null || !permission.UserHasPermission(player.UserIDString, PermissionUse)) return;

            StorageContainer container = entity as StorageContainer;
            if (container == null || container.net == null || !container.net.ID.IsValid || container.ShortPrefabName == null) return;

            if (!_lootableContainerPrefabs.Contains(container.ShortPrefabName)) return;

            ulong containerIdValue = container.net.ID.Value;

            // --- ИЗМЕНЕНИЕ ЛОГИКИ ---
            // 1. Проверяем, обрабатывали ли мы уже этот контейнер
            if (_handledContainerInstanceIds.Contains(containerIdValue))
            {
                // Если да, то больше ничего с ним не делаем в этом цикле жизни
                return;
            }

            // 2. Если не обрабатывали, СРАЗУ помечаем его как обработанный
            _handledContainerInstanceIds.Add(containerIdValue);
            // --- КОНЕЦ ИЗМЕНЕНИЯ ЛОГИКИ ---

            // 3. Теперь проверяем шанс (только для этой первой попытки)
            if (UnityEngine.Random.Range(0f, 1f) <= _config.DropChance)
            {
                // Если шанс сработал, выдаем монеты
                GiveCoins(player, containerIdValue);
                // Дополнительно помечать не нужно, он уже помечен как обработанный (_handledContainerInstanceIds.Add(containerIdValue) было выше)
            }
            // Если шанс не сработал, ничего не делаем, но контейнер уже помечен и больше проверяться не будет
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            // Проверяем, что сущность и ее сетевой ID существуют и валидны
            if (entity?.net != null && entity.net.ID.IsValid)
            {
                // Удаляем ID из списка обработанных контейнеров
                // Убедитесь, что здесь используется имя '_handledContainerInstanceIds'
                _handledContainerInstanceIds.Remove(entity.net.ID.Value);
            }
        }

        // Хук: Контейнер заполнен лутом (респавн)
        private void OnContainerPopulate(StorageContainer container)
        {
            // Удаляем ID из списка обработанных
            if (container?.net != null && container.net.ID.IsValid)
            {
                _handledContainerInstanceIds.Remove(container.net.ID.Value);
            }
        }

        // При выгрузке плагина очищаем список
        private void Unload()
        {
            _handledContainerInstanceIds.Clear();
            Puts("CoinFinder unloaded, cleared handled container list.");
        }

        #endregion

        #region Chat Command

        // Обработка команды обмена
        private void CmdExchange(IPlayer iplayer, string command, string[] args)
        {
            // 1. Проверка, включена ли команда
            if (!_config.EnableExchangeCommand)
            {
                iplayer.Reply(GetLang("ExchangeDisabled", iplayer.Id));
                return;
            }

            // 2. Проверка разрешения игрока
            if (!iplayer.HasPermission(PermissionExchange))
            {
                iplayer.Reply(GetLang("NoPermission", iplayer.Id));
                return;
            }

            // 3. Проверка конфигурации API
            if (_config.GameStoresShopId == 0 || string.IsNullOrEmpty(_config.GameStoresSecretKey))
            {
                iplayer.Reply(GetLang("APINotConfigured", iplayer.Id));
                // Дополнительно выводим ошибку в консоль для администратора
                PrintError("Exchange command failed: GameStores Shop ID or Secret Key is missing in the configuration.");
                return;
            }

            // 4. Получение объекта BasePlayer
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return; // Не должно происходить для команды из чата

            // 5. Проверка валидности предмета-монеты
            ItemDefinition coinItemDefinition = ItemManager.FindItemDefinition(_config.CoinItemShortname);
            if (coinItemDefinition == null)
            {
                iplayer.Reply(GetLang("InvalidCoinItem", iplayer.Id, _config.CoinItemShortname));
                PrintError($"Exchange command failed: Coin item definition '{_config.CoinItemShortname}' not found in ItemManager.");
                return;
            }

            // 6. Подсчет монет и сбор предметов для удаления
            int totalCoinsFound = 0;
            List<Item> coinsToRemove = new List<Item>(); // Собираем здесь, удаляем ПОСЛЕ успеха API

            // Ищем только в основном инвентаре
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                // Проверяем shortname И skin ID
                if (item.info.shortname == _config.CoinItemShortname && item.skin == _config.CoinSkinID)
                {
                    totalCoinsFound += item.amount;
                    coinsToRemove.Add(item); // Добавляем сам объект Item в список
                }
            }

            // 7. Проверка, есть ли монеты для обмена
            if (totalCoinsFound <= 0)
            {
                // Используем настроенное имя монеты для сообщения
                string coinNameForMsg = !string.IsNullOrEmpty(_config.CustomCoinName) ? _config.CustomCoinName : coinItemDefinition.displayName.english;
                iplayer.Reply(GetLang("NoCoinsToExchange", iplayer.Id, coinNameForMsg));
                return;
            }

            // 8. Формирование URL и отправка запроса к API
            string steamId = player.UserIDString; // SteamID64
            int amountToDeposit = totalCoinsFound; // Сумма для API
            // Экранируем текст для параметра 'mess' на случай спецсимволов
            string encodedActionName = Uri.EscapeDataString(_config.GameStoresActionName ?? string.Empty);
            string url = $"{GameStoresApiBaseUrl}?shop_id={_config.GameStoresShopId}&secret={_config.GameStoresSecretKey}&action=moneys&type=plus&steam_id={steamId}&amount={amountToDeposit}&mess={encodedActionName}";

            // Логирование запроса (можно закомментировать в продакшене)
            Puts($"DEBUG: Sending WebRequest to GameStores API for {player.displayName} ({steamId}), Amount: {amountToDeposit}. URL: {url}");

            webrequest.Enqueue(url, null, (code, response) =>
            {
                // --- Начало обработки ответа (асинхронно) ---

                // Проверяем, существует ли еще игрок
                if (player == null || !player.IsConnected)
                {
                    Puts($"DEBUG: Player {steamId} disconnected before GameStores API response arrived.");
                    return; // Игрок вышел
                }

                // Логирование ответа (можно закомментировать в продакшене)
                Puts($"DEBUG: GameStores API Response for {steamId}: Code={code}, Response='{response}'");

                // Проверяем успешность ответа
                if (code == 200 && response != null && response.Contains("success"))
                {
                    // --- УСПЕХ API ---
                    Puts($"GameStores API reported SUCCESS for {player.displayName} ({steamId}), Amount: {amountToDeposit}. Attempting to remove coins...");

                    // Удаляем монеты из инвентаря
                    int totalAmountRemoved = 0;
                    // Используем копию списка или итерируем осторожно, т.к. удаление меняет коллекцию
                    List<Item> itemsActuallyRemoved = new List<Item>(); // Для отладки, если нужно

                    for (int i = coinsToRemove.Count - 1; i >= 0; i--) // Итерация с конца для безопасного удаления
                    {
                        Item item = coinsToRemove[i];
                        // Проверяем, что предмет все еще валиден и у игрока
                        if (item != null && !item.isBroken && item.GetRootContainer() == player.inventory.containerMain && item.amount > 0)
                        {
                            int amountInStack = item.amount;
                            int amountNeeded = amountToDeposit - totalAmountRemoved;
                            int amountToRemoveFromStack = Math.Min(amountInStack, amountNeeded); // Берем сколько нужно, но не больше чем есть в стаке

                            if (amountToRemoveFromStack <= 0) continue; // Уже все забрали

                            item.UseItem(amountToRemoveFromStack); // Уменьшаем стак или удаляем предмет
                            totalAmountRemoved += amountToRemoveFromStack;
                            itemsActuallyRemoved.Add(item); // Для дебага

                            if (totalAmountRemoved >= amountToDeposit) break; // Забрали нужное количество
                        }
                    }

                    // Проверка совпадения удаленного и найденного количества
                    if (totalAmountRemoved != amountToDeposit)
                    {
                        PrintWarning($"Coin removal mismatch for {player.displayName}. Expected: {amountToDeposit}, Removed: {totalAmountRemoved}. Possible inventory change during API call or item removal issue.");
                    }
                    else
                    {
                        Puts($"Successfully removed {totalAmountRemoved} coins for {player.displayName}.");
                    }

                    // Сообщение игроку об успехе
                    string finalCoinName = !string.IsNullOrEmpty(_config.CustomCoinName) ? _config.CustomCoinName : coinItemDefinition.displayName.english;
                    iplayer.Reply(GetLang("ExchangeSuccess", iplayer.Id, totalAmountRemoved, finalCoinName)); // Сообщаем фактически удаленное кол-во
                }
                else
                {
                    // --- ОШИБКА API ---
                    // Монеты НЕ удаляем
                    string errorDetails;
                    string langKey;

                    if (code != 200) { errorDetails = $"HTTP Status {code}"; langKey = "ExchangeWebApiError"; }
                    else if (response == null) { errorDetails = "Empty Response"; langKey = "ExchangeWebApiError"; }
                    else { errorDetails = "API Rejected"; langKey = "ExchangeApiRejected"; } // Предполагаем отказ

                    // Сообщение игроку об ошибке
                    iplayer.Reply(GetLang(langKey, iplayer.Id, errorDetails));
                    // Лог ошибки на сервер
                    PrintError($"GameStores API exchange FAILED for {player.displayName} ({steamId}). Reason: {errorDetails}. Response: {response}. Coins were NOT removed.");
                }
                // --- Конец обработки ответа ---

            }, this); // Важно передать 'this'

            // Команда завершается здесь, но обработка ответа произойдет позже в callback'е
        }

        [ChatCommand("coinfinderui")]
        private void CmdOpenCoinFinderUI(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                Player.Message(player, GetLang("NoPermission", player.UserIDString));
                return;
            }

            GUIARCADIA(player.Connection);
            Puts($"CoinFinder UI shown to {player.displayName}");
        }
        #endregion

        #region Logic (GiveCoins)

        // Логика выдачи монет игроку
        private void GiveCoins(BasePlayer player, ulong containerNetIdValue) // Принимаем ID для возможного логирования
        {
            // Находим определение предмета монеты
            ItemDefinition coinItemDefinition = ItemManager.FindItemDefinition(_config.CoinItemShortname);
            // Проверка на валидность уже была в Init, но на всякий случай
            if (coinItemDefinition == null)
            {
                PrintError($"INTERNAL ERROR: Cannot find ItemDefinition for '{_config.CoinItemShortname}' in GiveCoins. Check Init logs.");
                return;
            }

            // Определяем случайное количество монет в заданном диапазоне
            int amount = UnityEngine.Random.Range(_config.MinCoinAmount, _config.MaxCoinAmount + 1); // +1 т.к. верхняя граница исключается
            if (amount <= 0) return; // Не выдаем 0 или меньше

            // Создаем предмет с указанным Skin ID
            Item coinItem = ItemManager.Create(coinItemDefinition, amount, _config.CoinSkinID);

            if (coinItem != null)
            {
                // Выдаем предмет в основной инвентарь игрока
                // GiveItem обработает переполнение (выбросит предмет рядом)
                player.inventory.GiveItem(coinItem, player.inventory.containerMain);

                // Отправляем сообщение игроку, если включено
                if (_config.ShowFoundMessage)
                {
                    // Используем CustomCoinName или стандартное имя
                    string displayName = !string.IsNullOrEmpty(_config.CustomCoinName)
                       ? _config.CustomCoinName
                       : (coinItem.info.displayName?.translated ?? coinItem.info.displayName?.english ?? _config.CoinItemShortname); // Запасной вариант - shortname

                    Player.Message(player, GetLang("CoinFound", player.UserIDString, amount, displayName));
                }

                // Опциональный лог успешной выдачи (раскомментировать для отладки)
                // Puts($"DEBUG: Gave {amount}x {_config.CoinItemShortname} (Skin:{_config.CoinSkinID}) to {player.displayName} from container {containerNetIdValue}");
            }
            else
            {
                // Ошибка создания предмета (маловероятно, но возможно)
                PrintError($"Failed to create coin item '{_config.CoinItemShortname}' with Skin ID {_config.CoinSkinID}. Check ItemDefinition and SkinID validity.");
            }
        }
        #endregion


    }
    #region 0xF UI Library 2.3.2
    partial class CoinFinder
    {
        public class CUI
        {
            public enum Font
            {
                RobotoCondensedBold,
                RobotoCondensedRegular,
                RobotoMonoRegular,
                DroidSansMono,
                PermanentMarker,
                PressStart2PRegular,
                LSD,
                NotoSansArabicBold,
                NotoSansArabicRegular,
                NotoSansHebrewBold,
            }

            private static readonly Dictionary<Font, string> FontToString = new Dictionary<Font, string>
            {
                { Font.RobotoCondensedBold, "RobotoCondensed-Bold.ttf" },
                { Font.RobotoCondensedRegular, "RobotoCondensed-Regular.ttf" },
                { Font.RobotoMonoRegular, "RobotoMono-Regular.ttf" },
                { Font.DroidSansMono, "DroidSansMono.ttf" },
                { Font.PermanentMarker, "PermanentMarker.ttf" },
                { Font.PressStart2PRegular, "PressStart2P-Regular.ttf" },
                { Font.LSD, "lcd.ttf" },
                { Font.NotoSansArabicBold, "_nonenglish/arabic/notosansarabic-bold.ttf" },
                { Font.NotoSansArabicRegular, "_nonenglish/arabic/notosansarabic-regular.ttf" },
                { Font.NotoSansHebrewBold, "_nonenglish/notosanshebrew-bold.ttf" },
            };

            public enum InputType
            {
                None,
                Default,
                HudMenuInput
            }

            private static readonly Dictionary<TextAnchor, string> TextAnchorToString = new Dictionary<TextAnchor, string>
            {
                { TextAnchor.UpperLeft, TextAnchor.UpperLeft.ToString() },
                { TextAnchor.UpperCenter, TextAnchor.UpperCenter.ToString() },
                { TextAnchor.UpperRight, TextAnchor.UpperRight.ToString() },
                { TextAnchor.MiddleLeft, TextAnchor.MiddleLeft.ToString() },
                { TextAnchor.MiddleCenter, TextAnchor.MiddleCenter.ToString() },
                { TextAnchor.MiddleRight, TextAnchor.MiddleRight.ToString() },
                { TextAnchor.LowerLeft, TextAnchor.LowerLeft.ToString() },
                { TextAnchor.LowerCenter, TextAnchor.LowerCenter.ToString() },
                { TextAnchor.LowerRight, TextAnchor.LowerRight.ToString() }
            };

            private static readonly Dictionary<VerticalWrapMode, string> VWMToString = new Dictionary<VerticalWrapMode, string>
            {
                { VerticalWrapMode.Truncate, VerticalWrapMode.Truncate.ToString() },
                { VerticalWrapMode.Overflow, VerticalWrapMode.Overflow.ToString() },
            };

            private static readonly Dictionary<Image.Type, string> ImageTypeToString = new Dictionary<Image.Type, string>
            {
                { Image.Type.Simple, Image.Type.Simple.ToString() },
                { Image.Type.Sliced, Image.Type.Sliced.ToString() },
                { Image.Type.Tiled, Image.Type.Tiled.ToString() },
                { Image.Type.Filled, Image.Type.Filled.ToString() },
            };

            private static readonly Dictionary<InputField.LineType, string> LineTypeToString = new Dictionary<InputField.LineType, string>
            {
                { InputField.LineType.MultiLineNewline, InputField.LineType.MultiLineNewline.ToString() },
                { InputField.LineType.MultiLineSubmit, InputField.LineType.MultiLineSubmit.ToString() },
                { InputField.LineType.SingleLine, InputField.LineType.SingleLine.ToString() },
            };

            private static readonly Dictionary<ScrollRect.MovementType, string> MovementTypeToString = new Dictionary<ScrollRect.MovementType, string>
            {
                { ScrollRect.MovementType.Unrestricted, ScrollRect.MovementType.Unrestricted.ToString() },
                { ScrollRect.MovementType.Elastic, ScrollRect.MovementType.Elastic.ToString() },
                { ScrollRect.MovementType.Clamped, ScrollRect.MovementType.Clamped.ToString() },
            };


            private static readonly Dictionary<TimerFormat, string> TimerFormatToString = new Dictionary<TimerFormat, string>
            {
                { TimerFormat.None, TimerFormat.None.ToString() },
                { TimerFormat.SecondsHundreth, TimerFormat.SecondsHundreth.ToString() },
                { TimerFormat.MinutesSeconds, TimerFormat.MinutesSeconds.ToString() },
                { TimerFormat.MinutesSecondsHundreth, TimerFormat.MinutesSecondsHundreth.ToString() },
                { TimerFormat.HoursMinutes, TimerFormat.HoursMinutes.ToString() },
                { TimerFormat.HoursMinutesSeconds, TimerFormat.HoursMinutesSeconds.ToString() },
                { TimerFormat.HoursMinutesSecondsMilliseconds, TimerFormat.HoursMinutesSecondsMilliseconds.ToString() },
                { TimerFormat.HoursMinutesSecondsTenths, TimerFormat.HoursMinutesSecondsTenths.ToString() },
                { TimerFormat.DaysHoursMinutes, TimerFormat.DaysHoursMinutes.ToString() },
                { TimerFormat.DaysHoursMinutesSeconds, TimerFormat.DaysHoursMinutesSeconds.ToString() },
                { TimerFormat.Custom, TimerFormat.Custom.ToString() },
            };

            public static class Defaults
            {
                public const string VectorZero = "0 0";
                public const string VectorOne = "1 1";
                public const string Color = "1 1 1 1";
                public const string OutlineColor = "0 0 0 1";
                public const string Sprite = "assets/content/ui/ui.background.tile.psd";
                public const string Material = "assets/content/ui/namefontmaterial.mat";
                public const string IconMaterial = "assets/icons/iconmaterial.mat";
                public const Image.Type ImageType = Image.Type.Simple;
                public const CUI.Font Font = CUI.Font.RobotoCondensedRegular;
                public const int FontSize = 14;
                public const TextAnchor Align = TextAnchor.UpperLeft;
                public const VerticalWrapMode VerticalOverflow = VerticalWrapMode.Overflow;
                public const InputField.LineType LineType = InputField.LineType.SingleLine;
            }

            public static Color GetColor(string colorStr)
            {
                return ColorEx.Parse(colorStr);
            }

            public static string GetColorString(Color color)
            {
                return string.Format("{0} {1} {2} {3}", color.r, color.g, color.b, color.a);
            }

            public static void AddUI(Connection connection, string json)
            {
                CommunityEntity.ServerInstance.ClientRPCEx<string>(new SendInfo
                {
                    connection = connection
                }, null, "AddUI", json);
            }

            private static void SerializeType(ICuiComponent component, JsonWriter jsonWriter)
            {
                jsonWriter.WritePropertyName("type");
                jsonWriter.WriteValue(component.Type);
            }

            private static void SerializeField(string key, object value, object defaultValue, JsonWriter jsonWriter)
            {
                if (value != null && !value.Equals(defaultValue))
                {
                    if (value is string && defaultValue != null && string.IsNullOrEmpty(value as string))
                        return;

                    jsonWriter.WritePropertyName(key);

                    if (value is ICuiComponent)
                        SerializeComponent(value as ICuiComponent, jsonWriter);
                    else
                        jsonWriter.WriteValue(value ?? defaultValue);
                }
            }


            private static void SerializeField(string key, CuiScrollbar scrollbar, JsonWriter jsonWriter)
            {
                const string defaultHandleSprite = "assets/content/ui/ui.rounded.tga";
                const string defaultHandleColor = "0.15 0.15 0.15 1";
                const string defaultHighlightColor = "0.17 0.17 0.17 1";
                const string defaultPressedColor = "0.2 0.2 0.2 1";
                const string defaultTrackSprite = "assets/content/ui/ui.background.tile.psd";
                const string defaultTrackColor = "0.09 0.09 0.09 1";

                if (scrollbar == null)
                    return;

                jsonWriter.WritePropertyName(key);
                jsonWriter.WriteStartObject();
                SerializeField("invert", scrollbar.Invert, false, jsonWriter);
                SerializeField("autoHide", scrollbar.AutoHide, false, jsonWriter);
                SerializeField("handleSprite", scrollbar.HandleSprite, defaultHandleSprite, jsonWriter);
                SerializeField("size", scrollbar.Size, 20f, jsonWriter);
                SerializeField("handleColor", scrollbar.HandleColor, defaultHandleColor, jsonWriter);
                SerializeField("highlightColor", scrollbar.HighlightColor, defaultHighlightColor, jsonWriter);
                SerializeField("pressedColor", scrollbar.PressedColor, defaultPressedColor, jsonWriter);
                SerializeField("trackSprite", scrollbar.TrackSprite, defaultTrackSprite, jsonWriter);
                SerializeField("trackColor", scrollbar.TrackColor, defaultTrackColor, jsonWriter);
                jsonWriter.WriteEndObject();
            }

            private static void SerializeComponent(ICuiComponent IComponent, JsonWriter jsonWriter)
            {
                const string vector2zero = "0 0";
                const string vector2one = "1 1";
                const string colorWhite = "1 1 1 1";
                const string backgroundTile = "assets/content/ui/ui.background.tile.psd";
                const string iconMaterial = "assets/icons/iconmaterial.mat";
                const string fontBold = "RobotoCondensed-Bold.ttf";
                const string defaultOutlineDistance = "1.0 -1.0";

                void SerializeType() => CUI.SerializeType(IComponent, jsonWriter);
                void SerializeField(string key, object value, object defaultValue) => CUI.SerializeField(key, value, defaultValue, jsonWriter);
                void SerializeScrollbar(string key, CuiScrollbar value) => CUI.SerializeField(key, value, jsonWriter);

                switch (IComponent.Type)
                {
                    case "RectTransform":
                        {
                            CuiRectTransformComponent component = IComponent as CuiRectTransformComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("anchormin", component.AnchorMin, vector2zero);
                            SerializeField("anchormax", component.AnchorMax, vector2one);
                            SerializeField("offsetmin", component.OffsetMin, vector2zero);
                            SerializeField("offsetmax", component.OffsetMax, vector2one);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.Image":
                        {
                            CuiImageComponent component = IComponent as CuiImageComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("sprite", component.Sprite, backgroundTile);
                            SerializeField("material", component.Material, iconMaterial);
                            SerializeField("imagetype", ImageTypeToString[component.ImageType], ImageTypeToString[Image.Type.Simple]);
                            SerializeField("png", component.Png, null);
                            SerializeField("itemid", component.ItemId, 0);
                            SerializeField("skinid", component.SkinId, 0UL);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.RawImage":
                        {
                            CuiRawImageComponent component = IComponent as CuiRawImageComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("sprite", component.Sprite, backgroundTile);
                            SerializeField("material", component.Material, iconMaterial);
                            SerializeField("url", component.Url, null);
                            SerializeField("png", component.Png, null);
                            SerializeField("steamid", component.SteamId, null);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.Text":
                        {
                            CuiTextComponent component = IComponent as CuiTextComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("text", component.Text, null);
                            SerializeField("font", component.Font, fontBold);
                            SerializeField("fontSize", component.FontSize, 14);
                            SerializeField("align", TextAnchorToString[component.Align], TextAnchorToString[TextAnchor.UpperLeft]);
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("verticalOverflow", VWMToString[component.VerticalOverflow], VWMToString[VerticalWrapMode.Truncate]);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.Button":
                        {
                            CuiButtonComponent component = IComponent as CuiButtonComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("sprite", component.Sprite, backgroundTile);
                            SerializeField("material", component.Material, iconMaterial);
                            SerializeField("imagetype", ImageTypeToString[component.ImageType], ImageTypeToString[Image.Type.Simple]);
                            SerializeField("command", component.Command, null);
                            SerializeField("close", component.Close, null);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.InputField":
                        {
                            CuiInputFieldComponent component = IComponent as CuiInputFieldComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("text", component.Text, null);
                            SerializeField("font", component.Font, fontBold);
                            SerializeField("fontSize", component.FontSize, 14);
                            SerializeField("align", TextAnchorToString[component.Align], TextAnchorToString[TextAnchor.UpperLeft]);
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("command", component.Command, null);
                            SerializeField("characterLimit", component.CharsLimit, 0);
                            SerializeField("lineType", LineTypeToString[component.LineType], LineTypeToString[InputField.LineType.SingleLine]);
                            SerializeField("readOnly", component.ReadOnly, false);
                            SerializeField("password", component.IsPassword, false);
                            SerializeField("needsKeyboard", component.NeedsKeyboard, false);
                            SerializeField("hudMenuInput", component.HudMenuInput, false);
                            SerializeField("autofocus", component.Autofocus, false);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.ScrollView":
                        {
                            CuiScrollViewComponent component = IComponent as CuiScrollViewComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("contentTransform", component.ContentTransform, null);
                            SerializeField("horizontal", component.Horizontal, false);
                            SerializeField("vertical", component.Vertical, false);
                            SerializeField("movementType", MovementTypeToString[component.MovementType], MovementTypeToString[ScrollRect.MovementType.Clamped]);
                            SerializeField("elasticity", component.Elasticity, 0.1f);
                            SerializeField("inertia", component.Inertia, false);
                            SerializeField("decelerationRate", component.DecelerationRate, 0.135f);
                            SerializeField("scrollSensitivity", component.ScrollSensitivity, 1f);
                            SerializeScrollbar("horizontalScrollbar", component.HorizontalScrollbar);
                            SerializeScrollbar("verticalScrollbar", component.VerticalScrollbar);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.Outline":
                        {
                            CuiOutlineComponent component = IComponent as CuiOutlineComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("distance", component.Distance, defaultOutlineDistance);
                            SerializeField("useGraphicAlpha", component.UseGraphicAlpha, false);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "Countdown":
                        {
                            CuiCountdownComponent component = IComponent as CuiCountdownComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("endTime", component.EndTime, 0f);
                            SerializeField("startTime", component.StartTime, 0f);
                            SerializeField("step", component.Step, 1f);
                            SerializeField("interval", component.Interval, 1f);
                            SerializeField("timerFormat", TimerFormatToString[component.TimerFormat], TimerFormatToString[TimerFormat.None]);
                            SerializeField("numberFormat", component.NumberFormat, "0.####");
                            SerializeField("destroyIfDone", component.DestroyIfDone, true);
                            SerializeField("command", component.Command, null);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "NeedsKeyboard":
                    case "NeedsCursor":
                        {
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            jsonWriter.WriteEndObject();
                            break;
                        }
                }
            }


            [JsonObject(MemberSerialization.OptIn)]
            public class Element : CuiElement
            {
                public new string Name { get; set; } = null;

                public Element ParentElement { get; set; }
                public virtual List<Element> Container => ParentElement?.Container;
                public ComponentList Components { get; set; } = new ComponentList();

                [JsonProperty("name")]
                public string JsonName
                {
                    get
                    {
                        if (Name == null)
                        {
                            string result = this.GetHashCode().ToString();
                            if (ParentElement != null)
                                result.Insert(0, ParentElement.JsonName);
                            return result.GetHashCode().ToString();
                        }
                        return Name;
                    }
                }

                public Element() { }
                public Element(Element parent)
                {
                    AssignParent(parent);
                }

                public CUI.Element AssignParent(Element parent)
                {
                    if (parent == null)
                        return this;

                    ParentElement = parent;
                    Parent = ParentElement.JsonName;
                    return this;
                }

                public Element AddDestroy(string elementName)
                {
                    this.DestroyUi = elementName;
                    return this;
                }

                public Element AddDestroySelfAttribute()
                {
                    return AddDestroy(this.Name);
                }

                public virtual void WriteJson(JsonWriter jsonWriter)
                {
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("name");
                    jsonWriter.WriteValue(this.JsonName);
                    if (!string.IsNullOrEmpty(Parent))
                    {
                        jsonWriter.WritePropertyName("parent");
                        jsonWriter.WriteValue(this.Parent);
                    }
                    if (!string.IsNullOrEmpty(this.DestroyUi))
                    {
                        jsonWriter.WritePropertyName("destroyUi");
                        jsonWriter.WriteValue(this.DestroyUi);
                    }
                    if (this.Update)
                    {
                        jsonWriter.WritePropertyName("update");
                        jsonWriter.WriteValue(this.Update);
                    }
                    if (this.FadeOut > 0f)
                    {
                        jsonWriter.WritePropertyName("fadeOut");
                        jsonWriter.WriteValue(this.FadeOut);
                    }
                    jsonWriter.WritePropertyName("components");
                    jsonWriter.WriteStartArray();
                    for (int i = 0; i < this.Components.Count; i++)
                    {
                        SerializeComponent(this.Components[i], jsonWriter);
                    }
                    jsonWriter.WriteEndArray();
                    jsonWriter.WriteEndObject();
                }

                public Element Add(Element element)
                {
                    if (element.ParentElement == null)
                        element.AssignParent(this);
                    Container.Add(element);
                    return element;
                }

                public Element AddEmpty(string name = null)
                {
                    return Add(new Element(this) { Name = name });
                }

                public Element AddUpdateElement(string name = null)
                {
                    Element element = AddEmpty(name);
                    element.Parent = null;
                    element.Update = true;
                    return element;
                }

                public Element AddText(
                    string text,
                    string color = Defaults.Color,
                    CUI.Font font = Defaults.Font,
                    int fontSize = Defaults.FontSize,
                    TextAnchor align = Defaults.Align,
                    VerticalWrapMode overflow = Defaults.VerticalOverflow,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateText(text, color, font, fontSize, align, overflow, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddOutlinedText(
                   string text,
                   string color = Defaults.Color,
                   CUI.Font font = Defaults.Font,
                   int fontSize = Defaults.FontSize,
                   TextAnchor align = Defaults.Align,
                   VerticalWrapMode overflow = Defaults.VerticalOverflow,
                   string outlineColor = Defaults.OutlineColor,
                   int outlineWidth = 1,
                   string anchorMin = Defaults.VectorZero,
                   string anchorMax = Defaults.VectorOne,
                   string offsetMin = Defaults.VectorZero,
                   string offsetMax = Defaults.VectorZero,
                   string name = null)
                {
                    return Add(ElementContructor.CreateOutlinedText(text, color, font, fontSize, align, overflow, outlineColor, outlineWidth, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddInputfield(
                    string command = null,
                    string text = "",
                    string color = Defaults.Color,
                    CUI.Font font = Defaults.Font,
                    int fontSize = Defaults.FontSize,
                    TextAnchor align = Defaults.Align,
                    InputField.LineType lineType = Defaults.LineType,
                    CUI.InputType inputType = CUI.InputType.Default,
                    bool @readonly = false,
                    bool autoFocus = false,
                    bool isPassword = false,
                    int charsLimit = 0,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateInputfield(command, text, color, font, fontSize, align, lineType, inputType, @readonly, autoFocus, isPassword, charsLimit, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddPanel(
                    string color = Defaults.Color,
                    string sprite = Defaults.Sprite,
                    string material = Defaults.Material,
                    Image.Type imageType = Defaults.ImageType,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    bool cursorEnabled = false,
                    bool keyboardEnabled = false,
                    string name = null)
                {
                    return Add(ElementContructor.CreatePanel(color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, cursorEnabled, keyboardEnabled, name));
                }

                public Element AddButton(
                   string command = null,
                   string close = null,
                   string color = Defaults.Color,
                   string sprite = Defaults.Sprite,
                   string material = Defaults.Material,
                   Image.Type imageType = Defaults.ImageType,
                   string anchorMin = Defaults.VectorZero,
                   string anchorMax = Defaults.VectorOne,
                   string offsetMin = Defaults.VectorZero,
                   string offsetMax = Defaults.VectorZero,
                   string name = null)
                {
                    return Add(ElementContructor.CreateButton(command, close, color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddImage(
                    string content,
                    string color = Defaults.Color,
                    string material = null,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateImage(content, color, material, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddHImage(
                    string content,
                    string color = Defaults.Color,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return AddImage(content, color, Defaults.IconMaterial, anchorMin, anchorMax, offsetMin, offsetMax, name);
                }

                public Element AddIcon(
                    int itemId,
                    ulong skin = 0,
                    string color = Defaults.Color,
                    string sprite = Defaults.Sprite,
                    string material = Defaults.IconMaterial,
                    Image.Type imageType = Defaults.ImageType,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateIcon(itemId, skin, color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddContainer(
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public CUI.Element WithRect(
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero)
                {
                    if (this.Components.Count > 0)
                        this.Components.RemoveAll(c => c is CuiRectTransformComponent);
                    this.Components.Add(new CuiRectTransformComponent()
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    });
                    return this;
                }

                public CUI.Element WithFade(
                    float @in = 0f,
                    float @out = 0f)
                {
                    this.FadeOut = @out;
                    foreach (ICuiComponent component in this.Components)
                    {
                        if (component is CuiRawImageComponent rawImage)
                            rawImage.FadeIn = @in;
                        else if (component is CuiImageComponent image)
                            image.FadeIn = @in;
                        else if (component is CuiButtonComponent button)
                            button.FadeIn = @in;
                        else if (component is CuiTextComponent text)
                            text.FadeIn = @in;
                        else if (component is CuiCountdownComponent countdown)
                            countdown.FadeIn = @in;
                    }
                    return this;
                }

                public void AddComponents(params ICuiComponent[] components)
                {
                    this.Components.AddRange(components);
                }

                public CUI.Element WithComponents(params ICuiComponent[] components)
                {
                    AddComponents(components);
                    return this;
                }

                public CUI.Element CreateChild(string name = null, params ICuiComponent[] components)
                {
                    return CUI.Element.Create(name, components).AssignParent(this);
                }

                public static CUI.Element Create(string name = null, params ICuiComponent[] components)
                {
                    return new CUI.Element()
                    {
                        Name = name
                    }.WithComponents(components);
                }

                public class ComponentList : List<ICuiComponent>
                {
                    private Dictionary<Type, ICuiComponent> typeToComponent = new Dictionary<Type, ICuiComponent>();

                    public T Get<T>() where T : ICuiComponent
                    {
                        if (typeToComponent.TryGetValue(typeof(T), out ICuiComponent component))
                            return (T)component;
                        return default(T);
                    }

                    public new void Add(ICuiComponent item)
                    {
                        base.Add(item);
                        typeToComponent.Add(item.GetType(), item);
                    }

                    public new void Remove(ICuiComponent item)
                    {
                        base.Remove(item);
                        typeToComponent.Remove(item.GetType());
                    }

                    public new void Clear()
                    {
                        base.Clear();
                        typeToComponent.Clear();
                    }


                    public ComponentList AddImage(
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.Material,
                        Image.Type imageType = Defaults.ImageType,
                        int itemId = 0,
                        ulong skinId = 0UL)
                    {
                        Add(new CuiImageComponent
                        {
                            Color = color,
                            Sprite = sprite,
                            Material = material,
                            ImageType = imageType,
                            ItemId = itemId,
                            SkinId = skinId,
                        });
                        return this;
                    }

                    public ComponentList AddRawImage(
                        string content,
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.IconMaterial)
                    {
                        CuiRawImageComponent rawImageComponent = new CuiRawImageComponent
                        {
                            Color = color,
                            Sprite = sprite,
                            Material = material,
                        };
                        if (!string.IsNullOrEmpty(content))
                        {
                            if (content.Contains("://"))
                                rawImageComponent.Url = content;
                            else if (content.IsNumeric())
                            {
                                if (content.IsSteamId())
                                    rawImageComponent.SteamId = content;
                                else
                                    rawImageComponent.Png = content;
                            }
                        }
                        Add(rawImageComponent);
                        return this;
                    }

                    public ComponentList AddButton(
                        string command = null,
                        string close = null,
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.Material,
                        Image.Type imageType = Defaults.ImageType)
                    {
                        Add(new CuiButtonComponent
                        {
                            Command = command,
                            Close = close,
                            Color = color,
                            Sprite = sprite,
                            Material = material,
                            ImageType = imageType,
                        });
                        return this;
                    }

                    public ComponentList AddText(
                        string text,
                        string color = Defaults.Color,
                        CUI.Font font = Defaults.Font,
                        int fontSize = Defaults.FontSize,
                        TextAnchor align = Defaults.Align,
                        VerticalWrapMode overflow = Defaults.VerticalOverflow)
                    {
                        Add(new CuiTextComponent
                        {
                            Text = text,
                            Color = color,
                            Font = FontToString[font],
                            FontSize = fontSize,
                            Align = align,
                            VerticalOverflow = overflow
                        });
                        return this;
                    }

                    public ComponentList AddInputfield(
                        string command = null,
                        string text = "",
                        string color = Defaults.Color,
                        CUI.Font font = Defaults.Font,
                        int fontSize = Defaults.FontSize,
                        TextAnchor align = Defaults.Align,
                        InputField.LineType lineType = Defaults.LineType,
                        CUI.InputType inputType = CUI.InputType.Default,
                        bool @readonly = false,
                        bool autoFocus = false,
                        bool isPassword = false,
                        int charsLimit = 0)
                    {
                        Add(new CuiInputFieldComponent
                        {
                            Command = command,
                            Text = text,
                            Color = color,
                            Font = FontToString[font],
                            FontSize = fontSize,
                            Align = align,
                            NeedsKeyboard = inputType == InputType.Default,
                            HudMenuInput = inputType == InputType.HudMenuInput,
                            Autofocus = autoFocus,
                            ReadOnly = @readonly,
                            CharsLimit = charsLimit,
                            IsPassword = isPassword,
                            LineType = lineType
                        });
                        return this;
                    }

                    public ComponentList AddScrollView(
                        bool horizontal = false,
                        CuiScrollbar horizonalScrollbar = null,
                        bool vertical = false,
                        CuiScrollbar verticalScrollbar = null,
                        bool inertia = false,
                        ScrollRect.MovementType movementType = ScrollRect.MovementType.Clamped,
                        float decelerationRate = 0.135f,
                        float elasticity = 0.1f,
                        float scrollSensitivity = 1f,
                        string anchorMin = "0 0",
                        string anchorMax = "1 1",
                        string offsetMin = "0 0",
                        string offsetMax = "0 0")
                    {
                        Add(new CuiScrollViewComponent()
                        {
                            ContentTransform =
                                         new CuiRectTransformComponent()
                                         {
                                             AnchorMin = anchorMin,
                                             AnchorMax = anchorMax,
                                             OffsetMin = offsetMin,
                                             OffsetMax = offsetMax
                                         },
                            Horizontal = horizontal,
                            HorizontalScrollbar = horizonalScrollbar,
                            Vertical = vertical,
                            VerticalScrollbar = verticalScrollbar,
                            Inertia = inertia,
                            DecelerationRate = decelerationRate,
                            Elasticity = elasticity,
                            ScrollSensitivity = scrollSensitivity,
                            MovementType = movementType,
                        });
                        return this;
                    }

                    public ComponentList AddOutline(
                        string color = Defaults.OutlineColor,
                        int width = 1)
                    {
                        Add(new CuiOutlineComponent
                        {
                            Color = color,
                            Distance = string.Format("{0} -{0}", width)
                        });
                        return this;
                    }
                    public ComponentList AddNeedsKeyboard()
                    {
                        Add(new CuiNeedsKeyboardComponent());
                        return this;
                    }

                    public ComponentList AddNeedsCursor()
                    {
                        Add(new CuiNeedsCursorComponent());
                        return this;
                    }

                    public ComponentList AddCountdown(
                        string command = null,
                        float endTime = 0,
                        float startTime = 0,
                        float step = 1,
                        float interval = 1f,
                        TimerFormat timerFormat = TimerFormat.None,
                        string numberFormat = "0.####",
                        bool destroyIfDone = true)
                    {
                        Add(new CuiCountdownComponent
                        {
                            Command = command,
                            EndTime = endTime,
                            StartTime = startTime,
                            Step = step,
                            Interval = interval,
                            TimerFormat = timerFormat,
                            NumberFormat = numberFormat,
                            DestroyIfDone = destroyIfDone
                        });
                        return this;
                    }
                }
            }

            public static class ElementContructor
            {
                public static CUI.Element CreateText(
                 string text,
                 string color = Defaults.Color,
                 CUI.Font font = Defaults.Font,
                 int fontSize = Defaults.FontSize,
                 TextAnchor align = Defaults.Align,
                 VerticalWrapMode overflow = Defaults.VerticalOverflow,
                 string anchorMin = Defaults.VectorZero,
                 string anchorMax = Defaults.VectorOne,
                 string offsetMin = Defaults.VectorZero,
                 string offsetMax = Defaults.VectorZero,
                 string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddText(text, color, font, fontSize, align, overflow);
                    return element;
                }

                public static CUI.Element CreateOutlinedText(
                   string text,
                   string color = Defaults.Color,
                   CUI.Font font = Defaults.Font,
                   int fontSize = Defaults.FontSize,
                   TextAnchor align = Defaults.Align,
                   VerticalWrapMode overflow = Defaults.VerticalOverflow,
                   string outlineColor = Defaults.OutlineColor,
                   int outlineWidth = 1,
                   string anchorMin = Defaults.VectorZero,
                   string anchorMax = Defaults.VectorOne,
                   string offsetMin = Defaults.VectorZero,
                   string offsetMax = Defaults.VectorZero,
                   string name = null)
                {
                    CUI.Element element = CreateText(text, color, font, fontSize, align, overflow, anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddOutline(outlineColor, outlineWidth);
                    return element;
                }

                public static CUI.Element CreateInputfield(
                      string command = null,
                      string text = "",
                      string color = Defaults.Color,
                      CUI.Font font = Defaults.Font,
                      int fontSize = Defaults.FontSize,
                      TextAnchor align = Defaults.Align,
                      InputField.LineType lineType = Defaults.LineType,
                      CUI.InputType inputType = CUI.InputType.Default,
                      bool @readonly = false,
                      bool autoFocus = false,
                      bool isPassword = false,
                      int charsLimit = 0,
                      string anchorMin = Defaults.VectorZero,
                      string anchorMax = Defaults.VectorOne,
                      string offsetMin = Defaults.VectorZero,
                      string offsetMax = Defaults.VectorZero,
                      string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddInputfield(command, text, color, font, fontSize, align, lineType, inputType, @readonly, autoFocus, isPassword, charsLimit);
                    return element;
                }

                public static CUI.Element CreateButton(
                        string command = null,
                        string close = null,
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.Material,
                        Image.Type imageType = Defaults.ImageType,
                        string anchorMin = Defaults.VectorZero,
                        string anchorMax = Defaults.VectorOne,
                        string offsetMin = Defaults.VectorZero,
                        string offsetMax = Defaults.VectorZero,
                        string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddButton(command, close, color, sprite, material, imageType);
                    return element;
                }

                public static CUI.Element CreatePanel(
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.Material,
                        Image.Type imageType = Defaults.ImageType,
                        string anchorMin = Defaults.VectorZero,
                        string anchorMax = Defaults.VectorOne,
                        string offsetMin = Defaults.VectorZero,
                        string offsetMax = Defaults.VectorZero,
                        bool cursorEnabled = false,
                        bool keyboardEnabled = false,
                        string name = null)
                {

                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddImage(color, sprite, material, imageType);
                    if (cursorEnabled)
                        element.Components.AddNeedsCursor();
                    if (keyboardEnabled)
                        element.Components.AddNeedsKeyboard();
                    return element;
                }

                public static CUI.Element CreateImage(
                    string content,
                    string color = Defaults.Color,
                    string material = null,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddRawImage(content, color, material: material);
                    return element;
                }

                public static CUI.Element CreateIcon(
                        int itemId,
                        ulong skin = 0,
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.IconMaterial,
                        Image.Type imageType = Defaults.ImageType,
                        string anchorMin = Defaults.VectorZero,
                        string anchorMax = Defaults.VectorOne,
                        string offsetMin = Defaults.VectorZero,
                        string offsetMax = Defaults.VectorZero,
                        string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddImage(color, sprite, material, imageType, itemId, skin);
                    return element;
                }

                public static Element CreateContainer(
                       string anchorMin = Defaults.VectorZero,
                       string anchorMax = Defaults.VectorOne,
                       string offsetMin = Defaults.VectorZero,
                       string offsetMax = Defaults.VectorZero,
                       string name = null)
                {
                    return Element.Create(name).WithRect(anchorMin, anchorMax, offsetMin, offsetMax);
                }
            }


            public class Root : Element
            {
                public bool wasRendered = false;
                private static StringBuilder stringBuilder = new StringBuilder();

                public Root()
                {
                    Name = string.Empty;
                }

                public Root(string rootObjectName = "Overlay")
                {
                    Name = rootObjectName;
                }

                public override List<Element> Container { get; } = new List<Element>();

                public string ToJson(List<Element> elements)
                {
                    stringBuilder.Clear();
                    try
                    {
                        using (StringWriter stringWriter = new StringWriter(stringBuilder))
                        {
                            using (JsonWriter jsonWriter = new JsonTextWriter(stringWriter))
                            {
                                jsonWriter.WriteStartArray();
                                foreach (Element element in elements)
                                    element.WriteJson(jsonWriter);
                                jsonWriter.WriteEndArray();
                            }
                        }
                        return stringBuilder.ToString().Replace("\\n", "\n");
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError(ex.Message + "\n" + ex.StackTrace);
                        return string.Empty;
                    }
                }

                public string ToJson()
                {
                    return ToJson(Container);
                }

                public void Render(Connection connection)
                {
                    if (connection == null || !connection.connected)
                        return;

                    wasRendered = true;
                    CUI.AddUI(connection, ToJson(Container));
                }

                public void Render(BasePlayer player)
                {
                    Render(player.Connection);
                }

                public void Update(Connection connection)
                {
                    foreach (Element element in Container)
                        element.Update = true;
                    CUI.AddUI(connection, ToJson(Container));
                }

                public void Update(BasePlayer player)
                {
                    Update(player.Connection);
                }

            }
        }
    }
    #endregion
}
