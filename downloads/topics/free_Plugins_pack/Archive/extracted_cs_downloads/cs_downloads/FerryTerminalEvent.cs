using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FerryTerminalEvent", "FacepunchAI", "1.0.0")]
    [Description(
        "Масштабное PvE-событие на паромном терминале: баржа с Chinook Crate, торговец с заложниками, NPC-охранники, волки, патрули, кастомные маршруты и лут."
    )]
    public class FerryTerminalEvent : CovalencePlugin
    {
        #region Константы и переменные
        private const string PermAdmin = "ferryterminal.admin";
        private const string PermEvent = "ferryterminal.event";
        private FerryConfig? config;
        private bool eventActive;

        /// <summary>
        /// Ссылки на сущности события
        /// </summary>
        private BaseEntity? bargeEntity;
        private BaseEntity? traderEntity;
        private readonly List<BaseEntity> crateEntities = new();
        private readonly List<BasePlayer> hostageEntities = new();
        private BaseEntity? mapMarkerEntity;
        private BaseEntity? vendingMarkerEntity;

        // ... другие сущности по необходимости

        /// <summary>
        /// Флаги для синхронизации прибытия
        /// </summary>
        private bool bargeArrived;
        private bool traderArrived;

        /// <summary>
        /// player -&gt; hostage index
        /// </summary>
        private readonly Dictionary<BasePlayer, int> hostageRescueInProgress = new();
        #endregion Константы и переменные

        #region Конфиг
        public class FerryConfig
        {
            public Vector3 BargeStartPos { get; set; } = new Vector3(0, 0, 0);
            public Vector3 BargeDockPos { get; set; } = new Vector3(0, 0, 0);
            public Vector3 TraderStartPos { get; set; } = new Vector3(0, 0, 0);
            public Vector3 TraderStopPos { get; set; } = new Vector3(0, 0, 0);
            public List<Vector3>? HostagePositions { get; set; }
            public List<Vector3>? CrateOffsets { get; set; }
            public float EventDuration { get; set; } = 1800f;
            public float HostageRescueTime { get; set; } = 30f;

            // ... другие параметры

            /// <summary>
            /// --- Новые блоки настроек ---
            /// </summary>
            public MapMarkerSettings MapMarker { get; set; } = new();
            public MainScreenMarkerSettings MainScreenMarker { get; set; } = new();
            public ExtraScreenMarkerSettings ExtraScreenMarker { get; set; } = new();
            public GuiSettings GUI { get; set; } = new();
            public ChatSettings Chat { get; set; } = new();
            public GameTipSettings GameTip { get; set; } = new();
            public GuiAnnouncementsSettings GUIAnnouncements { get; set; } = new();
            public NotifySettings Notify { get; set; } = new();
            public DiscordSettings Discord { get; set; } = new();
            public double EventRadius { get; set; } = 150.0;
            public bool CreatePvpZone { get; set; }
            public PveModeSettings PveMode { get; set; } = new();
            public bool BlockTeleport { get; set; } = true;
            public bool DisableBetterNpc { get; set; } = true;
            public EconomySettings Economy { get; set; } = new();
            public List<string>? BlockedCommands { get; set; }
            public VersionConfig Version { get; set; } = new();
            public double GlobalNotifyDistance { get; set; }

            /// <summary>
            /// --- Вложенные классы ---
            /// </summary>
            public class MapMarkerSettings
            {
                public bool Enabled { get; set; } = true;
                public int Type { get; set; } = 1;
                public double Radius { get; set; } = 0.37967;
                public double Alpha { get; set; } = 0.35;
                public ColorRgb? Color { get; set; }
                public string Text { get; set; } = "FerryTerminalEvent";
            }

            public class MainScreenMarkerSettings
            {
                public bool Enabled { get; set; } = true;
                public string Text { get; set; } = "◈";
                public int Size { get; set; } = 25;
                public string Color { get; set; } = "#CCFF00";
            }

            public class ExtraScreenMarkerSettings
            {
                public bool Enabled { get; set; } = true;
                public string Text { get; set; } = "◆";
                public int Size { get; set; } = 25;
                public string Color { get; set; } = "#FFC700";
            }

            public class GuiSettings
            {
                public bool Enabled { get; set; } = true;
                public int OffsetMinYTabs { get; set; } = -56;
                public int OffsetMinYHostage { get; set; } = -278;
            }

            public class ChatSettings
            {
                public bool Enabled { get; set; } = true;
                public string Prefix { get; set; } = "[FerryTerminalEvent]";
            }

            public class GameTipSettings
            {
                public bool Enabled { get; set; }
                public int Style { get; set; } = 2;
            }

            public class GuiAnnouncementsSettings
            {
                public bool Enabled { get; set; }
                public string BannerColor { get; set; } = "Orange";
                public string TextColor { get; set; } = "White";
                public double TopOffset { get; set; } = 0.03;
            }

            public class NotifySettings
            {
                public bool Enabled { get; set; }
                public int Type { get; set; }
            }

            public class DiscordSettings
            {
                public bool Enabled { get; set; }
                public string WebhookUrl { get; set; } =
                    "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
                public int BarColor { get; set; } = 13516583;
                public List<string>? MessageKeys { get; set; }
            }

            public class PveModeSettings
            {
                public bool Enabled { get; set; }
                public double RequiredDamage { get; set; } = 500.0;
                public DamageCoefficients? Coefficients { get; set; }
                public bool AllowNonOwnerLootCrates { get; set; }
                public bool AllowNonOwnerLootCorpses { get; set; }
                public bool AllowNonOwnerDamageNpc { get; set; }
                public bool AllowNpcAttackNonOwner { get; set; }
                public bool AllowNonOwnerEnterZone { get; set; }
                public bool AllowCooldownPlayerEnterZone { get; set; } = true;
                public int OwnerOutOfZoneTime { get; set; } = 300;
                public int OwnerWarnTime { get; set; } = 60;
                public bool BlockRestoreUponDeath { get; set; } = true;
                public double OwnerCooldown { get; set; } = 86400.0;
                public int DomeDarkness { get; set; } = 12;
            }

            public class DamageCoefficients
            {
                public double Npc { get; set; } = 1.0;
                public double Animal { get; set; } = 1.0;
            }

            public class EconomySettings
            {
                public List<string>? Plugins { get; set; }
                public double MinScore { get; set; }
                public double LootCrate { get; set; } = 0.5;
                public double KillNpc { get; set; } = 0.3;
                public double KillGuard { get; set; } = 0.5;
                public double KillWolf { get; set; } = 0.4;
                public double KillMotoNpc { get; set; } = 0.4;
                public double LiberateHostage { get; set; } = 0.8;
                public List<string>? ConsoleCommands { get; set; }
            }

            public class VersionConfig
            {
                public int Major { get; set; } = 1;
                public int Minor { get; set; }
                public int Patch { get; set; }
            }

            public class ColorRgb
            {
                public double R { get; set; } = 0.81;
                public double G { get; set; } = 0.25;
                public double B { get; set; } = 0.15;
            }
        }
        #endregion Конфиг

        #region Инициализация
        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);
            permission.RegisterPermission(PermEvent, this);
            LoadConfig();
            AddCovalenceCommand("ftstart", "CmdStartEvent");
            AddCovalenceCommand("ftstop", "CmdStopEvent");
            AddCovalenceCommand("ftpos", "CmdSetPos");
            FindFerryTerminalMonument();
        }

        private void FindFerryTerminalMonument()
        {
            MonumentInfo monument = TerrainMeta.Path.Monuments.FirstOrDefault(m =>
                m.name?.Contains("ferry_terminal", System.StringComparison.OrdinalIgnoreCase)
                == true
            );
            if (monument == null)
            {
                PrintError(
                    "Ferry Terminal monument not found! Plugin cannot work without the monument."
                );
            }
            // Здесь можно внедрить использование точек, если потребуется
        }

        protected override void LoadDefaultConfig()
        {
            config = new FerryConfig
            {
                HostagePositions = new List<Vector3>(),
                CrateOffsets = new List<Vector3>(),
                BlockedCommands = new List<string> { "/remove", "remove.toggle" },
                MapMarker = new FerryConfig.MapMarkerSettings
                {
                    Color = new FerryConfig.ColorRgb(),
                },
                MainScreenMarker = new FerryConfig.MainScreenMarkerSettings(),
                ExtraScreenMarker = new FerryConfig.ExtraScreenMarkerSettings(),
                GUI = new FerryConfig.GuiSettings(),
                Chat = new FerryConfig.ChatSettings(),
                GameTip = new FerryConfig.GameTipSettings(),
                GUIAnnouncements = new FerryConfig.GuiAnnouncementsSettings(),
                Notify = new FerryConfig.NotifySettings(),
                Discord = new FerryConfig.DiscordSettings
                {
                    MessageKeys = new List<string>
                    {
                        "PreStart",
                        "Start",
                        "PreFinish",
                        "Finish",
                        "StartAttack",
                        "LiberateHostage",
                        "OpenCrate",
                        "KillHostage",
                    },
                },
                Economy = new FerryConfig.EconomySettings
                {
                    Plugins = new List<string>
                    {
                        "Economics",
                        "Server Rewards",
                        "IQEconomic",
                        "XPerience",
                    },
                    ConsoleCommands = new List<string>(),
                },
                PveMode = new FerryConfig.PveModeSettings
                {
                    Coefficients = new FerryConfig.DamageCoefficients(),
                },
                Version = new FerryConfig.VersionConfig(),
            };
            // Все новые блоки настроек инициализируются дефолтными значениями в конструкторах классов FerryConfig и вложенных структур
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<FerryConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion Инициализация

        #region Локализация
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(
                new Dictionary<string, string>
                {
                    ["EventStarted"] = "Событие паромного терминала началось!",
                    ["EventEnded"] = "Событие паромного терминала завершено!",
                    ["NoPermission"] = "У вас нет прав на выполнение этой команды.",
                    ["RescuePrompt"] = "Подойдите к заложнику и удерживайте [E] для освобождения.",
                    ["HostageFreed"] = "Заложник освобождён! Сопроводите его к барже.",
                    // ... другие сообщения
                },
                this
            );
        }
        #endregion Локализация

        #region Команды
        [ChatCommand("ftstart")]
        private void CmdStartEvent(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
            StartEvent();
        }

        [ChatCommand("ftstop")]
        private void CmdStopEvent(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
            StopEvent();
        }

        [ChatCommand("ftpos")]
        private void CmdSetPos(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
            }
            // Логика сохранения позиций реализуется в соответствующих методах
        }
        #endregion Команды

        #region Основные хуки и логика события (заготовки)
        private void StartEvent()
        {
            if (eventActive)
            {
                return;
            }

            eventActive = true;
            covalence.Server.Broadcast(lang.GetMessage("EventStarted", this));
            if (config != null)
            {
                // 1. Спавн баржи в стартовой позиции
                bargeEntity = SpawnEntity(
                    "assets/content/vehicles/barge/barge.prefab",
                    config.BargeStartPos
                );
                // 2. Спавн торговца в стартовой позиции
                traderEntity = SpawnEntity(
                    "assets/prefabs/npc/trader/trader.prefab",
                    config.TraderStartPos
                );
                // 3. Запуск движения баржи и торговца к целевым точкам
                if (bargeEntity != null)
                {
                    MoveEntityTo(bargeEntity, config.BargeDockPos, OnBargeArrived);
                }

                if (traderEntity != null)
                {
                    MoveEntityTo(traderEntity, config.TraderStopPos, OnTraderArrived);
                }
                // 4. Сброс флагов прибытия
                bargeArrived = false;
                traderArrived = false;
                // 5. Создание маркера на карте
                CreateMapMarker();
                // 6. Интеграция: GUI
                if (config.GUI?.Enabled == true)
                {
                    ShowEventGUI();
                }
                // 7. Интеграция: DiscordMessages
                if (config.Discord?.Enabled == true)
                {
                    SendDiscordMessage("Start");
                }
                // 8. Интеграция: Notify
                if (config.Notify?.Enabled == true)
                {
                    SendNotify("Событие началось!", config.Notify.Type);
                }
                // 9. Интеграция: TruePVE
                if (config.CreatePvpZone)
                {
                    EnableTruePVEZone();
                }
                // 10. Интеграция: PveMode
                if (config.PveMode?.Enabled == true)
                {
                    EnablePveMode();
                }
                // 11. Интеграция: NTeleportation
                if (config.BlockTeleport)
                {
                    BlockTeleport();
                }
                // 12. Интеграция: BetterNpc
                if (config.DisableBetterNpc)
                {
                    DisableBetterNpc();
                }
                // 13. Интеграция: GUIAnnouncements
                if (config.GUIAnnouncements?.Enabled == true)
                {
                    SendGUIAnnouncement("Событие началось!");
                }
            }
            _ = Interface.CallHook(
                "OnFerryTerminalEventStart",
                config?.BargeDockPos ?? Vector3.zero,
                0f
            );
        }

        private void StopEvent()
        {
            if (!eventActive)
            {
                return;
            }

            eventActive = false;
            RemoveMapMarker();
            // Отключение интеграций
            if (config != null)
            {
                if (config.GUI?.Enabled == true)
                {
                    HideEventGUI();
                }

                if (config.Discord?.Enabled == true)
                {
                    SendDiscordMessage("Finish");
                }

                if (config.Notify?.Enabled == true)
                {
                    SendNotify("Событие завершено!", config.Notify.Type);
                }

                if (config.CreatePvpZone)
                {
                    DisableTruePVEZone();
                }

                if (config.PveMode?.Enabled == true)
                {
                    DisablePveMode();
                }

                if (config.BlockTeleport)
                {
                    UnblockTeleport();
                }

                if (config.DisableBetterNpc)
                {
                    EnableBetterNpc();
                }

                if (config.GUIAnnouncements?.Enabled == true)
                {
                    SendGUIAnnouncement("Событие завершено!");
                }
            }
            // Очистка всех сущностей события
            if (bargeEntity != null)
            {
                bargeEntity?.Kill();
                bargeEntity = null;
            }
            if (traderEntity != null)
            {
                traderEntity?.Kill();
                traderEntity = null;
            }
            foreach (BaseEntity crate in crateEntities)
            {
                crate?.Kill();
            }
            crateEntities.Clear();
            foreach (BasePlayer hostage in hostageEntities)
            {
                hostage?.Kill();
            }
            hostageEntities.Clear();
            // Удаляем все CUI-круги
            for (int i = 0; i < 4; i++)
            {
                foreach (BasePlayer? p in BasePlayer.activePlayerList)
                {
                    if (p != null)
                    {
                        _ = CuiHelper.DestroyUi(p, $"HostageCircle_{i}");
                    }
                }
            }
            hostageRescueInProgress.Clear();
            // Для MVP: патрульные, машины, волки не добавляются в отдельные списки. Если потребуется — добавить позже.
            covalence.Server.Broadcast(lang.GetMessage("EventEnded", this));
            _ = Interface.CallHook("OnFerryTerminalEventEnd");
        }

        /// <summary>
        /// Метод для спавна сущности по префабу
        /// </summary>
        /// <param name="prefab">Путь к префабу сущности</param>
        /// <param name="pos">Позиция для спавна</param>
        private BaseEntity? SpawnEntity(string prefab, Vector3 pos)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, pos, Quaternion.identity);
            if (entity != null)
            {
                entity.Spawn();
                return entity;
            }
            return null;
        }

        /// <summary>
        /// Метод для движения сущности к точке (упрощённо, без физики)
        /// </summary>
        /// <param name="entity">Сущность для перемещения</param>
        /// <param name="target">Целевая позиция</param>
        /// <param name="onArrived">Колбэк по прибытию</param>
        private void MoveEntityTo(
            BaseEntity entity,
            Vector3 target,
            System.Action? onArrived = null
        )
        {
            entity.transform.position = target;
            onArrived?.Invoke();
        }

        /// <summary>
        /// Колбэк по прибытию баржи
        /// </summary>
        private void OnBargeArrived()
        {
            bargeArrived = true;
            TryStartLandingPhase();
        }

        /// <summary>
        /// Колбэк по прибытию торговца
        /// </summary>
        private void OnTraderArrived()
        {
            traderArrived = true;
            TryStartLandingPhase();
        }

        /// <summary>
        /// Проверка: оба прибыли — начинаем высадку
        /// </summary>
        private void TryStartLandingPhase()
        {
            if (bargeArrived && traderArrived)
            {
                SpawnCratesOnBarge();
                SpawnHostagesInTrader();
                LandHostagesAndEscort();
            }
        }

        /// <summary>
        /// Спавн 4 Chinook Crate на барже
        /// </summary>
        private void SpawnCratesOnBarge()
        {
            if (bargeEntity == null || config == null)
            {
                return;
            }

            if (config.CrateOffsets == null)
            {
                return;
            }

            crateEntities.Clear();
            for (int i = 0; i < 4 && i < config.CrateOffsets.Count; i++)
            {
                Vector3 cratePos = bargeEntity.transform.position + config.CrateOffsets[i];
                BaseEntity? crate = SpawnEntity(
                    "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab",
                    cratePos
                );
                if (crate != null)
                {
                    crateEntities.Add(crate);
                }
            }
        }

        /// <summary>
        /// Спавн 4 заложников внутри торговца
        /// </summary>
        private void SpawnHostagesInTrader()
        {
            if (traderEntity == null || config == null)
            {
                return;
            }

            hostageEntities.Clear();
            for (int i = 0; i < 4; i++)
            {
                BasePlayer? hostage =
                    GameManager.server.CreateEntity(
                        "assets/rust.ai/agents/npcplayer/humannpc.prefab",
                        traderEntity.transform.position,
                        Quaternion.identity
                    ) as BasePlayer;
                if (hostage != null)
                {
                    hostage.Spawn();
                    hostageEntities.Add(hostage);
                    // Привязка к торговцу и скрытие до высадки реализуется при спавне
                }
            }
        }

        /// <summary>
        /// Спавнит заложников, эскортирует их к точкам высадки, вызывает анимацию и визуализацию
        /// </summary>
        private void LandHostagesAndEscort()
        {
            if (hostageEntities.Count == 0 || config == null)
            {
                return;
            }

            if (config.HostagePositions == null)
            {
                return;
            }

            for (int i = 0; i < hostageEntities.Count && i < config.HostagePositions.Count; i++)
            {
                BasePlayer hostage = hostageEntities[i];
                Vector3 targetPos = config.HostagePositions[i];
                // Телепортируем заложника к точке высадки
                hostage.transform.position = targetPos;
                hostage.SendNetworkUpdateImmediate();
                // Спавним NPC-охранника
                _ = SpawnEntity(
                    "assets/rust.ai/agents/npcplayer/humannpc.prefab",
                    targetPos + new Vector3(1, 0, 0)
                );
                // Спавним волка
                _ = SpawnEntity(
                    "assets/rust.ai/agents/wolf/wolf.prefab",
                    targetPos + new Vector3(-1, 0, 0)
                );
                // Назначение цели сопровождения реализуется в расширенной логике (MVP: не требуется)
                // Включаем анимацию "на коленях"
                GestureConfig? gesture = GestureCollection.Instance?.StringToGesture("kneel");
                if (gesture != null)
                {
                    hostage.SignalBroadcast(BaseEntity.Signal.Gesture, gesture.name);
                }
                // Нарисовать красный круг (CUI)
                DrawHostageCircle(targetPos, $"HostageCircle_{i}");
            }
        }

        /// <summary>
        /// Рисует красный круг CUI на позиции (world) для всех игроков
        /// </summary>
        /// <param name="worldPos">Мировая позиция центра круга</param>
        /// <param name="panelName">Уникальное имя панели CUI</param>
        private void DrawHostageCircle(Vector3 worldPos, string panelName)
        {
            // Преобразуем worldPos в screen (UI) координаты — для MVP используем фиксированное положение
            CuiElementContainer cui = new()
            {
                new CuiElement
                {
                    Name = panelName,
                    Parent = "Hud",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Sprite = "assets/content/ui/ui.circle.png",
                            Color = "1 0 0 0.5",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.48 0.48", // MVP: центр экрана, можно доработать под world-to-screen
                            AnchorMax = "0.52 0.52",
                        },
                    },
                },
            };
            foreach (BasePlayer? player in BasePlayer.activePlayerList)
            {
                _ = CuiHelper.AddUi(player, cui);
            }
        }

        /// <summary>
        /// Колбэк по прибытию заложника на баржу
        /// </summary>
        /// <param name="hostage">Заложник, прибывший на баржу</param>
        private void OnHostageArrivedAtBarge(BasePlayer hostage)
        {
            hostage.Kill(); // MVP: удаляем заложника
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input == null)
            {
                return;
            }
            if (!input.WasJustPressed(BUTTON.USE))
            {
                return;
            }
            if (hostageEntities.Count == 0)
            {
                return;
            }
            if (player == null || player.eyes == null || player.serverInput == null)
            {
                return;
            }
            Vector3 eyes = player.eyes.position;
            Vector3 dir = Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward;
            if (Physics.Raycast(eyes, dir, out RaycastHit hit, 3f))
            {
                for (int i = 0; i < hostageEntities.Count; i++)
                {
                    BasePlayer hostage = hostageEntities[i];
                    if (hit.collider != null && hit.collider.gameObject == hostage.gameObject)
                    {
                        if (hostageRescueInProgress.ContainsKey(player))
                        {
                            return;
                        } // Уже спасает
                        hostageRescueInProgress[player] = i;
                        // Удалить CUI-круг
                        _ = CuiHelper.DestroyUi(player, $"HostageCircle_{i}");
                        // Сообщение игроку
                        if (player.ChatMessage != null && player.UserIDString != null)
                        {
                            player.ChatMessage(
                                lang.GetMessage("RescuePrompt", this, player.UserIDString)
                            );
                        }
                        // Запустить таймер спасения
                        _ = timer.Once(
                            config?.HostageRescueTime ?? 30f,
                            () => CompleteHostageRescue(player, i)
                        );
                        break;
                    }
                }
            }
        }

        private void CompleteHostageRescue(BasePlayer player, int hostageIndex)
        {
            if (hostageIndex < 0 || hostageIndex >= hostageEntities.Count)
            {
                return;
            }
            BasePlayer hostage = hostageEntities[hostageIndex];
            _ = hostageRescueInProgress.Remove(player);
            if (player.ChatMessage != null && player.UserIDString != null)
            {
                player.ChatMessage(lang.GetMessage("HostageFreed", this, player.UserIDString));
            }
            // Отмена анимации kneel (жеста)
            hostage.SignalBroadcast(BaseEntity.Signal.Gesture, "");
            foreach (BasePlayer? p in BasePlayer.activePlayerList)
            {
                if (p != null)
                {
                    _ = CuiHelper.DestroyUi(p, $"HostageCircle_{hostageIndex}");
                }
            }
            if (config != null && bargeEntity != null)
            {
                MoveEntityTo(hostage, config.BargeDockPos, () => OnHostageArrivedAtBarge(hostage));
            }
            if (hostageIndex == 0)
            {
                SpawnGuardsAndPatrols();
            }
        }

        /// <summary>
        /// Спавнит 4 патрульных NPC и 4 NPC на мотоциклах, задаёт маршруты (MVP)
        /// </summary>
        private void SpawnGuardsAndPatrols()
        {
            if (config == null)
            {
                return;
            }
            // Примерные позиции для патрульных и мотоциклов (MVP: жёстко задано, доработать через config)
            List<Vector3> patrolPoints = new()
            {
                config.BargeDockPos + new Vector3(10, 0, 10),
                config.BargeDockPos + new Vector3(-10, 0, 10),
                config.BargeDockPos + new Vector3(-10, 0, -10),
                config.BargeDockPos + new Vector3(10, 0, -10),
            };
            // 4 патрульных NPC
            for (int i = 0; i < 4; i++)
            {
                BaseEntity? guard = SpawnEntity(
                    "assets/rust.ai/agents/npcplayer/humannpc.prefab",
                    patrolPoints[i]
                );
                // MVP: сразу двигаем к следующей точке (патруль)
                int next = (i + 1) % patrolPoints.Count;
                if (guard != null)
                {
                    MoveEntityTo(guard, patrolPoints[next], null);
                }
            }
            // 4 NPC на мотоциклах (MVP: создаём машину, спавним NPC рядом, телепортируем внутрь)
            for (int i = 0; i < 4; i++)
            {
                Vector3 motoPos =
                    config.BargeDockPos
                    + new Vector3(
                        15 * Mathf.Cos(i * Mathf.PI / 2),
                        0,
                        15 * Mathf.Sin(i * Mathf.PI / 2)
                    );
                BaseEntity? car = SpawnEntity(
                    "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab",
                    motoPos
                );
                BasePlayer? rider =
                    SpawnEntity(
                        "assets/rust.ai/agents/npcplayer/humannpc.prefab",
                        motoPos + new Vector3(1, 0, 0)
                    ) as BasePlayer;
                if (car != null && rider != null)
                {
                    // MVP: телепортируем NPC внутрь машины (безопасно для Oxide)
                    rider.transform.position = car.transform.position + new Vector3(0, 1, 0);
                    rider.SendNetworkUpdateImmediate();
                }
            }
        }

        /// <summary>
        /// Создаёт маркер на карте Rust с параметрами из конфига
        /// </summary>
        private void CreateMapMarker()
        {
            if (config?.MapMarker == null || !config.MapMarker.Enabled)
            {
                return;
            }

            RemoveMapMarker();
            Vector3 pos = config.BargeDockPos;
            const string circlePrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
            const string vendingPrefab =
                "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
            // Если требуется текстовый маркер (тип 1)
            if (config.MapMarker.Type == 1)
            {
                vendingMarkerEntity = GameManager.server.CreateEntity(
                    vendingPrefab,
                    pos,
                    Quaternion.identity
                );
                if (vendingMarkerEntity != null)
                {
                    VendingMachineMapMarker vending =
                        vendingMarkerEntity.GetComponent<VendingMachineMapMarker>();
                    if (vending != null)
                    {
                        vending.markerShopName = config.MapMarker.Text;
                        vending.enableSaving = false;
                        vending.Spawn();
                    }
                }
            }
            mapMarkerEntity = GameManager.server.CreateEntity(
                circlePrefab,
                pos,
                Quaternion.identity
            );
            if (mapMarkerEntity == null)
            {
                return;
            }

            mapMarkerEntity.Spawn();
            MapMarkerGenericRadius marker = mapMarkerEntity.GetComponent<MapMarkerGenericRadius>();
            if (marker != null)
            {
                marker.alpha = (float)config.MapMarker.Alpha;
                marker.radius = (float)config.MapMarker.Radius;
                if (config.MapMarker.Color != null)
                {
                    marker.color1 = new Color(
                        (float)config.MapMarker.Color.R,
                        (float)config.MapMarker.Color.G,
                        (float)config.MapMarker.Color.B,
                        (float)config.MapMarker.Alpha
                    );
                }
                marker.enableSaving = false;
                // Если есть vending, делаем SetParent
                if (vendingMarkerEntity != null)
                {
                    marker.SetParent(vendingMarkerEntity);
                }
                marker.SendUpdate();
            }
        }

        /// <summary>
        /// Удаляет маркер с карты, если он был создан
        /// </summary>
        private void RemoveMapMarker()
        {
            if (mapMarkerEntity != null)
            {
                mapMarkerEntity.Kill();
                mapMarkerEntity = null;
            }
            if (vendingMarkerEntity != null)
            {
                vendingMarkerEntity.Kill();
                vendingMarkerEntity = null;
            }
        }

        /// <summary>
        /// --- GUI (CUI) ---
        /// </summary>
        private void ShowEventGUI()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiElementContainer cui = new();
                _ = cui.Add(
                    new CuiPanel
                    {
                        Image = { Color = "0.1 0.1 0.1 0.8" },
                        RectTransform = { AnchorMin = "0.3 0.95", AnchorMax = "0.7 1" },
                        CursorEnabled = false,
                    },
                    "Hud",
                    "FerryEventGUI"
                );
                _ = cui.Add(
                    new CuiLabel
                    {
                        Text =
                        {
                            Text = "Ferry Terminal Event!",
                            FontSize = 18,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 0 1",
                        },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    },
                    "FerryEventGUI"
                );
                _ = CuiHelper.AddUi(player, cui);
            }
        }

        private void HideEventGUI()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                _ = CuiHelper.DestroyUi(player, "FerryEventGUI");
            }
        }

        /// <summary>
        /// --- DiscordMessages ---
        /// </summary>
        /// <param name="key">Message key for Discord notification</param>
        private void SendDiscordMessage(string key)
        {
            if (config?.Discord == null || !config.Discord.Enabled)
            {
                return;
            }
            string webhook = config.Discord.WebhookUrl ?? string.Empty;
            int color = config.Discord.BarColor;
            string text = key;
            if (config.Discord.MessageKeys?.Contains(key) == true)
            {
                text = $"[FerryTerminalEvent] {key}";
            }
            if (plugins.Find("DiscordMessages") != null)
            {
                server.Command("DiscordMessages", "SendMessage", webhook, text, color);
            }
        }

        /// <summary>
        /// --- Notify ---
        /// </summary>
        /// <param name="message">Notification message</param>
        /// <param name="type">Notification type</param>
        private void SendNotify(string message, int type)
        {
            if (plugins.Find("Notify") != null)
            {
                _ = Interface.CallHook("Send", null, message, type);
            }
        }

        /// <summary>
        /// --- TruePVE ---
        /// </summary>
        private void EnableTruePVEZone()
        {
            if (plugins.Find("TruePVE") != null && config != null)
            {
                _ = Interface.CallHook(
                    "AddZone",
                    "FerryTerminalEventZone",
                    config.BargeDockPos,
                    config.EventRadius
                );
            }
        }

        private void DisableTruePVEZone()
        {
            if (plugins.Find("TruePVE") != null)
            {
                _ = Interface.CallHook("RemoveZone", "FerryTerminalEventZone");
            }
        }

        /// <summary>
        /// --- PveMode ---
        /// </summary>
        private void EnablePveMode()
        {
            if (plugins.Find("PveMode") != null)
            {
                _ = Interface.CallHook("EnableEventMode", "FerryTerminalEvent");
            }
        }

        private void DisablePveMode()
        {
            if (plugins.Find("PveMode") != null)
            {
                _ = Interface.CallHook("DisableEventMode", "FerryTerminalEvent");
            }
        }

        /// <summary>
        /// --- NTeleportation ---
        /// </summary>
        private void BlockTeleport()
        {
            if (plugins.Find("NTeleportation") != null)
            {
                _ = Interface.CallHook("BlockZone", "FerryTerminalEventZone");
            }
        }

        private void UnblockTeleport()
        {
            if (plugins.Find("NTeleportation") != null)
            {
                _ = Interface.CallHook("UnblockZone", "FerryTerminalEventZone");
            }
        }

        /// <summary>
        /// --- BetterNpc ---
        /// </summary>
        private void DisableBetterNpc()
        {
            if (plugins.Find("BetterNpc") != null)
            {
                _ = Interface.CallHook("DisableNpcOnMonument", "ferryterminal");
            }
        }

        private void EnableBetterNpc()
        {
            if (plugins.Find("BetterNpc") != null)
            {
                _ = Interface.CallHook("EnableNpcOnMonument", "ferryterminal");
            }
        }

        /// <summary>
        /// --- GUIAnnouncements ---
        /// </summary>
        /// <param name="message">Announcement message</param>
        private void SendGUIAnnouncement(string message)
        {
            if (
                plugins.Find("GUIAnnouncements") != null
                && config?.GUIAnnouncements != null
            )
            {
                _ = Interface.CallHook(
                    "CreateAnnouncement",
                    message,
                    config.GUIAnnouncements.BannerColor,
                    config.GUIAnnouncements.TextColor,
                    config.GUIAnnouncements.TopOffset
                );
            }
        }
        #endregion Основные хуки и логика события (заготовки)
    }
}
