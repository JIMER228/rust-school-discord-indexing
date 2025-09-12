using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using Network;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AdminCustomization", "ExpertDev", "1.0.0")]
    public class AdminCustomization : RustPlugin
    {
        private const string AdminPermission = "admincustomization.use";
        private Dictionary<ulong, CustomizationData> _playerData = new Dictionary<ulong, CustomizationData>();
        private Dictionary<ulong, PlayerInfo> _originalData = new Dictionary<ulong, PlayerInfo>();

        private readonly string[] _defaultAvatars = new string[]
        {
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/fe/fef49e7fa7e1997310d705b2a6158ff8dc1cdfeb_full.jpg",
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/22/22a4f44c8d4f2c64dd04f156eaa4da148f444a9e_full.jpg",
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/b5/b5bd56c1106e7cfa51e4a2f3d838f1eb635784a6_full.jpg",
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/d8/d8a26564c83e98f6f29c4c8afb3c36e6bab66d8c_full.jpg",
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/9b/9b9779c79d389a6ce0689fb7d4b52a7dd7aa4673_full.jpg",
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/c5/c5d56249ee5d28a07db4ac11d1d12c86bdb9a603_full.jpg",
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/86/86a0b2d5bf9a9d11fd56f48665d9af0c9d787b8b_full.jpg",
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/f1/f1dd60a188883caf82d0cbfccfe6aba0af1732d4_full.jpg",
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/5c/5c8c0e0256811d60bb5c62e35beb6c5969e75356_full.jpg",
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/17/17f40b0c9fd2d5e76f646dd79a46da8a4ca11f48_full.jpg"
        };

        private readonly string[] _rustNicknames = new string[]
        {
            "rust.player", "RUST_PLAYER", "[RU]Player", "ClanPlayer", "ProRaider",
            "Raider", "PVPGod", "Farmer", "Builder", "Survivor",
            "RustLord", "BaseBuilder", "Zerg", "Solo", "Chad",
            "Grub", "Warrior", "Hunter", "Bandit", "Scientist",
            "RoamingPlayer", "Researcher", "Scrapper", "Looter", "Recycler",
            "Mechanic", "Pilot", "Sniper", "Camper", "Defender",
            "Attacker", "Rust.gg", "RustDB", "Gamer", "Pro",
            "Noob", "Veteran", "Legend", "Ghost", "Shadow",
            "Ninja", "Samurai", "Viking", "Berserker", "Warlord",
            "King", "Queen", "Lord", "Knight", "Peasant",
            "Scavenger", "Scout", "Spy", "Agent", "Mercenary",
            "Soldier", "Commander", "General", "Recruit", "Elite",
            "Specialist", "Expert", "Master", "Champion", "Victor",
            "Winner", "Leader", "Boss", "Chief", "Captain",
            "Sergeant", "Warrior", "Fighter", "Brawler", "Boxer",
            "Gunner", "Shooter", "Marksman", "Archer", "Bowman",
            "Swordsman", "Axeman", "Spearman", "Lancer", "Rider",
            "Runner", "Sprinter", "Climber", "Jumper", "Swimmer",
            "Diver", "Fisher", "Angler", "Hunter", "Trapper",
            "Gatherer", "Collector", "Hoarder", "Trader", "Merchant",
            "Vendor", "Seller", "Buyer", "Dealer", "Broker",
            "Agent", "Operator", "Controller", "Manager", "Director",
            "Producer", "Creator", "Maker", "Builder", "Designer"
        };

        private readonly string[] _clanTags = new string[]
        {
            "[RU]", "[EU]", "[NA]", "[CN]", "[BR]", "[TR]", 
            "[FR]", "[DE]", "[UK]", "[PL]", "[ESP]", "[ITA]",
            "Team", "Clan", "Squad", "Guild", "Group", "Band",
            "{", "[", "(", "<", "™", "●", "★"
        };

        private readonly string[] _nameSuffixes = new string[]
        {
            "Pro", "Noob", "Gaming", "YT", "TV", "Live", "TTV",
            "Official", "Real", "Original", "Best", "Top", "Elite",
            "_", ".", "-", "1337", "420", "69", "777", "999",
            "2024", "2025"
        };

        private class CustomizationData
        {
            public string CustomName { get; set; }
            public string AvatarUrl { get; set; }
            public bool IsDisguised { get; set; }
            public string DisguiseAvatar { get; set; }
        }

        private class PlayerInfo
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public ulong SteamId { get; set; }
            public bool IsAdmin { get; set; }
            public string AvatarUrl { get; set; }
        }

        private string GenerateRealisticNickname()
        {
            string nickname = "";
            int style = UnityEngine.Random.Range(0, 5);

            switch (style)
            {
                case 0:
                    nickname = _rustNicknames[UnityEngine.Random.Range(0, _rustNicknames.Length)];
                    if (UnityEngine.Random.value > 0.5f)
                        nickname += UnityEngine.Random.Range(1, 999).ToString();
                    break;
                case 1:
                    nickname = _clanTags[UnityEngine.Random.Range(0, _clanTags.Length)] + 
                             _rustNicknames[UnityEngine.Random.Range(0, _rustNicknames.Length)];
                    break;
                case 2:
                    nickname = _rustNicknames[UnityEngine.Random.Range(0, _rustNicknames.Length)] +
                             _nameSuffixes[UnityEngine.Random.Range(0, _nameSuffixes.Length)];
                    break;
                case 3:
                    nickname = _rustNicknames[UnityEngine.Random.Range(0, _rustNicknames.Length)] +
                             UnityEngine.Random.Range(1, 99).ToString();
                    break;
                case 4:
                    if (UnityEngine.Random.value > 0.5f)
                        nickname = _clanTags[UnityEngine.Random.Range(0, _clanTags.Length)];
                    nickname += _rustNicknames[UnityEngine.Random.Range(0, _rustNicknames.Length)] +
                              _nameSuffixes[UnityEngine.Random.Range(0, _nameSuffixes.Length)] +
                              UnityEngine.Random.Range(10, 99).ToString();
                    break;
            }

            return nickname;
        }

        void Init()
        {
            permission.RegisterPermission(AdminPermission, this);
            LoadData();
        }

        void OnServerSave() => SaveData();

        void Unload() => SaveData();

        void LoadData()
        {
            _playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, CustomizationData>>("AdminCustomization") 
                         ?? new Dictionary<ulong, CustomizationData>();
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("AdminCustomization", _playerData);
        }

        [ChatCommand("setname")]
        void SetNameCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                player.ChatMessage("У вас нет прав на использование этой команды!");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /setname <новое_имя>");
                return;
            }

            string newName = string.Join(" ", args);
            if (newName.Length > 32)
            {
                player.ChatMessage("Имя слишком длинное! Максимальная длина: 32 символа");
                return;
            }

            if (!_playerData.ContainsKey(player.userID))
                _playerData[player.userID] = new CustomizationData();

            _playerData[player.userID].CustomName = newName;
            player.displayName = newName;
            player.ChatMessage($"Ваше имя изменено на: {newName}");
            SaveData();
            player.SendNetworkUpdate();
        }

        [ChatCommand("setavatar")]
        void SetAvatarCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                player.ChatMessage("У вас нет прав на использование этой команды!");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /setavatar <url>");
                return;
            }

            string avatarUrl = args[0];
            if (!Uri.IsWellFormedUriString(avatarUrl, UriKind.Absolute))
            {
                player.ChatMessage("Неверный формат URL!");
                return;
            }

            if (!_playerData.ContainsKey(player.userID))
                _playerData[player.userID] = new CustomizationData();

            _playerData[player.userID].AvatarUrl = avatarUrl;
            player.ChatMessage("Ваш аватар обновлен!");
            SaveData();
        }

        [ChatCommand("disguise")]
        void DisguiseCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                player.ChatMessage("У вас нет прав на использование этой команды!");
                return;
            }

            if (!_playerData.ContainsKey(player.userID))
                _playerData[player.userID] = new CustomizationData();

            var data = _playerData[player.userID];

            if (!data.IsDisguised)
            {
                if (args.Length == 0)
                {
                    player.ChatMessage("Использование: /disguise <ссылка_на_аватар>");
                    return;
                }

                string avatarUrl = args[0];
                if (!Uri.IsWellFormedUriString(avatarUrl, UriKind.Absolute))
                {
                    player.ChatMessage("Неверный формат URL!");
                    return;
                }

                _originalData[player.userID] = new PlayerInfo
                {
                    Name = player.displayName,
                    DisplayName = player.displayName,
                    SteamId = player.userID,
                    IsAdmin = permission.UserHasPermission(player.UserIDString, "admin"),
                    AvatarUrl = data.AvatarUrl
                };

                string disguiseName = GenerateRealisticNickname();
                
                player.displayName = disguiseName;
                permission.RevokeUserPermission(player.UserIDString, "admin");
                data.IsDisguised = true;
                data.DisguiseAvatar = avatarUrl;
                
                var avatarCommand = $"userconfig {player.UserIDString} avatar {avatarUrl}";
                Puts($"Устанавливаем аватар через команду: {avatarCommand}");
                rust.RunServerCommand(avatarCommand);
                
                player.ChatMessage($"Вы замаскированы под обычного игрока с именем: {disguiseName}");
                player.ChatMessage($"Установлен аватар: {avatarUrl}");
                player.SendNetworkUpdate();
            }
            else
            {
                if (_originalData.ContainsKey(player.userID))
                {
                    var originalInfo = _originalData[player.userID];
                    player.displayName = originalInfo.DisplayName;
                    if (originalInfo.IsAdmin)
                    {
                        permission.GrantUserPermission(player.UserIDString, "admin", this);
                    }
                    
                    if (!string.IsNullOrEmpty(originalInfo.AvatarUrl))
                    {
                        rust.RunServerCommand($"userconfig {player.UserIDString} avatar {originalInfo.AvatarUrl}");
                    }
                    
                    _originalData.Remove(player.userID);
                }
                data.IsDisguised = false;
                data.DisguiseAvatar = null;
                player.ChatMessage("Маскировка снята!");
                player.SendNetworkUpdate();
            }
            SaveData();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || player.userID == 0 || string.IsNullOrEmpty(player.UserIDString))
                return;

            if (_playerData.ContainsKey(player.userID))
            {
                var data = _playerData[player.userID];
                if (data.IsDisguised)
                {
                    if (_originalData.ContainsKey(player.userID))
                    {
                        permission.RevokeUserPermission(player.UserIDString, "admin");
                        string disguiseName = GenerateRealisticNickname();
                        string disguiseAvatar = _defaultAvatars[UnityEngine.Random.Range(0, _defaultAvatars.Length)];
                        
                        if (!string.IsNullOrEmpty(disguiseName))
                            player.displayName = disguiseName;
                            
                        data.DisguiseAvatar = disguiseAvatar;
                        
                        if (!string.IsNullOrEmpty(disguiseAvatar))
                            rust.RunServerCommand($"userconfig {player.UserIDString} avatar {disguiseAvatar}");
                        
                        player.SendNetworkUpdate();
                    }
                }
                else if (!string.IsNullOrEmpty(data.CustomName))
                {
                    player.displayName = data.CustomName;
                    if (!string.IsNullOrEmpty(data.AvatarUrl))
                    {
                        rust.RunServerCommand($"userconfig {player.UserIDString} avatar {data.AvatarUrl}");
                    }
                    player.SendNetworkUpdate();
                }
            }
        }

        [ConsoleCommand("resetcustomization")]
        void ResetCustomizationCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                var player = arg.Player();
                if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
                {
                    player.ChatMessage("У вас нет прав на использование этой команды!");
                    return;
                }

                if (_playerData.ContainsKey(player.userID))
                {
                    if (_originalData.ContainsKey(player.userID))
                    {
                        var originalInfo = _originalData[player.userID];
                        player.displayName = originalInfo.DisplayName;
                        if (originalInfo.IsAdmin)
                        {
                            permission.GrantUserPermission(player.UserIDString, "admin", this);
                        }
                        
                        if (!string.IsNullOrEmpty(originalInfo.AvatarUrl))
                        {
                            rust.RunServerCommand($"userconfig {player.UserIDString} avatar {originalInfo.AvatarUrl}");
                        }
                        
                        _originalData.Remove(player.userID);
                    }
                    _playerData.Remove(player.userID);
                    player.SendNetworkUpdate();
                    player.ChatMessage("Ваши настройки кастомизации сброшены!");
                    SaveData();
                }
            }
        }
    }
}
