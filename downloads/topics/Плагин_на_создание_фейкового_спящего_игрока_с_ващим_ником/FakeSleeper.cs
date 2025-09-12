using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("FakeSleeper", "Dopler", "1.0.12")]
    [Description("Фейк спящий игрок")]
    public class FakeSleeper : RustPlugin
    {
        private const string permUse = "fakesleeper.use";
        private const string permAdmin = "fakesleeper.admin";

        /// <summary>
        /// Префикс для ID фейковых игроков, используется для их идентификации
        /// </summary>
        private const uint ID_PREFIX = 123000; // Уникальный префикс для наших игроков

        /// <summary>
        /// Максимальное количество фейковых игроков, которые могут быть созданы
        /// </summary>
        private const int MAX_FAKE_PLAYERS = 20;

        /// <summary>
        /// Вспомогательный список для отслеживания созданных фейковых игроков в текущей сессии
        /// Используется ТОЛЬКО для ограничения количества создаваемых игроков
        /// </summary>
        private readonly List<uint> _createdPlayersNetIds = new();

        /// <summary>
        /// Флаг для предотвращения вложенных вызовов удаления игроков
        /// </summary>
        private bool _isRemovingPlayers;

        #region Initialization

        private void Init()
        {
            Puts("FakeSleeper initialization started...");
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permAdmin, this);
            Puts($"Permissions registered: {permUse}, {permAdmin}");

            // Очищаем список при инициализации
            _createdPlayersNetIds.Clear();
            _isRemovingPlayers = false;

            // Проверяем и удаляем зависшие фейковые объекты при загрузке
            // Используем прямой вызов с задержкой, чтобы сервер успел загрузиться
            _ = timer.Once(5f, EmergencyCleanup);

            Puts("FakeSleeper successfully loaded!");
        }

        private void OnServerSave()
        {
            // Очищаем мусор только каждые 10 минут для снижения нагрузки
            if (DateTime.UtcNow.Minute % 10 == 0)
            {
                Puts("[Auto] Performing scheduled cleanup during server save...");
                _ = timer.Once(0.1f, EmergencyCleanup); // Запускаем с задержкой для избежания проблем с сохранением
            }
        }

        private void Unload()
        {
            Puts("Plugin unloading...");
            // Быстрое удаление всех созданных игроков перед выгрузкой плагина
            EmergencyCleanup();
            _createdPlayersNetIds.Clear();
            Puts("Plugin unloaded.");
        }

        /// <summary>
        /// Экстренная очистка фейковых игроков в случае зависаний
        /// </summary>
        private void EmergencyCleanup()
        {
            if (_isRemovingPlayers)
            {
                Puts("Emergency cleanup already in progress, skipping");
                return;
            }

            _isRemovingPlayers = true;
            Puts("Starting emergency cleanup of fake sleepers...");

            try
            {
                // Получаем все спящие объекты
                List<BasePlayer> allSleepers = new(BasePlayer.sleepingPlayerList);
                Puts($"[Emergency] Found {allSleepers.Count} sleeping players to check");
                int removed = 0;

                // Удаляем всех фейковых игроков напрямую
                foreach (BasePlayer player in allSleepers)
                {
                    if (player?.IsDestroyed != false)
                    {
                        continue;
                    }

                    try
                    {
                        // Проверяем, является ли это наш игрок по ID или сетевому ID
                        bool isFake =
                            IsFakePlayerId(player.userID)
                            || (
                                player.net != null
                                && _createdPlayersNetIds.Contains((uint)player.net.ID.Value)
                            );

                        if (isFake)
                        {
                            Puts(
                                $"[Emergency] Killing fake player: {player.displayName} ({player.UserIDString})"
                            );
                            EmergencyKillPlayer(player);
                            removed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Puts(
                            $"[ERROR] Error during emergency cleanup for player {player.displayName}: {ex.Message}"
                        );
                    }
                }

                // Очищаем список созданных игроков
                _createdPlayersNetIds.Clear();
                Puts($"[Emergency] Removed {removed} fake players during cleanup");
            }
            catch (Exception ex)
            {
                Puts($"[ERROR] Error during emergency cleanup: {ex.Message}");
            }
            finally
            {
                _isRemovingPlayers = false;
            }
        }

        /// <summary>
        /// Экстренное удаление игрока напрямую
        /// </summary>
        /// <param name="player">Игрок, которого нужно уничтожить</param>
        private void EmergencyKillPlayer(BasePlayer player)
        {
            try
            {
                // Проверка на null и уже уничтоженного игрока
                if (player?.IsDestroyed != false)
                {
                    return;
                }

                // 1. Сохраняем ID для отслеживания
                ulong userId = player.userID;
                NetworkableId netId = player.net?.ID ?? default;

                // 2. Предотвращаем любые обновления - КРИТИЧНО для предотвращения NullReferenceException
                player.syncPosition = false;
                player.limitNetworking = true;

                // 3. Отменяем все запланированные вызовы
                player.CancelInvoke();

                // 4. Удаляем из списков игроков ДО уничтожения объекта
                _ = BasePlayer.sleepingPlayerList.Remove(player);
                _ = BasePlayer.activePlayerList.Remove(player);

                // 5. Очищаем нашу коллекцию отслеживания, если есть совпадение
                if (netId.IsValid && _createdPlayersNetIds.Contains((uint)netId.Value))
                {
                    _ = _createdPlayersNetIds.Remove((uint)netId.Value);
                }

                // 6. Отключаем все компоненты
                // Отключение работы физики
                Rigidbody rigidBody = player.GetComponent<Rigidbody>();
                if (rigidBody != null)
                {
                    rigidBody.isKinematic = true;
                    rigidBody.velocity = Vector3.zero;
                    rigidBody.detectCollisions = false;
                }

                // Отключаем сетевые компоненты
                if (player.net != null)
                {
                    player.net.sv = null;
                    player.net.group = null;
                    player.net.subscriber = null;
                    // Важно! Перед вызовом Kill сбрасываем ID
                    player.net.ID = default;
                }

                // Отключаем модели и коллайдеры
                foreach (Collider col in player.GetComponentsInChildren<Collider>())
                {
                    if (col != null)
                    {
                        col.enabled = false;
                    }
                }

                foreach (Renderer rend in player.GetComponentsInChildren<Renderer>())
                {
                    if (rend != null)
                    {
                        rend.enabled = false;
                    }
                }

                // Очищаем все ссылки
                player.userID = 0;
                player.UserIDString = null;
                player.displayName = null;
                player._name = null;

                // 7. Явно отключаем компонент перед удалением
                player.enabled = false;

                // 8. Отключаем все соединения
                // Только отсоединяем соединение, так как мы не можем напрямую изменить Connection
                if (player.net?.connection != null)
                {
                    player.net.connection.player = null;
                }

                // 9. Финальное уничтожение - НЕ используем DestroyMode.None
                // Используем DestroyMode.Gib, чтобы гарантировать полное уничтожение
                player.Kill(BaseNetworkable.DestroyMode.Gib);

                // 10. Проверка на случай, если объект все еще существует
                if (!player.IsDestroyed)
                {
                    Puts("[DEBUG] Primary kill failed, using Object.Destroy");
                    UnityEngine.Object.Destroy(player.gameObject);
                }

                // 11. Проверяем существование объекта после удаления и логируем
                if (player.IsDestroyed)
                {
                    Puts($"[DEBUG] Successfully killed player with ID {userId}");
                }
                else
                {
                    Puts("[WARN] Failed to properly destroy player!");
                }
            }
            catch (Exception ex)
            {
                Puts($"[ERROR] Error during emergency kill: {ex.Message}");

                // Последняя попытка удаления в случае ошибки
                try
                {
                    if (player?.IsDestroyed == false)
                    {
                        UnityEngine.Object.DestroyImmediate(player.gameObject);
                    }
                }
                catch (Exception innerEx)
                {
                    // Логируем внутреннюю ошибку
                    Puts($"[CRITICAL] Final destroy attempt failed: {innerEx.Message}");
                }
            }
        }

        /// <summary>
        /// Безопасный метод для уничтожения игрока
        /// </summary>
        /// <param name="player">Игрок, которого нужно уничтожить</param>
        private void SafeKillPlayer(BasePlayer player)
        {
            if (player?.IsDestroyed != false)
            {
                Puts("[DEBUG] SafeKillPlayer: Player is NULL or already destroyed, returning");
                return;
            }

            try
            {
                // Используем Invariant культуру для избежания проблем с локализацией
                Puts(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Destroying player ID: {0}, Name: {1}, NetID: {2}",
                        player.UserIDString,
                        player.displayName,
                        player.net?.ID
                    )
                );

                // Используем тот же подход, что и в EmergencyKillPlayer, но с дополнительными проверками

                // 1. Отключаем обновление позиции и блокируем сеть
                player.syncPosition = false;
                player.limitNetworking = true;

                // 2. Отключаем все флаги игрока
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, false);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Connected, false);

                // 3. Отменяем все отложенные вызовы
                player.CancelInvoke();

                // 4. Очищаем инвентарь, если есть
                player.inventory?.Strip();

                // 5. Удаляем из списков игроков перед уничтожением
                _ = BasePlayer.sleepingPlayerList.Remove(player);
                _ = BasePlayer.activePlayerList.Remove(player);

                // 6. Отключаем физику
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                }

                // 7. Отключаем все компоненты - коллайдеры и рендеры
                foreach (Collider col in player.GetComponentsInChildren<Collider>())
                {
                    if (col?.enabled == true)
                    {
                        col.enabled = false;
                    }
                }

                foreach (Renderer rend in player.GetComponentsInChildren<Renderer>())
                {
                    if (rend?.enabled == true)
                    {
                        rend.enabled = false;
                    }
                }

                // 8. Отключаем сетевые компоненты и удаляем из нашего списка
                if (player.net != null)
                {
                    // Проверяем и удаляем из нашего списка отслеживания
                    if (
                        player.net.ID.IsValid
                        && _createdPlayersNetIds.Contains((uint)player.net.ID.Value)
                    )
                    {
                        _ = _createdPlayersNetIds.Remove((uint)player.net.ID.Value);
                    }

                    // Отключаем сетевые компоненты
                    player.net.sv = null;
                    player.net.group = null;
                    player.net.subscriber = null;
                    player.net.ID = default;
                }

                // 9. Очистка идентификаторов
                player.userID = 0;
                player.UserIDString = null;
                player.displayName = null;
                player._name = null;

                // 10. Явно отключаем компонент
                player.enabled = false;

                // 11. Отключаем соединение
                // Очищаем соединение с игроком безопасным способом
                if (player.net?.connection != null)
                {
                    player.net.connection.player = null;
                }

                // 12. Уничтожаем с помощью Gib для более полного уничтожения
                player.Kill(BaseNetworkable.DestroyMode.Gib);

                // 13. Дополнительное уничтожение для надежности
                if (!player.IsDestroyed)
                {
                    Puts("[DEBUG] Primary SafeKill failed, using Object.Destroy");
                    UnityEngine.Object.Destroy(player.gameObject);
                }

                // 14. Дополнительная проверка после удаления
                if (!player.IsDestroyed && player.gameObject != null)
                {
                    Puts("[WARN] Final Destroy attempt with DestroyImmediate");
                    UnityEngine.Object.DestroyImmediate(player.gameObject);
                }

                Puts("[DEBUG] SafeKillPlayer: Player destroy sequence completed");
            }
            catch (Exception ex)
            {
                Puts($"[ERROR] SafeKillPlayer: Error occurred: {ex.Message}");
                Puts($"[ERROR] SafeKillPlayer: Stack trace: {ex.StackTrace}");

                // Решительная попытка удаления в случае ошибки
                try
                {
                    if (player?.IsDestroyed == false)
                    {
                        // Принудительное удаление из списков
                        _ = BasePlayer.sleepingPlayerList.Remove(player);
                        _ = BasePlayer.activePlayerList.Remove(player);

                        // Прямое удаление GameObject
                        UnityEngine.Object.DestroyImmediate(player.gameObject);
                    }
                }
                catch (Exception innerEx)
                {
                    Puts($"[CRITICAL] Final destroy attempt failed: {innerEx.Message}");
                }
            }
        }

        #endregion Initialization

        #region Commands

        [ChatCommand("fakesleeper")]
        private void FakeSleeperCommand(BasePlayer player, string command, string[] args)
        {
            Puts(
                $"Player {player.displayName} ({player.UserIDString}) called /fakesleeper command with args: {string.Join(", ", args)}"
            );

            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                Puts($"Player {player.displayName} does not have permission {permUse}");
                player.ChatMessage("У вас нет разрешения на использование этой команды!");
                return;
            }

            if (args.Length < 1)
            {
                Puts($"Player {player.displayName} did not provide fake player name");
                player.ChatMessage("Использование: /fakesleeper <имя_игрока>");
                return;
            }

            string fakePlayerName = args[0];
            Puts($"Creating fake sleeper with name: {fakePlayerName}");
            SpawnFakeSleeper(player, fakePlayerName);
        }

        [ConsoleCommand("fakesleeper")]
        private void FakeSleeperConsoleCommand(ConsoleSystem.Arg arg)
        {
            Puts($"Console command fakesleeper called. IsAdmin: {arg.IsAdmin}");

            if (
                arg.IsAdmin
                || (
                    arg.Player() != null
                    && permission.UserHasPermission(arg.Player().UserIDString, permUse)
                )
            )
            {
                if (arg.Args == null || arg.Args.Length < 1)
                {
                    Puts("No fake player name provided");
                    SendReply(
                        arg,
                        "Использование: fakesleeper <имя_игрока> | fakesleeper remove <имя_игрока> | fakesleeper clearall"
                    );
                    return;
                }

                if (arg.Args[0].ToLower(CultureInfo.CurrentCulture) is "clearall" or "removeall")
                {
                    if (
                        arg.IsAdmin
                        || (
                            arg.Player() != null
                            && permission.UserHasPermission(arg.Player().UserIDString, permAdmin)
                        )
                    )
                    {
                        Puts("Removing all fake sleepers");
                        int removed = RemoveAllFakeSleepers();
                        SendReply(arg, $"Удалено {removed} фейковых спящих игроков.");
                        return;
                    }
                    SendReply(arg, "У вас нет разрешения на удаление всех фейковых игроков!");

                    return;
                }

                if (arg.Args[0].ToLower(CultureInfo.CurrentCulture) is "remove" or "delete")
                {
                    if (arg.Args.Length < 2)
                    {
                        SendReply(arg, "Использование: fakesleeper remove <имя_игрока>");
                        return;
                    }

                    string fakePlayerName = arg.Args[1];
                    Puts($"Removing fake sleeper with name: {fakePlayerName}");
                    RemoveFakeSleeper(arg.Player(), fakePlayerName);
                    return;
                }

                string playerName = arg.Args[0];
                Puts($"Creating fake sleeper with name: {playerName}");
                SpawnFakeSleeper(arg.Player(), playerName);
            }
            else
            {
                Puts("Access denied to fakesleeper command");
                SendReply(arg, "У вас нет разрешения на использование этой команды!");
            }
        }

        [ConsoleCommand("fakesleeper.clearall")]
        private void ClearAllFakeSleepersCommand(ConsoleSystem.Arg arg)
        {
            Puts($"Console command fakesleeper.clearall called. IsAdmin: {arg.IsAdmin}");

            if (
                arg.IsAdmin
                || (
                    arg.Player() != null
                    && permission.UserHasPermission(arg.Player().UserIDString, permAdmin)
                )
            )
            {
                Puts("Removing all fake sleepers...");
                int removed = RemoveAllFakeSleepers();
                SendReply(arg, $"Удалено {removed} фейковых спящих игроков.");
            }
            else
            {
                Puts("Access denied to fakesleeper.clearall command");
                SendReply(arg, "У вас нет разрешения на удаление всех фейковых игроков!");
            }
        }

        [ConsoleCommand("fakesleeper.fix")]
        private void FixFakeSleeperNames(ConsoleSystem.Arg arg)
        {
            Puts($"Console command fakesleeper.fix called. IsAdmin: {arg.IsAdmin}");

            if (
                arg.IsAdmin
                || (
                    arg.Player() != null
                    && permission.UserHasPermission(arg.Player().UserIDString, permAdmin)
                )
            )
            {
                Puts("Attempting to fix all fake sleeper names...");
                SendReply(
                    arg,
                    "Функция исправления имен больше не поддерживается, так как плагин не хранит данные о созданных игроках."
                );
            }
            else
            {
                Puts("Access denied to fakesleeper.fix command");
                SendReply(arg, "У вас нет разрешения на исправление фейковых игроков!");
            }
        }

        [ConsoleCommand("fakesleeper.emergency")]
        private void EmergencyCleanupCommand(ConsoleSystem.Arg arg)
        {
            Puts($"Console command fakesleeper.emergency called. IsAdmin: {arg.IsAdmin}");

            if (arg.IsAdmin)
            {
                Puts("Starting emergency cleanup...");
                EmergencyCleanup();
                SendReply(arg, "Экстренная очистка фейковых игроков выполнена.");
            }
            else
            {
                Puts("Access denied to fakesleeper.emergency command");
                SendReply(arg, "У вас нет разрешения на использование этой команды!");
            }
        }

        #endregion Commands

        #region Core Functionality

        private void SpawnFakeSleeper(BasePlayer caller, string fakePlayerName)
        {
            Puts($"Starting creation of fake sleeper '{fakePlayerName}'...");

            try
            {
                // Проверяем количество уже созданных фейковых игроков
                // Сначала обновляем список, удаляя уже несуществующие ID
                CleanupNetIdsList();

                int currentFakePlayerCount = _createdPlayersNetIds.Count;
                Puts($"Current fake player count: {currentFakePlayerCount}/{MAX_FAKE_PLAYERS}");

                if (currentFakePlayerCount >= MAX_FAKE_PLAYERS)
                {
                    string errorMessage =
                        $"Достигнут лимит фейковых игроков ({MAX_FAKE_PLAYERS}). Удалите существующих перед созданием новых.";
                    Puts($"ERROR: {errorMessage}");
                    caller?.ChatMessage(errorMessage);
                    return;
                }

                // Generate a random ID for the fake player (needed for proper name display)
                ulong randomId = GenerateRandomId();
                Puts($"Generated random ID: {randomId}");

                // Create fake sleeper
                Vector3 spawnPosition = caller != null ? caller.transform.position : Vector3.zero;
                Puts($"Creating fake player at position: {spawnPosition}");

                BaseEntity entity = GameManager.server.CreateEntity(
                    "assets/prefabs/player/player.prefab",
                    spawnPosition
                );

                if (entity == null)
                {
                    Puts("Failed to create fake player entity!");

                    if (caller != null)
                    {
                        caller.ChatMessage("Не удалось создать фейкового спящего игрока!");
                    }
                    else
                    {
                        Puts("Failed to create fake sleeper!");
                    }

                    return;
                }

                Puts("Entity created, calling Spawn()...");
                entity.Spawn();
                Puts($"Entity successfully created. EntityID: {entity.net.ID}");

                // Добавляем сетевой ID в список для отслеживания
                if (entity.net?.ID.IsValid == true)
                {
                    _createdPlayersNetIds.Add((uint)entity.net.ID.Value);
                }

                // Безопасное приведение типов
                if (entity is not BasePlayer)
                {
                    Puts("Error! Created entity is not a BasePlayer!");
                    entity.Kill();
                    if (caller != null)
                    {
                        caller.ChatMessage(
                            "Не удалось создать фейкового спящего игрока: неверный тип сущности!"
                        );
                    }
                    else
                    {
                        Puts("Failed to create fake sleeper: wrong entity type!");
                    }

                    return;
                }

                BasePlayer fakePlayer = (BasePlayer)entity;
                Puts($"Entity successfully cast to BasePlayer. UserID: {fakePlayer.UserIDString}");

                // Установка идентификаторов - важно для корректного отображения имен
                fakePlayer.userID = randomId;
                fakePlayer.UserIDString = randomId.ToString(CultureInfo.InvariantCulture);
                Puts($"Set userID: {fakePlayer.userID}, UserIDString: {fakePlayer.UserIDString}");

                // Setup fake sleeper with proper sequencing for name display
                Puts($"Setting up fake player parameters. Name: {fakePlayerName}");

                // Проверяем на кириллицу и добавляем ASCII-префикс для корректного отображения русских имен
                bool containsCyrillic = ContainsCyrillicCharacters(fakePlayerName);
                if (containsCyrillic)
                {
                    // Используем транслитерацию для внутреннего имени, но отображаем оригинальное
                    string translitName = Transliterate(fakePlayerName);
                    fakePlayer._name = translitName; // Внутреннее имя - транслитерация
                    fakePlayer.displayName = fakePlayerName; // Отображаемое имя - оригинал
                    Puts(
                        $"Name contains Cyrillic characters, using transliterated name internally: {translitName}, display: {fakePlayerName}"
                    );
                }
                else
                {
                    // Для не-кириллических имен оставляем как есть
                    fakePlayer._name = fakePlayerName;
                    fakePlayer.displayName = fakePlayerName;
                }
                Puts($"_name and displayName set: {fakePlayer._name}, {fakePlayer.displayName}");

                // Важно! BaseNetworkable.serverEntities нужно обновить
                if (
                    BaseNetworkable.serverEntities.Find(fakePlayer.net.ID)
                    is BasePlayer networkPlayer
                )
                {
                    Puts("Found player in BaseNetworkable.serverEntities, updating name there too");
                    networkPlayer.userID = randomId; // Устанавливаем тот же ID
                    networkPlayer.UserIDString = randomId.ToString(CultureInfo.InvariantCulture);

                    if (containsCyrillic)
                    {
                        networkPlayer._name = Transliterate(fakePlayerName);
                        networkPlayer.displayName = fakePlayerName;
                    }
                    else
                    {
                        networkPlayer._name = fakePlayerName;
                        networkPlayer.displayName = fakePlayerName;
                    }
                }

                // Установка имени в компоненте BaseNetworkable
                if (fakePlayer.net.connection != null)
                {
                    fakePlayer.net.connection.username = containsCyrillic
                        ? Transliterate(fakePlayerName)
                        : fakePlayerName;
                    fakePlayer.net.connection.userid = randomId;
                    Puts(
                        $"Connection username set: {fakePlayer.net.connection.username}, userid: {fakePlayer.net.connection.userid}"
                    );
                }
                else
                {
                    Puts("Connection is null, cannot set username");
                }

                // Установка позиции и других параметров
                fakePlayer.syncPosition = true;
                fakePlayer.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Puts("syncPosition set: true, random rotation applied");

                // Установка позиции в правильное положение для спящего
                if (caller != null)
                {
                    // Немного сдвигаем от положения создателя, чтобы не застрять
                    Vector3 adjustedPosition = caller.transform.position + (Vector3.right * 1.5f);
                    fakePlayer.transform.position = adjustedPosition;
                    Puts($"Position adjusted to: {adjustedPosition}");
                }

                // Применяем некоторые базовые настройки для спящего игрока
                Item vest = ItemManager.CreateByName("attire.hide.vest", 1);
                if (vest != null)
                {
                    bool vestAdded = vest.MoveToContainer(fakePlayer.inventory.containerWear);
                    Puts($"Added vest to wear container: {(vestAdded ? "success" : "failed")}");
                }

                Item pants = ItemManager.CreateByName("attire.hide.pants", 1);
                if (pants != null)
                {
                    bool pantsAdded = pants.MoveToContainer(fakePlayer.inventory.containerWear);
                    Puts($"Added pants to wear container: {(pantsAdded ? "success" : "failed")}");
                }

                // Set to sleeping state перед сетевым обновлением
                Puts("Setting sleeping state...");
                fakePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);

                // Обновляем сетевую информацию
                fakePlayer.SendNetworkUpdateImmediate();
                Puts("Immediate network update sent");

                // Создаем КОПИЮ ссылки на фейкового игрока для безопасного использования в отложенном вызове
                ulong playerNetId = 0;
                if (fakePlayer.net?.ID != null && fakePlayer.net.ID.IsValid)
                {
                    playerNetId = (uint)fakePlayer.net.ID.Value;
                }

                // Создаем функцию для отправки обновления через некоторое время
                // Используя промежуточный поиск сущности вместо прямой ссылки
                _ = timer.Once(
                    0.5f,
                    () =>
                    {
                        // Находим игрока по сетевому ID - безопасно для случаев удаления
                        if (playerNetId != 0)
                        {
                            BaseNetworkable entity = BaseNetworkable.serverEntities.Find(
                                new NetworkableId(playerNetId)
                            );
                            if (entity is BasePlayer player && !player.IsDestroyed)
                            {
                                player.SendNetworkUpdate();
                                Puts("Delayed network update sent");
                            }
                        }
                    }
                );

                // Сообщаем об успехе
                if (caller != null)
                {
                    Puts($"Sending success message to player {caller.displayName}");
                    caller.ChatMessage(
                        $"Фейковый спящий игрок '{fakePlayerName}' успешно создан и установлен в состояние сна."
                    );
                }
                else
                {
                    Puts(
                        $"Fake sleeper '{fakePlayerName}' successfully created and set to sleeping state."
                    );
                }
            }
            catch (Exception ex)
            {
                Puts($"ERROR creating fake player: {ex.Message}");
                Puts($"Stack trace: {ex.StackTrace}");

                if (caller != null)
                {
                    caller.ChatMessage(
                        $"Ошибка при создании фейкового спящего игрока: {ex.Message}"
                    );
                }
                else
                {
                    Puts($"Error creating fake sleeper: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Очищает список сетевых ID от несуществующих объектов
        /// </summary>
        private void CleanupNetIdsList()
        {
            if (_createdPlayersNetIds.Count == 0)
            {
                return;
            }

            List<uint> toRemove = new();

            foreach (uint netId in _createdPlayersNetIds)
            {
                if (
                    netId == 0
                    || BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) == null
                )
                {
                    toRemove.Add(netId);
                }
            }

            foreach (uint netId in toRemove)
            {
                _ = _createdPlayersNetIds.Remove(netId);
            }

            Puts($"Cleaned up {toRemove.Count} invalid network IDs from tracking list");
        }

        private int RemoveAllFakeSleepers()
        {
            Puts("Starting removal of all fake sleepers...");

            // Если мы уже в процессе удаления, используем экстренный режим
            if (_isRemovingPlayers)
            {
                Puts("Already removing players, switching to emergency mode");
                EmergencyCleanup();
                return 0;
            }

            _isRemovingPlayers = true;

            try
            {
                int removedCount = 0;
                List<BasePlayer> sleepersToCheck = new(BasePlayer.sleepingPlayerList);

                if (sleepersToCheck.Count == 0)
                {
                    Puts("No sleeping players found to check");
                    return 0;
                }

                Puts($"Found {sleepersToCheck.Count} sleeping players to check");

                // Проходим по всем спящим игрокам и проверяем, не созданы ли они нашим плагином
                foreach (BasePlayer player in sleepersToCheck)
                {
                    if (player?.IsDestroyed != false)
                    {
                        Puts(
                            "WARNING: Found null or destroyed player in sleepingPlayerList, skipping"
                        );
                        continue;
                    }

                    // Проверяем, является ли это наш игрок
                    bool isFake =
                        IsFakePlayerId(player.userID)
                        || (
                            player.net != null
                            && _createdPlayersNetIds.Contains((uint)player.net.ID.Value)
                        );

                    if (isFake)
                    {
                        Puts($"Found fake sleeper: {player.displayName} ({player.UserIDString})");

                        // Сразу уничтожаем фейкового игрока
                        EmergencyKillPlayer(player);
                        removedCount++;
                    }
                }

                // Очищаем список созданных игроков
                _createdPlayersNetIds.Clear();
                Puts($"Removed {removedCount} fake players");
                return removedCount;
            }
            catch (Exception ex)
            {
                Puts($"ERROR removing fake players: {ex.Message}");
                Puts($"Stack trace: {ex.StackTrace}");
                return 0;
            }
            finally
            {
                _isRemovingPlayers = false;
            }
        }

        private void RemoveFakeSleeper(BasePlayer caller, string fakePlayerName)
        {
            Puts($"Starting removal of fake sleeper '{fakePlayerName}'...");

            try
            {
                // Ищем игрока среди спящих
                BasePlayer? targetPlayer = null;

                foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
                {
                    if (player == null)
                    {
                        Puts("WARNING: Found null player in sleepingPlayerList, skipping");
                        continue;
                    }

                    Puts($"[DEBUG] Checking player: {player.displayName}, ID: {player.userID}");

                    if (
                        string.Equals(
                            player._name,
                            fakePlayerName,
                            StringComparison.OrdinalIgnoreCase
                        )
                        || string.Equals(
                            player.displayName,
                            fakePlayerName,
                            StringComparison.OrdinalIgnoreCase
                        )
                        || player.displayName.Contains(
                            fakePlayerName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        targetPlayer = player;
                        Puts(
                            $"Found potential fake player entity: {player.displayName} ({player.userID})"
                        );
                        break;
                    }
                }

                // Если игрок найден, удаляем его
                if (targetPlayer != null)
                {
                    // Дополнительная проверка, что это наш фейковый игрок по ID
                    if (!IsFakePlayerId(targetPlayer.userID))
                    {
                        Puts(
                            $"Warning: Player {targetPlayer.displayName} doesn't have an ID matching our fake players format"
                        );
                    }

                    Puts($"Removing fake player entity with ID: {targetPlayer.net?.ID}");
                    SafeKillPlayer(targetPlayer);
                    Puts($"Fake player '{fakePlayerName}' removal process started");

                    if (caller != null)
                    {
                        caller.ChatMessage(
                            $"Фейковый спящий игрок '{fakePlayerName}' успешно удаляется."
                        );
                    }
                    else
                    {
                        Puts($"Fake sleeper '{fakePlayerName}' removal process started.");
                    }
                }
                else
                {
                    Puts($"Could not find fake player entity with name '{fakePlayerName}'");

                    if (caller != null)
                    {
                        caller.ChatMessage(
                            $"Фейковый спящий игрок с именем '{fakePlayerName}' не найден!"
                        );
                    }
                    else
                    {
                        Puts($"Fake sleeper with name '{fakePlayerName}' not found!");
                    }
                }
            }
            catch (Exception ex)
            {
                Puts($"ERROR removing fake player: {ex.Message}");
                Puts($"Stack trace: {ex.StackTrace}");

                if (caller != null)
                {
                    caller.ChatMessage(
                        $"Ошибка при удалении фейкового спящего игрока: {ex.Message}"
                    );
                }
                else
                {
                    Puts($"Error removing fake sleeper: {ex.Message}");
                }
            }
        }

        private bool ContainsCyrillicCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Проверяем наличие кириллических символов
            // Упрощаем с использованием LINQ
            return text.Any(c =>
                c
                    is (>= '\u0400' and <= '\u04FF')
                        or // Кириллица
                        (>= '\u0500' and <= '\u052F')
                        or // Дополнительная кириллица
                        (>= '\u2DE0' and <= '\u2DFF')
                        or // Расширенная кириллица A
                        (>= '\uA640' and <= '\uA69F')
            ); // Расширенная кириллица B
        }

        private string Transliterate(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Словарь транслитерации
            Dictionary<char, string> cyrillic = new()
            {
                { 'а', "a" },
                { 'б', "b" },
                { 'в', "v" },
                { 'г', "g" },
                { 'д', "d" },
                { 'е', "e" },
                { 'ё', "yo" },
                { 'ж', "zh" },
                { 'з', "z" },
                { 'и', "i" },
                { 'й', "y" },
                { 'к', "k" },
                { 'л', "l" },
                { 'м', "m" },
                { 'н', "n" },
                { 'о', "o" },
                { 'п', "p" },
                { 'р', "r" },
                { 'с', "s" },
                { 'т', "t" },
                { 'у', "u" },
                { 'ф', "f" },
                { 'х', "kh" },
                { 'ц', "ts" },
                { 'ч', "ch" },
                { 'ш', "sh" },
                { 'щ', "sch" },
                { 'ъ', "" },
                { 'ы', "y" },
                { 'ь', "" },
                { 'э', "e" },
                { 'ю', "yu" },
                { 'я', "ya" },
                { 'А', "A" },
                { 'Б', "B" },
                { 'В', "V" },
                { 'Г', "G" },
                { 'Д', "D" },
                { 'Е', "E" },
                { 'Ё', "Yo" },
                { 'Ж', "Zh" },
                { 'З', "Z" },
                { 'И', "I" },
                { 'Й', "Y" },
                { 'К', "K" },
                { 'Л', "L" },
                { 'М', "M" },
                { 'Н', "N" },
                { 'О', "O" },
                { 'П', "P" },
                { 'Р', "R" },
                { 'С', "S" },
                { 'Т', "T" },
                { 'У', "U" },
                { 'Ф', "F" },
                { 'Х', "Kh" },
                { 'Ц', "Ts" },
                { 'Ч', "Ch" },
                { 'Ш', "Sh" },
                { 'Щ', "Sch" },
                { 'Ъ', "" },
                { 'Ы', "Y" },
                { 'Ь', "" },
                { 'Э', "E" },
                { 'Ю', "Yu" },
                { 'Я', "Ya" },
            };

            // Используем StringBuilder для оптимизации производительности
            System.Text.StringBuilder result = new();
            foreach (char c in text)
            {
                if (cyrillic.TryGetValue(c, out string value))
                {
                    _ = result.Append(value);
                }
                else
                {
                    _ = result.Append(c);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Проверяет, является ли ID одним из созданных нашим плагином
        /// </summary>
        /// <param name="userId">ID для проверки</param>
        /// <returns>true если это ID фейкового игрока</returns>
        private bool IsFakePlayerId(ulong userId)
        {
            // Метод для определения, создан ли игрок нашим плагином
            // Проверяем по диапазону ID

            // Получаем первые цифры ID
            string userIdStr = userId.ToString(CultureInfo.InvariantCulture);
            if (userIdStr.Length < 6)
            {
                return false;
            }

            // Пытаемся получить первые 6 цифр, используя AsSpan вместо Substring
            if (!uint.TryParse(userIdStr.AsSpan(0, 6), out uint prefix))
            {
                return false;
            }

            // Сравниваем с нашим префиксом
            return prefix == ID_PREFIX;
        }

        /// <summary>
        /// Генерирует случайный ID для фейкового игрока
        /// </summary>
        /// <returns>Случайный ulong ID с нашим префиксом</returns>
        private ulong GenerateRandomId()
        {
            // Используем префикс + случайное число
            string idStr = ID_PREFIX.ToString(CultureInfo.InvariantCulture);

            // Генерируем случайное число для дополнения ID
            uint suffix = (uint)Random.Range(100000, 999999);
            idStr += suffix.ToString(CultureInfo.InvariantCulture);

            // Преобразуем в ulong
            if (ulong.TryParse(idStr, out ulong result))
            {
                Puts(
                    $"[DEBUG] Generated random ID: {result}, prefix: {ID_PREFIX}, suffix: {suffix}"
                );
                return result;
            }

            // Запасной вариант, если что-то пошло не так - используем конкатенацию строк
            // чтобы избежать переполнения при умножении
            string backupIdStr =
                ID_PREFIX.ToString(CultureInfo.InvariantCulture)
                + suffix.ToString(CultureInfo.InvariantCulture);

            if (ulong.TryParse(backupIdStr, out ulong backupId))
            {
                Puts($"[DEBUG] Used string concat backup method for ID generation: {backupId}");
                return backupId;
            }

            // Если и это не сработало, используем безопасный диапазон
            // Чтобы не было переполнения при компиляции
            const ulong lastResortId = 12300000001UL;
            Puts($"[DEBUG] Used fixed last resort ID generation: {lastResortId}");
            return lastResortId;
        }

        #endregion Core Functionality
    }
}
