using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Rust;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Universal Team", "Orange", "1.0.44")]
    [Description("")]
    public class UniTeam : RustPlugin
    {
        #region Oxide Hooks

        private void Init()
        {
            RelationshipManager.maxTeamSize = config.maxTeam;

            foreach (var alias in config.aliases)
            {
                cmd.AddChatCommand(alias, this, nameof(cmdControlChat));
                cmd.AddConsoleCommand(alias, this, nameof(cmdControlConsole));
            }

            cmd.AddChatCommand("ff", this, nameof(cmdControlChat));
            cmd.AddConsoleCommand("ff", this, nameof(cmdControlConsole));

            if (config.ff.enabled == false) Unsubscribe(nameof(OnEntityTakeDamage));

            LoadData();
        }

        private void OnNewSave(string name)
        {
            data.Clear();
            SaveData();
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            CheckDamage(player, info?.InitiatorPlayer, info);
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            ReturnTeam(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            ReturnTeam(player);
        }

        private void OnTeamDisbanded(RelationshipManager.PlayerTeam team)
        {
            if (data.ContainsKey(team.teamID))
            {
                data.Remove(team.teamID);
            }
        }

        #endregion

        #region Commands

        private void cmdControlConsole(ConsoleSystem.Arg arg)
        {
            cmdControlChat(arg.Player(), string.Empty, arg.Args);
        }

        private void cmdControlChat(BasePlayer player, string command, string[] args)
        {
            if (command == "ff")
            {
                var action = args?.Length > 0 ? args[0] : "check";
                ToggleFriendlyFire(player, action);
                return;
            }

            if (args == null || args?.Length == 0)
            {
                Message(player, "Usage");
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                case "new":
                case "inv":
                case "invite":
                    if (args.Length < 2)
                    {
                        Message(player, "Usage");
                        break;
                    }

                    SendInvite(player, args[1]);
                    break;

                case "leave":
                case "remove":
                    LeaveTeam(player);
                    break;

                case "ff":
                case "friendlyfire":
                case "fire":
                    var action = args.Length > 1 ? args[1] : "check";
                    ToggleFriendlyFire(player, action);
                    break;

                default:
                    Message(player, "Usage");
                    break;

            }
        }

        #endregion

        #region Core
        private bool IsNPC(BasePlayer player)
        {
            if (player is NPCPlayer) return true;
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L)) return true;
            return false;
        }

        private void CheckDamage(BasePlayer victim, BasePlayer initiator, HitInfo info)
        {
            if (initiator == null || initiator == victim || IsNPC(initiator) || IsNPC(victim) || initiator.Team == null || !initiator.Team.members.Contains(victim.userID)) return;

            if (OneVSOne != null && OneVSOne.Call<bool>("IsEventPlayer", victim)) return;

            if (info.damageTypes.Total() > 5f)
            {
                Message(initiator, "Attacking Friend");
                RunLocalEffect(initiator);
            }

            if (info.damageTypes.GetMajorityDamageType() == DamageType.Explosion && config.ff.bypassExplosiveDamage) return;

            if (HaveFFOn(initiator)) return;

            ClearDamage(info);
        }

        [PluginReference] Plugin OneVSOne;

        private void ReturnTeam(BasePlayer player)
        {
            var teamID = player.currentTeam;
            if (teamID == 0) return;

            var team = RelationshipManager.Instance.FindTeam(teamID);
            if (player.Team == null) return;

            timer.Once(1f, () => { team.AddPlayer(player); });
        }

        private void SendInvite(BasePlayer player, string targetName)
        {
            List<BasePlayer> players = BasePlayer.activePlayerList.Where(x => !x.Equals(player) && (x.UserIDString.Equals(targetName) || x.displayName.Contains(targetName, CompareOptions.IgnoreCase))).ToList(); ;
            if (players.Count.Equals(0))
            {
                Message(player, $"Нет игроков с таким именем или steamID! ({targetName})");
                return;
            }
            if (players.Count > 1)
            {
                Message(player, $"Есть много игроков с похожими никами:\n{players.Select(x => x.displayName).ToSentence()}");
                return;
            }
            BasePlayer target = players.FirstOrDefault();
            if (target == null) return;

            if (target.Team != null)
            {
                if (target.Team.members.Count == 1)
                {
                    LeaveTeam(target);
                }
                else
                {
                    Message(player, "Already In Team", target.displayName);
                    return;
                }
            }


            if (player.Team == null)
            {
                RelationshipManager.PlayerTeam team = RelationshipManager.Instance.CreateTeam();
                RelationshipManager.PlayerTeam playerTeam = team;
                playerTeam.teamLeader = player.userID;
                playerTeam.AddPlayer(player);
                Interface.CallHook("OnTeamCreated", (object)player, (object)team);
            }

            if (player.Team.members.Count >= config.maxTeam)
            {
                Message(player, "Max Members");
                return;
            }

            player.Team.SendInvite(target);
            Message(player, "Request Sent", target.displayName);
        }

        private void LeaveTeam(BasePlayer player)
        {
            if (player.Team == null)
            {
                Message(player, "Error", "Not In Team");
                return;
            }
            Interface.CallHook("OnTeamLeave", player.Team, player);
            player.Team.RemovePlayer(player.userID);
        }


        private void ToggleFriendlyFire(BasePlayer player, string action)
        {
            switch (action.ToLower())
            {
                case "activate":
                case "on":
                case "enable":
                    SetFriendlyFire(player, true);
                    break;

                case "deactivate":
                case "off":
                case "disable":
                    SetFriendlyFire(player, false);
                    break;

                default:
                    Message(player, HaveFFOn(player) ? "Friendly Fire Active" : "Friendly Fire Disabled");
                    break;
            }
        }

        private bool HaveFFOn(BasePlayer player)
        {
            if (player.Team == null) return true;
            if (data.ContainsKey(player.currentTeam)) return data[player.currentTeam];
            return true;
        }

        private void SetFriendlyFire(BasePlayer player, bool flag)
        {
            if (player.Team == null)
            {
                Message(player, "Not In Team");
                return;
            }

            if (player.Team.teamLeader != player.userID)
            {
                Message(player, "Not Owner");
                return;
            }
            if (!data.ContainsKey(player.currentTeam))
            {
                data.Add(player.currentTeam, flag);
            }
            else
            {
                data[player.currentTeam] = flag;
            }

            foreach (ulong value in player.Team.members)
            {
                player = BasePlayer.FindByID(value);
                if (player != null)
                {
                    Message(player, flag ? "Friendly Fire Active" : "Friendly Fire Disabled");
                }
            }
        }

        #endregion

        #region Utils

        private static void RunLocalEffect(BasePlayer player)
        {
            var effect = new Effect();
            var transform = player.transform;
            effect.Init(Effect.Type.Generic, transform.position, transform.forward);
            effect.pooledString = config.ff.effectHit;

            for (var i = 0; i < config.ff.effectVolume; i++)
            {
                EffectNetwork.Send(effect, player.net.connection);
            }
        }

        private static void ClearDamage(HitInfo info)
        {
            info.HitEntity = null;
            info.damageTypes = new DamageTypeList();
            info.DoHitEffects = false;
        }

        #endregion

        #region Configuration 1.1.2

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Commands")]
            public string[] aliases = new string[] { };

            [JsonProperty(PropertyName = "Max Team")]
            public int maxTeam = 3;

            [JsonProperty(PropertyName = "Friendly Fire settings")]
            public FriendlyFireSettings ff = new FriendlyFireSettings();
        }

        private class FriendlyFireSettings
        {
            [JsonProperty(PropertyName = "Check for friendly fire")]
            public bool enabled = true;

            [JsonProperty(PropertyName = "Bypass explosive damage at Friendly Fire")]
            public bool bypassExplosiveDamage = true;

            [JsonProperty(PropertyName = "Hit effect on friendly fire")]
            public string effectHit = "assets/bundled/prefabs/fx/invite_notice.prefab";

            [JsonProperty(PropertyName = "Effect volume")]
            public int effectVolume = 5;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                aliases = new string[]
                {
                    "friend",
                    "friends",
                    "team",
                    "clan",
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");

                timer.Every(10f, () =>
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                });
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization 1.1.1

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Usage", "Usage:\n<color=#00ffff>/friend add NameOrID</color> - add/remove player from friend list"},
                {"Error", "Error happened at <color=#ff0000>'{0}'</color>. Contact administrator and developer!"},

                {"Request Sent", "Team invite was sent to <color=#00ffff>'{0}'</color>"},
                {"Already In Team", "Player <color=#00ffff>'{0}'</color> already joined other team"},
                {"Not In Team", "You need to be in team to do that"},

                {"Name Changed", "Team name was changed to <color=#00ffff>'{0}'</color>"},
                {"Max Members", "You already have maximal team members!"},
                {"Not Owner", "You need to be leader of team to do that!"},

                {"Attacking Friend", "<color=#ff0000>Its your friend! Don't attack him!</color>"},
                {"Friendly Fire Active", "<color=#ff0000>Friendly fire is active! Your friends are in danger!</color>"},
                {"Friendly Fire Disabled", "<color=#00ff00>Friendly fire is disabled, your friends are safe!</color>"}
            }, this);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
            {
                return;
            }

            var message = GetMessage(messageKey, player.UserIDString, args);
            player.ChatMessage(message);
            //            player.SendConsoleCommand("chat.add", (object) 0, (object) message);
        }

        #endregion

        #region Data

        private const string filename = "UniTeam/ff";
        private static Dictionary<ulong, bool> data = new Dictionary<ulong, bool>();
        private bool corruptedData;

        private void LoadData()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, bool>>(filename);
                timer.Every(3600f, () => SaveData());
            }
            catch (Exception e)
            {
                corruptedData = true;

                timer.Every(10f, () =>
                {
                    PrintError($"!!! CRITICAL DATA ERROR !!!\n * Data was not loaded!\n * Data auto-save was disabled!\n * Error: {e.Message}");
                });
            }
        }

        private void SaveData()
        {
            if (corruptedData == false)
            {
                Interface.Oxide.DataFileSystem.WriteObject(filename, data);
            }
        }

        #endregion
    }
}