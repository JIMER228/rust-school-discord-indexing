using Facepunch.Extend;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SponsorChanger", "FourTeen", "1.0.1")]
    public class SponsorChanger : RustPlugin
    {
        [PluginReference] private Plugin WeaponDamageScale;
        public List<string> _SponsorGroups { get; set; } = new List<string>()
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
        private Dictionary<ulong, string> _SponsorsList = new Dictionary<ulong, string>();
        private Dictionary<ulong, string> _SponsorList = new Dictionary<ulong, string>();
        void OnServerInitialized()
        {
            LoadData();
        }
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (_SponsorsList.ContainsKey(player.userID.Get()) && _SponsorList.ContainsKey(player.userID.Get()))
                {
                    rust.RunServerCommand($"removegroup {player.UserIDString} sponsor{_SponsorList[player.userID.Get()]}");
                    rust.RunServerCommand($"addgroup {player.UserIDString} sponsor{_SponsorsList[player.userID.Get()]} 999d");
                    WeaponDamageScale?.CallHook("ChangeLvl", player);
                    _SponsorsList.Remove(player.userID.Get());
                    _SponsorList.Remove(player.userID.Get());
                }
            }
            SaveData();
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_SponsorsList.ContainsKey(player.userID.Get()) && _SponsorList.ContainsKey(player.userID.Get()))
            {
                rust.RunServerCommand($"removegroup {player.UserIDString} sponsor{_SponsorList[player.userID.Get()]}");
                rust.RunServerCommand($"addgroup {player.UserIDString} sponsor{_SponsorsList[player.userID.Get()]} 999d");
                WeaponDamageScale.CallHook("ChangeLvl", player);
                _SponsorsList.Remove(player.userID.Get());
                _SponsorList.Remove(player.userID.Get());
                SaveData();
            }
        }
        [ChatCommand("sp")]
        private void CmdAimAssist(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args.Length < 1)
            {
                player.ChatMessage("/sp <уровень>");
                return;
            }
            else
            {
                if (!int.TryParse(args[0], out int requestedLevel))
                {
                    player.ChatMessage("Укажите действительный уровень спонсора.");
                    return;
                }
                if (_SponsorsList.ContainsKey(player.userID.Get()))
                {
                    int ctNum = _SponsorsList[player.userID.Get()].ToInt();
                    if (((int)args[0].ToFloat()) < ctNum)
                    {
                        rust.RunServerCommand($"removegroup {player.UserIDString} sponsor{_SponsorsList[player.userID.Get()]}");
                        if (_SponsorList.ContainsKey(player.userID.Get())) rust.RunServerCommand($"removegroup {player.UserIDString} sponsor{_SponsorList[player.userID.Get()]}");
                        rust.RunServerCommand($"addgroup {player.UserIDString} sponsor{(int)args[0].ToFloat():D3} 999d");
                        player.ChatMessage($"Успешно получен спонсор уровня {(int)args[0].ToFloat():D2}/{ctNum.ToString("D2")}");
                        _SponsorList[player.userID.Get()] = requestedLevel.ToString("D3");
                    }
                    else if ((int)args[0].ToFloat() == ctNum && _SponsorList.ContainsKey(player.userID))
                    {
                        rust.RunServerCommand($"removegroup {player.UserIDString} sponsor{_SponsorList[player.userID.Get()]}");
                        rust.RunServerCommand($"addgroup {player.UserIDString} sponsor{(int)args[0].ToFloat():D3} 999d");
                        player.ChatMessage($"Успешно получен спонсор уровня {(int)args[0].ToFloat():D2}/{ctNum.ToString("D2")}");
                        _SponsorList[player.userID.Get()] = requestedLevel.ToString("D3");
                    }
                    else
                    {
                        player.ChatMessage($"{(int)args[0].ToFloat():D2} превышает максимальный уровень вашего спонсора ({ctNum.ToString("D2")})");
                    }
                    SaveData();
                    WeaponDamageScale.CallHook("ChangeLvl", player);
                    return;
                }
                bool hass = false;
                foreach (string sponsors in _SponsorGroups)
                {
                    if (permission.GroupExists(sponsors) && permission.UserHasGroup(player.UserIDString, sponsors))
                    {
                        hass = true; break;
                    }
                }
                if (hass)
                {
                    string currentPerm = null;
                    int currentNum = 0;

                    foreach (var group in permission.GetGroups())
                    {
                        if (permission.UserHasGroup(player.UserIDString, group) && _SponsorGroups.Contains(group))
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
                    if (_SponsorGroups.Contains(currentPerm))
                    {
                        if (((int)args[0].ToFloat()) < currentNum)
                        {
                            _SponsorsList.Add(player.userID.Get(), currentNum.ToString("D3"));
                            foreach (string groups in _SponsorGroups)
                            {
                                if (permission.UserHasGroup(player.UserIDString, groups))
                                {
                                    rust.RunServerCommand($"removegroup {player.UserIDString} {groups}");
                                }
                            }
                            rust.RunServerCommand($"addgroup {player.UserIDString} sponsor{(int)args[0].ToFloat():D3} 999d");
                            player.ChatMessage($"Успешно получен спонсор уровня {(int)args[0].ToFloat():D2}/{currentNum.ToString("D2")}");
                            _SponsorList.Add(player.userID.Get(), requestedLevel.ToString("D3"));
                            WeaponDamageScale.CallHook("ChangeLvl", player);
                            SaveData();
                            return;
                        }
                        else if (((int)args[0].ToFloat()) == currentNum)
                        {
                            player.ChatMessage($"У вас и так спонсор уровня {(int)args[0].ToFloat():D2}/{currentNum.ToString("D2")}");
                        }
                        else
                        {
                            player.ChatMessage($"{(int)args[0].ToFloat():D2} превышает максимальный уровень вашего спонсора ({currentNum.ToString("D2")})");
                        }
                    }
                }
                else
                {
                    player.ChatMessage("Вы не спонсор!");
                    return;
                }
            }
        }
        [ConsoleCommand("remsponsors")]
        void Csrem(ConsoleSystem.Arg args)
        {
            if (args.Player() != null) return;
            if (args.Args == null)
            {
                PrintError("remsponsors <стим айди>");
                return;
            }
            if (!ulong.TryParse(args.Args[0], out ulong steamid))
            {
                PrintError("Укажите действительный стим айди.");
                return;
            }
            int i = 0;
            foreach (string sponsor in _SponsorGroups)
            {
                if (permission.GroupExists(sponsor) && permission.UserHasGroup(steamid.ToString(), sponsor))
                {
                    rust.RunServerCommand($"removegroup {steamid} {sponsor}");
                    NextTick(() => permission.RemoveUserGroup(steamid.ToString(), sponsor));
                    i++;
                }
            }
            if (_SponsorList.ContainsKey(steamid))
            {
                _SponsorList.Remove(steamid);
                SaveData();
                i++;
            }
            if (_SponsorsList.ContainsKey(steamid))
            {
                _SponsorsList.Remove(steamid);
                SaveData();
                i++;
            }
            var player = BasePlayer.FindByID(steamid);
            if (player != null) WeaponDamageScale.CallHook("ChangeLvl", player);
            Puts($"Успешно ({i})");
            return;
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/SponsorsList", _SponsorsList);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/SponsorsList2", _SponsorList);
        }
        private void LoadData()
        {
            try
            {
                _SponsorsList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>(Title + "/SponsorsList");
                _SponsorList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>(Title + "/SponsorsList2");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }
        }
    }
}