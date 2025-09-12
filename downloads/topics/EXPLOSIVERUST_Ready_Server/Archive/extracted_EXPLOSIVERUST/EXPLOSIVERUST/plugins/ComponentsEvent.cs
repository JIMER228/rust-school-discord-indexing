using System;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("ComponentsEvent", "Drop Dead", "1.0.0")]
    public class ComponentsEvent : RustPlugin
    {
        [PluginReference] private Plugin RockEvent;

        bool EventHasStart = false;
        string WorkLayer = "ComponentsEvent.Main";
        DateTime canceldate;
        private Dictionary<BasePlayer, int> PlayersDB = new Dictionary<BasePlayer, int>();
        private Dictionary<BasePlayer, bool> UI = new Dictionary<BasePlayer, bool>();
        public List<uint> LootedBoxes = new List<uint>();

        #region Config

        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Основные настройки")]
            public Settings MainSettings = new Settings();
            [JsonProperty("Настройки доступа")]
            public AccesSets AccesSettings = new AccesSets();
            [JsonProperty("Настройки очков")]
            public ScoreSets ScoreSettings = new ScoreSets();
            
            [JsonProperty("Дополнительные настройки")]
            public AdditionalSettings AddSettings = new AdditionalSettings();

            public class Settings
            {
                [JsonProperty("Включить автоматичесский старт ивента?")]
                public bool AutoStartEvent = true;
                [JsonProperty("Включить ли минимальное количество игроков для старта ивента?")]
                public bool MinPlayers = false;
                [JsonProperty("Минимальное количество игроков для старта ивента")]
                public int MinPlayersCount = 5;
                [JsonProperty("Время для начала ивента после старта сервера, перезагрузки плагина (первый раз)")]
                public float FirstStartTime = 5400f;
                [JsonProperty("Время для начала ивента в последующие разы (второй, третий и тд)")]
                public float RepeatTime = 5400f;
                [JsonProperty("Время до конца ивента (в минутах)")]
                public double EventDuration = 15.0;
            }
            public class ScoreSets
            {
                [JsonProperty("Настройки очков за бочки", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<string, int> barrelsscore = new Dictionary<string, int>
                {
                    ["loot-barrel-1"] = 20,
                    ["loot-barrel-2"] = 20,
                    ["loot_barrel_1"] = 20,
                    ["loot_barrel_2"] = 20,
                    ["oil_barrel"] = 10
                };
                
                [JsonProperty("Настройки очков за лут ящиков", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<string, int> cratescore = new Dictionary<string, int>
                {
                    ["bradley_crate"] = 10,
                    ["heli_crate"] = 10,
                    ["crate_basic"] = 20,
                    ["crate_elite"] = 20,
                    ["crate_mine"] = 10,
                    ["crate_normal"] = 20,
                    ["crate_normal_2"] = 20,
                    ["crate_normal_2_food"] = 10,
                    ["crate_normal_2_medical"] = 20,
                    ["crate_tools"] = 10,
                    ["crate_underwater_advanced"] = 20,
                    ["crate_underwater_basic"] = 10,
                    ["codelockedhackablecrate"] = 30,
                    ["codelockedhackablecrate_oilrig"] = 20,
                    ["loot-barrel-1"] = 20,
                    ["loot-barrel-2"] = 20,
                    ["loot_barrel_1"] = 20,
                    ["loot_barrel_2"] = 20,
                    ["oil_barrel"] = 20
                };

                [JsonProperty("XP за 1 место")]
                public int XpTop1 = 15;
                [JsonProperty("XP за 2 место")]
                public int XpTop2 = 10;
                [JsonProperty("XP за 3 место")]
                public int XpTop3 = 5;
            }
            public class AccesSets
            {
                [JsonProperty("Название пермишна для использования команды /componentsevent (с приставкой ComponentsEvent)")]
                public string StartPermission = "ComponentsEvent.Use";
            }
            public class AdditionalSettings
            {
                [JsonProperty("Цвет выделения текста в интерфейсе")]
                public string Color = "#CF1E1E";
            }
        }

        private void Init()
        {
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        #endregion

        #region Hooks

        void Unload()
        {
            StopEvent();
            InvokeHandler.Instance.CancelInvoke(StartEvent);
            InvokeHandler.Instance.CancelInvoke(UpdateUI);
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, WorkLayer);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(cfg.AccesSettings.StartPermission, this);
            if (cfg.MainSettings.AutoStartEvent) InvokeHandler.Instance.InvokeRepeating(StartEvent, cfg.MainSettings.FirstStartTime, cfg.MainSettings.RepeatTime);
            foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);

            PlayersDB.Clear();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsNpc) return;
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            if (EventHasStart)
            {
                if (!PlayersDB.ContainsKey(player)) PlayersDB.Add(player, 0);
                if (!UI.ContainsKey(player)) UI.Add(player, true);
                InitializeUI(player);
            }
        }

        void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.Initiator == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !EventHasStart) return;
            foreach (var barrel in cfg.ScoreSettings.barrelsscore)
            {
                if (entity.ShortPrefabName == barrel.Key)
                {
                    if (PlayersDB.ContainsKey(info.InitiatorPlayer)) PlayersDB[info.InitiatorPlayer] += barrel.Value;
                    if (!PlayersDB.ContainsKey(info.InitiatorPlayer)) PlayersDB.Add(info.InitiatorPlayer, barrel.Value);
                }
            }
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null || entity.net == null || player.IsNpc || !EventHasStart) return;
            if (entity is LootableCorpse) return;
            var container = entity as LootContainer;
            if (container == null) return;
            if (LootedBoxes.Contains(container.net.ID)) return;
            foreach (var box in cfg.ScoreSettings.cratescore)
            {
                if (entity.ShortPrefabName == box.Key)
                {
                    if (PlayersDB.ContainsKey(player)) PlayersDB[player] += box.Value;
                    if (!PlayersDB.ContainsKey(player)) PlayersDB.Add(player, box.Value);
                }
            }
            if (!LootedBoxes.Contains(container.net.ID)) LootedBoxes.Add(container.net.ID);
        }

        #endregion

        #region API

        [HookMethod("ComponentsEventIsStart")]
        public object ComponentsEventIsStart()
        {
            if (EventHasStart) return "";
            else return null;
        }

        bool RockEventIsStart()
        {
            var result = RockEvent?.Call("RockEventIsStart");
            if (result != null) return true;
            else return false;
        }

        #endregion

        #region Methods



        void StartEvent()
        {
            if (EventHasStart == true) return;
            
            if (RockEventIsStart() == true)
            {
                Puts("Невозможно начать ивент так как в данный момент запущен ивент \"Двойные камни\"");
                EventLog("Невозможно начать ивент так как в данный момент запущен ивент \"Двойные камни\"");
                return;
            }


            EventHasStart = true;
            if (cfg.MainSettings.MinPlayers == true)
            {
                if (BasePlayer.activePlayerList.Count < cfg.MainSettings.MinPlayersCount)
                {
                    EventLog("Недостаточно игроков для старта ивента \"Фарм компонентов\"");
                    Puts("Недостаточно игроков для старта ивента \"Фарм компонентов\"");
                    return;
                }
            }
            canceldate = DateTime.Now.AddMinutes(cfg.MainSettings.EventDuration);
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!PlayersDB.ContainsKey(player)) PlayersDB.Add(player, 0);
                if (!UI.ContainsKey(player)) UI.Add(player, true);

                InitializeUI(player);
                player.ChatMessage("Инвент <color=#CF1E1E>\"Фарм компонентов\"</color> успешно начался, фармите как можно больше!");
                //player.ChatMessage(Messages["StartEvent"]);
            }

            InvokeHandler.Instance.InvokeRepeating(UpdateUI, 1f, 1f);
            EventLog("Ивент \"Фарм компонентов\" успешно запущен и инициализирован!");
            Puts("Ивент \"Фарм компонентов\" успешно запущен и инициализирован!");
        }

        void StopEvent()
        {
            if (EventHasStart == false) return;
            EventHasStart = false;
            InvokeHandler.Instance.CancelInvoke(UpdateUI);
            EventLog("Ивент \"Фарм компонентов\" остановлен или закончился!");
            Puts("Ивент \"Фарм компонентов\" остановлен или закончился!");

            foreach (var player in BasePlayer.activePlayerList) 
            {
                CuiHelper.DestroyUi(player, WorkLayer);
                //player.ChatMessage($"Инвент <color=#CF1E1E>\"Фарм компонентов\"</color> успешно завершился!\n1 место занимает - {GetTop(1, "nickname")}\n2 место занимает - {GetTop(2, "nickname")}\n3 место занимает - {GetTop(3, "nickname")}");
            }
            GivePrize();

            PlayersDB.Clear();
            LootedBoxes.Clear();
        }

        void UpdateUI()
        {
            if (EventHasStart == false) return;

           // PlayersDB.OrderByDescending(key => key.Value);


            TimeSpan timetocancel = DateTime.Now - canceldate;
            if (timetocancel.ToString("mm\\:ss") == "00:00")
            {
                StopEvent();
                return;
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "time");
                var container = new CuiElementContainer();
				container.Add(new CuiElement
				{
					Parent = WorkLayer,
					Name = "time",
					FadeOut = 0.1f,
					Components =
					{
						new CuiTextComponent {  Text = $"[{timetocancel.ToString("mm\\:ss")}]", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-bold.ttf" },
						new CuiRectTransformComponent {AnchorMin = "0.4428575 0.7881494", AnchorMax = "0.5809528 0.9733346"},
						new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "0.5 0.5" }
					}
				});
                if (UI[player] == true)
                {
                    CuiHelper.DestroyUi(player, "top");
                    container.Add(new CuiElement
                    {
                        Parent = WorkLayer,
                        Name = "top",
                        Components =
                        {
                            new CuiTextComponent { Text = $"<color={cfg.AddSettings.Color}>[1]</color> {GetTop(1, "nickname")} - {GetTop(1, "score")}\n<color={cfg.AddSettings.Color}>[2]</color> {GetTop(2, "nickname")} - {GetTop(2, "score")}\n<color={cfg.AddSettings.Color}>[3]</color> {GetTop(3, "nickname")} - {GetTop(3, "score")}\nВаши очки: <color={cfg.AddSettings.Color}>{PlayersDB[player]}</color>", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                            new CuiRectTransformComponent {AnchorMin = "0.02380949 0.09259259", AnchorMax = "1 0.6481483"},
                            new CuiOutlineComponent { Color = "0 0 0 0.25", Distance = "0.5 0.5" }
                        }
                    });
                }
                CuiHelper.AddUi(player, container);
            }
        }

        string GetTop(int pos, string type)
        {
            var players = PlayersDB.OrderByDescending(key => key.Value);
            if (pos == 1)
            {
                if (PlayersDB.Count < 1) return "";
                if (players.ElementAt(0).Value == 0) return "";
                if (type == "nickname") return Substrings(players.ElementAt(0).Key.displayName);
                if (type == "score") return players.ElementAt(0).Value.ToString();
            }
            if (pos == 2)
            {
                if (PlayersDB.Count < 2) return "";
                if (players.ElementAt(1).Value == 0) return "";
                if (type == "nickname") return Substrings(players.ElementAt(1).Key.displayName);
                if (type == "score") return players.ElementAt(1).Value.ToString();
            }
            if (pos == 3)
            {
                if (PlayersDB.Count < 3) return "";
                if (players.ElementAt(2).Value == 0) return "";
                if (type == "nickname") return Substrings(players.ElementAt(2).Key.displayName);
                if (type == "score") return players.ElementAt(2).Value.ToString();
            }

            return "";
        }

        void GivePrize()
        {
            string message = "Инвент <color=#CF1E1E>\"Фарм компонентов\"</color> успешно завершился!";
            var players = PlayersDB.OrderByDescending(key => key.Value);
            if (PlayersDB.Count < 1 || players.ElementAt(0).Value == 0)
            {
                message = message + "\nНикто не участвовал в ивенте :(";
                foreach (var player in BasePlayer.activePlayerList) player.ChatMessage(message);
                return;
            }
            if (PlayersDB.Count >= 1 && players.ElementAt(0).Value != 0)
            {
                Server.Command($"XpSystemGiveXP {players.ElementAt(0).Key.userID} {cfg.ScoreSettings.XpTop1}");
                message = message + $"\nПервое место занимает <color=#CF1E1E>{players.ElementAt(0).Key.displayName}</color>, и получает {cfg.ScoreSettings.XpTop1} XP";
            }
            if (PlayersDB.Count >= 2 && players.ElementAt(1).Value != 0)
            {
                Server.Command($"XpSystemGiveXP {players.ElementAt(1).Key.userID} {cfg.ScoreSettings.XpTop2}");
                message = message + $"\nВторое место занимает <color=#CF1E1E>{players.ElementAt(1).Key.displayName}</color>, и получает {cfg.ScoreSettings.XpTop2} XP";
            }
            if (PlayersDB.Count >= 3 && players.ElementAt(2).Value != 0)
            {
                Server.Command($"XpSystemGiveXP {players.ElementAt(2).Key.userID} {cfg.ScoreSettings.XpTop3}");
                message = message + $"\nТретье место занимает <color=#CF1E1E>{players.ElementAt(2).Key.displayName}</color>, и получает {cfg.ScoreSettings.XpTop3} XP";
            }

            foreach (var player in BasePlayer.activePlayerList) player.ChatMessage(message);
        }

        string Substrings(string text)
        {
            if (text == null) return null;
            if (text.Length <= 20) return text;
            if (text.Length > 20)
            {
                var textz = text.Substring(0, 20) + "..";
                return textz;
            }
            return null;
        }

        #endregion

        #region Commands [Команды]

        [ConsoleCommand("componentsevent.start")]
        private void ConsoleForcedEventStart(ConsoleSystem.Arg args)
        {
            if (!args.IsAdmin || args.IsClientside) return;
            if (EventHasStart == true) return;

            Puts("Принудительный старт ивента \"Фарм компонентов\"");
            EventLog("CONSOLE запустил принудительный старт ивента \"Фарм компонентов\"");

            StartEvent();
        }

        [ConsoleCommand("componentsevent.stop")]
        private void ConsoleForcedEventStop(ConsoleSystem.Arg args)
        {
            if (!args.IsAdmin || args.IsClientside) return;
            if (EventHasStart == false) return;
            Puts("Принудительная остановка ивента \"Фарм компонентов\"");
            EventLog("CONSOLE принудительно остановил ивент \"Фарм компонентов\"");
            StopEvent();
        }

        [ConsoleCommand("componentsevent.ui.close")]
        void ConsoleUIClose(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;
            if (EventHasStart == false) return;

            if (UI.ContainsKey(player)) UI[player] = false;

            CuiHelper.DestroyUi(player, "text");
            CuiHelper.DestroyUi(player, "button");
            CuiHelper.DestroyUi(player, "helptext");
            CuiHelper.DestroyUi(player, "top");

            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                FadeOut = 0.1f,
                Parent = WorkLayer,
                Name = "text",
                Components =
                {
                    new CuiTextComponent { Text = ">", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.5904775 0.8807421", AnchorMax = "0.6523823 0.9770385"},
                    new CuiOutlineComponent { Color = "0 0 0 0.25", Distance = "0.5 0.5" }
                }
            });
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = "componentsevent.ui.open", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "text", "button");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("componentsevent.ui.open")]
        void ConsoleUIOpen(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;
            if (EventHasStart == false) return;

            if (UI.ContainsKey(player)) UI[player] = true;

            CuiHelper.DestroyUi(player, "text");
            CuiHelper.DestroyUi(player, "button");

            var container = new CuiElementContainer();
                container.Add(new CuiElement
                {
                    Parent = WorkLayer,
                    Name = "helptext",
                    Components =
                    {
                        new CuiTextComponent { Text = $"Фармите ящики и бочки возле дороги и получайте XP для кейсов", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                        new CuiRectTransformComponent {AnchorMin = "0.02380949 0.7074077", AnchorMax = "1 0.8851868"},
                        new CuiOutlineComponent { Color = "0 0 0 0.25", Distance = "0.5 0.5" }
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.1f,
                    Parent = WorkLayer,
                    Name = "top",
                    Components =
                    {
                        new CuiTextComponent { Text = $"<color={cfg.AddSettings.Color}>[1]</color> {GetTop(1, "nickname")} - {GetTop(1, "score")}\n<color={cfg.AddSettings.Color}>[2]</color> {GetTop(2, "nickname")} - {GetTop(2, "score")}\n<color={cfg.AddSettings.Color}>[3]</color> {GetTop(3, "nickname")} - {GetTop(3, "score")}\nВаши очки: <color={cfg.AddSettings.Color}>{PlayersDB[player]}</color>", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                        new CuiRectTransformComponent {AnchorMin = "0.02380949 0.09259259", AnchorMax = "1 0.6481483"},
                        new CuiOutlineComponent { Color = "0 0 0 0.25", Distance = "0.5 0.5" }
                    }
                });
            container.Add(new CuiElement
            {
                FadeOut = 0.1f,
                Parent = WorkLayer,
                Name = "text",
                Components =
                {
                    new CuiTextComponent { Text = "x", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.5904775 0.8807421", AnchorMax = "0.6523823 0.9770385"},
                    new CuiOutlineComponent { Color = "0 0 0 0.25", Distance = "0.5 0.5" }
                }
            });
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = "componentsevent.ui.close", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "text", "button");
            CuiHelper.AddUi(player, container);
        }

        [ChatCommand("componentsevent")]
        void ChatForcedEventHandler(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, cfg.AccesSettings.StartPermission))
                return;

            if (args.Length < 1)
            {
                player.ChatMessage(" <color=#CF1E1E>/componentsevent start</color> - запустить ивент Фарм компонентов\n <color=#CF1E1E>/componentsevent stop</color> - остановить ивент Фарм компонентов");
                return;
            }

            if (args[0] == "start")
            {
                if (EventHasStart == true)
                {
                    player.ChatMessage(" Ивент <color=#CF1E1E>Фарм компонентов</color> уже запущен");
                    return;
                }

                player.ChatMessage(" Вы успешно запустили ивент <color=#CF1E1E>Фарм компонентов</color>");
                Puts($"{player.displayName}/{player.userID} запустил принудительный старт ивента \"Фарм компонентов\"");
                EventLog($"{player.displayName}/{player.userID} запустил принудительный старт ивента \"Фарм компонентов\"");

                StartEvent();
            }
            if (args[0] == "stop")
            {
                if (EventHasStart == false)
                {
                    player.ChatMessage(" Ивент <color=#CF1E1E>Фарм компонентов</color> не был запущен");
                    return;
                }

                player.ChatMessage(" Вы успешно остановили ивент <color=#CF1E1E>Фарм компонентов</color>");
                Puts($"{player.displayName}/{player.userID} запустил принудительную остановку ивента \"Фарм компонентов\"");
                EventLog($"{player.displayName}/{player.userID} запустил принудительную остановку ивента \"Фарм компонентов\"");

                StopEvent();
            }
        }

        #endregion

        #region Helpers

        void EventLog(string text)
        {
            LogToFile("Events", text, this, true);
        }

        #endregion

        #region UI

        private void InitializeUI(BasePlayer player)
        {
            if (!PlayersDB.ContainsKey(player)) PlayersDB.Add(player, 0);
            if (!UI.ContainsKey(player)) UI.Add(player, false);

            TimeSpan timetocancel = DateTime.Now - canceldate;

            CuiHelper.DestroyUi(player, WorkLayer);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "20 -250", OffsetMax = "300 -70" },
                CursorEnabled = false,
            }, "Hud", WorkLayer);

            container.Add(new CuiElement
            {
                Parent = WorkLayer,
                Components =
                {
                    new CuiTextComponent { Text = $"<color={cfg.AddSettings.Color}>Фарм компонентов</color>", Align = TextAnchor.UpperLeft, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.02380949 0.04814815", AnchorMax = "0.4904762 0.9814821"},
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "0.5 0.5" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = WorkLayer,
                Name = "time",
				FadeOut = 0.1f,
                Components =
                {
                    new CuiTextComponent {  Text = $"[{timetocancel.ToString("mm\\:ss")}]", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.4428575 0.7881494", AnchorMax = "0.5809528 0.9733346"},
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "0.5 0.5" }
                }
            });
            
            if (UI[player] == true)
            {
                container.Add(new CuiElement
                {
                    Parent = WorkLayer,
                    Name = "helptext",
                    Components =
                    {
                        new CuiTextComponent { Text = $"Фармите ящики и бочки возле дороги и получайте XP для кейсов", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                        new CuiRectTransformComponent {AnchorMin = "0.02380949 0.7074077", AnchorMax = "1 0.8851868"},
                        new CuiOutlineComponent { Color = "0 0 0 0.25", Distance = "0.5 0.5" }
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.1f,
                    Parent = WorkLayer,
                    Name = "top",
                    Components =
                    {
                        new CuiTextComponent { Text = $"<color={cfg.AddSettings.Color}>[1]</color> {GetTop(1, "nickname")} - {GetTop(1, "score")}\n<color={cfg.AddSettings.Color}>[2]</color> {GetTop(2, "nickname")} - {GetTop(2, "score")}\n<color={cfg.AddSettings.Color}>[3]</color> {GetTop(3, "nickname")} - {GetTop(3, "score")}\nВаши очки: <color={cfg.AddSettings.Color}>{PlayersDB[player]}</color>", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                        new CuiRectTransformComponent {AnchorMin = "0.02380949 0.09259259", AnchorMax = "1 0.6481483"},
                        new CuiOutlineComponent { Color = "0 0 0 0.25", Distance = "0.5 0.5" }
                    }
                });
            }

            container.Add(new CuiElement
            {
                FadeOut = 0.1f,
                Parent = WorkLayer,
                Name = "text",
                Components =
                {
                    new CuiTextComponent { Text = "x", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.5904775 0.8807421", AnchorMax = "0.6523823 0.9770385"},
                    new CuiOutlineComponent { Color = "0 0 0 0.25", Distance = "0.5 0.5" }
                }
            });
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = "componentsevent.ui.close", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "text", "button");

            CuiHelper.AddUi(player, container);
        }

        #endregion
    }
}