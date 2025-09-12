using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HealthTick", "FourTeen", "1.0.9")]
    [Description("HealthTick")]
    public class HealthTick : RustPlugin
    {
        private List<ulong> off = new List<ulong>();
        string permuse = "healthtick.use";
        private Dictionary<ulong, int> playerRegenData = new Dictionary<ulong, int>();

        void OnServerInitialized()
        {
            LoadConfig();
            LoadData();
            RegisterPermissions();
            if (cfg.mdregen == true)
            {
                timer.Every(cfg.Секунды, () => HTick());
                Unsubscribe(nameof(OnTick));
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg, true);
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(permuse, this);
            foreach (var kvp in cfg.regen)
            {
                permission.RegisterPermission(kvp.Key, this);
            }
        }
        object xl96plHook(string key, BasePlayer player)
        {
            if (key == "off")
            {
                off.Add(player.userID.Get());
            }
            if (key == "on")
            {
                off.Remove(player.userID.Get());
            }
            return null;
        }
        [ChatCommand("regen")]
        void regenCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permuse))
            {
                player.ChatMessage("Нет прав!");
                return;
            }
            if (args.Length < 1)
            {
                int maxRegen = GetMaxRegenForPlayer(player);
                player.ChatMessage(off.Contains(player.userID) ? "У вас выключена регенерация!" : "У вас включена регенерация!");
                player.ChatMessage($"Некорректное значение регенерации. \nИспользуйте /regen set (от 1 до {maxRegen}). \nИли используйте /regen (on - off).");
                return;
            }
            switch (args[0].ToLower())
            {
                case "off":
                    if (off.Contains(player.userID))
                    {
                        player.ChatMessage("У вас уже выключена регенерация!");
                        return;
                    }
                    off.Add(player.userID);
                    player.ChatMessage("Вы успешно выключили регенерацию");
                    SaveData();
                    break;
                case "on":
                    if (!off.Contains(player.userID))
                    {
                        player.ChatMessage("У вас уже включена регенерация!");
                        return;
                    }
                    off.Remove(player.userID);
                    player.ChatMessage("Вы успешно включили регенерацию");
                    SaveData();
                    break;
                case "set":
                    if (args.Length == 2 && int.TryParse(args[1], out int regenAmount) && regenAmount > 0)
                    {
                        int maxRegen1 = GetMaxRegenForPlayer(player);
                        if (regenAmount > maxRegen1)
                        {
                            player.ChatMessage($"Вы не можете установить значение регенерации более {maxRegen1}. Используйте меньшее значение.");
                            return;
                        }

                        playerRegenData[player.userID] = regenAmount;
                        player.ChatMessage($"Вы установили регенерацию в {regenAmount} единиц(у).");
                        SaveData();
                    }
                    else
                    {
                        int maxRegen2 = GetMaxRegenForPlayer(player);
                        player.ChatMessage($"Некорректное значение регенерации. \nИспользуйте /regen set (от 1 до {maxRegen2}). \nИли используйте /regen (on - off).");
                    }
                    break;
                default:
                    int maxRegen = GetMaxRegenForPlayer(player);
                    player.ChatMessage(off.Contains(player.userID) ? "У вас выключена регенерация!" : "У вас включена регенерация!");
                    player.ChatMessage($"Некорректное значение регенерации. \nИспользуйте /regen set (от 1 до {maxRegen}). \nИли используйте /regen (on - off).");
                    break;
            }
        }

        int GetMaxRegenForPlayer(BasePlayer player)
        {
            int maxRegen = 0;
            foreach (var kvp in cfg.regen)
            {
                string perm = kvp.Key;
                int regenAmount = (int)kvp.Value;

                if (permission.UserHasPermission(player.UserIDString, perm) && regenAmount > maxRegen)
                {
                    maxRegen = regenAmount;
                }
                int cregen = 0;
                if (playerRegenData.TryGetValue(player.userID.Get(), out cregen))
                {
                    if (maxRegen < cregen)
                    {
                        playerRegenData.Remove(player.userID.Get());
                    }
                }
            }

            return maxRegen;
        }
        private int GetCurrentRegenPlayer(BasePlayer player)
        {
            if (playerRegenData.ContainsKey(player.userID.Get()))
            {
                return playerRegenData[player.userID.Get()];
            }
            return GetMaxRegenForPlayer(player);
        }
        [ConsoleCommand("hticki")]
        void Commandhticki(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
            {
                return;
            }

            BasePlayer player = BasePlayer.FindByID(ulong.Parse(args.Args[0]));

            if (player == null)
            {
                PrintError($"Игрок не найден!");
                return;
            }

            float maxRegen = 0.0f;
            string maxPerm = null;

            foreach (var perm in cfg.regen)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Key) && perm.Value > maxRegen)
                {
                    maxRegen = perm.Value;
                    maxPerm = perm.Key;
                }
            }

            if (maxPerm != null)
            {
                string nextPerm = null;
                bool foundCurrent = false;

                foreach (var perm in cfg.regen)
                {
                    if (foundCurrent)
                    {
                        if (maxRegen >= 100)
                        {
                            player.ChatMessage($"Приобрести более <color=#FF8000>100 единиц регенерации</color> через магазин невозможно. Пожалуйста, свяжитесь с <color=#674ea7>создателем</color> лично\n[DS = <color=#674ea7>maicrof</color>, VK = <color=#674ea7>https://vk.com/maic_rof</color>]");
                            PrintWarning($"{player} Попытка купить больше 100 единиц регенерации.");
                            return;
                        }
                        nextPerm = perm.Key;
                        break;
                    }

                    if (perm.Key == maxPerm)
                    {
                        foundCurrent = true;
                    }
                }

                if (nextPerm != null)
                {
                    rust.RunServerCommand($"grantperm {player.UserIDString} {nextPerm} 999d");
                    Match match = Regex.Match(nextPerm, @"\d+");
                    if (int.TryParse(match.Value, out int number))
                    {
                        player.ChatMessage($"Успешно получен(а) {number} единиц(а) регенерации");
                    }
                }
                else
                {
                    PrintError("Не удалось найти следующий уровень разрешения.");
                }
            }
            else
            {
                player.ChatMessage($"Успешно получен(a) 1 единиц(а) регенерации");
                rust.RunServerCommand($"grantperm {player.UserIDString} HealthTick.1 999d");
            }
        }

        private PluginConfig cfg;
        public class PluginConfig
        {
            [JsonProperty("Настраиваемый режим")]
            public bool mdregen = false;
            public float Секунды = 1;

            [JsonProperty("Настройки регена")]
            public Dictionary<string, float> regen = new Dictionary<string, float>()
            {
                ["HealthTick.1"] = 1,
                ["HealthTick.2"] = 2,
                ["HealthTick.3"] = 3,
                ["HealthTick.4"] = 4,
                ["HealthTick.5"] = 5,
                ["HealthTick.6"] = 6,
                ["HealthTick.7"] = 7,
                ["HealthTick.8"] = 8,
                ["HealthTick.9"] = 9
            };
        }

        void OnTick()
        {
            if (cfg.mdregen == false)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null) continue;
                    if (player.health >= player.MaxHealth()) continue;
                    if (off.Contains(player.userID.Get())) continue;
                    int regen = 0;
                    regen = GetCurrentRegenPlayer(player);
                    if (regen != 0) player.Heal(regen);
                }
            }
        }

        void HTick()
        {
            if (cfg.mdregen == true)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null) continue;
                    if (player.health >= player.MaxHealth()) continue;
                    if (off.Contains(player.userID.Get())) continue;
                    int regen = 0;
                    regen = GetCurrentRegenPlayer(player);
                    if (regen != 0) player.Heal(regen);
                }
            }
        }

        private void LoadData()
        {
            playerRegenData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>($"{Title}/PlayerRegenData");
            off = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>($"{Title}/off");
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/PlayerRegenData", playerRegenData);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/off", off);
        }
    }
}