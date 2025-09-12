using System;
using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;
using Facepunch.Extend;
using UnityEngine;
using System.Text.RegularExpressions;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("WeaponDamageScale", "Drop Dead & FourTeen", "1.0.0")]
    public class WeaponDamageScale : RustPlugin
    {
        [PluginReference] private Plugin HealthTick, BetterHealth;
        public Dictionary<ulong, bool> PlayerData = new Dictionary<ulong, bool>();
        public Dictionary<ulong, float> PlayerLevelData = new Dictionary<ulong, float>();
        public Dictionary<ulong, string> SpawnMessage = new Dictionary<ulong, string>();

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/PlayerData", PlayerData);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/PlayerLevelData", PlayerLevelData);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/SpawnMessage", SpawnMessage);
        }

        private void LoadData()
        {
            try
            {
                PlayerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, bool>>($"{Title}/PlayerData");
                SpawnMessage = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>($"{Title}/SpawnMessage");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }
            if (PlayerData == null) PlayerData = new Dictionary<ulong, bool>();
            try
            {
                PlayerLevelData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>($"{Title}/PlayerLevelData");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }
            if (PlayerLevelData == null) PlayerLevelData = new Dictionary<ulong, float>();
        }

        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Настройки урона")]
            public Dictionary<string, float> damage = new Dictionary<string, float>()
            {
                ["WeaponDamageScale.x2"] = 2f,
                ["WeaponDamageScale.x3"] = 3f,
                ["WeaponDamageScale.x5"] = 5f,
            };
        }

        private void Init()
        {
            LoadConfig();
            LoadData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg, true);
        }

        void OnServerInitialized()
        {
            if (!permission.PermissionExists("WeaponDamageScale.allow"))
                permission.RegisterPermission("WeaponDamageScale.allow", this);

            foreach (var perm in cfg.damage)
            {
                if (!permission.PermissionExists(perm.Key))
                    permission.RegisterPermission(perm.Key, this);
            }
            foreach (var num in PlayerLevelData)
            {
                string perm1 = "no";
                foreach (var perm in cfg.damage)
                {
                    if (perm.Value == num.Value)
                    {
                        perm1 = perm.Key;
                        break;
                    }
                }
                if (permission.UserHasPermission(num.Key.ToString(), perm1) == false || perm1 == "no") PlayerLevelData.Remove(num.Key);
            }
            foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);
        }

        bool HasAccess(ulong playerid) => permission.UserHasPermission(playerid.ToString(), "WeaponDamageScale.allow");

        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            if (!PlayerData.ContainsKey(player.userID.Get()))
                PlayerData.Add(player.userID.Get(), true);
        }

        private void Unload()
        {
            SaveData();
        }

        void OnServerSave()
        {
            SaveData();
        }
        [ConsoleCommand("wpdamagei")]
        void Commandwpdamagei(ConsoleSystem.Arg args)
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
            float maxDamage = 0.0f;
            string maxPerm = null;

            foreach (var perm in cfg.damage)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Key) && perm.Value > maxDamage)
                {
                    maxDamage = perm.Value;
                    maxPerm = perm.Key;
                }
            }

            if (maxPerm != null)
            {
                string nextPerm = null;
                bool foundCurrent = false;

                foreach (var perm in cfg.damage)
                {
                    if (foundCurrent)
                    {
                        if (maxDamage == 2) { nextPerm = "WeaponDamageScale.x3"; break; }
                        if (maxDamage == 3) { nextPerm = "WeaponDamageScale.x4"; break; }
                        if (maxDamage == 4) { nextPerm = "WeaponDamageScale.x5"; break; }
                        if (maxDamage == 5) { nextPerm = "WeaponDamageScale.x6"; break; }
                        if (maxDamage >= 100)
                        {
                            player.ChatMessage($"Приобрести более <color=#FF8000>х100 урона</color> через магазин невозможно. Пожалуйста, свяжитесь с <color=#674ea7>создателем</color> лично\n[DS = <color=#674ea7>maicrof</color>, VK = <color=#674ea7>https://vk.com/maic_rof</color>]");
                            PrintWarning($"{player} Попытка купить больше х100 урона.");
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
                        player.ChatMessage($"Успешно получен х{number} урон");
                    }
                }
                else
                {
                    PrintError("Не удалось найти следующий уровень разрешения.");
                }
            }
            else
            {
                player.ChatMessage($"Успешно получен х2 урон");
                rust.RunServerCommand($"grantperm {player.UserIDString} WeaponDamageScale.allow 999d");
                rust.RunServerCommand($"grantperm {player.UserIDString} WeaponDamageScale.x2 999d");
            }
        }
        [ChatCommand("damage")]
        void DamageCMD(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (!HasAccess(player.userID))
            {
                player.ChatMessage("У вас нет доступа к этой команде");
                return;
            }

            if (!PlayerData.ContainsKey(player.userID))
                PlayerData.Add(player.userID, true);

            if (args.Length < 1)
            {
                player.ChatMessage("/damage <кол-во>");
                return;
            }
            if (args.Length >= 1)
            {
                float requestedDamage = args[0].ToFloat();
                if (requestedDamage < 1)
                {
                    player.ChatMessage("Ошибка: Меньше 1 нельзя!");
                    return;
                }
                float maxDamage = 0.0f;
                string maxPerm = null;

                foreach (var perm in cfg.damage)
                {
                    if (permission.UserHasPermission(player.UserIDString, perm.Key))
                    {
                        if (perm.Value > maxDamage)
                        {
                            maxDamage = perm.Value;
                            maxPerm = perm.Key;
                        }
                    }
                }

                timer.Once(0.5f, () =>
                {
                    if (requestedDamage <= maxDamage)
                    {
                        if (PlayerLevelData.ContainsKey(player.userID))
                        {
                            PlayerLevelData[player.userID] = requestedDamage;
                        }
                        else
                        {
                            PlayerLevelData.Add(player.userID, requestedDamage);
                        }
                        player.ChatMessage($"Успешно: {requestedDamage}/{maxDamage}");
                    }
                    else
                    {
                        player.ChatMessage($"Ошибка: {requestedDamage}x превышает максимальное значение {maxDamage}x");
                    }
                });
            }
        }
        public List<string> Perms { get; set; } = new List<string>()
        {
            "sponsor001", "sponsor002", "sponsor003", "sponsor004", "sponsor005",
            "sponsor006", "sponsor007", "sponsor008", "sponsor009", "sponsor010",
            "sponsor011", "sponsor012", "sponsor013", "sponsor014", "sponsor015",
            "sponsor016", "sponsor017", "sponsor018", "sponsor019", "sponsor020",
            "sponsor021", "sponsor022", "sponsor023", "sponsor024", "sponsor025",
            "sponsor026", "sponsor027", "sponsor028", "sponsor029", "sponsor030",
            "sponsor031", "sponsor032", "sponsor033", "sponsor034", "sponsor035",
            "sponsor036", "sponsor037", "sponsor038", "sponsor039", "sponsor040",
            "sponsor041", "sponsor042", "sponsor043", "sponsor044", "sponsor045",
            "sponsor046", "sponsor047", "sponsor048", "sponsor049", "sponsor050"
        };

        public string GetCurrentSponsor(string playeruserid)
        {
            string currentPerm = null;
            int currentNum = 0;

            foreach (var group in permission.GetGroups())
            {
                if (permission.UserHasGroup(playeruserid, group) && Perms.Contains(group))
                {
                    int num;
                    if (int.TryParse(Regex.Match(group, @"\d+").Value, out num))
                    {
                        if (num > currentNum)
                        {
                            currentNum = num;
                            currentPerm = group;
                        }
                    }
                }
            }
            if (currentPerm != null) { return currentPerm; }
            return string.Empty;
        }
        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info == null || player == null) return;
            var iniatior = info.InitiatorPlayer;
            if (iniatior != null)
            {
                if (player.IsBot || iniatior.IsBot) return;
                if (iniatior.userID.Get() == player.userID.Get()) return;
                int rhp = 0;
                float hp = 100;
                string sponsor = GetCurrentSponsor(iniatior.UserIDString);
                if (HealthTick) { rhp = (int)HealthTick.Call("GetMaxRegenForPlayer", iniatior); }
                if (BetterHealth) { hp = (float)BetterHealth.Call("GetMaxHealth", iniatior); if (hp == 0) hp = 100; }
                if (GetDamageScale(iniatior.userID.Get()) == 0 && hp == 100 && rhp == 0) return;
                float damage = 1;
                if (GetDamageScale(iniatior.userID.Get()) != 0) damage = GetDamageScale(iniatior.userID.Get());
                string text = $"<color=#ad0c1c>[ВНИМАНИЕ]</color> <color=#6A5ACD>-</color> Вас убил игрок <color=#FF9740>{iniatior.displayName}</color> с привилегиями <color=#FF9740>х{damage}</color> урон, <color=#64FF33>{hp} хп</color> и <color=#64FF33>{rhp} регенерации</color>." +
                        $" \n\n<color=#ad0c1c>[ВНИМАНИЕ]</color> <color=#6A5ACD>-</color> Для покупки таких привилегий зайдите на сайт <color=#6A5ACD>-</color> <color=#6b53f5>rust-crom.ru</color> или пишите создателю discord <color=#6b53f5>maicrof</color>";
                if (sponsor != string.Empty)
                {
                    text = $"<color=#ad0c1c>[ВНИМАНИЕ]</color> <color=#6A5ACD>-</color> Вас убил игрок <color=#FF9740>{iniatior.displayName}</color> с привилегиями <color=#FF9740>х{damage}</color> урон, <color=#64FF33>{hp} хп</color> и <color=#64FF33>{rhp} регенерации</color>. <color=#6b53f5>{sponsor}</color>." +
                        $" \n\n<color=#ad0c1c>[ВНИМАНИЕ]</color> <color=#6A5ACD>-</color> Для покупки таких привилегий зайдите на сайт <color=#6A5ACD>-</color> <color=#6b53f5>rust-crom.ru</color> или пишите создателю discord <color=#6b53f5>maicrof</color>";
                }
                if (!SpawnMessage.ContainsKey(player.userID.Get())) SpawnMessage.Add(player.userID.Get(), text);
                SaveData();
            }
            return;
        }
        void ChangeLvl(BasePlayer player)
        {
            PlayerLevelData[player.userID] = GetDamageScale(player.userID.Get(), true);
            BetterHealth.CallHook("ChangeLvl", player);
        }
        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player != null && player.IsConnected)
            {
                if (SpawnMessage.ContainsKey(player.userID.Get()))
                {
                    string text;
                    if (SpawnMessage.TryGetValue(player.userID.Get(), out text))
                    {
                        if (text != null)
                        {
                            player.ChatMessage(text);
                        }
                    }
                    SpawnMessage.Remove(player.userID.Get());
                    SaveData();
                }
            }
        }
        float GetPlayerLvlData(ulong userid)
        {
            if (PlayerLevelData.ContainsKey(userid))
            {
                float damage = GetDamageScale(userid, true);
                if ((float)PlayerLevelData[userid] > damage)
                {
                    PlayerLevelData[userid] = damage;
                    return damage; 
                }
                return (float)PlayerLevelData[userid];
            }
            return 0;
        }
        float GetDamageScale(ulong UserID, bool b = false)
        {
            float size = 0;
            if (!HasAccess(UserID)) return 0;
            if (!b)
            {
                if (GetPlayerLvlData(UserID) != 0) return GetPlayerLvlData(UserID);
            }
            else
            {
                foreach (var num in cfg.damage)
                {
                    if (permission.UserHasPermission(UserID.ToString(), num.Key))
                        if (num.Value > size) size = num.Value;
                }
            }
            return size;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            var attacker = hitInfo?.Initiator as BasePlayer;
            if (entity == null || hitInfo == null || attacker == null) return;
            var type = hitInfo?.damageTypes?.GetMajorityDamageType() ?? Rust.DamageType.Generic;
            if (type == Rust.DamageType.Suicide) return;
            if (attacker != null)
            {
                if (!HasAccess(attacker.userID.Get())) return;
                var damage = GetDamageScale(attacker.userID.Get());
                if (PlayerLevelData.ContainsKey(attacker.userID.Get()))
                {
                    hitInfo?.damageTypes?.ScaleAll(PlayerLevelData[attacker.userID.Get()]);
                    return;
                }
                if (!PlayerData.ContainsKey(attacker.userID.Get()))
                    PlayerData.Add(attacker.userID.Get(), true);

                if (PlayerData[attacker.userID.Get()] && damage != 0f)
                {
                    hitInfo?.damageTypes?.ScaleAll(damage);
                }
            }
        }
    }
}