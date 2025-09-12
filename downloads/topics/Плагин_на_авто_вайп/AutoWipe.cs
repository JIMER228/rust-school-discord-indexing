using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("AutoWipe", "Author", "1.0.0")]
    public class AutoWipe : RustPlugin
    {
        #region Fields

        private Configuration? config;
        private Timer? wipeCheckTimer;
        private Timer? warningTimer;
        private bool isWipeDay;
        private const string WipePermission = "autowipe.wiper";
        private const string AdminSteamId = "76561198199880946";
        private static readonly string[] argsArray = new[] { "Только что был вайп!" };

        [PluginReference]
        private readonly Plugin? Backpacks;

        [PluginReference]
        private readonly Plugin? XRPG;

        #endregion Fields

        #region Configuration

        private sealed class Configuration
        {
            [JsonProperty("Время вайпа (час)")]
            public int WipeHour = 14;

            [JsonProperty("Минута вайпа")]
            public int WipeMinute;

            [JsonProperty("За сколько минут предупреждать о вайпе")]
            public int WarningMinutes = 60;

            [JsonProperty("Сообщение о предстоящем вайпе рюкзаков")]
            public string BackpackWipeWarning = "Через {0} минут произойдет вайп рюкзаков!";

            [JsonProperty("Сообщение о предстоящем вайпе XRPG")]
            public string XRPGWipeWarning = "Через {0} минут произойдет вайп XRPG!";

            [JsonProperty("Сообщение о предстоящем вайпе карты")]
            public string MapWipeWarning = "Через {0} минут произойдет вайп карты!";

            [JsonProperty("Сообщение о предстоящем вайпе чертежей")]
            public string BlueprintWipeWarning = "Через {0} минут произойдет вайп чертежей!";

            [JsonProperty("Сообщение об отсутствии прав")]
            public string NoPermissionMessage = "У вас нет прав для выполнения этой команды!";

            public static Configuration DefaultConfig()
            {
                return new Configuration();
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.DefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion Configuration

        #region Oxide Hooks

        private void OnServerInitialized(bool initial)
        {
            if (Backpacks == null)
            {
                PrintError("Backpacks plugin is not installed!");
            }

            if (XRPG == null)
            {
                PrintError("XRPG plugin is not installed!");
            }

            permission.RegisterPermission(WipePermission, this);
            wipeCheckTimer = timer.Every(60f, CheckWipeSchedule);
        }

        private void Unload()
        {
            wipeCheckTimer?.Destroy();
            warningTimer?.Destroy();
        }

        #endregion Oxide Hooks

        #region Commands

        [ChatCommand("WipeMap")]
        private void CmdWipeMap(BasePlayer player, string command, string[] args)
        {
            if (config == null)
            {
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, WipePermission))
            {
                SendReply(player, config.NoPermissionMessage);
                return;
            }

            Server.Broadcast("<color=orange>Начинаем вайп карты...</color>");

            // Обновляем информацию о вайпе в списке серверов
            _ = ConsoleSystem.Run(
                ConsoleSystem.Option.Server,
                "server.writecfg",
                Array.Empty<string>()
            );
            _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.description", argsArray);
            _ = ConsoleSystem.Run(
                ConsoleSystem.Option.Server,
                "server.save",
                Array.Empty<string>()
            );

            // Кикаем всех игроков
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer?.IsDestroyed == false && activePlayer.UserIDString != AdminSteamId)
                {
                    activePlayer.Kick("Вайп сервера! Сервер будет доступен через 10 минут");
                }
            }

            // Сначала вайпим рюкзаки
            PerformBackpackWipe();

            try
            {
                // Выполняем команду wipe для полной очистки
                _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "wipe", Array.Empty<string>());

                _ = timer.Once(
                    2f,
                    () =>
                    {
                        // Удаляем все сущности
                        BaseEntity[] entities = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
                        int count = 0;

                        foreach (BaseEntity entity in entities)
                        {
                            if (entity?.IsDestroyed == false)
                            {
                                try
                                {
                                    entity.Kill();
                                    count++;

                                    if (count % 100 == 0)
                                    {
                                        Server.Broadcast(
                                            $"<color=orange>Уничтожено объектов: {count}</color>"
                                        );
                                    }
                                }
                                catch (Exception) { }
                            }
                        }

                        Server.Broadcast($"<color=orange>Уничтожено объектов: {count}</color>");

                        _ = timer.Once(
                            2f,
                            () =>
                            {
                                // Пересоздаем группы спавна
                                _ = ConsoleSystem.Run(
                                    ConsoleSystem.Option.Server,
                                    "spawn.fill_populations",
                                    Array.Empty<string>()
                                );

                                _ = timer.Once(
                                    2f,
                                    () =>
                                    {
                                        _ = ConsoleSystem.Run(
                                            ConsoleSystem.Option.Server,
                                            "respawn_groups",
                                            Array.Empty<string>()
                                        );

                                        _ = timer.Once(
                                            2f,
                                            () =>
                                            {
                                                _ = ConsoleSystem.Run(
                                                    ConsoleSystem.Option.Server,
                                                    "server.save",
                                                    Array.Empty<string>()
                                                );

                                                Server.Broadcast(
                                                    "<color=orange>Сервер будет перезагружен через 10 секунд...</color>"
                                                );

                                                _ = timer.Once(
                                                    10f,
                                                    () =>
                                                    {
                                                        _ = ConsoleSystem.Run(
                                                            ConsoleSystem.Option.Server,
                                                            "restart",
                                                            Array.Empty<string>()
                                                        );
                                                    }
                                                );
                                            }
                                        );
                                    }
                                );
                            }
                        );
                    }
                );
            }
            catch (Exception)
            {
                Server.Broadcast("<color=red>Ошибка при выполнении вайпа карты!</color>");
            }
        }

        [ChatCommand("GlobalWipe")]
        private void CmdGlobalWipe(BasePlayer player, string command, string[] args)
        {
            if (config == null)
            {
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, WipePermission))
            {
                SendReply(player, config.NoPermissionMessage);
                return;
            }

            // Обновляем информацию о вайпе в списке серверов
            _ = ConsoleSystem.Run(
                ConsoleSystem.Option.Server,
                "server.writecfg",
                Array.Empty<string>()
            );
            _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.description", argsArray);
            _ = ConsoleSystem.Run(
                ConsoleSystem.Option.Server,
                "server.save",
                Array.Empty<string>()
            );

            // Кикаем всех игроков
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer?.IsDestroyed == false && activePlayer.UserIDString != AdminSteamId)
                {
                    activePlayer.Kick("Вайп сервера! Сервер будет доступен через 10 минут");
                }
            }

            // Сначала вайпим XRPG и рюкзаки
            PerformXRPGWipe();
            PerformBackpackWipe();

            // Затем выполняем вайп карты и чертежей
            Server.Command("wipe");

            _ = timer.Once(
                1f,
                () =>
                {
                    _ = ConsoleSystem.Run(
                        ConsoleSystem.Option.Server,
                        "kill_all",
                        Array.Empty<string>()
                    );

                    _ = timer.Once(
                        1f,
                        () =>
                        {
                            Server.Command("global.kill");

                            _ = timer.Once(
                                1f,
                                () =>
                                {
                                    _ = ConsoleSystem.Run(
                                        ConsoleSystem.Option.Server,
                                        "wipebps",
                                        Array.Empty<string>()
                                    );

                                    _ = timer.Once(
                                        1f,
                                        () =>
                                        {
                                            _ = ConsoleSystem.Run(
                                                ConsoleSystem.Option.Server,
                                                "respawn_groups",
                                                Array.Empty<string>()
                                            );

                                            _ = timer.Once(
                                                1f,
                                                () =>
                                                {
                                                    _ = ConsoleSystem.Run(
                                                        ConsoleSystem.Option.Server,
                                                        "restart",
                                                        Array.Empty<string>()
                                                    );
                                                }
                                            );
                                        }
                                    );
                                }
                            );
                        }
                    );
                }
            );
        }

        #endregion Commands

        #region Methods

        private void CheckWipeSchedule()
        {
            if (config == null)
            {
                return;
            }

            DateTime currentTime = DateTime.UtcNow.AddHours(3); // MSK = UTC+3

            // Check if it's Wednesday or Saturday
            bool isWednesday = currentTime.DayOfWeek == DayOfWeek.Wednesday;
            bool isSaturday = currentTime.DayOfWeek == DayOfWeek.Saturday;

            if (!isWednesday && !isSaturday)
            {
                return;
            }

            int currentHour = currentTime.Hour;
            int currentMinute = currentTime.Minute;

            // Check warning time
            if (
                currentHour == (config.WipeHour - 1)
                && currentMinute == config.WipeMinute
                && !isWipeDay
            )
            {
                isWipeDay = true;

                if (isWednesday)
                {
                    BroadcastWarning(config.BackpackWipeWarning, config.WarningMinutes);
                    BroadcastWarning(config.MapWipeWarning, config.WarningMinutes);
                }
                else if (isSaturday)
                {
                    BroadcastWarning(config.XRPGWipeWarning, config.WarningMinutes);
                    BroadcastWarning(config.BackpackWipeWarning, config.WarningMinutes);
                    BroadcastWarning(config.MapWipeWarning, config.WarningMinutes);
                    BroadcastWarning(config.BlueprintWipeWarning, config.WarningMinutes);
                }

                warningTimer = timer.Every(
                    600f,
                    () =>
                    {
                        int remainingMinutes =
                            (config.WipeHour * 60)
                            + config.WipeMinute
                            - ((currentHour * 60) + currentMinute);
                        if (remainingMinutes > 0)
                        {
                            if (isWednesday)
                            {
                                BroadcastWarning(config.BackpackWipeWarning, remainingMinutes);
                                BroadcastWarning(config.MapWipeWarning, remainingMinutes);
                            }
                            else if (isSaturday)
                            {
                                BroadcastWarning(config.XRPGWipeWarning, remainingMinutes);
                                BroadcastWarning(config.BackpackWipeWarning, remainingMinutes);
                                BroadcastWarning(config.MapWipeWarning, remainingMinutes);
                                BroadcastWarning(config.BlueprintWipeWarning, remainingMinutes);
                            }
                        }
                    }
                );
            }

            // Check wipe time
            if (currentHour == config.WipeHour && currentMinute == config.WipeMinute && isWipeDay)
            {
                isWipeDay = false;
                warningTimer?.Destroy();

                if (isWednesday)
                {
                    PerformBackpackWipe();

                    _ = ConsoleSystem.Run(
                        ConsoleSystem.Option.Server,
                        "wipe",
                        Array.Empty<string>()
                    );

                    _ = timer.Once(
                        2f,
                        () =>
                        {
                            _ = ConsoleSystem.Run(
                                ConsoleSystem.Option.Server,
                                "restart",
                                Array.Empty<string>()
                            );
                        }
                    );
                }
                else if (isSaturday)
                {
                    PerformXRPGWipe();
                    PerformBackpackWipe();

                    _ = ConsoleSystem.Run(
                        ConsoleSystem.Option.Server,
                        "wipe",
                        Array.Empty<string>()
                    );

                    _ = timer.Once(
                        2f,
                        () =>
                        {
                            _ = ConsoleSystem.Run(
                                ConsoleSystem.Option.Server,
                                "wipebps",
                                Array.Empty<string>()
                            );

                            _ = timer.Once(
                                2f,
                                () =>
                                {
                                    _ = ConsoleSystem.Run(
                                        ConsoleSystem.Option.Server,
                                        "restart",
                                        Array.Empty<string>()
                                    );
                                }
                            );
                        }
                    );
                }
            }
        }

        private void PerformBackpackWipe()
        {
            if (Backpacks == null)
            {
                return;
            }

            try
            {
                // Пробуем использовать API для вайпа
                if (
                    Backpacks.Call("API_GetExistingBackpacks")
                    is Dictionary<ulong, ItemContainer> backpackManager
                )
                {
                    int count = 0;
                    foreach (ulong userId in backpackManager.Keys)
                    {
                        _ = Backpacks.Call("API_EraseBackpack", userId);
                        count++;
                    }
                }
                else
                {
                    // Пробуем использовать прямую команду вайпа
                    _ = ConsoleSystem.Run(
                        ConsoleSystem.Option.Server,
                        "backpack.wipeall",
                        Array.Empty<string>()
                    );
                }
            }
            catch (Exception)
            {
                // Пробуем еще раз выполнить команду вайпа как запасной вариант
                _ = ConsoleSystem.Run(
                    ConsoleSystem.Option.Server,
                    "backpack.wipeall",
                    Array.Empty<string>()
                );
            }

            Server.Broadcast("<color=orange>Выполнен вайп рюкзаков!</color>");
        }

        private void PerformXRPGWipe()
        {
            if (XRPG == null)
            {
                return;
            }

            try
            {
                // Сначала пробуем использовать команду вайпа
                _ = ConsoleSystem.Run(
                    ConsoleSystem.Option.Server,
                    "xrpg.wipe",
                    Array.Empty<string>()
                );

                // Затем пробуем использовать API
                _ = XRPG.Call("OnNewSave");
            }
            catch (Exception) { }

            Server.Broadcast("<color=orange>Выполнен вайп XRPG!</color>");
        }

        private void BroadcastWarning(string message, int minutes)
        {
            string formattedMessage = string.Format(CultureInfo.InvariantCulture, message, minutes);
            Server.Broadcast($"<color=orange>{formattedMessage}</color>");
        }

        #endregion Methods
    }
}
