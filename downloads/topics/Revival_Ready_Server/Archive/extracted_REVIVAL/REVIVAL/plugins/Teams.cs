using System;
using System.Globalization;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Teams", "Drop Dead", "1.0.32")]
    public class Teams : RustPlugin
    {
        bool debug = false;
        private static Teams _ins;

        public Dictionary<ulong, uint> playerteam = new Dictionary<ulong, uint>();
        public Dictionary<uint, team> teams = new Dictionary<uint, team>();
        public Dictionary<ulong, string> players = new Dictionary<ulong, string>();
        public Dictionary<ulong, uint> pending = new Dictionary<ulong, uint>();
        string Layer = "Teams.Team";

        public class team
        {
            [JsonProperty("Список игроков в команде")]
            public Dictionary<ulong, string> members = new Dictionary<ulong, string>();
        }

        #region config

        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Настройки плагина")]
            public PSettings pluginsettings = new PSettings();

            public class PSettings
            {
                [JsonProperty("Лимит на количество игроков в команде (1-999999)")]
                public int teamlimit = 1;
                [JsonProperty("Отображение игроков точками в игре")]
                public bool usedraw = true;
                [JsonProperty("Отображение GameTip при попытке превысить лимит команды")]
                public bool usegametip = true;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        #endregion

        private void Init()
        {
            _ins = this;
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg);
        }

        #region data

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Players/Teams", playerteam);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Teams/Data", teams);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Players/Nicknames", players);
        }

        private void LoadData()
        {
            playerteam = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, uint>>($"{Title}/Players/Teams");
            teams = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, team>>($"{Title}/Teams/Data");
            players = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>($"{Title}/Players/Nicknames");
        }

        #endregion

        #region lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Command Helper", "Использование:\n/team create - создать команду\n/team add (+) <ник> - добавить игрока в команду\n/team remove (-) <ник> - удалить игрока из команды\n/team accept - принять приглашение в команду\n/team decline - отказаться от приглашения в команду\n/team leave - выйти из команды" },
                { "Has Team", "Вы уже состоите в команде" },
                { "Not Have Team", "Вы не состоите в команде" },
                { "No Invite", "Вам не отправляли приглашения в команду" },
                { "Accepted Invite", "Игрок {0} принял ваше приглашение в команду" },
                { "Declined Invite", "Игрок {0} отклонил ваше приглашение в команду" },
                { "Not Leader", "Вы не являетесь лидером команды" },
                { "Player Not Found", "Игрок с ником \"{0}\" не найден" }, 
                { "Target Has Team", "Игрок {0} уже состоит в другой команде" },
                { "Target In Player Team", "Игрок {0} уже состоит в вашей команде" },
                { "Player Not In Team", "Игрок {0} не состоит в вашей команде" },
                { "Succesful Deleted Player", "Вы успешно удалили игрока {0} из своей команды" },
                { "Sended Invite", "Вы успешно пригласили игрока {0} в свою команду" },
                { "Sended Invite Player", "{0} отправил вам приглашение в команду\nЧтобы принять введите /team accept\nЧтобы отклонить введите /team decline" },
                { "Pending Time Out", "Игрок {0} не ответил на приглашение" },
                { "Limit", "Невозможно пригласить игрока {0}, так как вы достигли лимита количеcтва игроков в команде" },
                { "GAMETIP LIMIT", "Вы достигли лимита количеcтва игроков в команде" },
                { "UI CREATE TEAM", "<size=14>CREATE TEAM</size>" },
                { "UI DELETE TEAM", "<size=14>DELETE TEAM</size>" },
                { "UI LEAVE TEAM", "<size=14>LEAVE TEAM</size>" }
            }, this);
        }

        private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);

        #endregion

        void OnServerInitialized()
        {
            LoadData();
            if (BasePlayer.activePlayerList.Count > 0) foreach (var player in BasePlayer.activePlayerList) OnPlayerInit(player);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) { CuiHelper.DestroyUi(player, Layer); CuiHelper.DestroyUi(player, "Teams.CreateButton"); CuiHelper.DestroyUi(player, "Teams.LeaveButton"); }
            SaveData();
            _ins = null;
        }

        void OnServerSave()
        {
            SaveData();
        }

        #region connect and disconnect

        private void OnPlayerInit(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerInit(player));
                return;
            }

            if (!players.ContainsKey(player.userID)) players.Add(player.userID, player.displayName);
            if (players.ContainsKey(player.userID) && players[player.userID] != player.displayName) { players[player.userID] = player.displayName; } 

            CheckTeam(player);

            if (!HaveTeam(player.userID)) DrawCreateTeamButton(player);
            else 
            { 
                if (!playerteam.ContainsKey(player.userID)) return;
                timer.Once(0.5f, () => 
                { 
                    if (!HaveTeam(player.userID)) return;
                    if (!playerteam.ContainsKey(player.userID)) return;
                    UpdateTeam(playerteam[player.userID]);
                }); 
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (!HaveTeam(player.userID)) return;
            if (!playerteam.ContainsKey(player.userID)) return;

            timer.Once(0.5f, () => 
            { 
                if (!HaveTeam(player.userID)) return;
                if (!playerteam.ContainsKey(player.userID)) return;
                UpdateTeam(playerteam[player.userID]); 
            });
        }

        #endregion

        #region wound

        void OnPlayerWound(BasePlayer player)
        {
            if (debug) Puts("OnPlayerWound works!");
            if (player == null) return;
            
            if (!playerteam.ContainsKey(player.userID)) return;
			timer.Once(0.5f, () =>
            {
                if (debug == true) Puts($"{player.displayName} has wounded");
                if (HaveTeam(player.userID) && playerteam.ContainsKey(player.userID)) UpdateTeam(playerteam[player.userID]);
            });
        }

        void OnPlayerRecover(BasePlayer player)
        {
            if (debug) Puts("OnPlayerRecover works!");
            if (player == null) return;

            if (!playerteam.ContainsKey(player.userID)) return;
            timer.Once(0.5f, () =>
            {
                if (debug == true) Puts($"{player.displayName} has recover");
                if (HaveTeam(player.userID) && playerteam.ContainsKey(player.userID)) UpdateTeam(playerteam[player.userID]);
            });
        }

        #endregion

        #region sleep

        void OnPlayerSleep(BasePlayer player)
        {
            if (debug) Puts("OnPlayerSleep works!");
            if (player == null) return;

            if (!playerteam.ContainsKey(player.userID)) return;
            timer.Once(0.5f, () =>
            {
                if (debug == true) Puts($"{player.displayName} has sleep");
                if (HaveTeam(player.userID) && playerteam.ContainsKey(player.userID)) UpdateTeam(playerteam[player.userID]);
            });
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (debug) Puts("OnPlayerSleepEnded works!");
            if (player == null) return;

            if (!playerteam.ContainsKey(player.userID)) return;
            timer.Once(0.5f, () =>
            {
                if (debug == true) Puts($"{player.displayName} has sleep ended");
                if (HaveTeam(player.userID) && playerteam.ContainsKey(player.userID)) UpdateTeam(playerteam[player.userID]);
            });
        }

        #endregion

        #region die

        void OnPlayerDeath(BasePlayer player)
        {
            if (player == null) return;

            if (!playerteam.ContainsKey(player.userID)) return;
            timer.Once(0.5f, () =>
            {
                if (HaveTeam(player.userID) && playerteam.ContainsKey(player.userID)) UpdateTeam(playerteam[player.userID]);
            });
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;

            if (!playerteam.ContainsKey(player.userID)) return;
            timer.Once(0.5f, () =>
            {
                if (HaveTeam(player.userID) && playerteam.ContainsKey(player.userID)) UpdateTeam(playerteam[player.userID]);
            });
        }

        #endregion

        uint GetRandomID()
        {
            int num;
            do
            {
                num = Oxide.Core.Random.Range(1, 99999999);
            }
            while (teams.ContainsKey(uint.Parse(num.ToString())));
            
            return uint.Parse(num.ToString());
        }

        [ConsoleCommand("CreateNewTeam")]
        private void CreateTeamCMD(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            if (debug) Puts($"{player.displayName} use command 'CreateNewTeam'");

            if (HaveTeam(player.userID)) return;

            CreateTeam(player, GetRandomID());
            CuiHelper.DestroyUi(player, "Teams.CreateButton");
            DrawTeamMenu(player);
        }

        [ConsoleCommand("LeaveFromTeam")]
        private void LeaveTeamCMD(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;
            if (debug) Puts($"{player.displayName} use command 'LeaveFromTeam'");

            if (!playerteam.ContainsKey(player.userID)) return;
            var team = teams[playerteam[player.userID]];
            if (team == null) return;
            if (!team.members.ContainsKey(player.userID)) return; 
            if (team.members[player.userID] == "member") LeaveTeam(player, playerteam[player.userID]);
            if (team.members[player.userID] == "leader") DeleteTeam(player, playerteam[player.userID]);

            /*foreach (var team in teams)
            {
                if (!playerteam.ContainsKey(player.userID)) return;
                if (team.Key != playerteam[player.userID]) continue;
                if (!team.Value.members.ContainsKey(player.userID)) return; 
                if (team.Value.members[player.userID] == "member") LeaveTeam(player, playerteam[player.userID]);
                if (team.Value.members[player.userID] == "leader") DeleteTeam(player, playerteam[player.userID]);
            }*/
        }

        [ChatCommand("team")]
        private void TeamHandler(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (args.Length < 1)
            {
                player.ChatMessage(GetMsg("Command Helper"));
                return;
            }

            if (args[0] == "create")
            {
                if (HaveTeam(player.userID))
                {
                    player.ChatMessage(GetMsg("Has Team"));
                    return;
                }
                player.SendConsoleCommand("CreateNewTeam");
                return;
            }
            if (args[0] == "leave")
            {
                if (!HaveTeam(player.userID))
                {
                    player.ChatMessage(GetMsg("Not Have Team"));
                    return;
                }
                player.SendConsoleCommand("LeaveFromTeam");
                return;
            }

            if (args[0] == "accept")
            {
                if (!pending.ContainsKey(player.userID))
                {
                    player.ChatMessage(GetMsg("No Invite"));
                    return;
                }

                var team = teams[pending[player.userID]];
                if (team == null) return;
                AddToTeam(pending[player.userID], player.userID);
                foreach (var leader in team.members)
                {
                    if (leader.Value == "leader")
                    {
                        BasePlayer member = BasePlayer.FindByID(leader.Key);
                        if (member == null) continue;
                        member.ChatMessage(GetMsg("Accepted Invite").Replace("{0}", player.displayName));
                    }
                }
                /*foreach (var team in teams)
                {
                    if (team.Key != playerteam[player.userID]) continue;
                    foreach (var mem in team.Value.members)
                    {
                        if (mem.Value == "leader")
                        {
                            BasePlayer member = BasePlayer.FindByID(mem.Key);
                            if (member == null) continue;
                            member.ChatMessage($"Игрок {player.displayName} принял ваше приглашение в команду");
                        }
                        else continue;
                    }
                }*/
                pending.Remove(player.userID);
            }
            if (args[0] == "decline")
            {
                if (!pending.ContainsKey(player.userID))
                {
                    player.ChatMessage(GetMsg("No Invite"));
                    return;
                }

                foreach (var team in teams)
                {
                    if (team.Key != pending[player.userID]) continue;
                    foreach (var leader in teams[pending[player.userID]].members)
                    {
                        if (leader.Value == "leader")
                        {
                            BasePlayer member = BasePlayer.FindByID(leader.Key);
                            if (member == null) continue;
                            member.ChatMessage(GetMsg("Declined Invite").Replace("{0}", player.displayName));
                        }
                    }
                    /*foreach (var mem in team.Value.members)
                    {
                        if (mem.Value == "leader")
                        {
                            BasePlayer member = BasePlayer.FindByID(mem.Key);
                            if (member == null) continue;
                            member.ChatMessage($"Игрок {player.displayName} отклонил ваше приглашение в команду");
                        }
                        else continue;
                    }*/
                }
                pending.Remove(player.userID);
            }

            if (args[0] == "add" || args[0] == "+")
            {
                if (!HaveTeam(player.userID) || !playerteam.ContainsKey(player.userID))
                {
                    player.ChatMessage(GetMsg("Not Have Team"));
                    return;
                }
                if (!isLeader(player))
                {
                    player.ChatMessage(GetMsg("Not Leader"));
                    return;
                }

                if (args.Length < 1 || args.Length < 2)
                {
                    player.ChatMessage(GetMsg("Command Helper"));
                    return;
                }
                if (args.Length > 3)
                {
                    player.ChatMessage(GetMsg("Command Helper"));
                    return;
                }

                var target = BasePlayer.Find(args[1]);
                if (target == null)
                {
                    player.ChatMessage(GetMsg("Player Not Found").Replace("{0}", args[1])); 
                    return;
                }
                if (playerteam.ContainsKey(target.userID) && playerteam[target.userID] != playerteam[player.userID])
                {
                    player.ChatMessage(GetMsg("Target Has Team").Replace("{0}", target.displayName));
                    return;
                }

                if (playerteam.ContainsKey(target.userID) && playerteam[target.userID] == playerteam[player.userID])
                {
                    player.ChatMessage(GetMsg("Target In Player Team").Replace("{0}", target.displayName));
                    return;
                }

                if (debug) Puts($"Trying 'AddToTeam' {target.displayName}");
                //AddToTeam(playerteam[player.userID], target.userID);
                //player.ChatMessage($"Вы успешно добавили игрока {target.displayName} в свою команду");
                SendInviteToTeam(player, target, playerteam[player.userID]);
            }

            if (args[0] == "remove" || args[0] == "-")
            {
                if (!HaveTeam(player.userID) || !playerteam.ContainsKey(player.userID))
                {
                    player.ChatMessage(GetMsg("Not Have Team"));
                    return;
                }
                if (!isLeader(player))
                {
                    player.ChatMessage(GetMsg("Not Leader"));
                    return;
                }

                if (args.Length < 1 || args.Length < 2)
                {
                    player.ChatMessage(GetMsg("Command Helper"));
                    return;
                }
                if (args.Length > 3)
                {
                    player.ChatMessage(GetMsg("Command Helper"));
                    return;
                }
                

                
                var target = BasePlayer.Find(args[1]);
                if (target == null)
                {
                    foreach (var p in players)
                    {
                        if (!p.Value.Contains(args[1])) continue;
                        var sleep = BasePlayer.FindSleeping(p.Key);
                        if (sleep == null)
                        {
                            player.ChatMessage(GetMsg("Player Not Found").Replace("{0}", args[1])); 
                            return;
                        }

                        if (!playerteam.ContainsKey(sleep.userID) || !playerteam.ContainsKey(sleep.userID) || playerteam[sleep.userID] != playerteam[player.userID])
                        {
                            player.ChatMessage(GetMsg("Player Not In Team").Replace("{0}", sleep.displayName));
                            return;
                        }

                        if (debug) Puts($"Trying 'RemoveFromTeam' {sleep.displayName}");
                        RemoveFromTeam(playerteam[player.userID], sleep.userID);
                        player.ChatMessage(GetMsg("Succesful Deleted Player").Replace("{0}", sleep.displayName));
                        return;
                    }
                }
                else
                {
                    if (!playerteam.ContainsKey(target.userID) || !playerteam.ContainsKey(player.userID) || playerteam[target.userID] != playerteam[player.userID])
                    {
                        player.ChatMessage(GetMsg("Player Not In Team").Replace("{0}", target.displayName));
                        return;
                    }

                    if (debug) Puts($"Trying 'RemoveFromTeam' {target.displayName}");
                    RemoveFromTeam(playerteam[player.userID], target.userID);
                    player.ChatMessage(GetMsg("Succesful Deleted Player").Replace("{0}", target.displayName));
                }
            }
        }

        #region ui

        void DrawCreateTeamButton(BasePlayer player)
        {
            return;

            CuiHelper.DestroyUi(player, "Teams.CreateButton");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat("#66625DA1"), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "136 34", OffsetMax = "277 62" },
                CursorEnabled = false,
            }, "Overlay", "Teams.CreateButton");

            container.Add(new CuiElement
            {
                Parent = "Teams.CreateButton",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#CCC1B8FF"), Text = GetMsg("UI CREATE TEAM"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = "CreateNewTeam", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "Teams.CreateButton");

            CuiHelper.AddUi(player, container);
        }

        void DrawTeamMenu(BasePlayer player)
        {
            if (!HaveTeam(player.userID)) { DrawCreateTeamButton(player); return; }

            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "30 30", OffsetMax = "280 90" },
                CursorEnabled = false,
            }, "Overlay", Layer);

            const double startAnMinX = 0.03733328;
            const double startAnMaxX = 0.48;
            const double startAnMinY = 0.6222221;
            const double startAnMaxY = 0.9777777;
            double anMinX = startAnMinX;
            double anMaxX = startAnMaxX;
            double anMinY = startAnMinY;
            double anMaxY = startAnMaxY;

            foreach (var team in teams)
            {
                if (team.Key != playerteam[player.userID]) continue;
                Dictionary<ulong, string> dict = team.Value.members.Take(6).ToDictionary(pair => pair.Key, pair => pair.Value);
                for (int i = 0; i < dict.Count(); i++)
                {
                    var memberid = dict.Keys.ToList()[i];
                    var membervalue = dict.Values.ToList()[i];
                    if (!players.ContainsKey(memberid)) continue;

                    if ((i != 0) && (i % 3 == 0))
                    {
                        anMinX += 0.40533232;
                        anMaxX += 0.4053313;
                        //anMinY -= 0.3111113;
                        //anMaxY -= 0.3111119;
                        anMinY = startAnMinY;
                        anMaxY = startAnMaxY;
                    }

                    container.Add(new CuiElement
                    {
                        Parent = Layer,
                        Components =
                        {
                            new CuiTextComponent { Text = $"<color={GetColor(memberid)}>" + GetPrefix(memberid) + " " + CheckText(15, players[memberid]) + "</color>", Align = TextAnchor.MiddleLeft, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                            new CuiRectTransformComponent {AnchorMin = $"{anMinX} {anMinY}", AnchorMax = $"{anMaxX} {anMaxY}"},
                            new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1 1" }
                        }
                    });

                    //anMinX += 0.40533232;
                    //anMaxX += 0.4053313;
                    anMinY -= 0.3111113;
                    anMaxY -= 0.3111119;
                }
            }

            CuiHelper.AddUi(player, container);
            DrawLeaveTeamButton(player);
        }

        void DrawLeaveTeamButton(BasePlayer player)
        {
            return;
            if (!HaveTeam(player.userID)) return;

            CuiHelper.DestroyUi(player, "Teams.LeaveButton");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat("#66625DA1"), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "277 60", OffsetMax = "395 86" },
                CursorEnabled = false,
            }, "Overlay", "Teams.LeaveButton");

            container.Add(new CuiElement
            {
                Parent = "Teams.LeaveButton",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#CCC1B8FF"), Text = isLeader(player) ? GetMsg("UI DELETE TEAM") : GetMsg("UI LEAVE TEAM"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = "LeaveFromTeam", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "Teams.LeaveButton");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region helpers

        void SendInviteToTeam(BasePlayer player, BasePlayer target, uint teamid)
        {
            if (!playerteam.ContainsKey(player.userID)) return;
            var team = teams[playerteam[player.userID]];
            if (team == null) return;
            if (team.members.Count() >= cfg.pluginsettings.teamlimit)
            {
                if (!cfg.pluginsettings.usegametip) player.ChatMessage(GetMsg("Limit").Replace("{0}", target.displayName));
                else CreateGameTip(GetMsg("GAMETIP LIMIT").Replace("{0}", target.displayName), player);
                return;
            }
            if (HaveTeam(target.userID))
            {
                player.ChatMessage(GetMsg("Target Has Team").Replace("{0}", target.displayName));
                return;
            }

            pending.Add(target.userID, teamid);
            player.ChatMessage(GetMsg("Sended Invite").Replace("{0}", target.displayName));
            target.ChatMessage(GetMsg("Sended Invite Player").Replace("{0}", player.displayName));

            timer.Once(15f, () => { 
                if (!pending.ContainsKey(target.userID)) return;
                pending.Remove(target.userID);
                player.ChatMessage(GetMsg("Pending Time Out").Replace("{0}", target.displayName));
            });
        }

        string CheckText(int chars, string text)
        {
            if (text.Length >= chars) return text.Substring(0, chars - 4) + "..";
            return text;
        }

        void AddToTeam(uint teamid, ulong target)
        {
            var team = teams[teamid];
            if (team == null) return;
            if (!team.members.ContainsKey(target)) { team.members.Add(target, "member"); if (debug) Puts($"'AddToTeam' succeful {target}, {teamid}"); }
            if (!playerteam.ContainsKey(target)) playerteam.Add(target, teamid);
            else playerteam[target] = teamid;

            BasePlayer targetz = BasePlayer.FindByID(target);
            if (targetz == null) return;
            CuiHelper.DestroyUi(targetz, "Teams.CreateButton");

            /*foreach (var team in teams)
            {
                if (team.Key != teamid) continue;
                if (!team.Value.members.ContainsKey(target)) { team.Value.members.Add(target, "member"); if (debug) Puts($"'AddToTeam' succeful {target}, {teamid}"); }
                if (!playerteam.ContainsKey(target)) playerteam.Add(target, teamid);
                else playerteam[target] = teamid;

                BasePlayer targetz = BasePlayer.FindByID(target);
                if (targetz == null) return;
                CuiHelper.DestroyUi(targetz, "Teams.CreateButton");
            }*/
            UpdateTeam(teamid);
        }

        void RemoveFromTeam(uint teamid, ulong target)
        {
            if (!HaveTeam(target)) return;

            var team = teams[teamid];
            if (team == null) return;
            if (!team.members.ContainsKey(target)) return;
            else team.members.Remove(target);
            if (!playerteam.ContainsKey(target)) return;
            else playerteam.Remove(target);
            if (debug) Puts($"'RemoveFromTeam' succeful {target}, {teamid}");

            /*foreach (var team in teams)
            {
                if (team.Key != teamid) continue;
                if (!team.Value.members.ContainsKey(target)) return;
                else team.Value.members.Remove(target);
                if (!playerteam.ContainsKey(target)) return;
                else playerteam.Remove(target);

                if (debug) Puts($"'RemoveFromTeam' succeful {target}, {teamid}");
            }*/
            UpdateTeam(teamid);

            BasePlayer targetz = BasePlayer.FindByID(target);
            if (targetz == null) return;
            CuiHelper.DestroyUi(targetz, Layer);
            CuiHelper.DestroyUi(targetz, "Teams.LeaveButton");
            DrawCreateTeamButton(targetz);
        }

        void UpdateTeam(uint teamid)
        {
            if (debug) Puts($"Trying 'UpdateTeam' {teamid}");

            if (!teams.ContainsKey(teamid)) return;
            var team = teams[teamid];
            if (team == null) return;
            foreach (var member in team.members)
            {
                var onlinemember = BasePlayer.FindByID(member.Key);
                if (onlinemember == null) continue;
                DrawTeamMenu(onlinemember);
                if (debug) Puts($"'UpdateTeam' succeful - {teamid}, {onlinemember.displayName}");
            }

            /*foreach (var team in teams)
            {
                if (team.Key != teamid) continue;
                foreach (var member in team.Value.members)
                {
                    var onlinemember = BasePlayer.FindByID(member.Key);
                    if (onlinemember == null) continue;
                    DrawTeamMenu(onlinemember);
                    if (debug) Puts($"'UpdateTeam' succeful - {teamid}, {onlinemember.displayName}");
                }
            }*/
        }

        void CreateTeam(BasePlayer leader, uint teamid)
        {
            teams.Add(teamid, new team { members = new Dictionary<ulong, string> { [leader.userID] = "leader" } });
            if (!playerteam.ContainsKey(leader.userID)) playerteam.Add(leader.userID, teamid);
            else { playerteam.Remove(leader.userID); playerteam.Add(leader.userID, teamid); }
        }

        void DeleteTeam(BasePlayer leader, uint teamid)
        {
            if (!playerteam.ContainsKey(leader.userID))
            {
                leader.ChatMessage(GetMsg("Not Have Team"));
                return;
            }

            var team = teams[teamid];
            if (team == null) return;
            foreach (var p in team.members)
            {
                BasePlayer mem = BasePlayer.FindByID(p.Key);
                if (mem != null)
                {
                    CuiHelper.DestroyUi(mem, Layer);
                    CuiHelper.DestroyUi(mem, "Teams.LeaveButton");
                    DrawCreateTeamButton(mem);
                }
                if (playerteam.ContainsKey(p.Key)) playerteam.Remove(p.Key);
            }
            teams.Remove(teamid);

            /*foreach (var team in teams)
            {
                if (team.Key != teamid) continue;
                foreach (var p in team.Value.members)
                {
                    BasePlayer mem = BasePlayer.FindByID(p.Key);
                    if (mem != null)
                    {
                        CuiHelper.DestroyUi(mem, Layer);
                        CuiHelper.DestroyUi(mem, "Teams.LeaveButton");
                        DrawCreateTeamButton(mem);
                        if (playerteam.ContainsKey(p.Key)) playerteam.Remove(p.Key);
                    }
                }
                teams.Remove(team.Key);
            }*/
        }

        void LeaveTeam(BasePlayer member, uint teamid)
        {
            RemoveFromTeam(teamid, member.userID);
        }

        void CheckTeam(BasePlayer player)
        {
            if (player == null) return;
            if (playerteam.ContainsKey(player.userID))
            {
                if (!teams.ContainsKey(playerteam[player.userID]))
                    playerteam.Remove(player.userID);
            }
        }

        bool isLeader(BasePlayer player)
        {
            if (!HaveTeam(player.userID)) return false;

            var team = teams[playerteam[player.userID]];
            if (team == null) return false;
            if (!team.members.ContainsKey(player.userID)) return false;
            if (team.members[player.userID] == "leader") return true;

            return false;
            /*foreach (var team in teams)
            {
                if (team.Key != playerteam[player.userID]) continue;
                if (team.Value.members[player.userID] == "leader") return true;
            }
            return false;*/
        }

        bool HaveTeam(ulong playerid)
        {
            if (!playerteam.ContainsKey(playerid)) return false;
            else if (!teams.ContainsKey(playerteam[playerid])) return false;
            else return true;
        }

        string GetPrefix(ulong playerid)
        {
            if (!playerteam.ContainsKey(playerid)) return null;

            var team = teams[playerteam[playerid]];
            if (team == null) return null;
            if (!team.members.ContainsKey(playerid)) return null;
            if (team.members[playerid] == "leader") return "✓";
            if (team.members[playerid] == "member") return " • ";
            return " • ";

            /*foreach (var team in teams)
            {
                if (team.Key != playerteam[playerid]) continue;
                if (team.Value.members[playerid] == "leader") return "✓";
                if (team.Value.members[playerid] == "member") return " • ";
            }
            return " • ";*/
        }

        string GetColor(ulong playerid)
        {
            var player = BasePlayer.FindByID(playerid);
            if (player == null) return "#707070";
            if (player.IsWounded() || player.IsDead()) return "#CD3232";
            return "#aaff55";
        }

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        private void CreateGameTip(string text, BasePlayer player, float length = 15f)
        {
            if (player == null)
                return;
            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showgametip", text);
            timer.Once(length, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        #endregion

        #region API

        [HookMethod("TeamMembers")]
        public List<ulong> TeamMembers(BasePlayer player)
        {
            if (!playerteam.ContainsKey(player.userID) || !HaveTeam(player.userID) || !cfg.pluginsettings.usedraw) return null;
            List<ulong> TeamMembers = new List<ulong>();

            if (!teams.ContainsKey(playerteam[player.userID])) return null;
            var team = teams[playerteam[player.userID]];
            if (team == null) return null;

            foreach (var member in team.members)
            {
                if (member.Key == player.userID) continue;
                if (!TeamMembers.Contains(member.Key)) TeamMembers.Add(member.Key);
            }
            if (TeamMembers == null) return null;
            return TeamMembers;
        }

        #endregion
    }
}