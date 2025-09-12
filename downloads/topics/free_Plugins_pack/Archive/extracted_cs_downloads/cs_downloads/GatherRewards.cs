#define DEBUG
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;


//GatherRewards created with PluginMerge v(1.0.4.0) by MJSU @ https://github.com/dassjosh/Plugin.Merge
namespace Oxide.Plugins
{
    [Info("Gather Rewards", "Shady14u", "1.6.5")]
    [Description("Earn rewards through Economics/Server Rewards for killing and gathering")]
    public partial class GatherRewards : RustPlugin
    {
        #region GatherRewards.cs
        [PluginReference]
        private Plugin Economics, ServerRewards, Friends, Clans;
        
        private string _resource;
        #endregion

        #region GatherRewards.Config.cs
        private PluginConfig config;
        
        PluginConfig DefaultConfig()
        {
            var defaultConfig = new PluginConfig
            {
                Settings = new PluginSettings
                {
                    ChatEditCommand = "gatherrewards",
                    ConsoleEditCommand = "gatherrewards",
                    EditPermission = "gatherrewards.canedit",
                    ShowMessagesOnKill = true,
                    ShowMessagesOnGather = true,
                    UseEconomics = true,
                    UseServerRewards = false,
                    PluginPrefix = "<color=#00FFFF>[GatherRewards]</color>",
                    AwardOnlyOnFullHarvest = false
                },
                Rewards = new Dictionary<string, float>
                {
                    { PluginRewards.Player, 0 },
                    { PluginRewards.PlayerFriend, -25 },
                    { PluginRewards.ClanMember, -25 },
                    { PluginRewards.Ore, 25 },
                    { PluginRewards.Wood, 25 },
                    { PluginRewards.Stone, 25 },
                    { PluginRewards.Corn, 25 },
                    { PluginRewards.Hemp, 25 },
                    { PluginRewards.Mushroom, 25 },
                    { PluginRewards.Pumpkin, 25 },
                    { PluginRewards.TeamMember, -25 },
                    {PluginRewards.Suicide, -100 }
                    
                }
            };
            
            foreach (GameManifest.PooledString str in GameManifest.Current.pooledStrings)
            {
                if (str.str.StartsWith("assets/rust.ai/agents"))
                {
                    if (str.str.Contains("-") || str.str.Contains("_"))
                    {
                        continue;
                    }
                    
                    if (str.str.Contains("test"))
                    {
                        continue;
                    }
                    
                    if (str.str.Contains("npc"))
                    {
                        continue;
                    }
                    
                    var animal = str.str.Substring(str.str.LastIndexOf('/') + 1).Replace(".prefab", string.Empty);
                    if (animal.Contains("."))
                    {
                        continue;
                    }
                    
                    defaultConfig.Rewards[UppercaseFirst(animal)] = 25;
                }
            }
            
            defaultConfig.Rewards["Scientist"] = 25;
            defaultConfig.Rewards["Murderer"] = 25;
            return defaultConfig;
        }
        
        
        private bool _configChanged;
        
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(DefaultConfig(), true);
            PrintWarning("New configuration file created.");
        }
        
        private void LoadConfigValues()
        {
            config = Config.ReadObject<PluginConfig>();
            var defaultConfig = DefaultConfig();
            Merge(config.Rewards, defaultConfig.Rewards);
            
            if (!_configChanged) return;
            PrintWarning("Configuration file updated.");
            Config.WriteObject(config);
        }
        
        private void Merge<T1, T2>(IDictionary<T1, T2> current, IDictionary<T1, T2> defaultDict)
        {
            foreach (var pair in defaultDict)
            {
                if (current.ContainsKey(pair.Key)) continue;
                current[pair.Key] = pair.Value;
                _configChanged = true;
            }
        }
        #endregion

        #region GatherRewards.Permissions.cs
        private bool CheckPermission(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }
        
        private void RegisterPermsAndCommands()
        {
            permission.RegisterPermission(config.Settings.EditPermission, this);
            foreach (var groupModifier in config.Settings.GroupModifiers)
            {
                permission.RegisterPermission(groupModifier.Key,this);
            }
            
            var command = Interface.Oxide.GetLibrary<Command>();
            command.AddChatCommand(config.Settings.ChatEditCommand, this, "cmdGatherRewards");
            command.AddConsoleCommand(config.Settings.ConsoleEditCommand, this, "cmdConsoleGatherRewards");
        }
        #endregion

        #region GatherRewards.Lang.cs
        private string Lang(string key, object userID = null) =>
        lang.GetMessage(key, this, userID?.ToString());
        
        private void Language()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ReceivedForGather", "You have received ${0} for gathering {1}." },
                { "LostForGather", "You have lost ${0} for gathering {1}." },
                { "ReceivedForKill", "You have received ${0} for killing a {1}." },
                { "LostForKill", "You have lost ${0} for killing a {1}." },
                { "NoPermission", "You have no permission to use this command." },
                { "Usage", "Usage: /{0} [value] [amount]" },
                { "NotaNumber", "Error: value is not a number." },
                { "Success", "Successfully changed '{0}' to earn amount '{1}'." },
                { "ValueDoesNotExist", "Value '{0}' does not exist." }
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ReceivedForGather", "Вы получили ${0} за сбор {1}." },
                { "LostForGather", "Вы потеряли ${0} за сбор {1}." },
                { "ReceivedForKill", "Вы получили ${0} за убийство {1}." },
                { "LostForKill", "Вы потеряли $ {0} за убийство {1}." },
                { "NoPermission", "У вас нет прав использовать эту команду." },
                { "Usage", "Использование: / {0} [значение] [количество]" },
                { "NotaNumber", "Ошибка: значение не является числом." },
                { "Success", "Успешно изменено '{0}', чтобы заработать деньги '{1}'." },
                { "ValueDoesNotExist", "Значение '{0}' не существует." }
            }, this, "ru");
        }
        #endregion

        #region GatherRewards.Hooks.cs
        private void Init()
        {
            LoadConfigValues();
            Language();
            RegisterPermsAndCommands();
        }
        
        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (!Economics && !ServerRewards) return;
            if (player == null) return;
            _resource = null;
            var amount = 0f;
            
            foreach (var configValue in config.Rewards)
            {
                if (!collectible.ToString().ToLower().Contains(configValue.Key.ToLower())) continue;
                amount = config.Rewards[configValue.Key];
                _resource = configValue.Key;
                break;
            }
            
            if (_resource != null && amount != 0)
            {
                GiveCredit(player, "gather", amount, _resource);
            }
        }
        
        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!Economics && !ServerRewards) return;
            var player = entity?.ToPlayer();
            if (player==null) return;
            var amount = 0f;
            var shortName = item.info.shortname;
            _resource = null;
            
            foreach (KeyValuePair<string, float> configValue in config.Rewards)
            {
                if (!shortName.Contains(configValue.Key.ToLower())) continue;
                amount = CheckPoints(configValue.Key);
                _resource = item.info.shortname;
                break;
            }
            
            if (_resource == null || amount == 0) return;
            
            if (player.GetHeldEntity() is Jackhammer)
            {
                amount *= config.Settings.JackHammerModifier;
            }
            if(player.GetHeldEntity() is Chainsaw)
            {
                amount *= config.Settings.ChainsawModifier;
            }
            
            if (config.Settings.AwardOnlyOnFullHarvest)
            {
                var ent = dispenser.GetComponent<BaseEntity>();
                NextTick(() =>
                {
                    
                    if (dispenser.gatherType == ResourceDispenser.GatherType.Tree && dispenser.fractionRemaining <=0)
                    {
                        GiveCredit(player, "gather", amount, "Tree");
                        return;
                    }
                    
                    if (ent != null)
                    {
                        return;
                    }
                    
                    GiveCredit(player, "gather", amount, item.info.shortname);
                    
                    return;
                });
                
            }
            else
            {
                GiveCredit(player, "gather", amount, _resource);
            }
            
            
        }
        
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;
            if (!Economics && !ServerRewards) return;
            var player = info?.Initiator?.ToPlayer();
            if (player==null) return;
            
            string animal;
            float amount;
            
            if (entity.ToPlayer() != null && !(entity is NPCPlayer))
            {
                //Player Killed was a Real Player
                var victim = entity.ToPlayer();
                if (player == victim)
                {
                    GiveCredit(player, "suicide", CheckPoints(PluginRewards.Suicide), "suicide");
                    return;
                }
                
                amount = CheckPoints(PluginRewards.Player);
                animal = "player";
                
                if (Friends && config.Rewards[PluginRewards.PlayerFriend] != 0)
                {
                    var isFriend = Friends.Call<bool>("HasFriend", victim.userID, player.userID);
                    var isFriendReverse = Friends.Call<bool>("HasFriend", player.userID, victim.userID);
                    if (isFriend || isFriendReverse)
                    {
                        amount = CheckPoints(PluginRewards.PlayerFriend);
                        animal = "friend";
                    }
                }
                
                if (Clans && config.Rewards[PluginRewards.ClanMember] != 0)
                {
                    var victimClan = Clans.Call<string>("GetClanOf", victim.userID);
                    var playerClan = Clans.Call<string>("GetClanOf", player.userID);
                    if (victimClan == playerClan && !string.IsNullOrEmpty(playerClan))
                    {
                        amount = CheckPoints(PluginRewards.ClanMember);
                        animal = "clan member";
                    }
                }
                
                if(player.Team!=null && player.Team.members.Contains(victim.userID) && config.Rewards[PluginRewards.TeamMember] != 0)
                {
                    amount = CheckPoints(PluginRewards.TeamMember);
                    animal = "team member";
                }
                
            }
            else
            {
                if (entity is NPCPlayer)
                {
                    var npcPlayer = (NPCPlayer)entity;
                    animal = npcPlayer?.displayName ?? "Murderer";
                    
                    //Patch Tunnel Dwellers
                    if (entity.ShortPrefabName.Contains("npc_tunnel"))
                    {
                        animal = "Tunnel Dweller";
                    }
                    
                    //set the default amount to that of a murdered or 125
                    amount = CheckPoints(animal, CheckPoints("Murderer", 125));
                    
                    long npcId;
                    if (long.TryParse(animal, out npcId))
                    {
                        //override names that are just numbers
                        amount = CheckPoints(entity.ShortPrefabName, CheckPoints("Murderer", 125));
                        animal = "Scientist";
                    }
                }
                else
                {
                    animal = entity.ShortPrefabName;
                    amount = CheckPoints(animal);
                }
            }
            
            if (amount != 0)
            {
                GiveCredit(player, "kill", amount, animal);
            }
        }
        #endregion

        #region GatherRewards.Helpers.cs
        private float CheckPoints(string animal, float defaultAmount = 0)
        {
            foreach (var configValue in config.Rewards.Where(configValue =>
            string.Equals(animal, configValue.Key, StringComparison.CurrentCultureIgnoreCase)))
            {
                animal = configValue.Key;
                return config.Rewards[configValue.Key];
            }
            
            if (defaultAmount == 0) return defaultAmount;
            
            config.Rewards.Add(animal, defaultAmount);
            Config.WriteObject(config);
            
            return defaultAmount;
        }
        
        private void GiveCredit(BasePlayer player, string type, float amount, string gathered)
        {
            if(amount==0) return;
            foreach (var groupModifier in config.Settings.GroupModifiers.OrderByDescending(x=>x.Value))
            {
                if (permission.UserHasPermission(player.UserIDString, groupModifier.Key))
                {
                    amount *= float.Parse(groupModifier.Value.ToString());
                    break;
                }
            }
            
            if (amount > 0)
            {
                if (config.Settings.UseEconomics && Economics)
                {
                    Economics.Call("Deposit", player.UserIDString, (double)amount);
                }
                
                if (config.Settings.UseServerRewards && ServerRewards)
                {
                    ServerRewards.Call("AddPoints", new object[] { player.userID, (int)amount });
                }
                
                if (type == "gather" && config.Settings.ShowMessagesOnGather)
                {
                    PrintToChat(player,
                    config.Settings.PluginPrefix + " " + string.Format(Lang("ReceivedForGather", player), amount,
                    gathered.ToLower()));
                }
                else if (type == "kill" && config.Settings.ShowMessagesOnKill)
                {
                    PrintToChat(player,
                    config.Settings.PluginPrefix + " " + string.Format(Lang("ReceivedForKill", player), amount,
                    gathered.ToLower()));
                }
            }
            else
            {
                amount = Math.Abs(amount);
                
                if (config.Settings.UseEconomics && Economics)
                {
                    Economics.Call("Withdraw", player.UserIDString, (double)amount);
                }
                
                if (config.Settings.UseServerRewards && ServerRewards)
                {
                    var points = ServerRewards.Call<int>("CheckPoints", player.userID);
                    if (points < amount && points > 0)
                    {
                        amount = points;
                    }
                    ServerRewards.Call("TakePoints", new object[] { player.userID, (int)amount });
                }
                
                if (type == "gather" && config.Settings.ShowMessagesOnGather)
                {
                    PrintToChat(player,
                    config.Settings.PluginPrefix + " " +
                    string.Format(Lang("LostForGather", player), amount, gathered.ToLower()));
                }
                else if (type == "kill" && config.Settings.ShowMessagesOnKill)
                {
                    PrintToChat(player,
                    config.Settings.PluginPrefix + " " +
                    string.Format(Lang("LostForKill", player), amount, gathered.ToLower()));
                }
            }
        }
        
        private static string UppercaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
            return string.Empty;
            var a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }
        #endregion

        #region GatherRewards.Commands.cs
        private void cmdGatherRewards(BasePlayer player, string command, string[] args)
        {
            if (!(CheckPermission(player, config.Settings.EditPermission)))
            {
                SendReply(player, config.Settings.PluginPrefix + " " + Lang("NoPermission", player.UserIDString));
                return;
            }
            
            if (args.Length < 2)
            {
                SendReply(player,
                config.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString),
                config.Settings.ChatEditCommand));
                return;
            }
            
            float value;
            if (float.TryParse(args[1], out value) == false)
            {
                SendReply(player, config.Settings.PluginPrefix + " " + Lang("NotaNumber", player.UserIDString));
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "clan":
                {
                    config.Rewards[PluginRewards.ClanMember] = value;
                    Config.WriteObject(config);
                    SendReply(player,
                    config.Settings.PluginPrefix + " " +
                    string.Format(Lang("Success", player.UserIDString), "clan member", value));
                    break;
                }
                case "friend":
                {
                    config.Rewards[PluginRewards.PlayerFriend] = value;
                    Config.WriteObject(config);
                    SendReply(player,
                    config.Settings.PluginPrefix + " " +
                    string.Format(Lang("Success", player.UserIDString), "friend", value));
                    break;
                }
                default:
                {
                    if (!config.Rewards.ContainsKey(UppercaseFirst(args[0].ToLower())))
                    {
                        SendReply(player,
                        config.Settings.PluginPrefix + " " +
                        string.Format(Lang("ValueDoesNotExist", player.UserIDString), args[0].ToLower()));
                        break;
                    }
                    
                    config.Rewards[UppercaseFirst(args[0].ToLower())] = float.Parse(args[1]);
                    Config.WriteObject(config);
                    
                    SendReply(player,
                    config.Settings.PluginPrefix + " " + string.Format(Lang("Success", player.UserIDString),
                    args[0].ToLower(), value));
                    
                    break;
                }
            }
        }
        
        private void cmdConsoleGatherRewards(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true)
            {
                return;
            }
            
            if (arg.Args == null)
            {
                Puts(string.Format(Lang("Usage"), config.Settings.ConsoleEditCommand));
                return;
            }
            
            if (arg.Args.Length <= 1)
            {
                Puts(string.Format(Lang("Usage"), config.Settings.ConsoleEditCommand));
                return;
            }
            
            float value = 0;
            if (float.TryParse(arg.Args[1], out value) == false)
            {
                Puts(Lang("NotaNumber"));
                return;
            }
            
            switch (arg.Args[0].ToLower())
            {
                case "clan":
                {
                    config.Rewards[PluginRewards.ClanMember] = value;
                    Config.WriteObject(config);
                    Puts(string.Format(Lang("Success"), "clan member", value));
                    break;
                }
                case "friend":
                {
                    config.Rewards[PluginRewards.PlayerFriend] = value;
                    Config.WriteObject(config);
                    Puts(string.Format(Lang("Success"), "friend", value));
                    break;
                }
                default:
                {
                    if (!config.Rewards.ContainsKey(UppercaseFirst(arg.Args[0].ToLower())))
                    {
                        Puts(string.Format(Lang("ValueDoesNotExist"), arg.Args[0].ToLower()));
                        break;
                    }
                    
                    config.Rewards[UppercaseFirst(arg.Args[0].ToLower())] = float.Parse(arg.Args[1]);
                    Config.WriteObject(config);
                    Puts(string.Format(Lang("Success"), arg.Args[0].ToLower(), value));
                    
                    break;
                }
            }
        }
        #endregion

        #region GatherRewards.Class.PluginSettings.cs
        private class PluginSettings
        {
            #region Properties and Indexers
            
            public bool AwardOnlyOnFullHarvest { get; set; } = false;
            
            public float ChainsawModifier { get; set; } = .25f;
            public string ChatEditCommand { get; set; }
            public string ConsoleEditCommand { get; set; }
            public string EditPermission { get; set; }
            
            public Dictionary<string, object> GroupModifiers { get; set; } = new Dictionary<string, object>
            {
                { "gatherrewards.vip1", 4.0 },
                { "gatherrewards.vip2", 3.0 },
                { "gatherrewards.vip3", 2.0 }
            };
            
            public float JackHammerModifier { get; set; } = .25f;
            public string PluginPrefix { get; set; }
            public bool ShowMessagesOnGather { get; set; }
            public bool ShowMessagesOnKill { get; set; }
            public bool UseEconomics { get; set; }
            public bool UseServerRewards { get; set; }
            
            #endregion
        }
        #endregion

        #region GatherRewards.Class.PluginRewards.cs
        private static class PluginRewards
        {
            public const string ClanMember = "Clan Member";
            public const string Corn = "Corn";
            public const string Hemp = "Hemp";
            public const string Mushroom = "Mushroom";
            public const string Ore = "Ore";
            public const string Player = "Player";
            public const string PlayerFriend = "Player's Friend";
            public const string Pumpkin = "Pumpkin";
            public const string Stone = "Stone";
            public const string TeamMember = "Team Member";
            public const string Wood = "Wood";
            public const string Suicide = "Suicide";
        }
        #endregion

        #region GatherRewards.Class.PluginConfig.cs
        private class PluginConfig
        {
            #region Properties and Indexers
            
            public Dictionary<string, float> Rewards { get; set; }
            public PluginSettings Settings { get; set; }
            
            #endregion
        }
        #endregion

    }

}
