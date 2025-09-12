using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("XpSystem", "Chibubrik", "1.0.0")]
    public class XpSystem : RustPlugin
    {
        #region Класс
        private Dictionary<ulong, PlayerSetting> playerSettings;
        public class PlayerSetting
        {
            [JsonProperty("Ник игрока")] public string DisplayName;
            [JsonProperty("Баланс игрока")] public float Balance;
            [JsonProperty("XP игрока")] public float Xp;
        }

        public class Settings
        {
            [JsonProperty("Xp за убийство игрока")] public float KillPlayer;
            [JsonProperty("Xp за сбитие вертолета")] public float KillHeli;
            [JsonProperty("Xp за уничтожение танка")] public float KillBradley;
            [JsonProperty("Xp за добычу ресурсов")] public float Gather;
            [JsonProperty("Сколько нужно xp для обмена на $")] public float Exchange;
            [JsonProperty("Сколько будет получать игрок $ при обмене xp")] public float Bucks;
            [JsonProperty("Стартовый баланс игрока")] public float StartBalance;
        }
        #endregion

        #region Конфиг
        public Configuration config;
        public class Configuration
        {
            [JsonProperty("Настройки")] public Settings settings = new Settings();
            public static Configuration GetNewCong()
            {
                return new Configuration
                {
                    settings = new Settings
                    {
                        KillPlayer = 20f,
                        KillHeli = 20f,
                        KillBradley = 25f,
                        Gather = 1f,
                        Exchange = 100f,
                        Bucks = 1f,
                        StartBalance = 0f
                    }
                };
            }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.settings == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("XpSystem/Player"))
            {
                playerSettings = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerSetting>>("XpSystem/Player");
            }
            else
            {
                playerSettings = new Dictionary<ulong, PlayerSetting>();
            }

            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!playerSettings.ContainsKey(player.userID))
                playerSettings.Add(player.userID, new PlayerSetting { DisplayName = player.displayName.ToUpper(), Balance = config.settings.StartBalance, Xp = 0f });
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("XpSystem/Player", playerSettings);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SaveData();
        }

        void Unload()
        {
            SaveData();
        }

        #region Добыча xp
        private void OnPlayerDie(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            BasePlayer player = null;

            if (info.InitiatorPlayer != null)
            {
                player = info.InitiatorPlayer;
                playerSettings[player.userID].Xp += config.settings.KillPlayer;
                Update(player);
            }
            return;
        }

        public ulong lastDamageName;
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BaseHelicopter && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
            if (entity is BradleyAPC && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.Initiator == null || info.InitiatorPlayer == null || info.Initiator.IsNpc || info.InitiatorPlayer.IsNpc) return;
            BasePlayer player = null;

            if (entity is BradleyAPC)
            {
                player = BasePlayer.FindByID(lastDamageName);
                if (!playerSettings.ContainsKey(player.userID)) playerSettings.Add(player.userID, new PlayerSetting { DisplayName = player.displayName.ToUpper(), Balance = config.settings.StartBalance, Xp = 0f });
                playerSettings[player.userID].Xp += config.settings.KillBradley;
                Update(player);
                return;
            }

            if (entity is BaseHelicopter)
            {
                player = BasePlayer.FindByID(lastDamageName);
                if (!playerSettings.ContainsKey(player.userID)) playerSettings.Add(player.userID, new PlayerSetting { DisplayName = player.displayName.ToUpper(), Balance = config.settings.StartBalance, Xp = 0f });
                playerSettings[player.userID].Xp += config.settings.KillHeli;
                Update(player);
                return;
            }
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (item.info.shortname == "hq.metal.ore")
            {
                return;
            }
            playerSettings[player.userID].Xp += config.settings.Gather;
            Update(player);
            return;
        }
        #endregion
        #endregion

        #region Команды
        [ConsoleCommand("balance")]
        private void ConsoleCommand(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!player.IsAdmin)
            {
                SendReply(player, "Отказано в доступе!");
                return;
            }
            if (args.Args == null || args.Args.Length < 1)
            {
                player.ConsoleMessage("Команда для выдачи баланса: balance steamid кол-во");
                return;
            }
            BasePlayer targetPlayer = BasePlayer.Find(args.Args[0]);
            if (targetPlayer == null)
            {
                player.ConsoleMessage("Игрок не найден");
                return;
            }
            int change;
            if (!int.TryParse(args.Args[1], out change))
            {
                Puts("В сумме необходимо ввести число");
                return;
            }
            player.ConsoleMessage($"Игроку {targetPlayer} был выдан баланс, в размере {change}$.");
            playerSettings[player.userID].Balance += change;
            SaveData();

        }

        [ConsoleCommand("balances")]
        private void ServerCommand(ConsoleSystem.Arg args)
        {
            if (args.Player() != null && !args.Player().IsAdmin) return;
            if (args.Args == null || args.Args.Length < 1)
            {
                Puts("Команда для выдачи баланса: balances steamid кол-во");
                return;
            }
            var targetPlayer = BasePlayer.Find(args.Args[0]);
            if (targetPlayer == null)
            {
                Puts("Игрок не найден");
                return;
            }
            int change;
            if (!int.TryParse(args.Args[1], out change))
            {
                Puts("В сумме необходимо ввести число");
                return;
            }
            Puts($"Игроку {targetPlayer} был выдан баланс, в размере {change}$");
            playerSettings[targetPlayer.userID].Balance += change;
            SaveData();
        }

        [ConsoleCommand("clear.data")]
        private void ConsoleClear(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!player.IsAdmin)
            {
                SendReply(player, "Отказано в доступе!");
                return;
            }
            playerSettings.Clear();
            player.ConsoleMessage($"Вы успешно очистили дату {Name}");
            player.SendConsoleCommand("o.reload XpSystem");
        }
        #endregion

        [ConsoleCommand("XpSystemGiveXP")]
        private void GiveXP(ConsoleSystem.Arg args)
        {
            if (args.IsClientside || !args.HasArgs(2)) return;

            var userid = ulong.Parse(args.Args[0]);
            var amount = int.Parse(args.Args[1]);

			if (playerSettings.ContainsKey(userid)) playerSettings[userid].Xp += amount;

            var player = BasePlayer.FindByID(userid);
            if (player == null) return;
            timer.Once(1f, () =>
            {
                player.ChatMessage($"<color=#CF1E1E>•</color> Информация:\nВам начислено {amount} <color=#CF1E1E>XP</color>, используйте <color=#CF1E1E>/case</color>");
            });
        }

        #region Конвектор
        private void Update(BasePlayer player)
        {
            if (playerSettings[player.userID].Xp >= config.settings.Exchange)
            {
                playerSettings[player.userID].Balance += config.settings.Bucks;
                playerSettings[player.userID].Xp -= config.settings.Exchange;
            }
            return;
        }
        #endregion

        #region Апи
        private float API_GetBalance(ulong userid)
        {
            return playerSettings[userid].Balance;
        }

        private float API_GetXp(ulong userid)
        {
            return playerSettings[userid].Xp;
        }

        private float API_SetXp()
        {
            return config.settings.Exchange;
        }

        private void API_AddBalance(ulong userid, float balance)
        {
            playerSettings[userid].Balance += balance;
            return;
        }

        private void API_ShopRemBalance(ulong userid, float balance)
        {
            if (playerSettings[userid].Balance >= balance)
            {
                playerSettings[userid].Balance -= balance;
            }
            else
            {
                playerSettings[userid].Balance = 0f;
            }
            return;
        }
        #endregion
    }
}
