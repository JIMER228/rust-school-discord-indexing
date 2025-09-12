using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AutoRestart", "crash", "2.5.1")]
    [Description("All features are managed through an intuitive UI")]
    public class AutoRestart : RustPlugin
    {
        #region Fields & References

        // Plugin References
        [PluginReference] private Plugin Economics;
        [PluginReference] private Plugin ServerRewards;

        // Constants
        private const string CurrentVersion = "2.5.1";
        private const int DISCORD_SUCCESS_CODE = 204;
        private const float DISCORD_ONLINE_DELAY = 300f;
        private const float RESTART_DELAY = 65f;
        private const float MINIMUM_CHECK_INTERVAL = 60f;
        private const int MINIMUM_RESTART_INTERVAL = 60;
        private const string PERMISSION_USE = "autorestart.use";
        private const string CHECKBOX_ON_SOUND = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
        private const string CHECKBOX_OFF_SOUND = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";

        // Configurations & Data Storage
        private ConfigData config;
        private List<int> notifiedWarningTimes = new List<int>();
        private Dictionary<string, string> _inputFieldValues = new Dictionary<string, string>();
        private Dictionary<string, string> _searchInputValues = new Dictionary<string, string>();
        private HashSet<ulong> rewardedPlayers = new HashSet<ulong>();
        private DateTime rewardStartTime = DateTime.MinValue;

        // Timers & Time Tracking
        private List<TimeSpan> restartTimes = new List<TimeSpan>();
        private List<Timer> activeTimers = new List<Timer>();
        private Timer nextRestartTimer;
        private Timer rewardLimitTimer;
        private DateTime lastRestartTime;
        private DateTime lastRestartCheck = DateTime.MinValue;

        // State Management
        private static bool hasSentDiscordOnlineNotification = false;
        private bool hasSentFinalRestartNotification = false;
        private bool isCustomRestart = false;
        private bool isCancelled = false;
        private bool isCustomUICreated = false;
        private bool isShuttingDown;
        private int customRestartTime = 0;

        // UI & Messages
        private string lastChatMessage = string.Empty;
        private string lastUIMessage = string.Empty;

        #endregion

        #region Configuration

        private class ConfigData
        {
            public string Version { get; set; } = CurrentVersion;
            public RestartConfig Restart { get; set; } = new RestartConfig();
            public PlayerRestrictionConfig PlayerRestrictions { get; set; } = new PlayerRestrictionConfig();
            public AlertConfig Alerts { get; set; } = new AlertConfig();
            public UpdateCheckConfig UpdateCheck { get; set; } = new UpdateCheckConfig();
            public UIConfig UI { get; set; } = new UIConfig();
            public DiscordConfig Discord { get; set; } = new DiscordConfig();
            public bool DisableDiscordCountdownMessages { get; set; } = false;
            public RewardsConfig Rewards { get; set; } = new RewardsConfig();
        }

        private class RestartConfig
        {
            public List<string> RestartTimes { get; set; } = new List<string>();
            public string AlertSound { get; set; } = "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab";
            public bool UseSound { get; set; } = true;
            public bool UseChatAlerts { get; set; } = true;
            public bool SkipWipeDays { get; set; } = false;
            public List<string> WipeDays { get; set; } = new List<string>();
        }

        private class PlayerRestrictionConfig
        {
            public int MaxPlayersBeforeCancel { get; set; } = 10;
            public bool RestrictPlayerCount { get; set; } = false;
        }

        public class AlertConfig
        {
            public List<int> InGameWarningTimes { get; set; } = new List<int>();
            public List<int> DiscordWarningTimes { get; set; } = new List<int>();
        }

        private class UpdateCheckConfig
        {
            public bool CheckForUpdates { get; set; } = false;
            public int CheckInterval { get; set; } = 600;
        }

        private class UIConfig
        {
            public bool UseCustomUI { get; set; } = false;
            public bool UseRustUI { get; set; } = true;
            public CustomUISettings CustomUI { get; set; } = new CustomUISettings();
        }

        private class CustomUISettings
        {
            public string AnchorMin { get; set; } = "0 0.5";
            public string AnchorMax { get; set; } = "0 0.5";
            public string OffsetMin { get; set; } = "10 -45.9695";
            public string OffsetMax { get; set; } = "177.325 46.7435";
            public string CheckIcon { get; set; } = "✓";
            public string CrossIcon { get; set; } = "✗";
            public string CheckIconColor { get; set; } = "0.5568627 0.7764706 0.1843137 1";
            public string CrossIconColor { get; set; } = "0.7764706 0.5137255 0.4196078 1";

            [JsonIgnore]
            public string BackgroundColor
            {
                get => _backgroundColor;
                set
                {
                    _backgroundColor = value;
                    BackgroundColorHex = value.ToHex();
                }
            }

            private string _backgroundColor = "0.3137255 0.5843138 0.772549 0.85";

            public string BackgroundColorHex { get; private set; } = "#5094C5";

            public int TitleFontSize { get; set; } = 12;
            public int MessageFontSize { get; set; } = 10;
        }

        private class DiscordConfig
        {
            public string WebhookUrl { get; set; } = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            public string ServerName { get; set; } = "Rust Server";
            public bool DiscordNotifications { get; set; } = false;
            public bool UseFullMinuteCountdown { get; set; } = true;
        }

        private class RewardsConfig
        {
            public bool EnableRewards { get; set; } = false;
            public bool UseEconomics { get; set; } = false;
            public bool UseServerRewards { get; set; } = false;
            public Dictionary<string, int> ItemRewards { get; set; } = new Dictionary<string, int>();
            public double EconomicsAmount { get; set; } = 0;
            public int ServerRewardsPoints { get; set; } = 0;
            public int RewardTimeLimit { get; set; } = 300;
        }

        
        private class RewardsData
        {
            public List<ulong> RewardedPlayers { get; set; } = new List<ulong>();
            public DateTime RewardStartTime { get; set; }
        }

        #endregion

        #region Oxide Hooks

		private void Init()
		{
			permission.RegisterPermission(PERMISSION_USE, this);
			LoadConfigValues();
			UpdateLanguage();
			lastRestartTime = DateTime.UtcNow;
			isShuttingDown = false;
			
			DestroyAllUI();
			ClearAllTimers();
            LoadRewardsData();
			lastChatMessage = string.Empty;
			lastUIMessage = string.Empty;
			isCustomUICreated = false;
			notifiedWarningTimes.Clear();
			hasSentFinalRestartNotification = false;
		}

		private void OnServerInitialized()
		{
			try 
			{
				timer.Once(5f, () =>
				{
					LogInfo("Initializing restart schedule...");
					if (restartTimes.Count > 0)
					{
						LogInfo($"Configured restart times: {string.Join(", ", restartTimes.Select(t => t.ToString(@"hh\:mm")))}");
        
						if (nextRestartTimer == null && !isCustomRestart)
						{
							ScheduleNextRestartQuietly();
						}
					}
					else
					{
						LogInfo("No restart times configured.");
					}

					if (config.Discord.DiscordNotifications && !hasSentDiscordOnlineNotification)
					{
						NotifyDiscordOnline();
					}

                    if (config.Rewards.EnableRewards)
                    {
                        rewardedPlayers.Clear();
                        timer.Once(10f, () => StartRewardPeriod());
                    }

					timer.Every(3600f, () =>
					{
						if (nextRestartTimer != null)
						{
							var nextRestart = DateTime.UtcNow.AddSeconds(nextRestartTimer.Repetitions);
							LogInfo($"Periodic check - Next restart at: {nextRestart} UTC");
						}
						else if (restartTimes.Count > 0 && !isCustomRestart && !isCancelled)
						{
							LogWarning("Periodic check - No restart scheduled! Attempting to reschedule...");
							ScheduleNextRestartQuietly();
						}
					});
				});
			}
			catch (Exception ex)
			{
				LogError($"Error in OnServerInitialized: {ex}", ex);
			}
		}

        private void ScheduleNextRestartQuietly()
        {
            if (isCustomRestart || isCancelled)
            {
                LogInfo($"Skipping restart schedule: CustomRestart={isCustomRestart}, Cancelled={isCancelled}");
                return;
            }

            if ((DateTime.UtcNow - lastRestartCheck).TotalSeconds < MINIMUM_CHECK_INTERVAL)
            {
                LogInfo("Skipping restart check - too soon since last check");
                return;
            }
            lastRestartCheck = DateTime.UtcNow;

            ClearAllTimers();

            if (restartTimes.Count == 0)
            {
                LogWarning("No restart times configured");
                return;
            }

            try
            {
                DateTime now = DateTime.UtcNow;
                DateTime nextRestart = FindNextRestartTime(now);
                
                if ((nextRestart - lastRestartTime).TotalSeconds < MINIMUM_RESTART_INTERVAL)
                {
                    LogWarning($"Preventing duplicate restart: Last restart at {lastRestartTime}, next scheduled for {nextRestart}");
                    nextRestart = nextRestart.AddDays(1);
                }

                TimeSpan timeUntilRestart = nextRestart - now;

                if (timeUntilRestart.TotalSeconds <= 0)
                {
                    LogWarning("Invalid restart time (in past), scheduling for next day");
                    nextRestart = nextRestart.AddDays(1);
                    timeUntilRestart = nextRestart - now;
                }

                int totalSeconds = (int)timeUntilRestart.TotalSeconds;
                
                LogInfo($"Scheduling next restart:");
                LogInfo($"Current time (UTC): {now}");
                LogInfo($"Next restart (UTC): {nextRestart}");
                LogInfo($"Time until restart: {timeUntilRestart.TotalHours:F2} hours");

				if (timeUntilRestart.TotalSeconds > 0 && CanScheduleRestart(nextRestart))
				{
					nextRestartTimer = timer.Once((float)timeUntilRestart.TotalSeconds, CheckPlayerCountAndRestart);
					activeTimers.Add(nextRestartTimer);

					if (config.Alerts.InGameWarningTimes != null && config.Alerts.InGameWarningTimes.Count > 0)
					{
						foreach (int warningMinutes in config.Alerts.InGameWarningTimes)
						{
							int warningSeconds = warningMinutes * 60;
							if (warningSeconds < totalSeconds)
							{
								Timer warningTimer = timer.Once(totalSeconds - warningSeconds, () =>
								{
									if (!isCancelled)
									{
										ShowMessage(warningSeconds.ToString(), false);
										
										if (warningSeconds == 60)
										{
											customRestartTime = 60;
											CountdownFromMinute();
										}
									}
								});
								activeTimers.Add(warningTimer);
							}
						}
					}

					if (!config.Alerts.InGameWarningTimes.Contains(1))
					{
						Timer lastMinuteTimer = timer.Once(totalSeconds - 60, () =>
						{
							if (!isCancelled)
							{
								customRestartTime = 60;
								CountdownFromMinute();
							}
						});
						activeTimers.Add(lastMinuteTimer);
					}
				}
                else
                {
                    LogWarning("Invalid restart time calculated, skipping schedule");
                    timer.Once(300f, ScheduleNextRestartQuietly);
                }
            }
            catch (Exception ex)
            {
                LogError($"Error scheduling next restart: {ex}", ex);
                timer.Once(300f, ScheduleNextRestartQuietly);
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (!string.IsNullOrEmpty(lastUIMessage))
            {
                ShowMessage(lastUIMessage, false);
            }
        }

        #endregion

        #region Configuration Methods

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                Alerts = new AlertConfig
                {
                    InGameWarningTimes = new List<int>(),
                    DiscordWarningTimes = new List<int>()
                }
            };

            Puts("Default config loaded without default WarningTimes.");
            
            SaveConfig();
        }

        private void SaveConfig()
        {
            Puts("Saving configuration to AutoRestart.json");
            Config.WriteObject(config, true);
            Puts("Configuration written to AutoRestart.json, please reload the plugin.");
        }

        private void LoadConfigValues()
        {
            if (!Config.Exists())
            {
                Puts("Config does not exist, creating empty config.");
                LoadDefaultConfig();
                return;
            }

            config = Config.ReadObject<ConfigData>();
            
            if (config.Alerts.InGameWarningTimes == null)
            {
                config.Alerts.InGameWarningTimes = new List<int>();
            }
            if (config.Alerts.DiscordWarningTimes == null)
            {
                config.Alerts.DiscordWarningTimes = new List<int>();
            }
            
            if (config.Restart.WipeDays == null)
            {
	            config.Restart.WipeDays = new List<string>();
            }
            
            else if (config.Restart.WipeDays.Count == 1 && config.Restart.WipeDays[0] == "Friday")
            {
	            config.Restart.WipeDays.Clear();
            }

            if (config.Version != CurrentVersion)
            {
                Puts("Config version mismatch, updating config.");
                UpdateConfig();
            }
            
            config.UI.CustomUI.BackgroundColor = config.UI.CustomUI.BackgroundColor;

            LoadRestartTimes();
        }


        private void UpdateConfig()
        {
            PrintWarning("Updating configuration to the latest version.");
            
            config.Version = CurrentVersion;

            SaveConfig();
            PrintWarning("Configuration updated successfully.");
        }

		private void LoadRestartTimes()
		{
			try
			{
				LogInfo("Loading restart times...");
				restartTimes.Clear();
				HashSet<TimeSpan> uniqueRestartTimes = new HashSet<TimeSpan>();
		
				foreach (var timeString in config.Restart.RestartTimes)
				{
					if (TimeSpan.TryParse(timeString, out TimeSpan restartTime))
					{
						if (restartTime.TotalHours >= 0 && restartTime.TotalHours < 24)
						{
							if (uniqueRestartTimes.Add(restartTime))
							{
								restartTimes.Add(restartTime);
								LogInfo($"Valid restart time added: {timeString}");
							}
							else
							{
								LogWarning($"Duplicate restart time ignored: {timeString}");
							}
						}
						else
						{
							LogError($"Invalid time range (must be 00:00-23:59): {timeString}");
						}
					}
					else
					{
						LogError($"Invalid time format (use HH:mm): {timeString}");
					}
				}

				if (restartTimes.Count == 0)
				{
					LogWarning("No valid restart times configured!");
				}
				else
				{
					LogInfo($"Successfully loaded {restartTimes.Count} restart time(s)");
				}
			}
			catch (Exception ex)
			{
				LogError("Error loading restart times", ex);
			}
		}

        #endregion

        #region Language Methods

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(GetDefaultMessages(), this);
        }

		private void UpdateLanguage()
		{
			var langFile = $"{Name}.json";
			var langPath = Path.Combine(Interface.Oxide.LangDirectory, "en", langFile);

			Dictionary<string, string> messages;
			if (File.Exists(langPath))
			{
				messages = Config.ReadObject<Dictionary<string, string>>(langPath);
				
				var currentVersion = messages.ContainsKey("Version") ? messages["Version"] : "0.0.0";

				if (currentVersion != CurrentVersion)
				{
					PrintWarning($"Updating language file from version {currentVersion} to {CurrentVersion}");

					messages["Version"] = CurrentVersion;
					Config.WriteObject(messages, false, langPath);
					PrintWarning("Language file version updated while preserving existing translations.");
				}
			}
			else
			{
				messages = GetDefaultMessages();
			}

			lang.RegisterMessages(messages, this);
		}

        private void UpdateLanguageFile(Dictionary<string, string> messages, string langPath)
        {
            PrintWarning($"Updating language file from version {messages["Version"]} to {CurrentVersion}");

            var defaultMessages = GetDefaultMessages();
            bool changed = false;

            foreach (var entry in defaultMessages)
            {
                if (!messages.ContainsKey(entry.Key) || messages[entry.Key] != entry.Value)
                {
                    messages[entry.Key] = entry.Value;
                    changed = true;
                }
            }

            if (changed)
            {
                messages["Version"] = CurrentVersion;
                Config.WriteObject(messages, false, langPath);
                PrintWarning($"Language file updated successfully. New version: {CurrentVersion}");
            }
        }

        private Dictionary<string, string> GetDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                ["Version"] = CurrentVersion,
                ["ChatRestartAlert"] = "Server restart scheduled in {0}.",
                ["ChatRestartCanceled"] = "The server restart has been canceled.",
                ["ChatRestartNow"] = "RESTARTING...",
                ["ChatRestartCanceledTooManyPlayers"] = "Restart canceled due to too many players.",
                ["NoRestartScheduled"] = "No restart is currently scheduled.",
                ["RustUITitle"] = "SCHEDULED SERVER RESTART",
                ["RustUIMessage"] = "The server will restart in {0}.",
                ["CustomUITitle"] = "SCHEDULED SERVER RESTART",
                ["CustomUIMessage"] = "The server will restart in {0}.",
                ["NoPermission"] = "You do not have permission to use this command.",
                ["Usage"] = "Usage: /restart <time>[s|m|h] (e.g., 30s, 5m, 1h)",
                ["InvalidNumber"] = "Invalid time format. Use <number>[s|m|h] (e.g., 30s, 5m, 1h)",
                ["UpdateDetected"] = "A uMod update has been detected. The server will restart to apply the update.",
                ["DiscordRestartAlert"] = "🔄 **SERVER RESTART**\nServer will restart in {0}.",
                ["DiscordRestarting"] = "⚠️ **RESTARTING**\nServer is restarting now. Please wait 5 minutes for the server to be back online.",
                ["DiscordOnline"] = "✅ **SERVER ONLINE**\nServer is now online and ready to play!",
                ["TimeMinute"] = "minute",
                ["TimeMinutes"] = "minutes",
                ["TimeHour"] = "hour",
                ["TimeHours"] = "hours",
                ["TimeSecond"] = "second",
                ["TimeSeconds"] = "seconds",
                ["RewardEconomicsReceived"] = "You received ${0} from Economics reward!",
                ["RewardPointsReceived"] = "You received {0} RP from ServerRewards!",
                ["RewardItemReceived"] = "You received {0}x {1} from Item rewards!",
                ["RewardTimeLeft"] = "You received rewards for connecting during server restart. Rewards will be available for {0} more.",
                ["RewardPeriodEnded"] = "The reward period has ended."
            };

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Version"] = CurrentVersion,
                ["ChatRestartAlert"] = "Перезапуск сервера запланирован через {0}.",
                ["ChatRestartCanceled"] = "Перезапуск сервера был отменен.",
                ["ChatRestartNow"] = "ПЕРЕЗАПУСК...",
                ["ChatRestartCanceledTooManyPlayers"] = "Перезапуск отменен из-за большого количества игроков.",
                ["NoRestartScheduled"] = "В настоящее время перезапуск не запланирован.",
                ["RustUITitle"] = "ЗАПЛАНИРОВАННЫЙ ПЕРЕЗАПУСК СЕРВЕРА",
                ["RustUIMessage"] = "Сервер перезапустится через {0}.",
                ["CustomUITitle"] = "ЗАПЛАНИРОВАННЫЙ ПЕРЕЗАПУСК СЕРВЕРА",
                ["CustomUIMessage"] = "Сервер перезапустится через {0}.",
                ["NoPermission"] = "У вас нет прав для использования этой команды.",
                ["Usage"] = "Использование: /restart <время>[s|m|h] (пример: 30s, 5m, 1h)",
                ["InvalidNumber"] = "Неверный формат времени. Используйте <число>[s|m|h] (пример: 30s, 5m, 1h)",
                ["UpdateDetected"] = "Обнаружено обновление uMod. Сервер будет перезапущен для применения обновления.",
                ["TimeMinute"] = "минута",
                ["TimeMinutes"] = "минут",
                ["TimeHour"] = "час",
                ["TimeHours"] = "часов",
                ["TimeSecond"] = "секунда",
                ["TimeSeconds"] = "секунд",
                ["RewardEconomicsReceived"] = "Вы получили ${0} от Economics!",
                ["RewardPointsReceived"] = "Вы получили {0} RP от ServerRewards!",
                ["RewardItemReceived"] = "Вы получили {0}x {1} из награды предметами!",
                ["RewardTimeLeft"] = "Вы получили награды за подключение во время перезапуска сервера. Награды будут доступны еще {0}.",
                ["RewardPeriodEnded"] = "Период получения наград закончился."
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Version"] = CurrentVersion,
                ["ChatRestartAlert"] = "Перезапуск сервера заплановано через {0}.",
                ["ChatRestartCanceled"] = "Перезапуск сервера було скасовано.",
                ["ChatRestartNow"] = "ПЕРЕЗАПУСК...",
                ["ChatRestartCanceledTooManyPlayers"] = "Перезапуск скасовано через велику кількість гравців.",
                ["NoRestartScheduled"] = "На даний момент перезапуск не заплановано.",
                ["RustUITitle"] = "ЗАПЛАНОВАНИЙ ПЕРЕЗАПУСК СЕРВЕРА",
                ["RustUIMessage"] = "Сервер перезапуститься через {0}.",
                ["CustomUITitle"] = "ЗАПЛАНОВАНИЙ ПЕРЕЗАПУСК СЕРВЕРА",
                ["CustomUIMessage"] = "Сервер перезапуститься через {0}.",
                ["NoPermission"] = "У вас немає прав для використання цієї команди.",
                ["Usage"] = "Використання: /restart <час>[s|m|h] (приклад: 30s, 5m, 1h)",
                ["InvalidNumber"] = "Невірний формат часу. Використовуйте <число>[s|m|h] (приклад: 30s, 5m, 1h)",
                ["UpdateDetected"] = "Виявлено оновлення uMod. Сервер буде перезапущено для застосування оновлення.",
                ["TimeMinute"] = "хвилина",
                ["TimeMinutes"] = "хвилин",
                ["TimeHour"] = "година",
                ["TimeHours"] = "годин",
                ["TimeSecond"] = "секунда",
                ["TimeSeconds"] = "секунд",
                ["RewardEconomicsReceived"] = "Ви отримали ${0} від Economics!",
                ["RewardPointsReceived"] = "Ви отримали {0} RP від ServerRewards!",
                ["RewardItemReceived"] = "Ви отримали {0}x {1} з нагороди предметами!",
                ["RewardTimeLeft"] = "Ви отримали нагороди за підключення під час перезапуску сервера. Нагороди будуть доступні ще {0}.",
                ["RewardPeriodEnded"] = "Період отримання нагород закінчився."
            }, this, "uk");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Version"] = CurrentVersion,
                ["ChatRestartAlert"] = "Reinicio del servidor programado en {0}.",
                ["ChatRestartCanceled"] = "El reinicio del servidor ha sido cancelado.",
                ["ChatRestartNow"] = "REINICIANDO...",
                ["ChatRestartCanceledTooManyPlayers"] = "Reinicio cancelado debido a demasiados jugadores.",
                ["NoRestartScheduled"] = "No hay reinicio programado actualmente.",
                ["RustUITitle"] = "REINICIO DE SERVIDOR PROGRAMADO",
                ["RustUIMessage"] = "El servidor se reiniciará en {0}.",
                ["CustomUITitle"] = "REINICIO DE SERVIDOR PROGRAMADO",
                ["CustomUIMessage"] = "El servidor se reiniciará en {0}.",
                ["NoPermission"] = "No tienes permiso para usar este comando.",
                ["Usage"] = "Uso: /restart <tiempo>[s|m|h] (ejemplo: 30s, 5m, 1h)",
                ["InvalidNumber"] = "Formato de tiempo inválido. Usa <número>[s|m|h] (ejemplo: 30s, 5m, 1h)",
                ["UpdateDetected"] = "Se ha detectado una actualización de uMod. El servidor se reiniciará para aplicar la actualización.",
                ["TimeMinute"] = "minuto",
                ["TimeMinutes"] = "minutos",
                ["TimeHour"] = "hora",
                ["TimeHours"] = "horas",
                ["TimeSecond"] = "segundo",
                ["TimeSeconds"] = "segundos",
                ["RewardEconomicsReceived"] = "¡Has recibido ${0} de recompensa Economics!",
                ["RewardPointsReceived"] = "¡Has recibido {0} RP de ServerRewards!",
                ["RewardItemReceived"] = "¡Has recibido {0}x {1} de recompensa de objetos!",
                ["RewardTimeLeft"] = "Has recibido recompensas por conectarte durante el reinicio del servidor. Las recompensas estarán disponibles por {0} más.",
                ["RewardPeriodEnded"] = "El período de recompensas ha terminado."
            }, this, "es-ES");


            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Version"] = CurrentVersion,
                ["ChatRestartAlert"] = "Reinício do servidor programado em {0}.",
                ["ChatRestartCanceled"] = "O reinício do servidor foi cancelado.",
                ["ChatRestartNow"] = "REINICIANDO...",
                ["ChatRestartCanceledTooManyPlayers"] = "Reinício cancelado devido ao excesso de jogadores.",
                ["NoRestartScheduled"] = "Nenhum reinício está programado no momento.",
                ["RustUITitle"] = "REINÍCIO DO SERVIDOR PROGRAMADO",
                ["RustUIMessage"] = "O servidor será reiniciado em {0}.",
                ["CustomUITitle"] = "REINÍCIO DO SERVIDOR PROGRAMADO",
                ["CustomUIMessage"] = "O servidor será reiniciado em {0}.",
                ["NoPermission"] = "Você não tem permissão para usar este comando.",
                ["Usage"] = "Uso: /restart <tempo>[s|m|h] (exemplo: 30s, 5m, 1h)",
                ["InvalidNumber"] = "Formato de tempo inválido. Use <número>[s|m|h] (exemplo: 30s, 5m, 1h)",
                ["UpdateDetected"] = "Uma atualização do uMod foi detectada. O servidor será reiniciado para aplicar a atualização.",
                ["TimeMinute"] = "minuto",
                ["TimeMinutes"] = "minutos",
                ["TimeHour"] = "hora",
                ["TimeHours"] = "horas",
                ["TimeSecond"] = "segundo",
                ["TimeSeconds"] = "segundos",
                ["RewardEconomicsReceived"] = "Você recebeu ${0} de recompensa Economics!",
                ["RewardPointsReceived"] = "Você recebeu {0} RP de ServerRewards!",
                ["RewardItemReceived"] = "Você recebeu {0}x {1} de recompensa de itens!",
                ["RewardTimeLeft"] = "Você recebeu recompensas por se conectar durante o reinício do servidor. As recompensas estarão disponíveis por mais {0}.",
                ["RewardPeriodEnded"] = "O período de recompensas terminou."
            }, this, "pt-BR");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Version"] = CurrentVersion,
                ["ChatRestartAlert"] = "Redémarrage du serveur prévu dans {0}.",
                ["ChatRestartCanceled"] = "Le redémarrage du serveur a été annulé.",
                ["ChatRestartNow"] = "REDÉMARRAGE...",
                ["ChatRestartCanceledTooManyPlayers"] = "Redémarrage annulé en raison de trop de joueurs.",
                ["NoRestartScheduled"] = "Aucun redémarrage n'est actuellement prévu.",
                ["RustUITitle"] = "REDÉMARRAGE DU SERVEUR PROGRAMMÉ",
                ["RustUIMessage"] = "Le serveur redémarrera dans {0}.",
                ["CustomUITitle"] = "REDÉMARRAGE DU SERVEUR PROGRAMMÉ",
                ["CustomUIMessage"] = "Le serveur redémarrera dans {0}.",
                ["NoPermission"] = "Vous n'avez pas la permission d'utiliser cette commande.",
                ["Usage"] = "Utilisation: /restart <temps>[s|m|h] (exemple: 30s, 5m, 1h)",
                ["InvalidNumber"] = "Format de temps invalide. Utilisez <nombre>[s|m|h] (exemple: 30s, 5m, 1h)",
                ["UpdateDetected"] = "Une mise à jour uMod a été détectée. Le serveur va redémarrer pour appliquer la mise à jour.",
                ["TimeMinute"] = "minute",
                ["TimeMinutes"] = "minutes",
                ["TimeHour"] = "heure",
                ["TimeHours"] = "heures",
                ["TimeSecond"] = "seconde",
                ["TimeSeconds"] = "secondes",
                ["RewardEconomicsReceived"] = "Vous avez reçu ${0} de récompense Economics !",
                ["RewardPointsReceived"] = "Vous avez reçu {0} RP de ServerRewards !",
                ["RewardItemReceived"] = "Vous avez reçu {0}x {1} de récompense d'objets !",
                ["RewardTimeLeft"] = "Vous avez reçu des récompenses pour vous être connecté pendant le redémarrage du serveur. Les récompenses seront disponibles pendant encore {0}.",
                ["RewardPeriodEnded"] = "La période de récompenses est terminée."
            }, this, "fr");

            return messages;
        }

        #endregion

        #region Command Handlers

        [ChatCommand("crestart")]
        private void CmdCRestart(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                player.ChatMessage(GetLang("NoPermission", player.UserIDString));
                return;
            }

            if (args == null || args.Length == 0)
            {
                ShowCRestartUI(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "rewards":
                    ShowRewardsUI(player);
                    break;
                case "help":
                    ShowCommandHelp(player);
                    break;
                default:
                    ShowCRestartUI(player);
                    break;
            }
        }

        private void ShowCommandHelp(BasePlayer player)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available CRestart Commands:");
            sb.AppendLine("/crestart - Opens the main configuration UI");
            sb.AppendLine("/crestart rewards - Opens the rewards configuration UI");
            sb.AppendLine("/crestart help - Shows this help message");

            player.ChatMessage(sb.ToString());
        }

		[ChatCommand("restart")]
		private void CommandRestart(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
			{
				player.ChatMessage(GetLang("NoPermission", player.UserIDString));
				return;
			}

			if (args == null || args.Length < 1)
			{
				player.ChatMessage(GetLang("Usage", player.UserIDString));
				return;
			}

			int seconds = ParseTimeWithUnit(args[0]);
			if (seconds <= 0)
			{
				player.ChatMessage(GetLang("InvalidNumber", player.UserIDString));
				return;
			}

			LogInfo($"Restart command received: Input='{args[0]}', Converted to {seconds} seconds");
			StartCustomRestart(seconds);
		}

		[ChatCommand("restartstop")]
		private void ChatRestartStopCommand(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
			{
				SendReply(player, GetLang("NoPermission", player.UserIDString));
				return;
			}

			bool hasActiveRestart = nextRestartTimer != null || activeTimers.Count > 0 || isCustomRestart;
			
			if (hasActiveRestart)
			{
				CancelRestart();
			}
			else
			{
				SendReply(player, GetLang("NoRestartScheduled", player.UserIDString));
			}
		}

		[ConsoleCommand("restart")]
		private void ConsoleRestart(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside && !arg.IsAdmin)
			{
				Puts("You don't have permission to use this command.");
				return;
			}

			if (arg.Args == null || arg.Args.Length < 1)
			{
				Puts("Usage: restart <time>[s|m|h] (e.g., 30s, 5m, 1h)");
				return;
			}

			int seconds = ParseTimeWithUnit(arg.Args[0]);
			if (seconds <= 0)
			{
				Puts("Invalid time format. Use <number>[s|m|h] (e.g., 30s, 5m, 1h)");
				return;
			}

			StartCustomRestart(seconds);
			Puts($"Initiated a custom restart in {seconds} seconds.");
		}

        [ConsoleCommand("restartstop")]
        private void ConsoleRestartStopCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsServerside || arg.IsAdmin)
            {
                if (isCustomRestart || nextRestartTimer != null)
                {
                    CancelRestart();
                }
                else
                {
                    Puts("No restart is currently scheduled.");
                }
            }
            else
            {
                Puts("You don't have permission to use this command.");
            }
        }
        
        [ConsoleCommand("restart.list")]
        private void CmdListNextRestarts(ConsoleSystem.Arg arg)
        {
	        if (!arg.IsAdmin) return;

	        var sb = new StringBuilder();
	        sb.AppendLine("Upcoming Restarts (UTC):");
    
	        if (config.Restart.SkipWipeDays && config.Restart.WipeDays != null && config.Restart.WipeDays.Count > 0)
	        {
		        sb.AppendLine("Skipped Wipe Days:");
		        foreach (var day in config.Restart.WipeDays)
		        {
			        sb.AppendLine($"- {day}");
		        }
		        sb.AppendLine();
	        }

	        DateTime now = DateTime.UtcNow;
	        var upcomingRestarts = new List<DateTime>();

	        foreach (var time in restartTimes)
	        {
		        var todayTime = now.Date.Add(time);
		        if (todayTime > now)
		        {
			        if (!ShouldSkipRestart(todayTime))
			        {
				        upcomingRestarts.Add(todayTime);
			        }
		        }
	        }

	        foreach (var time in restartTimes)
	        {
		        var tomorrowTime = now.Date.AddDays(1).Add(time);
		        if (!ShouldSkipRestart(tomorrowTime))
		        {
			        upcomingRestarts.Add(tomorrowTime);
		        }
	        }

	        if (upcomingRestarts.Count == 0)
	        {
		        sb.AppendLine("No upcoming restarts found.");
	        }
	        else
	        {
		        foreach (var restart in upcomingRestarts.OrderBy(x => x).Take(5))
		        {
			        var timeUntil = restart - now;
			        string timeUntilString = FormatTimeUntil(timeUntil);
			        sb.AppendLine($"- {restart:yyyy-MM-dd HH:mm:ss} (in {timeUntilString})");
		        }
	        }

	        Puts(sb.ToString());
        }
        
        private string FormatTimeUntil(TimeSpan timeSpan)
        {
	        if (timeSpan.TotalMinutes < 1)
	        {
		        return "less than a minute";
	        }
	        else if (timeSpan.TotalHours < 1)
	        {
		        return $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.Minutes == 1 ? "" : "s")}";
	        }
	        else if (timeSpan.TotalHours < 24)
	        {
		        int hours = (int)timeSpan.TotalHours;
		        int minutes = timeSpan.Minutes;
        
		        if (minutes == 0)
			        return $"{hours} hour{(hours == 1 ? "" : "s")}";
		        else
			        return $"{hours} hour{(hours == 1 ? "" : "s")} {minutes} minute{(minutes == 1 ? "" : "s")}";
	        }
	        else
	        {
		        int days = (int)timeSpan.TotalDays;
		        int hours = timeSpan.Hours;
        
		        if (hours == 0)
			        return $"{days} day{(days == 1 ? "" : "s")}";
		        else
			        return $"{days} day{(days == 1 ? "" : "s")} {hours} hour{(hours == 1 ? "" : "s")}";
	        }
        }

		private bool ShouldSkipRestart(DateTime restartTime)
		{
			if (!config.Restart.SkipWipeDays || config.Restart.WipeDays == null || config.Restart.WipeDays.Count == 0)
				return false;

			string dayName = restartTime.DayOfWeek.ToString();
			return config.Restart.WipeDays.Contains(dayName, StringComparer.OrdinalIgnoreCase);
		}

        [ConsoleCommand("crestart.toggle")]
        private void CmdToggleSetting(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            var setting = arg.GetString(0);
            bool newValue = !GetConfigValue(setting);
            SetConfigValue(setting, newValue);

            string soundEffect = newValue ? CHECKBOX_ON_SOUND : CHECKBOX_OFF_SOUND;
            Effect.server.Run(soundEffect, player.transform.position);

            Puts($"Toggled setting: {setting} to {newValue}");
            SaveConfig();
    
            ShowCRestartUI(player);
        }
        
        [ConsoleCommand("crestart.input")]
        private void CmdInputSetting(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            if (arg.Args == null || arg.Args.Length < 2)
            {
                PrintError("Insufficient arguments provided for crestart.input command.");
                return;
            }

            string setting = arg.Args[0];
            string value = string.Join(" ", arg.Args.Skip(1).ToArray());

            Puts($"Received input for setting: {setting}, with value: {value}");

            string key = $"{player.UserIDString}_{setting}";
            _inputFieldValues[key] = value;

            if (string.IsNullOrEmpty(value))
            {
                Puts($"Error: No value provided for setting {setting}");
                return;
            }

            switch (setting)
            {
                case "InGameWarningTimes":
                    try
                    {
                        var newInGameWarningTimes = value.Split(',')
                            .Select(s => int.Parse(s.Trim()))
                            .Where(i => i > 0)
                            .Distinct()
                            .OrderByDescending(x => x)
                            .ToList();

                        config.Alerts.InGameWarningTimes = newInGameWarningTimes;
                        Puts($"Updated InGameWarningTimes: {string.Join(", ", config.Alerts.InGameWarningTimes)}");
                    }
                    catch (FormatException)
                    {
                        PrintError($"Invalid format for InGameWarningTimes: {value}");
                    }
                    break;

                case "DiscordWarningTimes":
                    try
                    {
                        var newDiscordWarningTimes = value.Split(',')
                            .Select(s => int.Parse(s.Trim()))
                            .Where(i => i > 0)
                            .Distinct()
                            .OrderByDescending(x => x)
                            .ToList();

                        config.Alerts.DiscordWarningTimes = newDiscordWarningTimes;
                        Puts($"Updated DiscordWarningTimes: {string.Join(", ", config.Alerts.DiscordWarningTimes)}");
                    }
                    catch (FormatException)
                    {
                        PrintError($"Invalid format for DiscordWarningTimes: {value}");
                    }
                    break;

                case "RestartTimes":
                    try
                    {
                        var newRestartTimes = value.Split(',').Select(s => s.Trim()).ToList();
                        config.Restart.RestartTimes = newRestartTimes;
                        LoadRestartTimes();
                        Puts($"Updated RestartTimes: {string.Join(", ", config.Restart.RestartTimes)}");
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Error updating RestartTimes: {ex.Message}");
                    }
                    break;

                case "AlertSound":
                    config.Restart.AlertSound = value;
                    Puts($"Updated AlertSound: {value}");
                    break;

                case "MaxPlayersBeforeCancel":
                    if (int.TryParse(value, out int maxPlayers))
                    {
                        config.PlayerRestrictions.MaxPlayersBeforeCancel = maxPlayers;
                        Puts($"Updated MaxPlayersBeforeCancel: {maxPlayers}");
                    }
                    else
                    {
                        PrintError($"Invalid MaxPlayersBeforeCancel value: {value}");
                    }
                    break;

                case "CheckInterval":
                    if (int.TryParse(value, out int interval))
                    {
                        config.UpdateCheck.CheckInterval = interval;
                        Puts($"Updated CheckInterval: {interval}");
                    }
                    else
                    {
                        PrintError($"Invalid CheckInterval value: {value}");
                    }
                    break;

                case "WebhookUrl":
                    config.Discord.WebhookUrl = value;
                    Puts($"Updated WebhookUrl: {value}");
                    break;

                case "ServerName":
                    config.Discord.ServerName = value;
                    Puts($"Updated ServerName: {value}");
                    break;
                
                case "WipeDays":
	                try
	                {
		                var newWipeDays = value.Split(',')
			                .Select(s => s.Trim())
			                .Where(s => !string.IsNullOrEmpty(s))
			                .Select(s => char.ToUpper(s[0]) + s.Substring(1).ToLower())
			                .Where(day => Enum.TryParse<DayOfWeek>(day, out _))
			                .ToList();

		                if (newWipeDays.Any())
		                {
			                config.Restart.WipeDays = newWipeDays;
			                Puts($"Updated WipeDays: {string.Join(", ", newWipeDays)}");
            
			                string inputKey = $"{player.UserIDString}_WipeDays";
			                _inputFieldValues[inputKey] = string.Join(", ", newWipeDays);
            
			                ShowCRestartUI(player);
		                }
		                else
		                {
			                PrintError($"No valid days found in input: {value}");
		                }
	                }
	                catch (Exception ex)
	                {
		                PrintError($"Error updating WipeDays: {ex.Message}");
	                }
	                break;

                default:
                    Puts($"Setting '{setting}' is not recognized.");
                    return;
            }

            SaveConfig();
            LoadConfigValues();
            UpdateCRestartUI(player);
        }


        [ConsoleCommand("crestart.submit")]
        private void CmdSubmitSetting(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            var setting = arg.GetString(0);
            var value = arg.GetString(1, "");

            if (string.IsNullOrEmpty(value))
            {
                Puts($"Error: No value provided for setting {setting}");
                return;
            }

            UpdateConfigValue(setting, value);
            SaveConfig();
            Puts($"Config updated: {setting} = {value}");
    
            CuiHelper.DestroyUi(player, "InputFieldsPanel");
            CuiHelper.DestroyUi(player, "ScrollContainer");
            CuiHelper.DestroyUi(player, "ScrollContainer.View");
            CuiHelper.DestroyUi(player, "ScrollContainer.View.Content");
    
            ShowCRestartUI(player);
        }
        
        private void UpdateConfigValue(string setting, string value)
        {
            switch (setting)
            {
                case "InGameWarningTimes":
                    try
                    {
                        var newInGameWarningTimes = value.Split(',')
                            .Select(s => int.Parse(s.Trim()))
                            .Where(i => i > 0)
                            .Distinct()
                            .OrderByDescending(x => x)
                            .ToList();

                        config.Alerts.InGameWarningTimes = newInGameWarningTimes;
                        Puts($"Updated InGameWarningTimes: {string.Join(", ", config.Alerts.InGameWarningTimes)}");
                    }
                    catch (FormatException)
                    {
                        PrintError($"Invalid format for InGameWarningTimes: {value}");
                    }
                    break;

                case "DiscordWarningTimes":
                    try
                    {
                        var newDiscordWarningTimes = value.Split(',')
                            .Select(s => int.Parse(s.Trim()))
                            .Where(i => i > 0)
                            .Distinct()
                            .OrderByDescending(x => x)
                            .ToList();

                        config.Alerts.DiscordWarningTimes = newDiscordWarningTimes;
                        Puts($"Updated DiscordWarningTimes: {string.Join(", ", config.Alerts.DiscordWarningTimes)}");
                    }
                    catch (FormatException)
                    {
                        PrintError($"Invalid format for DiscordWarningTimes: {value}");
                    }
                    break;

                case "AlertSound":
                    config.Restart.AlertSound = value;
                    break;
                    
                case "MaxPlayersBeforeCancel":
                    if (int.TryParse(value, out int maxPlayers))
                        config.PlayerRestrictions.MaxPlayersBeforeCancel = maxPlayers;
                    break;
                    
                case "CheckInterval":
                    if (int.TryParse(value, out int interval))
                        config.UpdateCheck.CheckInterval = interval;
                    break;
                    
                case "WebhookUrl":
                    config.Discord.WebhookUrl = value;
                    break;
                    
                case "ServerName":
                    config.Discord.ServerName = value;
                    break;
                    
                case "RestartTimes":
                    try
                    {
                        config.Restart.RestartTimes = value.Split(',')
                            .Select(s => s.Trim())
                            .ToList();
                        LoadRestartTimes();
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Invalid RestartTimes format: {value}. Error: {ex.Message}");
                    }
                    break;
                
                case "WipeDays":
	                try
	                {
		                var newWipeDays = value.Split(',')
			                .Select(s => s.Trim())
			                .Where(s => !string.IsNullOrEmpty(s))
			                .Select(s => char.ToUpper(s[0]) + s.Substring(1).ToLower())
			                .Where(day => Enum.TryParse<DayOfWeek>(day, out _))
			                .ToList();

		                if (newWipeDays.Any())
		                {
			                config.Restart.WipeDays = newWipeDays;
			                Puts($"Updated WipeDays: {string.Join(", ", newWipeDays)}");
		                }
		                else
		                {
			                PrintError($"No valid days found in input: {value}");
		                }
	                }
	                catch (Exception ex)
	                {
		                PrintError($"Error updating WipeDays: {ex.Message}");
	                }
	                break;
            }
        }

        #endregion

        #region Restart Handling Methods

		private void StartCustomRestart(int seconds)
		{
			if (isCustomRestart) return;

			isCustomRestart = true;
			customRestartTime = seconds;
			isCancelled = false;

			ClearAllTimers();
			DestroyAllUI();

			Timer countdownTimer = timer.Once(seconds, CheckPlayerCountAndRestart);
			activeTimers.Add(countdownTimer);

			if (seconds > 60)
			{
				ShowMessage(seconds.ToString(), false);
				
				if (config.Restart.UseSound)
				{
					PlaySoundToAllPlayers(config.Restart.AlertSound);
				}

				timer.Once(8f, () =>
				{
					if (!isCancelled)
					{
						DestroyAllUI();
						isCustomUICreated = false;
					}
				});

				List<int> warningIntervals = new List<int>();
				
				if (config.Alerts.InGameWarningTimes != null && config.Alerts.InGameWarningTimes.Count > 0)
				{
					warningIntervals = config.Alerts.InGameWarningTimes
						.Select(minutes => minutes * 60)
						.Where(secs => secs < seconds)
						.OrderByDescending(secs => secs)
						.ToList();
				}
				else
				{
					int[] defaultIntervals = { 1800, 900, 600, 300 };
					warningIntervals = defaultIntervals.Where(secs => secs < seconds).ToList();
				}

				foreach (int interval in warningIntervals)
				{
					Timer warningTimer = timer.Once(seconds - interval, () =>
					{
						if (!isCancelled)
						{
							ShowMessage(interval.ToString(), false);
							if (config.Restart.UseSound)
							{
								PlaySoundToAllPlayers(config.Restart.AlertSound);
							}

							timer.Once(8f, () =>
							{
								if (!isCancelled)
								{
									DestroyAllUI();
									isCustomUICreated = false;
								}
							});
						}
					});
					activeTimers.Add(warningTimer);
				}

				Timer countdownMinuteTimer = timer.Once(seconds - 60, () =>
				{
					if (!isCancelled)
					{
						CountdownFromMinute();
					}
				});
				activeTimers.Add(countdownMinuteTimer);
			}
			else
			{
				timer.Once(0.1f, () => 
				{
					if (!isCancelled)
					{
						CountdownFromMinute();
					}
				});
			}

			if (config.Discord.DiscordNotifications)
			{
				NotifyDiscord(seconds);
			}

			LogInfo($"Custom restart scheduled for {seconds} seconds from now. Current time: {DateTime.UtcNow}");
		}

        private void CancelRestart()
        {
            if (isCancelled) return;

            isCancelled = true;
            ClearAllTimers();
            notifiedWarningTimes.Clear();

            foreach (var player in BasePlayer.activePlayerList)
            {
                string cancelMessage = GetLang("ChatRestartCanceled", player.UserIDString);

                player.ChatMessage(cancelMessage);

                if (config.UI.UseCustomUI)
                {
                    ShowCustomUI(player, cancelMessage, true);
                }

                if (config.UI.UseRustUI)
                {
                    string title = GetLang("RustUITitle", player.UserIDString);
                    player.SendConsoleCommand("gametip.showgametip",
                        $"<size=20>{title}</size>\n<size=14>{cancelMessage}</size>");
                }
            }

            if (!isCustomRestart && nextRestartTimer != null)
            {
                ReScheduleNextRestartForNextDay();
            }
            else
            {
                ResetRestartState();
            }

            timer.Once(5f, () => 
            {
                DestroyAllUI();
                isCustomUICreated = false;
            });
        }

        private bool CanScheduleRestart(DateTime nextRestart)
        {
	        var timeSinceLastRestart = (nextRestart - lastRestartTime).TotalSeconds;
	        if (timeSinceLastRestart < MINIMUM_RESTART_INTERVAL)
	        {
		        LogWarning($"Too soon to schedule next restart. Last restart was {timeSinceLastRestart:F0} seconds ago");
		        return false;
	        }

	        if (config.Restart.SkipWipeDays && config.Restart.WipeDays != null)
	        {
		        string dayName = nextRestart.DayOfWeek.ToString();
		        if (config.Restart.WipeDays.Contains(dayName, StringComparer.OrdinalIgnoreCase))
		        {
			        LogInfo($"Skipping restart on wipe day: {dayName}");
			        return false;
		        }
	        }

	        return true;
        }
        
        private void ShowCancelMessageAndDestroyUI(string cancelMessage)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                string localizedMessage = GetLang("ChatRestartCanceled", player.UserIDString);
        
                if (config.UI.UseCustomUI)
                {
                    ShowCustomUI(player, localizedMessage, true);
                }

                if (config.UI.UseRustUI)
                {
                    player.SendConsoleCommand("gametip.hidegametip");
                    player.SendConsoleCommand("gametip.showgametip",
                        $"<size=20>{GetLang("RustUITitle", player.UserIDString)}</size>\n<size=14>{localizedMessage}</size>");
                }
            }

            timer.Once(5f, () => 
            {
                DestroyAllUI();
                isCustomUICreated = false;
            });
        }

        private void BroadcastCancelMessage(string cancelMessage)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                string localizedMessage = GetLang("ChatRestartCanceled", player.UserIDString);
                player.ChatMessage(localizedMessage);
            }

            if (config.Discord.DiscordNotifications)
            {
                SendDiscordNotification(GetLang("ChatRestartCanceled", null));
            }
        }

        private void ResetRestartState()
        {
            isCustomRestart = false;
            customRestartTime = 0;
            lastChatMessage = string.Empty;
            lastUIMessage = string.Empty;
            isCancelled = false;
        }

        private void ReScheduleNextRestartForNextDay()
        {
            if (restartTimes.Count == 0)
            {
                LogWarning("No restart times are scheduled.");
                return;
            }

            try 
            {
                DateTime now = DateTime.UtcNow;
                DateTime nextRestart = FindNextRestartTime(now);
                TimeSpan timeUntilRestart = nextRestart - now;

                LogInfo($"Rescheduling restart:");
                LogInfo($"Current time (UTC): {now}");
                LogInfo($"Available restart times: {string.Join(", ", restartTimes.Select(t => t.ToString(@"hh\:mm")))}");
                LogInfo($"Next restart time (UTC): {nextRestart}");
                LogInfo($"Time until restart: {timeUntilRestart.TotalHours:F2} hours");

                ClearAllTimers();

                nextRestartTimer = timer.Once((float)timeUntilRestart.TotalSeconds, CheckPlayerCountAndRestart);
                activeTimers.Add(nextRestartTimer);

                bool hasWarningTimes = (config.Alerts.InGameWarningTimes != null && config.Alerts.InGameWarningTimes.Count > 0) ||
                                       (config.Alerts.DiscordWarningTimes != null && config.Alerts.DiscordWarningTimes.Count > 0);

                if (hasWarningTimes)
                {
                    ScheduleAlerts(timeUntilRestart);
                }

                isCustomRestart = false;
                isCancelled = false;
                customRestartTime = 0;
                notifiedWarningTimes.Clear();

                LogInfo($"Next restart successfully scheduled for {nextRestart} UTC");
            }
            catch (Exception ex)
            {
                LogError($"Error scheduling next restart: {ex}", ex);
                timer.Once(300f, ScheduleNextRestart);
            }
        }

        private int ParseTimeWithUnit(string input)
        {
            if (string.IsNullOrEmpty(input))
                return -1;

            input = input.ToLower().Trim();
            
            string numberPart = new string(input.TakeWhile(c => char.IsDigit(c)).ToArray());
            if (!int.TryParse(numberPart, out int value) || value <= 0)
                return -1;

            if (numberPart.Length == input.Length)
                return value;

            char unit = input[input.Length - 1];

            switch (unit)
            {
                case 'h':
                    if (value > 24)
                        return -1;
                    return value * 3600;
                    
                case 'm':
                    if (value > 1440)
                        return -1;
                    return value * 60;
                    
                case 's':
                    if (value > 86400)
                        return -1;
                    return value;
                    
                default:
                    if (value > 86400)
                        return -1;
                    return value;
            }
        }
        
		private void ScheduleNextRestart()
		{
			if (isCustomRestart || isCancelled)
			{
				LogInfo($"Skipping restart schedule: CustomRestart={isCustomRestart}, Cancelled={isCancelled}");
				return;
			}

			if ((DateTime.UtcNow - lastRestartCheck).TotalSeconds < MINIMUM_CHECK_INTERVAL)
			{
				LogInfo("Skipping restart check - too soon since last check");
				return;
			}
			lastRestartCheck = DateTime.UtcNow;

			ClearAllTimers();

			if (restartTimes.Count == 0)
			{
				LogWarning("No restart times configured");
				return;
			}

			try
			{
				DateTime now = DateTime.UtcNow;
				DateTime nextRestart = FindNextRestartTime(now);
				
				if ((nextRestart - lastRestartTime).TotalSeconds < MINIMUM_RESTART_INTERVAL)
				{
					LogWarning($"Preventing duplicate restart: Last restart at {lastRestartTime}, next scheduled for {nextRestart}");
					nextRestart = nextRestart.AddDays(1);
				}

				TimeSpan timeUntilRestart = nextRestart - now;

				if (timeUntilRestart.TotalSeconds <= 0)
				{
					LogWarning("Invalid restart time (in past), scheduling for next day");
					nextRestart = nextRestart.AddDays(1);
					timeUntilRestart = nextRestart - now;
				}

				int totalSeconds = (int)timeUntilRestart.TotalSeconds;
				
				LogInfo($"Scheduling next restart:");
				LogInfo($"Current time (UTC): {now}");
				LogInfo($"Next restart (UTC): {nextRestart}");
				LogInfo($"Time until restart: {timeUntilRestart.TotalHours:F2} hours");
				LogInfo($"Total seconds until restart: {totalSeconds}");

				if (timeUntilRestart.TotalSeconds > 0 && CanScheduleRestart(nextRestart))
				{
					isCustomRestart = false;
					customRestartTime = totalSeconds;
					isCancelled = false;

					Timer mainTimer = timer.Once(totalSeconds, CheckPlayerCountAndRestart);
					activeTimers.Add(mainTimer);

					if (totalSeconds > 60)
					{
						ShowMessage(totalSeconds.ToString(), false);

						List<int> warningIntervals = new List<int>();
						if (config.Alerts.InGameWarningTimes != null && config.Alerts.InGameWarningTimes.Count > 0)
						{
							warningIntervals = config.Alerts.InGameWarningTimes
								.Select(minutes => minutes * 60)
								.Where(secs => secs < totalSeconds && secs > 60)
								.OrderByDescending(secs => secs)
								.ToList();
						}

						foreach (int warningSeconds in warningIntervals)
						{
							Timer warningTimer = timer.Once(totalSeconds - warningSeconds, () =>
							{
								if (!isCancelled)
								{
									LogInfo($"Warning timer triggered for {warningSeconds} seconds");
									ShowMessage(warningSeconds.ToString(), false);

									timer.Once(8f, () =>
									{
										if (!isCancelled)
										{
											DestroyAllUI();
											isCustomUICreated = false;
										}
									});
								}
							});
							activeTimers.Add(warningTimer);
						}

						Timer lastMinuteTimer = timer.Once(totalSeconds - 60, () =>
						{
							if (!isCancelled)
							{
								LogInfo("Starting last minute countdown for scheduled restart");
								customRestartTime = 60;
								CountdownFromMinute();
							}
						});
						activeTimers.Add(lastMinuteTimer);
					}
					else
					{
						timer.Once(0.1f, () =>
						{
							if (!isCancelled)
							{
								LogInfo("Starting immediate countdown");
								CountdownFromMinute();
							}
						});
					}

					if (config.Discord.DiscordNotifications)
					{
						NotifyDiscord(totalSeconds);
					}

					LogInfo($"Scheduled restart timers set up successfully. Total timers: {activeTimers.Count}");
				}
				else
				{
					LogWarning("Invalid restart time calculated, skipping schedule");
					timer.Once(300f, ScheduleNextRestart);
				}
			}
			catch (Exception ex)
			{
				LogError($"Error scheduling next restart: {ex}", ex);
				timer.Once(300f, ScheduleNextRestart);
			}
		}
        
        private void LogRestartInfo(DateTime now, DateTime nextRestart)
        {
            Puts($"Current time: {now} UTC");
            Puts($"Available restart times: {string.Join(", ", restartTimes.Select(t => t.ToString(@"hh\:mm")))}");
            Puts($"Next restart scheduled for {nextRestart} UTC");
        }

		private DateTime FindNextRestartTime(DateTime fromTime)
		{
			if (restartTimes.Count == 0)
			{
				throw new InvalidOperationException("No restart times are scheduled.");
			}

			var possibleTimes = new List<DateTime>();
			DateTime today = fromTime.Date;
			DateTime tomorrow = today.AddDays(1);

			foreach (var time in restartTimes)
			{
				var todayTime = today.Add(time);
				if (todayTime > fromTime.AddMinutes(5))
				{
					possibleTimes.Add(todayTime);
				}
			}

			foreach (var time in restartTimes)
			{
				possibleTimes.Add(tomorrow.Add(time));
			}

			if (!possibleTimes.Any())
			{
				return tomorrow.Add(restartTimes.First());
			}

			return possibleTimes.OrderBy(t => t).First();
		}

        private void ScheduleAlerts(TimeSpan timeUntilRestart)
        {
            foreach (int warningTime in config.Alerts.InGameWarningTimes.Where(t => t > 1))
            {
                if (timeUntilRestart.TotalMinutes >= warningTime)
                {
                    ScheduleInGameWarningAlert(warningTime * 60);
                }
            }

            foreach (int warningTime in config.Alerts.DiscordWarningTimes.Where(t => t > 1))
            {
                if (timeUntilRestart.TotalMinutes >= warningTime)
                {
                    ScheduleDiscordWarningAlert(warningTime * 60);
                }
            }

            Timer countdownTimer = timer.Once((float)(timeUntilRestart.TotalMinutes - 1) * 60, CountdownFromMinute);
            activeTimers.Add(countdownTimer);
        }
        
        private void ScheduleInGameWarningAlert(int warningTime)
        {
            if (isCancelled) return;

            Timer alertTimer = timer.Once((float)(customRestartTime - warningTime), () =>
            {
                string message = GetTimeFormattedMessage(warningTime);
                ShowMessage(message, false);

                if (warningTime > 60)
                {
                    timer.Once(8f, () =>
                    {
                        if (!isCancelled)
                        {
                            DestroyAllUI();
                            isCustomUICreated = false;
                        }
                    });
                }
            });
            activeTimers.Add(alertTimer);
        }

        private void ScheduleDiscordWarningAlert(int warningTime)
        {
            if (isCancelled) return;

            Timer alertTimer = timer.Once((float)(customRestartTime - warningTime), () =>
            {
                NotifyDiscord(warningTime);
            });
            activeTimers.Add(alertTimer);
        }

		private void ScheduleWarningAlert(int warningTime)
		{
			if (isCancelled) return;

			int warningSeconds = warningTime * 60;

			Timer alertTimer = timer.Once((float)(customRestartTime - warningSeconds), () =>
			{
				string message = GetTimeFormattedMessage(warningSeconds);
				ShowMessage(message, false);
				NotifyDiscord(warningSeconds);

				if (warningSeconds > 60)
				{
					timer.Once(8f, () =>
					{
						if (!isCancelled)
						{
							DestroyAllUI();
							isCustomUICreated = false;
						}
					});
				}
			});
			activeTimers.Add(alertTimer);
		}

		private void CountdownFromMinute()
		{
			if (isCancelled)
			{
				LogInfo("CountdownFromMinute: Cancelled, returning early");
				return;
			}

			LogInfo($"CountdownFromMinute: Starting with customRestartTime={customRestartTime}");

			DestroyAllUI();
			ClearAllTimers();
			
			int[] intervals;
			if (customRestartTime <= 60)
			{
				intervals = new[] { 60, 50, 40, 30, 20, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 }
					.Where(x => x <= customRestartTime)
					.ToArray();
				LogInfo($"CountdownFromMinute: Using shortened intervals for time <= 60: {string.Join(", ", intervals)}");
			}
			else
			{
				intervals = new[] { 60, 50, 40, 30, 20, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
				LogInfo($"CountdownFromMinute: Using full intervals: {string.Join(", ", intervals)}");
			}

			LogInfo($"CountdownFromMinute: Showing initial message for {intervals[0]} seconds");
			ShowMessage(intervals[0].ToString(), false);
			
			foreach (int secondsLeft in intervals)
			{
				LogInfo($"CountdownFromMinute: Scheduling alert for {secondsLeft} seconds");
				ScheduleCountdownAlert(secondsLeft);
			}

			float finalMessageDelay = Math.Min(59.5f, customRestartTime - 0.5f);
			LogInfo($"CountdownFromMinute: Scheduling final message with delay {finalMessageDelay}");
			
			Timer finalMessageTimer = timer.Once(finalMessageDelay, () =>
			{
				if (!isCancelled)
				{
					LogInfo("CountdownFromMinute: Final message timer triggered");
					if (config.Discord.DiscordNotifications)
					{
						NotifyDiscordRestarting();
					}
				}
			});
			activeTimers.Add(finalMessageTimer);

			LogInfo($"CountdownFromMinute: Scheduling final restart with delay {RESTART_DELAY}");
			Timer restartTimer = timer.Once(RESTART_DELAY, () =>
			{
				if (!isCancelled)
				{
					LogInfo("CountdownFromMinute: Final restart timer triggered");
					DestroyAllUI();
					ShowMessage(GetLang("ChatRestartNow"), false);
					RestartServer();
				}
			});
			activeTimers.Add(restartTimer);

			LogInfo("CountdownFromMinute: All timers scheduled successfully");
		}

		private void ScheduleCountdownAlert(int secondsLeft)
		{
			float delay = customRestartTime <= 60 ? 
				customRestartTime - secondsLeft : 
				60 - secondsLeft;

			Timer intervalTimer = timer.Once(delay, () =>
			{
				if (isCancelled) return;

				ShowMessage(secondsLeft.ToString(), true);

				if (config.Discord.DiscordNotifications)
				{
					if (config.Discord.UseFullMinuteCountdown || 
						secondsLeft <= 10 || 
						config.Alerts.DiscordWarningTimes.Contains(secondsLeft))
					{
						NotifyDiscord(secondsLeft);
					}
				}

				if (config.Restart.UseSound)
				{
					PlaySoundToAllPlayers(config.Restart.AlertSound);
				}
			});
			activeTimers.Add(intervalTimer);
		}

        private void ScheduleFinalMessage()
        {
            Timer restartNowDelay = timer.Once(4f, () =>
            {
                SafeUpdateMessage(GetLang("ChatRestartNow"));
            });
            activeTimers.Add(restartNowDelay);
        }

        private void ScheduleFinalRestart()
        {
            Timer restartTimer = timer.Once(RESTART_DELAY, () =>
            {
                if (isCancelled) return;

                DestroyAllUI();
                ShowMessage(GetLang("ChatRestartNow"), false);
                RestartServer();
            });
            activeTimers.Add(restartTimer);
        }

        private void SafeUpdateMessage(string timeInSeconds)
        {
            if (!int.TryParse(timeInSeconds, out int seconds))
            {
                LogError($"Invalid time format received: {timeInSeconds}");
                return;
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                string timeMessage = GetTimeFormattedMessage(seconds, player.UserIDString);

                if (config.UI.UseCustomUI)
                {
                    string formattedMessage = string.Format(GetLang("CustomUIMessage", player.UserIDString), timeMessage);
                    ShowCustomUI(player, formattedMessage, false);
                }

                if (config.UI.UseRustUI)
                {
                    string title = GetLang("RustUITitle", player.UserIDString);
                    string uiMessage = string.Format(GetLang("RustUIMessage", player.UserIDString), timeMessage);
                    player.SendConsoleCommand("gametip.showgametip",
                        $"<size=20>{title}</size>\n<size=14>{uiMessage}</size>");
                }

                if (config.Restart.UseChatAlerts)
                {
                    string chatMessage = string.Format(GetLang("ChatRestartAlert", player.UserIDString), timeMessage);
                    UpdateChatMessage(chatMessage);
                }
            }
        }

        private void UpdateUIForAllPlayers(string title, string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (config.UI.UseCustomUI)
                {
                    ShowCustomUI(player, message, false);
                }

                if (config.UI.UseRustUI)
                {
                    player.SendConsoleCommand("gametip.showgametip",
                        $"<size=20>{title}</size>\n<size=14>{message}</size>");
                }
            }
        }

        private void UpdateChatMessage(string message)
        {
            Server.Broadcast(message);
            lastChatMessage = message;
        }

		private void ShowMessage(string message, bool isUpdate)
		{
			LogInfo($"ShowMessage called with message: {message}, isUpdate: {isUpdate}");
			
			if (!isUpdate || message == GetLang("ChatRestartNow"))
			{
				DestroyAllUI();
			}

			string timeMessage;
			if (int.TryParse(message, out int seconds))
			{
				timeMessage = GetTimeFormattedMessage(seconds);
			}
			else
			{
				timeMessage = message;
			}

			if (config.Restart.UseChatAlerts && message != GetLang("ChatRestartNow"))
			{
				if (!isUpdate || seconds <= 60)
				{
					Server.Broadcast(string.Format(GetLang("ChatRestartAlert", null), timeMessage));
				}
			}

			if (config.Restart.UseSound)
			{
				PlaySoundToAllPlayers(config.Restart.AlertSound);
			}

			foreach (var player in BasePlayer.activePlayerList)
			{
				if (config.UI.UseRustUI)
				{
					string title = GetLang("RustUITitle", player.UserIDString);
					string uiMessage = string.Format(GetLang("RustUIMessage", player.UserIDString), timeMessage);
					
					if (!isUpdate)
					{
						player.SendConsoleCommand("gametip.hidegametip");
					}
					player.SendConsoleCommand("gametip.showgametip", 
						$"<size=20>{title}</size>\n<size=14>{uiMessage}</size>");
				}

				if (config.UI.UseCustomUI)
				{
					string uiMessage = string.Format(GetLang("CustomUIMessage", player.UserIDString), timeMessage);
					ShowCustomUI(player, uiMessage, false);
					isCustomUICreated = true;
				}
			}

			lastUIMessage = message;

			if (!isUpdate && !message.Contains(GetLang("ChatRestartNow")))
			{
				if (!int.TryParse(message, out int secs) || secs > 60)
				{
					timer.Once(8f, () =>
					{
						if (!isCancelled)
						{
							LogInfo("Cleaning up UI after 8 seconds");
							DestroyAllUI();
							isCustomUICreated = false;
						}
					});
				}
			}
		}

        private void UpdateUIMessage(string title, string secondParagraph)
        {
            if (config.UI.UseRustUI || config.UI.UseCustomUI)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    SendFormattedAlert(player, title, secondParagraph);
                }
            }
        }

        private void UpdateChatAndSound(string message)
        {
            if (config.Restart.UseChatAlerts)
            {
                SendChatMessage(message);
            }

            if (config.Restart.UseSound)
            {
                PlaySoundToAllPlayers(config.Restart.AlertSound);
            }
        }

        private void SendFormattedAlert(BasePlayer player, string title, string secondParagraph)
        {
            if (player == null) return;

            if (config.UI.UseCustomUI)
            {
                bool isCancelMessage = secondParagraph.Contains("canceled") || secondParagraph.Contains("cancelled");
                ShowCustomUI(player, secondParagraph, isCancelMessage);
            }

            if (config.UI.UseRustUI)
            {
                string formattedMessage = $"<size=20>{title}</size>\n<size=14>{secondParagraph}</size>";
                player.SendConsoleCommand("gametip.showgametip", formattedMessage);
                
                timer.Once(8f, () => { player.SendConsoleCommand("gametip.hidegametip"); });
            }
        }

        private void SendChatMessage(string message)
        {
            Server.Broadcast(FormatChatMessage(message));
            lastChatMessage = message;
        }

        private string FormatChatMessage(string message)
        {
            return string.Format(GetLang("ChatRestartAlert"), message);
        }

        private void DestroyAllUI()
        {
            string[] uiIds = new string[]
            {
                "RESTARTBACKGROUND",
                "RewardsMainPanel",
                "RewardsContent",
                "TitlePanel",
                "PluginNameText",
                "ClosePANEL",
                "CloseBTN",
                "CloseIMG",
                "CRESTART_CONFIG_UI",
                "MainSettingsBKCRestart",
                "MainCRestartPanel",
                "CheckBXPanel",
                "LeftInputFieldsPanel",
                "InputFieldsPanel",
                "ScrollContainer",
                "ScrollContainer.View",
                "ScrollContainer.View.Content"
            };

            foreach (var player in BasePlayer.activePlayerList)
            {
                foreach (var uiId in uiIds)
                {
                    CuiHelper.DestroyUi(player, uiId);
                }
                player.SendConsoleCommand("gametip.hidegametip");
            }

            isCustomUICreated = false;
        }

        private void DestroyCustomUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "RESTARTBACKGROUND");
            }

            isCustomUICreated = false;
        }

        private void ClearAllTimers()
        {
            foreach (var timer in activeTimers)
            {
                timer?.Destroy();
            }

            activeTimers.Clear();
            if (nextRestartTimer != null)
            {
                nextRestartTimer.Destroy();
                nextRestartTimer = null;
            }
        }

        private void CheckPlayerCountAndRestart()
        {
            if (isCancelled)
            {
                LogInfo("Restart cancelled, skipping player count check");
                return;
            }

            int playerCount = BasePlayer.activePlayerList.Count;
            LogInfo($"Checking player count before restart. Current players: {playerCount}");

            if (ShouldCancelRestartDueToPlayerCount())
            {
                LogWarning($"Restart cancelled due to player count ({playerCount} > {config.PlayerRestrictions.MaxPlayersBeforeCancel})");
                CancelRestartDueToPlayerCount();
                return;
            }

            LogInfo("Proceeding with server restart");
            RestartServer();
        }

        private bool ShouldCancelRestartDueToPlayerCount()
        {
            return config.PlayerRestrictions.RestrictPlayerCount &&
                   BasePlayer.activePlayerList.Count >= config.PlayerRestrictions.MaxPlayersBeforeCancel;
        }

        private void CancelRestartDueToPlayerCount()
        {
            CancelRestart();
            ShowMessage(GetLang("RestartCanceledTooManyPlayers"), false);
        }

        private void RestartServer()
        {
            if (isShuttingDown)
            {
                LogWarning("Shutdown already in progress, ignoring duplicate request");
                return;
            }

            try 
            {
                isShuttingDown = true;
                LogInfo("Starting server restart process...");

                foreach (var player in BasePlayer.activePlayerList)
                {
                    string finalMessage = GetLang("ChatRestartNow", player.UserIDString);
                    player.ChatMessage(finalMessage);
                }

                DestroyAllUI();
                isCustomUICreated = false;
                LogInfo("UIs destroyed successfully");

                if (!hasSentFinalRestartNotification)
                {
                    NotifyDiscordRestarting();
                    hasSentFinalRestartNotification = true;
                }

                lastRestartTime = DateTime.UtcNow;

                timer.Once(3f, () =>
                {
                    LogInfo("Executing restart command...");
                    ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.save");
                    
                    timer.Once(1f, () => {
                        foreach (var player in BasePlayer.activePlayerList)
                        {
                            player.Kick(GetLang("ChatRestartNow", player.UserIDString));
                        }

                        timer.Once(0.5f, () => {
                            ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit");
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                LogError("Critical error during server restart", ex);
                isShuttingDown = false;
            }
        }

		void OnServerShutdown()
		{
			if (!isShuttingDown)
			{
				LogInfo("Server shutdown detected, cleaning up...");
			}
			ClearAllTimers();
			DestroyAllUI();
		}

		private bool IsValidTimeFormat(string timeString)
		{
			if (TimeSpan.TryParse(timeString, out TimeSpan time))
			{
				return time.TotalHours >= 0 && time.TotalHours < 24 
					&& time.Minutes >= 0 && time.Minutes < 60
					&& time.Seconds == 0;
			}
			return false;
		}

		private string GetTimeFormattedMessage(int seconds, string userId = null)
		{
			if (seconds >= 3600)
			{
				int hours = seconds / 3600;
				int remainingMinutes = (seconds % 3600) / 60;
				
				if (remainingMinutes > 0)
				{
					return string.Format("{0} {1} {2} {3}",
						hours,
						hours == 1 ? GetLang("TimeHour", userId) : GetLang("TimeHours", userId),
						remainingMinutes,
						remainingMinutes == 1 ? GetLang("TimeMinute", userId) : GetLang("TimeMinutes", userId));
				}
				
				return string.Format("{0} {1}",
					hours,
					hours == 1 ? GetLang("TimeHour", userId) : GetLang("TimeHours", userId));
			}
			else if (seconds >= 60)
			{
				int minutes = seconds / 60;
				return string.Format("{0} {1}",
					minutes,
					minutes == 1 ? GetLang("TimeMinute", userId) : GetLang("TimeMinutes", userId));
			}
			else
			{
				return string.Format("{0} {1}",
					seconds,
					seconds == 1 ? GetLang("TimeSecond", userId) : GetLang("TimeSeconds", userId));
			}
		}

        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, PERMISSION_USE);
        }

        private void PlaySoundToAllPlayers(string fx)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                Effect effect = new Effect(fx, player, 0, Vector3.zero, Vector3.forward);
                EffectNetwork.Send(effect, player.net.connection);
            }
        }
        
        #endregion
        
        #region Logging Methods

        private void LogToFile(string message, LogType type = LogType.Info)
        {
            string fileName = $"{Name}_{DateTime.UtcNow:yyyy-MM}";
            string logMessage = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{type}] {message}";
    
            LogToFile(fileName, logMessage, this);
            
            if (type == LogType.Error)
                PrintError(message);
            else if (type == LogType.Warning)
                PrintWarning(message);
        }

        private enum LogType
        {
            Info,
            Warning,
            Error,
            Debug
        }

        private void LogDebug(string message)
        {
        #if DEBUG
            LogToFile(message, LogType.Debug);
        #endif
        }

        private void LogError(string message, Exception ex = null)
        {
            string errorMessage = message;
            if (ex != null)
            {
                errorMessage += $"\nException: {ex.Message}";
                errorMessage += $"\nStackTrace: {ex.StackTrace}";
            }
            LogToFile(errorMessage, LogType.Error);
        }

        private void LogWarning(string message)
        {
            LogToFile(message, LogType.Warning);
        }

        private void LogInfo(string message)
        {
            LogToFile(message, LogType.Info);
        }

        #endregion
        
        #region UI Interface
        
        private void ShowCRestartUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MainSettingsBKCRestart");
            
            var container = new CuiElementContainer();
            MainSettingsBKCRestart(player, container);
            CuiHelper.AddUi(player, container);
        }

        private void MainSettingsBKCRestart(BasePlayer player, CuiElementContainer container)
        {
            container.Add(new CuiElement
            {
                Name = "MainSettingsBKCRestart",
                Parent = "Overlay",
                Components =
                {
                    new CuiNeedsCursorComponent(),
                    new CuiNeedsKeyboardComponent(),
                    new CuiImageComponent { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur.mat" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "MainCRestartPanel",
                Parent = "MainSettingsBKCRestart",
                Components =
                {
                    new CuiImageComponent
                        { Color = "0.1 0.1 0.1 0.95", Material = "assets/content/ui/uibackgroundblur.mat" },
                    new CuiRectTransformComponent
                        { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300 -200", OffsetMax = "300 200" }
                }
            });
            
            container.Add(new CuiElement
            {
                Name = "CheckBXPanel",
                Parent = "MainCRestartPanel",
                Components =
                {
                    new CuiImageComponent { Color = "0.15 0.15 0.15 0" },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.30",
                        AnchorMax = "0.5 0.95",
                        OffsetMin = "10 10",
                        OffsetMax = "-10 -10"
                    }
                }
            });
            
            container.Add(new CuiElement
            {
                Name = "LeftInputFieldsPanel",
                Parent = "MainCRestartPanel",
                Components =
                {
                    new CuiImageComponent { Color = "0.15 0.15 0.15 0" },
                    new CuiRectTransformComponent
                        { AnchorMin = "0 0", AnchorMax = "0.5 0.20", OffsetMin = "10 10", OffsetMax = "-10 -10" }
                }
            });
            
            container.Add(new CuiElement
            {
                Name = "InputFieldsPanel",
                Parent = "MainCRestartPanel",
                Components =
                {
                    new CuiImageComponent { Color = "0.15 0.15 0.15 0" },
                    new CuiRectTransformComponent
                        { AnchorMin = "0.5 0", AnchorMax = "1 0.95", OffsetMin = "10 10", OffsetMax = "-10 -10" }
                }
            });
            
            AddCheckboxes(container);
            AddInputFields(container);
            AddTitleAndVersion(container);
            AddCloseButton(container);
        }

        private void AddTitleAndVersion(CuiElementContainer container)
        {
            container.Add(new CuiElement
            {
                Name = "TitlePanel",
                Parent = "MainCRestartPanel",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.3396226 0.3396226 0.3396226 0.95",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    new CuiRectTransformComponent
                        { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 10", OffsetMax = "0 60" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "PluginNameText",
                Parent = "TitlePanel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "CRestart UI Configurator", Font = "robotocondensed-regular.ttf", FontSize = 16,
                        Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "RestartIMG",
                Parent = "TitlePanel",
                Components =
                {
                    new CuiImageComponent { Color = "1 1 1 1", Sprite = "assets/icons/refresh.png" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-284.341 -16.062",
                        OffsetMax = "-247.653 16.969"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "VersionText",
                Parent = "TitlePanel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"v.{CurrentVersion}", Font = "robotocondensed-regular.ttf", FontSize = 12,
                        Align = TextAnchor.MiddleRight, Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                        { AnchorMin = "0.8 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-10 0" }
                }
            });
        }

        private void AddCloseButton(CuiElementContainer container)
        {
            container.Add(new CuiElement
            {
                Name = "ClosePANEL",
                Parent = "MainSettingsBKCRestart",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.4156863 0.1101371 0.1101371 0.9", Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-47.743 -42.205", OffsetMax = "-7.057 -6.395"
                    }
                }
            });

            container.Add(new CuiButton
            {
                Button =
                {
                    Close = "MainSettingsBKCRestart", Color = "0 0 0 0"
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "" }
            }, "ClosePANEL", "CloseBTN");

            container.Add(new CuiElement
            {
                Name = "CloseIMG",
                Parent = "CloseBTN",
                Components =
                {
                    new CuiImageComponent
                        { Color = "0.7764706 0.5137255 0.4196078 0.85", Sprite = "assets/icons/close.png" },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-36.575 -30.802", OffsetMax = "-4.111 -5.008"
                    }
                }
            });
        }

        private void ShowRewardsUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "RewardsMainPanel");
            
            var container = new CuiElementContainer();
            
            container.Add(new CuiElement
            {
                Name = "RewardsMainPanel",
                Parent = "Overlay",
                Components =
                {
                    new CuiNeedsCursorComponent(),
                    new CuiNeedsKeyboardComponent(),
                    new CuiImageComponent { 
                        Color = "0 0 0 0.9", 
                        Material = "assets/content/ui/uibackgroundblur.mat" 
                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0 0", 
                        AnchorMax = "1 1" 
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "RewardsContent",
                Parent = "RewardsMainPanel",
                Components =
                {
                    new CuiImageComponent { 
                        Color = "0.1 0.1 0.1 0.95", 
                        Material = "assets/content/ui/uibackgroundblur.mat" 
                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.5 0.5", 
                        AnchorMax = "0.5 0.5", 
                        OffsetMin = "-300 -200", 
                        OffsetMax = "300 200" 
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "TitlePanel",
                Parent = "RewardsContent",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.3396226 0.3396226 0.3396226 0.95",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    new CuiRectTransformComponent
                    { 
                        AnchorMin = "0 1", 
                        AnchorMax = "1 1", 
                        OffsetMin = "0 10", 
                        OffsetMax = "0 60" 
                    }
                }
            });
            
            container.Add(new CuiElement
            {
                Name = "RewardsIcon",
                Parent = "TitlePanel",
                Components =
                {
                    new CuiImageComponent
                    {
                        Sprite = "assets/icons/gem.png",
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 1",
                        OffsetMin = "10 5",
                        OffsetMax = "50 -5"
                    }
                }
            });
            
            container.Add(new CuiElement
            {
                Name = "VersionLabel",
                Parent = "TitlePanel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"v{CurrentVersion}",
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 0.8",
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 1",
                        OffsetMin = "-110 5",
                        OffsetMax = "-10 -5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "PluginNameText",
                Parent = "TitlePanel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "CRestart Rewards Configurator",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 16,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0 0", 
                        AnchorMax = "1 1" 
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "ClosePANEL",
                Parent = "RewardsMainPanel",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.4156863 0.1101371 0.1101371 0.9",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = "-47.743 -42.205",
                        OffsetMax = "-7.057 -6.395"
                    }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Close = "RewardsMainPanel", Color = "0 0 0 0" },
                RectTransform = { 
                    AnchorMin = "0 0", 
                    AnchorMax = "1 1", 
                    OffsetMin = "0 0", 
                    OffsetMax = "0 0" 
                },
                Text = { Text = "" }
            }, "ClosePANEL", "CloseBTN");

            container.Add(new CuiElement
            {
                Name = "CloseIMG",
                Parent = "CloseBTN",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.7764706 0.5137255 0.4196078 0.85",
                        Sprite = "assets/icons/close.png"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = "-36.575 -30.802",
                        OffsetMax = "-4.111 -5.008"
                    }
                }
            });

            AddRewardTypeToggles(container);
            AddItemSelectionPanel(container);
            AddRewardSettingsInputs(container);
            
            CuiHelper.AddUi(player, container);

        }

        private void AddRewardTypeToggles(CuiElementContainer container)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 0" },
                RectTransform = { 
                    AnchorMin = "0.05 0.5",
                    AnchorMax = "0.4 0.9"
                }
            }, "RewardsContent", "ToggleContainer");

            string[][] toggles = new[]
            {
                new[] { "EnableRewards", "Enable Rewards" },
                new[] { "UseEconomics", "Use Economics" },
                new[] { "UseServerRewards", "Use Server Rewards" }
            };

            float yOffset = 20;
            float height = 30f;
            float spacing = 5f;

            foreach (var toggle in toggles)
            {
                AddToggleButton(container, toggle[0], toggle[1],
                GetRewardConfigValue(toggle[0]), GetRewardConfigValue(toggle[0]), yOffset, "ToggleContainer");
                yOffset -= (height + spacing);
            }
        }

        private void AddToggleButton(CuiElementContainer container, string setting, string label, bool state, bool isChecked, float yOffset, string parent)
        {
            string toggleName = $"Toggle_{setting}";
            
            container.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 0.95" },
                RectTransform = {
                    AnchorMin = "0 1",
                    AnchorMax = "1 1",
                    OffsetMin = $"5 {yOffset - 30}",
                    OffsetMax = $"-5 {yOffset}"
                }
            }, parent, toggleName);

            container.Add(new CuiPanel
            {
                Image = { 
                    Color = state ? "0.3647059 0.4470588 0.2235294 0.85" : "0.4156863 0.1101371 0.1101371 0.85" 
                },
                RectTransform = {
                    AnchorMin = "0 0.5",
                    AnchorMax = "0 0.5",
                    OffsetMin = "10 -10",
                    OffsetMax = "30 10"
                }
            }, toggleName, $"{toggleName}_box");

            container.Add(new CuiLabel
            {
                Text = {
		            Text = isChecked ? config.UI.CustomUI.CheckIcon : config.UI.CustomUI.CrossIcon,
		            FontSize = 14,
		            Align = TextAnchor.MiddleCenter,
		            Color = isChecked ? config.UI.CustomUI.CheckIconColor : config.UI.CustomUI.CrossIconColor
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, $"{toggleName}_box");

            container.Add(new CuiLabel
            {
                Text = {
                    Text = label,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform = {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                    OffsetMin = "40 0",
                    OffsetMax = "-5 0"
                }
            }, toggleName);

            container.Add(new CuiButton
            {
                Button = {
                    Color = "0 0 0 0",
                    Command = $"crestart.rewards.toggle {setting}"
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, toggleName);
        }

        private bool GetRewardConfigValue(string key)
        {
            switch (key)
            {
                case "EnableRewards": return config.Rewards.EnableRewards;
                case "UseEconomics": return config.Rewards.UseEconomics;
                case "UseServerRewards": return config.Rewards.UseServerRewards;
                default: return false;
            }
        }

        [ConsoleCommand("crestart.rewards.toggle")]
        private void CmdToggleRewardSetting(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string setting = arg.GetString(0);
            switch (setting)
            {
                case "EnableRewards":
                    config.Rewards.EnableRewards = !config.Rewards.EnableRewards;
                    break;
                case "UseEconomics":
                    config.Rewards.UseEconomics = !config.Rewards.UseEconomics;
                    break;
                case "UseServerRewards":
                    config.Rewards.UseServerRewards = !config.Rewards.UseServerRewards;
                    break;
            }

            SaveConfig();
            ShowRewardsUI(player);
        }

        private void AddItemSelectionPanel(CuiElementContainer container)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 0" },
                RectTransform = { 
                    AnchorMin = "0.45 0.02",
                    AnchorMax = "0.95 0.95"
                }
            }, "RewardsContent", "ItemSelectionParent");

            AddSearchBar(container);

            container.Add(new CuiButton
            {
                RectTransform = { 
                    AnchorMin = "0.8 0.93", 
                    AnchorMax = "1 1" 
                },
                Button = { 
                    Color = "0.15 0.15 0.15 0", 
                    Command = "crestart.rewards.customskin" 
                },
                Text = { 
                    Text = "Add Custom Item", 
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1",
                    Font = "robotocondensed-regular.ttf"
                }
            }, "ItemSelectionParent");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 1" },
                RectTransform = { 
                    AnchorMin = "0 0",  
                    AnchorMax = "1 0.93"
                }
            }, "ItemSelectionParent", "ItemMainPanel");

            container.Add(new CuiElement
            {
                Parent = "ItemMainPanel",
                Name = "ItemScrollView",
                Components =
                {
                    new CuiScrollViewComponent
                    {
                        ContentTransform = new CuiRectTransformComponent 
                        { 
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -21500",
                            OffsetMax = "0 0"
                        },
                        Vertical = true,
                        Horizontal = false,
                        VerticalScrollbar = new CuiScrollbar { Size = 8f }
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "2 0",
                        OffsetMax = "10 0"
                    }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                    OffsetMin = "0 -21500",
                    OffsetMax = "0 0"
                },
                Image = { Color = "0 0 0 0" }
            }, "ItemScrollView", "ItemScrollView.Content");

            int itemsPerRow = 4;
            float itemHeight = 80f;
            float itemWidth = 0.95f / itemsPerRow;
            float spacing = 0.01f;
            int itemCount = 0;

            var categories = new[]
            {
                "Custom Items",
                "Weapon", "Construction", "Items", "Resources", "Attire",
                "Tool", "Medical", "Food", "Ammunition", "Traps",
                "Misc", "Component", "Electrical", "Fun"
            };

            var customItems = config.Rewards.ItemRewards
                .Where(kv => kv.Key.Contains('_'))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var validItems = ItemManager.itemList
                .Where(item => item != null && 
                    !string.IsNullOrEmpty(item.shortname) && 
                    !string.IsNullOrEmpty(item.displayName?.english) &&
                    ItemMatchesSearch(item, GetSearchText()))
                .ToList();

            foreach (var category in categories)
            {
                List<(ItemDefinition itemDef, string shortname, ulong skinId, int amount)> categoryItems;
                
                if (category == "Custom Items")
                {
                    categoryItems = customItems
                        .Select(kv =>
                        {
                            var parts = kv.Key.Split('_');
                            var shortname = parts[0];
                            var skinId = parts.Length > 1 ? ulong.Parse(parts[1]) : 0UL;
                            var itemDef = ItemManager.FindItemDefinition(shortname);
                            return (itemDef, kv.Key, skinId, kv.Value);
                        })
                        .Where(x => x.itemDef != null)
                        .OrderBy(x => x.itemDef.displayName.english)
                        .ToList();
                }
                else
                {
                    categoryItems = validItems
                        .Where(item => GetItemCategory(item) == category)
                        .Select(item => (item, item.shortname, 0UL, config.Rewards.ItemRewards.GetValueOrDefault(item.shortname, 0)))
                        .OrderBy(x => x.Item1.displayName.english)
                        .ToList();
                }

                if (categoryItems.Count > 0)
                {
                    if (itemCount % itemsPerRow != 0)
                    {
                        itemCount += itemsPerRow - (itemCount % itemsPerRow);
                    }

                    int row = -itemCount / itemsPerRow;

                    container.Add(new CuiLabel
                    {
                        Text = { 
                            Text = category,
                            FontSize = 16,
                            Align = TextAnchor.MiddleLeft,
                            Color = category == "Custom Items" ? "0.922 0.788 0.388 1" : "0.969 0.922 0.882 1",
                            Font = "robotocondensed-bold.ttf"
                        },
                        RectTransform = { 
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"10 {row * itemHeight - 40}",
                            OffsetMax = $"0 {row * itemHeight}"
                        }
                    }, "ItemScrollView.Content", $"category_{category}");

                    itemCount += 2;

                    foreach (var (itemDef, shortname, skinId, amount) in categoryItems)
                    {
                        row = -itemCount / itemsPerRow;
                        int col = itemCount % itemsPerRow;

                        float xMin = (col * itemWidth) + (spacing / 2);
                        float xMax = ((col + 1) * itemWidth) - (spacing / 2);
                        float yPos = row * itemHeight;

                        string itemId = skinId > 0 ? $"item_{shortname}_{skinId}" : $"item_{shortname}";

                        container.Add(new CuiPanel
                        {
                            Image = { Color = amount > 0 ? "0.2 0.4 0.2 1" : "0.2 0.2 0.2 1" },
                            RectTransform = { 
                                AnchorMin = $"{xMin} 1",
                                AnchorMax = $"{xMax} 1",
                                OffsetMin = $"0 {yPos - itemHeight + 5}",
                                OffsetMax = $"0 {yPos - 5}"
                            }
                        }, "ItemScrollView.Content", itemId);

                        container.Add(new CuiElement
                        {
                            Parent = itemId,
                            Components =
                            {
                                new CuiImageComponent { 
                                    ItemId = itemDef.itemid,
                                    SkinId = skinId
                                },
                                new CuiRectTransformComponent { 
                                    AnchorMin = "0.1 0.25", 
                                    AnchorMax = "0.9 0.75" 
                                }
                            }
                        });

                        string displayName = itemDef.displayName.english;
                        if (skinId > 0)
                        {
                            displayName += $"\n(Skin: {skinId})";
                        }

                        container.Add(new CuiLabel
                        {
                            Text = { 
                                Text = displayName, 
                                FontSize = 8, 
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1",
                                Font = "robotocondensed-regular.ttf"
                            },
                            RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" }
                        }, itemId);

                        if (amount > 0)
                        {
                            container.Add(new CuiLabel
                            {
                                Text = { 
                                    Text = $"x{amount}", 
                                    FontSize = 10, 
                                    Align = TextAnchor.MiddleCenter,
                                    Color = "0.7 1 0.7 1",
                                    Font = "robotocondensed-bold.ttf"
                                },
                                RectTransform = { AnchorMin = "0 0.75", AnchorMax = "1 0.95" }
                            }, itemId);
                        }

                        container.Add(new CuiButton
                        {
                            Button = { 
                                Color = "0 0 0 0", 
                                Command = skinId > 0 ? 
                                    $"crestart.rewards.selectitem {shortname}_{skinId}" : 
                                    $"crestart.rewards.selectitem {shortname}"
                            },
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Text = { Text = "" }
                        }, itemId);

                        itemCount++;
                    }
                }
            }
        }

        private void AddSearchBar(CuiElementContainer container)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 1" },
                RectTransform = { 
                    AnchorMin = "0 0.93", 
                    AnchorMax = "1 1"
                }
            }, "ItemSelectionParent", "SearchPanel");

            container.Add(new CuiElement
            {
                Parent = "SearchPanel",
                Name = "SearchInput",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = GetSearchText(),
                        FontSize = 14,
                        Align = TextAnchor.MiddleLeft,
                        Command = "crestart.rewards.search",
                        Color = "1 1 1 1",
                        CharsLimit = 50,
                        IsPassword = false,
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "30 5",
                        OffsetMax = "-5 -5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "SearchPanel",
                Name = "SearchIcon",
                Components =
                {
                    new CuiImageComponent 
                    { 
                        Sprite = "assets/icons/examine.png",
                        Color = "1 1 1 0.7"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.5",
                        AnchorMax = "0 0.5",
                        OffsetMin = "5 -10",
                        OffsetMax = "25 10"
                    }
                }
            });
        }

        private void ShowCustomSkinDialog(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "CustomSkinDialog");
            
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.9" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true
            }, "Overlay", "CustomSkinDialog");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95" },
                RectTransform = { 
                    AnchorMin = "0.5 0.5", 
                    AnchorMax = "0.5 0.5", 
                    OffsetMin = "-200 -125", 
                    OffsetMax = "200 125" 
                }
            }, "CustomSkinDialog", "CustomSkinContent");

            container.Add(new CuiLabel
            {
                Text = { 
                    Text = "Add Custom Item", 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 20, 
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform = { AnchorMin = "0 0.8", AnchorMax = "1 0.95" }
            }, "CustomSkinContent");

            container.Add(new CuiElement
            {
                Parent = "CustomSkinContent",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = "Item Shortname",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Command = "crestart.rewards.customskin.shortname",
                        CharsLimit = 100
                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.1 0.6", 
                        AnchorMax = "0.9 0.7" 
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "CustomSkinContent",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = "Skin ID",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Command = "crestart.rewards.customskin.skinid",
                        CharsLimit = 50
                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.1 0.45", 
                        AnchorMax = "0.9 0.55" 
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "CustomSkinContent",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = "Amount",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Command = "crestart.rewards.customskin.amount",
                        CharsLimit = 50

                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.1 0.3", 
                        AnchorMax = "0.9 0.4" 
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { 
                    AnchorMin = "0.2 0.1", 
                    AnchorMax = "0.45 0.2" 
                },
                Button = { 
                    Color = "0.282353 0.407843 0.066667 1", 
                    Command = "crestart.rewards.customskin.add" 
                },
                Text = { 
                    Text = "Add Item", 
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = "0.5568627 0.7764706 0.1843137 1" 
                }
            }, "CustomSkinContent");

            container.Add(new CuiButton
            {
                RectTransform = { 
                    AnchorMin = "0.55 0.1", 
                    AnchorMax = "0.8 0.2" 
                },
                Button = { 
                    Color = "0.501961 0.117647 0.098039", 
                    Command = "crestart.rewards.customskin.cancel" 
                },
                Text = { 
                    Text = "Cancel", 
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = "0.7764706 0.5137255 0.4196078 1"
                }
            }, "CustomSkinContent");

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("crestart.rewards.customskin")]
        private void CmdShowCustomSkinDialog(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            ShowCustomSkinDialog(player);
        }

        [ConsoleCommand("crestart.rewards.customskin.shortname")]
        private void CmdCustomSkinShortname(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string shortname = arg.GetString(0);
            _inputFieldValues[$"{player.UserIDString}_customskin_shortname"] = shortname;
        }

        [ConsoleCommand("crestart.rewards.customskin.skinid")]
        private void CmdCustomSkinId(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string skinId = arg.GetString(0);
            _inputFieldValues[$"{player.UserIDString}_customskin_skinid"] = skinId;
        }

        [ConsoleCommand("crestart.rewards.customskin.amount")]
        private void CmdCustomSkinAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string amount = arg.GetString(0);
            _inputFieldValues[$"{player.UserIDString}_customskin_amount"] = amount;
        }

        [ConsoleCommand("crestart.rewards.customskin.add")]
        private void CmdAddCustomSkin(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string shortname = _inputFieldValues.GetValueOrDefault($"{player.UserIDString}_customskin_shortname");
            string skinIdStr = _inputFieldValues.GetValueOrDefault($"{player.UserIDString}_customskin_skinid");
            string amountStr = _inputFieldValues.GetValueOrDefault($"{player.UserIDString}_customskin_amount");

            if (string.IsNullOrEmpty(shortname) || string.IsNullOrEmpty(skinIdStr) || string.IsNullOrEmpty(amountStr))
            {
                player.ChatMessage("Please fill in all fields");
                return;
            }

            if (!ulong.TryParse(skinIdStr, out ulong skinId))
            {
                player.ChatMessage("Invalid Skin ID");
                return;
            }

            if (!int.TryParse(amountStr, out int amount) || amount <= 0)
            {
                player.ChatMessage("Invalid amount");
                return;
            }

            var itemDefinition = ItemManager.FindItemDefinition(shortname);
            if (itemDefinition == null)
            {
                player.ChatMessage("Invalid item shortname");
                return;
            }

            string customItemKey = $"{shortname}_{skinId}";
            config.Rewards.ItemRewards[customItemKey] = amount;
            SaveConfig();

            CuiHelper.DestroyUi(player, "CustomSkinDialog");
            ShowRewardsUI(player);
        }

        [ConsoleCommand("crestart.rewards.customskin.cancel")]
        private void CmdCancelCustomSkin(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            CuiHelper.DestroyUi(player, "CustomSkinDialog");
            ShowRewardsUI(player);
        }

        private string GetSearchText()
        {
            string searchText = _searchInputValues.GetValueOrDefault("current_search", string.Empty);
            return string.IsNullOrEmpty(searchText) ? "Search items..." : searchText;
        }

        private bool ItemMatchesSearch(ItemDefinition item, string searchText)
        {
            if (string.IsNullOrEmpty(searchText) || searchText == "Search items...") 
                return true;

            searchText = searchText.ToLower().Trim();
            
            return item.displayName.english.ToLower().Contains(searchText) || 
                item.shortname.ToLower().Contains(searchText) ||
                GetItemCategory(item).ToLower().Contains(searchText);
        }

        [ConsoleCommand("crestart.rewards.search")]
        private void CmdSearch(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string searchText = arg.GetString(0, "").Trim();
            
            if (searchText != "Search items...")
            {
                _searchInputValues["current_search"] = searchText;
            }

            ShowRewardsUI(player);
        }

        private string GetItemCategory(ItemDefinition item)
        {
            if (item.category == ItemCategory.Weapon) return "Weapon";
            if (item.category == ItemCategory.Construction) return "Construction";
            if (item.category == ItemCategory.Items) return "Items";
            if (item.category == ItemCategory.Resources) return "Resources";
            if (item.category == ItemCategory.Attire) return "Attire";
            if (item.category == ItemCategory.Tool) return "Tool";
            if (item.category == ItemCategory.Medical) return "Medical";
            if (item.category == ItemCategory.Food) return "Food";
            if (item.category == ItemCategory.Ammunition) return "Ammunition";
            if (item.category == ItemCategory.Traps) return "Traps";
            if (item.category == ItemCategory.Component) return "Component";
            if (item.category == ItemCategory.Electrical) return "Electrical";
            if (item.isUsable) return "Fun";
            return "Misc";
        }

       private void AddRewardSettingsInputs(CuiElementContainer container)
       {
           float startY = 0.4f;
           float height = 0.1f;
           float spacing = 0.02f;
           int index = 0;
           string anchorMinX = "0.05";
           string anchorMaxX = "0.4";
   
           if (config.Rewards.EnableRewards)

           {
               float yMax = startY - index * (height + spacing);
               float yMin = yMax - height;
               container.Add(new CuiPanel
               {
                   Image = { Color = "0.15 0.15 0.15 0.95" },
                   RectTransform = { AnchorMin = $"{anchorMinX} {yMin}", AnchorMax = $"{anchorMaxX} {yMax}" }
               }, "RewardsContent", "RewardTimePanel");
   
               container.Add(new CuiLabel
               {
                   Text = { Text = "Reward Time Limit (seconds):", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                   RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 1" }
               }, "RewardTimePanel");
   
               container.Add(new CuiElement
               {
                   Parent = "RewardTimePanel",
                   Components =
                   {
                       new CuiInputFieldComponent
                       {
                           Text = config.Rewards.RewardTimeLimit.ToString(),
                           Font = "robotocondensed-regular.ttf",
                           Command = "crestart.rewards.settimelimit",
                           FontSize = 14,
                           Align = TextAnchor.MiddleCenter,
                           CharsLimit = 6
                       },
                       new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.4" }
                   }
               });
               index++;
           }
   
           if (config.Rewards.UseEconomics)
           {
               float yMax = startY - index * (height + spacing);
               float yMin = yMax - height;
               container.Add(new CuiPanel
               {
                   Image = { Color = "0.15 0.15 0.15 0.95" },
                   RectTransform = { AnchorMin = $"{anchorMinX} {yMin}", AnchorMax = $"{anchorMaxX} {yMax}" }
               }, "RewardsContent", "EconomicsPanel");
   
               container.Add(new CuiLabel
               {
                   Text = { Text = "Economics Amount:", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                   RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 1" }
               }, "EconomicsPanel");
   
               container.Add(new CuiElement
               {
                   Parent = "EconomicsPanel",
                   Components =
                   {
                       new CuiInputFieldComponent
                       {
                           Text = config.Rewards.EconomicsAmount.ToString(),
                           Font = "robotocondensed-regular.ttf",
                           Command = "crestart.rewards.seteconomics",
                           FontSize = 14,
                           Align = TextAnchor.MiddleCenter
                       },
                       new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.4" }
                   }
              });
               index++;
           }
   
           if (config.Rewards.UseServerRewards)
           {
               float yMax = startY - index * (height + spacing);
               float yMin = yMax - height;
               container.Add(new CuiPanel
               {
                   Image = { Color = "0.15 0.15 0.15 0.95" },
                   RectTransform = { AnchorMin = $"{anchorMinX} {yMin}", AnchorMax = $"{anchorMaxX} {yMax}" }
               }, "RewardsContent", "ServerRewardsPanel");
   
               container.Add(new CuiLabel
               {
                   Text = { Text = "Server Rewards Points:", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                   RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 1" }
               }, "ServerRewardsPanel");
   
               container.Add(new CuiElement
               {
                   Parent = "ServerRewardsPanel",
                   Components =
                   {
                       new CuiInputFieldComponent
                       {
                           Text = config.Rewards.ServerRewardsPoints.ToString(),
                           Font = "robotocondensed-regular.ttf",
                           Command = "crestart.rewards.setserverrewards",
                           FontSize = 14,
                           Align = TextAnchor.MiddleCenter
                       },
                       new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.4" }
                   }
               });
               index++;
           }
       }

        private void ShowItemAmountInput(BasePlayer player, string itemKey)
        {
            CuiHelper.DestroyUi(player, "AmountInputDialog");
            
            string cleanItemKey = itemKey;
            if (itemKey.Contains('_'))
            {
                string[] keyParts = itemKey.Split('_');
                if (keyParts.Length >= 3)
                {
                    cleanItemKey = $"{keyParts[0]}_{keyParts[1]}";
                }
            }

            bool isCustomItem = cleanItemKey.Contains('_');
            string[] parts = cleanItemKey.Split('_');
            string shortname = parts[0];
            ulong skinId = parts.Length > 1 ? ulong.Parse(parts[1]) : 0UL;
            var itemDef = ItemManager.FindItemDefinition(shortname);
            if (itemDef == null) return;

            int currentAmount = config.Rewards.ItemRewards.GetValueOrDefault(cleanItemKey, 0);
            LogInfo($"Loading amount for item {cleanItemKey} (original key: {itemKey}): {currentAmount} (Config contains key: {config.Rewards.ItemRewards.ContainsKey(cleanItemKey)})");

            if (!isCustomItem)
            {
                var container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.9" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    CursorEnabled = true
                }, "Overlay", "AmountInputDialog");

                container.Add(new CuiPanel
                {
                    Image = { Color = "0.1 0.1 0.1 0.95" },
                    RectTransform = { 
                        AnchorMin = "0.5 0.5", 
                        AnchorMax = "0.5 0.5", 
                        OffsetMin = "-200 -125", 
                        OffsetMax = "200 125" 
                    }
                }, "AmountInputDialog", "AmountInputContent");

                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = "Set Item Amount", 
                        Font = "robotocondensed-bold.ttf", 
                        FontSize = 20, 
                        Align = TextAnchor.MiddleCenter, 
                        Color = "1 1 1 1" 
                    },
                    RectTransform = { AnchorMin = "0 0.8", AnchorMax = "1 0.95" }
                }, "AmountInputContent");

                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = itemDef.displayName.english, 
                        Font = "robotocondensed-bold.ttf", 
                        FontSize = 16, 
                        Align = TextAnchor.MiddleCenter, 
                        Color = "0.7 0.7 0.7 1" 
                    },
                    RectTransform = { AnchorMin = "0 0.65", AnchorMax = "1 0.75" }
                }, "AmountInputContent");

                container.Add(new CuiElement
                {
                    Parent = "AmountInputContent",
                    Components =
                    {
                        new CuiImageComponent { ItemId = itemDef.itemid },
                        new CuiRectTransformComponent { 
                            AnchorMin = "0.4 0.35", 
                            AnchorMax = "0.6 0.6" 
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "AmountInputContent",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = currentAmount.ToString(),
                            FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5,
                            Command = $"crestart.rewards.updateamount {itemKey}",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf"
                        },
                        new CuiRectTransformComponent { 
                            AnchorMin = "0.3 0.25", 
                            AnchorMax = "0.7 0.32" 
                        }
                    }
                });

                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = "Enter 0 to remove item from rewards", 
                        Font = "robotocondensed-regular.ttf", 
                        FontSize = 12, 
                        Align = TextAnchor.MiddleCenter, 
                        Color = "0.7 0.7 0.7 1" 
                    },
                    RectTransform = { AnchorMin = "0 0.15", AnchorMax = "1 0.2" }
                }, "AmountInputContent");

                container.Add(new CuiButton
                {
                    RectTransform = { 
                        AnchorMin = "0.2 0.05", 
                        AnchorMax = "0.45 0.12" 
                    },
                    Button = { 
                        Color = "0.282353 0.407843 0.066667 1", 
                        Command = $"crestart.rewards.setitemamount {itemKey}" 
                    },
                    Text = { 
                        Text = "Confirm", 
                        Font = "robotocondensed-bold.ttf", 
                        FontSize = 14, 
                        Align = TextAnchor.MiddleCenter, 
                        Color = "0.5568627 0.7764706 0.1843137 1" 
                    }
                }, "AmountInputContent");

                container.Add(new CuiButton
                {
                    RectTransform = { 
                        AnchorMin = "0.55 0.05", 
                        AnchorMax = "0.8 0.12" 
                    },
                    Button = { 
                        Color = "0.501961 0.117647 0.098039", 
                        Command = "crestart.rewards.closeamount" 
                    },
                    Text = { 
                        Text = "Cancel", 
                        Font = "robotocondensed-bold.ttf", 
                        FontSize = 14, 
                        Align = TextAnchor.MiddleCenter, 
                        Color = "0.7764706 0.5137255 0.4196078 1" 
                    }
                }, "AmountInputContent");

                CuiHelper.AddUi(player, container);
                return;
            }

            var customContainer = new CuiElementContainer();

            customContainer.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.9" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true
            }, "Overlay", "AmountInputDialog");

            customContainer.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95" },
                RectTransform = { 
                    AnchorMin = "0.5 0.5", 
                    AnchorMax = "0.5 0.5", 
                    OffsetMin = "-200 -150", 
                    OffsetMax = "200 150" 
                }
            }, "AmountInputDialog", "AmountInputContent");

            customContainer.Add(new CuiLabel
            {
                Text = { 
                    Text = "Edit Custom Item", 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 20, 
                    Align = TextAnchor.MiddleCenter, 
                    Color = "1 1 1 1" 
                },
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" }
            }, "AmountInputContent");

            customContainer.Add(new CuiLabel
            {
                Text = { 
                    Text = "Item Shortname", 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 12, 
                    Align = TextAnchor.MiddleCenter, 
                    Color = "1 1 1 1" 
                },
                RectTransform = { AnchorMin = "0.1 0.7", AnchorMax = "0.9 0.75" }
            }, "AmountInputContent");

            customContainer.Add(new CuiElement
            {
                Parent = "AmountInputContent",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = shortname,
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Command = $"crestart.rewards.edititem.shortname {itemKey}",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.1 0.63", 
                        AnchorMax = "0.9 0.68" 
                    }
                }
            });

            customContainer.Add(new CuiLabel
            {
                Text = { 
                    Text = "Skin ID", 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 12, 
                    Align = TextAnchor.MiddleCenter, 
                    Color = "1 1 1 1" 
                },
                RectTransform = { AnchorMin = "0.1 0.55", AnchorMax = "0.9 0.6" }
            }, "AmountInputContent");

            customContainer.Add(new CuiElement
            {
                Parent = "AmountInputContent",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = skinId.ToString(),
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Command = $"crestart.rewards.edititem.skinid {itemKey}",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.1 0.48", 
                        AnchorMax = "0.9 0.53" 
                    }
                }
            });

            customContainer.Add(new CuiLabel
            {
                Text = { 
                    Text = "Amount", 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 12, 
                    Align = TextAnchor.MiddleCenter, 
                    Color = "1 1 1 1" 
                },
                RectTransform = { 
                    AnchorMin = "0.1 0.4", 
                    AnchorMax = "0.9 0.45" 
                }
            }, "AmountInputContent");

            customContainer.Add(new CuiElement
            {
                Parent = "AmountInputContent",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = currentAmount.ToString(),
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Command = $"crestart.rewards.edititem.amount {itemKey}",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.1 0.33", 
                        AnchorMax = "0.9 0.38" 
                    }
                }
            });

            customContainer.Add(new CuiElement
            {
                Parent = "AmountInputContent",
                Components =
                {
                    new CuiImageComponent { 
                        ItemId = itemDef.itemid,
                        SkinId = skinId
                    },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.35 0.15", 
                        AnchorMax = "0.65 0.3" 
                    }
                }
            });

            customContainer.Add(new CuiButton
            {
                RectTransform = { 
                    AnchorMin = "0.2 0.05", 
                    AnchorMax = "0.45 0.12" 
                },
                Button = { 
                    Color = "0.282353 0.407843 0.066667 1", 
                    Command = $"crestart.rewards.edititem.save {itemKey}" 
                },
                Text = { 
                    Text = "Save", 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 14, 
                    Align = TextAnchor.MiddleCenter, 
                    Color = "0.5568627 0.7764706 0.1843137 1" 
                }
            }, "AmountInputContent");

            customContainer.Add(new CuiButton
            {
                RectTransform = { 
                    AnchorMin = "0.55 0.05", 
                    AnchorMax = "0.8 0.12" 
                },
                Button = { 
                    Color = "0.501961 0.117647 0.098039", 
                    Command = "crestart.rewards.closeamount" 
                },
                Text = { 
                    Text = "Cancel", 
                    Font = "robotocondensed-bold.ttf", 
                    FontSize = 14, 
                    Align = TextAnchor.MiddleCenter, 
                    Color = "0.7764706 0.5137255 0.4196078 1" 
                }
            }, "AmountInputContent");

            CuiHelper.AddUi(player, customContainer);
        }

        [ConsoleCommand("crestart.rewards.edititem.shortname")]
        private void CmdEditItemShortname(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string itemKey = arg.GetString(0);
            string newShortname = arg.GetString(1);
            
            string key = $"{player.UserIDString}_edit_shortname_{itemKey}";
            _inputFieldValues[key] = newShortname;
        }

        [ConsoleCommand("crestart.rewards.edititem.skinid")]
        private void CmdEditItemSkinId(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string itemKey = arg.GetString(0);
            string newSkinId = arg.GetString(1);
            
            string key = $"{player.UserIDString}_edit_skinid_{itemKey}";
            _inputFieldValues[key] = newSkinId;
        }

        [ConsoleCommand("crestart.rewards.edititem.amount")]
        private void CmdEditItemAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string itemKey = arg.GetString(0);
            string newAmount = arg.GetString(1);
            
            string key = $"{player.UserIDString}_edit_amount_{itemKey}";
            _inputFieldValues[key] = newAmount;
        }

        [ConsoleCommand("crestart.rewards.edititem.save")]
        private void CmdSaveEditedItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string oldItemKey = arg.GetString(0);
            string cleanOldItemKey = oldItemKey;
            if (oldItemKey.Contains('_'))
            {
                string[] keyParts = oldItemKey.Split('_');
                if (keyParts.Length >= 3)
                {
                    cleanOldItemKey = $"{keyParts[0]}_{keyParts[1]}";
                }
            }

            if (!config.Rewards.ItemRewards.TryGetValue(cleanOldItemKey, out int currentAmount))
            {
                LogError($"Item not found in rewards: {cleanOldItemKey}");
                player.ChatMessage("Error: Item not found in rewards");
                return;
            }

            string[] oldParts = cleanOldItemKey.Split('_');
            string currentShortname = oldParts[0];
            ulong currentSkinId = oldParts.Length > 1 ? ulong.Parse(oldParts[1]) : 0UL;

            bool shortnameChanged = _inputFieldValues.TryGetValue($"{player.UserIDString}_edit_shortname_{oldItemKey}", out string newShortname);
            bool skinIdChanged = _inputFieldValues.TryGetValue($"{player.UserIDString}_edit_skinid_{oldItemKey}", out string skinIdStr);
            bool amountChanged = _inputFieldValues.TryGetValue($"{player.UserIDString}_edit_amount_{oldItemKey}", out string amountStr);

            if (amountChanged && int.TryParse(amountStr, out int amount) && amount <= 0)
            {
                config.Rewards.ItemRewards.Remove(cleanOldItemKey);
                SaveConfig();
                
                _inputFieldValues.Remove($"{player.UserIDString}_edit_shortname_{oldItemKey}");
                _inputFieldValues.Remove($"{player.UserIDString}_edit_skinid_{oldItemKey}");
                _inputFieldValues.Remove($"{player.UserIDString}_edit_amount_{oldItemKey}");
                
                CuiHelper.DestroyUi(player, "AmountInputDialog");
                ShowRewardsUI(player);
                player.ChatMessage("Custom item removed from rewards");
                return;
            }

            string finalShortname = shortnameChanged ? newShortname : currentShortname;
            ulong finalSkinId = currentSkinId;
            int finalAmount = currentAmount;

            if (shortnameChanged)
            {
                var itemDefinition = ItemManager.FindItemDefinition(finalShortname);
                if (itemDefinition == null)
                {
                    player.ChatMessage("Invalid item shortname");
                    return;
                }
            }

            if (skinIdChanged)
            {
                if (!ulong.TryParse(skinIdStr, out finalSkinId))
                {
                    player.ChatMessage("Invalid Skin ID");
                    return;
                }
            }

            if (amountChanged)
            {
                if (!int.TryParse(amountStr, out finalAmount) || finalAmount <= 0)
                {
                    player.ChatMessage("Invalid amount");
                    return;
                }
            }

            config.Rewards.ItemRewards.Remove(cleanOldItemKey);

            string newItemKey = finalSkinId > 0 ? $"{finalShortname}_{finalSkinId}" : finalShortname;
            config.Rewards.ItemRewards[newItemKey] = finalAmount;

            _inputFieldValues.Remove($"{player.UserIDString}_edit_shortname_{oldItemKey}");
            _inputFieldValues.Remove($"{player.UserIDString}_edit_skinid_{oldItemKey}");
            _inputFieldValues.Remove($"{player.UserIDString}_edit_amount_{oldItemKey}");

            SaveConfig();
            
            CuiHelper.DestroyUi(player, "AmountInputDialog");
            ShowRewardsUI(player);
            player.ChatMessage("Item updated successfully");
        }

        [ConsoleCommand("crestart.rewards.updateamount")]
        private void CmdUpdateAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            string itemShortname = arg.GetString(0);
            string amount = arg.GetString(1);
            
            string key = $"{player.UserIDString}_amount_{itemShortname}";
            _inputFieldValues[key] = amount;
        }

        [ConsoleCommand("crestart.rewards.closeamount")]
        private void CmdCloseAmountInput(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;
            
            CuiHelper.DestroyUi(player, "AmountInputDialog");
            ShowRewardsUI(player);
        }

        [ConsoleCommand("crestart.rewards.setitemamount")]
        private void CmdSetItemAmount(ConsoleSystem.Arg arg)
        {
	        var player = arg.Player();
	        if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
		        return;

	        string itemKey = arg.GetString(0);
	        string key = $"{player.UserIDString}_amount_{itemKey}";
	        string amountStr = _inputFieldValues.GetValueOrDefault(key, "0");
    
	        if (int.TryParse(amountStr, out int amount))
	        {
		        if (amount <= 0)
		        {
			        config.Rewards.ItemRewards.Remove(itemKey);
			        LogInfo($"Removed item {itemKey} from rewards");
		        }
		        else
		        {
			        config.Rewards.ItemRewards[itemKey] = amount;
			        LogInfo($"Updated item {itemKey} amount to {amount}");
		        }

		        SaveConfig();
	        }

	        _inputFieldValues.Remove(key);
    
	        CuiHelper.DestroyUi(player, "AmountInputDialog");
	        ShowRewardsUI(player);
        }

        [ConsoleCommand("crestart.rewards.clearsearch")]
        private void CmdClearSearch(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            _searchInputValues["current_search"] = string.Empty;
            ShowRewardsUI(player);
        }

        [ConsoleCommand("crestart.rewards.settimelimit")]
        private void CmdSetRewardTimeLimit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            int timeLimit;
            if (int.TryParse(arg.GetString(0), out timeLimit) && timeLimit >= 0)
            {
                config.Rewards.RewardTimeLimit = timeLimit;
                SaveConfig();
            }

            ShowRewardsUI(player);
        }

        [ConsoleCommand("crestart.rewards.close")]
        private void CmdCloseRewardsUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;
            
            CuiHelper.DestroyUi(player, "RewardsMainPanel");
        }

        [ConsoleCommand("crestart.rewards.selectitem")]
        private void CmdSelectRewardItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            string itemShortname = arg.GetString(0);
            ShowItemAmountInput(player, itemShortname);
        }

        [ConsoleCommand("crestart.rewards.seteconomics")]
        private void CmdSetEconomicsAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            double amount;
            if (double.TryParse(arg.GetString(0), out amount))
            {
                config.Rewards.EconomicsAmount = amount;
                SaveConfig();
            }

            ShowRewardsUI(player);
        }

        [ConsoleCommand("crestart.rewards.setserverrewards")]
        private void CmdSetServerRewardsPoints(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            int points;
            if (int.TryParse(arg.GetString(0), out points))
            {
                config.Rewards.ServerRewardsPoints = points;
                SaveConfig();
            }

            ShowRewardsUI(player);
        }

		private bool GetConfigValue(string key)
		{
		    switch (key)
		    {
		        case "UseCustomUI": return config.UI.UseCustomUI;
		        case "UseRustUI": return config.UI.UseRustUI;
		        case "UseChatAlerts": return config.Restart.UseChatAlerts;
		        case "UseSound": return config.Restart.UseSound;
		        case "CheckForUpdates": return config.UpdateCheck.CheckForUpdates;
		        case "RestrictPlayerCount": return config.PlayerRestrictions.RestrictPlayerCount;
		        case "DiscordNotifications": return config.Discord.DiscordNotifications;
		        case "UseFullMinuteCountdown": return config.Discord.UseFullMinuteCountdown;
		        case "SkipWipeDays": return config.Restart.SkipWipeDays;
		        case "DisableDiscordCountdownMessages": return config.DisableDiscordCountdownMessages;
		        default: return false;
		    }
		}

		private void SetConfigValue(string setting, bool value)
		{
		    switch (setting)
		    {
		        case "UseCustomUI":
		            config.UI.UseCustomUI = value;
		            break;
		        case "UseRustUI":
		            config.UI.UseRustUI = value;
		            break;
		        case "UseChatAlerts":
		            config.Restart.UseChatAlerts = value;
		            break;
		        case "UseSound":
		            config.Restart.UseSound = value;
		            break;
		        case "CheckForUpdates":
		            config.UpdateCheck.CheckForUpdates = value;
		            break;
		        case "RestrictPlayerCount":
		            config.PlayerRestrictions.RestrictPlayerCount = value;
		            break;
		        case "DiscordNotifications":
		            config.Discord.DiscordNotifications = value;
		            break;
		        case "UseFullMinuteCountdown":
		            config.Discord.UseFullMinuteCountdown = value;
		            break;
		        case "SkipWipeDays":
		            config.Restart.SkipWipeDays = value;
		            break;
		        case "DisableDiscordCountdownMessages":
		            config.DisableDiscordCountdownMessages = value;
		            break;
		    }
		}

		private void AddCheckboxes(CuiElementContainer container)
		{
		    string[][] checkboxes = new string[][]
		    {
		        new[] { "UseCustomUIPanel", "Use custom UI", "UseCustomUI" },
		        new[] { "UseRustUIPanel", "Use Rust UI", "UseRustUI" },
		        new[] { "UseChatNotiPanel", "Use chat notifications", "UseChatAlerts" },
		        new[] { "UseSoundPanel", "Use sound notifications", "UseSound" },
		        new[] { "UseUpdatesPanel", "Use updates check", "CheckForUpdates" },
		        new[] { "UsePlayerResPanel", "Use player restriction", "RestrictPlayerCount" },
		        new[] { "UseDiscordPanel", "Use Discord notifications", "DiscordNotifications" },
		        new[] { "UseFullMinuteCountdownPanel", "Use full minute countdown (Discord)", "UseFullMinuteCountdown" },
		        new[] { "SkipWipeDaysPanel", "Skip restarts on wipe days", "SkipWipeDays" },
		        new[] { "DisableDiscordCountdownPanel", "Disable Discord countdown messages", "DisableDiscordCountdownMessages" }
		    };

		    float yStartPosition = -26f;
		    float height = 25f;
		    float spacing = 1f;
		    float currentY = yStartPosition;

		    float totalHeight = checkboxes.Length * (height + spacing);

		    container.Add(new CuiPanel
		    {
		        Image = { Color = "0 0 0 0" },
		        RectTransform = {
		            AnchorMin = "0 0",
		            AnchorMax = "1 1",
		            OffsetMin = $"0 -{totalHeight}",
		            OffsetMax = "0 0"
		        }
		    }, "CheckBXPanel", "CheckboxContainer");

		    for (int i = 0; i < checkboxes.Length; i++)
		    {
		        AddAdaptedCheckbox(container, checkboxes[i][0], "CheckboxContainer", checkboxes[i][1],
		            GetConfigValue(checkboxes[i][2]), checkboxes[i][2], currentY, height);
		        currentY -= (height + spacing);
		    }
		}
        
        private void UpdateCRestartUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            AddInputFields(container);
            CuiHelper.AddUi(player, container);
        }

        private void AddOptionPanel(CuiElementContainer container, string name, string anchorMin, string anchorMax,
            string offsetMin, string offsetMax)
        {
            container.Add(new CuiElement
            {
                Name = name,
                Parent = "CheckBXPanel",
                Components =
                {
                    new CuiNeedsCursorComponent(),
                    new CuiImageComponent { Color = "0.2 0.2 0.2 1" },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"10 {offsetMin}",
                        OffsetMax = $"-10 {offsetMax}"
                    }
                }
            });
        }

		private void AddAdaptedCheckbox(CuiElementContainer container, string checkboxName, string panelName,
		    string label, bool isChecked, string setting, float yOffset, float height)
		{
		    container.Add(new CuiPanel
		    {
		        Image = { Color = "0.15 0.15 0.15 0.95" },
		        RectTransform = {
		            AnchorMin = "0 1",
		            AnchorMax = "1 1",
		            OffsetMin = $"10 {yOffset}",
		            OffsetMax = $"-10 {yOffset + height}"
		        }
		    }, panelName, checkboxName);

		    container.Add(new CuiPanel
		    {
		        Image = { Color = isChecked ? "0.3647059 0.4470588 0.2235294 0.85" : "0.4156863 0.1101371 0.1101371 0.85" },
		        RectTransform = {
		            AnchorMin = "0 0.5",
		            AnchorMax = "0 0.5",
		            OffsetMin = "10 -10",
		            OffsetMax = "30 10"
		        }
		    }, checkboxName, $"{checkboxName}_ColorBox");

		    container.Add(new CuiLabel
		    {
		        Text = {
		            Text = isChecked ? config.UI.CustomUI.CheckIcon : config.UI.CustomUI.CrossIcon,
		            FontSize = 14,
		            Align = TextAnchor.MiddleCenter,
		            Color = isChecked ? config.UI.CustomUI.CheckIconColor : config.UI.CustomUI.CrossIconColor
		        },
		        RectTransform = {
		            AnchorMin = "0 0",
		            AnchorMax = "1 1",
		            OffsetMin = "0 0",
		            OffsetMax = "0 0"
		        }
		    }, $"{checkboxName}_ColorBox");

		    container.Add(new CuiLabel
		    {
		        Text = {
		            Text = label,
		            Font = "robotocondensed-regular.ttf",
		            FontSize = 11,
		            Align = TextAnchor.MiddleLeft,
		            Color = "1 1 1 1"
		        },
		        RectTransform = {
		            AnchorMin = "0 0",
		            AnchorMax = "1 1",
		            OffsetMin = "35 0",
		            OffsetMax = "-5 0"
		        }
		    }, checkboxName);

		    container.Add(new CuiButton
		    {
		        Button = {
		            Color = "0 0 0 0",
		            Command = $"crestart.toggle {setting}"
		        },
		        RectTransform = {
		            AnchorMin = "0 0",
		            AnchorMax = "1 1"
		        },
		        Text = { Text = "" }
		    }, checkboxName);
		}

        private void AddAdaptedInputField(CuiElementContainer container, string inputName, string label, string value, float yOffset, float height, string panelName)
        {
            string inputFieldName = $"{inputName}Field";
			float inputWidth = inputName == "WebhookUrl" ? 0.9f : 0.7f;
            
            container.Add(new CuiElement
            {
                Parent = panelName,
                Components =
                {
                    new CuiImageComponent { Color = "0.2 0.2 0.2 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"5 {yOffset}", OffsetMax = $"-5 {yOffset + height}" }
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = panelName,
                Components =
                {
                    new CuiTextComponent { Text = label, FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1", Font = "robotocondensed-bold.ttf" },
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"10 {yOffset + height / 2}", OffsetMax = $"-10 {yOffset + height}" }
                }
            });
            
            container.Add(new CuiElement
            {
                Name = inputFieldName,
                Parent = panelName,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = value,
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "0.7 0.7 0.7 1",
                        Command = $"crestart.input {inputName}",
    					CharsLimit = inputName == "WebhookUrl" ? 200 : 100,
                        Font = "robotocondensed-regular.ttf"
                    },
					new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = $"{inputWidth} 1", OffsetMin = $"10 {yOffset}", OffsetMax = $"-10 {yOffset + height / 2 - 5}" }
                }
            });

            string submitCommand = $"crestart.submitfield {inputName}";
    
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.75 1", AnchorMax = "0.98 1", OffsetMin = $"0 {yOffset}", OffsetMax = $"0 {yOffset + height / 2 - 5}" },
                Button = { Color = "0.3137255 0.5843138 0.772549 0.85", Command = submitCommand },
                Text = { Text = "Submit", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.3960784 0.7137255 0.9294118 1", Font = "robotocondensed-bold.ttf" }
            }, panelName);
        }
        
        [ConsoleCommand("crestart.updateinput")]
        private void CmdUpdateInput(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            if (arg.Args == null || arg.Args.Length < 2)
            {
                Puts("No value provided for updateinput command.");
                return;
            }

            var setting = arg.Args[0];
            var value = string.Join(" ", arg.Args.Skip(1).ToArray());

            Puts($"Input updated for setting '{setting}': {value}");

            UpdateConfigValue(setting, value);
            SaveConfig();
            LoadConfigValues();
            Puts($"Config updated: {setting} = {value}");
        }
        
        [ConsoleCommand("crestart.submitcurrent")]
        private void CmdSubmitCurrentSetting(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            var setting = arg.GetString(0);
            var currentValue = arg.GetString(1, "");

            if (string.IsNullOrEmpty(currentValue))
            {
                Puts($"Error: No value provided for setting {setting}");
                return;
            }

            UpdateConfigValue(setting, currentValue);
            SaveConfig();
            Puts($"Config updated: {setting} = {currentValue}");
            ShowCRestartUI(player);
        }
        
        [ConsoleCommand("crestart.submitfield")]
        private void CmdSubmitField(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            var setting = arg.GetString(0);
    
            string currentValue = GetCurrentInputValue(player, setting);
            if (string.IsNullOrEmpty(currentValue))
            {
                Puts($"Error: No value provided for setting {setting}");
                return;
            }

            UpdateConfigValue(setting, currentValue);
            SaveConfig();
            LoadConfigValues();
            Puts($"Config updated: {setting} = {currentValue}");

            ShowCRestartUI(player);
        }

        private void AddInputFields(CuiElementContainer container)
        {
            container.Add(new CuiPanel
            {
                RectTransform = { 
                    AnchorMin = "0 0", 
                    AnchorMax = "1 1",
                    OffsetMin = "0 0",
                    OffsetMax = "0 0"
                },
                Image = { Color = "0.15 0.15 0.15 0.95" }
            }, "InputFieldsPanel", "ScrollContainer");

            string scrollViewName = "ScrollContainer" + ".View";
            container.Add(new CuiElement
            {
                Parent = "ScrollContainer",
                Name = scrollViewName,
                Components =
                {
                    new CuiScrollViewComponent
                    {
                        ContentTransform = new CuiRectTransformComponent 
                        { 
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -625",
                            OffsetMax = "0 0"
                        },
                        Vertical = true,
                        Horizontal = false,
                        VerticalScrollbar = new CuiScrollbar 
                        {
                            Size = 8f
                        }
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "10 10",
                        OffsetMax = "-10 -10"
                    }
                }
            });

            string contentName = scrollViewName + ".Content";
            container.Add(new CuiPanel
            {
                RectTransform = {
                    AnchorMin = "0 1",
                    AnchorMax = "1 1",
                    OffsetMin = "0 -625",
                    OffsetMax = "0 0"
                },
                Image = { Color = "0 0 0 0" }
            }, scrollViewName, contentName);

            string[][] inputFields = new string[][]
            {
	            new[] { "AlertSound", "Alert Sound", config.Restart.AlertSound },
	            new[] { "MaxPlayersBeforeCancel", "Max Players Before Cancel", config.PlayerRestrictions.MaxPlayersBeforeCancel.ToString() },
	            new[] { "CheckInterval", "Update Check Interval", config.UpdateCheck.CheckInterval.ToString() },
	            new[] { "WebhookUrl", "Discord Webhook URL", config.Discord.WebhookUrl },
	            new[] { "ServerName", "Discord Server Name", config.Discord.ServerName },
	            new[] { "InGameWarningTimes", "In-Game Warning Times (Format: MM, MM)", string.Join(", ", config.Alerts.InGameWarningTimes) },
	            new[] { "DiscordWarningTimes", "Discord Warning Times (Format: MM, MM)", string.Join(", ", config.Alerts.DiscordWarningTimes) },
	            new[] { "RestartTimes", "UTC Restart Times (Format: HH:MM, HH:MM)", string.Join(", ", config.Restart.RestartTimes) },
	            new[] { "WipeDays", "Wipe Days to Skip (Format: Monday, Friday)", string.Join(", ", config.Restart.WipeDays ?? new List<string>()) }
            };

            float currentY = 8f;
            float itemHeight = 60f;
            float spacing = 10f;

            foreach (var field in inputFields)
            {
                var inputPanel = $"InputPanel_{field[0]}";
                
                container.Add(new CuiPanel
                {
                    RectTransform = {
                        AnchorMin = "-0.1 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"20 -{currentY + itemHeight}",
                        OffsetMax = $"-20 -{currentY}"
                    },
                    Image = { Color = "0.2 0.2 0.2 1" }
                }, contentName, inputPanel);

                container.Add(new CuiLabel
                {
                    RectTransform = {
                        AnchorMin = "0 0.5",
                        AnchorMax = "1 1",
                        OffsetMin = "10 0",
                        OffsetMax = "-10 0"
                    },
                    Text = {
                        Text = field[1],
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf"
                    }
                }, inputPanel);

                container.Add(new CuiElement
                {
                    Parent = inputPanel,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = field[2],
                            FontSize = 12,
                            Align = TextAnchor.MiddleLeft,
                            Color = "0.7 0.7 0.7 1",
                            Command = $"crestart.input {field[0]}",
                            CharsLimit = field[0] == "WebhookUrl" ? 200 : 100,
                            Font = "robotocondensed-regular.ttf"
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0 0",
                            AnchorMax = "0.7 0.5",
                            OffsetMin = "10 5",
                            OffsetMax = "-10 -5"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { 
                        AnchorMin = "0.80 0", 
                        AnchorMax = "0.98 0.5",
                        OffsetMin = "0 5",
                        OffsetMax = "0 -5"
                    },
                    Button = { 
                        Color = "0.3137255 0.5843138 0.772549 0.9", 
                        Command = $"crestart.submit {field[0]} {field[2]}"
                    },
                    Text = { 
                        Text = "Submit",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "0.3960784 0.7137255 0.9294118 1",
                        Font = "robotocondensed-bold.ttf"
                    }
                }, inputPanel);

                currentY += (itemHeight + spacing);
            }
        }

        [ConsoleCommand("crestart.scroll")]
        private void CmdScroll(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) 
                return;

            float scrollDelta = arg.GetFloat(0, 0f);
            float currentScroll = Mathf.Clamp01(scrollDelta);
            
            UpdateScrollPosition(player, currentScroll);
        }

        private void UpdateScrollPosition(BasePlayer player, float position)
        {
            var container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                RectTransform = { 
                    AnchorMin = $"0 {position}", 
                    AnchorMax = $"1 {position + 0.25}",
                    OffsetMin = "0 0",
                    OffsetMax = "0 0"
                },
                Image = { Color = "0.4 0.4 0.4 1" }
            }, "ScrollBarBG", "ScrollHandle");

            CuiHelper.DestroyUi(player, "ScrollHandle");
            CuiHelper.AddUi(player, container);
        }
        
        private void UpdateUIElement(BasePlayer player, string setting, string value)
        {
            var container = new CuiElementContainer();
            var elementName = $"{setting}Input";
            UpdateInputField(container, elementName, value);
            CuiHelper.DestroyUi(player, elementName);
            CuiHelper.AddUi(player, container);
        }
        
        private void UpdateCheckbox(CuiElementContainer container, string elementName, bool isChecked)
        {
            var element = container.FirstOrDefault(e => e.Name == elementName);
            if (element != null)
            {
                var imageComponent = element.Components.OfType<CuiImageComponent>().FirstOrDefault();
                if (imageComponent != null)
                {
                    imageComponent.Color = isChecked ? "0.2 0.8 0.2 0.9" : "0.8 0.2 0.2 0.9";
                }
            }
        }
        
        private void UpdateInputField(CuiElementContainer container, string elementName, string value)
        {
            var element = container.FirstOrDefault(e => e.Name == elementName);
            if (element != null)
            {
                var textComponent = element.Components.OfType<CuiTextComponent>().FirstOrDefault();
                if (textComponent != null)
                {
                    textComponent.Text = value;
                }
            }
        }
        
        private void ShowCustomUI(BasePlayer player, string message, bool isCancelMessage)
        {
            CuiHelper.DestroyUi(player, "RESTARTBACKGROUND");

            var container = new CuiElementContainer();

            string title = GetLang("CustomUITitle", player.UserIDString);
            
            string displayMessage = isCancelMessage ? 
                GetLang("ChatRestartCanceled", player.UserIDString) : 
                message;

            RESTARTBACKGROUND(player, container, displayMessage, title);
    
            CuiHelper.AddUi(player, container);
            isCustomUICreated = true;
        }
        
        private void RESTARTBACKGROUND(BasePlayer player, CuiElementContainer container, string message, string title)
        {
            string panelName = "RESTARTBACKGROUND";

            container.Add(CreateBackgroundPanel(panelName));
            container.Add(CreateMainPanel(panelName));
            container.Add(CreateBroadcastImage());
            
            container.Add(new CuiElement
            {
                Name = "TitleText",
                Parent = "RestartPanelMain",
                Components = {
                    new CuiTextComponent 
                    { 
                        Text = title,
                        Font = "robotocondensed-bold.ttf", 
                        FontSize = config.UI.CustomUI.TitleFontSize, 
                        Align = TextAnchor.MiddleCenter, 
                        Color = "0.7607843 0.9058824 1 1" 
                    },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.5 0.5", 
                        AnchorMax = "0.5 0.5", 
                        OffsetMin = "-43.014 0", 
                        OffsetMax = "80.785 35.78" 
                    }
                }
            });

            AddCountdownText(container, message);
        }

        private CuiElement CreateBackgroundPanel(string panelName)
        {
            return new CuiElement
            {
                Name = panelName,
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent{ Color = "0 0 0 0" },
                    new CuiRectTransformComponent
                    { 
                        AnchorMin = config.UI.CustomUI.AnchorMin, 
                        AnchorMax = config.UI.CustomUI.AnchorMax, 
                        OffsetMin = config.UI.CustomUI.OffsetMin, 
                        OffsetMax = config.UI.CustomUI.OffsetMax 
                    }
                }
            };
        }

        private CuiElement CreateMainPanel(string parent)
        {
            return new CuiElement
            {
                Name = "RestartPanelMain",
                Parent = parent,
                Components =
                {
                    new CuiImageComponent
                    { 
                        Color = config.UI.CustomUI.BackgroundColor,
                        Material = "assets/icons/iconmaterial.mat"
                    },
                    new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            };
        }

        private CuiElement CreateBroadcastImage()
        {
            return new CuiElement
            {
                Name = "ImageBroadcast",
                Parent = "RestartPanelMain",
                Components = {
                    new CuiImageComponent { Color = "0.3960784 0.7137255 0.9294118 1", Sprite = "assets/icons/broadcast.png" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "7.353 -35.781", OffsetMax = "40.647 35.78" }
                }
            };
        }

        private CuiElement CreateTitleText()
        {
            return new CuiElement
            {
                Name = "TitleText",
                Parent = "RestartPanelMain",
                Components = {
                    new CuiTextComponent 
                    { 
                        Text = GetLang("CustomUITitle"), 
                        Font = "robotocondensed-bold.ttf", 
                        FontSize = config.UI.CustomUI.TitleFontSize, 
                        Align = TextAnchor.MiddleCenter, 
                        Color = "0.7607843 0.9058824 1 1" 
                    },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-43.014 0", OffsetMax = "80.785 35.78" }
                }
            };
        }
        
        private void AddCountdownText(CuiElementContainer container, string message)
        {
            container.Add(new CuiElement
            {
                Name = "CountdownText",
                Parent = "RestartPanelMain",
                Components = {
                    new CuiTextComponent { 
                        Text = message,
                        Font = "robotocondensed-regular.ttf", 
                        FontSize = config.UI.CustomUI.MessageFontSize, 
                        Align = TextAnchor.MiddleCenter,
                        Color = "0.7607843 0.9058824 1 1" 
                    },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { 
                        AnchorMin = "0.5 0.5", 
                        AnchorMax = "0.5 0.5", 
                        OffsetMin = "-43.016 -35.78", 
                        OffsetMax = "78.894 0" 
                    }
                }
            });
        }

        private string GetLang(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion

        #region Discord Notification Methods

        private void SendDiscordNotification(string message)
        {
            if (string.IsNullOrEmpty(config.Discord.WebhookUrl) || !config.Discord.DiscordNotifications) 
                return;

            DiscordPayload payload = new DiscordPayload { Content = message };
            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(config.Discord.WebhookUrl, json, HandleDiscordResponse, this, 
                Oxide.Core.Libraries.RequestMethod.POST, 
                new Dictionary<string, string> { { "Content-Type", "application/json" } });

            LogInfo($"Sent Discord notification: {message}");
        }

                private void HandleDiscordResponse(int code, string response)
                {
                    if (code != DISCORD_SUCCESS_CODE)
                    {
                        LogError($"Failed to send Discord notification. Code: {code}, Response: {response}");
                    }
                    else
                    {
                        LogInfo("Discord notification sent successfully");
                    }
                }

                private void NotifyDiscord(int seconds)
                {
	                if (config.DisableDiscordCountdownMessages)
		                return;

	                if (notifiedWarningTimes.Contains(seconds)) 
		                return;

	                if (!config.Discord.DiscordNotifications)
		                return;

	                bool shouldNotify = false;
	                int currentMinutes = (int)Math.Ceiling(seconds / 60.0);

	                if (config.Alerts.DiscordWarningTimes != null && 
	                    config.Alerts.DiscordWarningTimes.Contains(currentMinutes))
	                {
		                shouldNotify = true;
	                }
	                else if (seconds <= 60)
	                {
		                shouldNotify = config.Discord.UseFullMinuteCountdown || 
		                               seconds == 60 || 
		                               seconds <= 10;
	                }

	                if (shouldNotify)
	                {
		                string timeMessage = GetTimeFormattedMessage(seconds);
		                string message = GetLang("DiscordRestartAlert", null);
		                message = string.Format(message, timeMessage);
		                SendDiscordNotification(message);

		                LogInfo($"Sending Discord notification for {seconds} seconds (CustomRestart: {isCustomRestart})");

		                notifiedWarningTimes.Add(seconds);
	                }
                }
                
        private void SendFinalRestartMessage()
        {
            SendDiscordNotification(GetLang("DiscordRestarting"));
        }

        private void NotifyDiscordOnline()
        {
	        if (hasSentDiscordOnlineNotification) return;

	        string message = string.Format(GetLang("DiscordOnline"), config.Discord.ServerName);
	        SendDiscordNotification(message);
	        hasSentDiscordOnlineNotification = true;
    
	        LogInfo("Sent Discord online notification");
        }

		private void NotifyDiscordRestarting()
		{
			if (hasSentFinalRestartNotification) return;
			
			SendDiscordNotification(GetLang("DiscordRestarting"));
			hasSentFinalRestartNotification = true;
			
			timer.Once(300f, () => hasSentFinalRestartNotification = false);
		}

        #endregion

        #region Update Check Methods

        private void CheckForUpdates()
        {
            webrequest.Enqueue("https://umod.org/games/rust.json", null, HandleUpdateResponse, this);
        }

        private void HandleUpdateResponse(int code, string response)
        {
            if (code != 200 || string.IsNullOrEmpty(response))
            {
                HandleUpdateCheckError();
                return;
            }

            try
            {
                ProcessUpdateData(response);
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }

            ScheduleNextUpdateCheck();
        }

        private void HandleUpdateCheckError()
        {
            PrintError("Failed to check for uMod updates.");
            ScheduleNextUpdateCheck();
        }

		private void ProcessUpdateData(string response)
		{
			if (!config.UpdateCheck.CheckForUpdates)
				return;
				
			try
			{
				var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
				if (data != null && data.ContainsKey("latest_release_version"))
				{
					Version latestVersion = new Version(data["latest_release_version"].ToString());
					Version currentVersion = new Version(Interface.Oxide.GetAllExtensions()
						.First(e => e.Name == "Rust").Version.ToString());

					if (latestVersion > currentVersion)
					{
						Puts("A new uMod update has been detected! Restarting the server to apply the update.");
						foreach (var player in BasePlayer.activePlayerList)
						{
							player.ChatMessage(GetLang("UpdateDetected", player.UserIDString));
						}
						StartRestartSequence();
					}
				}
			}
			catch (Exception ex)
			{
				LogError($"Error processing update data: {ex.Message}"); 
			}
		}

        private void ScheduleNextUpdateCheck()
        {
            timer.Once(config.UpdateCheck.CheckInterval, CheckForUpdates);
        }

        private void StartRestartSequence()
        {
            int totalSeconds = 300;
            StartCustomRestart(totalSeconds);
        }

        #endregion

        #region Rewards Methods

        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.Rewards.EnableRewards && rewardLimitTimer != null && !rewardedPlayers.Contains(player.userID))
            {
                GiveRewardsToPlayer(player);
            }
        }

        private void StartRewardPeriod()
        {
            if (rewardStartTime == DateTime.MinValue)
            {
                rewardStartTime = DateTime.UtcNow;
                LogInfo($"Starting rewards period: {rewardStartTime}");
            }
            
            if (rewardLimitTimer != null)
            {
                rewardLimitTimer.Destroy();
            }

            rewardLimitTimer = timer.Once(config.Rewards.RewardTimeLimit, () =>
            {
                LogInfo("Rewards period ended. Cleaning list of rewarded players.");
                
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage(GetLang("RewardPeriodEnded", player.UserIDString));
                }
                
                rewardedPlayers.Clear();
                rewardStartTime = DateTime.MinValue;
            });
        }

        private void GiveRewardsToPlayer(BasePlayer player)
        {
            if (!config.Rewards.EnableRewards || player == null) 
                return;

            if (rewardedPlayers.Contains(player.userID))
            {
                LogInfo($"Player {player.displayName} has already received rewards in this period");
                return;
            }

            if (rewardStartTime == DateTime.MinValue)
            {
                LogInfo($"No active rewards period for {player.displayName}");
                return;
            }

            TimeSpan timeSinceStart = DateTime.UtcNow - rewardStartTime;
            if (timeSinceStart.TotalSeconds > config.Rewards.RewardTimeLimit)
            {
                LogInfo($"Outside rewards period for {player.displayName}. {timeSinceStart.TotalMinutes:F1} minutes have passed.");
                return;
            }

            rewardedPlayers.Add(player.userID);
            bool rewardsGiven = false;

            try
            {
                if (config.Rewards.UseEconomics && Economics != null)
                {
                    rewardsGiven |= GiveEconomicsReward(player);
                }

                if (config.Rewards.UseServerRewards && ServerRewards != null)
                {
                    rewardsGiven |= GiveServerRewardsPoints(player);
                }

                if (config.Rewards.ItemRewards != null && config.Rewards.ItemRewards.Count > 0)
                {
                    rewardsGiven |= GiveItemRewards(player);
                }

                if (rewardsGiven)
                {
                    LogInfo($"Rewards successfully delivered to {player.displayName}");
                    string timeLeft = FormatTimeLeft(config.Rewards.RewardTimeLimit - (int)timeSinceStart.TotalSeconds);
                    player.ChatMessage(string.Format(GetLang("RewardTimeLeft", player.UserIDString), timeLeft));
                }
                else
                {
                    rewardedPlayers.Remove(player.userID);
                }
            }
            catch (Exception ex)
            {
                LogError($"Error giving rewards to {player.displayName}: {ex.Message}");
                rewardedPlayers.Remove(player.userID);
            }
        }

        private string FormatTimeLeft(int seconds)
        {
            if (seconds <= 0)
                return "0 segundos";

            int minutes = seconds / 60;
            int remainingSeconds = seconds % 60;

            if (minutes > 0)
            {
                if (remainingSeconds > 0)
                    return $"{minutes} minutes{(minutes != 1 ? "s" : "")} and {remainingSeconds} second{(remainingSeconds != 1 ? "s" : "")}";
                return $"{minutes} minute{(minutes != 1 ? "s" : "")}";
            }
            
            return $"{seconds} segundo{(seconds != 1 ? "s" : "")}";
        }

        private void OnServerSave()
        {
            if (config.Rewards.EnableRewards)
            {
                Interface.Oxide.DataFileSystem.WriteObject("AutoRestart_Rewards", new RewardsData
                {
                    RewardedPlayers = rewardedPlayers.ToList(),
                    RewardStartTime = rewardStartTime
                });
            }
        }

        private void LoadRewardsData()
        {
            if (config.Rewards.EnableRewards)
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<RewardsData>("AutoRestart_Rewards");
                if (data != null)
                {
                    rewardedPlayers = new HashSet<ulong>(data.RewardedPlayers);
                    rewardStartTime = data.RewardStartTime;

                    if (rewardStartTime != DateTime.MinValue)
                    {
                        TimeSpan timeSinceStart = DateTime.UtcNow - rewardStartTime;
                        if (timeSinceStart.TotalSeconds > config.Rewards.RewardTimeLimit)
                        {
                            rewardedPlayers.Clear();
                            rewardStartTime = DateTime.MinValue;
                        }
                        else
                        {
                            float remainingTime = config.Rewards.RewardTimeLimit - (float)timeSinceStart.TotalSeconds;
                            rewardLimitTimer = timer.Once(remainingTime, () =>
                            {
                                rewardedPlayers.Clear();
                                rewardStartTime = DateTime.MinValue;
                            });
                        }
                    }
                }
            }
        }

        private bool GiveEconomicsReward(BasePlayer player)
        {
            if (Economics == null || !config.Rewards.UseEconomics) return false;

            double amount = config.Rewards.EconomicsAmount;
            if (amount <= 0) return false;

            object success = Economics.Call("Deposit", player.userID, amount);
            if (success is bool && (bool)success)
            {
                player.ChatMessage(string.Format(GetLang("RewardEconomicsReceived", player.UserIDString), amount.ToString("F2")));
                LogInfo($"Successfully gave ${amount:F2} to {player.displayName} via Economics");
                return true;
            }

            LogError($"Failed to give Economics reward to {player.displayName}");
            return false;
        }

        private bool GiveServerRewardsPoints(BasePlayer player)
        {
            if (ServerRewards == null || !config.Rewards.UseServerRewards) return false;

            int points = config.Rewards.ServerRewardsPoints;
            if (points <= 0) return false;

            object success = ServerRewards.Call("AddPoints", player.userID, points);
            if (success is bool && (bool)success)
            {
                player.ChatMessage(string.Format(GetLang("RewardPointsReceived", player.UserIDString), points));
                LogInfo($"Successfully gave {points} RP to {player.displayName} via ServerRewards");
                return true;
            }

            LogError($"Failed to give ServerRewards points to {player.displayName}");
            return false;
        }

        private bool GiveItemRewards(BasePlayer player)
        {
            if (config.Rewards.ItemRewards == null || config.Rewards.ItemRewards.Count == 0) return false;

            bool anyItemGiven = false;

            foreach (var reward in config.Rewards.ItemRewards)
            {
                try 
                {
                    string[] itemParts = reward.Key.Split('_');
                    string shortname = itemParts[0];
                    ulong skinId = 0UL;

                    if (itemParts.Length > 1 && !ulong.TryParse(itemParts[1], out skinId))
                    {
                        LogError($"Invalid skin ID format for item {shortname}: {itemParts[1]}");
                        continue;
                    }

                    var itemDef = ItemManager.FindItemDefinition(shortname);
                    if (itemDef == null)
                    {
                        LogError($"Failed to find item definition for {shortname}");
                        continue;
                    }

                    int amount = reward.Value;
                    if (amount <= 0) continue;

                    Item item = skinId > 0 ? 
                        ItemManager.CreateByItemID(itemDef.itemid, amount, skinId) : 
                        ItemManager.CreateByName(shortname, amount);

                    if (item == null)
                    {
                        LogError($"Failed to create item {shortname} (Skin: {skinId}) for {player.displayName}");
                        continue;
                    }

                    if (!item.MoveToContainer(player.inventory.containerMain))
                    {
                        Vector3 dropPosition = player.transform.position + (player.eyes.BodyForward() * 1f) + new Vector3(0f, 1f, 0f);
                        item.Drop(dropPosition, Vector3.zero);
                        player.ChatMessage(string.Format(GetLang("RewardItemReceived", player.UserIDString), 
                            amount, 
                            itemDef.displayName.english + (skinId > 0 ? $" (Skin: {skinId})" : "")) + " (Dropped at your feet)");
                        LogInfo($"Dropped {amount}x {itemDef.displayName.english} (Skin: {skinId}) at {player.displayName}'s feet (inventory full)");
                    }
                    else
                    {
                        player.ChatMessage(string.Format(GetLang("RewardItemReceived", player.UserIDString), 
                            amount, 
                            itemDef.displayName.english + (skinId > 0 ? $" (Skin: {skinId})" : "")));
                        LogInfo($"Successfully gave {amount}x {itemDef.displayName.english} (Skin: {skinId}) to {player.displayName}");
                    }
                    anyItemGiven = true;
                }
                catch (Exception ex)
                {
                    LogError($"Error giving item reward {reward.Key} to {player.displayName}: {ex.Message}");
                }
            }

            return anyItemGiven;
        }

        void OnEconomicsDeposit(string playerId, double amount)
        {
            ulong userId;
            if (!ulong.TryParse(playerId, out userId)) return;
            
            var player = BasePlayer.FindByID(userId);
            if (player != null)
            {
                LogInfo($"Economics deposit confirmed for {player.displayName}: ${amount:F2}");
            }
        }

        private bool ValidateEconomicsPlugin()
        {
            if (Economics == null)
            {
                LogError("Economics plugin not found");
                return false;
            }

            var version = Economics?.Version;
            LogInfo($"Economics plugin found, version: {version}");
            return true;
        }

        private bool ValidateServerRewardsPlugin()
        {
            if (ServerRewards == null)
            {
                LogError("ServerRewards plugin not found");
                return false;
            }

            var version = ServerRewards?.Version;
            LogInfo($"ServerRewards plugin found, version: {version}");
            return true;
        }
        #endregion

        #region Helper Classes

        private class DiscordPayload
        {
            [JsonProperty("content")]
            public string Content { get; set; }
        }
        
        private string GetCurrentInputValue(BasePlayer player, string inputName)
        {
            string key = $"{player.UserIDString}_{inputName}";
            return _inputFieldValues.TryGetValue(key, out string value) ? value : null;
        }

        private void Unload()
        {
            rewardLimitTimer?.Destroy();
            rewardLimitTimer = null;
            
            nextRestartTimer?.Destroy();
            nextRestartTimer = null;
            
            foreach (var timer in activeTimers)
            {
                timer?.Destroy();
            }
            activeTimers.Clear();
            
            rewardedPlayers.Clear();
            notifiedWarningTimes.Clear();
            
            isCustomRestart = false;
            isCancelled = false;
            isShuttingDown = false;
            isCustomUICreated = false;
            
            DestroyAllUI();
        }

        #endregion
    }

    #region ColorExtensions

    public static class ColorExtensions
    {
        public static string ToHex(this string rgbaColor)
        {
            if (string.IsNullOrEmpty(rgbaColor)) return "#FFFFFF";

            var parts = rgbaColor.Split(' ');
            if (parts.Length != 4) return "#FFFFFF";

            byte r = (byte)(float.Parse(parts[0]) * 255);
            byte g = (byte)(float.Parse(parts[1]) * 255);
            byte b = (byte)(float.Parse(parts[2]) * 255);

            return $"#{r:X2}{g:X2}{b:X2}";
        }   
    }
    #endregion
}   